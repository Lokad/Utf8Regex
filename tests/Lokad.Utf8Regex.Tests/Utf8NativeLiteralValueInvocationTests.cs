using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8NativeLiteralValueInvocationTests
{
    private static Utf8PreparedRegex Prepared(Utf8Regex regex) =>
        (Utf8PreparedRegex)typeof(Utf8Regex).GetProperty("PreparedRegex", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(regex)!;

    public static IEnumerable<object[]> InterruptedOperations()
    {
        foreach (var pattern in new[] { "target", "café", @"\btarget\b", "TARGET" })
        foreach (var compiled in new[] { false, true })
        foreach (var operation in new[] { "ismatch", "count", "match" })
        foreach (var validationPhase in new[] { false, true })
        foreach (var expire in new[] { false, true })
            yield return [pattern, compiled, operation, validationPhase, expire];
    }

    [Theory]
    [MemberData(nameof(InterruptedOperations))]
    public void PublicValuesPollDuringValidationAndSearchAndMapOriginalFailure(
        string pattern, bool compiled, string operation, bool validationPhase, bool expire)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (pattern == "TARGET" ? RegexOptions.IgnoreCase : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, expire ? TimeSpan.FromMilliseconds(200) : Regex.InfiniteMatchTimeout);
        Assert.Contains(Prepared(regex).ExecutionKind, new[] {
            NativeExecutionKind.ExactAsciiLiteral, NativeExecutionKind.ExactUtf8Literal, NativeExecutionKind.AsciiLiteralIgnoreCase });
        var input = Encoding.UTF8.GetBytes(new string('x', 320000));
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (entered || !validationPhase && steps <= validationSteps) return;
            entered = true;
            if (expire) Thread.Sleep(250);
            else source.Cancel();
        });
        try
        {
            void Run()
            {
                if (operation == "ismatch") _ = regex.IsMatch(input, source.Token);
                else if (operation == "count") _ = regex.Count(input, source.Token);
                else _ = regex.Match(input, source.Token);
            }
            if (expire)
            {
                var failure = Assert.Throws<RegexMatchTimeoutException>(Run);
                Assert.Equal(pattern, failure.Pattern);
                Assert.Equal(regex.MatchTimeout, failure.MatchTimeout);
                Assert.Equal(Encoding.UTF8.GetString(input), failure.Input);
            }
            else Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(Run).CancellationToken);
            Assert.True(entered);
            Assert.Equal(0, session.VerifierInvocations);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
        }
        finally { session.Complete(); }
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes(pattern == "TARGET" ? "target" : pattern == @"\btarget\b" ? "target" : pattern)));
    }

    public static IEnumerable<object[]> SemanticCases()
    {
        foreach (var pattern in new[] { "target", "café", "😀", @"\ba\b", @"a\B", @"target(?=!)", "TARGET", "(tar)(get)" })
        foreach (var compiled in new[] { false, true })
        foreach (var reverse in new[] { false, true }) yield return [pattern, compiled, reverse];
    }

    [Theory]
    [MemberData(nameof(SemanticCases))]
    public void PublicValuesAndCompleteOperationsKeepOracleCoordinatesAndOriginalNeighbors(string pattern, bool compiled, bool reverse)
    {
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (reverse ? RegexOptions.RightToLeft : RegexOptions.None) | (pattern == "TARGET" ? RegexOptions.IgnoreCase : RegexOptions.None);
        var regex = new Utf8Regex(pattern, options, TimeSpan.FromSeconds(10));
        var oracle = new Regex(pattern, options);
        string[] texts = ["", "é😀 target 中 café target! 😀 target 😀", "aaa a éa a aé", "TARGET target! xTarget"];
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, oracle, texts);
        using var source = new CancellationTokenSource();
        foreach (var text in texts)
        {
            var input = Encoding.UTF8.GetBytes(text);
            Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, source.Token));
            Assert.Equal(oracle.Count(text), regex.Count(input, source.Token));
            var expected = oracle.Match(text);
            var actual = regex.Match(input, source.Token);
            Assert.Equal(expected.Success, actual.Success);
            if (!expected.Success) continue;
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Length, actual.LengthInUtf16);
            Assert.Equal(Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)), actual.IndexInBytes);
            Assert.Equal(Encoding.UTF8.GetByteCount(expected.Value), actual.LengthInBytes);
        }
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void CountCutoffsIncludeValidationAndEveryMatchWithoutPublishingPartialTotal(bool reverse, bool folded)
    {
        var regex = new Utf8Regex("target", RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None));
        var plan = Prepared(regex).SearchPlan;
        var input = Encoding.UTF8.GetBytes("é target 中 TARGET 😀 target " + new string('x', 10000));
        var baseline = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 7), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref baseline);
        var validationSteps = baseline.Budget.Steps;
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(plan,
            input, reverse, ref baseline, out var count));
        Assert.Equal(folded ? 3 : 2, count);
        for (var cap = validationSteps; cap < baseline.Budget.Steps; cap++)
        {
            var invocation = new Utf8SliceInvocation(new(cap, 0, 7), Regex.InfiniteMatchTimeout, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            _ = Utf8Validation.Validate(input, ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(plan,
                input, reverse, ref invocation, out var denied));
            Assert.Equal(0, denied);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false, "ismatch")]
    [InlineData(true, "ismatch")]
    [InlineData(false, "count")]
    [InlineData(true, "count")]
    [InlineData(false, "match")]
    [InlineData(true, "match")]
    public void DeadlineOnlyRuntimeConsumersBorrowAlreadyExpiredCallerDeadline(bool reverse, string operation)
    {
        var regex = new Utf8Regex("target", RegexOptions.CultureInvariant);
        var plan = Prepared(regex);
        var runtime = new Utf8LiteralCompiledEngineRuntime(Utf8CompiledEngineSelector.Select(plan), plan, reverse);
        var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(1));
        Thread.Sleep(10);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => {
            if (operation == "ismatch") _ = runtime.IsMatch("target"u8, default, deadline);
            else if (operation == "count") _ = runtime.Count("target"u8, default, deadline);
            else _ = runtime.Match("target"u8, default, deadline);
        });
        Assert.True(runtime.IsMatch("target"u8, default, Utf8ExecutionDeadline.Infinite));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublicCountCancelsAfterSuccessfulMatchesBeforeItsTerminalMiss(bool compiled)
    {
        var regex = new Utf8Regex("target", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("target ", 1000)) + new string('x', 320000));
        var validation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        _ = Utf8Validation.Validate(input, ref validation);
        var validationSteps = validation.Budget.Steps;
        using var source = new CancellationTokenSource();
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (!entered && steps > validationSteps && Utf8SearchDiagnosticsSession.Current!.SearchCandidates >= 100)
            { entered = true; source.Cancel(); }
        });
        try
        {
            Assert.Equal(source.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, source.Token)).CancellationToken);
            Assert.True(entered);
            Assert.InRange(session.SearchCandidates, 100, 999);
            Assert.Equal(0, session.VerifierInvocations);
        }
        finally { session.Complete(); }
        Assert.Equal(1000, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void InvalidUtf8RetainsStrictFailureAndPreCanceledTokensWinAtEntry(bool compiled)
    {
        var regex = new Utf8Regex("target", RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None));
        byte[] input = [.. "target"u8, 0xF0, 0x80, 0x80, 0x80];
        using var live = new CancellationTokenSource();
        Assert.Throws<ArgumentException>(() => regex.IsMatch(input, live.Token));
        Assert.Throws<ArgumentException>(() => regex.Count(input, live.Token));
        Assert.Throws<ArgumentException>(() => regex.Match(input, live.Token));
        live.Cancel();
        Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.IsMatch(input, live.Token)).CancellationToken);
        Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Count(input, live.Token)).CancellationToken);
        Assert.Equal(live.Token, Assert.Throws<OperationCanceledException>(() => regex.Match(input, live.Token)).CancellationToken);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void RuntimeProjectionRetainsTheDeadlineUsedForDiscovery(bool reverse, bool folded)
    {
        var regex = new Utf8Regex("target", RegexOptions.CultureInvariant | (folded ? RegexOptions.IgnoreCase : RegexOptions.None));
        var plan = Prepared(regex);
        var input = Encoding.UTF8.GetBytes(new string('é', 160000) + "target");
        var search = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, default);
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SearchExecutor.TryFindNativeLiteralMatch(plan.SearchPlan, input,
            0, input.Length, reverse, ref search, out _, out var found));
        Assert.True(found);
        var searchSteps = search.Budget.Steps;
        var runtime = new Utf8LiteralCompiledEngineRuntime(Utf8CompiledEngineSelector.Select(plan), plan, reverse);
        var entered = false;
        var session = Utf8SearchDiagnosticsSession.Start(steps => {
            if (!entered && steps > searchSteps) { entered = true; Thread.Sleep(250); }
        });
        try
        {
            var deadline = Utf8ExecutionDeadline.Start(TimeSpan.FromMilliseconds(200));
            Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() => runtime.Match(input, default, deadline));
            Assert.True(entered);
        }
        finally { session.Complete(); }
    }

    public static IEnumerable<object[]> ExcludedCases()
    {
        foreach (var id in Enumerable.Range(0, 7))
        foreach (var compiled in new[] { false, true }) yield return [id, compiled];
    }

    [Theory]
    [MemberData(nameof(ExcludedCases))]
    public void SemanticExclusionsKeepUnitProgressionCapturesAndFolding(int id, bool compiled)
    {
        string[] patterns = ["", "\uD83D", "(?<x>t)(?<x>arget)", "(?<7>target)", "(?<x-00>a)", "(?i:k)", "(?-i:A)"];
        var options = RegexOptions.CultureInvariant | (compiled ? RegexOptions.Compiled : RegexOptions.None) |
            (id == 6 ? RegexOptions.IgnoreCase : RegexOptions.None);
        var regex = new Utf8Regex(patterns[id], options, TimeSpan.FromSeconds(10));
        var oracle = new Regex(patterns[id], options);
        const string text = "😀 target a K k A a";
        var input = Encoding.UTF8.GetBytes(text);
        using var source = new CancellationTokenSource();
        Assert.Equal(oracle.IsMatch(text), regex.IsMatch(input, source.Token));
        Assert.Equal(oracle.Count(text), regex.Count(input, source.Token));
        var expected = oracle.Match(text);
        var actual = regex.Match(input, source.Token);
        Assert.Equal(expected.Success, actual.Success);
        Assert.Equal(expected.Index, actual.IndexInUtf16);
        Assert.Equal(expected.Length, actual.LengthInUtf16);
        Utf8SliceSparseGroupTests.AssertOwnedOperations(regex, oracle, [text]);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }
}
