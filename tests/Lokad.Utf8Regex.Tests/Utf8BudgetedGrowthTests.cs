using System.Threading;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// Legacy capture-journal budget behavior. Invocation-owned scalar journals use
/// the shared exact ledger and have separate aggregate boundary witnesses.
/// </summary>
public sealed class Utf8BudgetedGrowthTests
{
    private static Utf8ExecutionBudget Unlimited()
    {
        return new Utf8ExecutionBudget(new Utf8ExecutionLimits(0, 0, 0), Timeout.InfiniteTimeSpan, CancellationToken.None);
    }

    private static Utf8ExecutionBudget Capped(ulong maxWorkingBytes)
    {
        return new Utf8ExecutionBudget(new Utf8ExecutionLimits(0, 0, maxWorkingBytes), Timeout.InfiniteTimeSpan, CancellationToken.None);
    }

    [Fact]
    public void SetWithoutJournalingChargesNothing()
    {
        var slots = new Utf8CaptureSlots(4);
        var budget = Capped(8);
        Assert.True(slots.TrySet(1, 10, 3, ref budget));
        Assert.Equal(0UL, budget.PeakWorkingBytes);
        Assert.True(slots.TryGet(1, out var start, out var length));
        Assert.Equal(10, start);
        Assert.Equal(3, length);
    }

    [Fact]
    public void JournalGrowthReservesBeforeRenting()
    {
        var slots = new Utf8CaptureSlots(4);
        slots.BeginInvocation();
        var budget = Capped(100);
        Assert.False(slots.TrySet(1, 10, 3, ref budget));
        Assert.Equal(192UL, budget.PeakWorkingBytes);
        slots.EndInvocation();
    }

    [Fact]
    public void JournalSucceedsWithinBudget()
    {
        var slots = new Utf8CaptureSlots(4);
        slots.BeginInvocation();
        var budget = Unlimited();
        Assert.True(slots.TrySet(1, 10, 3, ref budget));
        Assert.True(budget.PeakWorkingBytes >= 192UL);
        slots.CreateCheckpoint();
        slots.EndInvocation();
    }

    [Fact]
    public void OutOfRangeSlotIsHarmless()
    {
        var slots = new Utf8CaptureSlots(4);
        var budget = Capped(8);
        Assert.True(slots.TrySet(99, 10, 3, ref budget));
        Assert.Equal(0UL, budget.PeakWorkingBytes);
    }
}
