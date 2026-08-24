using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiLiteralBetweenNegatedRunsExecutor
{
    public static bool TryIsMatchWithoutValidation(
        ReadOnlySpan<byte> input,
        int startIndex,
        byte literal,
        byte excludedHead,
        byte excludedTail,
        out bool isMatch)
    {
        isMatch = false;
        if ((uint)startIndex >= (uint)input.Length || input.Length < 3)
        {
            return input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0;
        }

        var minimumCandidate = Math.Max(startIndex, 1);
        var maximumCandidate = input.Length - 2;
        var offset = 0;
        ref var inputRef = ref MemoryMarshal.GetReference(input);

        // A found candidate is not enough to bypass validation: keep scanning so the
        // definitive result also proves that every input byte is ASCII.
        if (Vector256.IsHardwareAccelerated)
        {
            var literalVector = Vector256.Create(literal);
            var vectorLimit = input.Length - Vector256<byte>.Count;
            while (offset <= vectorLimit)
            {
                var values = Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, offset));
                if (values.ExtractMostSignificantBits() != 0)
                {
                    return false;
                }

                var literalMask = Vector256.Equals(values, literalVector).ExtractMostSignificantBits();
                isMatch |= HasValidCandidate(input, offset, literalMask, minimumCandidate, maximumCandidate, excludedHead, excludedTail);
                offset += Vector256<byte>.Count;
            }
        }

        if (Vector128.IsHardwareAccelerated && input.Length - offset >= Vector128<byte>.Count)
        {
            var values = Vector128.LoadUnsafe(ref Unsafe.Add(ref inputRef, offset));
            if (values.ExtractMostSignificantBits() != 0)
            {
                return false;
            }

            var literalMask = Vector128.Equals(values, Vector128.Create(literal)).ExtractMostSignificantBits();
            isMatch |= HasValidCandidate(input, offset, literalMask, minimumCandidate, maximumCandidate, excludedHead, excludedTail);
            offset += Vector128<byte>.Count;
        }

        while (offset < input.Length)
        {
            var value = input[offset];
            if (value >= 0x80)
            {
                return false;
            }

            if (value == literal &&
                offset >= minimumCandidate &&
                offset <= maximumCandidate &&
                input[offset - 1] != excludedHead &&
                input[offset + 1] != excludedTail)
            {
                isMatch = true;
            }

            offset++;
        }

        return true;

        static bool HasValidCandidate(
            ReadOnlySpan<byte> input,
            int blockOffset,
            uint literalMask,
            int minimumCandidate,
            int maximumCandidate,
            byte excludedHead,
            byte excludedTail)
        {
            while (literalMask != 0)
            {
                var literalIndex = blockOffset + BitOperations.TrailingZeroCount(literalMask);
                if (literalIndex >= minimumCandidate &&
                    literalIndex <= maximumCandidate &&
                    input[literalIndex - 1] != excludedHead &&
                    input[literalIndex + 1] != excludedTail)
                {
                    return true;
                }

                literalMask &= literalMask - 1;
            }

            return false;
        }
    }

    public static bool TryFind(
        ReadOnlySpan<byte> input,
        int startIndex,
        byte literal,
        byte excludedHead,
        byte excludedTail,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex >= (uint)input.Length || input.Length < 3)
        {
            return false;
        }

        var searchIndex = Math.Max(startIndex, 0);
        if (searchIndex == 0)
        {
            searchIndex = 1;
        }

        while (searchIndex < input.Length - 1)
        {
            var relative = input[searchIndex..^1].IndexOf(literal);
            if (relative < 0)
            {
                return false;
            }

            var literalIndex = searchIndex + relative;
            if (input[literalIndex - 1] == excludedHead || input[literalIndex + 1] == excludedTail)
            {
                searchIndex = literalIndex + 1;
                continue;
            }

            var left = literalIndex - 1;
            while (left > 0 && input[left - 1] != excludedHead)
            {
                left--;
            }

            var right = literalIndex + 2;
            while (right < input.Length && input[right] != excludedTail)
            {
                right++;
            }

            matchIndex = left;
            matchedLength = right - left;
            return true;
        }

        return false;
    }
}
