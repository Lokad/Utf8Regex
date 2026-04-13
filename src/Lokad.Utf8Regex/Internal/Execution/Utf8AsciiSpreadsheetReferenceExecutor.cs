namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiSpreadsheetReferenceExecutor
{
    public static bool TryMatchCellReferenceWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        if (input.Length < 2 || !IsAsciiLetterIgnoreCase(input[0]))
        {
            return false;
        }

        var index = 1;
        if (!IsAsciiDigit(input[index]))
        {
            return false;
        }

        index++;
        while (index < input.Length && IsAsciiDigit(input[index]))
        {
            index++;
        }

        if (index != input.Length)
        {
            return false;
        }

        matchedLength = index;
        return true;
    }

    public static bool TryMatchRangeReferenceWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        if (!TryConsumeCellReference(input, 0, out var index))
        {
            return false;
        }

        if (index < input.Length && input[index] == (byte)':')
        {
            index++;
        }

        if (!TryConsumeCellReference(input, index, out index) || index != input.Length)
        {
            return false;
        }

        matchedLength = index;
        return true;
    }

    private static bool TryConsumeCellReference(ReadOnlySpan<byte> input, int startIndex, out int index)
    {
        index = startIndex;
        if ((uint)index >= (uint)input.Length || !IsAsciiLetterIgnoreCase(input[index]))
        {
            return false;
        }

        index++;
        if ((uint)index >= (uint)input.Length || !IsAsciiDigit(input[index]))
        {
            return false;
        }

        index++;
        while ((uint)index < (uint)input.Length && IsAsciiDigit(input[index]))
        {
            index++;
        }

        return true;
    }

    private static bool IsAsciiLetterIgnoreCase(byte value)
    {
        value = Internal.Utilities.AsciiSearch.FoldCase(value);
        return value is >= (byte)'a' and <= (byte)'z';
    }

    private static bool IsAsciiDigit(byte value)
    {
        return value is >= (byte)'0' and <= (byte)'9';
    }
}
