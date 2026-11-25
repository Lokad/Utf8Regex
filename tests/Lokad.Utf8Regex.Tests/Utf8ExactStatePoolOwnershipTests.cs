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

    [Fact]
    public void FirstFrameLeafReusesStorageOnlyAfterAdmission()
    {
        var leafBytes = Utf8SliceRunner.FrameReserveBytes;
        var bytes = (ulong)leafBytes + Utf8ConstructionBudget.ArrayBytes<Utf8SliceFrame[]>(Utf8SliceRunner.ChunkTableCapacity);
        var stacks = new Utf8SliceRunner.ChunkedStacks();
        Utf8ExactStatePool.Clear();
        try
        {
            var ordinary = Utf8SliceInvocation.CreateExclusive(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
            try
            {
                Assert.True(stacks.PushFrame(default, ref ordinary));
                Assert.Equal(bytes, ordinary.Ledger.ReservedBytes);
            }
            finally { stacks.ReleaseAll(ref ordinary); }
            Assert.Equal(0UL, ordinary.Ledger.ReservedBytes);
            Assert.Equal(leafBytes, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);

            var denied = Utf8SliceInvocation.CreateExclusive(new(0, 0, bytes - 1), Regex.InfiniteMatchTimeout, default);
            try
            {
                Assert.False(stacks.PushFrame(default, ref denied));
                Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, stacks.Breach);
                Assert.Equal(0UL, denied.Ledger.ReservedBytes);
            }
            finally { stacks.ReleaseAll(ref denied); }
            Assert.Equal(leafBytes, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);

            var finite = Utf8SliceInvocation.CreateExclusive(new(0, 0, bytes), Regex.InfiniteMatchTimeout, default);
            try
            {
                Assert.True(stacks.PushFrame(default, ref finite));
                Assert.Equal(bytes, finite.Ledger.ReservedBytes);
                Assert.Equal(0, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);
            }
            finally { stacks.ReleaseAll(ref finite); }
            Assert.Equal(0UL, finite.Ledger.ReservedBytes);
            Assert.Equal(0, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }
}
