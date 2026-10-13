using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ConstructionByteLimitTests
{
    [Fact]
    public void AdditionalCeilingPreservesParentReservationsAndHistoricalPeak()
    {
        using var parent = new Utf8ConstructionScope(1000, 1000);
        parent.Budget.ReserveBytes(113); parent.Budget.ReleaseBytes(100);
        using (var child = Utf8ConstructionScope.RestrictAdditionalBytes(32))
        {
            Assert.Same(parent.Budget, child.Budget);
            Assert.Equal(45UL, child.Budget.MaximumBytes);
            Assert.Equal(13UL, child.Budget.PeakBytes);
            child.Budget.ReserveBytes(32);
            var failure = Assert.Throws<Utf8ResourceLimitExceededException>(() => child.Budget.ReserveBytes(1));
            Assert.Equal(Utf8ResourceLimitKind.ConstructionBytes, failure.ResourceKind);
            Assert.Equal(46UL, failure.Requested);
            Assert.Equal(45UL, child.Budget.ReservedBytes);
        }
        Assert.Equal(13UL, parent.Budget.ReservedBytes);
        Assert.Equal(1000UL, parent.Budget.MaximumBytes);
        Assert.Equal(113UL, parent.Budget.PeakBytes);
    }

    [Fact]
    public void NestedCeilingsRestoreCompletedLoansAndOuterPeak()
    {
        using var parent = new Utf8ConstructionScope(1000, 1000);
        parent.Budget.ReserveBytes(13);
        using (var outer = Utf8ConstructionScope.RestrictAdditionalBytes(64))
        {
            outer.Budget.ReserveBytes(5);
            using (var inner = Utf8ConstructionScope.RestrictAdditionalBytes(16))
            {
                Assert.Equal(34UL, inner.Budget.MaximumBytes);
                inner.Budget.ReserveBytes(16);
                inner.Complete();
            }
            Assert.Equal(77UL, outer.Budget.MaximumBytes);
            Assert.Equal(34UL, outer.Budget.ReservedBytes);
            Assert.Equal(34UL, outer.Budget.PeakBytes);
        }
        Assert.Equal(13UL, parent.Budget.ReservedBytes);
        Assert.Equal(1000UL, parent.Budget.MaximumBytes);
        Assert.Equal(34UL, parent.Budget.PeakBytes);
    }

    [Fact]
    public void ExistingTighterCeilingAndOverflowRemainBounded()
    {
        using var parent = new Utf8ConstructionScope(1000, 20);
        parent.Budget.ReserveBytes(13);
        using (var child = Utf8ConstructionScope.RestrictAdditionalBytes(ulong.MaxValue))
        {
            Assert.Equal(20UL, child.Budget.MaximumBytes);
            Assert.Throws<Utf8ResourceLimitExceededException>(() => child.Budget.ReserveBytes(8));
        }
        Assert.Equal(13UL, parent.Budget.ReservedBytes);
        Assert.Equal(20UL, parent.Budget.MaximumBytes);
    }
}
