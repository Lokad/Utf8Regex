using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiPrefixTokenExecutor
{
    private static readonly SearchValues<byte> s_asciiIdentifierTail = SearchValues.Create("0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz"u8);
    private static readonly SearchValues<byte> s_asciiOperators = SearchValues.Create("=+-^*/.<>~!&|?"u8);
    private static readonly SearchValues<byte> s_utf8QuotedStringSpecials = SearchValues.Create("\"\\\n"u8);

    private static readonly byte[] s_stringStops =
    [
        (byte)'"', (byte)'\\', (byte)'\n',
    ];

    private static readonly SearchValues<byte> s_stringStopsSearchValues = SearchValues.Create(s_stringStops);

    public static bool TryMatchIdentifierPrefixIgnoreCase(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        if (input.IsEmpty)
        {
            return false;
        }

        if (!IsAsciiIdentifierHeadIgnoreCase(input[0]))
        {
            return false;
        }

        var tail = input[1..];
        if (tail.Length <= 16)
        {
            var length = 1;
            while (length < input.Length && s_asciiIdentifierTail.Contains(input[length]))
            {
                length++;
            }

            matchedLength = length;
            return true;
        }

        var stop = tail.IndexOfAnyExcept(s_asciiIdentifierTail);
        var index = stop < 0 ? input.Length : 1 + stop;

        matchedLength = index;
        return true;
    }

    public static bool TryMatchNumberPrefix(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        if (input.IsEmpty)
        {
            return false;
        }

        var index = 0;
        if (input[index] == (byte)'-')
        {
            index++;
            if ((uint)index >= (uint)input.Length)
            {
                return false;
            }
        }

        var digitStart = index;
        index += CountLeadingDigits(input[index..]);

        if (index == digitStart)
        {
            return false;
        }

        var matched = index;

        if ((uint)index < (uint)input.Length && input[index] == (byte)'.')
        {
            var fractionIndex = index + 1;
            var fractionStart = fractionIndex;
            fractionIndex += CountLeadingDigits(input[fractionIndex..]);

            if (fractionIndex > fractionStart)
            {
                index = fractionIndex;
                matched = index;
            }
        }

        if ((uint)index < (uint)input.Length && input[index] == (byte)'e')
        {
            var exponentIndex = index + 1;
            if ((uint)exponentIndex < (uint)input.Length &&
                (input[exponentIndex] == (byte)'+' || input[exponentIndex] == (byte)'-'))
            {
                exponentIndex++;
            }

            var exponentStart = exponentIndex;
            exponentIndex += CountLeadingDigits(input[exponentIndex..]);

            if (exponentIndex > exponentStart)
            {
                matched = exponentIndex;
            }
        }

        matchedLength = matched;
        return true;
    }

    public static bool TryMatchSignedDecimalWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;

        var length = input.Length;
        if (length > 0 && input[^1] == (byte)'\n')
        {
            length--;
        }

        var index = 0;
        if (index < length && (input[index] == (byte)'+' || input[index] == (byte)'-'))
        {
            index++;
        }

        while (index < length && IsAsciiDigit(input[index]))
        {
            index++;
        }

        if (index < length && input[index] == (byte)'.')
        {
            index++;
            while (index < length && IsAsciiDigit(input[index]))
            {
                index++;
            }
        }

        if (index != length)
        {
            return false;
        }

        matchedLength = length;
        return true;
    }

    public static bool TryMatchOperatorRunPrefix(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        if (input.IsEmpty || !IsAsciiOperator(input[0]))
        {
            return false;
        }

        var tail = input[1..];
        var stop = tail.IndexOfAnyExcept(s_asciiOperators);
        var index = stop < 0 ? input.Length : 1 + stop;

        matchedLength = index;
        return true;
    }

    public static bool TryMatchStringPrefix(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        if (input.IsEmpty || input[0] != (byte)'"')
        {
            return false;
        }

        var index = 1;
        while ((uint)index < (uint)input.Length)
        {
            var relative = input[index..].IndexOfAny(s_stringStopsSearchValues);
            if (relative < 0)
            {
                return false;
            }

            index += relative;
            var value = input[index];
            if (value == (byte)'"')
            {
                matchedLength = index + 1;
                return true;
            }

            if (value == (byte)'\n')
            {
                return false;
            }

            index++;
            if ((uint)index >= (uint)input.Length || input[index] == (byte)'\n')
            {
                return false;
            }

            index++;
        }

        return false;
    }

    public static bool TryMatchAnchoredPrefixUntilByte(ReadOnlySpan<byte> input, ReadOnlySpan<byte> prefix, byte terminator, out int matchedLength)
    {
        matchedLength = 0;
        if (input.Length <= prefix.Length || !input.StartsWith(prefix))
        {
            return false;
        }

        var terminatorOffset = input[prefix.Length..].IndexOf(terminator);
        if (terminatorOffset < 0)
        {
            return false;
        }

        matchedLength = prefix.Length + terminatorOffset + 1;
        return true;
    }

    public static bool TryMatchAnchoredTrimmedOptionalLiteralPrefixTail(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> requiredPrefix,
        ReadOnlySpan<byte> optionalPrefix,
        out int matchedLength)
    {
        matchedLength = 0;
        var effectiveLength = input.Length;
        if (effectiveLength > 0 && input[effectiveLength - 1] == (byte)'\n')
        {
            effectiveLength--;
        }

        input = input[..effectiveLength];
        var index = 0;

        while ((uint)index < (uint)input.Length && char.IsWhiteSpace((char)input[index]))
        {
            index++;
        }

        if (!optionalPrefix.IsEmpty &&
            (uint)(input.Length - index) >= (uint)optionalPrefix.Length &&
            input[index..].StartsWith(optionalPrefix))
        {
            index += optionalPrefix.Length;
            while ((uint)index < (uint)input.Length && char.IsWhiteSpace((char)input[index]))
            {
                index++;
            }
        }

        if ((uint)(input.Length - index) < (uint)requiredPrefix.Length ||
            !input[index..].StartsWith(requiredPrefix))
        {
            return false;
        }

        index += requiredPrefix.Length;
        if ((uint)index < (uint)input.Length)
        {
            if (input[index] == (byte)'\n')
            {
                return false;
            }

            if (char.IsWhiteSpace((char)input[index]))
            {
                index++;
            }
        }

        if (input[index..].IndexOf((byte)'\n') >= 0)
        {
            return false;
        }

        matchedLength = input.Length;
        return true;
    }

    public static bool TryMatchQuotedStringPrefix(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        if (input.Length < 2 || input[0] != (byte)'"')
        {
            return false;
        }

        var index = 1;
        while ((uint)index < (uint)input.Length)
        {
            var special = input[index..].IndexOfAny(s_utf8QuotedStringSpecials);
            if (special < 0)
            {
                return false;
            }

            index += special;
            switch (input[index])
            {
                case (byte)'"':
                    matchedLength = index + 1;
                    return true;

                case (byte)'\n':
                    return false;

                case (byte)'\\':
                    if ((uint)(index + 1) >= (uint)input.Length || input[index + 1] == (byte)'\n')
                    {
                        return false;
                    }

                    index += 2;
                    break;
            }
        }

        return false;
    }

    private static bool IsAsciiIdentifierHeadIgnoreCase(byte value)
    {
        value = (byte)(value | 0x20);
        return (uint)(value - 'a') <= ('z' - 'a');
    }

    private static int CountLeadingDigits(ReadOnlySpan<byte> input)
    {
        var stop = input.IndexOfAnyExceptInRange((byte)'0', (byte)'9');
        return stop < 0 ? input.Length : stop;
    }

    private static bool IsAsciiDigit(byte value) => (uint)(value - '0') <= 9;

    private static bool IsAsciiOperator(byte value) => s_asciiOperators.Contains(value);
}
