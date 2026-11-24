using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8SliceSlotAdmissionTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(16)]
    [InlineData(65)]
    [InlineData(4096)]
    public void CaptureSlotsShareTheirParentsExactBoundary(int groups)
    {
        var bytes = Utf8ConstructionBudget.ArrayBytes<int>(checked(groups * 2));
        foreach (var allow in new[] { false, true })
        {
            var limit = 7 + bytes - (allow || groups == 0 ? 0UL : 1UL);
            var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, limit), Timeout.InfiniteTimeSpan, default);
            Assert.True(invocation.Ledger.TryReserve(7));
            if (!allow && groups != 0)
            {
                try { _ = Utf8SliceRunner.RentSlots(groups, ref invocation); Assert.Fail("Expected full-array denial."); }
                catch (InvalidOperationException) { }
                Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            }
            else
            {
                var slots = Utf8SliceRunner.RentSlots(groups, ref invocation);
                Assert.Equal(groups, slots.Starts.Length);
                Assert.Equal(groups, slots.Ends.Length);
                Assert.Equal(7 + bytes, invocation.Ledger.ReservedBytes);
                Assert.False(slots.Starts.Overlaps(slots.Ends));
                Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
                Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            }
            invocation.Ledger.Release(7);
        }
    }
}
