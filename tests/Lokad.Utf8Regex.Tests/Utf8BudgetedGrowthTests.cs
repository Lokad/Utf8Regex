using System.Threading;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R02 extraction 2: pooled-stack pushes and capture-journal writes reserve working
/// bytes before renting, account actual rented size plus copy overlap, and decline
/// with false on breach while preserving maximum-count behavior. Legacy unbudgeted
/// paths are unchanged.
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
    public void PushWithoutGrowthChargesNothing()
    {
        var stack = new Utf8PooledStateStack<int>(1024);
        var budget = Capped(1UL << 20);
        Assert.True(stack.TryPush(1, ref budget));
        var peak = budget.PeakWorkingBytes;
        Assert.True(peak >= 16UL);
        Assert.True(stack.TryPush(2, ref budget));
        Assert.True(stack.TryPush(3, ref budget));
        Assert.Equal(peak, budget.PeakWorkingBytes);
        Assert.Equal(3, stack.Count);
        stack.Dispose();
    }

    [Fact]
    public void BreachDeclinesPushWithoutGrowing()
    {
        var stack = new Utf8PooledStateStack<int>(1024);
        var budget = Capped(15);
        Assert.False(stack.TryPush(1, ref budget));
        Assert.Equal(0, stack.Count);
        Assert.Equal(16UL, budget.PeakWorkingBytes);
        stack.Dispose();
    }

    [Fact]
    public void MaximumCountIsEnforcedOnBothPushPaths()
    {
        var budgeted = new Utf8PooledStateStack<int>(2);
        var budget = Unlimited();
        Assert.True(budgeted.TryPush(1, ref budget));
        Assert.True(budgeted.TryPush(2, ref budget));
        Assert.False(budgeted.TryPush(3, ref budget));
        Assert.Equal(2, budgeted.Count);
        budgeted.Dispose();

        var legacy = new Utf8PooledStateStack<int>(2);
        Assert.True(legacy.TryPush(1));
        Assert.True(legacy.TryPush(2));
        Assert.False(legacy.TryPush(3));
        legacy.Dispose();
    }

    [Fact]
    public void RepeatedGrowthKeepsAnHonestPeak()
    {
        var stack = new Utf8PooledStateStack<int>(1024);
        var budget = Unlimited();
        for (var i = 0; i < 20; i++)
        {
            Assert.True(stack.TryPush(i, ref budget));
        }

        Assert.True(budget.PeakWorkingBytes >= 128UL);
        Assert.Equal(20, stack.Count);
        stack.Dispose();
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
