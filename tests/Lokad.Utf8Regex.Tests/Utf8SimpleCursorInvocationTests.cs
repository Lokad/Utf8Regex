using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SimpleCursorInvocationTests
{
    private static readonly string[] Patterns = [@"ab[0-9]{1,2}d", @"(?:ab[0-9]d|xy[A-Z]z)", @"ab.[0-9]?d",
        @"(?:a[0-9]|ab[0-9])", @"(?:ab[0-9]{1,2}|ab[0-9]x)", @"(?:[A-Z]{2}|[0-9]{3})",
        @"^[A-Z][A-Za-z0-9_]{0,4}$", @"^(?:ab[0-9]d|xy[A-Z]z)$", @"(?<n>ab[0-9]{1,2}d)"];
    private static readonly string[] Texts = ["", "ab1d ab12d xyAz a1 ab2 ab12 ab1x ZZ 123 Abc",
        "ab1d ab12d xyAz a1 ab2 ab12 ab1x ZZ 123 Abc\nab1d\nxyBz\nAbc\nX9\nabéd ab😀d\n",
        "é😀 ab1d ab12d xyAz a1 ab2 ab12 ab1x ZZ 123 Abc",
        new string('x', 8193) + " ab1d ab12d xyAz a1 ab2 ZZ 123"];
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
        const string text = "ab1d ab12d xyAz a1 ab2 ZZ 123 Abc\nab1d\nxyBz\nAbc\nX9\n";
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
        var pattern = phase == "verifier" ? @"(?:ab[0-9]{60}|ab[A-Z])" : @"ab[0-9]{1,2}d";
        var text = phase == "verifier" ? "ab" + new string('1', 60) : phase == "projection" ? new string('é', 160000) + "ab12d" : new string('x', 320000);
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
    public void MalformedInputAndPreCanceledIdentityRemainStrict(bool compiled)
    {
        var regex = new Utf8Regex(Patterns[0], options: Options(compiled), policy: new Utf8RegexPolicy
        {
            MatchTimeout = TimeSpan.FromSeconds(10)
        });
        using var source = new CancellationTokenSource();
        var malformed = "ab12d"u8.ToArray().Concat(new byte[] { 0xff }).ToArray();
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => { var cursor = regex.EnumerateMatches(malformed, cancellationToken: source.Token); _ = cursor.MoveNext(); }).CancellationToken);
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
        var input = Encoding.UTF8.GetBytes(mixed ? "é😀 ab1d ab12d" : "ab1d ab12d");
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

    [Theory]
    [InlineData(@"ab[0-9](?=d)")]
    [InlineData(@"ab[0-9]{1,2}(?=d)")]
    [InlineData(@"(?:ab[0-9](?=d)|xy[A-Z](?=z))")]
    public void AssertionsKeepOwnedUnitSemanticsAcrossAllInvocationModes(string pattern)
    {
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        foreach (var live in new[] { false, true })
        {
            var regex = new Utf8Regex(pattern, options: Options(compiled), policy: new Utf8RegexPolicy
            {
                MatchTimeout = finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout
            });
            using var source = new CancellationTokenSource();
            foreach (var text in new[] { "ab1x ab12d ab2d xyAz xyBx", "é😀 ab1x ab12d ab2d xyAz xyBx" })
            {
                var cursor = regex.EnumerateMatches(Encoding.UTF8.GetBytes(text), cancellationToken: live ? source.Token : default);
                foreach (Match expected in new Regex(pattern, Options(compiled)).Matches(text))
                {
                    Assert.True(cursor.MoveNext());
                            Assert.Equal((System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), System.Text.Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length))), (cursor.Current.StartOffsetInBytes, (cursor.Current.EndOffsetInBytes - cursor.Current.StartOffsetInBytes)));
                        }
                Assert.False(cursor.MoveNext());
            }
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }
}
