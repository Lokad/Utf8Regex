using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

// The scalar engine produces parallel edge spans. The backtracking engine stores
// all fields for a capture together. Both publish from their original storage.
internal readonly ref struct Pcre2CaptureRanges
{
    private readonly ReadOnlySpan<Pcre2CaptureState> _state;
    private readonly ReadOnlySpan<int> _starts;
    private readonly ReadOnlySpan<int> _ends;

    internal Pcre2CaptureRanges(ReadOnlySpan<Pcre2CaptureState> state) => _state = state;

    internal Pcre2CaptureRanges(ReadOnlySpan<int> starts, ReadOnlySpan<int> ends)
    {
        _starts = starts;
        _ends = ends;
    }

    private (int Start, int End) this[int slot]
    {
        get
        {
            if (_state.IsEmpty) return (_starts[slot], _ends[slot]);
            ref readonly var state = ref _state[slot];
            return (state.Start, state.End);
        }
    }

    internal bool TryGetEdges(int slot, int matchStart, int matchEnd, bool scalarRanges,
        out int start, out int end)
    {
        if (slot == 0) { start = matchStart; end = matchEnd; return true; }
        (start, end) = this[scalarRanges ? slot - 1 : slot];
        if (start < 0 || end < 0) return false;
        // Shared scalar assertions may write the endpoints in reverse order.
        if (scalarRanges && start > end) (start, end) = (end, start);
        return end >= start;
    }

    internal static bool IsWellFormed(ReadOnlySpan<byte> input, int start, int end, bool scalarRanges)
        => start <= end && (scalarRanges ||
            Utf8BytePosition.IsScalarBoundary(input, start) && Utf8BytePosition.IsScalarBoundary(input, end));
}

internal static partial class Pcre2Runner
{
    // The final array stays charged until its caller publishes the result.
    internal static Pcre2GroupData[] PublishCaptures(
        int captureSlotCount, int matchStart, int matchEnd,
        scoped Pcre2CaptureRanges captureRanges,
        ReadOnlySpan<byte> input, ref Pcre2ResourceBudget budget)
    {
        var control = new Pcre2ResultControl(ref budget);
        return PublishCaptureGroups(captureSlotCount, matchStart, matchEnd,
            captureRanges, input, false, control);
    }

    internal static Pcre2GroupData[] PublishSliceCaptures(
        int captureSlotCount, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds,
        ref Utf8SliceInvocation invocation, Pcre2GroupData[]? groups = null)
    {
        var control = new Pcre2ResultControl(ref invocation);
        // The shared VM admits scalar starts and its consuming instructions
        // preserve the boundary of every capture edge.
        return PublishCaptureGroups(captureSlotCount, matchStart, matchEnd,
            new(captureStarts, captureEnds), default, true, control, groups);
    }

    private static Pcre2GroupData[] PublishCaptureGroups(
        int captureSlotCount, int matchStart, int matchEnd,
        scoped Pcre2CaptureRanges captureRanges,
        ReadOnlySpan<byte> input, bool scalarRanges,
        scoped Pcre2ResultControl budget, Pcre2GroupData[]? groups = null)
    {
        var groupBytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(captureSlotCount);
        var needsAllocation = groups is null;
        if (needsAllocation) budget.ReserveWorking(groupBytes);
        var completed = false;
        try
        {
            budget.CheckWorkspace();
            budget.TryChargeResultBytes(groupBytes);
            groups ??= captureSlotCount == 0 ? [] : new Pcre2GroupData[captureSlotCount];
            for (var slot = 0; slot < captureSlotCount; slot++)
            {
                if ((slot & 63) == 0) budget.CheckWorkspace();
                if (!captureRanges.TryGetEdges(slot, matchStart, matchEnd, scalarRanges, out var start, out var end))
                {
                    groups[slot] = new Pcre2GroupData { Number = slot };
                    continue;
                }
                groups[slot] = new Pcre2GroupData
                {
                    Number = slot,
                    Success = true,
                    StartOffsetInBytes = start,
                    EndOffsetInBytes = end,
                    Utf8SliceIsWellFormed = Pcre2CaptureRanges.IsWellFormed(input, start, end, scalarRanges)
                };
            }
            budget.CheckWorkspace();
            completed = true;
            return groups;
        }
        finally { if (needsAllocation && !completed) budget.ReleaseWorking(groupBytes); }
    }

}
