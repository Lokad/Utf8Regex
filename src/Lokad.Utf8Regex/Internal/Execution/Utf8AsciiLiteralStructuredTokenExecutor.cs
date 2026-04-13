using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiLiteralStructuredTokenExecutor
{
    public static bool TryFindStructuredToken(
        ReadOnlySpan<byte> input,
        int startIndex,
        PreparedAsciiLiteralStructuredTokenSearch search,
        out int matchIndex,
        out int matchedLength)
    {
        return search.TryFindNext(input, startIndex, out matchIndex, out matchedLength);
    }

    public static bool TryFindStructuredToken(
        ReadOnlySpan<byte> input,
        int startIndex,
        ReadOnlySpan<byte> literalUtf8,
        ReadOnlySpan<byte> headCharSetUtf8,
        ReadOnlySpan<byte> firstBodyCharSetUtf8,
        ReadOnlySpan<byte> secondBodyCharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter1Utf8,
        ReadOnlySpan<byte> optionalTail1CharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter2Utf8,
        ReadOnlySpan<byte> optionalTail2CharSetUtf8,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;

        if (startIndex < 0 ||
            startIndex >= input.Length ||
            literalUtf8.IsEmpty ||
            headCharSetUtf8.IsEmpty ||
            firstBodyCharSetUtf8.IsEmpty ||
            secondBodyCharSetUtf8.IsEmpty ||
            optionalDelimiter1Utf8.Length > 1 ||
            optionalDelimiter2Utf8.Length > 1)
        {
            return false;
        }

        var finder = new Utf8AsciiLiteralFinder(literalUtf8);
        var headSearch = SearchValues.Create(headCharSetUtf8);
        var firstBodySearch = SearchValues.Create(firstBodyCharSetUtf8);
        var secondBodySearch = SearchValues.Create(secondBodyCharSetUtf8);
        var hasOptionalTail1 = !optionalTail1CharSetUtf8.IsEmpty;
        var optionalTail1Search = hasOptionalTail1 ? SearchValues.Create(optionalTail1CharSetUtf8) : SearchValues.Create(" "u8);
        var hasOptionalTail2 = !optionalTail2CharSetUtf8.IsEmpty;
        var optionalTail2Search = hasOptionalTail2 ? SearchValues.Create(optionalTail2CharSetUtf8) : SearchValues.Create(" "u8);
        var optionalDelimiter1 = optionalDelimiter1Utf8.IsEmpty ? (byte)0 : optionalDelimiter1Utf8[0];
        var optionalDelimiter2 = optionalDelimiter2Utf8.IsEmpty ? (byte)0 : optionalDelimiter2Utf8[0];
        var searchFrom = startIndex;

        while (finder.TryFindNext(input, searchFrom, out var literalIndex))
        {
            var tokenStart = literalIndex;
            while (tokenStart > searchFrom && headSearch.Contains(input[tokenStart - 1]))
            {
                tokenStart--;
            }

            if (tokenStart == literalIndex)
            {
                searchFrom = literalIndex + 1;
                continue;
            }

            var firstBodyStart = literalIndex + literalUtf8.Length;
            if ((uint)firstBodyStart >= (uint)input.Length ||
                !firstBodySearch.Contains(input[firstBodyStart]))
            {
                searchFrom = literalIndex + 1;
                continue;
            }

            var bodyEnd = firstBodyStart + 1;
            while ((uint)bodyEnd < (uint)input.Length && secondBodySearch.Contains(input[bodyEnd]))
            {
                bodyEnd++;
            }

            if (bodyEnd - firstBodyStart < 2)
            {
                searchFrom = literalIndex + 1;
                continue;
            }

            var firstBodyMaxEnd = firstBodyStart + 1;
            while (firstBodyMaxEnd < bodyEnd && firstBodySearch.Contains(input[firstBodyMaxEnd]))
            {
                firstBodyMaxEnd++;
            }

            if (firstBodyMaxEnd <= firstBodyStart)
            {
                searchFrom = literalIndex + 1;
                continue;
            }

            var tokenEnd = bodyEnd;
            if (hasOptionalTail1 && optionalDelimiter1 != 0 && (uint)tokenEnd < (uint)input.Length && input[tokenEnd] == optionalDelimiter1)
            {
                tokenEnd++;
                while ((uint)tokenEnd < (uint)input.Length && optionalTail1Search.Contains(input[tokenEnd]))
                {
                    tokenEnd++;
                }
            }

            if (hasOptionalTail2 && optionalDelimiter2 != 0 && (uint)tokenEnd < (uint)input.Length && input[tokenEnd] == optionalDelimiter2)
            {
                tokenEnd++;
                while ((uint)tokenEnd < (uint)input.Length && optionalTail2Search.Contains(input[tokenEnd]))
                {
                    tokenEnd++;
                }
            }

            matchIndex = tokenStart;
            matchedLength = tokenEnd - tokenStart;
            return true;
        }

        return false;
    }

    public static int CountStructuredTokens(
        ReadOnlySpan<byte> input,
        PreparedAsciiLiteralStructuredTokenSearch search)
    {
        return search.Count(input);
    }

    public static int CountStructuredTokens(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literalUtf8,
        ReadOnlySpan<byte> headCharSetUtf8,
        ReadOnlySpan<byte> firstBodyCharSetUtf8,
        ReadOnlySpan<byte> secondBodyCharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter1Utf8,
        ReadOnlySpan<byte> optionalTail1CharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter2Utf8,
        ReadOnlySpan<byte> optionalTail2CharSetUtf8)
    {
        if (literalUtf8.IsEmpty ||
            headCharSetUtf8.IsEmpty ||
            firstBodyCharSetUtf8.IsEmpty ||
            secondBodyCharSetUtf8.IsEmpty ||
            optionalDelimiter1Utf8.Length > 1 ||
            optionalDelimiter2Utf8.Length > 1)
        {
            return 0;
        }

        var count = 0;
        var searchFrom = 0;

        while (TryFindStructuredToken(
            input,
            searchFrom,
            literalUtf8,
            headCharSetUtf8,
            firstBodyCharSetUtf8,
            secondBodyCharSetUtf8,
            optionalDelimiter1Utf8,
            optionalTail1CharSetUtf8,
            optionalDelimiter2Utf8,
            optionalTail2CharSetUtf8,
            out var matchIndex,
            out var matchedLength))
        {
            count++;
            searchFrom = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }
}
