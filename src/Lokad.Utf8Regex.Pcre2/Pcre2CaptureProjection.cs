using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

internal static partial class Pcre2Runner
{
    // The final array stays charged until its caller publishes the result.
    // Only temporary endpoint storage returns to the bounded shared pool.
    internal static Pcre2GroupData[] ProjectCaptures(
        int captureSlotCount, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds,
        ref Utf8ValidatedInput input, ref Pcre2ResourceBudget budget)
    {
        var projectionBudget = new Pcre2CaptureProjectionBudget(ref budget);
        return ProjectCaptureGroups(captureSlotCount, matchStart, matchEnd,
            captureStarts, captureEnds, 0, false, ref input, projectionBudget);
    }

    internal static Pcre2GroupData[] ProjectSliceCaptures(
        int captureSlotCount, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds,
        ref Utf8ValidatedInput input, ref Utf8SliceInvocation invocation)
    {
        var projectionBudget = new Pcre2CaptureProjectionBudget(ref invocation);
        return ProjectCaptureGroups(captureSlotCount, matchStart, matchEnd,
            captureStarts, captureEnds, -1, true, ref input, projectionBudget);
    }

    private static Pcre2GroupData[] ProjectCaptureGroups(
        int captureSlotCount, int matchStart, int matchEnd,
        ReadOnlySpan<int> captureStarts, ReadOnlySpan<int> captureEnds,
        int slotOffset, bool normalizeDirection,
        ref Utf8ValidatedInput input, scoped Pcre2CaptureProjectionBudget budget)
    {
        const int MaximumStackEndpointCount = 64;
        var endpointCapacity = checked(captureSlotCount * 2);
        var stagingLength = checked(endpointCapacity * 2);
        var groupBytes = Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(captureSlotCount);
        var stagingBytes = endpointCapacity <= MaximumStackEndpointCount ? 0
            : Utf8ConstructionBudget.ArrayBytes<int>(stagingLength);
        budget.ReserveWorking(checked(groupBytes + stagingBytes));
        int[]? rentedStaging = null;
        var completed = false;
        try
        {
            scoped Span<int> staging;
            if (stagingBytes == 0) staging = stackalloc int[stagingLength];
            else
            {
                budget.ChargeWorkspaceStep();
                rentedStaging = Utf8ExactStatePool<int>.Rent(stagingLength, budget.CanRetainWorkspace);
                staging = rentedStaging;
            }
            var endpoints = staging[..endpointCapacity];
            var utf16Endpoints = staging[endpointCapacity..];
            var endpointCount = 0;
            for (var slot = 0; slot < captureSlotCount; slot++)
            {
                if ((slot & 63) == 0) budget.ChargeWorkspaceStep();
                var hasCapture = ReadCaptureEdges(slot, matchStart, matchEnd,
                    captureStarts, captureEnds, slotOffset, normalizeDirection,
                    out var captureStart, out var captureEnd);
                if (!hasCapture) continue;
                if (input.IsScalarBoundary(new Utf8BytePosition(captureStart))) endpoints[endpointCount++] = captureStart;
                if (input.IsScalarBoundary(new Utf8BytePosition(captureEnd))) endpoints[endpointCount++] = captureEnd;
            }
            SortCaptureEndpoints(endpoints[..endpointCount], budget);
            var uniqueCount = 0;
            for (var index = 0; index < endpointCount; index++)
            {
                if ((index & 63) == 0) budget.ChargeWorkspaceStep();
                if (uniqueCount == 0 || endpoints[index] != endpoints[uniqueCount - 1]) endpoints[uniqueCount++] = endpoints[index];
            }
            var sortedEndpoints = endpoints[..uniqueCount];
            var projection = input.CreateProjectionCursor();
            for (var index = 0; index < uniqueCount; index++)
                utf16Endpoints[index] = ProjectCaptureEndpoint(ref input, ref projection, sortedEndpoints[index], budget);

            budget.ChargeWorkspaceStep();
            budget.TryChargeResultBytes(groupBytes);
            var groups = captureSlotCount == 0 ? [] : new Pcre2GroupData[captureSlotCount];
            for (var slot = 0; slot < captureSlotCount; slot++)
            {
                if ((slot & 63) == 0) budget.ChargeWorkspaceStep();
                var hasCapture = ReadCaptureEdges(slot, matchStart, matchEnd,
                    captureStarts, captureEnds, slotOffset, normalizeDirection,
                    out var captureStart, out var captureEnd);
                if (!hasCapture)
                {
                    groups[slot] = new Pcre2GroupData { Number = slot, Success = false };
                    continue;
                }
                var startIndex = sortedEndpoints.BinarySearch(captureStart);
                var endIndex = sortedEndpoints.BinarySearch(captureEnd);
                var hasUtf16Projection = startIndex >= 0 && endIndex >= 0;
                groups[slot] = new Pcre2GroupData
                {
                    Number = slot,
                    Success = true,
                    StartOffsetInBytes = captureStart,
                    EndOffsetInBytes = captureEnd,
                    StartOffsetInUtf16 = hasUtf16Projection ? utf16Endpoints[startIndex] : 0,
                    EndOffsetInUtf16 = hasUtf16Projection ? utf16Endpoints[endIndex] : 0,
                    CoordinateFlagsSpecified = true,
                    Utf8SliceIsWellFormed = captureStart <= captureEnd && hasUtf16Projection,
                    Utf16ProjectionIsExact = hasUtf16Projection,
                };
            }
            completed = true;
            return groups;
        }
        finally
        {
            if (rentedStaging is not null) Utf8ExactStatePool<int>.Return(rentedStaging, budget.CanRetainWorkspace);
            budget.ReleaseWorking(stagingBytes);
            if (!completed) budget.ReleaseWorking(groupBytes);
        }
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

    internal static int ProjectCaptureEndpoint(ref Utf8ValidatedInput input,
        ref Utf8ProjectionCursor projection, int endpoint, scoped Pcre2CaptureProjectionBudget budget)
    {
        if (budget.IsUnmetered || input.IsAscii)
        {
            budget.ChargeWorkspaceStep();
            return projection.Project(new Utf8BytePosition(endpoint)).Value;
        }
        // Project in bounded byte windows; every cut remains a whole scalar.
        while (projection.BytesConsumed < endpoint)
        {
            budget.ChargeWorkspaceStep();
            var end = projection.BytesConsumed + Math.Min(2048, endpoint - projection.BytesConsumed);
            while (!input.IsScalarBoundary(new Utf8BytePosition(end))) end--;
            projection.Project(new Utf8BytePosition(end));
        }
        return projection.Project(new Utf8BytePosition(endpoint)).Value;
    }

    private static void SortCaptureEndpoints(scoped Span<int> values, scoped Pcre2CaptureProjectionBudget budget)
    {
        if (budget.IsUnmetered) { values.Sort(); return; }
        for (var root = values.Length / 2 - 1; root >= 0; root--) SiftCaptureEndpoint(values, root, values.Length, budget);
        for (var end = values.Length - 1; end > 0; end--)
        {
            budget.ChargeWorkspaceStep();
            (values[0], values[end]) = (values[end], values[0]);
            SiftCaptureEndpoint(values, 0, end, budget);
        }
    }

    private static void SiftCaptureEndpoint(scoped Span<int> values, int root, int count, scoped Pcre2CaptureProjectionBudget budget)
    {
        while (root < count / 2)
        {
            budget.ChargeWorkspaceStep();
            var child = checked(root * 2 + 1);
            if (child + 1 < count && values[child] < values[child + 1]) child++;
            if (values[root] >= values[child]) return;
            (values[root], values[child]) = (values[child], values[root]);
            root = child;
        }
    }
}
