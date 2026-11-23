using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8ExactStatePoolOwnershipTests
{
    private readonly record struct HeaderState(long Value);

    [Theory]
    [InlineData(16)]
    [InlineData(512)]
    public void RetainedTypeLoanIncludesItsArrayHeaderAndRounding(int capacity)
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var items = Utf8ExactStatePool<HeaderState>.Rent(capacity, true);
            Utf8ExactStatePool<HeaderState>.Return(items, true);
            Assert.Equal(Utf8ConstructionBudget.ArrayBytes<HeaderState>(capacity), (ulong)Utf8ExactStatePool<HeaderState>.RetainedBytes);
            Assert.Same(items, Utf8ExactStatePool<HeaderState>.Rent(capacity, false));
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void LargerReferenceBearingArraysStayOutsideThePool()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            var small = Utf8ExactStatePool<object>.Rent(256, true);
            var large = Utf8ExactStatePool<object>.Rent(512, true);
            Utf8ExactStatePool<object>.Return(small, true);
            Utf8ExactStatePool<object>.Return(large, true);
            Assert.Equal(Utf8ConstructionBudget.ArrayBytes<object>(256), (ulong)Utf8ExactStatePool<object>.RetainedBytes);
            Assert.NotSame(large, Utf8ExactStatePool<object>.Rent(512, false));
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void HeaderDenialPrecedesWorkingBufferAcquisition()
    {
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 0, 128), Regex.InfiniteMatchTimeout, CancellationToken.None);
        var buffer = new Utf8InvocationStateBuffer<long>();
        try
        {
            Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, buffer.TryPush(1, ref invocation));
            Assert.Equal(0ul, invocation.Ledger.ReservedBytes);
        }
        finally { buffer.Release(ref invocation); }
    }
}
