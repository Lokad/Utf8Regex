using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiExactLiteralCountExecutor
{
    private const int MinimumLiteralLength = 17;
    private const int MaximumLiteralLength = 32;
    private const int MaximumAnchorFrequencyRank = 70;

    public static int SelectRarestAnchorOffset(ReadOnlySpan<byte> literal)
    {
        if (literal.Length is < MinimumLiteralLength or > MaximumLiteralLength)
        {
            return -1;
        }

        var bestOffset = 0;
        var bestRank = int.MaxValue;
        for (var offset = 0; offset < literal.Length; offset++)
        {
            if (literal[offset] >= 0x80)
            {
                return -1;
            }

            var rank = PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(literal[offset]);
            if (rank < bestRank)
            {
                bestOffset = offset;
                bestRank = rank;
            }
        }

        // The fused loop is a selective throughput route, not a replacement for the
        // prepared searcher. Reject common anchors that would cause dense verification.
        return bestRank <= MaximumAnchorFrequencyRank ? bestOffset : -1;
    }

    public static bool TryCountAndValidateAscii(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal,
        int anchorOffset,
        out int count)
    {
        if (!Vector256.IsHardwareAccelerated ||
            input.Length < Vector256<byte>.Count ||
            (uint)anchorOffset >= (uint)literal.Length)
        {
            count = 0;
            return false;
        }

        var anchor = Vector256.Create(literal[anchorOffset]);
        var maxStart = input.Length - literal.Length;
        var nextMatchStart = 0;
        var foundCount = 0;
        var index = 0;
        ref var inputRef = ref MemoryMarshal.GetReference(input);

        while (index <= input.Length - Vector256<byte>.Count)
        {
            var values = Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, index));
            if (values.ExtractMostSignificantBits() != 0)
            {
                // Valid non-ASCII input can still contain ASCII matches. Let the caller
                // perform complete UTF-8 validation and use the general exact-literal path.
                count = 0;
                return false;
            }

            var candidates = Vector256.Equals(values, anchor).ExtractMostSignificantBits();
            while (candidates != 0)
            {
                var anchorIndex = index + BitOperations.TrailingZeroCount(candidates);
                var candidate = anchorIndex - anchorOffset;
                if (candidate >= nextMatchStart &&
                    candidate <= maxStart &&
                    Utf8LiteralEquality.EqualsAt(
                        input,
                        candidate,
                        literal,
                        Utf8LiteralComparisonKind.Scalar))
                {
                    foundCount++;
                    nextMatchStart = candidate + literal.Length;
                }

                candidates &= candidates - 1;
            }

            index += Vector256<byte>.Count;
        }

        for (; index < input.Length; index++)
        {
            var value = input[index];
            if (value >= 0x80)
            {
                count = 0;
                return false;
            }

            if (value != literal[anchorOffset])
            {
                continue;
            }

            var candidate = index - anchorOffset;
            if (candidate >= nextMatchStart &&
                candidate <= maxStart &&
                Utf8LiteralEquality.EqualsAt(
                    input,
                    candidate,
                    literal,
                    Utf8LiteralComparisonKind.Scalar))
            {
                foundCount++;
                nextMatchStart = candidate + literal.Length;
            }
        }

        count = foundCount;
        return true;
    }
}
