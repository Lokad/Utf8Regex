using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Execution;

internal readonly struct PreparedAsciiLiteralStructuredTokenSearch
{
    private readonly Utf8AsciiLiteralFinder _literalFinder;
    private readonly SearchValues<byte> _headSearchValues;
    private readonly SearchValues<byte> _firstBodySearchValues;
    private readonly SearchValues<byte> _secondBodySearchValues;
    private readonly SearchValues<byte>? _optionalTail1SearchValues;
    private readonly SearchValues<byte>? _optionalTail2SearchValues;

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
        _literalFinder = new Utf8AsciiLiteralFinder(literalUtf8);
        _headSearchValues = SearchValues.Create(headCharSetUtf8);
        _firstBodySearchValues = SearchValues.Create(firstBodyCharSetUtf8);
        _secondBodySearchValues = SearchValues.Create(secondBodyCharSetUtf8);
        _optionalTail1SearchValues = optionalTail1CharSetUtf8.IsEmpty ? null : SearchValues.Create(optionalTail1CharSetUtf8);
        _optionalTail2SearchValues = optionalTail2CharSetUtf8.IsEmpty ? null : SearchValues.Create(optionalTail2CharSetUtf8);
        OptionalDelimiter1 = optionalDelimiter1Utf8.IsEmpty ? (byte)0 : optionalDelimiter1Utf8[0];
        OptionalDelimiter2 = optionalDelimiter2Utf8.IsEmpty ? (byte)0 : optionalDelimiter2Utf8[0];
        LiteralLength = literalUtf8.Length;
    }

    public int LiteralLength { get; }

    public byte OptionalDelimiter1 { get; }

    public byte OptionalDelimiter2 { get; }

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
            while (tokenStart > searchFrom && _headSearchValues.Contains(input[tokenStart - 1]))
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
                !_firstBodySearchValues.Contains(input[firstBodyStart]))
            {
                searchFrom = literalIndex + 1;
                continue;
            }

            var bodyEnd = firstBodyStart + 1;
            while ((uint)bodyEnd < (uint)input.Length && _secondBodySearchValues.Contains(input[bodyEnd]))
            {
                bodyEnd++;
            }

            if (bodyEnd - firstBodyStart < 2)
            {
                searchFrom = literalIndex + 1;
                continue;
            }

            var firstBodyMaxEnd = firstBodyStart + 1;
            while (firstBodyMaxEnd < bodyEnd && _firstBodySearchValues.Contains(input[firstBodyMaxEnd]))
            {
                firstBodyMaxEnd++;
            }

            if (firstBodyMaxEnd <= firstBodyStart)
            {
                searchFrom = literalIndex + 1;
                continue;
            }

            var tokenEnd = bodyEnd;
            if (_optionalTail1SearchValues is { } optionalTail1Search &&
                OptionalDelimiter1 != 0 &&
                (uint)tokenEnd < (uint)input.Length &&
                input[tokenEnd] == OptionalDelimiter1)
            {
                tokenEnd++;
                while ((uint)tokenEnd < (uint)input.Length && optionalTail1Search.Contains(input[tokenEnd]))
                {
                    tokenEnd++;
                }
            }

            if (_optionalTail2SearchValues is { } optionalTail2Search &&
                OptionalDelimiter2 != 0 &&
                (uint)tokenEnd < (uint)input.Length &&
                input[tokenEnd] == OptionalDelimiter2)
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
