namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiUriTokenExecutor
{
    public static bool TryMatchWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        var delimiterIndex = input.IndexOf("://"u8);
        return delimiterIndex >= 0 &&
            TryMatchAtDelimiter(input, 0, delimiterIndex, out _, out matchedLength) &&
            matchedLength == input.Length;
    }

    public static int CountAsciiUriTokens(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindAsciiUriToken(input, startIndex, out var matchIndex, out var matchedLength))
        {
            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public static bool TryFindAsciiUriToken(ReadOnlySpan<byte> input, int startIndex, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        var searchFrom = startIndex;
        while (searchFrom < input.Length)
        {
            var relative = input[searchFrom..].IndexOf("://"u8);
            if (relative < 0)
            {
                return false;
            }

            var delimiterIndex = searchFrom + relative;
            searchFrom = delimiterIndex + 1;
            if (!TryMatchAtDelimiter(input, startIndex, delimiterIndex, out matchIndex, out matchedLength))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static bool TryMatchAtDelimiter(ReadOnlySpan<byte> input, int minStartIndex, int delimiterIndex, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;

        var schemeStart = delimiterIndex;
        while (schemeStart > minStartIndex && IsAsciiWordChar(input[schemeStart - 1]))
        {
            schemeStart--;
        }

        if (schemeStart == delimiterIndex)
        {
            return false;
        }

        if (schemeStart > 0 && input[schemeStart - 1] >= 0x80)
        {
            return false;
        }

        var index = delimiterIndex + 3;
        if ((uint)index >= (uint)input.Length || !IsAsciiUriBodyStart(input[index]))
        {
            return false;
        }

        index++;
        if ((uint)index >= (uint)input.Length || !IsAsciiUriBodyContinuation(input[index]))
        {
            return false;
        }

        index++;
        while ((uint)index < (uint)input.Length && IsAsciiUriBodyContinuation(input[index]))
        {
            index++;
        }

        if ((uint)index < (uint)input.Length)
        {
            if (input[index] >= 0x80)
            {
                return false;
            }

            if (input[index] == (byte)'?')
            {
                index++;
                while ((uint)index < (uint)input.Length && IsAsciiUriQueryByte(input[index]))
                {
                    index++;
                }

                if ((uint)index < (uint)input.Length && input[index] >= 0x80)
                {
                    return false;
                }
            }

            if ((uint)index < (uint)input.Length && input[index] == (byte)'#')
            {
                index++;
                while ((uint)index < (uint)input.Length && IsAsciiUriFragmentByte(input[index]))
                {
                    index++;
                }

                if ((uint)index < (uint)input.Length && input[index] >= 0x80)
                {
                    return false;
                }
            }
        }

        matchIndex = schemeStart;
        matchedLength = index - schemeStart;
        return true;
    }

    private static bool IsAsciiWordChar(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9' or >= (byte)'A' and <= (byte)'Z' or >= (byte)'a' and <= (byte)'z' or (byte)'_';
    }

    private static bool IsAsciiWhitespace(byte value)
    {
        return value is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v';
    }

    private static bool IsAsciiUriBodyStart(byte value)
    {
        return value < 0x80 &&
            value != (byte)'/' &&
            value != (byte)'?' &&
            value != (byte)'#' &&
            !IsAsciiWhitespace(value);
    }

    private static bool IsAsciiUriBodyContinuation(byte value)
    {
        return value < 0x80 &&
            value != (byte)'?' &&
            value != (byte)'#' &&
            !IsAsciiWhitespace(value);
    }

    private static bool IsAsciiUriQueryByte(byte value)
    {
        return value < 0x80 &&
            value != (byte)'#' &&
            !IsAsciiWhitespace(value);
    }

    private static bool IsAsciiUriFragmentByte(byte value)
    {
        return value < 0x80 && !IsAsciiWhitespace(value);
    }
}
