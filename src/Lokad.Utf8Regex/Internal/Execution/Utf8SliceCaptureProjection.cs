using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Admitted conversion of byte capture histories to original UTF-16
/// positions. Callback drivers retain this reservation through evaluation;
/// detailed match results transfer the arrays to the caller at return.</summary>
internal struct Utf8SliceCaptureProjection
{
    private ulong _reservedBytes;
    internal int[] Starts;
    internal int[] Lengths;
    internal int[][] CaptureStarts;
    internal int[][] CaptureLengths;

    internal static Utf8SliceCaptureProjection Create(Utf8SliceSearchOutcome outcome, Utf8BoundaryMap executionMap, scoped ref Utf8SliceInvocation invocation)
    {
        if (outcome.CaptureStarts is not { } byteStarts || outcome.CaptureLengths is not { } byteLengths)
            throw new InvalidOperationException("Owned detailed match is missing its capture histories.");
        var groups = byteStarts.Length + 1;
        ulong reservation = (ulong)groups * 24 + 8;
        for (var group = 0; group < byteStarts.Length; group++)
        {
            if ((group & 255) == 0) Utf8SliceBufferWork.Checkpoint(ref invocation);
            reservation += (ulong)byteStarts[group].Length * 8;
        }
        if (!invocation.Ledger.TryReserve(reservation))
            throw new InvalidOperationException("Owned capture projection exceeded the invocation storage budget.");
        try
        {
            var projection = new Utf8SliceCaptureProjection
            {
                Starts = new int[groups], Lengths = new int[groups],
                CaptureStarts = new int[groups][], CaptureLengths = new int[groups][],
                _reservedBytes = reservation,
            };
            var startUnit = executionMap.GetUtf16OffsetForByteOffset(outcome.StartByte);
            var endUnit = executionMap.GetUtf16OffsetForByteOffset(outcome.EndByte);
            projection.Starts[0] = startUnit;
            projection.Lengths[0] = endUnit - startUnit;
            projection.CaptureStarts[0] = [startUnit];
            projection.CaptureLengths[0] = [endUnit - startUnit];
            var converted = 0;
            for (var group = 1; group < groups; group++)
            {
                if ((group & 255) == 0) Utf8SliceBufferWork.Checkpoint(ref invocation);
                var starts = projection.CaptureStarts[group] = new int[byteStarts[group - 1].Length];
                var lengths = projection.CaptureLengths[group] = new int[starts.Length];
                for (var capture = 0; capture < starts.Length; capture++)
                {
                    if ((converted++ & 255) == 0) Utf8SliceBufferWork.Checkpoint(ref invocation);
                    starts[capture] = executionMap.GetUtf16OffsetForByteOffset(byteStarts[group - 1][capture]);
                    lengths[capture] = executionMap.GetUtf16OffsetForByteOffset(byteStarts[group - 1][capture] + byteLengths[group - 1][capture]) - starts[capture];
                }
                projection.Starts[group] = starts.Length == 0 ? -1 : starts[^1];
                projection.Lengths[group] = lengths.Length == 0 ? 0 : lengths[^1];
            }
            return projection;
        }
        catch
        {
            invocation.Ledger.Release(reservation);
            throw;
        }
    }

    internal readonly Utf8MatchContext CreateContext(ReadOnlySpan<byte> input, string? decoded, Utf8BoundaryMap originalMap, Utf8SliceProgram program)
        => new(input, decoded, originalMap, program.GroupNames, Starts[0], Lengths[0], Starts, Lengths, CaptureStarts, CaptureLengths, program.CaptureCatalog);

    internal void Release(ref Utf8SliceInvocation invocation)
    {
        invocation.Ledger.Release(_reservedBytes);
        _reservedBytes = 0;
    }
}
