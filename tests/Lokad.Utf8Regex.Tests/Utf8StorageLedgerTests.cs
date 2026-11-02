using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// The invocation owns numerical admission and sticky denial. Its ledger owns
/// live bytes, overflow and balanced release. Growth admits overlapping extents.
/// </summary>
public sealed class Utf8StorageLedgerTests
{
    [Fact]
    public void UnlimitedLedgerAdmitsEverythingExceptOverflow()
    {
        var ledger = default(Utf8StorageLedger);
        Assert.True(ledger.TryReserve(ulong.MaxValue));
        Assert.Equal(ulong.MaxValue, ledger.ReservedBytes);
        Assert.False(ledger.TryReserve(1));
        Assert.Equal(ulong.MaxValue, ledger.ReservedBytes);
        ledger.Release(ulong.MaxValue);
        Assert.Equal(0UL, ledger.ReservedBytes);
    }

    [Fact]
    public void ReservationTripsAboveTheConfiguredLimit()
    {
        var invocation = Capped();
        Assert.False(invocation.HasUnlimitedWorkingMemory);
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(100));
        Assert.Equal(100UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.TryReserveWorking(1));
        Assert.Equal(100UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(101UL, invocation.Budget.PeakWorkingBytes);
        invocation.Ledger.Release(100);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
        // Refund does not reopen an invocation after its first resource denial.
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.TryReserveWorking(1));
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void ZeroReservationIsAlwaysANoOp()
    {
        var ledger = default(Utf8StorageLedger);
        Assert.True(ledger.TryReserve(0));
        Assert.Equal(0UL, ledger.ReservedBytes);
        ledger.Release(0);
        Assert.Equal(0UL, ledger.ReservedBytes);
    }

    [Fact]
    public void OverflowFailsClosedWithoutWrapping()
    {
        var ledger = default(Utf8StorageLedger);
        Assert.True(ledger.TryReserve(ulong.MaxValue - 7));
        Assert.False(ledger.TryReserve(8));
        Assert.Equal(ulong.MaxValue - 7, ledger.ReservedBytes);
    }

    [Fact]
    public void GrowthOverlapIsAdmittedNotAssumed()
    {
        var denied = Capped();
        Assert.Equal(Utf8ExecutionLimitKind.None, denied.TryReserveWorking(60));
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, denied.TryReserveWorking(50));
        Assert.Equal(60UL, denied.Ledger.ReservedBytes);
        denied.Ledger.Release(60);
        Assert.Equal(0UL, denied.Ledger.ReservedBytes);

        var admitted = Capped();
        Assert.Equal(Utf8ExecutionLimitKind.None, admitted.TryReserveWorking(60));
        Assert.Equal(Utf8ExecutionLimitKind.None, admitted.TryReserveWorking(40));
        Assert.Equal(100UL, admitted.Ledger.ReservedBytes);
        admitted.Ledger.Release(60);
        Assert.Equal(40UL, admitted.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.None, admitted.TryReserveWorking(50));
        Assert.Equal(90UL, admitted.Ledger.ReservedBytes);
    }

    [Fact]
    public void TwoLiveBuffersAreTrackedTogether()
    {
        var invocation = Capped();
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(30));
        Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryReserveWorking(40));
        Assert.Equal(70UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, invocation.TryReserveWorking(31));
        invocation.Ledger.Release(30);
        invocation.Ledger.Release(40);
        Assert.Equal(0UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void UnbalancedReleaseThrows()
    {
        var ledger = default(Utf8StorageLedger);
        Assert.True(ledger.TryReserve(60));
        Assert.Throws<InvalidOperationException>(() => ledger.Release(61));
        Assert.Equal(60UL, ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => default(Utf8StorageLedger).Release(1));
    }

    private static Utf8SliceInvocation Capped()
        => new(new(0, 0, 100), Timeout.InfiniteTimeSpan, default);
}
