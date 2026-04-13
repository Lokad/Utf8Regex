namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiDottedDecimalQuadExecutor
{
    public static int Count(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindNext(input, startIndex, out var matchIndex, out var matchLength))
        {
            count++;
            startIndex = matchIndex + Math.Max(matchLength, 1);
        }

        return count;
    }

    public static bool TryFindNext(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchLength)
    {
        matchIndex = -1;
        matchLength = 0;

        if (startIndex < 0 || startIndex >= input.Length)
        {
            return false;
        }

        var searchFrom = startIndex;
        while (searchFrom <= input.Length - 11)
        {
            var relative = input[searchFrom..].IndexOfAnyInRange((byte)'0', (byte)'9');
            if (relative < 0)
            {
                return false;
            }

            var candidateStart = searchFrom + relative;
            if (TryMatchAt(input, candidateStart, out matchLength))
            {
                matchIndex = candidateStart;
                return true;
            }

            searchFrom = candidateStart + 1;
        }

        return false;
    }

    private static bool TryMatchAt(ReadOnlySpan<byte> input, int index, out int matchedLength)
    {
        matchedLength = 0;
        var current = index;
        for (var i = 0; i < 4; i++)
        {
            if (!TryReadOctet(input, ref current))
            {
                return false;
            }

            if (i < 3)
            {
                if ((uint)current >= (uint)input.Length || input[current] != (byte)'.')
                {
                    return false;
                }

                current++;
            }
        }

        matchedLength = current - index;
        return true;
    }

    private static bool TryReadOctet(ReadOnlySpan<byte> input, ref int index)
    {
        if ((uint)index >= (uint)input.Length || !IsDigit(input[index]))
        {
            return false;
        }

        if (input[index] == (byte)'2')
        {
            if ((uint)(index + 2) >= (uint)input.Length)
            {
                return false;
            }

            var second = input[index + 1];
            var third = input[index + 2];
            if (second == (byte)'5')
            {
                if (third is < (byte)'0' or > (byte)'5')
                {
                    return false;
                }

                index += 3;
                return true;
            }

            if (second is >= (byte)'0' and <= (byte)'4' && IsDigit(third))
            {
                index += 3;
                return true;
            }

            return false;
        }

        if (input[index] is (byte)'0' or (byte)'1')
        {
            if ((uint)(index + 2) < (uint)input.Length &&
                IsDigit(input[index + 1]) &&
                IsDigit(input[index + 2]))
            {
                index += 3;
                return true;
            }
        }

        if ((uint)(index + 1) < (uint)input.Length &&
            IsDigit(input[index]) &&
            IsDigit(input[index + 1]))
        {
            index += 2;
            return true;
        }

        return false;
    }

    private static bool IsDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';
}
