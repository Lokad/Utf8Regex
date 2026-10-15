using System.Numerics;
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

        var minimumCandidate = startIndex + 1;
        var maximumCandidate = input.Length - 2;
        var offset = 0;

        // A found candidate is not enough to bypass validation: keep scanning so the
        // definitive result also proves that every input byte is ASCII.
        if (Vector256.IsHardwareAccelerated)
        {
            var literalVector = Vector256.Create(literal);
            var vectorLimit = input.Length - Vector256<byte>.Count;
            while (offset <= vectorLimit)
            {
                var values = MemoryMarshal.Read<Vector256<byte>>(input[offset..]);
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
            var values = MemoryMarshal.Read<Vector128<byte>>(input[offset..]);
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

        var headStart = startIndex;
        while (headStart < input.Length - 2)
        {
            while (headStart < input.Length && input[headStart] == excludedHead) headStart++;
            var headEnd = headStart;
            while (headEnd < input.Length && input[headEnd] != excludedHead) headEnd++;
            // First select the latest feasible split of the complete greedy
            // head run. Then consume the tail greedily. Never rewind the floor.
            for (var split = Math.Min(headEnd, input.Length - 2); split > headStart; split--)
            {
                if (input[split] != literal || input[split + 1] == excludedTail) continue;
                var tailEnd = split + 2;
                while (tailEnd < input.Length && input[tailEnd] != excludedTail) tailEnd++;
                matchIndex = headStart; matchedLength = tailEnd - headStart; return true;
            }
            if (headEnd == input.Length) break;
            headStart = headEnd + 1;
        }
        return false;
    }
}
