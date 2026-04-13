using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiDelimitedTokenExecutor
{
    public static bool TryFindDelimitedToken(
        ReadOnlySpan<byte> input,
        int startIndex,
        ReadOnlySpan<byte> delimiterUtf8,
        ReadOnlySpan<byte> secondaryDelimiterUtf8,
        ReadOnlySpan<byte> headCharSetUtf8,
        ReadOnlySpan<byte> middleCharSetUtf8,
        ReadOnlySpan<byte> tailCharSetUtf8,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;

        if (startIndex < 0 ||
            startIndex >= input.Length ||
            delimiterUtf8.IsEmpty ||
            secondaryDelimiterUtf8.Length != 1 ||
            headCharSetUtf8.IsEmpty ||
            middleCharSetUtf8.IsEmpty ||
            tailCharSetUtf8.IsEmpty)
        {
            return false;
        }

        var delimiterFinder = new Utf8AsciiLiteralFinder(delimiterUtf8);
        var headSearchValues = SearchValues.Create(headCharSetUtf8);
        var middleSearchValues = SearchValues.Create(middleCharSetUtf8);
        var tailSearchValues = SearchValues.Create(tailCharSetUtf8);
        var secondaryDelimiter = secondaryDelimiterUtf8[0];
        var searchFrom = startIndex;

        while (delimiterFinder.TryFindNext(input, searchFrom, out var delimiterIndex))
        {
            var tokenStart = delimiterIndex;
            while (tokenStart > searchFrom && headSearchValues.Contains(input[tokenStart - 1]))
            {
                tokenStart--;
            }

            if (tokenStart == delimiterIndex)
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var middleStart = delimiterIndex + delimiterUtf8.Length;
            if ((uint)middleStart >= (uint)input.Length ||
                !middleSearchValues.Contains(input[middleStart]))
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var tokenEnd = middleStart + 1;
            while ((uint)tokenEnd < (uint)input.Length &&
                IsPotentialTailByte(input[tokenEnd], middleSearchValues, tailSearchValues, secondaryDelimiter))
            {
                tokenEnd++;
            }

            if (!TryFindTailSplit(input, middleStart, tokenEnd, secondaryDelimiter, tailSearchValues))
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            matchIndex = tokenStart;
            matchedLength = tokenEnd - tokenStart;
            return true;
        }

        return false;
    }

    public static int CountDelimitedTokens(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> delimiterUtf8,
        ReadOnlySpan<byte> secondaryDelimiterUtf8,
        ReadOnlySpan<byte> headCharSetUtf8,
        ReadOnlySpan<byte> middleCharSetUtf8,
        ReadOnlySpan<byte> tailCharSetUtf8)
    {
        if (delimiterUtf8.IsEmpty ||
            secondaryDelimiterUtf8.Length != 1 ||
            headCharSetUtf8.IsEmpty ||
            middleCharSetUtf8.IsEmpty ||
            tailCharSetUtf8.IsEmpty)
        {
            return 0;
        }

        var count = 0;
        var searchFrom = 0;

        while (TryFindDelimitedToken(
            input,
            searchFrom,
            delimiterUtf8,
            secondaryDelimiterUtf8,
            headCharSetUtf8,
            middleCharSetUtf8,
            tailCharSetUtf8,
            out var matchIndex,
            out var matchedLength))
        {
            count++;
            searchFrom = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public static int CountDelimitedTokens(ReadOnlySpan<byte> input, PreparedAsciiDelimitedTokenSearch search)
    {
        return search.Count(input);
    }

    public static bool TryFindDelimitedToken(
        ReadOnlySpan<byte> input,
        int startIndex,
        PreparedAsciiDelimitedTokenSearch search,
        out int matchIndex,
        out int matchedLength)
    {
        return search.TryFindNext(input, startIndex, out matchIndex, out matchedLength);
    }

    private static bool IsPotentialTailByte(byte value, SearchValues<byte> middleSearchValues, SearchValues<byte> tailSearchValues, byte secondaryDelimiter)
    {
        return value == secondaryDelimiter ||
            middleSearchValues.Contains(value) ||
            tailSearchValues.Contains(value);
    }

    private static bool TryFindTailSplit(ReadOnlySpan<byte> input, int middleStart, int tokenEnd, byte secondaryDelimiter, SearchValues<byte> tailSearchValues)
    {
        for (var secondaryIndex = tokenEnd - 1; secondaryIndex > middleStart; secondaryIndex--)
        {
            if (input[secondaryIndex] != secondaryDelimiter)
            {
                continue;
            }

            var tailStart = secondaryIndex + 1;
            if ((uint)tailStart >= (uint)input.Length ||
                tailStart >= tokenEnd ||
                !tailSearchValues.Contains(input[tailStart]))
            {
                continue;
            }

            var isTail = true;
            for (var index = tailStart; index < tokenEnd; index++)
            {
                if (!tailSearchValues.Contains(input[index]))
                {
                    isTail = false;
                    break;
                }
            }

            if (isTail)
            {
                return true;
            }
        }

        return false;
    }
}
