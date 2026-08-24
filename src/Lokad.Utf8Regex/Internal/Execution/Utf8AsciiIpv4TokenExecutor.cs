using System.Runtime.CompilerServices;

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

        // Every accepted first octet has two or three digits, so its dot gives
        // exactly two candidate starts and is much rarer than a digit in text.
        var searchIndex = Math.Max(startIndex + 2, 2);
        while (searchIndex < input.Length)
        {
            var relative = input[searchIndex..].IndexOf((byte)'.');
            if (relative < 0)
            {
                return false;
            }

            var firstDot = searchIndex + relative;
            var threeDigitStart = firstDot - 3;
            if (threeDigitStart >= startIndex &&
                TryMatchAt(input[threeDigitStart..], out matchedLength))
            {
                matchIndex = threeDigitStart;
                return true;
            }

            var twoDigitStart = firstDot - 2;
            if (twoDigitStart >= startIndex &&
                TryMatchAt(input[twoDigitStart..], out matchedLength))
            {
                matchIndex = twoDigitStart;
                return true;
            }

            searchIndex = firstDot + 1;
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
        // The benchmark and corpus-heavy shape has three leading three-digit
        // octets. Its final octet greedily takes three digits when they form a
        // value through 255 and otherwise retains the legal two-digit match.
        if (TryMatchThreeDigitPrefix(input, out matchedLength))
        {
            return true;
        }

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

        static bool TryMatchThreeDigitPrefix(ReadOnlySpan<byte> input, out int matchedLength)
        {
            matchedLength = 0;
            if (input.Length < 14 ||
                input[3] != (byte)'.' ||
                input[7] != (byte)'.' ||
                input[11] != (byte)'.' ||
                !IsThreeDigitOctet(input[0], input[1], input[2]) ||
                !IsThreeDigitOctet(input[4], input[5], input[6]) ||
                !IsThreeDigitOctet(input[8], input[9], input[10]) ||
                !IsAsciiDigit(input[12]) ||
                !IsAsciiDigit(input[13]))
            {
                return false;
            }

            matchedLength = input.Length >= 15 && IsThreeDigitOctet(input[12], input[13], input[14])
                ? 15
                : 14;
            return true;

            static bool IsThreeDigitOctet(byte first, byte second, byte third)
            {
                return IsAsciiDigit(second) &&
                    IsAsciiDigit(third) &&
                    (first is (byte)'0' or (byte)'1' ||
                     first == (byte)'2' &&
                     (second < (byte)'5' || second == (byte)'5' && third <= (byte)'5'));
            }
        }
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

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAsciiDigit(byte value) => Utf8AsciiBytePredicates.IsDigit(value);
}
