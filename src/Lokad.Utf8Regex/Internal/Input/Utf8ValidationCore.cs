using System.Numerics;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;

namespace Lokad.Utf8Regex.Internal.Input;

internal static class Utf8ValidationCore
{
    private const int DenseThreeByteLeadThreshold = 2;
    private const int SafeThreeByteIdleBlockLimit = 4;

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
        => TryValidate(input, computeUtf16Length, out validation, out errorOffset, out _);

    public static bool TryValidate(
        ReadOnlySpan<byte> input,
        bool computeUtf16Length,
        out Utf8ValidationResult validation,
        out int errorOffset,
        out bool containsKelvinSign)
    {
        var utf16Length = 0;
        var containsSupplementaryScalars = false;
        containsKelvinSign = false;
        var isAscii = true;
        var offset = 0;
        var drainSafeThreeByte = false;
        if (!computeUtf16Length)
        {
            // The lean two-byte mask loop wins on Cyrillic-like text; the wider
            // three-byte mask loop wins on CJK-like text. The sample chooses the
            // initial mode. The drain can still enter and leave the wider mode
            // when a heterogeneous subject changes character density later.
            // The 256-byte window bounds selection to a short constant scan; a
            // wrong initial pick only costs the first drain segment because the
            // drain re-resolves the mode below. Re-derive the window with
            // --measure-utf8-validation-profile over the frozen two-byte,
            // three-byte, and mixed profiles; it is a cost trade-off, not a
            // correctness gate.
            var sampleLength = Math.Min(input.Length, 256);
            var twoByteLeads = 0;
            var safeThreeByteLeads = 0;
            for (var i = 0; i < sampleLength; i++)
            {
                var value = input[i];
                if (value is >= 0xC2 and < 0xE0)
                {
                    twoByteLeads++;
                }
                else if (value is >= 0xE1 and <= 0xEC or >= 0xEE and < 0xF0)
                {
                    safeThreeByteLeads++;
                }
            }

            drainSafeThreeByte = safeThreeByteLeads > twoByteLeads;
        }

        while (offset < input.Length)
        {
            if (!computeUtf16Length)
            {
                if (!TryDrainAsciiAndCommonUtf8(input, offset, drainSafeThreeByte, out offset, out errorOffset))
                {
                    validation = default;
                    return false;
                }
            }
            else
            {
                var asciiStart = offset;
                offset = DrainAscii(input, offset);
                utf16Length += offset - asciiStart;
            }

            if (offset >= input.Length)
            {
                validation = new Utf8ValidationResult(
                    input.Length,
                    computeUtf16Length ? utf16Length : 0,
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

                containsKelvinSign |= b0 == 0xE2 && b1 == 0x84 && b2 == 0xAA;

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
                else if (!computeUtf16Length && b0 is 0xE0 or 0xED)
                {
                    // Keep a locally constrained run in one scalar phase instead
                    // of re-entering the common-width SIMD drain for every scalar.
                    offset = ConsumeConstrainedThreeByteRun(input, offset);
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
            isAscii,
            containsSupplementaryScalars);
        errorOffset = -1;
        return true;
    }

    internal static bool TryDrainAsciiAndCommonUtf8(
        ReadOnlySpan<byte> input,
        int offset,
        bool drainSafeThreeByte,
        out int nextOffset,
        out int errorOffset)
    {
        if (Vector256.IsHardwareAccelerated)
        {
            var lastVectorStart = input.Length - Vector256<byte>.Count;
            var expectedContinuationCarry = 0u;
            var includeSafeThreeByte = drainSafeThreeByte;
            var safeThreeByteIdleBlocks = 0;
            var continuationTag = Vector256.Create((byte)0x80);
            var continuationMask = Vector256.Create((byte)0xC0);
            var twoByteLeadTag = Vector256.Create((byte)0xC0);
            var twoByteLeadMask = Vector256.Create((byte)0xE0);
            var threeByteLeadTag = Vector256.Create((byte)0xE0);
            var threeByteLeadMask = Vector256.Create((byte)0xF0);
            var e0 = Vector256.Create((byte)0xE0);
            var ed = Vector256.Create((byte)0xED);
            var overlongLeadTag = Vector256.Create((byte)0xC0);
            var overlongLeadMask = Vector256.Create((byte)0xFE);

            while (offset <= lastVectorStart)
            {
                var current = MemoryMarshal.Read<Vector256<byte>>(input[offset..]);
                var nonAsciiBits = current.ExtractMostSignificantBits();
                if (nonAsciiBits == 0 && expectedContinuationCarry == 0)
                {
                    if (includeSafeThreeByte &&
                        ++safeThreeByteIdleBlocks >= SafeThreeByteIdleBlockLimit)
                    {
                        includeSafeThreeByte = false;
                    }

                    offset += Vector256<byte>.Count;
                    continue;
                }

                var continuationBits = Vector256.Equals(
                    Vector256.BitwiseAnd(current, continuationMask),
                    continuationTag).ExtractMostSignificantBits();
                var twoByteLeadBits = Vector256.Equals(
                    Vector256.BitwiseAnd(current, twoByteLeadMask),
                    twoByteLeadTag).ExtractMostSignificantBits();
                var safeThreeByteLeadBits = 0u;
                if (includeSafeThreeByte)
                {
                    var threeByteLeadBits = Vector256.Equals(
                        Vector256.BitwiseAnd(current, threeByteLeadMask),
                        threeByteLeadTag).ExtractMostSignificantBits();
                    var constrainedThreeByteLeadBits =
                        Vector256.Equals(current, e0).ExtractMostSignificantBits() |
                        Vector256.Equals(current, ed).ExtractMostSignificantBits();
                    safeThreeByteLeadBits = threeByteLeadBits & ~constrainedThreeByteLeadBits;
                }
                var leadBits = twoByteLeadBits | safeThreeByteLeadBits;
                var overlongLeadBits = Vector256.Equals(
                    Vector256.BitwiseAnd(current, overlongLeadMask),
                    overlongLeadTag).ExtractMostSignificantBits();
                var unsupportedBits = nonAsciiBits & ~(continuationBits | leadBits);
                // Keep the SIMD phase stable when a scalar crosses a block:
                // high bits carry the expected continuation lanes forward.
                var expectedContinuationWide =
                    ((ulong)leadBits << 1) |
                    ((ulong)safeThreeByteLeadBits << 2) |
                    expectedContinuationCarry;
                var expectedContinuationBits = (uint)expectedContinuationWide;
                if (overlongLeadBits == 0 &&
                    unsupportedBits == 0 &&
                    continuationBits == expectedContinuationBits)
                {
                    if (includeSafeThreeByte)
                    {
                        safeThreeByteIdleBlocks = safeThreeByteLeadBits == 0
                            ? safeThreeByteIdleBlocks + 1
                            : 0;
                        if (safeThreeByteIdleBlocks >= SafeThreeByteIdleBlockLimit)
                        {
                            includeSafeThreeByte = false;
                        }
                    }

                    expectedContinuationCarry = (uint)(expectedContinuationWide >> 32);
                    offset += Vector256<byte>.Count;
                    continue;
                }

                var stop = unsupportedBits == 0
                    ? Vector256<byte>.Count
                    : BitOperations.TrailingZeroCount(unsupportedBits);
                var prefixBits = stop == Vector256<byte>.Count
                    ? uint.MaxValue
                    : (1u << stop) - 1u;
                var invalidLeadBits = overlongLeadBits & prefixBits;
                var structuralMismatchBits = (continuationBits ^ expectedContinuationBits) & prefixBits;
                var invalidBits = invalidLeadBits | structuralMismatchBits;
                if (invalidBits != 0)
                {
                    var invalidIndex = BitOperations.TrailingZeroCount(invalidBits);
                    var missingContinuation =
                        (expectedContinuationBits & (1u << invalidIndex)) != 0 &&
                        (continuationBits & (1u << invalidIndex)) == 0;
                    errorOffset = missingContinuation
                        ? FindPreviousScalarLead(input, offset + invalidIndex)
                        : offset + invalidIndex;

                    nextOffset = errorOffset;
                    return false;
                }

                if (stop < Vector256<byte>.Count)
                {
                    if (!includeSafeThreeByte &&
                        input[offset + stop] is >= 0xE1 and <= 0xEC or >= 0xEE and < 0xF0)
                    {
                        var threeByteLeadBits = Vector256.Equals(
                            Vector256.BitwiseAnd(current, threeByteLeadMask),
                            threeByteLeadTag).ExtractMostSignificantBits();
                        var constrainedThreeByteLeadBits =
                            Vector256.Equals(current, e0).ExtractMostSignificantBits() |
                            Vector256.Equals(current, ed).ExtractMostSignificantBits();
                        if (BitOperations.PopCount(threeByteLeadBits & ~constrainedThreeByteLeadBits) >=
                            DenseThreeByteLeadThreshold)
                        {
                            includeSafeThreeByte = true;
                            safeThreeByteIdleBlocks = 0;
                            continue;
                        }
                    }

                    if ((expectedContinuationBits & (1u << stop)) != 0)
                    {
                        errorOffset = FindPreviousScalarLead(input, offset + stop);
                        nextOffset = errorOffset;
                        return false;
                    }

                    nextOffset = offset + stop;
                    errorOffset = -1;
                    return true;
                }
            }

            while (expectedContinuationCarry != 0)
            {
                if (offset >= input.Length || !IsContinuationByte(input[offset]))
                {
                    errorOffset = FindPreviousScalarLead(input, offset);
                    nextOffset = errorOffset;
                    return false;
                }

                offset++;
                expectedContinuationCarry >>= 1;
            }
        }

        while (offset < input.Length)
        {
            var value = input[offset];
            if (value < 0x80)
            {
                offset++;
                continue;
            }

            if (value is >= 0xC2 and < 0xE0)
            {
                if (offset + 1 >= input.Length || !IsContinuationByte(input[offset + 1]))
                {
                    nextOffset = offset;
                    errorOffset = offset;
                    return false;
                }

                offset += 2;
                continue;
            }

            if (drainSafeThreeByte &&
                value is >= 0xE1 and <= 0xEC or >= 0xEE and < 0xF0)
            {
                if (offset + 2 >= input.Length ||
                    !IsContinuationByte(input[offset + 1]) ||
                    !IsContinuationByte(input[offset + 2]))
                {
                    nextOffset = offset;
                    errorOffset = offset;
                    return false;
                }

                offset += 3;
                continue;
            }

            if (value >= 0xE0)
            {
                nextOffset = offset;
                errorOffset = -1;
                return true;
            }

            nextOffset = offset;
            errorOffset = offset;
            return false;
        }

        nextOffset = offset;
        errorOffset = -1;
        return true;
    }

    private static int FindPreviousScalarLead(ReadOnlySpan<byte> input, int offset)
    {
        var leadOffset = offset - 1;
        while (leadOffset > 0 && IsContinuationByte(input[leadOffset]))
        {
            leadOffset--;
        }

        return Math.Max(leadOffset, 0);
    }

    private static int ConsumeConstrainedThreeByteRun(ReadOnlySpan<byte> input, int offset)
    {
        while (offset <= input.Length - 3)
        {
            var lead = input[offset];
            if (lead is not (0xE0 or 0xED))
            {
                break;
            }

            var second = input[offset + 1];
            var validSecond = lead == 0xE0
                ? second is >= 0xA0 and <= 0xBF
                : second is >= 0x80 and <= 0x9F;
            if (!validSecond || !IsContinuationByte(input[offset + 2]))
            {
                break;
            }

            offset += 3;
        }

        return offset;
    }

    private static int DrainAscii(ReadOnlySpan<byte> input, int offset)
    {
        var relative = input[offset..].IndexOfAnyInRange((byte)0x80, (byte)0xFF);
        return relative < 0 ? input.Length : offset + relative;
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
