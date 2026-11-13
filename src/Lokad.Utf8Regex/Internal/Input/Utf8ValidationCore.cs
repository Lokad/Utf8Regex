using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;

namespace Lokad.Utf8Regex.Internal.Input;

internal static partial class Utf8ValidationCore
{
    // Metadata consumers need an exact UTF-16 length and scalar flags. The
    // validity-only entry point lives in Utf8InputAnalyzer and does not compute
    // coordinates; this decoder also supplies its first-error offsets.
    public static bool TryValidate(
        ReadOnlySpan<byte> input,
        out Utf8ValidationResult validation,
        out int errorOffset)
        => TryValidate(input, out validation, out errorOffset, out _);

    public static bool TryValidate(
        ReadOnlySpan<byte> input,
        out Utf8ValidationResult validation,
        out int errorOffset,
        out bool containsKelvinSign)
    {
        var utf16Length = 0;
        var containsSupplementaryScalars = false;
        containsKelvinSign = false;
        var isAscii = true;
        var offset = 0;
        while (offset < input.Length)
        {
            var asciiStart = offset;
            offset = DrainAscii(input, offset);
            utf16Length += offset - asciiStart;

            if (offset >= input.Length)
            {
                validation = new Utf8ValidationResult(
                    input.Length,
                    utf16Length,
                    isAscii,
                    containsSupplementaryScalars);
                errorOffset = -1;
                return true;
            }

            isAscii = false;
            var b0 = input[offset];
            if (b0 < 0xC2)
            {
                validation = default;
                errorOffset = offset;
                return false;
            }

            if (b0 < 0xE0)
            {
                if (offset + 1 >= input.Length || !IsContinuationByte(input[offset + 1]))
                {
                    validation = default;
                    errorOffset = offset;
                    return false;
                }

                offset += 2;
                utf16Length++;
                offset = ConsumeTwoByteRun(input, offset, ref utf16Length);

                continue;
            }

            if (b0 < 0xF0)
            {
                if (offset + 2 >= input.Length)
                {
                    validation = default;
                    errorOffset = offset;
                    return false;
                }

                var b1 = input[offset + 1];
                var b2 = input[offset + 2];
                var validSecond =
                    b0 == 0xE0 ? b1 is >= 0xA0 and <= 0xBF :
                    b0 == 0xED ? b1 is >= 0x80 and <= 0x9F :
                    IsContinuationByte(b1);

                if (!validSecond || !IsContinuationByte(b2))
                {
                    validation = default;
                    errorOffset = offset;
                    return false;
                }

                containsKelvinSign |= b0 == 0xE2 && b1 == 0x84 && b2 == 0xAA;

                offset += 3;
                utf16Length++;
                if (b0 is >= 0xE1 and <= 0xEC or >= 0xEE and <= 0xEF)
                    offset = ConsumeThreeByteRun(input, offset, ref utf16Length, ref containsKelvinSign);

                continue;
            }

            if (b0 < 0xF5)
            {
                if (offset + 3 >= input.Length)
                {
                    validation = default;
                    errorOffset = offset;
                    return false;
                }

                var b1 = input[offset + 1];
                var b2 = input[offset + 2];
                var b3 = input[offset + 3];
                var validSecond =
                    b0 == 0xF0 ? b1 is >= 0x90 and <= 0xBF :
                    b0 == 0xF4 ? b1 is >= 0x80 and <= 0x8F :
                    IsContinuationByte(b1);

                if (!validSecond || !IsContinuationByte(b2) || !IsContinuationByte(b3))
                {
                    validation = default;
                    errorOffset = offset;
                    return false;
                }

                offset += 4;
                containsSupplementaryScalars = true;
                utf16Length += 2;

                continue;
            }

            validation = default;
            errorOffset = offset;
            return false;
        }

        validation = new Utf8ValidationResult(
            input.Length,
            utf16Length,
            isAscii,
            containsSupplementaryScalars);
        errorOffset = -1;
        return true;
    }

    private static int DrainAscii(ReadOnlySpan<byte> input, int offset)
    {
        // Match each checked Slice's end admission. Invocation callers retain
        // their original 2 KiB checkpoints; no validation block is widened.
        if (Vector512.IsHardwareAccelerated)
        {
            while ((ulong)(uint)offset + (uint)Vector512<byte>.Count <= (uint)input.Length)
            {
                var mask = MemoryMarshal.Read<Vector512<byte>>(
                    input.Slice(offset, Vector512<byte>.Count)).ExtractMostSignificantBits();
                if (mask != 0) return offset + BitOperations.TrailingZeroCount(mask);
                offset += Vector512<byte>.Count;
            }
        }

        if (Vector256.IsHardwareAccelerated)
        {
            while ((ulong)(uint)offset + (uint)Vector256<byte>.Count <= (uint)input.Length)
            {
                var mask = MemoryMarshal.Read<Vector256<byte>>(
                    input.Slice(offset, Vector256<byte>.Count)).ExtractMostSignificantBits();
                if (mask != 0) return offset + BitOperations.TrailingZeroCount(mask);
                offset += Vector256<byte>.Count;
            }
        }

        if (Vector128.IsHardwareAccelerated)
        {
            while ((ulong)(uint)offset + (uint)Vector128<byte>.Count <= (uint)input.Length)
            {
                var mask = MemoryMarshal.Read<Vector128<byte>>(
                    input.Slice(offset, Vector128<byte>.Count)).ExtractMostSignificantBits();
                if (mask != 0) return offset + BitOperations.TrailingZeroCount(mask);
                offset += Vector128<byte>.Count;
            }
        }

        while ((ulong)(uint)offset + sizeof(ulong) <= (uint)input.Length)
        {
            var mask = BinaryPrimitives.ReadUInt64LittleEndian(
                input.Slice(offset, sizeof(ulong))) & 0x8080_8080_8080_8080ul;
            if (mask != 0) return offset + (BitOperations.TrailingZeroCount(mask) >> 3);
            offset += sizeof(ulong);
        }

        while ((ulong)(uint)offset + sizeof(uint) <= (uint)input.Length)
        {
            var mask = BinaryPrimitives.ReadUInt32LittleEndian(
                input.Slice(offset, sizeof(uint))) & 0x8080_8080u;
            if (mask != 0) return offset + (BitOperations.TrailingZeroCount(mask) >> 3);
            offset += sizeof(uint);
        }

        while (offset < input.Length && input[offset] < 0x80) offset++;
        return offset;
    }

    private static int ConsumeTwoByteRun(ReadOnlySpan<byte> input, int offset, ref int utf16Length)
    {
        while (offset + 1 < input.Length)
        {
            var b0 = input[offset];
            if (b0 < 0xC2 || b0 >= 0xE0 || !IsContinuationByte(input[offset + 1]))
            {
                return offset;
            }

            offset += 2;
            utf16Length++;
        }

        return offset;
    }

    private static int ConsumeThreeByteRun(ReadOnlySpan<byte> input, int offset, ref int utf16Length, ref bool containsKelvinSign)
    {
        while (offset + 2 < input.Length)
        {
            var b0 = input[offset];
            if ((b0 < 0xE1 || b0 > 0xEF || b0 == 0xED) ||
                !IsContinuationByte(input[offset + 1]) ||
                !IsContinuationByte(input[offset + 2]))
            {
                return offset;
            }

            containsKelvinSign |= b0 == 0xE2 && input[offset + 1] == 0x84 && input[offset + 2] == 0xAA;
            offset += 3;
            utf16Length++;
        }

        return offset;
    }

    private static bool IsContinuationByte(byte value) => (value & 0xC0) == 0x80;
}
