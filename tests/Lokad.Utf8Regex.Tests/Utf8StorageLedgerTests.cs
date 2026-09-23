using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

/// <summary>
/// R02a accounting: the aggregate admission ledger grants reservations up to the
/// configured live cap, fails closed on overflow without wrapping, restores
/// capacity on balanced release, rejects unbalanced release loudly, and admits
/// growth overlap by holding the old extent while reserving the new one.
/// </summary>
public sealed class Utf8StorageLedgerTests
{
    [Fact]
    public void UnlimitedLedgerAdmitsEverythingExceptOverflow()
    {
        var ledger = new Utf8StorageLedger(0);
        Assert.True(ledger.IsUnlimited);
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
        var ledger = new Utf8StorageLedger(100);
        Assert.False(ledger.IsUnlimited);
        Assert.True(ledger.TryReserve(100));
        Assert.Equal(100UL, ledger.ReservedBytes);
        Assert.False(ledger.TryReserve(1));
        Assert.Equal(100UL, ledger.ReservedBytes);
        ledger.Release(100);
        Assert.Equal(0UL, ledger.ReservedBytes);
        Assert.True(ledger.TryReserve(1));
    }

    [Fact]
    public void ZeroReservationIsAlwaysANoOp()
    {
        var ledger = new Utf8StorageLedger(100);
        Assert.True(ledger.TryReserve(0));
        Assert.Equal(0UL, ledger.ReservedBytes);
        ledger.Release(0);
        Assert.Equal(0UL, ledger.ReservedBytes);
    }

    [Fact]
    public void OverflowFailsClosedWithoutWrapping()
    {
        var ledger = new Utf8StorageLedger(0);
        Assert.True(ledger.TryReserve(ulong.MaxValue - 7));
        Assert.False(ledger.TryReserve(8));
        Assert.Equal(ulong.MaxValue - 7, ledger.ReservedBytes);
    }

    [Fact]
    public void GrowthOverlapIsAdmittedNotAssumed()
    {
        var ledger = new Utf8StorageLedger(100);
        Assert.True(ledger.TryReserve(60));
        Assert.False(ledger.TryReserve(50));
        Assert.True(ledger.TryReserve(40));
        Assert.Equal(100UL, ledger.ReservedBytes);
        ledger.Release(60);
        Assert.Equal(40UL, ledger.ReservedBytes);
    }

    [Fact]
    public void TwoLiveBuffersAreTrackedTogether()
    {
        var ledger = new Utf8StorageLedger(100);
        Assert.True(ledger.TryReserve(30));
        Assert.True(ledger.TryReserve(40));
        Assert.Equal(70UL, ledger.ReservedBytes);
        Assert.False(ledger.TryReserve(31));
        ledger.Release(30);
        ledger.Release(40);
        Assert.Equal(0UL, ledger.ReservedBytes);
    }

    [Fact]
    public void UnbalancedReleaseThrows()
    {
        var ledger = new Utf8StorageLedger(100);
        Assert.True(ledger.TryReserve(60));
        Assert.Throws<InvalidOperationException>(() => ledger.Release(61));
        Assert.Equal(60UL, ledger.ReservedBytes);
        Assert.Throws<InvalidOperationException>(() => new Utf8StorageLedger(100).Release(1));
    }
}
