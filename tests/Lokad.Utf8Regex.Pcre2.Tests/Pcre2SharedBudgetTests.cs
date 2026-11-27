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

    private static void AssertWorkDenial(ref Utf8Pcre2ValueMatchEnumerator cursor)
    {
        Assert.Fail("A finite shared work allowance did not stop discovery.");
    }

    [Theory]
[InlineData(1)] [InlineData(2)]
    public void BudgetCopiesShareTheFirstWorkDepthOrHeapDenial(int kind)
    {
        var original = new Pcre2ResourceBudget(new() { DepthLimit = kind == 1 ? 1u : 0u, HeapLimitInBytes = kind == 2 ? 128UL : 0UL }, Timeout.InfiniteTimeSpan, false);
        var alias = original;
        var expected = kind == 1 ? Pcre2ErrorKind.DepthLimit : Pcre2ErrorKind.HeapLimit;
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
        Assert.Equal(expected, Assert.Throws<Pcre2MatchException>(() => original.CheckWorkspace()).ErrorKind);
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
        var original = new Pcre2ResourceBudget(new() { HeapLimitInBytes = 64 }, Timeout.InfiniteTimeSpan, false);
        var alias = original;
        for (var index = 0; index < 10; index++)
        {
            original.ReserveWorking(64); alias.CheckWorkspace(); alias.ReleaseWorking(64);
            Assert.Equal(0UL, original.ReservedWorkingBytes);
        }
        Assert.Equal(64UL, alias.HeapBytes);
    }

    [Fact]
    public void NativeStepsDoNotInvokeTheScalarSliceDiagnosticHook()
    {
        var calls = 0;
        var session = Utf8SearchDiagnosticsSession.Start(_ => calls++);
        try
        {
            var budget = new Pcre2ResourceBudget(new() { DepthLimit = 4096 }, Timeout.InfiniteTimeSpan, false);
            Assert.Null(Invocation(ref budget).CheckpointHook);
            for (var index = 0; index < 128; index++) budget.ChargeCandidate();
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
        var original = new Pcre2ResourceBudget(new() { DepthLimit = 4096 }, Timeout.InfiniteTimeSpan, true);
        original.ChargeCandidate();
        var alias = original;
        original.ChargeBacktracking(Pcre2BacktrackingInstruction.CreateToken(
            Pcre2CharacterToken.CreateLiteral(default, default)));
        alias.ChargeBacktracking(Pcre2BacktrackingInstruction.CreateToken(
            Pcre2CharacterToken.CreateClass(Pcre2CharacterClass.Empty, default)));
        Assert.Equal(1UL, original.CandidateSteps); Assert.Equal(1UL, alias.CandidateSteps);
        Assert.Equal(1UL, original.BacktrackingSteps); Assert.Equal(1UL, alias.BacktrackingSteps);
        Assert.Equal(2UL, original.Diagnostics.VmTokenSteps);
        Assert.Equal(1UL, original.Diagnostics.VmLiteralTokenSteps);
        Assert.Equal(1UL, original.Diagnostics.VmClassTokenSteps);
    }
}
