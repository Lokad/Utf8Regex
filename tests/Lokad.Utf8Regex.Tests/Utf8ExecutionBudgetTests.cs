using System.Text.RegularExpressions;
using System.Threading;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R02 extraction 1: the shared invocation budget counts steps, tracks depth and
/// working-bytes peaks, enforces zero-means-unlimited limits, polls deadlines, and
/// observes cancellation tokens without allocating on the unmetered path.
/// </summary>
public sealed class Utf8ExecutionBudgetTests
{
    [Theory]
    [InlineData(0UL)]
    [InlineData(ulong.MaxValue - 1)]
    [InlineData(ulong.MaxValue)]
    public void StepCounterSaturatesAndFiniteDenialRemainsSticky(ulong limit)
    {
        object boxed = new Utf8ExecutionBudget(new(limit, 0, 0), Regex.InfiniteMatchTimeout, default);
        typeof(Utf8ExecutionBudget).GetProperty(nameof(Utf8ExecutionBudget.Steps),
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .SetValue(boxed, ulong.MaxValue - 1);
        var budget = (Utf8ExecutionBudget)boxed;
        Assert.Equal(limit == ulong.MaxValue - 1 ? Utf8ExecutionLimitKind.WorkSteps : Utf8ExecutionLimitKind.None,
            budget.TryChargeStep());
        Assert.Equal(ulong.MaxValue, budget.Steps);
        Assert.Equal(limit == 0 ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, budget.TryChargeStep());
        Assert.Equal(ulong.MaxValue, budget.Steps);
        Assert.Equal(limit != 0, budget.WorkLimitExceeded);
        Assert.Equal(limit == 0 ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, budget.TryChargeStep());
        Assert.Equal(ulong.MaxValue, budget.Steps);
    }

    private static readonly Utf8ExecutionLimits s_unlimited = new(0, 0, 0);

    [Fact]
    public void UnlimitedBudgetNeverBreachesAndTracksPeaks()
    {
        var budget = new Utf8ExecutionBudget(s_unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(budget.IsUnmetered);

        for (var i = 0; i < 100_000; i++)
        {
            Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryChargeStep());
        }

        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryNoteDepth(7));
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryNoteDepth(3));
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryNoteWorkingBytes(4096));
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryNoteWorkingBytes(512));
        Assert.Equal(100_000UL, budget.Steps);
        Assert.Equal(7U, budget.PeakDepth);
        Assert.Equal(4096UL, budget.PeakWorkingBytes);
    }

    [Fact]
    public void WorkLimitTripsOneStepAboveTheConfiguredLimit()
    {
        var budget = new Utf8ExecutionBudget(new Utf8ExecutionLimits(3, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.False(budget.IsUnmetered);
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryChargeStep());
        Assert.Equal(Utf8ExecutionLimitKind.WorkSteps, budget.TryChargeStep());
        Assert.Equal(4UL, budget.Steps);
    }

    [Fact]
    public void DepthLimitTripsAboveTheConfiguredLimit()
    {
        var budget = new Utf8ExecutionBudget(new Utf8ExecutionLimits(0, 4, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryNoteDepth(4));
        Assert.Equal(Utf8ExecutionLimitKind.Depth, budget.TryNoteDepth(5));
        Assert.Equal(5U, budget.PeakDepth);
        Assert.Equal(Utf8ExecutionLimitKind.Depth, budget.TryNoteDepth(1));
        Assert.Equal(Utf8ExecutionLimitKind.Depth, budget.TryChargeStep());
        var failure = budget.CreateResourceFailure();
        Assert.Equal(Utf8ResourceLimitKind.ExecutionDepth, failure.ResourceKind);
        Assert.Equal(4UL, failure.Limit); Assert.Equal(5UL, failure.Requested);
    }

    [Fact]
    public void WorkingBytesLimitTripsAboveTheConfiguredLimit()
    {
        var budget = new Utf8ExecutionBudget(new Utf8ExecutionLimits(0, 0, 1024), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryNoteWorkingBytes(1024));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, budget.TryNoteWorkingBytes(1025));
        Assert.Equal(1025UL, budget.PeakWorkingBytes);
    }

    [Fact]
    public void FiniteDeadlineExpiresThroughTheSharedCheckpoint()
    {
        var budget = new Utf8ExecutionBudget(s_unlimited, TimeSpan.FromMilliseconds(1), CancellationToken.None);
        Assert.False(budget.IsUnmetered);
        Assert.Throws<Utf8ExecutionDeadlineExpiredException>(() =>
        {
            for (var i = 0; i < 50_000_000; i++)
            {
                budget.TryChargeStep();
            }
        });
    }

    [Fact]
    public void CancelledTokenThrowsCarryingTheToken()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        var budget = new Utf8ExecutionBudget(s_unlimited, Regex.InfiniteMatchTimeout, source.Token);
        Assert.False(budget.IsUnmetered);
        var exception = Assert.Throws<OperationCanceledException>(() => budget.TryChargeStep());
        Assert.Equal(source.Token, exception.CancellationToken);
        Assert.Equal(0UL, budget.Steps);
    }

    [Fact]
    public void NonCancellableTokenKeepsTheCheapPath()
    {
        var budget = new Utf8ExecutionBudget(s_unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.Equal(Utf8ExecutionLimitKind.None, budget.TryChargeStep());
        Assert.Equal(1UL, budget.Steps);
    }
}
