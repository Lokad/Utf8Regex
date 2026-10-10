using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8UnicodeLetterBoundedRepeatExecutor
{
    public static int CountLetters(ReadOnlySpan<byte> input, bool containsSupplementaryScalars)
    {
        var count = 0;
        var index = 0;
        while (index < input.Length)
        {
            var first = input[index];
            if (first < 0x80)
            {
                if ((uint)((first | 0x20) - (byte)'a') <= (byte)'z' - (byte)'a')
                {
                    count++;
                }

                index++;
                continue;
            }

            if ((first & 0xE0) == 0xC0)
            {
                var ch = (char)(((first & 0x1F) << 6) | (input[index + 1] & 0x3F));
                if (char.IsLetter(ch))
                {
                    count++;
                }

                index += 2;
                continue;
            }

            if ((first & 0xF0) == 0xE0)
            {
                var ch = (char)(((first & 0x0F) << 12) |
                                ((input[index + 1] & 0x3F) << 6) |
                                (input[index + 2] & 0x3F));
                if (char.IsLetter(ch))
                {
                    count++;
                }

                index += 3;
                continue;
            }

            if (!containsSupplementaryScalars)
            {
                index += 4;
                continue;
            }

            var scalar = ((first & 0x07) << 18) |
                         ((input[index + 1] & 0x3F) << 12) |
                         ((input[index + 2] & 0x3F) << 6) |
                         (input[index + 3] & 0x3F);
            if (Rune.IsLetter(new Rune(scalar)))
            {
                count++;
            }

            index += 4;
        }

        return count;
    }

    internal static bool TryCountLetters(ReadOnlySpan<byte> input, int minCount, int maxCount, out int count)
    {
        count = 0;
        if (minCount <= 0 || maxCount < minCount) return false;
        var index = 0;
        while (index < input.Length)
        {
            if (!TryDecodeScalar(input, ref index, out var ch))
            {
                return false;
            }

            if (!Rune.IsLetter(ch))
            {
                continue;
            }

            var runLength = 1;
            while (index < input.Length)
            {
                var nextIndex = index;
                if (!TryDecodeScalar(input, ref nextIndex, out ch))
                {
                    return false;
                }

                if (!Rune.IsLetter(ch))
                {
                    break;
                }

                runLength++;
                index = nextIndex;
            }

            count += CountGreedyNonOverlappingMatches(runLength, minCount, maxCount);
        }

        return true;
    }

    private static int CountGreedyNonOverlappingMatches(int runLength, int minCount, int maxCount)
    {
        var count = 0;
        while (runLength >= minCount)
        {
            var consumed = Math.Min(runLength, maxCount);
            count++;
            runLength -= consumed;
        }

        return count;
    }

    private static bool TryDecodeScalar(ReadOnlySpan<byte> input, ref int index, out Rune scalar)
    {
        var status = Rune.DecodeFromUtf8(input[index..], out scalar, out var width);
        if (status != System.Buffers.OperationStatus.Done) return false;
        index += width;
        return true;
    }
}
