using System.Runtime.CompilerServices;
using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Collection(Pcre2AllocationTestCollection.Name)]
public sealed class Pcre2SharedBudgetTests
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_invocation")]
    private static extern ref Utf8SliceInvocation Invocation(ref Pcre2ResourceBudget budget);

    [Theory]
    [InlineData("a", "aaaa", Pcre2MatchOptions.None, 1u, false)]
    [InlineData("a", "aaaa", Pcre2MatchOptions.None, 1u, true)]
    [InlineData("[ab]", "abab", Pcre2MatchOptions.None, 1u, false)]
    [InlineData("[ab]", "abab", Pcre2MatchOptions.None, 1u, true)]
    [InlineData("(a+)+b", "aaab aaab", Pcre2MatchOptions.NotBol, 27u, false)]
    [InlineData("(a+)+b", "aaab aaab", Pcre2MatchOptions.NotBol, 27u, true)]
    [InlineData("foo(?<Bar>BAR)?", "fooBAR fooBAR", Pcre2MatchOptions.None, 17u, false)]
    [InlineData("foo(?<Bar>BAR)?", "fooBAR fooBAR", Pcre2MatchOptions.None, 17u, true)]
    public void PublicCopiedCursorsCannotForkTheirFiniteFirstMatchAllowance(string pattern, string input,
        Pcre2MatchOptions options, uint steps, bool afterFirst)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = steps }, Timeout.InfiniteTimeSpan);
        var bytes = Encoding.UTF8.GetBytes(input);
        var original = regex.EnumerateMatches(bytes, 0, options);
        var alias = original;
        Assert.True(original.MoveNext());
        var retained = original.Current;
        if (afterFirst) alias = original;
        Pcre2MatchException? failure = null;
        try { alias.MoveNext(); }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(0, retained.StartOffsetInBytes);
        Assert.Equal(bytes.AsSpan(0, retained.EndOffsetInBytes).ToArray(), retained.GetValueBytes().ToArray());
        Assert.Equal(retained.EndOffsetInBytes, original.Current.EndOffsetInBytes);
        failure = null;
        try { original.MoveNext(); }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        var fresh = regex.EnumerateMatches(bytes, 0, options);
        Assert.True(fresh.MoveNext());
        Assert.Equal(retained.EndOffsetInBytes, fresh.Current.EndOffsetInBytes);
    }

    [Theory]
    [InlineData("a", "aaaa")]
    [InlineData("[ab]", "abab")]
    public void PublicAliasesKeepIndependentPositionsWithOneWorkAllowance(string pattern, string input)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default,
            new() { MatchLimit = 3 }, Timeout.InfiniteTimeSpan);
        var original = regex.EnumerateMatches(Encoding.UTF8.GetBytes(input));
        Assert.True(original.MoveNext());
        var first = original.Current;
        var alias = original;
        Assert.True(original.MoveNext()); Assert.Equal(1, original.Current.StartOffsetInBytes);
        Assert.Equal(0, alias.Current.StartOffsetInBytes);
        Assert.True(alias.MoveNext()); Assert.Equal(1, alias.Current.StartOffsetInBytes);
        Pcre2MatchException? failure = null;
        try { original.MoveNext(); }
        catch (Pcre2MatchException error) { failure = error; }
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.IsType<Pcre2MatchException>(failure).ErrorKind);
        Assert.Equal(0, first.StartOffsetInBytes); Assert.Equal(1, first.EndOffsetInBytes);
        Assert.Equal(1, alias.Current.StartOffsetInBytes);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void BudgetCopiesShareTheFirstWorkDepthOrHeapDenial(int kind)
    {
        var original = new Pcre2ResourceBudget(new() { MatchLimit = kind == 0 ? 1u : 0u,
            DepthLimit = kind == 1 ? 1u : 0u, HeapLimitInBytes = kind == 2 ? 128UL : 0UL }, Timeout.InfiniteTimeSpan, false);
        var alias = original;
        var expected = kind == 0 ? Pcre2ErrorKind.MatchLimit : kind == 1 ? Pcre2ErrorKind.DepthLimit : Pcre2ErrorKind.HeapLimit;
        if (kind == 0) { original.ChargeCandidate(); Assert.Equal(expected, Assert.Throws<Pcre2MatchException>(() => alias.ChargeCandidate()).ErrorKind); }
        else if (kind == 1) Assert.Equal(expected, Assert.Throws<Pcre2MatchException>(() => original.ChargeFrame(2)).ErrorKind);
        else
        {
            original.ReserveWorking(100);
            Assert.Equal(100UL, alias.ReservedWorkingBytes);
            Assert.Equal(expected, Assert.Throws<Pcre2MatchException>(() => alias.ReserveWorking(29)).ErrorKind);
            Assert.Equal(129UL, original.HeapBytes);
            Assert.Equal(100UL, original.ReservedWorkingBytes);
            alias.ReleaseWorking(100);
            Assert.Equal(0UL, original.ReservedWorkingBytes);
        }
        Assert.Equal(expected, Assert.Throws<Pcre2MatchException>(() => original.ChargeWorkspaceStep()).ErrorKind);
        Assert.Equal(expected, Assert.Throws<Pcre2MatchException>(() => alias.ChargeFrame(1)).ErrorKind);
        Assert.Equal(expected, Assert.Throws<Pcre2MatchException>(() => alias.ReserveWorking(1)).ErrorKind);
        Assert.Equal(Invocation(ref original).Budget.LimitBreach, Invocation(ref alias).Budget.LimitBreach);
    }

    [Fact]
    public void AggregateOverflowSaturatesAndRefusesWithoutChangingReservations()
    {
        var original = new Pcre2ResourceBudget(new() { HeapLimitInBytes = ulong.MaxValue }, Timeout.InfiniteTimeSpan, false);
        original.ReserveWorking(ulong.MaxValue - 4);
        var alias = original;
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() => alias.ReserveWorking(8)).ErrorKind);
        Assert.Equal(ulong.MaxValue, original.HeapBytes);
        Assert.Equal(ulong.MaxValue - 4, alias.ReservedWorkingBytes);
        alias.ReleaseWorking(ulong.MaxValue - 4);
        Assert.Equal(0UL, original.ReservedWorkingBytes);
        Assert.Equal(Pcre2ErrorKind.HeapLimit, Assert.Throws<Pcre2MatchException>(() => original.ReserveWorking(1)).ErrorKind);
        var fresh = new Pcre2ResourceBudget(new() { HeapLimitInBytes = ulong.MaxValue }, Timeout.InfiniteTimeSpan, false);
        fresh.ReserveWorking(8); fresh.ReleaseWorking(8);
        Assert.Equal(0UL, fresh.ReservedWorkingBytes);
    }

    [Fact]
    public void SharedTemporaryCapacityCanBeReusedWithoutRefundingWork()
    {
        var original = new Pcre2ResourceBudget(new() { HeapLimitInBytes = 64, MatchLimit = 100 }, Timeout.InfiniteTimeSpan, false);
        var alias = original;
        for (var index = 0; index < 10; index++)
        {
            original.ReserveWorking(64); alias.ChargeWorkspaceStep(); alias.ReleaseWorking(64);
            Assert.Equal(0UL, original.ReservedWorkingBytes);
        }
        Assert.Equal(10UL, Invocation(ref original).Budget.Steps);
        Assert.Equal(64UL, alias.HeapBytes);
    }

    [Fact]
    public void NativeStepsDoNotInvokeTheScalarSliceDiagnosticHook()
    {
        var calls = 0;
        var session = Utf8SearchDiagnosticsSession.Start(_ => calls++);
        try
        {
            var budget = new Pcre2ResourceBudget(new() { MatchLimit = 1000 }, Timeout.InfiniteTimeSpan, false);
            Assert.Null(Invocation(ref budget).CheckpointHook);
            for (var index = 0; index < 128; index++) budget.ChargeCandidate();
            Assert.Equal(128UL, Invocation(ref budget).Budget.Steps);
            Assert.Equal(0, calls);
        }
        finally { session.Complete(); }
    }

    [Fact]
    public void AliasesRetainTheOriginalDeadlineBeforeWorkingAdmission()
    {
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var original = new Pcre2ResourceBudget(new() { HeapLimitInBytes = 1 }, TimeSpan.FromMilliseconds(20), false);
        ticks = 15;
        var alias = original;
        Assert.Same(Invocation(ref original).Budget.Deadline, Invocation(ref alias).Budget.Deadline);
        ticks = 25;
        Assert.Equal(Pcre2ErrorKind.Timeout, Assert.Throws<Pcre2MatchException>(() => alias.ReserveWorking(1000)).ErrorKind);
        Assert.Equal(0UL, original.ReservedWorkingBytes);
        Assert.Equal(Utf8ExecutionLimitKind.None, Invocation(ref original).Budget.LimitBreach);
    }

    [Fact]
    public void ProfileDiagnosticCountsKeepTheirOriginalMeaning()
    {
        var original = new Pcre2ResourceBudget(new() { MatchLimit = 1000 }, Timeout.InfiniteTimeSpan, true);
        original.ChargeCandidate();
        var alias = original;
        original.ChargeBacktracking(Pcre2BacktrackingInstructionKind.Token, Pcre2CharacterTokenKind.Literal);
        alias.ChargeBacktracking(Pcre2BacktrackingInstructionKind.Token, Pcre2CharacterTokenKind.CharacterClass);
        Assert.Equal(1UL, original.CandidateSteps); Assert.Equal(1UL, alias.CandidateSteps);
        Assert.Equal(1UL, original.BacktrackingSteps); Assert.Equal(1UL, alias.BacktrackingSteps);
        Assert.Equal(2UL, original.Diagnostics.VmTokenSteps);
        Assert.Equal(1UL, original.Diagnostics.VmLiteralTokenSteps);
        Assert.Equal(1UL, original.Diagnostics.VmClassTokenSteps);
        Assert.Equal(3UL, Invocation(ref alias).Budget.Steps);
    }
}
