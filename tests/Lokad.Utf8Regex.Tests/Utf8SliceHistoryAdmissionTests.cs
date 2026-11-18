using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8SliceHistoryAdmissionTests
{
    [Theory]
    [InlineData(319UL, false)]
    [InlineData(320UL, true)]
    public void NonBacktrackingFundsRootsAndParticipatingLeavesTogether(ulong available, bool success)
    {
        var invocation = Invocation(new(0, 0, available + 13));
        var result = Utf8SliceRunner.BuildNonBacktrackingHistory([0, -1, 4], [4, -1, 6], true,
            ref invocation, out var starts, out var lengths);
        Assert.Equal(success ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkingBytes, result);
        if (result == Utf8ExecutionLimitKind.None)
        {
            Assert.NotNull(starts);
            Assert.NotNull(lengths);
            Assert.Equal([0], starts[0]);
            Assert.Empty(starts[1]);
            Assert.Equal([4], starts[2]);
            Assert.Equal([4], lengths[0]);
            Assert.Equal([2], lengths[2]);
            Assert.Equal(320UL, HistoryBytes(starts, lengths));
            Assert.Equal(333UL, invocation.Ledger.ReservedBytes);
        }
        else
        {
            Assert.Null(starts);
            Assert.Null(lengths);
        }
        Utf8SliceRunner.ReleaseHistory(starts, lengths, ref invocation);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void NonBacktrackingEmptyHistoryUsesSharedEmptyArraysWithoutAByteLoan()
    {
        var invocation = Invocation(new(0, 0, 13));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SliceRunner.BuildNonBacktrackingHistory([], [], true,
            ref invocation, out var starts, out var lengths));
        Assert.Same(Array.Empty<int[]>(), starts);
        Assert.Same(Array.Empty<int[]>(), lengths);
        Utf8SliceRunner.ReleaseHistory(starts, lengths, ref invocation);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    [Fact]
    public void NonBacktrackingEveryWorkCutoffRestoresTheParentLoan()
    {
        var baseline = Invocation(new(ulong.MaxValue, 0, 0));
        Assert.Equal(Utf8ExecutionLimitKind.None, Utf8SliceRunner.BuildNonBacktrackingHistory([0, -1, 4], [4, -1, 6],
            true, ref baseline, out var starts, out var lengths));
        Utf8SliceRunner.ReleaseHistory(starts, lengths, ref baseline);
        for (ulong cap = 1; cap <= baseline.Budget.Steps; cap++)
        {
            var invocation = Invocation(new(cap, 0, 0));
            var result = Utf8SliceRunner.BuildNonBacktrackingHistory([0, -1, 4], [4, -1, 6], true,
                ref invocation, out starts, out lengths);
            Assert.Equal(cap == baseline.Budget.Steps ? Utf8ExecutionLimitKind.None : Utf8ExecutionLimitKind.WorkSteps, result);
            if (result != Utf8ExecutionLimitKind.None) { Assert.Null(starts); Assert.Null(lengths); }
            Utf8SliceRunner.ReleaseHistory(starts, lengths, ref invocation);
            Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PublishedScalarHistoriesRemainValidAcrossLaterRuns(bool reverse)
    {
        var regex = new Utf8Regex("(?<x>.)+", options: RegexOptions.CultureInvariant | (reverse ? RegexOptions.RightToLeft : 0));
        var program = regex.Inspection.AvailableOwnedSliceProgram ?? throw new InvalidOperationException();
        var input = "é😀a"u8.ToArray();
        var invocation = Invocation(new(ulong.MaxValue, 0, 0));
        var first = Utf8SliceRunner.TryFindFirst(program, input, reverse ? input.Length : 0, new int[1], new int[1], true, ref invocation);
        Assert.True(first.Found);
        var firstStarts = first.CaptureStarts ?? throw new InvalidOperationException();
        var firstLengths = first.CaptureLengths ?? throw new InvalidOperationException();
        Assert.Equal(reverse ? [6, 2, 0] : [0, 2, 6], firstStarts[0]);
        Assert.Equal(reverse ? [1, 4, 2] : [2, 4, 1], firstLengths[0]);
        var firstBytes = HistoryBytes(firstStarts, firstLengths);
        Assert.Equal(13UL + firstBytes, invocation.Ledger.ReservedBytes);
        var second = Utf8SliceRunner.TryFindFirst(program, "b"u8, reverse ? 1 : 0, new int[1], new int[1], true, ref invocation);
        Assert.True(second.Found);
        Assert.Equal(reverse ? [6, 2, 0] : [0, 2, 6], firstStarts[0]);
        Assert.Equal(13UL + firstBytes + HistoryBytes(second.CaptureStarts ?? throw new InvalidOperationException(),
            second.CaptureLengths ?? throw new InvalidOperationException()), invocation.Ledger.ReservedBytes);
        Utf8SliceRunner.ReleaseHistory(firstStarts, firstLengths, ref invocation);
        Utf8SliceRunner.ReleaseHistory(second.CaptureStarts, second.CaptureLengths, ref invocation);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(reverse ? [1, 4, 2] : [2, 4, 1], firstLengths[0]);
    }

    [Theory]
    [InlineData(64UL)]
    [InlineData(128UL)]
    public void NonBacktrackingCancellationBeforeAndAfterAcquisitionRestoresTheParentLoan(ulong cancelAt)
    {
        using var source = new CancellationTokenSource();
        var invocation = new Utf8SliceInvocation(new(ulong.MaxValue, 0, 0), Regex.InfiniteMatchTimeout, source.Token);
        Assert.True(invocation.Ledger.TryReserve(13));
        invocation.CheckpointHook = steps => { if (steps >= cancelAt) source.Cancel(); };
        var starts = new int[96];
        var ends = Enumerable.Repeat(4, 96).ToArray();
        var error = Assert.Throws<OperationCanceledException>(() => Utf8SliceRunner.BuildNonBacktrackingHistory(
            starts, ends, true, ref invocation, out _, out _));
        Assert.Equal(source.Token, error.CancellationToken);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
    }

    private static Utf8SliceInvocation Invocation(Utf8ExecutionLimits limits)
    {
        var invocation = new Utf8SliceInvocation(limits, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        return invocation;
    }

    // Independently census the modeled complete escaping graph, excluding shared empty arrays.
    internal static ulong HistoryBytes(int[][] starts, int[][] lengths)
    {
        static ulong ArrayExtent(int count, int width) => count == 0 ? 0 : 32UL + ((ulong)count * (ulong)width + 15) / 16 * 16;
        var bytes = ArrayExtent(starts.Length, IntPtr.Size) + ArrayExtent(lengths.Length, IntPtr.Size);
        foreach (var leaf in starts) bytes += ArrayExtent(leaf.Length, sizeof(int));
        foreach (var leaf in lengths) bytes += ArrayExtent(leaf.Length, sizeof(int));
        return bytes;
    }
}
