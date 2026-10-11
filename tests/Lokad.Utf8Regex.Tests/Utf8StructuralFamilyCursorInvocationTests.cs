using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8StructuralFamilyCursorInvocationTests
{
    private static Utf8PreparedRegex Prepared(Utf8Regex regex) =>
        (Utf8PreparedRegex)typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(regex)!;

    private static readonly string[] Patterns = [@"(?:get|set)[A-Z][A-Za-z0-9_]*", @"\b(?:get|set)[A-Z][A-Za-z0-9_]*\b",
        @"\b(?:LogError|LogWarning|LogInformation)\s*\(", @"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+",
        @"(?:get|set)[A-Z][A-Za-z0-9_]*\s*\(", @"\b(?:LogTrace|LogDebug|LogInformation|LogWarning|LogError)\b",
        @"(?:foo|foobar)[A-Z][A-Za-z0-9_]*", @"(?:foo|foob)[A-Z][A-Za-z0-9_]*"];
    private static readonly string[] Texts = ["", "xgetAlpha getOmega setBeta getlower setGamma(" ,
        "LogError ( skip xLogWarning( LogInformation\t( LogError", "class Order struct Worker record Customer class lower",
        "fooEnd foobarAlpha foobBeta fooLower foobarZ", "é😀 getAlpha setBeta LogError\u2003( class\u2003Order foobarAlpha",
        new string('x', 8193) + " getAlpha LogError\t( class Order", "getAlpha setBeta\nLogError ( LogWarning(\nclass Order struct Worker fooAlpha foobarBeta foobGamma"];

    public static IEnumerable<object[]> ValueCases()
    {
        foreach (var pattern in Patterns)
        foreach (var compiled in new[] { false, true })
        foreach (var finite in new[] { false, true })
        foreach (var live in new[] { false, true }) yield return [pattern, compiled, finite, live];
    }

    [Theory]
    [MemberData(nameof(ValueCases))]
    public void GlobalRangesPreservePrefixPriorityUnicodeAndEveryInvocationMode(string pattern, bool compiled, bool finite, bool live)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout);
        Assert.Equal(NativeExecutionKind.AsciiStructuralIdentifierFamily, regex.Inspection.ExecutionKind);
        var oracle = new Regex(pattern, options);
        using var source = new CancellationTokenSource();
        foreach (var text in Texts)
        {
            var input = Encoding.UTF8.GetBytes(text);
            var cursor = regex.EnumerateMatches(input, live ? source.Token : default);
            foreach (Match expected in oracle.Matches(text))
            {
                Assert.True(cursor.MoveNext());
                Assert.Equal((expected.Index, expected.Length), (cursor.Current.IndexInUtf16, cursor.Current.LengthInUtf16));
                Assert.Equal(expected.Value, Encoding.UTF8.GetString(input.AsSpan(cursor.Current.IndexInBytes, cursor.Current.LengthInBytes)));
            }
            Assert.False(cursor.MoveNext());
            Assert.False(cursor.MoveNext());
            Assert.Equal(oracle.Count(text), regex.Count(input, live ? source.Token : default));
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, live ? source.Token : default));
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
    }

    public static IEnumerable<object[]> CompleteCases()
    {
        foreach (var pattern in Patterns.Take(6).Concat(new[] { @"(?<method>LogError|LogWarning)\s*\(", @"(?<verb>get|set)[A-Z][A-Za-z0-9_]*" }))
        foreach (var compiled in new[] { false, true }) yield return [pattern, compiled];
    }

    [Theory]
    [MemberData(nameof(CompleteCases))]
    public void CompleteOperationsRetainCapturesOffsetsSplitAndReplacement(string pattern, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        // This helper checks every offset. Large scans have separate global
        // range and interruption cases rather than an exhaustive offset grid.
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, new Regex(pattern, options), Texts.Where(text => text.Length <= 128).ToArray());
    }

    [Theory]
    [InlineData(@"(?:get|set)[A-Z][A-Za-z0-9_]*")]
    [InlineData(@"\b(?:get|set)[A-Z][A-Za-z0-9_]*\b")]
    [InlineData(@"\b(?:LogError|LogWarning|LogInformation)\s*\(")]
    [InlineData(@"\b(?:record|struct|class)\s+[A-Z][A-Za-z0-9_]+")]
    public void EverySecondMoveCutoffRetainsPreviousRangeCandidateProgressAndBorrowedLease(string pattern)
    {
        var prepared = Prepared(new Utf8Regex(pattern, RegexOptions.CultureInvariant));
        var input = Encoding.UTF8.GetBytes(Texts[^1]);
        var baselineInvocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref baselineInvocation);
        var baseline = new Utf8OperationMatchCursor(input, prepared, baselineInvocation);
        Assert.Equal(Utf8ExecutionLimitKind.None, baseline.TryMoveNextStructuralFamily(out var found));
        Assert.True(found);
        var firstSteps = baseline.DebugInvocationSteps;
        Assert.Equal(Utf8ExecutionLimitKind.None, baseline.TryMoveNextStructuralFamily(out found));
        Assert.True(found);
        for (var cap = firstSteps; cap < baseline.DebugInvocationSteps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            _ = Utf8Validation.Validate(input, ref invocation);
            var cursor = new Utf8OperationMatchCursor(input, prepared, invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, cursor.TryMoveNextStructuralFamily(out found));
            Assert.True(found);
            var previous = cursor.CurrentValueMatch;
            var previousState = cursor.DebugStructuralSearchState;
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, cursor.TryMoveNextStructuralFamily(out var denied));
            Assert.False(denied);
            Assert.Equal(previous, cursor.CurrentValueMatch);
            Assert.Equal(previousState, cursor.DebugStructuralSearchState);
            Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
        }
    }

    public static IEnumerable<object[]> OffsetCases()
    {
        foreach (var pattern in Patterns.Take(4))
        foreach (var compiled in new[] { false, true }) yield return [pattern, compiled];
    }

    [Theory]
    [MemberData(nameof(OffsetCases))]
    public void FactoryOffsetsBorrowOwnedPlansAndKeepFullSubjectBoundaryContext(string pattern, bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options);
        var prepared = Prepared(regex);
        var oracle = new Regex(pattern, options);
        const string text = "xgetAlpha getOmega setBeta LogError ( xLogWarning( LogInformation( class Order struct Worker";
        var input = Encoding.UTF8.GetBytes(text);
        var validation = Utf8Validation.Validate(input);
        for (var floor = 0; floor <= text.Length; floor++)
        {
            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(prepared, input,
                validation, Utf8ExecutionDeadline.Infinite, floor, floor);
            foreach (Match expected in oracle.Matches(text, floor))
            {
                Assert.True(cursor.MoveNext());
                Assert.Equal((expected.Index, expected.Length), (cursor.CurrentValueMatch.IndexInBytes, cursor.CurrentValueMatch.LengthInBytes));
            }
            Assert.False(cursor.MoveNext());
        }
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    public static IEnumerable<object[]> PhaseCases()
    {
        foreach (var method in new[] { false, true })
        foreach (var compiled in new[] { false, true })
        foreach (var phase in new[] { "validation", "search", "verifier" })
        foreach (var expire in new[] { false, true }) yield return [method, compiled, phase, expire];
    }

    [Theory]
    [MemberData(nameof(PhaseCases))]
    public void EnumerationEntryAndOneLongMoveCarryValidationSearchAndVerifierInterruption(bool method, bool compiled, string phase, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var pattern = method ? Patterns[2] : Patterns[0];
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        var text = phase == "verifier" ? (method ? "LogError" + new string(' ', 160000) + "(" : "get" + new string('A', 160000))
            : new string('x', 320000);
        var input = Encoding.UTF8.GetBytes(text);
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (entered || phase == "search" && steps <= validation.Budget.Steps ||
                phase == "verifier" && Utf8SearchDiagnosticsSession.Current!.VerifierInvocations == 0) return;
            if (phase == "validation") Assert.True(steps <= validation.Budget.Steps);
            entered = true;
            if (expire) clock.Advance(250); else source.Cancel();
        });
        try
        {
            void Run() { var cursor = regex.EnumerateMatches(input, source.Token); _ = cursor.MoveNext(); }
            if (expire)
            {
                var exception = Assert.Throws<RegexMatchTimeoutException>(Run);
                Assert.Equal(pattern, exception.Pattern);
                Assert.Equal(text, exception.Input);
                Assert.Equal(regex.MatchTimeout, exception.MatchTimeout);
            }
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(entered);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
        finally { session.Complete(); }
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(method ? "LogError(" : "getAlpha")));
    }

    public static IEnumerable<object[]> LifetimeCases()
    {
        foreach (var pattern in Patterns.Take(4).Concat(Patterns.Skip(6)))
        foreach (var compiled in new[] { false, true })
        foreach (var mixed in new[] { false, true })
        foreach (var expire in new[] { false, true }) yield return [pattern, compiled, mixed, expire];
    }

    [Theory]
    [MemberData(nameof(LifetimeCases))]
    public void InterruptionBetweenAcceptedMatchesRetainsOriginalDeadlineTokenAndCurrent(string pattern, bool compiled, bool mixed, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        var text = mixed ? "é😀 " + Texts[^1] : Texts[^1];
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        var cursor = regex.EnumerateMatches(input, source.Token);
        Assert.True(cursor.MoveNext());
        var previous = cursor.Current;
        if (expire) clock.Advance(250); else source.Cancel();
        Exception? observed = null;
        try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
        if (expire) Assert.IsType<RegexMatchTimeoutException>(observed);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.Equal(previous, cursor.Current);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        Assert.Equal(new Regex(pattern, options).Count(text), regex.Count(input));
    }

    public static IEnumerable<object[]> TerminalCases()
    {
        foreach (var compiled in new[] { false, true })
        foreach (var matches in new[] { false, true })
        foreach (var expire in new[] { false, true }) yield return [compiled, matches, expire];
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WholeSubjectValidationAndPreCanceledIdentitySurviveNativeAdmission(bool compiled)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None);
        using var source = new CancellationTokenSource();
        foreach (var finite in new[] { false, true })
        foreach (var input in new[] { new byte[] { 0xff }, new byte[] { (byte)'x', 0xc0, 0xaf }, new byte[] { 0xf0, 0x9f } })
        {
            var regex = new Utf8Regex(Patterns[0], options, finite ? TimeSpan.FromSeconds(10) : Regex.InfiniteMatchTimeout);
            var malformed = "getAlpha "u8.ToArray().Concat(input).ToArray();
            Assert.Throws<ArgumentException>(() => { var cursor = regex.EnumerateMatches(malformed); _ = cursor.MoveNext(); });
            Assert.Throws<ArgumentException>(() => { var cursor = regex.EnumerateMatches(malformed, source.Token); _ = cursor.MoveNext(); });
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
        source.Cancel();
        var canceled = new Utf8Regex(Patterns[0], options);
        Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => { var cursor = canceled.EnumerateMatches(new byte[] { 0xff }, source.Token); _ = cursor.MoveNext(); }).CancellationToken);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void TimeBeforeFirstMoveCountsAgainstTheOriginalEntryDeadline(bool compiled, bool mixed)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var regex = new Utf8Regex(Patterns[0], RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None), TimeSpan.FromMilliseconds(200));
        var text = mixed ? "é😀 getAlpha setBeta" : "getAlpha setBeta";
        var input = Encoding.UTF8.GetBytes(text);
        var cursor = regex.EnumerateMatches(input);
        clock.Advance(250);
        Exception? observed = null;
        try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
        var timeout = Assert.IsType<RegexMatchTimeoutException>(observed);
        Assert.Equal(Patterns[0], timeout.Pattern);
        Assert.Equal(text, timeout.Input);
        Assert.Equal(regex.MatchTimeout, timeout.MatchTimeout);
        Assert.False(cursor.Current.Success);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
    }

    [Theory]
    [MemberData(nameof(TerminalCases))]
    public void TerminalCursorReplayUsesConstantWorkAndChecksOriginalTokenAndDeadline(bool compiled, bool matches, bool expire)
    {
        using var clock = Utf8DeadlineTestClock.Start();
        var regex = new Utf8Regex(Patterns[0], RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var input = Encoding.UTF8.GetBytes(matches ? "getAlpha setBeta" : new string('x', 32000));
        using var source = new CancellationTokenSource();
        var deadline = expire ? Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(200)) : Utf8ExecutionDeadline.Infinite;
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), deadline, source.Token);
        Assert.True(invocation.Ledger.TryReserve(7));
        var cursor = new Utf8OperationMatchCursor(input, Prepared(regex), invocation);
        while (cursor.MoveNext()) { }
        var previous = cursor.CurrentValueMatch;
        var terminal = cursor.DebugInvocationSteps;
        for (var i = 0; i < 3; i++) Assert.False(cursor.MoveNext());
        Assert.InRange(cursor.DebugInvocationSteps - terminal, 3UL, 6UL);
        if (expire) clock.Advance(250); else source.Cancel();
        Exception? observed = null;
        try { _ = cursor.MoveNext(); } catch (Exception exception) { observed = exception; }
        if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(observed);
        else Assert.Equal(source.Token, Assert.IsType<OperationCanceledException>(observed).CancellationToken);
        Assert.Equal(previous, cursor.CurrentValueMatch);
        Assert.Equal(7UL, cursor.DebugInvocationReservedBytes);
    }
}
