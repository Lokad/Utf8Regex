using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiEmailWholeExecutor
{
    private static readonly SearchValues<byte> s_localChars = SearchValues.Create("-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz"u8);
    private static readonly SearchValues<byte> s_domainLabelChars = SearchValues.Create("-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);
    private static readonly SearchValues<byte> s_asciiLetters = SearchValues.Create("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

    public static bool TryMatchWhole(ReadOnlySpan<byte> input, out int matchedLength)
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

        if (effectiveLength < 5)
        {
            return false;
        }

        var index = 0;
        if (!TryConsumeRun(input, ref index, effectiveLength, s_localChars, minCount: 1) ||
            !TryConsumeByte(input, ref index, effectiveLength, (byte)'@'))
        {
            return false;
        }

        if (index < effectiveLength && input[index] == (byte)'[')
        {
            index++;
            if (!TryConsumeDigitRun(input, ref index, effectiveLength, 1, 3) ||
                !TryConsumeByte(input, ref index, effectiveLength, (byte)'.') ||
                !TryConsumeDigitRun(input, ref index, effectiveLength, 1, 3) ||
                !TryConsumeByte(input, ref index, effectiveLength, (byte)'.') ||
                !TryConsumeDigitRun(input, ref index, effectiveLength, 1, 3) ||
                !TryConsumeByte(input, ref index, effectiveLength, (byte)'.'))
            {
                return false;
            }
        }
        else
        {
            var labelCount = 0;
            while (true)
            {
                var segmentStart = index;
                if (!TryConsumeRun(input, ref index, effectiveLength, s_domainLabelChars, minCount: 1))
                {
                    return false;
                }

                if (index >= effectiveLength || input[index] != (byte)'.')
                {
                    index = segmentStart;
                    break;
                }

                index++;
                labelCount++;
            }

            if (labelCount == 0)
            {
                return false;
            }
        }

        if (!TryConsumeAsciiLettersOrDigitsTail(input, ref index, effectiveLength))
        {
            return false;
        }

        if (index < effectiveLength && input[index] == (byte)']')
        {
            index++;
        }

        if (index != effectiveLength)
        {
            return false;
        }

        matchedLength = effectiveLength;
        return true;
    }

    private static bool TryConsumeAsciiLettersOrDigitsTail(ReadOnlySpan<byte> input, ref int index, int effectiveLength)
    {
        var letterIndex = index;
        var letterCount = 0;
        while (letterIndex < effectiveLength && letterCount < 12 && s_asciiLetters.Contains(input[letterIndex]))
        {
            letterIndex++;
            letterCount++;
        }

        if (letterCount >= 2)
        {
            index = letterIndex;
            return true;
        }

        return TryConsumeDigitRun(input, ref index, effectiveLength, 1, 3);
    }

    private static bool TryConsumeDigitRun(ReadOnlySpan<byte> input, ref int index, int effectiveLength, int minCount, int maxCount)
    {
        var count = 0;
        while (index < effectiveLength && count < maxCount && IsAsciiDigit(input[index]))
        {
            index++;
            count++;
        }

        return count >= minCount;
    }

    private static bool TryConsumeRun(ReadOnlySpan<byte> input, ref int index, int effectiveLength, SearchValues<byte> values, int minCount)
    {
        var count = 0;
        while (index < effectiveLength && values.Contains(input[index]))
        {
            index++;
            count++;
        }

        return count >= minCount;
    }

    private static bool TryConsumeByte(ReadOnlySpan<byte> input, ref int index, int effectiveLength, byte value)
    {
        if (index >= effectiveLength || input[index] != value)
        {
            return false;
        }

        index++;
        return true;
    }

    private static bool IsAsciiDigit(byte value) => (uint)(value - '0') <= 9;
}
