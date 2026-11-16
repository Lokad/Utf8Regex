using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8UnicodeLetterCounter
{
    public static int CountLetters(ReadOnlySpan<byte> input)
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

}
