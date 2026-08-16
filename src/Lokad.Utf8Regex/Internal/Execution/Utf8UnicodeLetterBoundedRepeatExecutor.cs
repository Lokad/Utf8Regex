using System.Text;
using System.Text.RegularExpressions;

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

    public static int CountLettersOrFallback(ReadOnlySpan<byte> input, int minCount, int maxCount, Regex fallbackRegex)
    {
        if (TryCountLetters(input, minCount, maxCount, out var count))
        {
            return count;
        }

        return fallbackRegex.Count(Encoding.UTF8.GetString(input));
    }

    private static bool TryCountLetters(ReadOnlySpan<byte> input, int minCount, int maxCount, out int count)
    {
        count = 0;
        var index = 0;
        while (index < input.Length)
        {
            if (!TryDecodeBmpChar(input, ref index, out var ch))
            {
                return false;
            }

            if (!char.IsLetter(ch))
            {
                continue;
            }

            var runLength = 1;
            while (index < input.Length)
            {
                var nextIndex = index;
                if (!TryDecodeBmpChar(input, ref nextIndex, out ch))
                {
                    return false;
                }

                if (!char.IsLetter(ch))
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

    private static bool TryDecodeBmpChar(ReadOnlySpan<byte> input, ref int index, out char ch)
    {
        ch = '\0';
        if ((uint)index >= (uint)input.Length)
        {
            return false;
        }

        var first = input[index];
        if (first < 0x80)
        {
            ch = (char)first;
            index++;
            return true;
        }

        if ((first & 0xE0) == 0xC0)
        {
            if (index + 1 >= input.Length)
            {
                return false;
            }

            ch = (char)(((first & 0x1F) << 6) | (input[index + 1] & 0x3F));
            index += 2;
            return true;
        }

        if ((first & 0xF0) == 0xE0)
        {
            if (index + 2 >= input.Length)
            {
                return false;
            }

            ch = (char)(((first & 0x0F) << 12) |
                        ((input[index + 1] & 0x3F) << 6) |
                        (input[index + 2] & 0x3F));
            index += 3;
            return true;
        }

        return false;
    }
}
