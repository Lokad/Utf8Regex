namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly partial struct PreparedAsciiDelimitedTokenSearch
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

    public PreparedAsciiDelimitedTokenSearch(ReadOnlySpan<byte> delimiterUtf8,
        ReadOnlySpan<byte> secondaryDelimiterUtf8, Utf8FallbackTokenMasks masks)
    {
        using var scope = Utf8ConstructionScope.Enter(); var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork(1);
            _headMask = masks.Head; _middleMask = masks.Middle; _tailMask = masks.Tail;
            _secondaryDelimiter = secondaryDelimiterUtf8[0];
            _delimiterFinder = new Utf8AsciiLiteralFinder(delimiterUtf8); DelimiterLength = delimiterUtf8.Length;
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

            var middleEnd = middleStart + 1;
            while ((uint)middleEnd < (uint)input.Length && _middleMask.Contains(input[middleEnd])) middleEnd++;
            if (!TryFindTailSplit(input, middleStart, middleEnd, out var tokenEnd))
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
        var count = 0; var searchFrom = 0;
        while (TryFindNext(input, searchFrom, out var index, out var length))
        { count++; searchFrom = index + length; }
        return count;
    }

    private bool TryFindTailSplit(ReadOnlySpan<byte> input, int middleStart, int middleEnd, out int tokenEnd)
    {
        tokenEnd = 0;
        // Greedy middle may contain the separator, but every byte before the
        // selected separator must belong to middle. Then tail is greedy alone.
        for (var split = middleEnd; split > middleStart; split--)
        {
            if ((uint)split >= (uint)input.Length || input[split] != _secondaryDelimiter ||
                (uint)(split + 1) >= (uint)input.Length || !_tailMask.Contains(input[split + 1])) continue;
            tokenEnd = split + 2;
            while ((uint)tokenEnd < (uint)input.Length && _tailMask.Contains(input[tokenEnd])) tokenEnd++;
            return true;
        }
        return false;
    }
}
