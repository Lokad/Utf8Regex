using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

[Collection(Utf8RuntimeAllocationCollection.Name)]
public sealed class Utf8SliceHistoryAdmissionTests
{
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
        var firstHistory = first.CaptureHistory ?? throw new InvalidOperationException();
        (int Start, int End)[] expected = reverse ? [(6, 7), (2, 6), (0, 2)] : [(0, 2), (2, 6), (6, 7)];
        Assert.Equal(expected, firstHistory[0]);
        var firstBytes = HistoryBytes(firstHistory);
        Assert.Equal(13UL + firstBytes, invocation.Ledger.ReservedBytes);
        var second = Utf8SliceRunner.TryFindFirst(program, "b"u8, reverse ? 1 : 0, new int[1], new int[1], true, ref invocation);
        Assert.True(second.Found);
        Assert.Equal(expected, firstHistory[0]);
        Assert.Equal(13UL + firstBytes + HistoryBytes(second.CaptureHistory ?? throw new InvalidOperationException()),
            invocation.Ledger.ReservedBytes);
        Utf8SliceRunner.ReleaseHistory(firstHistory, ref invocation);
        Utf8SliceRunner.ReleaseHistory(second.CaptureHistory, ref invocation);
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(expected, firstHistory[0]);
    }

    private static Utf8SliceInvocation Invocation(Utf8ExecutionLimits limits)
    {
        var invocation = new Utf8SliceInvocation(limits, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        return invocation;
    }

    // Independently census the modeled complete escaping graph, excluding shared empty arrays.
    internal static ulong HistoryBytes((int Start, int End)[][] history)
    {
        static ulong ArrayExtent(int count, int width) => count == 0 ? 0 : 32UL + ((ulong)count * (ulong)width + 15) / 16 * 16;
        var bytes = ArrayExtent(history.Length, IntPtr.Size);
        foreach (var leaf in history) bytes += ArrayExtent(leaf.Length, sizeof(int) * 2);
        return bytes;
    }
}
