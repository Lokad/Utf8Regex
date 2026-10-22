using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using static Lokad.Utf8Regex.Internal.Execution.AsciiOrderedLiteralWindowExecutor;

namespace Lokad.Utf8Regex.Benchmarks;

// Diagnostic replay only. Shared candidate discovery/verification remains in production.
internal static class Utf8OrderedWindowCountReplay
{
    internal static int CountSeparatorOnlySingleLiteralTrailingCandidates(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan)
    {
        var count = 0;
        var trailingLiteral = plan.TrailingLiteralUtf8.AsSpan();
        var trailingSearchFrom = plan.LeadingLiteralUtf8.Length + plan.GapLeadingSeparatorMinCount;

        while (trailingSearchFrom <= input.Length - trailingLiteral.Length)
        {
            var relative = input[trailingSearchFrom..].IndexOf(trailingLiteral);
            if (relative < 0)
            {
                return count;
            }

            var trailingStart = trailingSearchFrom + relative;
            trailingSearchFrom = trailingStart + 1;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingStart + trailingLiteral.Length))
            {
                continue;
            }

            count++;
        }

        return count;
    }
    internal static int CountSingleLiteralTrailingBoundaryCandidates(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan)
    {
        var count = 0;
        var trailingLiteral = plan.TrailingLiteralUtf8.AsSpan();
        var trailingSearchFrom = Math.Max(plan.LeadingLiteralUtf8.Length + plan.GapLeadingSeparatorMinCount, 0);

        while (trailingSearchFrom <= input.Length - trailingLiteral.Length)
        {
            var relative = input[trailingSearchFrom..].IndexOf(trailingLiteral);
            if (relative < 0)
            {
                return count;
            }

            var trailingStart = trailingSearchFrom + relative;
            trailingSearchFrom = trailingStart + 1;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingStart + trailingLiteral.Length))
            {
                continue;
            }

            count++;
        }

        return count;
    }
    internal static int CountSingleLiteralGapQualifiedCandidates(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan)
    {
        var count = 0;
        var trailingLiteral = plan.TrailingLiteralUtf8.AsSpan();
        var leadingLength = plan.LeadingLiteralUtf8.Length;
        var trailingSearchFrom = Math.Max(leadingLength + plan.GapLeadingSeparatorMinCount, 0);

        while (trailingSearchFrom <= input.Length - trailingLiteral.Length)
        {
            var relative = input[trailingSearchFrom..].IndexOf(trailingLiteral);
            if (relative < 0)
            {
                return count;
            }

            var trailingStart = trailingSearchFrom + relative;
            trailingSearchFrom = trailingStart + 1;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingStart + trailingLiteral.Length))
            {
                continue;
            }

            var earliestLeadingStart = Math.Max(0, trailingStart - (plan.MaxGap + plan.GapLeadingSeparatorMinCount + leadingLength));
            var latestLeadingStart = trailingStart - (plan.GapLeadingSeparatorMinCount + leadingLength);
            if (latestLeadingStart < earliestLeadingStart)
            {
                continue;
            }

            var searchLength = latestLeadingStart - earliestLeadingStart + 1;
            while (searchLength > 0)
            {
                var relativeLeading = input.Slice(earliestLeadingStart, searchLength).LastIndexOf(plan.LeadingLiteralUtf8);
                if (relativeLeading < 0)
                {
                    break;
                }

                var leadingStart = earliestLeadingStart + relativeLeading;
                var gapSearchStart = leadingStart + leadingLength;
                var separatorCount = 0;
                while (gapSearchStart + separatorCount < input.Length &&
                       input[gapSearchStart + separatorCount] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
                {
                    separatorCount++;
                }

                if (separatorCount >= plan.GapLeadingSeparatorMinCount)
                {
                    gapSearchStart += separatorCount;
                    if (trailingStart >= gapSearchStart && trailingStart - gapSearchStart <= plan.MaxGap)
                    {
                        if (!plan.GapSameLine || input[gapSearchStart..trailingStart].IndexOfAny((byte)'\r', (byte)'\n') < 0)
                        {
                            count++;
                            break;
                        }
                    }
                }

                searchLength = relativeLeading;
            }
        }

        return count;
    }
    internal static int CountSingleLiteralLeadingQualifiedMatches(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan)
    {
        var count = 0;
        var trailingLiteral = plan.TrailingLiteralUtf8.AsSpan();
        var leadingLiteral = plan.LeadingLiteralUtf8.AsSpan();
        var trailingSearchFrom = Math.Max(leadingLiteral.Length + plan.GapLeadingSeparatorMinCount, 0);

        while (trailingSearchFrom <= input.Length - trailingLiteral.Length)
        {
            var relative = input[trailingSearchFrom..].IndexOf(trailingLiteral);
            if (relative < 0)
            {
                return count;
            }

            var trailingStart = trailingSearchFrom + relative;
            trailingSearchFrom = trailingStart + 1;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingStart + trailingLiteral.Length))
            {
                continue;
            }

            var earliestLeadingStart = Math.Max(0, trailingStart - (plan.MaxGap + plan.GapLeadingSeparatorMinCount + leadingLiteral.Length));
            var latestLeadingStart = trailingStart - (plan.GapLeadingSeparatorMinCount + leadingLiteral.Length);
            if (latestLeadingStart < earliestLeadingStart)
            {
                continue;
            }

            var searchLength = latestLeadingStart - earliestLeadingStart + 1;
            while (searchLength > 0)
            {
                var relativeLeading = input.Slice(earliestLeadingStart, searchLength).LastIndexOf(leadingLiteral);
                if (relativeLeading < 0)
                {
                    break;
                }

                var leadingStart = earliestLeadingStart + relativeLeading;
                if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingStart) ||
                    !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingStart + leadingLiteral.Length))
                {
                    searchLength = relativeLeading;
                    continue;
                }

                if (TryMatchLeadingAgainstTrailing(input, plan, leadingStart, leadingLiteral.Length, trailingStart, out _))
                {
                    count++;
                    break;
                }

                searchLength = relativeLeading;
            }
        }

        return count;
    }
    internal static int CountSeparatorOnlySingleLiteralSeparatorQualifiedCandidates(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan)
    {
        var count = 0;
        var trailingLiteral = plan.TrailingLiteralUtf8.AsSpan();
        var trailingSearchFrom = plan.LeadingLiteralUtf8.Length + plan.GapLeadingSeparatorMinCount;

        while (trailingSearchFrom <= input.Length - trailingLiteral.Length)
        {
            var relative = input[trailingSearchFrom..].IndexOf(trailingLiteral);
            if (relative < 0)
            {
                return count;
            }

            var trailingStart = trailingSearchFrom + relative;
            trailingSearchFrom = trailingStart + 1;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingStart + trailingLiteral.Length))
            {
                continue;
            }

            var separatorStart = trailingStart;
            while (separatorStart > 0 &&
                   input[separatorStart - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
            {
                separatorStart--;
            }

            var separatorLength = trailingStart - separatorStart;
            if (separatorLength < plan.GapLeadingSeparatorMinCount)
            {
                continue;
            }

            count++;
        }

        return count;
    }
    internal static int CountSeparatorOnlySingleLiteralLeadingMatches(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan)
    {
        var count = 0;
        var trailingLiteral = plan.TrailingLiteralUtf8.AsSpan();
        var leadingLiteral = plan.LeadingLiteralUtf8.AsSpan();
        var trailingSearchFrom = leadingLiteral.Length + plan.GapLeadingSeparatorMinCount;

        while (trailingSearchFrom <= input.Length - trailingLiteral.Length)
        {
            var relative = input[trailingSearchFrom..].IndexOf(trailingLiteral);
            if (relative < 0)
            {
                return count;
            }

            var trailingStart = trailingSearchFrom + relative;
            trailingSearchFrom = trailingStart + 1;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingStart + trailingLiteral.Length))
            {
                continue;
            }

            var separatorStart = trailingStart;
            while (separatorStart > 0 &&
                   input[separatorStart - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
            {
                separatorStart--;
            }

            var separatorLength = trailingStart - separatorStart;
            if (separatorLength < plan.GapLeadingSeparatorMinCount)
            {
                continue;
            }

            var leadingStart = separatorStart - leadingLiteral.Length;
            if (leadingStart < 0 ||
                !input[leadingStart..].StartsWith(leadingLiteral) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingStart + leadingLiteral.Length))
            {
                continue;
            }

            count++;
        }

        return count;
    }
    internal static int CountPairedLiteralFamilyLeadingCandidates(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch)
    {
        var count = 0;
        var searchFrom = 0;
        while (searchFrom < input.Length)
        {
            if (!familySearch.TryFindFirstMatchWithLength(input[searchFrom..], out var relativeIndex, out var leadingMatchLength))
            {
                return count;
            }

            var leadingStart = searchFrom + relativeIndex;
            searchFrom = leadingStart + 1;
            if (!TryResolvePairedLeadingBranch(plan, input, leadingStart, leadingMatchLength, out _, out _))
            {
                continue;
            }

            if (AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingStart) &&
                AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingStart + leadingMatchLength))
            {
                count++;
            }
        }

        return count;
    }
    internal static int CountPairedLiteralFamilyGapQualifiedCandidates(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch)
    {
        var count = 0;
        var searchFrom = 0;
        while (searchFrom < input.Length)
        {
            if (!familySearch.TryFindFirstMatchWithLength(input[searchFrom..], out var relativeIndex, out var leadingMatchLength))
            {
                return count;
            }

            var leadingStart = searchFrom + relativeIndex;
            searchFrom = leadingStart + 1;
            if (!TryResolvePairedLeadingBranch(plan, input, leadingStart, leadingMatchLength, out _, out var trailingLiteral) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingStart + leadingMatchLength))
            {
                continue;
            }

            var gapSearchStart = leadingStart + leadingMatchLength;
            if (plan.GapLeadingSeparatorMinCount > 0)
            {
                var sepCount = 0;
                while (gapSearchStart + sepCount < input.Length &&
                       input[gapSearchStart + sepCount] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
                {
                    sepCount++;
                }

                if (sepCount < plan.GapLeadingSeparatorMinCount)
                {
                    continue;
                }

                gapSearchStart += sepCount;
            }

            var gapSearchEnd = Math.Min(input.Length, gapSearchStart + plan.MaxGap + trailingLiteral.Length);
            if (plan.GapSameLine)
            {
                var newlineOffset = input[gapSearchStart..gapSearchEnd].IndexOf((byte)'\n');
                if (newlineOffset >= 0)
                {
                    gapSearchEnd = gapSearchStart + newlineOffset;
                }
            }

            if (input[gapSearchStart..gapSearchEnd].IndexOf(trailingLiteral) >= 0)
            {
                count++;
            }
        }

        return count;
    }
    internal static int CountPairedLiteralFamily(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch,
        Utf8ExecutionDeadline budget)
    {
        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNextPairedLiteralFamily(input, plan, familySearch, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }
    internal static int CountPairedLiteralFamilyTrailingAnchorMatches(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch)
    {
        return CountPairedLiteralFamilyByTrailingAnchor(input, plan, familySearch, default, budget: Utf8ExecutionDeadline.Infinite);
    }
    private static bool TryResolvePairedTrailingBranch(
        AsciiOrderedLiteralWindowPlan plan,
        ReadOnlySpan<byte> input,
        int trailingStart,
        int trailingMatchLength,
        out ReadOnlySpan<byte> leadingLiteral)
    {
        leadingLiteral = default;
        if (!plan.HasPairedTrailingLiterals)
        {
            return false;
        }

        var leadingLiterals = plan.LeadingLiteralsUtf8;
        var trailingLiterals = plan.TrailingLiteralsUtf8;
        for (var i = 0; i < trailingLiterals.Length; i++)
        {
            var trailingLiteral = trailingLiterals[i];
            if (trailingLiteral.Length != trailingMatchLength)
            {
                continue;
            }

            if (input[trailingStart..].StartsWith(trailingLiteral))
            {
                leadingLiteral = leadingLiterals[i];
                return true;
            }
        }

        return false;
    }
    private static bool TryFindNextTrailingFamilyBoundaryMatch(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch trailingFamilySearch,
        PreparedAsciiFindPlan trailingFindPlan,
        int minTrailingIndex,
        out int trailingIndex,
        out int trailingMatchLength)
    {
        trailingIndex = -1;
        trailingMatchLength = 0;
        var searchFrom = minTrailingIndex;
        while (searchFrom < input.Length)
        {
            if (trailingFindPlan.HasValue)
            {
                if (!Utf8AsciiFindExecutor.TryFindNextAnchor(input, trailingFindPlan, searchFrom, out trailingIndex, out trailingMatchLength))
                {
                    return false;
                }

                searchFrom = trailingIndex + 1;
            }
            else if (!trailingFamilySearch.TryFindFirstMatchWithLength(input[searchFrom..], out var relativeIndex, out var matchLength))
            {
                return false;
            }
            else
            {
                trailingIndex = searchFrom + relativeIndex;
                trailingMatchLength = matchLength;
                searchFrom = trailingIndex + 1;
            }

            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingIndex) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingIndex + trailingMatchLength))
            {
                continue;
            }

            return true;
        }

        return false;
    }
    private static int CountPairedLiteralFamilyByTrailingAnchor(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch,
        PreparedAsciiFindPlan trailingFindPlan,
        Utf8ExecutionDeadline budget)
    {
        var count = 0;
        var minTrailingIndex = 0;
        var maxLeadingLength = GetMaxLeadingLiteralLength(plan);
        var minLeadingLength = GetMinLeadingLiteralLength(plan);
        var trailingFamilySearch = CanReuseFamilySearchForTrailing(plan)
            ? familySearch
            : new PreparedLiteralSetSearch(plan.TrailingLiteralsUtf8);

        while (true)
        {
            budget.Step();
            if (!TryFindNextTrailingFamilyBoundaryMatch(
                    input,
                    plan,
                    trailingFamilySearch,
                    trailingFindPlan,
                    minTrailingIndex,
                    out var trailingIndex,
                    out var trailingMatchLength))
            {
                return count;
            }

            if (!TryResolvePairedTrailingBranch(plan, input, trailingIndex, trailingMatchLength, out var leadingLiteral) ||
                !TryFindSpecificLeadingMatchBeforeTrailing(
                    input,
                    plan,
                    leadingLiteral,
                    trailingIndex,
                    maxLeadingLength,
                    minLeadingLength,
                    out var leadingIndex,
                    out var matchedLength))
            {
                minTrailingIndex = trailingIndex + 1;
                continue;
            }

            count++;
            minTrailingIndex = leadingIndex + Math.Max(matchedLength, 1);
        }
    }
    private static bool TryFindSpecificLeadingMatchBeforeTrailing(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        ReadOnlySpan<byte> leadingLiteral,
        int trailingIndex,
        int maxLeadingLength,
        int minLeadingLength,
        out int leadingIndex,
        out int matchedLength)
    {
        leadingIndex = -1;
        matchedLength = 0;

        var earliestStart = Math.Max(0, trailingIndex - (plan.MaxGap + plan.GapLeadingSeparatorMinCount + maxLeadingLength));
        var latestStart = trailingIndex - (plan.GapLeadingSeparatorMinCount + minLeadingLength);
        if (latestStart < earliestStart)
        {
            return false;
        }

        var searchLength = latestStart - earliestStart + 1;
        while (searchLength > 0)
        {
            var relative = AsciiSearch.LastIndexOfExact(input.Slice(earliestStart, searchLength), leadingLiteral);
            if (relative < 0)
            {
                return false;
            }

            leadingIndex = earliestStart + relative;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingIndex) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingIndex + leadingLiteral.Length))
            {
                searchLength = relative;
                continue;
            }

            if (TryMatchLeadingAgainstTrailing(input, plan, leadingIndex, leadingLiteral.Length, trailingIndex, out matchedLength))
            {
                return true;
            }

            searchLength = relative;
        }

        return false;
    }
    private static bool CanReuseFamilySearchForTrailing(AsciiOrderedLiteralWindowPlan plan)
    {
        if (!plan.HasPairedTrailingLiterals)
        {
            return false;
        }

        var leadingLiterals = plan.LeadingLiteralsUtf8;
        var trailingLiterals = plan.TrailingLiteralsUtf8;
        if (leadingLiterals.Length != trailingLiterals.Length)
        {
            return false;
        }

        foreach (var trailingLiteral in trailingLiterals)
        {
            var found = false;
            foreach (var leadingLiteral in leadingLiterals)
            {
                if (leadingLiteral.AsSpan().SequenceEqual(trailingLiteral))
                {
                    found = true;
                    break;
                }
            }

            if (!found)
            {
                return false;
            }
        }

        return true;
    }
}
