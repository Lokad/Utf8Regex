using System.Buffers.Binary;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;

namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8ValidationCore
{
    public static Utf8ValidationResult Validate(ReadOnlySpan<byte> input)
    {
        TryValidate(input, computeUtf16Length: true, out var validation, out _);
        return validation;
    }

    public static void ThrowIfInvalidOnly(ReadOnlySpan<byte> input)
    {
        TryValidate(input, computeUtf16Length: false, out _, out _);
    }

    public static bool TryValidate(
        ReadOnlySpan<byte> input,
        bool computeUtf16Length,
        out Utf8ValidationResult validation,
        out int errorOffset)
    {
        var utf16Length = 0;
        var containsSupplementaryScalars = false;
        var offset = 0;

        while (offset < input.Length)
        {
            var asciiStart = offset;
            offset = DrainAscii(input, offset);
            if (computeUtf16Length)
            {
                utf16Length += offset - asciiStart;
            }

            if (offset >= input.Length)
            {
                validation = new Utf8ValidationResult(
                    input.Length,
                    computeUtf16Length ? utf16Length : 0,
                    isAscii: asciiStart == 0,
                    containsSupplementaryScalars);
                errorOffset = -1;
                return true;
            }

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
                if (computeUtf16Length)
                {
                    utf16Length++;
                    offset = ConsumeTwoByteRun(input, offset, ref utf16Length);
                }
                else
                {
                    offset = ConsumeTwoByteRun(input, offset);
                }

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

                offset += 3;
                if (computeUtf16Length)
                {
                    utf16Length++;
                }

                if (b0 is >= 0xE1 and <= 0xEC or >= 0xEE and <= 0xEF)
                {
                    if (computeUtf16Length)
                    {
                        offset = ConsumeThreeByteRun(input, offset, ref utf16Length);
                    }
                    else
                    {
                        offset = ConsumeThreeByteRun(input, offset);
                    }
                }

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
                if (computeUtf16Length)
                {
                    utf16Length += 2;
                }

                continue;
            }

            validation = default;
            errorOffset = offset;
            return false;
        }

        validation = new Utf8ValidationResult(
            input.Length,
            computeUtf16Length ? utf16Length : 0,
            isAscii: true,
            containsSupplementaryScalars);
        errorOffset = -1;
        return true;
    }

    private static int DrainAscii(ReadOnlySpan<byte> input, int offset)
    {
        if (Vector256.IsHardwareAccelerated && input.Length - offset >= Vector256<byte>.Count)
        {
            ref var inputRef = ref MemoryMarshal.GetReference(input);
            var lastVectorStart = input.Length - Vector256<byte>.Count;
            while (offset <= lastVectorStart)
            {
                var current = Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, offset));
                var nonAsciiMask = current.ExtractMostSignificantBits();
                if (nonAsciiMask != 0)
                {
                    return offset + BitOperations.TrailingZeroCount(nonAsciiMask);
                }

                offset += Vector256<byte>.Count;
            }
        }

        if (Vector128.IsHardwareAccelerated && input.Length - offset >= Vector128<byte>.Count)
        {
            ref var inputRef = ref MemoryMarshal.GetReference(input);
            var lastVectorStart = input.Length - Vector128<byte>.Count;
            while (offset <= lastVectorStart)
            {
                var current = Vector128.LoadUnsafe(ref Unsafe.Add(ref inputRef, offset));
                var nonAsciiMask = current.ExtractMostSignificantBits();
                if (nonAsciiMask != 0)
                {
                    return offset + BitOperations.TrailingZeroCount(nonAsciiMask);
                }

                offset += Vector128<byte>.Count;
            }
        }

        while (offset + sizeof(ulong) <= input.Length)
        {
            var value = BinaryPrimitives.ReadUInt64LittleEndian(input[offset..]);
            if ((value & 0x8080_8080_8080_8080ul) != 0)
            {
                if ((value & 0x0000_0000_0000_0080ul) != 0) return offset;
                if ((value & 0x0000_0000_0000_8000ul) != 0) return offset + 1;
                if ((value & 0x0000_0000_0080_0000ul) != 0) return offset + 2;
                if ((value & 0x0000_0000_8000_0000ul) != 0) return offset + 3;
                if ((value & 0x0000_0080_0000_0000ul) != 0) return offset + 4;
                if ((value & 0x0000_8000_0000_0000ul) != 0) return offset + 5;
                if ((value & 0x0080_0000_0000_0000ul) != 0) return offset + 6;
                return offset + 7;
            }

            offset += sizeof(ulong);
        }

        while (offset + sizeof(uint) <= input.Length)
        {
            var value = BinaryPrimitives.ReadUInt32LittleEndian(input[offset..]);
            if ((value & 0x8080_8080u) != 0)
            {
                if ((value & 0x0000_0080u) != 0) return offset;
                if ((value & 0x0000_8000u) != 0) return offset + 1;
                if ((value & 0x0080_0000u) != 0) return offset + 2;
                return offset + 3;
            }

            offset += sizeof(uint);
        }

        while (offset < input.Length && input[offset] < 0x80)
        {
            offset++;
        }

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

    private static int ConsumeTwoByteRun(ReadOnlySpan<byte> input, int offset)
    {
        while (offset + 1 < input.Length)
        {
            var b0 = input[offset];
            if (b0 < 0xC2 || b0 >= 0xE0 || !IsContinuationByte(input[offset + 1]))
            {
                return offset;
            }

            offset += 2;
        }

        return offset;
    }

    private static int ConsumeThreeByteRun(ReadOnlySpan<byte> input, int offset, ref int utf16Length)
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

            offset += 3;
            utf16Length++;
        }

        return offset;
    }

    private static int ConsumeThreeByteRun(ReadOnlySpan<byte> input, int offset)
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

            offset += 3;
        }

        return offset;
    }

    private static bool IsContinuationByte(byte value) => (value & 0xC0) == 0x80;
}
