using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Replacement;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8OutputExtentAdmissionTests
{
    private static ulong Bytes(int count) => count == 0 ? 0 : 32UL + (((ulong)count + 15) & ~15UL);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_buffer")]
    private static extern ref byte[]? Buffer(ref Utf8SliceOutputWriter writer);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pieces")]
    private static extern ref int[]? Pieces(Utf8SliceByteOutputBuffer buffer);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(16)] [InlineData(17)] [InlineData(8193)]
    public void CompleteOutputExtentIsAdmittedBeforeAllocation(int count)
    {
        foreach (var allow in new[] { false, true })
        {
            if (!allow && count == 0) continue;
            var invocation = new Utf8SliceInvocation(new(0, 0, 7 + Bytes(count) - (allow ? 0UL : 1UL)),
                Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(invocation.Ledger.TryReserve(7));
            var control = new Utf8InvocationBufferControl(ref invocation);
            Assert.Equal(Utf8ExecutionLimitKind.None, Utf8InvocationOutput.TryAllocateBytes(0, control, out _, out _));
            var before = GC.GetAllocatedBytesForCurrentThread();
            var breach = Utf8InvocationOutput.TryAllocateBytes(count, control, out var result, out var reservation);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.Equal(allow ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, breach);
            if (!allow) { Assert.Equal(0, allocated); Assert.Empty(result); Assert.Equal(0UL, reservation); }
            else { Assert.Equal(count, result.Length); Assert.Equal(Bytes(count), reservation); }
            control.ReleaseWorking(reservation);
            Assert.Equal(7UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(7);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void EveryGrowthAndPublicationWorkCutoffKeepsAnUnpublishedResultAndBalances(bool publication)
    {
        const ulong seed = 5;
        var complete = publication ? 5UL : 8UL;
        var payload = Enumerable.Repeat((byte)'x', 8192).ToArray();
        for (ulong cutoff = 0; cutoff <= complete; cutoff++)
        {
            var invocation = new Utf8SliceInvocation(new(seed + cutoff, 0, 1UL << 20), Regex.InfiniteMatchTimeout, CancellationToken.None);
            Assert.True(invocation.Ledger.TryReserve(13));
            Assert.True(Utf8SliceOutputWriter.Rent(8192, ref invocation, out var writer));
            Assert.True(writer.Append(payload, ref invocation));
            Assert.Equal(seed, invocation.Budget.Steps);
            var original = Buffer(ref writer);
            byte[] result = [];
            Exception? failure = null;
            try
            {
                if (publication) Assert.True(writer.TryToArrayAndRelease(ref invocation, out result));
                else Assert.True(writer.Append("!"u8, ref invocation));
            }
            catch (InvalidOperationException error) { failure = error; }
            try
            {
                if (cutoff == complete)
                {
                    Assert.Null(failure);
                    if (publication) { Assert.Equal(payload, result); Assert.Equal(0, writer.Length); Assert.Equal(13UL, invocation.Ledger.ReservedBytes); }
                    else Assert.Equal(8193, writer.Length);
                }
                else
                {
                    Assert.NotNull(failure);
                    Assert.Empty(result);
                    Assert.Equal(8192, writer.Length);
                    var capacity = publication || cutoff < 5 ? 8192 : 16384;
                    Assert.Equal(13 + Bytes(capacity), invocation.Ledger.ReservedBytes);
                    if (capacity == 8192) Assert.Same(original, Buffer(ref writer));
                    Assert.Equal(payload, Buffer(ref writer)!.AsSpan(0, 8192).ToArray());
                    Assert.True(invocation.Budget.Steps > seed + cutoff);
                }
            }
            finally { writer.Release(ref invocation); }
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
            invocation.Ledger.Release(13);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void CancellationAndDeadlineAfterFirstPublicationCopyBlockPreserveTheWorkingOwner(bool expire)
    {
        using var token = new CancellationTokenSource();
        long ticks = 0;
        using var clock = Utf8ExecutionDeadline.UseClockForDiagnostics(() => ticks);
        var invocation = new Utf8SliceInvocation(new(0, 0, 1UL << 20),
            expire ? TimeSpan.FromMilliseconds(20) : Regex.InfiniteMatchTimeout, token.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        Assert.True(Utf8SliceOutputWriter.Rent(8192, ref invocation, out var writer));
        Assert.True(writer.Append(new byte[8192], ref invocation));
        var original = Buffer(ref writer);
        while ((invocation.Budget.Steps & 63) != 60) Assert.Equal(Utf8ExecutionLimitKind.None, invocation.TryChargeInputStep());
        var entered = false;
        invocation.CheckpointHook = _ => { entered = true; if (expire) ticks = 50; else token.Cancel(); };
        byte[] result = [];
        Exception? failure = null;
        try { _ = writer.TryToArrayAndRelease(ref invocation, out result); }
        catch (Exception error) { failure = error; }
        try
        {
            Assert.True(entered);
            if (expire) Assert.IsType<Utf8ExecutionDeadlineExpiredException>(failure);
            else Assert.Equal(token.Token, Assert.IsType<OperationCanceledException>(failure).CancellationToken);
            Assert.Empty(result);
            Assert.Same(original, Buffer(ref writer));
            Assert.Equal(8192, writer.Length);
            Assert.Equal(13 + Bytes(8192), invocation.Ledger.ReservedBytes);
        }
        finally { writer.Release(ref invocation); }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        invocation.Ledger.Release(13);
    }

    [Theory]
    [InlineData(-37)] [InlineData(0)] [InlineData(37)]
    public void BoundedUnchangedCopiesPreserveOverlappingSpanSemantics(int displacement)
    {
        var actual = Enumerable.Range(0, 20074).Select(i => (byte)(i % 251)).ToArray();
        var expected = actual.ToArray();
        var start = displacement < 0 ? 37 : 0;
        expected.AsSpan(start, 20000).CopyTo(expected.AsSpan(start + displacement, 20000));
        var invocation = new Utf8SliceInvocation(new(32, 0, 0), Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(Utf8ReplacementOutput.TryCopyUnchanged(actual.AsSpan(start, 20000),
            actual.AsSpan(start + displacement, 20000), out var written, new Utf8InvocationBufferControl(ref invocation)));
        Assert.Equal(20000, written);
        Assert.Equal(expected, actual);
        Assert.Equal(7UL, invocation.Budget.Steps);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ReversePieceTablesAdmitCompleteArrayGrowthOverlap(bool allow)
    {
        const ulong controls = 2 * Utf8SliceByteOutputScope.ControlBytes;
        // Byte capacity 16 (48), int capacities 16 (96) and 32 (160).
        var scope = Utf8SliceByteOutputScope.Enter(new(0, 0, controls + 13 + 304 - (allow ? 0UL : 1UL)),
            Regex.InfiniteMatchTimeout, CancellationToken.None);
        Assert.True(scope.Invocation.Ledger.TryReserve(13));
        try
        {
            var writer = scope.CreateBuffer();
            writer.Append("x"u8);
            for (var i = 0; i < 16; i++) writer.MarkPiece();
            var original = Pieces(writer);
            if (allow) { writer.MarkPiece(); Assert.Equal(32, Pieces(writer)!.Length); }
            else { Assert.Throws<InvalidOperationException>(() => writer.MarkPiece()); Assert.Same(original, Pieces(writer)); }
            Assert.Equal(controls + 13 + 48 + (allow ? 160UL : 96UL), scope.Invocation.Ledger.ReservedBytes);
            writer.ReversePieces();
            Assert.Equal("x"u8.ToArray(), writer.ToArray());
        }
        finally { scope.Release(); }
        Assert.Equal(13UL, scope.Invocation.Ledger.ReservedBytes);
        scope.Invocation.Ledger.Release(13);
    }
}
