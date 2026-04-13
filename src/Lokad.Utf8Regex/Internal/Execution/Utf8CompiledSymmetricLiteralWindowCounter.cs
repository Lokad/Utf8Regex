namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8CompiledSymmetricLiteralWindowCounter
{
    private readonly AsciiSimplePatternSymmetricLiteralWindowPlan _plan;
    private readonly byte[] _firstLiteral;
    private readonly byte[] _secondLiteral;
    private readonly int _firstLength;
    private readonly int _secondLength;

    public Utf8CompiledSymmetricLiteralWindowCounter(AsciiSimplePatternSymmetricLiteralWindowPlan plan)
    {
        _plan = plan;
        _firstLiteral = plan.FirstLiteralUtf8;
        _secondLiteral = plan.SecondLiteralUtf8;
        _firstLength = _firstLiteral.Length;
        _secondLength = _secondLiteral.Length;
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        var shortestLength = _plan.SearchData.ShortestLength;
        var count = 0;
        var searchFrom = 0;

        while (searchFrom <= input.Length - shortestLength)
        {
            var anchorSearchStart = searchFrom + _plan.AnchorOffset;
            if (anchorSearchStart > input.Length - 1)
            {
                return count;
            }

            var relative = input[anchorSearchStart..].IndexOfAny(_plan.AnchorByteA, _plan.AnchorByteB);
            if (relative < 0)
            {
                return count;
            }

            var candidateStart = anchorSearchStart + relative - _plan.AnchorOffset;
            if (candidateStart < searchFrom)
            {
                searchFrom = anchorSearchStart + relative + 1 - _plan.AnchorOffset;
                continue;
            }

            if (!PassesFilters(input, candidateStart))
            {
                searchFrom = candidateStart + 1;
                continue;
            }

            var anchorIndex = candidateStart + _plan.AnchorOffset;
            var anchor = input[anchorIndex];
            if (anchor == _plan.AnchorByteA)
            {
                if (!LiteralMatches(input, candidateStart, _firstLiteral))
                {
                    searchFrom = candidateStart + 1;
                    continue;
                }

                if (TryMatchTrailing(input, candidateStart, _firstLength, _secondLiteral, out var matchedLength))
                {
                    count++;
                    searchFrom = candidateStart + matchedLength;
                    continue;
                }

                searchFrom = candidateStart + 1;
                continue;
            }

            if (anchor == _plan.AnchorByteB)
            {
                if (!LiteralMatches(input, candidateStart, _secondLiteral))
                {
                    searchFrom = candidateStart + 1;
                    continue;
                }

                if (TryMatchTrailing(input, candidateStart, _secondLength, _firstLiteral, out var matchedLength))
                {
                    count++;
                    searchFrom = candidateStart + matchedLength;
                    continue;
                }
            }

            searchFrom = candidateStart + 1;
        }

        return count;
    }

    private bool PassesFilters(ReadOnlySpan<byte> input, int candidateStart)
    {
        return PassesFilter(input, candidateStart, _plan.FirstFilterOffset, _plan.FirstFilterByteA, _plan.FirstFilterByteB) &&
            PassesFilter(input, candidateStart, _plan.SecondFilterOffset, _plan.SecondFilterByteA, _plan.SecondFilterByteB);
    }

    private static bool PassesFilter(ReadOnlySpan<byte> input, int candidateStart, int filterOffset, byte firstAllowed, byte secondAllowed)
    {
        if (filterOffset < 0)
        {
            return true;
        }

        var filterIndex = candidateStart + filterOffset;
        if ((uint)filterIndex >= (uint)input.Length)
        {
            return false;
        }

        var value = input[filterIndex];
        return value == firstAllowed || value == secondAllowed;
    }

    private static bool LiteralMatches(ReadOnlySpan<byte> input, int candidateStart, byte[] literal)
    {
        return (uint)(candidateStart + literal.Length) <= (uint)input.Length &&
            input.Slice(candidateStart, literal.Length).SequenceEqual(literal);
    }

    private bool TryMatchTrailing(ReadOnlySpan<byte> input, int candidateStart, int leadingLength, byte[] trailingLiteral, out int matchedLength)
    {
        matchedLength = 0;
        var trailingSearchStart = candidateStart + leadingLength + _plan.MinGap;
        if (trailingSearchStart > input.Length - trailingLiteral.Length)
        {
            return false;
        }

        var trailingSearchEndExclusive = Math.Min(
            input.Length,
            candidateStart + leadingLength + _plan.MaxGap + trailingLiteral.Length);

        if (_plan.GapSameLine)
        {
            var gapStart = candidateStart + leadingLength;
            var newlineOffset = input[gapStart..trailingSearchEndExclusive].IndexOfAny((byte)'\r', (byte)'\n');
            if (newlineOffset >= 0)
            {
                trailingSearchEndExclusive = gapStart + newlineOffset;
            }
        }

        if (trailingSearchStart > trailingSearchEndExclusive - trailingLiteral.Length)
        {
            return false;
        }

        var searchLength = trailingSearchEndExclusive - trailingSearchStart;
        var relative = input.Slice(trailingSearchStart, searchLength).LastIndexOf(trailingLiteral);
        if (relative < 0)
        {
            return false;
        }

        matchedLength = trailingSearchStart + relative + trailingLiteral.Length - candidateStart;
        return true;
    }
}
