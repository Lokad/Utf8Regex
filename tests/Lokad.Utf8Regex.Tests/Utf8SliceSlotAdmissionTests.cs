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
    public void BothPhysicalSlotArraysShareTheirParentsExactBoundary(int groups)
    {
        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(groups) * 2);
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
                if (groups > 0)
                {
                    Assert.NotSame(slots.Starts, slots.Ends);
                    Assert.All(slots.Starts, value => Assert.Equal(-1, value));
                    Assert.All(slots.Ends, value => Assert.Equal(-1, value));
                }
                Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
                Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            }
            invocation.Ledger.Release(7);
        }
    }
}
