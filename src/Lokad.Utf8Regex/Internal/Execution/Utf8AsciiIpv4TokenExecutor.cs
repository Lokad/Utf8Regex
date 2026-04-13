namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiIpv4TokenExecutor
{
    public static bool TryFindAsciiIpv4Token(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex >= (uint)input.Length)
        {
            return false;
        }

        for (var index = Math.Max(startIndex, 0); index < input.Length; index++)
        {
            if (!IsAsciiDigit(input[index]))
            {
                continue;
            }

            if (!TryMatchAt(input[index..], out matchedLength))
            {
                continue;
            }

            matchIndex = index;
            return true;
        }

        return false;
    }

    public static int CountAsciiIpv4Tokens(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindAsciiIpv4Token(input, startIndex, out var matchIndex, out var matchedLength))
        {
            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static bool TryMatchAt(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        var index = 0;
        for (var octet = 0; octet < 4; octet++)
        {
            if (!TryConsumeOctet(input, ref index))
            {
                matchedLength = 0;
                return false;
            }

            if (octet == 3)
            {
                matchedLength = index;
                return true;
            }

            if ((uint)index >= (uint)input.Length || input[index] != (byte)'.')
            {
                matchedLength = 0;
                return false;
            }

            index++;
        }

        return false;
    }

    private static bool TryConsumeOctet(ReadOnlySpan<byte> input, ref int index)
    {
        if ((uint)index >= (uint)input.Length || !IsAsciiDigit(input[index]))
        {
            return false;
        }

        var remaining = input.Length - index;
        if (remaining >= 3)
        {
            var b0 = input[index];
            var b1 = input[index + 1];
            var b2 = input[index + 2];
            if (b0 == (byte)'2')
            {
                if (b1 == (byte)'5')
                {
                    if (b2 is >= (byte)'0' and <= (byte)'5')
                    {
                        index += 3;
                        return true;
                    }
                }
                else if (b1 is >= (byte)'0' and <= (byte)'4' && IsAsciiDigit(b2))
                {
                    index += 3;
                    return true;
                }
            }
            else
            {
                if (b0 is (byte)'0' or (byte)'1' && IsAsciiDigit(b1) && IsAsciiDigit(b2))
                {
                    index += 3;
                    return true;
                }

                if (IsAsciiDigit(b1))
                {
                    index += 2;
                    return true;
                }
            }
        }

        if (remaining >= 2 && IsAsciiDigit(input[index + 1]))
        {
            index += 2;
            return true;
        }

        index++;
        return true;
    }

    private static bool IsAsciiDigit(byte value) => value is >= (byte)'0' and <= (byte)'9';
}
