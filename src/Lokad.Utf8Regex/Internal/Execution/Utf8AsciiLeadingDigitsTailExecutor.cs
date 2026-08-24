namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiLeadingDigitsTailExecutor
{
    public static bool TryMatchPrintableAsciiWhole(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> separatorBytes,
        out bool isMatch)
    {
        isMatch = false;
        var index = 0;
        while ((uint)index < (uint)input.Length && IsAsciiDigit(input[index]))
        {
            index++;
        }

        if (index == 0)
        {
            return input.IndexOfAnyExceptInRange((byte)0x20, (byte)0x7F) < 0;
        }

        if (index < input.Length)
        {
            if (separatorBytes.IsEmpty || separatorBytes.IndexOf(input[index]) < 0)
            {
                return input.IndexOfAnyExceptInRange((byte)0x20, (byte)0x7F) < 0;
            }

            index++;
            if (input[index..].IndexOfAnyExceptInRange((byte)0x20, (byte)0x7F) >= 0)
            {
                return false;
            }
        }

        isMatch = true;
        return true;
    }

    public static bool TryMatchWhole(ReadOnlySpan<byte> input, ReadOnlySpan<byte> separatorBytes, out int matchedLength)
    {
        if (input.IsEmpty)
        {
            matchedLength = 0;
            return false;
        }

        var effectiveLength = input.Length;
        if (effectiveLength > 0 && input[effectiveLength - 1] == (byte)'\n')
        {
            effectiveLength--;
        }

        return TryMatchWholeCore(input, separatorBytes, effectiveLength, out matchedLength);
    }

    private static bool TryMatchWholeCore(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> separatorBytes,
        int effectiveLength,
        out int matchedLength)
    {
        matchedLength = 0;
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

            if (separatorBytes.IndexOf(input[index]) < 0)
            {
                return false;
            }

            index++;
            if (index < effectiveLength &&
                input[index..effectiveLength].IndexOf((byte)'\n') >= 0)
            {
                return false;
            }
        }

        matchedLength = effectiveLength;
        return true;
    }

    private static bool IsAsciiDigit(byte value) => Utf8AsciiBytePredicates.IsDigit(value);
}
