using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2Runner
{
    // The final array stays charged until its caller publishes the result.
    internal static Pcre2GroupData[] PublishCaptures(
        int captureSlotCount, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds,
        ref Utf8ValidatedInput input, ref Pcre2ResourceBudget budget)
    {
        var control = new Pcre2ResultControl(ref budget);
        return PublishCaptureGroups(captureSlotCount, matchStart, matchEnd,
            captureStarts, captureEnds, 0, false, ref input, control);
    }

    internal static Pcre2GroupData[] PublishSliceCaptures(
        int captureSlotCount, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds,
        ref Utf8ValidatedInput input, ref Utf8SliceInvocation invocation)
    {
        var control = new Pcre2ResultControl(ref invocation);
        return PublishCaptureGroups(captureSlotCount, matchStart, matchEnd,
            captureStarts, captureEnds, -1, true, ref input, control);
    }

    private static Pcre2GroupData[] PublishCaptureGroups(
        int captureSlotCount, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds,
        int slotOffset, bool normalizeDirection,
        ref Utf8ValidatedInput input, scoped Pcre2ResultControl budget)
    {
        var groupBytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(captureSlotCount);
        budget.ReserveWorking(groupBytes);
        var completed = false;
        try
        {
            budget.ChargeWorkspaceStep();
            budget.TryChargeResultBytes(groupBytes);
            var groups = captureSlotCount == 0 ? [] : new Pcre2GroupData[captureSlotCount];
            for (var slot = 0; slot < captureSlotCount; slot++)
            {
                if ((slot & 63) == 0) budget.ChargeWorkspaceStep();
                if (!ReadCaptureEdges(slot, matchStart, matchEnd, captureStarts,
                    captureEnds, slotOffset, normalizeDirection, out var start, out var end))
                {
                    groups[slot] = new Pcre2GroupData { Number = slot };
                    continue;
                }
                groups[slot] = new Pcre2GroupData
                {
                    Number = slot, Success = true,
                    StartOffsetInBytes = start, EndOffsetInBytes = end,
                    Utf8SliceIsWellFormed = start <= end &&
                        input.IsScalarBoundary(new Utf8BytePosition(start)) &&
                        input.IsScalarBoundary(new Utf8BytePosition(end)),
                };
            }
            budget.ChargeWorkspaceStep();
            completed = true;
            return groups;
        }
        finally { if (!completed) budget.ReleaseWorking(groupBytes); }
    }

    private static bool ReadCaptureEdges(int slot, int matchStart, int matchEnd,
        ReadOnlySpan<int> starts, ReadOnlySpan<int> ends, int slotOffset,
        bool normalizeDirection, out int start, out int end)
    {
        if (slot == 0) { start = matchStart; end = matchEnd; return true; }
        start = starts[slot + slotOffset];
        end = ends[slot + slotOffset];
        if (start < 0 || end < 0) return false;
        if (normalizeDirection && start > end) (start, end) = (end, start);
        return end >= start;
    }

}
