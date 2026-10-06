using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8DeterministicCursorInvocationTests
{
    private static readonly string[] Patterns = [@"ab[0-9]{2}", @"[A-Za-z]{8,13}", @"[0-9]{2,4}", @"^ab[0-9]d",
        "ab.cd", "a[^x]d", @"(?<n>ab[0-9]{2})", @"ab[0-9]{2}$", @"[0-9]{2}ab"];
    private static readonly string[] Texts = ["", "12 34 Ab Cd ab12 ab34 word:12 next:34",
        "abcdefgh ijklmnop ab12 ab34 12ab 34ab ab1cd ab2cd\nab1d\nab2d\naéd a😀d abéd ab😀cd\nab12\nab34\n",
        "é😀 12 Ab ab12 word:34 abcdefgh ijklmnop 12ab 34ab",
        new string('x', 8193) + " ab12 34 Ab word:56 abcdefgh 12ab"];
    private static Utf8PreparedRegex Prepared(Utf8Regex regex) =>
        (Utf8PreparedRegex)typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(regex)!;
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
    public void RangesPreserveAnchorsRunsCapturesMixedUnitsAndInvocationModes(string pattern, bool compiled, bool finite, bool live)
    {
        var regex = new Utf8Regex(pattern, Options(compiled), finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout);
        Assert.True(Prepared(regex).StructuralLinearProgram.DeterministicProgram.HasValue);
        var oracle = new Regex(pattern, Options(compiled));
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        {
            var input = Encoding.UTF8.GetBytes(text);
            var cursor = regex.EnumerateMatches(input, live ? source.Token : default);
            foreach (Match expected in oracle.Matches(text))
            {
                Assert.True(cursor.MoveNext());
                Assert.Equal((expected.Index, expected.Length), (cursor.Current.IndexInUtf16, cursor.Current.LengthInUtf16));
                var byteIndex = Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index));
                var byteLength = Encoding.UTF8.GetByteCount(expected.Value);
                if (cursor.Current.IsByteAligned) Assert.Equal((byteIndex, byteLength), (cursor.Current.IndexInBytes, cursor.Current.LengthInBytes));
            }
            var previous = cursor.Current;
            Assert.False(cursor.MoveNext());
            Assert.False(cursor.MoveNext());
            // Native deterministic terminal replay retains its last published range.
            if (Utf8Validation.Validate(input).IsAscii || Prepared(regex).StructuralLinearProgram.AllowsUtf8ByteSafe)
                Assert.Equal(previous, cursor.Current);
        }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        Assert.False(regex.Inspection.DebugAnchoredFallbackRegexIsCreated);
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
        var regex = new Utf8Regex(pattern, Options(compiled), TimeSpan.FromSeconds(10));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, Options(compiled)), Texts.Where(text => text.Length <= 128).ToArray());
    }

    [Theory]
    [InlineData(@"ab[0-9]{2}", false)]
    [InlineData(@"ab[0-9]{2}", true)]
    [InlineData(@"[0-9]{2,4}", false)]
    [InlineData(@"[0-9]{2,4}", true)]
    [InlineData(@"[0-9]{2}ab", true)]
    public void EverySecondMoveCutoffKeepsPublishedRangeScanProgressAndBorrowedStorage(string pattern, bool mixed)
    {
        var prepared = Prepared(new Utf8Regex(pattern, Options(false)));
        var input = Encoding.UTF8.GetBytes(mixed ? "é😀 ab12 12ab 中ab34 34ab" : "ab12 12ab ab34 34ab");
        var baselineInvocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var validation = Utf8Validation.Validate(input, ref baselineInvocation);
        var baseline = new Utf8OperationMatchCursor(input, prepared, baselineInvocation, validation.IsAscii);
        Assert.Equal(Utf8ExecutionLimitKind.None, baseline.TryMoveNextDeterministic(out var found));
        Assert.True(found);
        var firstSteps = baseline.DebugInvocationSteps;
        Assert.Equal(Utf8ExecutionLimitKind.None, baseline.TryMoveNextDeterministic(out found));
        Assert.True(found);
        for (var cap = firstSteps; cap < baseline.DebugInvocationSteps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            _ = Utf8Validation.Validate(input, ref invocation);
            var cursor = new Utf8OperationMatchCursor(input, prepared, invocation, validation.IsAscii);
            Assert.Equal(Utf8ExecutionLimitKind.None, cursor.TryMoveNextDeterministic(out found));
            Assert.True(found);
            var previous = cursor.CurrentValueMatch;
            var state = cursor.DebugDeterministicScanState;
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, cursor.TryMoveNextDeterministic(out var denied));
            Assert.False(denied);
            Assert.Equal(previous, cursor.CurrentValueMatch);
            Assert.Equal(state, cursor.DebugDeterministicScanState);
            Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
        }
    }

    [Theory]
    [MemberData(nameof(CompleteCases))]
    public void OffsetFactoryUsesFullSubjectAnchorsAndOriginalDeadline(string pattern, bool compiled)
    {
        var regex = new Utf8Regex(pattern, Options(compiled));
        var prepared = Prepared(regex);
        const string text = "ab12 12ab ab34 34ab abcdefgh ijklmnop\nab1d\nab2d\nab12\nab34\n";
        var input = Encoding.UTF8.GetBytes(text);
        var validation = Utf8Validation.Validate(input);
        var oracle = new Regex(pattern, Options(compiled));
        for (var floor = 0; floor <= text.Length; floor++)
        {
            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(prepared, null, input, validation,
                Utf8ExecutionDeadline.Infinite, floor, floor);
            foreach (Match expected in oracle.Matches(text, floor))
            {
                Assert.True(cursor.MoveNext());
                Assert.Equal((expected.Index, expected.Length), (cursor.CurrentValueMatch.IndexInUtf16, cursor.CurrentValueMatch.LengthInUtf16));
            }
            Assert.False(cursor.MoveNext());
        }
        var budget = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(200));
        var timed = Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(prepared, null, input, validation, budget, 0, 0);
        Thread.Sleep(250);
        Exception? observed = null;
        try { _ = timed.MoveNext(); } catch (Exception exception) { observed = exception; }
        Assert.IsType<Utf8ExecutionDeadlineExpiredException>(observed);
    }

    public static IEnumerable<object[]> PhaseCases()
    {
        foreach (var compiled in new[] { false, true })
        foreach (var phase in new[] { "validation", "search", "verifier", "projection" })
        foreach (var expire in new[] { false, true }) yield return [compiled, phase, expire];
    }

    [Theory]
    [MemberData(nameof(PhaseCases))]
    public void ValidationCandidateScanVerifierAndGapProjectionObserveInterruption(bool compiled, string phase, bool expire)
    {
        // Stay inside the existing speculative preparation budget. Sixty
        // repeated tokens still reach a checkpoint during this verifier.
        var pattern = phase == "verifier" ? @"ab[0-9]{60}" : @"ab[0-9]{2}";
        var text = phase == "verifier" ? "ab" + new string('1', 60) : phase == "projection" ? new string('é', 160000) + "ab12" : new string('x', 320000);
        var input = Encoding.UTF8.GetBytes(text);
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var regex = new Utf8Regex(pattern, Options(compiled), expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        Assert.True(Prepared(regex).StructuralLinearProgram.DeterministicProgram.HasValue);
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (entered || phase != "validation" && steps <= validation.Budget.Steps ||
                phase == "verifier" && Utf8SearchDiagnosticsSession.Current!.VerifierInvocations == 0 ||
                phase == "projection" && Utf8SearchDiagnosticsSession.Current!.VerifierMatches == 0) return;
            entered = true;
            if (expire) Thread.Sleep(250); else source.Cancel();
        });
        try
        {
            void Run() { var cursor = regex.EnumerateMatches(input, source.Token); _ = cursor.MoveNext(); }
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
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
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
        var text = (mixed ? "é😀 " : "") + Texts[2];
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        var regex = new Utf8Regex(pattern, Options(compiled), expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        var cursor = regex.EnumerateMatches(input, source.Token);
        Assert.True(cursor.MoveNext());
        var previous = cursor.Current;
        if (expire) Thread.Sleep(250); else source.Cancel();
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
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MalformedInputAndPreCanceledIdentityRemainStrict(bool compiled)
    {
        var regex = new Utf8Regex(Patterns[0], Options(compiled), TimeSpan.FromSeconds(10));
        using var source = new CancellationTokenSource();
        var malformed = "ab12"u8.ToArray().Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => { var cursor = regex.EnumerateMatches(malformed, source.Token); _ = cursor.MoveNext(); });
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => { var cursor = regex.EnumerateMatches(malformed, source.Token); _ = cursor.MoveNext(); }).CancellationToken);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static IEnumerable<object[]> TerminalCases()
    {
        foreach (var pattern in new[] { Patterns[0], Patterns[2] })
        foreach (var compiled in new[] { false, true })
        foreach (var matches in new[] { false, true })
        foreach (var expire in new[] { false, true }) yield return [pattern, compiled, matches, expire];
    }

    [Theory]
    [MemberData(nameof(TerminalCases))]
    public void ExhaustedNativeCursorKeepsConstantWorkAndChecksTokenAndDeadline(string pattern, bool compiled, bool matches, bool expire)
    {
        var input = Encoding.UTF8.GetBytes(matches ? "ab12 ab34" : new string('x', 32000));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var validation = Utf8Validation.Validate(input, ref invocation);
        var prepared = Prepared(new Utf8Regex(pattern, Options(compiled)));
        var cursor = new Utf8OperationMatchCursor(input, prepared, invocation, validation.IsAscii);
        while (cursor.MoveNext()) { }
        var previous = cursor.CurrentValueMatch;
        var state = cursor.DebugDeterministicScanState;
        var terminal = cursor.DebugInvocationSteps;
        for (var i = 0; i < 3; i++) Assert.False(cursor.MoveNext());
        Assert.Equal(3UL, cursor.DebugInvocationSteps - terminal);
        if (expire) Thread.Sleep(250); else source.Cancel();
        Exception? observed = null;
        try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(observed);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.Equal(previous, cursor.CurrentValueMatch);
        Assert.Equal(state, cursor.DebugDeterministicScanState);
        Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void EntryDeadlineIncludesIdleTimeBeforeFirstMove(bool compiled, bool mixed)
    {
        var input = Encoding.UTF8.GetBytes(mixed ? "é😀 ab12 ab34" : "ab12 ab34");
        var regex = new Utf8Regex(Patterns[0], Options(compiled), TimeSpan.FromMilliseconds(200));
        var cursor = regex.EnumerateMatches(input);
        Thread.Sleep(250);
        Exception? observed = null;
        try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
        var timeout = Assert.IsType<RegexMatchTimeoutException>(observed);
        Assert.Equal(regex.MatchTimeout, timeout.MatchTimeout);
        Assert.Equal(regex.Pattern, timeout.Pattern);
        Assert.Equal(Encoding.UTF8.GetString(input), timeout.Input);
        Assert.False(cursor.Current.Success);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
