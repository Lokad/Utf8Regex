using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.PythonRe;

internal partial struct PythonReScalarMatchState
{
    private const int CoordinatePrefixBlockBytes = 256;

    internal bool TryGetScalarRange(ReadOnlySpan<byte> input, int groupNumber, out int start, out int end)
    {
        start = end = -1;
        Checkpoint();
        if (!TryGetCaptureByteRange(groupNumber, out var byteStart, out var byteEnd)) return false;
        PrepareCoordinates(input, byteEnd);
        start = ScalarOffset(input, byteStart);
        end = ScalarOffset(input, byteEnd);
        Checkpoint();
        return true;
    }

    // This index belongs to discovery, so span and durable operations share one
    // coordinate implementation and one deterministically released working loan.
    internal void PrepareCoordinates(ReadOnlySpan<byte> input, int end)
    {
        if (end <= _asciiPrefixEnd) return;
        if (_coordinatePrefixes is null)
        {
            var firstNonAscii = _asciiPrefixEnd;
            var breach = Utf8SliceRunner.TrySeekAsciiCandidate(input[..end], ref firstNonAscii,
                128, 255, ref _invocation);
            if (breach != Utf8ExecutionLimitKind.None) throw ResourceFailure();
            _asciiPrefixEnd = firstNonAscii < 0 ? end : firstNonAscii;
            if (firstNonAscii < 0 || end <= CoordinatePrefixBlockBytes) return;
        }
        EnsureCoordinatePrefixes(input, end);
    }

    private void EnsureCoordinatePrefixes(ReadOnlySpan<byte> input, int end)
    {
        var count = end / CoordinatePrefixBlockBytes + 1;
        if (_coordinatePrefixCount >= count) return;
        var prefixes = _coordinatePrefixes;
        var capacity = prefixes?.Length ?? 0;
        var reservation = 0UL;
        if (prefixes is null || capacity < count)
        {
            capacity = Math.Min(input.Length / CoordinatePrefixBlockBytes + 1,
                Math.Max(count, Math.Max(2, capacity * 2)));
            reservation = Utf8ConstructionBudget.ArrayBytes<int>(capacity);
            if (_invocation.TryReserveWorking(reservation) != Utf8ExecutionLimitKind.None)
                throw ResourceFailure();
        }
        try
        {
            Checkpoint();
            if (reservation != 0)
            {
                var grown = new int[capacity];
                for (var offset = 0; offset < _coordinatePrefixCount;)
                {
                    Checkpoint();
                    var length = Math.Min(256, _coordinatePrefixCount - offset);
                    prefixes!.AsSpan(offset, length).CopyTo(grown.AsSpan(offset, length));
                    offset += length;
                }
                prefixes = grown;
            }
            for (var block = Math.Max(1, _coordinatePrefixCount); block < count; block++)
            {
                if ((block & 255) == 0) Checkpoint();
                var to = block * CoordinatePrefixBlockBytes;
                if (to <= _asciiPrefixEnd) { prefixes![block] = to; continue; }
                var units = CountScalarUnits(input.Slice(to - CoordinatePrefixBlockBytes, CoordinatePrefixBlockBytes));
                var previous = prefixes![block - 1];
                prefixes[block] = previous + units;
            }
            Checkpoint();
        }
        catch { _invocation.Ledger.Release(reservation); throw; }
        _coordinatePrefixes = prefixes;
        _coordinatePrefixCount = count;
        if (reservation != 0)
        {
            var previous = _coordinatePrefixReservation;
            _coordinatePrefixReservation = reservation;
            _invocation.Ledger.Release(previous);
        }
    }

    private int ScalarOffset(ReadOnlySpan<byte> input, int byteOffset)
    {
        if (byteOffset <= _asciiPrefixEnd) return byteOffset;
        var block = byteOffset / CoordinatePrefixBlockBytes;
        if (_coordinatePrefixes is null) return CountScalarUnits(input[..byteOffset]);
        var count = _coordinatePrefixes[block];
        var from = block * CoordinatePrefixBlockBytes;
        var tail = CountScalarUnits(input.Slice(from, byteOffset - from));
        return count + tail;
    }

    private int CountScalarUnits(ReadOnlySpan<byte> bytes)
    {
        var scalars = 0;
        for (var offset = 0; offset < bytes.Length;)
        {
            // Valid UTF-8 lead counts compose even when a block cuts a scalar.
            Checkpoint();
            var block = bytes.Slice(offset, Math.Min(32, bytes.Length - offset));
            scalars += Utf8SliceBufferWork.CountValidUtf8Units(block).Scalars;
            offset += block.Length;
        }
        return scalars;
    }

    private void ReleaseCoordinatePrefixes()
    {
        _coordinatePrefixes = null;
        _invocation.Ledger.Release(_coordinatePrefixReservation);
        _coordinatePrefixReservation = 0;
        _coordinatePrefixCount = 0;
        _asciiPrefixEnd = 0;
    }
}
