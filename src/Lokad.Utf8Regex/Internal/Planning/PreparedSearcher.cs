using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Planning;

internal enum PreparedSearcherKind : byte
{
    None = 0,
    ExactLiteral = 1,
    IgnoreCaseLiteral = 2,
    MultiLiteral = 3,
    ByteSet = 4,
    QuotedAsciiRun = 5,
}

internal readonly struct PreparedSearcher
{
    public PreparedSearcher(PreparedSubstringSearch literalSearch, bool ignoreCase)
    {
        LiteralSearch = literalSearch;
        MultiLiteralSearch = default;
        Kind = ignoreCase ? PreparedSearcherKind.IgnoreCaseLiteral : PreparedSearcherKind.ExactLiteral;
    }

    public PreparedSearcher(PreparedMultiLiteralSearch multiLiteralSearch)
    {
        LiteralSearch = default;
        ByteSearch = default;
        MultiLiteralSearch = multiLiteralSearch;
        Kind = PreparedSearcherKind.MultiLiteral;
    }

    public PreparedSearcher(PreparedByteSearch byteSearch)
    {
        LiteralSearch = default;
        ByteSearch = byteSearch;
        MultiLiteralSearch = default;
        QuotedAsciiRunSearch = default;
        Kind = PreparedSearcherKind.ByteSet;
    }

    public PreparedSearcher(PreparedQuotedAsciiRunSearch quotedAsciiRunSearch)
    {
        LiteralSearch = default;
        ByteSearch = default;
        MultiLiteralSearch = default;
        QuotedAsciiRunSearch = quotedAsciiRunSearch;
        Kind = PreparedSearcherKind.QuotedAsciiRun;
    }

    private PreparedSubstringSearch LiteralSearch { get; }

    private PreparedByteSearch ByteSearch { get; }

    private PreparedMultiLiteralSearch MultiLiteralSearch { get; }

    private PreparedQuotedAsciiRunSearch QuotedAsciiRunSearch { get; }

    public PreparedSearcherKind Kind { get; }

    public bool HasValue => Kind != PreparedSearcherKind.None;

    public int FindFirst(ReadOnlySpan<byte> input)
    {
        return Kind switch
        {
            PreparedSearcherKind.ExactLiteral or PreparedSearcherKind.IgnoreCaseLiteral => LiteralSearch.IndexOf(input),
            PreparedSearcherKind.MultiLiteral => MultiLiteralSearch.IndexOf(input),
            PreparedSearcherKind.ByteSet => ByteSearch.IndexOf(input),
            PreparedSearcherKind.QuotedAsciiRun => QuotedAsciiRunSearch.IndexOf(input),
            _ => -1,
        };
    }

    public bool TryFindFirstMatch(ReadOnlySpan<byte> input, out PreparedSearchMatch match)
    {
        match = default;

        if (Kind == PreparedSearcherKind.MultiLiteral &&
            MultiLiteralSearch.TryFindFirstMatch(input, out var multiIndex, out var multiMatchedLength, out var multiLiteralId))
        {
            match = new PreparedSearchMatch(multiIndex, multiMatchedLength, multiLiteralId);
            return true;
        }

        var index = FindFirst(input);
        if (index < 0 || !TryGetMatchedLength(input, index, out var matchedLength))
        {
            return false;
        }

        match = new PreparedSearchMatch(index, matchedLength, 0);
        return true;
    }

    public bool TryFindLastMatch(ReadOnlySpan<byte> input, out PreparedSearchMatch match)
    {
        match = default;

        if (Kind == PreparedSearcherKind.MultiLiteral &&
            MultiLiteralSearch.TryFindLastMatch(input, out var multiIndex, out var multiMatchedLength, out var multiLiteralId))
        {
            match = new PreparedSearchMatch(multiIndex, multiMatchedLength, multiLiteralId);
            return true;
        }

        var index = FindLast(input);
        if (index < 0 || !TryGetMatchedLength(input, index, out var matchedLength))
        {
            return false;
        }

        match = new PreparedSearchMatch(index, matchedLength, 0);
        return true;
    }

    public bool TryFindNextNonOverlappingMatch(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out PreparedSearchMatch match)
    {
        match = default;

        if (Kind != PreparedSearcherKind.MultiLiteral ||
            !MultiLiteralSearch.TryFindNextNonOverlappingMatch(input, ref state, out var index, out var matchedLength, out var literalId))
        {
            return false;
        }

        match = new PreparedSearchMatch(index, matchedLength, literalId);
        return true;
    }

    public bool TryFindNextNonOverlappingLength(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength)
    {
        index = -1;
        matchedLength = 0;

        return Kind == PreparedSearcherKind.MultiLiteral &&
            MultiLiteralSearch.TryFindNextNonOverlappingLength(input, ref state, out index, out matchedLength);
    }

    public bool TryFindNextOverlappingMatch(
        ReadOnlySpan<byte> input,
        ref PreparedSearchScanState state,
        out PreparedSearchMatch match)
    {
        match = default;

        if ((uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        switch (Kind)
        {
            case PreparedSearcherKind.MultiLiteral:
                var multiState = state.MultiLiteralState;
                if (!MultiLiteralSearch.TryFindNextOverlappingMatch(input, ref multiState, out var index, out var matchedLength, out var literalId))
                {
                    state = new PreparedSearchScanState(input.Length, multiState);
                    return false;
                }

                match = new PreparedSearchMatch(index, matchedLength, literalId);
                state = new PreparedSearchScanState(index + 1, multiState);
                return true;

            case PreparedSearcherKind.ExactLiteral:
            case PreparedSearcherKind.IgnoreCaseLiteral:
            case PreparedSearcherKind.QuotedAsciiRun:
                if (!TryFindFirstMatch(input[state.NextStart..], out var relative))
                {
                    state = new PreparedSearchScanState(input.Length, default);
                    return false;
                }

                match = new PreparedSearchMatch(state.NextStart + relative.Index, relative.Length, relative.LiteralId);
                state = new PreparedSearchScanState(match.Index + 1, default);
                return true;

            default:
                return false;
        }
    }

    public int FindLast(ReadOnlySpan<byte> input)
    {
        return Kind switch
        {
            PreparedSearcherKind.ExactLiteral or PreparedSearcherKind.IgnoreCaseLiteral => LiteralSearch.LastIndexOf(input),
            PreparedSearcherKind.MultiLiteral => MultiLiteralSearch.LastIndexOf(input),
            PreparedSearcherKind.ByteSet => ByteSearch.LastIndexOf(input),
            PreparedSearcherKind.QuotedAsciiRun => QuotedAsciiRunSearch.LastIndexOf(input),
            _ => -1,
        };
    }

    public bool TryGetMatchedLength(ReadOnlySpan<byte> input, int index, out int matchedLength)
    {
        matchedLength = 0;

        switch (Kind)
        {
            case PreparedSearcherKind.ExactLiteral:
                var literal = LiteralSearch.Needle;
                if ((uint)index <= (uint)(input.Length - literal.Length) &&
                    input.Slice(index, literal.Length).SequenceEqual(literal))
                {
                    matchedLength = literal.Length;
                    return true;
                }

                return false;

            case PreparedSearcherKind.IgnoreCaseLiteral:
                var ignoreCaseLiteral = LiteralSearch.Needle;
                if ((uint)index <= (uint)(input.Length - ignoreCaseLiteral.Length) &&
                    AsciiSearch.MatchesIgnoreCase(input.Slice(index, ignoreCaseLiteral.Length), ignoreCaseLiteral))
                {
                    matchedLength = ignoreCaseLiteral.Length;
                    return true;
                }

                return false;

            case PreparedSearcherKind.MultiLiteral:
                return MultiLiteralSearch.TryGetMatchedLiteralLength(input, index, out matchedLength);

            case PreparedSearcherKind.ByteSet:
                if ((uint)index < (uint)input.Length)
                {
                    matchedLength = 1;
                    return true;
                }

                return false;

            case PreparedSearcherKind.QuotedAsciiRun:
                if (QuotedAsciiRunSearch.IsMatchAt(input, index))
                {
                    matchedLength = QuotedAsciiRunSearch.MatchLength;
                    return true;
                }

                return false;

            default:
                return false;
        }
    }
}

internal readonly record struct PreparedSearchMatch(int Index, int Length, int LiteralId);

internal readonly record struct PreparedSearchScanState(int NextStart, PreparedMultiLiteralScanState MultiLiteralState);
