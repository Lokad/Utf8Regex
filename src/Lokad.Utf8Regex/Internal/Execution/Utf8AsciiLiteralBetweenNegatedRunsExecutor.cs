namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiLiteralBetweenNegatedRunsExecutor
{
    public static bool TryFind(
        ReadOnlySpan<byte> input,
        int startIndex,
        byte literal,
        byte excludedHead,
        byte excludedTail,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if ((uint)startIndex >= (uint)input.Length || input.Length < 3)
        {
            return false;
        }

        var searchIndex = Math.Max(startIndex, 0);
        if (searchIndex == 0)
        {
            searchIndex = 1;
        }

        while (searchIndex < input.Length - 1)
        {
            var relative = input[searchIndex..^1].IndexOf(literal);
            if (relative < 0)
            {
                return false;
            }

            var literalIndex = searchIndex + relative;
            if (input[literalIndex - 1] == excludedHead || input[literalIndex + 1] == excludedTail)
            {
                searchIndex = literalIndex + 1;
                continue;
            }

            var left = literalIndex - 1;
            while (left > 0 && input[left - 1] != excludedHead)
            {
                left--;
            }

            var right = literalIndex + 2;
            while (right < input.Length && input[right] != excludedTail)
            {
                right++;
            }

            matchIndex = left;
            matchedLength = right - left;
            return true;
        }

        return false;
    }
}
