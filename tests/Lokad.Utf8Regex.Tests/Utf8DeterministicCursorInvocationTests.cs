using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeterministicCursorInvocationTests
{
    private static readonly string[] Patterns = [@"ab[0-9]{2}", @"[A-Za-z]{8,13}", @"[0-9]{2,4}", @"^ab[0-9]d",
        "ab.cd", "a[^x]d", @"(?<n>ab[0-9]{2})", @"ab[0-9]{2}$", @"[0-9]{2}ab"];
    private static readonly string[] Texts = ["", "12 34 Ab Cd ab12 ab34 word:12 next:34",
        "abcdefgh ijklmnop ab12 ab34 12ab 34ab ab1cd ab2cd\nab1d\nab2d\naéd a😀d abéd ab😀cd\nab12\nab34\n",
        "é😀 12 Ab ab12 word:34 abcdefgh ijklmnop 12ab 34ab",
        new string('x', 8193) + " ab12 34 Ab word:56 abcdefgh 12ab"];
    private static RegexOptions Options(bool compiled) => RegexOptions.CultureInvariant | RegexOptions.Multiline |
        (compiled ? RegexOptions.Compiled : RegexOptions.None);

    public static IEnumerable<object[]> Modes()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        foreach (var live in new[] { false, true }) yield return [pattern, compiled, finite, live];
    }

    [Theory]
    [MemberData(nameof(Modes))]
    public void RangesPreserveAnchorsRunsCapturesMixedScalarsAndInvocationModes(string pattern, bool compiled, bool finite, bool live)
    {
        var regex = new Utf8Regex(pattern, options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout
        });
        var oracle = Utf8ScalarCaptureFixture.Project(new Regex(pattern, Options(compiled)));
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        {
            var subject = new Utf8ScalarCaptureFixture.Subject(text);
            var input = subject.Input;
            var cursor = regex.EnumerateMatches(input, cancellationToken: live ? source.Token : default);
            foreach (Match expected in oracle.Matches(subject.Projected))
            {
                Assert.True(cursor.MoveNext());
                Utf8SliceSparseGroupTests.AssertValue(expected, cursor.Current, subject);
            }
            var previous = cursor.Current;
            Assert.False(cursor.MoveNext());
            Assert.False(cursor.MoveNext());
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    public static IEnumerable<object[]> CompleteCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true }) yield return [pattern, compiled];
    }

    [Theory]
    [MemberData(nameof(CompleteCases))]
    public void FullOperationSemanticsRemainOwned(string pattern, bool compiled)
    {
        var regex = new Utf8Regex(pattern, options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(10)
        });
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, Options(compiled)), Texts.Where(text => text.Length <= 128).ToArray());
    }

    [Theory]
    [MemberData(nameof(CompleteCases))]
    public void OffsetEnumerationUsesFullSubjectAnchorsAndOriginalDeadline(string pattern, bool compiled)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        using var regex = new Utf8Regex(pattern, options: Options(compiled), policy: new()
        {
            MatchTimeout = TimeSpan.FromMilliseconds(200)
        });
        const string text = "ab12 12ab ab34 34ab abcdefgh ijklmnop\nab1d\nab2d\nab12\nab34\n";
        var input = Encoding.UTF8.GetBytes(text);
        var oracle = new Regex(pattern, Options(compiled));
        for (var floor = 0; floor <= text.Length; floor++)
        {
            var cursor = regex.EnumerateMatches(input, startOffsetInBytes: floor);
            foreach (Match expected in oracle.Matches(text, floor))
            {
                Assert.True(cursor.MoveNext());
                Assert.Equal((expected.Index, expected.Index + expected.Length),
                    (cursor.Current.StartOffsetInBytes, cursor.Current.EndOffsetInBytes));
            }
            Assert.False(cursor.MoveNext());
        }
        var timed = regex.EnumerateMatches(input, startOffsetInBytes: 0);
        clock.Advance(250);
        Exception? observed = null;
        try { _ = timed.MoveNext(); } catch (Exception exception) { observed = exception; }
        Assert.Equal(regex.MatchTimeout, Assert.IsType<RegexMatchTimeoutException>(observed).MatchTimeout);
    }

    public static IEnumerable<object[]> PhaseCases()
    {
        foreach (var compiled in new[] { false, true })
            foreach (var phase in new[] { "validation", "search", "verifier" })
                foreach (var expire in new[] { false, true }) yield return [compiled, phase, expire];
    }

    [Theory]
    [MemberData(nameof(PhaseCases))]
    public void MatchingAndFirstPublicationObserveOriginalInterruption(bool compiled, string phase, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        // Stay inside the existing speculative preparation budget. Sixty
        // repeated tokens still reach a checkpoint during this verifier.
        var pattern = phase == "verifier" ? @"ab[0-9]{60}" : @"ab[0-9]{2}";
        var text = phase == "verifier" ? "ab" + new string('1', 60) : phase == "projection" ? new string('é', 160000) + "ab12" : new string('x', 320000);
        var input = Encoding.UTF8.GetBytes(text);
        var regex = new Utf8Regex(pattern, options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout
        });
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (entered) return;
            entered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        });
        try
        {
            void Run() { var cursor = regex.EnumerateMatches(input, cancellationToken: source.Token); _ = cursor.MoveNext(); }
            if (expire)
            {
                var timeout = Assert.Throws<RegexMatchTimeoutException>(Run);
                Assert.Equal(pattern, timeout.Pattern);
                Assert.Equal(text, timeout.Input);
                Assert.Equal(regex.MatchTimeout, timeout.MatchTimeout);
            }
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(entered);
        }
        finally { session.Complete(); }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.Equal(new Regex(pattern, Options(compiled)).Count(text), regex.Count(input));
    }

    public static IEnumerable<object[]> LifetimeCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var mixed in new[] { false, true })
        foreach (var expire in new[] { false, true }) yield return [pattern, compiled, mixed, expire];
    }

    [Theory]
    [MemberData(nameof(LifetimeCases))]
    public void LaterMovesAndTerminalResultsKeepOriginalTokenAndDeadline(string pattern, bool compiled, bool mixed, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var text = (mixed ? "é😀 " : "") + Texts[2];
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex(pattern, options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout
        });
        var cursor = regex.EnumerateMatches(input, cancellationToken: source.Token);
        Assert.True(cursor.MoveNext());
        var previous = cursor.Current;
        if (expire) clock.Advance(250); else source.Cancel();
        Exception? observed = null;
        try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
        if (expire)
        {
            var timeout = Assert.IsType<RegexMatchTimeoutException>(observed);
            Assert.Equal(pattern, timeout.Pattern);
            Assert.Equal(text, timeout.Input);
            Assert.Equal(regex.MatchTimeout, timeout.MatchTimeout);
        }
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.Equal(previous, cursor.Current);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PreCanceledCursorKeepsTheOriginalToken(bool compiled)
    {
        var regex = new Utf8Regex(Patterns[0], options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(10)
        });
        using var source = new CancellationTokenSource();
        var input = "ab12"u8.ToArray();
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => { var cursor = regex.EnumerateMatches(input, cancellationToken: source.Token); _ = cursor.MoveNext(); }).CancellationToken);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EntryDeadlineIncludesIdleTimeBeforeFirstMove(bool compiled, bool mixed)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var input = Encoding.UTF8.GetBytes(mixed ? "é😀 ab12 ab34" : "ab12 ab34");
        var regex = new Utf8Regex(Patterns[0], options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromMilliseconds(200)
        });
        var cursor = regex.EnumerateMatches(input);
        clock.Advance(250);
        Exception? observed = null;
        try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
        var timeout = Assert.IsType<RegexMatchTimeoutException>(observed);
        Assert.Equal(regex.MatchTimeout, timeout.MatchTimeout);
        Assert.Equal(regex.Pattern, timeout.Pattern);
        Assert.Equal(Encoding.UTF8.GetString(input), timeout.Input);
        Assert.False(cursor.Current.Success);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
