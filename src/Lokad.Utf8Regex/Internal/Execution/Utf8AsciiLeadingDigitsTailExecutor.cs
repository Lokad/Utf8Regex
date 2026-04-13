using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiLeadingDigitsTailExecutor
{
    public static bool TryMatchWhole(ReadOnlySpan<byte> input, ReadOnlySpan<byte> separatorBytes, out int matchedLength)
    {
        matchedLength = 0;
        if (input.IsEmpty)
        {
            return false;
        }

        var effectiveLength = input.Length;
        if (effectiveLength > 0 && input[effectiveLength - 1] == (byte)'\n')
        {
            effectiveLength--;
        }

        if (effectiveLength <= 0)
        {
            return false;
        }

        var index = 0;
        while (index < effectiveLength && IsAsciiDigit(input[index]))
        {
            index++;
        }

        if (index == 0)
        {
            return false;
        }

        if (index < effectiveLength)
        {
            if (separatorBytes.IsEmpty)
            {
                return false;
            }

            var separators = SearchValues.Create(separatorBytes);
            if (!separators.Contains(input[index]))
            {
                return false;
            }

            index++;
            if (index < effectiveLength && input[index..effectiveLength].IndexOf((byte)'\n') >= 0)
            {
                return false;
            }
        }

        matchedLength = effectiveLength;
        return true;
    }

    private static bool IsAsciiDigit(byte value) => (uint)(value - '0') <= 9;
}
