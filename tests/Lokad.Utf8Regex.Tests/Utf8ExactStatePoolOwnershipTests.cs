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
        var invocation = new Utf8SliceInvocation(new Utf8ExecutionLimits(0, 128), Regex.InfiniteMatchTimeout, CancellationToken.None);
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
        var bytes = (ulong)leafBytes;
        var stacks = new Utf8SliceRunner.ChunkedStacks();
        Utf8ExactStatePool.Clear();
        try
        {
            var ordinary = Utf8SliceInvocation.CreateExclusive(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, default);
            try
            {
                Assert.True(stacks.PushFrame(default, ref ordinary));
                Assert.Equal(bytes, ordinary.Ledger.ReservedBytes);
                var frames = Utf8SliceRunner.FrameChunkCapacity * (Utf8SliceRunner.ChunkTableCapacity + 1) + 1;
                for (var index = 1; index < frames; index++)
                    Assert.True(stacks.PushFrame(new() { InputIndex = index }, ref ordinary));
                for (var index = frames - 1; index >= 0; index--)
                {
                    Assert.True(stacks.PopFrame(out var frame));
                    Assert.Equal(index, frame.InputIndex);
                }
                Assert.False(stacks.PopFrame(out _));
            }
            finally { stacks.ReleaseAll(ref ordinary); }
            Assert.Equal(0UL, ordinary.Ledger.ReservedBytes);
            Assert.Equal(leafBytes, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);

            var denied = Utf8SliceInvocation.CreateExclusive(new(0, bytes - 1), Regex.InfiniteMatchTimeout, default);
            try
            {
                Assert.False(stacks.PushFrame(default, ref denied));
                Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, stacks.Breach);
                Assert.Equal(0UL, denied.Ledger.ReservedBytes);
            }
            finally { stacks.ReleaseAll(ref denied); }
            Assert.Equal(leafBytes, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);

            var finite = Utf8SliceInvocation.CreateExclusive(new(0, bytes), Regex.InfiniteMatchTimeout, default);
            try
            {
                Assert.True(stacks.PushFrame(default, ref finite));
                Assert.Equal(bytes, finite.Ledger.ReservedBytes);
                Assert.Equal(0, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);
                for (var index = 1; index < Utf8SliceRunner.FrameChunkCapacity; index++)
                    Assert.True(stacks.PushFrame(new() { InputIndex = index }, ref finite));
                Assert.False(stacks.PushFrame(default, ref finite));
                Assert.Equal(Utf8ExecutionLimitKind.WorkingBytes, stacks.Breach);
                Assert.Equal(bytes, finite.Ledger.ReservedBytes);
                Assert.True(stacks.PopFrame(out var last));
                Assert.Equal(Utf8SliceRunner.FrameChunkCapacity - 1, last.InputIndex);
            }
            finally { stacks.ReleaseAll(ref finite); }
            Assert.Equal(0UL, finite.Ledger.ReservedBytes);
            Assert.Equal(0, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);
        }
        finally { Utf8ExactStatePool.Clear(); }
    }

    [Fact]
    public void MemoryCursorReturnsItsFrameLeafAndKeepsPublishedCaptures()
    {
        Utf8ExactStatePool.Clear();
        try
        {
            using var regex = new Utf8Regex(@"\A(?<prefix>.*)(?<end>[bc])\z");
            using var cursor = regex.CreateMatchCursor("é😀b"u8.ToArray());
            var alias = cursor;
            Assert.True(cursor.MoveNext(out var result));

            alias.Dispose();

            Assert.Equal(Utf8SliceRunner.FrameReserveBytes, Utf8ExactStatePool<Utf8SliceFrame>.RetainedBytes);
            Assert.Equal(new Utf8CaptureRange(6, 7), Assert.Single(result.GetCaptures(2).ToArray()));
            Assert.Throws<ObjectDisposedException>(() => cursor.MoveNext(out _));
        }
        finally { Utf8ExactStatePool.Clear(); }
    }
}
