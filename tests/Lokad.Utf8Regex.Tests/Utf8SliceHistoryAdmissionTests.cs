using System.Runtime.CompilerServices;
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
        var workspace = new Utf8SliceRunner.AttemptWorkspace();
        var firstResults = new Utf8InvocationResultLedger();
        var secondResults = new Utf8InvocationResultLedger();
        (int Start, int End)[] expected = reverse ? [(6, 7), (2, 6), (0, 2)] : [(0, 2), (2, 6), (6, 7)];
        Utf8RangeMatch firstHistory;
        try
        {
            var first = Utf8SliceRunner.TryFindFirst(program, input, reverse ? input.Length : 0, new int[1], new int[1], true,
                ref workspace, ref invocation);
            Assert.True(first.Found);
            var held = invocation.Ledger.ReservedBytes;
            firstHistory = workspace.Stacks.SnapshotHistory(input, program.GroupNumbers, first.StartByte, first.EndByte,
                ref firstResults, ref invocation);
            Assert.Equal(expected, CaptureEdges(firstHistory, 1));
            Assert.Equal(held + HistoryBytes(firstHistory), invocation.Ledger.ReservedBytes);

            var second = Utf8SliceRunner.TryFindFirst(program, "b"u8, reverse ? 1 : 0, new int[1], new int[1], true,
                ref workspace, ref invocation);
            Assert.True(second.Found);
            held = invocation.Ledger.ReservedBytes;
            var secondHistory = workspace.Stacks.SnapshotHistory("b"u8.ToArray(), program.GroupNumbers, second.StartByte, second.EndByte,
                ref secondResults, ref invocation);
            Assert.Equal(expected, CaptureEdges(firstHistory, 1));
            Assert.Equal(held + HistoryBytes(secondHistory), invocation.Ledger.ReservedBytes);
        }
        finally
        {
            secondResults.Release(ref invocation);
            firstResults.Release(ref invocation);
            workspace.Release(ref invocation);
        }
        Assert.Equal(13UL, invocation.Ledger.ReservedBytes);
        Assert.Equal(expected, CaptureEdges(firstHistory, 1));
    }

    private static Utf8SliceInvocation Invocation(Utf8ExecutionLimits limits)
    {
        var invocation = new Utf8SliceInvocation(limits, Regex.InfiniteMatchTimeout, default);
        Assert.True(invocation.Ledger.TryReserve(13));
        return invocation;
    }

    internal static IEnumerable<(int Start, int End)> CaptureEdges(Utf8RangeMatch match, int group)
        => match.GetCaptures(group).ToArray().Select(static range => (range.StartOffsetInBytes, range.EndOffsetInBytes));

    // Census the two arrays independently of the result ledger. The catalog is borrowed.
    internal static ulong HistoryBytes(Utf8RangeMatch history)
    {
        static ulong ArrayExtent(int count, int width) => count == 0 ? 0 : 32UL + ((ulong)count * (ulong)width + 15) / 16 * 16;
        var count = 0;
        foreach (var group in history.GroupNumbers) count += history.GetCaptures(group).Length;
        return ArrayExtent(history.GroupNumbers.Length + 1, sizeof(int)) + ArrayExtent(count, Unsafe.SizeOf<Utf8CaptureRange>());
    }
}
