namespace Lokad.Utf8Regex.Internal.Execution;

using Lokad.Utf8Regex.Internal.Utilities;

internal static class Utf8AsciiSymmetricLiteralWindowExecutor
{
    public static int CountAnchorCandidates(ReadOnlySpan<byte> input, AsciiSimplePatternSymmetricLiteralWindowPlan plan)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindNextAnchorCandidate(input, plan, startIndex, out var candidateStart))
        {
            count++;
            startIndex = candidateStart + 1;
        }

        return count;
    }

    public static int CountFilterQualifiedCandidates(ReadOnlySpan<byte> input, AsciiSimplePatternSymmetricLiteralWindowPlan plan)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindNextAnchorCandidate(input, plan, startIndex, out var candidateStart))
        {
            if (PassesCandidateFilters(input, candidateStart, plan))
            {
                count++;
            }

            startIndex = candidateStart + 1;
        }

        return count;
    }

    public static int CountLeadingLiteralMatches(ReadOnlySpan<byte> input, AsciiSimplePatternSymmetricLiteralWindowPlan plan)
    {
        var count = 0;
        var startIndex = 0;
        var firstLiteral = plan.FirstLiteralUtf8.AsSpan();
        var secondLiteral = plan.SecondLiteralUtf8.AsSpan();
        while (TryFindNextAnchorCandidate(input, plan, startIndex, out var candidateStart))
        {
            if (PassesCandidateFilters(input, candidateStart, plan) &&
                TryMatchLeadingLiteral(input, candidateStart, plan, firstLiteral, secondLiteral, out _, out _))
            {
                count++;
            }

            startIndex = candidateStart + 1;
        }

        return count;
    }

    public static bool IsMatch(ReadOnlySpan<byte> input, AsciiSimplePatternSymmetricLiteralWindowPlan plan, Utf8ExecutionBudget? budget)
    {
        return FindNext(input, plan, 0, budget, out _) >= 0;
    }

    public static int Count(ReadOnlySpan<byte> input, AsciiSimplePatternSymmetricLiteralWindowPlan plan, Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, plan, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    public static Utf8ValueMatch Match(ReadOnlySpan<byte> input, AsciiSimplePatternSymmetricLiteralWindowPlan plan, Utf8ExecutionBudget? budget)
    {
        var matchIndex = FindNext(input, plan, 0, budget, out var matchedLength);
        return matchIndex < 0
            ? Utf8ValueMatch.NoMatch
            : new Utf8ValueMatch(true, true, matchIndex, matchedLength, matchIndex, matchedLength);
    }

    private static int FindNext(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternSymmetricLiteralWindowPlan plan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        if (startIndex < 0 || startIndex >= input.Length)
        {
            return -1;
        }

        var firstLiteral = plan.FirstLiteralUtf8.AsSpan();
        var secondLiteral = plan.SecondLiteralUtf8.AsSpan();
        var searchFrom = startIndex;
        while (searchFrom <= input.Length - plan.SearchData.ShortestLength)
        {
            budget?.Step(input);
            if (!TryFindNextAnchorCandidate(input, plan, searchFrom, out var candidateStart))
            {
                return -1;
            }

            if (!PassesCandidateFilters(input, candidateStart, plan) ||
                !TryMatchLeadingLiteral(input, candidateStart, plan, firstLiteral, secondLiteral, out var leadingLiteral, out var trailingLiteral))
            {
                searchFrom = candidateStart + 1;
                continue;
            }

            if (TryMatchTrailingLiteral(input, plan, candidateStart, leadingLiteral, trailingLiteral, out matchedLength))
            {
                return candidateStart;
            }

            searchFrom = candidateStart + 1;
        }

        return -1;
    }

    private static bool TryFindNextAnchorCandidate(ReadOnlySpan<byte> input, AsciiSimplePatternSymmetricLiteralWindowPlan plan, int searchFrom, out int candidateStart)
    {
        candidateStart = -1;
        while (true)
        {
            var anchorSearchStart = searchFrom + plan.AnchorOffset;
            if (anchorSearchStart > input.Length - 1)
            {
                return false;
            }

            var relative = input[anchorSearchStart..].IndexOfAny(plan.AnchorByteA, plan.AnchorByteB);
            if (relative < 0)
            {
                return false;
            }

            candidateStart = anchorSearchStart + relative - plan.AnchorOffset;
            if (candidateStart >= searchFrom)
            {
                return true;
            }

            searchFrom = anchorSearchStart + relative + 1 - plan.AnchorOffset;
        }
    }

    private static bool TryMatchLeadingLiteral(
        ReadOnlySpan<byte> input,
        int candidateStart,
        AsciiSimplePatternSymmetricLiteralWindowPlan plan,
        ReadOnlySpan<byte> firstLiteral,
        ReadOnlySpan<byte> secondLiteral,
        out ReadOnlySpan<byte> leadingLiteral,
        out ReadOnlySpan<byte> trailingLiteral)
    {
        leadingLiteral = default;
        trailingLiteral = default;

        var anchorIndex = candidateStart + plan.AnchorOffset;
        if ((uint)anchorIndex >= (uint)input.Length)
        {
            return false;
        }

        var anchor = input[anchorIndex];
        if (anchor == plan.AnchorByteA)
        {
            if ((uint)(candidateStart + firstLiteral.Length) > (uint)input.Length ||
                !input.Slice(candidateStart, firstLiteral.Length).SequenceEqual(firstLiteral))
            {
                return false;
            }

            leadingLiteral = firstLiteral;
            trailingLiteral = secondLiteral;
            return true;
        }

        if (anchor == plan.AnchorByteB)
        {
            if ((uint)(candidateStart + secondLiteral.Length) > (uint)input.Length ||
                !input.Slice(candidateStart, secondLiteral.Length).SequenceEqual(secondLiteral))
            {
                return false;
            }

            leadingLiteral = secondLiteral;
            trailingLiteral = firstLiteral;
            return true;
        }

        return false;
    }

    private static bool TryMatchTrailingLiteral(
        ReadOnlySpan<byte> input,
        AsciiSimplePatternSymmetricLiteralWindowPlan plan,
        int leadingStart,
        ReadOnlySpan<byte> leadingLiteral,
        ReadOnlySpan<byte> trailingLiteral,
        out int matchedLength)
    {
        matchedLength = 0;
        var trailingSearchStart = leadingStart + leadingLiteral.Length + plan.MinGap;
        if (trailingSearchStart > input.Length - trailingLiteral.Length)
        {
            return false;
        }

        var trailingSearchEndExclusive = Math.Min(
            input.Length,
            leadingStart + leadingLiteral.Length + plan.MaxGap + trailingLiteral.Length);
        if (plan.GapSameLine)
        {
            var gapStart = leadingStart + leadingLiteral.Length;
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

        var trailingStart = trailingSearchStart + relative;
        matchedLength = trailingStart + trailingLiteral.Length - leadingStart;
        return true;
    }

    private static bool PassesCandidateFilters(ReadOnlySpan<byte> input, int candidateStart, AsciiSimplePatternSymmetricLiteralWindowPlan plan)
    {
        return PassesCandidateFilter(input, candidateStart, plan.FirstFilterOffset, plan.FirstFilterByteA, plan.FirstFilterByteB) &&
            PassesCandidateFilter(input, candidateStart, plan.SecondFilterOffset, plan.SecondFilterByteA, plan.SecondFilterByteB);
    }

    private static bool PassesCandidateFilter(ReadOnlySpan<byte> input, int candidateStart, int filterOffset, byte firstAllowed, byte secondAllowed)
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
}
