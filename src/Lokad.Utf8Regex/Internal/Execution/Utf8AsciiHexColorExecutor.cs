using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiHexColorExecutor
{
    public static bool TryMatchWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        var index = 0;
        if ((uint)index < (uint)input.Length && input[index] == (byte)'#')
        {
            index++;
        }

        var remaining = input.Length - index;
        if (remaining != 3 && remaining != 6)
        {
            return false;
        }

        for (var i = index; i < input.Length; i++)
        {
            if (!IsAsciiHex(input[i]))
            {
                return false;
            }
        }

        matchedLength = input.Length;
        return true;
    }

    private static bool IsAsciiHex(byte value)
    {
        value = AsciiSearch.FoldCase(value);
        return (uint)(value - (byte)'0') <= 9 || (uint)(value - (byte)'a') <= (byte)('f' - 'a');
    }
}
