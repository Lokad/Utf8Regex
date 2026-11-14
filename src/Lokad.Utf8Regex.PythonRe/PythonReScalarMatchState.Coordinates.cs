using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
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
        start = CoordinateOffset(input, byteStart).Scalars;
        end = CoordinateOffset(input, byteEnd).Scalars;
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

    private void PrepareCaptureProjection(ReadOnlySpan<byte> input)
    {
        Checkpoint();
        var end = EndOffsetInBytes;
        for (var group = 1; group <= _program.GroupCount; group++)
        {
            if ((group & 255) == 0) Checkpoint();
            if (TryGetCaptureByteRange(group, out _, out var captureEnd)) end = Math.Max(end, captureEnd);
        }
        PrepareCoordinates(input, end);
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
            reservation = Utf8ConstructionBudget.ArrayBytes<(int, int)>(capacity);
            if (_invocation.TryReserveWorking(reservation) != Utf8ExecutionLimitKind.None)
                throw ResourceFailure();
        }
        try
        {
            Checkpoint();
            if (reservation != 0)
            {
                var grown = new (int Scalars, int Utf16)[capacity];
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
                if (to <= _asciiPrefixEnd) { prefixes![block] = (to, to); continue; }
                var units = CountCoordinateUnits(input.Slice(to - CoordinatePrefixBlockBytes, CoordinatePrefixBlockBytes));
                var previous = prefixes![block - 1];
                prefixes[block] = (previous.Scalars + units.Scalars, previous.Utf16 + units.Utf16);
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

    private (int Scalars, int Utf16) CoordinateOffset(ReadOnlySpan<byte> input, int byteOffset)
    {
        if (byteOffset <= _asciiPrefixEnd) return (byteOffset, byteOffset);
        var block = byteOffset / CoordinatePrefixBlockBytes;
        if (_coordinatePrefixes is null) return CountCoordinateUnits(input[..byteOffset]);
        var count = _coordinatePrefixes[block];
        var from = block * CoordinatePrefixBlockBytes;
        var tail = CountCoordinateUnits(input.Slice(from, byteOffset - from));
        return (count.Scalars + tail.Scalars, count.Utf16 + tail.Utf16);
    }

    private (int Scalars, int Utf16) CountCoordinateUnits(ReadOnlySpan<byte> bytes)
    {
        var scalars = 0;
        var supplementary = 0;
        for (var offset = 0; offset < bytes.Length;)
        {
            // Valid unchanged UTF-8 is the profile's input contract. A leading
            // four-byte octet adds one UTF-16 unit. Counts compose even when an
            // index block cuts through a scalar; queried captures end on scalars.
            Checkpoint();
            var block = bytes.Slice(offset, Math.Min(32, bytes.Length - offset));
            if (Vector256.IsHardwareAccelerated && block.Length == Vector256<byte>.Count)
            {
                var current = MemoryMarshal.Read<Vector256<byte>>(block);
                var continuations = Vector256.Equals(current & Vector256.Create((byte)0xc0), Vector256.Create((byte)0x80));
                scalars += Vector256<byte>.Count - BitOperations.PopCount(continuations.ExtractMostSignificantBits());
                supplementary += BitOperations.PopCount(Vector256.GreaterThanOrEqual(current, Vector256.Create((byte)0xf0)).ExtractMostSignificantBits());
            }
            else
            {
                foreach (var value in block)
                {
                    if ((value & 0xc0) != 0x80) scalars++;
                    if (value >= 0xf0) supplementary++;
                }
            }
            offset += block.Length;
        }
        return (scalars, scalars + supplementary);
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
