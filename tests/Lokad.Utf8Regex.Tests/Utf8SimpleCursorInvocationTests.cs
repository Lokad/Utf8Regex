using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

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
    public void RangesPreserveAnchorsRunsCapturesMixedScalarsAndInvocationModes(string pattern, bool compiled, bool finite, bool live)
    {
        var regex = new Utf8Regex(pattern, Options(compiled), finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout);
        Assert.False(Prepared(regex).StructuralLinearProgram.DeterministicProgram.HasValue);
        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, Prepared(regex).ExecutionKind);
        var oracle = Utf8ScalarCaptureFixture.Project(new Regex(pattern, Options(compiled)));
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        {
            var subject = new Utf8ScalarCaptureFixture.Subject(text);
            var input = subject.Input;
            var cursor = regex.EnumerateMatches(input, live ? source.Token : default);
            foreach (Match expected in oracle.Matches(subject.Projected))
            {
                Assert.True(cursor.MoveNext());
                Utf8SliceSparseGroupTests.AssertValue(expected, cursor.Current, subject);
            }
            var previous = cursor.Current;
            Assert.False(cursor.MoveNext());
            Assert.False(cursor.MoveNext());
            // Native simple terminal replay retains its last published range.
            if (Utf8Validation.Validate(input).IsAscii || Prepared(regex).SimplePatternPlan.IsUtf8ByteSafe)
                Assert.Equal(previous, cursor.Current);
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
        var regex = new Utf8Regex(pattern, Options(compiled), TimeSpan.FromSeconds(10));
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, Options(compiled)), Texts.Where(text => text.Length <= 128).ToArray());
    }

    [Theory]
    [InlineData(@"ab[0-9]{1,2}d", false)]
    [InlineData(@"ab[0-9]{1,2}d", true)]
    [InlineData(@"(?:a[0-9]|ab[0-9])", false)]
    [InlineData(@"(?:a[0-9]|ab[0-9])", true)]
    [InlineData(@"(?:[A-Z]{2}|[0-9]{3})", true)]
    public void EverySecondMoveCutoffKeepsPublishedRangeScanProgressAndBorrowedStorage(string pattern, bool mixed)
    {
        var prepared = Prepared(new Utf8Regex(pattern, Options(false)));
        var input = Encoding.UTF8.GetBytes(mixed ? "é😀 ab1d AB xyAz 中ab12d CD xyBz 123 456" : "ab1d AB xyAz ab12d CD xyBz 123 456");
        var baselineInvocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        var validation = Utf8Validation.Validate(input, ref baselineInvocation);
        var baseline = new Utf8OperationMatchCursor(input, prepared, baselineInvocation, validation.IsAscii);
        Assert.Equal(Utf8ExecutionLimitKind.None, baseline.TryMoveNextSimplePattern(out var found));
        Assert.True(found);
        var firstSteps = baseline.DebugInvocationSteps;
        Assert.Equal(Utf8ExecutionLimitKind.None, baseline.TryMoveNextSimplePattern(out found));
        Assert.True(found);
        for (var cap = firstSteps; cap < baseline.DebugInvocationSteps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            _ = Utf8Validation.Validate(input, ref invocation);
            var cursor = new Utf8OperationMatchCursor(input, prepared, invocation, validation.IsAscii);
            Assert.Equal(Utf8ExecutionLimitKind.None, cursor.TryMoveNextSimplePattern(out found));
            Assert.True(found);
            var previous = cursor.CurrentValueMatch;
            var state = cursor.DebugConsumed;
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, cursor.TryMoveNextSimplePattern(out var denied));
            Assert.False(denied);
            Assert.Equal(previous, cursor.CurrentValueMatch);
            Assert.Equal(state, cursor.DebugConsumed);
            Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
        }
    }

    [Theory]
    [MemberData(nameof(CompleteCases))]
    public void OffsetFactoryUsesFullSubjectAnchorsAndOriginalDeadline(string pattern, bool compiled)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var regex = new Utf8Regex(pattern, Options(compiled));
        var prepared = Prepared(regex);
        const string text = "ab1d ab12d xyAz a1 ab2 ZZ 123 Abc\nab1d\nxyBz\nAbc\nX9\n";
        var input = Encoding.UTF8.GetBytes(text);
        var validation = Utf8Validation.Validate(input);
        var oracle = new Regex(pattern, Options(compiled));
        for (var floor = 0; floor <= text.Length; floor++)
        {
            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(prepared, input, validation,
                Utf8ExecutionDeadline.Infinite, floor, floor);
            foreach (Match expected in oracle.Matches(text, floor))
            {
                Assert.True(cursor.MoveNext());
                Assert.Equal((expected.Index, expected.Length), (cursor.CurrentValueMatch.IndexInUtf16, cursor.CurrentValueMatch.LengthInUtf16));
            }
            Assert.False(cursor.MoveNext());
        }
        var budget = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(200));
        var timed = Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(prepared, input, validation, budget, 0, 0);
        clock.Advance(250);
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
        using var clock = Utf8DeadlineTestClock.Start();
        // Stay inside the existing speculative preparation budget. Sixty
        // repeated tokens still reach a checkpoint during this verifier.
        var pattern = phase == "verifier" ? @"(?:ab[0-9]{60}|ab[A-Z])" : @"ab[0-9]{1,2}d";
        var text = phase == "verifier" ? "ab" + new string('1', 60) : phase == "projection" ? new string('é', 160000) + "ab12d" : new string('x', 320000);
        var input = Encoding.UTF8.GetBytes(text);
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var regex = new Utf8Regex(pattern, Options(compiled), expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        Assert.False(Prepared(regex).StructuralLinearProgram.DeterministicProgram.HasValue);
        Assert.Equal(NativeExecutionKind.AsciiSimplePattern, Prepared(regex).ExecutionKind);
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (entered || phase != "validation" && steps <= validation.Budget.Steps ||
                phase == "verifier" && Utf8SearchDiagnosticsSession.Current!.VerifierInvocations == 0 ||
                phase == "projection" && Utf8SearchDiagnosticsSession.Current!.VerifierMatches == 0) return;
            entered = true;
            if (expire) clock.Advance(250); else source.Cancel();
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
        var regex = new Utf8Regex(pattern, Options(compiled), expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        var cursor = regex.EnumerateMatches(input, source.Token);
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
        var regex = new Utf8Regex(Patterns[0], Options(compiled), TimeSpan.FromSeconds(10));
        using var source = new CancellationTokenSource();
        var malformed = "ab12d"u8.ToArray().Concat(new byte[] { 0xff }).ToArray();
        Assert.Throws<ArgumentException>(() => { var cursor = regex.EnumerateMatches(malformed, source.Token); _ = cursor.MoveNext(); });
        source.Cancel();
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => { var cursor = regex.EnumerateMatches(malformed, source.Token); _ = cursor.MoveNext(); }).CancellationToken);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static IEnumerable<object[]> TerminalCases()
    {
        foreach (var pattern in new[] { Patterns[0], Patterns[5] })
        foreach (var compiled in new[] { false, true })
        foreach (var matches in new[] { false, true })
        foreach (var expire in new[] { false, true }) yield return [pattern, compiled, matches, expire];
    }

    [Theory]
    [MemberData(nameof(TerminalCases))]
    public void ExhaustedNativeCursorKeepsConstantWorkAndChecksTokenAndDeadline(string pattern, bool compiled, bool matches, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var input = Encoding.UTF8.GetBytes(matches ? "ab1d AB ab12d CD" : new string('x', 32000));
        // Preparation precedes invocation in the public API. Its construction
        // work must not consume this terminal-cursor control's execution budget.
        var prepared = Prepared(new Utf8Regex(pattern, Options(compiled)));
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7),
            expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var validation = Utf8Validation.Validate(input, ref invocation);
        var cursor = new Utf8OperationMatchCursor(input, prepared, invocation, validation.IsAscii);
        while (cursor.MoveNext()) { }
        var previous = cursor.CurrentValueMatch;
        var state = cursor.DebugConsumed;
        var terminal = cursor.DebugInvocationSteps;
        for (var i = 0; i < 3; i++) Assert.False(cursor.MoveNext());
        Assert.Equal(3UL, cursor.DebugInvocationSteps - terminal);
        if (expire) clock.Advance(250); else source.Cancel();
        Exception? observed = null;
        try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(observed);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.Equal(previous, cursor.CurrentValueMatch);
        Assert.Equal(state, cursor.DebugConsumed);
        Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
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
        var regex = new Utf8Regex(Patterns[0], Options(compiled), TimeSpan.FromMilliseconds(200));
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
            var regex = new Utf8Regex(pattern, Options(compiled), finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout);
            using var source = new CancellationTokenSource();
            foreach (var text in new[] { "ab1x ab12d ab2d xyAz xyBx", "é😀 ab1x ab12d ab2d xyAz xyBx" })
            {
                var cursor = regex.EnumerateMatches(Encoding.UTF8.GetBytes(text), live ? source.Token : default);
                foreach (Match expected in new Regex(pattern, Options(compiled)).Matches(text))
                {
                    Assert.True(cursor.MoveNext());
                    Assert.Equal((expected.Index, expected.Length), (cursor.Current.IndexInUtf16, cursor.Current.LengthInUtf16));
                }
                Assert.False(cursor.MoveNext());
            }
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    [Theory]
    [InlineData("ab[0-9]CD", false)]
    [InlineData("ab[0-9]CD", true)]
    [InlineData("ab[0-9]{1,2}CD", false)]
    [InlineData("ab[0-9]{1,2}CD", true)]
    public void BoundedInterpreterPreservesInvariantFoldingFixedChecksAndCumulativeWork(string pattern, bool compiled)
    {
        var options = Options(compiled) | RegexOptions.IgnoreCase;
        var regex = new Utf8Regex(pattern, options);
        var prepared = Prepared(regex);
        const string text = "AB1cd ab12CD Ab3Cd AB4xx ab5CD";
        var input = Encoding.UTF8.GetBytes(text);
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 1, 7), Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(7));
        var floor = 0;
        foreach (Match expected in new Regex(pattern, options).Matches(text))
        {
            var previous = invocation.Budget.Steps;
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8ExecutionInterpreter.TryFindNextSimplePattern(input,
                prepared.SearchPlan, prepared.SimplePatternPlan, floor, ref invocation,
                out var index, out var length));
            Assert.Equal((expected.Index, expected.Length), (index, length));
            Assert.True(invocation.Budget.Steps > previous);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            floor = index + length;
        }
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8ExecutionInterpreter.TryFindNextSimplePattern(input,
            prepared.SearchPlan, prepared.SimplePatternPlan, floor, ref invocation,
            out var terminalIndex, out var terminalLength));
        Assert.Equal(-1, terminalIndex);
        Assert.Equal(0, terminalLength);
        Assert.Equal(1U, invocation.Budget.PeakDepth);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }
}
