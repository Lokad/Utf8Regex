namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct PreparedAsciiDelimitedTokenSearch
{
    private readonly Utf8AsciiLiteralFinder _delimiterFinder;
    private readonly AsciiCharClass _headMask;
    private readonly AsciiCharClass _middleMask;
    private readonly AsciiCharClass _tailMask;
    private readonly byte _secondaryDelimiter;

    public PreparedAsciiDelimitedTokenSearch(
        ReadOnlySpan<byte> delimiterUtf8,
        ReadOnlySpan<byte> secondaryDelimiterUtf8,
        ReadOnlySpan<byte> headCharSetUtf8,
        ReadOnlySpan<byte> middleCharSetUtf8,
        ReadOnlySpan<byte> tailCharSetUtf8)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork((ulong)headCharSetUtf8.Length + (ulong)middleCharSetUtf8.Length + (ulong)tailCharSetUtf8.Length);
            _headMask = AsciiCharClass.FromBytes(headCharSetUtf8);
            _middleMask = AsciiCharClass.FromBytes(middleCharSetUtf8);
            _tailMask = AsciiCharClass.FromBytes(tailCharSetUtf8);
            _secondaryDelimiter = secondaryDelimiterUtf8[0];
            _delimiterFinder = new Utf8AsciiLiteralFinder(delimiterUtf8);
            DelimiterLength = delimiterUtf8.Length;
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
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
            while (tokenStart > searchFrom && _headMask.Contains(input[tokenStart - 1]))
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
                !_middleMask.Contains(input[middleStart]))
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
            while (tokenStart > searchFrom && _headMask.Contains(input[tokenStart - 1]))
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
                !_middleMask.Contains(input[middleStart]))
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
            _middleMask.Contains(value) ||
            _tailMask.Contains(value);
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
                !_tailMask.Contains(input[tailStart]))
            {
                continue;
            }

            var isTail = true;
            for (var index = tailStart; index < tokenEnd; index++)
            {
                if (!_tailMask.Contains(input[index]))
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
