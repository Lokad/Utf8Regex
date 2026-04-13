using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct PreparedAsciiDelimitedTokenSearch
{
    private readonly Utf8AsciiLiteralFinder _delimiterFinder;
    private readonly SearchValues<byte> _headSearchValues;
    private readonly SearchValues<byte> _middleSearchValues;
    private readonly SearchValues<byte> _tailSearchValues;
    private readonly byte _secondaryDelimiter;

    public PreparedAsciiDelimitedTokenSearch(
        ReadOnlySpan<byte> delimiterUtf8,
        ReadOnlySpan<byte> secondaryDelimiterUtf8,
        ReadOnlySpan<byte> headCharSetUtf8,
        ReadOnlySpan<byte> middleCharSetUtf8,
        ReadOnlySpan<byte> tailCharSetUtf8)
    {
        _delimiterFinder = new Utf8AsciiLiteralFinder(delimiterUtf8);
        _headSearchValues = SearchValues.Create(headCharSetUtf8);
        _middleSearchValues = SearchValues.Create(middleCharSetUtf8);
        _tailSearchValues = SearchValues.Create(tailCharSetUtf8);
        _secondaryDelimiter = secondaryDelimiterUtf8[0];
        DelimiterLength = delimiterUtf8.Length;
    }

    public int DelimiterLength { get; }

    public bool HasValue => DelimiterLength > 0;

    public bool TryFindNext(
        ReadOnlySpan<byte> input,
        int startIndex,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;

        if (!HasValue || (uint)startIndex >= (uint)input.Length)
        {
            return false;
        }

        var searchFrom = startIndex;
        while (_delimiterFinder.TryFindNext(input, searchFrom, out var delimiterIndex))
        {
            var tokenStart = delimiterIndex;
            while (tokenStart > searchFrom && _headSearchValues.Contains(input[tokenStart - 1]))
            {
                tokenStart--;
            }

            if (tokenStart == delimiterIndex)
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var middleStart = delimiterIndex + DelimiterLength;
            if ((uint)middleStart >= (uint)input.Length ||
                !_middleSearchValues.Contains(input[middleStart]))
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var tokenEnd = middleStart + 1;
            while ((uint)tokenEnd < (uint)input.Length &&
                IsPotentialTailByte(input[tokenEnd]))
            {
                tokenEnd++;
            }

            if (!TryFindTailSplit(input, middleStart, tokenEnd))
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

    public int Count(ReadOnlySpan<byte> input)
    {
        if (!HasValue)
        {
            return 0;
        }

        var count = 0;
        var searchFrom = 0;
        while (_delimiterFinder.TryFindNext(input, searchFrom, out var delimiterIndex))
        {
            var tokenStart = delimiterIndex;
            while (tokenStart > searchFrom && _headSearchValues.Contains(input[tokenStart - 1]))
            {
                tokenStart--;
            }

            if (tokenStart == delimiterIndex)
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var middleStart = delimiterIndex + DelimiterLength;
            if ((uint)middleStart >= (uint)input.Length ||
                !_middleSearchValues.Contains(input[middleStart]))
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            var tokenEnd = middleStart + 1;
            while ((uint)tokenEnd < (uint)input.Length &&
                IsPotentialTailByte(input[tokenEnd]))
            {
                tokenEnd++;
            }

            if (!TryFindTailSplit(input, middleStart, tokenEnd))
            {
                searchFrom = delimiterIndex + 1;
                continue;
            }

            count++;
            searchFrom = tokenEnd;
        }

        return count;
    }

    private bool IsPotentialTailByte(byte value)
    {
        return value == _secondaryDelimiter ||
            _middleSearchValues.Contains(value) ||
            _tailSearchValues.Contains(value);
    }

    private bool TryFindTailSplit(ReadOnlySpan<byte> input, int middleStart, int tokenEnd)
    {
        for (var secondaryIndex = tokenEnd - 1; secondaryIndex > middleStart; secondaryIndex--)
        {
            if (input[secondaryIndex] != _secondaryDelimiter)
            {
                continue;
            }

            var tailStart = secondaryIndex + 1;
            if ((uint)tailStart >= (uint)input.Length ||
                tailStart >= tokenEnd ||
                !_tailSearchValues.Contains(input[tailStart]))
            {
                continue;
            }

            var isTail = true;
            for (var index = tailStart; index < tokenEnd; index++)
            {
                if (!_tailSearchValues.Contains(input[index]))
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
