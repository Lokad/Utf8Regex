using System.Text;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.PythonRe;

internal static class PythonReAsciiGreedyStarFindAll
{
    public static Utf8PythonFindAllResult Execute(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        byte repeatedByte)
    {
        Utf8InputAnalyzer.ThrowIfInvalidOnly(input);

        var valueCount = 1;
        var position = startOffsetInBytes;
        while (position < input.Length)
        {
            valueCount++;
            if (input[position] == repeatedByte)
            {
                do
                {
                    position++;
                }
                while (position < input.Length && input[position] == repeatedByte);
            }
            else if (input[position] < 0x80)
            {
                position++;
            }
            else
            {
                _ = Rune.DecodeFromUtf8(input[position..], out _, out var scalarLength);
                position += scalarLength;
            }
        }

        var values = new string[valueCount];
        var valueIndex = 0;
        position = startOffsetInBytes;
        while (position < input.Length)
        {
            if (input[position] == repeatedByte)
            {
                var matchStart = position;
                do
                {
                    position++;
                }
                while (position < input.Length && input[position] == repeatedByte);

                values[valueIndex] = new string((char)repeatedByte, position - matchStart);
            }
            else
            {
                values[valueIndex] = string.Empty;
                if (input[position] < 0x80)
                {
                    position++;
                }
                else
                {
                    _ = Rune.DecodeFromUtf8(input[position..], out _, out var scalarLength);
                    position += scalarLength;
                }
            }

            valueIndex++;
        }

        values[valueIndex] = string.Empty;
        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.FullMatch,
            ScalarValues = values,
            TupleValues = [],
        };
    }
}
