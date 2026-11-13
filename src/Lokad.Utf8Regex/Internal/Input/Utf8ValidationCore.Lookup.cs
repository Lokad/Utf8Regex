using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Input;

// Checked-span adaptation of Keiser/Lemire's lookup validation, informed by
// simdutf/SimdUnicode d20b63710e8893dede79890f29fd5ae6745d3e2d (MIT).
// Copyright (c) 2023 SimdUnicode authors. See the repository's packed LICENSE.txt.
internal static partial class Utf8ValidationCore
{
    // The three lookups intersect fault bits for the previous byte's high/low
    // nibbles and the current byte's high nibble. Bit7 denotes consecutive
    // continuations; the preceding second/third byte decides if those are needed.
    private static Vector128<byte> LookupPreviousHigh => Vector128.Create(
        (byte)2, 2, 2, 2, 2, 2, 2, 2, 128, 128, 128, 128, 33, 1, 21, 73);
    private static Vector128<byte> LookupPreviousLow => Vector128.Create(
        (byte)231, 163, 131, 131, 139, 203, 203, 203, 203, 203, 203, 203, 203, 219, 203, 203);
    private static Vector128<byte> LookupCurrentHigh => Vector128.Create(
        (byte)1, 1, 1, 1, 1, 1, 1, 1, 230, 174, 186, 186, 1, 1, 1, 1);

    internal static bool IsValidLookup(ReadOnlySpan<byte> input)
        => Vector512.IsHardwareAccelerated && Avx512BW.IsSupported
            ? IsValidLookup512(input) : IsValidLookup256(input);

    internal static bool IsValidLookup256(ReadOnlySpan<byte> input)
    {
        if (input.Length < Vector256<byte>.Count + 3)
            return TryValidate(input, false, out _, out _);
        var offset = LookupStart(input);
        if (offset < 0) return false;
        var high = Vector256.Create(LookupPreviousHigh, LookupPreviousHigh);
        var low = Vector256.Create(LookupPreviousLow, LookupPreviousLow);
        var currentHigh = Vector256.Create(LookupCurrentHigh, LookupCurrentHigh);
        var nibble = Vector256.Create((byte)15);
        var continuation = Vector256.Create((byte)128);
        while (offset <= input.Length - Vector256<byte>.Count)
        {
            // Admit all four overlapping reads once. Each read's available
            // width follows directly from this fixed-size window.
            var window = input.Slice(offset - 3, Vector256<byte>.Count + 3);
            var current = MemoryMarshal.Read<Vector256<byte>>(window[3..]);
            if (current.ExtractMostSignificantBits() == 0)
            {
                if (LookupPending(input, offset)) return false;
            }
            else
            {
                var previous = MemoryMarshal.Read<Vector256<byte>>(window[2..]);
                var previous2 = MemoryMarshal.Read<Vector256<byte>>(window[1..]);
                var previous3 = MemoryMarshal.Read<Vector256<byte>>(window);
                var faults = Avx2.Shuffle(high, Avx2.ShiftRightLogical(previous.AsUInt16(), 4).AsByte() & nibble)
                    & Avx2.Shuffle(low, previous & nibble)
                    & Avx2.Shuffle(currentHigh, Avx2.ShiftRightLogical(current.AsUInt16(), 4).AsByte() & nibble);
                var required = (Avx2.SubtractSaturate(previous2, Vector256.Create((byte)0x60))
                    | Avx2.SubtractSaturate(previous3, Vector256.Create((byte)0x70))) & continuation;
                if (!Avx2.TestZ(faults ^ required, faults ^ required)) return false;
            }
            offset += Vector256<byte>.Count;
        }
        return LookupTail(input, offset);
    }

    internal static bool IsValidLookup512(ReadOnlySpan<byte> input)
    {
        if (input.Length < Vector512<byte>.Count + 3)
            return TryValidate(input, false, out _, out _);
        var offset = LookupStart(input);
        if (offset < 0) return false;
        var previousHigh256 = Vector256.Create(LookupPreviousHigh, LookupPreviousHigh);
        var previousLow256 = Vector256.Create(LookupPreviousLow, LookupPreviousLow);
        var currentHigh256 = Vector256.Create(LookupCurrentHigh, LookupCurrentHigh);
        var high = Vector512.Create(previousHigh256, previousHigh256);
        var low = Vector512.Create(previousLow256, previousLow256);
        var currentHigh = Vector512.Create(currentHigh256, currentHigh256);
        var nibble = Vector512.Create((byte)15);
        var continuation = Vector512.Create((byte)128);
        while (offset <= input.Length - Vector512<byte>.Count)
        {
            var window = input.Slice(offset - 3, Vector512<byte>.Count + 3);
            var current = MemoryMarshal.Read<Vector512<byte>>(window[3..]);
            if (current.ExtractMostSignificantBits() == 0)
            {
                if (LookupPending(input, offset)) return false;
            }
            else
            {
                var previous = MemoryMarshal.Read<Vector512<byte>>(window[2..]);
                var previous2 = MemoryMarshal.Read<Vector512<byte>>(window[1..]);
                var previous3 = MemoryMarshal.Read<Vector512<byte>>(window);
                var faults = Avx512BW.Shuffle(high, Avx512BW.ShiftRightLogical(previous.AsUInt16(), 4).AsByte() & nibble)
                    & Avx512BW.Shuffle(low, previous & nibble)
                    & Avx512BW.Shuffle(currentHigh, Avx512BW.ShiftRightLogical(current.AsUInt16(), 4).AsByte() & nibble);
                var required = (Avx512BW.SubtractSaturate(previous2, Vector512.Create((byte)0x60))
                    | Avx512BW.SubtractSaturate(previous3, Vector512.Create((byte)0x70))) & continuation;
                if ((faults ^ required) != Vector512<byte>.Zero) return false;
            }
            offset += Vector512<byte>.Count;
        }
        return LookupTail(input, offset);
    }

    private static int LookupStart(ReadOnlySpan<byte> input)
    {
        var offset = 0;
        // A few complete scalars supply the preceding three bytes for all
        // later windows. This also rejects an invalid initial continuation.
        while (offset < 3)
        {
            if (Rune.DecodeFromUtf8(input[offset..], out _, out var width) != OperationStatus.Done) return -1;
            offset += width;
        }
        return offset;
    }

    private static bool LookupPending(ReadOnlySpan<byte> input, int offset)
        => input[offset - 1] >= 0xC0 || input[offset - 2] >= 0xE0 || input[offset - 3] >= 0xF0;

    private static bool LookupTail(ReadOnlySpan<byte> input, int offset)
    {
        // Include the preceding scalar even at exact EOF: its lead may need
        // continuations beyond the last vector. Reuse the existing scalar/error
        // authority rather than introducing a separate tail decoder.
        var floor = offset - 1;
        while (floor > 0 && (input[floor] & 0xC0) == 0x80) floor--;
        return TryValidate(input[floor..], false, out _, out _);
    }
}
