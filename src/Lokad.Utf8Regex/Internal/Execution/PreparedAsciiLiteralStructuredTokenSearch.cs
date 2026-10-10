namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct PreparedAsciiLiteralStructuredTokenSearch
{
    private readonly Utf8AsciiLiteralFinder _literalFinder;
    private readonly AsciiCharClass _headMask;
    private readonly AsciiCharClass _firstBodyMask;
    private readonly AsciiCharClass _secondBodyMask;
    private readonly AsciiCharClass _optionalTail1Mask;
    private readonly AsciiCharClass _optionalTail2Mask;

    public PreparedAsciiLiteralStructuredTokenSearch(
        ReadOnlySpan<byte> literalUtf8,
        ReadOnlySpan<byte> headCharSetUtf8,
        ReadOnlySpan<byte> firstBodyCharSetUtf8,
        ReadOnlySpan<byte> secondBodyCharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter1Utf8,
        ReadOnlySpan<byte> optionalTail1CharSetUtf8,
        ReadOnlySpan<byte> optionalDelimiter2Utf8,
        ReadOnlySpan<byte> optionalTail2CharSetUtf8)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork((ulong)headCharSetUtf8.Length + (ulong)firstBodyCharSetUtf8.Length +
                (ulong)secondBodyCharSetUtf8.Length + (ulong)optionalTail1CharSetUtf8.Length + (ulong)optionalTail2CharSetUtf8.Length);
            _headMask = AsciiCharClass.FromBytes(headCharSetUtf8);
            _firstBodyMask = AsciiCharClass.FromBytes(firstBodyCharSetUtf8);
            _secondBodyMask = AsciiCharClass.FromBytes(secondBodyCharSetUtf8);
            _optionalTail1Mask = AsciiCharClass.FromBytes(optionalTail1CharSetUtf8);
            _optionalTail2Mask = AsciiCharClass.FromBytes(optionalTail2CharSetUtf8);
            // Absence is distinct from the NUL scalar; a present delimiter with
            // an empty star class still consumes its delimiter.
            OptionalDelimiter1 = optionalDelimiter1Utf8.IsEmpty ? null : optionalDelimiter1Utf8[0];
            OptionalDelimiter2 = optionalDelimiter2Utf8.IsEmpty ? null : optionalDelimiter2Utf8[0];
            _literalFinder = new Utf8AsciiLiteralFinder(literalUtf8);
            LiteralLength = literalUtf8.Length;
            scope.Complete();
        }
        finally { budget.LeaveDepth(); }
    }

    public int LiteralLength { get; }

    public byte? OptionalDelimiter1 { get; }

    public byte? OptionalDelimiter2 { get; }

    public bool HasValue => LiteralLength > 0;

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
        while (_literalFinder.TryFindNext(input, searchFrom, out var literalIndex))
        {
            var tokenStart = literalIndex;
            while (tokenStart > searchFrom && _headMask.Contains(input[tokenStart - 1]))
            {
                tokenStart--;
            }

            if (tokenStart == literalIndex)
            {
                searchFrom = literalIndex + 1;
                continue;
            }

            var firstBodyStart = literalIndex + LiteralLength;
            if ((uint)firstBodyStart >= (uint)input.Length ||
                !_firstBodyMask.Contains(input[firstBodyStart]))
            {
                searchFrom = literalIndex + 1;
                continue;
            }

            // First+ and Second+ are independent greedy runs. First need not
            // be a subset of Second. Keep the latest first-run split that leaves
            // one Second member, then consume the complete second run.
            var firstEnd = firstBodyStart + 1;
            while ((uint)firstEnd < (uint)input.Length && _firstBodyMask.Contains(input[firstEnd])) firstEnd++;
            var secondStart = firstEnd;
            while (secondStart > firstBodyStart &&
                ((uint)secondStart >= (uint)input.Length || !_secondBodyMask.Contains(input[secondStart]))) secondStart--;
            if (secondStart <= firstBodyStart)
            {
                searchFrom = literalIndex + 1;
                continue;
            }
            var bodyEnd = secondStart + 1;
            while ((uint)bodyEnd < (uint)input.Length && _secondBodyMask.Contains(input[bodyEnd])) bodyEnd++;

            var tokenEnd = bodyEnd;
            if (OptionalDelimiter1 is { } delimiter1 &&
                (uint)tokenEnd < (uint)input.Length &&
                input[tokenEnd] == delimiter1)
            {
                tokenEnd++;
                while ((uint)tokenEnd < (uint)input.Length && _optionalTail1Mask.Contains(input[tokenEnd]))
                {
                    tokenEnd++;
                }
            }

            if (OptionalDelimiter2 is { } delimiter2 &&
                (uint)tokenEnd < (uint)input.Length &&
                input[tokenEnd] == delimiter2)
            {
                tokenEnd++;
                while ((uint)tokenEnd < (uint)input.Length && _optionalTail2Mask.Contains(input[tokenEnd]))
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

    public int Count(ReadOnlySpan<byte> input)
    {
        if (!HasValue)
        {
            return 0;
        }

        var count = 0;
        var searchFrom = 0;
        while (TryFindNext(input, searchFrom, out var matchIndex, out var matchedLength))
        {
            count++;
            searchFrom = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }
}
