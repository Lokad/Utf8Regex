namespace Lokad.Utf8Regex.Internal.Execution;

internal static class AsciiOrderedLiteralWindowExecutor
{
    public static int FindNext(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        Utf8SearchPlan searchPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        return FindNext(input, plan, searchPlan, default, startIndex, budget, out matchedLength);
    }

    public static int FindNext(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        Utf8SearchPlan searchPlan,
        PreparedAsciiFindPlan trailingFindPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        var trailingLiteral = plan.TrailingLiteralUtf8.AsSpan();

        if (CanUseSeparatorOnlySingleLiteralFastPath(plan))
        {
            return FindNextSeparatorOnlySingleLiteral(input, plan, trailingLiteral, startIndex, budget, out matchedLength);
        }

        if (plan.IsLiteralFamily && searchPlan.AlternateLiteralSearch is { } familySearch)
        {
            return plan.HasPairedTrailingLiterals
                ? FindNextPairedLiteralFamily(input, plan, familySearch, trailingFindPlan, startIndex, budget, out matchedLength)
                : FindNextLiteralFamily(input, plan, familySearch, trailingLiteral, trailingFindPlan, startIndex, budget, out matchedLength);
        }

        var leadingLiteral = plan.LeadingLiteralUtf8.AsSpan();
        var searchFrom = startIndex;
        while (searchFrom <= input.Length - leadingLiteral.Length)
        {
            budget?.Step(input);

            var relative = input[searchFrom..].IndexOf(leadingLiteral);
            if (relative < 0)
            {
                return -1;
            }

            var leadingStart = searchFrom + relative;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingStart + leadingLiteral.Length))
            {
                searchFrom = leadingStart + 1;
                continue;
            }

            if (TryMatchAt(input, plan, trailingLiteral, leadingStart, leadingLiteral.Length, out matchedLength))
            {
                return leadingStart;
            }

            searchFrom = leadingStart + 1;
        }

        return -1;
    }

    public static int Count(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        Utf8SearchPlan searchPlan,
        Utf8ExecutionBudget? budget)
    {
        return Count(input, plan, searchPlan, default, budget);
    }

    public static int Count(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        Utf8SearchPlan searchPlan,
        PreparedAsciiFindPlan trailingFindPlan,
        Utf8ExecutionBudget? budget)
    {
        if (CanUseSeparatorOnlySingleLiteralFastPath(plan))
        {
            return CountSeparatorOnlySingleLiteral(input, plan, budget);
        }

        if (plan.IsLiteralFamily && searchPlan.AlternateLiteralSearch is { } familySearch)
        {
            return plan.HasPairedTrailingLiterals
                ? CountByFindNext(input, plan, searchPlan, trailingFindPlan, budget)
                : CountLiteralFamilyByTrailingAnchor(input, plan, familySearch, trailingFindPlan, budget);
        }

        if (plan.IsLiteralFamily && searchPlan.NativeSearch.HasPreparedSearcher)
        {
            return CountLiteralFamilyStateful(input, plan, searchPlan, trailingFindPlan, budget);
        }

        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, plan, searchPlan, trailingFindPlan, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    internal static bool CanUseSeparatorOnlySingleLiteralFastPath(AsciiOrderedLiteralWindowPlan plan)
    {
        return !plan.IsLiteralFamily &&
            plan.MaxGap == 0 &&
            plan.GapLeadingSeparatorMinCount > 0;
    }

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

    private static int CountSeparatorOnlySingleLiteral(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNextSeparatorOnlySingleLiteral(input, plan, plan.TrailingLiteralUtf8, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int FindNextSeparatorOnlySingleLiteral(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        ReadOnlySpan<byte> trailingLiteral,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        var leadingLiteral = plan.LeadingLiteralUtf8.AsSpan();
        var trailingSearchFrom = Math.Max(startIndex + leadingLiteral.Length + plan.GapLeadingSeparatorMinCount, 0);

        while (trailingSearchFrom <= input.Length - trailingLiteral.Length)
        {
            budget?.Step(input);

            var relative = input[trailingSearchFrom..].IndexOf(trailingLiteral);
            if (relative < 0)
            {
                return -1;
            }

            var trailingStart = trailingSearchFrom + relative;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingStart + trailingLiteral.Length))
            {
                trailingSearchFrom = trailingStart + 1;
                continue;
            }

            var separatorStart = trailingStart;
            while (separatorStart > startIndex &&
                   input[separatorStart - 1] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
            {
                separatorStart--;
            }

            var separatorLength = trailingStart - separatorStart;
            if (separatorLength < plan.GapLeadingSeparatorMinCount)
            {
                trailingSearchFrom = trailingStart + 1;
                continue;
            }

            var leadingStart = separatorStart - leadingLiteral.Length;
            if (leadingStart < startIndex)
            {
                trailingSearchFrom = trailingStart + 1;
                continue;
            }

            if (!input[leadingStart..].StartsWith(leadingLiteral) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingStart + leadingLiteral.Length))
            {
                trailingSearchFrom = trailingStart + 1;
                continue;
            }

            matchedLength = plan.YieldLeadingLiteralOnly
                ? leadingLiteral.Length
                : trailingStart + trailingLiteral.Length - leadingStart;
            return leadingStart;
        }

        return -1;
    }

    private static int CountLiteralFamilyByTrailingAnchor(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch,
        PreparedAsciiFindPlan trailingFindPlan,
        Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var minTrailingIndex = 0;
        var maxLeadingLength = GetMaxLeadingLiteralLength(plan);
        var minLeadingLength = GetMinLeadingLiteralLength(plan);

        while (true)
        {
            budget?.Step(input);
            if (!TryFindNextTrailingBoundaryMatch(input, plan, trailingFindPlan, minTrailingIndex, out var trailingIndex))
            {
                return count;
            }

            if (!TryFindLeadingMatchBeforeTrailing(
                    input,
                    plan,
                    familySearch,
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

    private static int CountLiteralFamilyStateful(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        Utf8SearchPlan searchPlan,
        PreparedAsciiFindPlan trailingFindPlan,
        Utf8ExecutionBudget? budget)
    {
        var trailingLiteral = plan.TrailingLiteralUtf8.AsSpan();
        var count = 0;
        var minStartIndex = 0;
        var trailingCandidateIndex = -1;
        var trailingSearchFrom = 0;

        if (plan.GapLeadingSeparatorMinCount == 0)
        {
            var state = new PreparedMultiLiteralScanState(0, 0, 0);
            while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var leadingStart, out var leadingMatchLength))
            {
                if (leadingStart < minStartIndex)
                {
                    continue;
                }

                budget?.Step(input);
                if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingStart) ||
                    !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingStart + leadingMatchLength))
                {
                    continue;
                }

                if (!TryMatchAtWithTrailingCursor(
                        input,
                        plan,
                        trailingLiteral,
                        trailingFindPlan,
                        leadingStart,
                        leadingMatchLength,
                        ref trailingCandidateIndex,
                        ref trailingSearchFrom,
                        out var matchedLength))
                {
                    continue;
                }

                count++;
                minStartIndex = leadingStart + Math.Max(matchedLength, 1);
            }

            return count;
        }

        var overlappingState = new PreparedSearchScanState(0, default);
        while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextOverlappingMatch(input, ref overlappingState, out var match))
        {
            if (match.Index < minStartIndex)
            {
                continue;
            }

            budget?.Step(input);
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, match.Index) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, match.Index + match.Length))
            {
                continue;
            }

            if (!TryMatchAtWithTrailingCursor(
                    input,
                    plan,
                    trailingLiteral,
                    trailingFindPlan,
                    match.Index,
                    match.Length,
                    ref trailingCandidateIndex,
                    ref trailingSearchFrom,
                    out var matchedLength))
            {
                continue;
            }

            count++;
            minStartIndex = match.Index + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int CountByFindNext(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        Utf8SearchPlan searchPlan,
        PreparedAsciiFindPlan trailingFindPlan,
        Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, plan, searchPlan, trailingFindPlan, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                return count;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
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
        Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNextPairedLiteralFamily(input, plan, familySearch, default, startIndex, budget, out var matchedLength);
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
        return CountPairedLiteralFamilyByTrailingAnchor(input, plan, familySearch, default, budget: null);
    }

    private static bool TryMatchAtWithTrailingCursor(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        ReadOnlySpan<byte> trailingLiteral,
        PreparedAsciiFindPlan trailingFindPlan,
        int leadingStart,
        int leadingMatchLength,
        ref int trailingCandidateIndex,
        ref int trailingSearchFrom,
        out int matchedLength)
    {
        matchedLength = 0;
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
                return false;
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

        var latestTrailingStart = gapSearchEnd - trailingLiteral.Length;
        if (latestTrailingStart < gapSearchStart)
        {
            return false;
        }

        if (trailingCandidateIndex < gapSearchStart)
        {
            trailingCandidateIndex = -1;
            trailingSearchFrom = Math.Max(trailingSearchFrom, gapSearchStart);
        }

        while (true)
        {
            if (trailingCandidateIndex < gapSearchStart)
            {
                if (!TryFindNextTrailingLiteral(input, trailingLiteral, trailingFindPlan, ref trailingSearchFrom, out trailingCandidateIndex))
                {
                    return false;
                }
            }

            if (trailingCandidateIndex > latestTrailingStart)
            {
                return false;
            }

            if (AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingCandidateIndex) &&
                AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingCandidateIndex + trailingLiteral.Length))
            {
                matchedLength = plan.YieldLeadingLiteralOnly
                    ? leadingMatchLength
                    : trailingCandidateIndex + trailingLiteral.Length - leadingStart;
                return true;
            }

            trailingSearchFrom = trailingCandidateIndex + 1;
            trailingCandidateIndex = -1;
        }
    }

    private static bool TryFindNextTrailingLiteral(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> trailingLiteral,
        PreparedAsciiFindPlan trailingFindPlan,
        ref int searchFrom,
        out int trailingCandidateIndex)
    {
        trailingCandidateIndex = -1;
        if (searchFrom > input.Length - trailingLiteral.Length)
        {
            return false;
        }

        if (trailingFindPlan.HasValue &&
            Utf8AsciiFindExecutor.TryFindNextAnchor(input, trailingFindPlan, searchFrom, out trailingCandidateIndex, out _))
        {
            return true;
        }

        var relative = input[searchFrom..].IndexOf(trailingLiteral);
        if (relative < 0)
        {
            searchFrom = input.Length;
            return false;
        }

        trailingCandidateIndex = searchFrom + relative;
        return true;
    }

    private static bool TryFindNextTrailingBoundaryMatch(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedAsciiFindPlan trailingFindPlan,
        int minTrailingIndex,
        out int trailingIndex)
    {
        trailingIndex = -1;
        var searchFrom = minTrailingIndex;
        while (searchFrom <= input.Length)
        {
            int matchedLength;
            if (trailingFindPlan.HasValue)
            {
                if (!Utf8AsciiFindExecutor.TryFindNextAnchor(input, trailingFindPlan, searchFrom, out trailingIndex, out matchedLength))
                {
                    return false;
                }
            }
            else
            {
                var trailingSearch = new PreparedSubstringSearch(plan.TrailingLiteralUtf8, ignoreCase: false);
                var relative = trailingSearch.IndexOf(input[searchFrom..]);
                if (relative < 0)
                {
                    return false;
                }

                trailingIndex = searchFrom + relative;
                matchedLength = trailingSearch.Length;
            }

            searchFrom = trailingIndex + 1;

            if (AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingIndex) &&
                AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingIndex + matchedLength))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindLeadingMatchBeforeTrailing(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch,
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
            var relative = familySearch.LastIndexOf(input.Slice(earliestStart, searchLength));
            if (relative < 0)
            {
                return false;
            }

            leadingIndex = earliestStart + relative;
            if (!familySearch.TryGetMatchedLiteralLength(input, leadingIndex, out var leadingMatchLength))
            {
                searchLength = relative;
                continue;
            }

            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingIndex) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingIndex + leadingMatchLength))
            {
                searchLength = relative;
                continue;
            }

            if (TryMatchLeadingAgainstTrailing(input, plan, leadingIndex, leadingMatchLength, trailingIndex, out matchedLength))
            {
                return true;
            }

            searchLength = relative;
        }

        return false;
    }

    private static bool TryMatchLeadingAgainstTrailing(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        int leadingIndex,
        int leadingMatchLength,
        int trailingIndex,
        out int matchedLength)
    {
        matchedLength = 0;
        var gapSearchStart = leadingIndex + leadingMatchLength;
        var separatorCount = 0;
        while (gapSearchStart + separatorCount < input.Length &&
               input[gapSearchStart + separatorCount] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or (byte)'\f' or (byte)'\v')
        {
            separatorCount++;
        }

        if (separatorCount < plan.GapLeadingSeparatorMinCount)
        {
            return false;
        }

        gapSearchStart += separatorCount;
        if (trailingIndex < gapSearchStart || trailingIndex - gapSearchStart > plan.MaxGap)
        {
            return false;
        }

        if (plan.GapSameLine && input[gapSearchStart..trailingIndex].IndexOfAny((byte)'\r', (byte)'\n') >= 0)
        {
            return false;
        }

        matchedLength = plan.YieldLeadingLiteralOnly
            ? leadingMatchLength
            : trailingIndex + plan.TrailingLiteralUtf8.Length - leadingIndex;
        return true;
    }

    private static int GetMaxLeadingLiteralLength(AsciiOrderedLiteralWindowPlan plan)
    {
        if (!plan.IsLiteralFamily)
        {
            return plan.LeadingLiteralUtf8.Length;
        }

        var max = 0;
        foreach (var literal in plan.LeadingLiteralsUtf8!)
        {
            if (literal.Length > max)
            {
                max = literal.Length;
            }
        }

        return max;
    }

    private static int GetMinLeadingLiteralLength(AsciiOrderedLiteralWindowPlan plan)
    {
        if (!plan.IsLiteralFamily)
        {
            return plan.LeadingLiteralUtf8.Length;
        }

        var min = int.MaxValue;
        foreach (var literal in plan.LeadingLiteralsUtf8!)
        {
            if (literal.Length < min)
            {
                min = literal.Length;
            }
        }

        return min;
    }

    private static int FindNextLiteralFamily(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch,
        ReadOnlySpan<byte> trailingLiteral,
        PreparedAsciiFindPlan trailingFindPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        var searchFrom = startIndex;
        while (searchFrom < input.Length)
        {
            budget?.Step(input);

            if (!familySearch.TryFindFirstMatchWithLength(input[searchFrom..], out var relativeIndex, out var leadingMatchLength))
            {
                return -1;
            }

            var leadingStart = searchFrom + relativeIndex;
            searchFrom = leadingStart + 1;

            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingStart + leadingMatchLength))
            {
                continue;
            }

            if (TryMatchAt(input, plan, trailingLiteral, leadingStart, leadingMatchLength, out matchedLength))
            {
                return leadingStart;
            }
        }

        return -1;
    }

    private static int FindNextPairedLiteralFamily(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        PreparedLiteralSetSearch familySearch,
        PreparedAsciiFindPlan trailingFindPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        var searchFrom = startIndex;
        while (searchFrom < input.Length)
        {
            budget?.Step(input);

            if (!familySearch.TryFindFirstMatchWithLength(input[searchFrom..], out var relativeIndex, out var leadingMatchLength))
            {
                return -1;
            }

            var leadingStart = searchFrom + relativeIndex;
            searchFrom = leadingStart + 1;
            if (!TryResolvePairedLeadingBranch(plan, input, leadingStart, leadingMatchLength, out var branchIndex, out var trailingLiteral))
            {
                continue;
            }

            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralLeadingBoundary, input, leadingStart) ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.LeadingLiteralTrailingBoundary, input, leadingStart + leadingMatchLength))
            {
                continue;
            }

            if (TryMatchAt(input, plan, trailingLiteral, leadingStart, leadingMatchLength, out matchedLength))
            {
                return leadingStart;
            }
        }

        return -1;
    }

    private static bool TryResolvePairedLeadingBranch(
        AsciiOrderedLiteralWindowPlan plan,
        ReadOnlySpan<byte> input,
        int leadingStart,
        int leadingMatchLength,
        out int branchIndex,
        out ReadOnlySpan<byte> trailingLiteral)
    {
        branchIndex = -1;
        trailingLiteral = default;
        if (!plan.HasPairedTrailingLiterals)
        {
            return false;
        }

        var leadingLiterals = plan.LeadingLiteralsUtf8!;
        var trailingLiterals = plan.TrailingLiteralsUtf8!;
        for (var i = 0; i < leadingLiterals.Length; i++)
        {
            var leadingLiteral = leadingLiterals[i];
            if (leadingLiteral.Length != leadingMatchLength)
            {
                continue;
            }

            if (input[leadingStart..].StartsWith(leadingLiteral))
            {
                branchIndex = i;
                trailingLiteral = trailingLiterals[i];
                return true;
            }
        }

        return false;
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

        var leadingLiterals = plan.LeadingLiteralsUtf8!;
        var trailingLiterals = plan.TrailingLiteralsUtf8!;
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
        Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var minTrailingIndex = 0;
        var maxLeadingLength = GetMaxLeadingLiteralLength(plan);
        var minLeadingLength = GetMinLeadingLiteralLength(plan);
        var trailingFamilySearch = CanReuseFamilySearchForTrailing(plan)
            ? familySearch
            : new PreparedLiteralSetSearch(plan.TrailingLiteralsUtf8!);

        while (true)
        {
            budget?.Step(input);
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

        var leadingLiterals = plan.LeadingLiteralsUtf8!;
        var trailingLiterals = plan.TrailingLiteralsUtf8!;
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

    private static bool TryMatchAt(
        ReadOnlySpan<byte> input,
        AsciiOrderedLiteralWindowPlan plan,
        ReadOnlySpan<byte> trailingLiteral,
        int leadingStart,
        int leadingMatchLength,
        out int matchedLength)
    {
        matchedLength = 0;
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
                return false;
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

        var searchLength = gapSearchEnd - gapSearchStart;
        while (searchLength >= trailingLiteral.Length)
        {
            var trailingRelative = input.Slice(gapSearchStart, searchLength).LastIndexOf(trailingLiteral);
            if (trailingRelative < 0)
            {
                return false;
            }

            var trailingStart = gapSearchStart + trailingRelative;
            if (AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralLeadingBoundary, input, trailingStart) &&
                AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(plan.TrailingLiteralTrailingBoundary, input, trailingStart + trailingLiteral.Length))
            {
                matchedLength = plan.YieldLeadingLiteralOnly
                    ? leadingMatchLength
                    : trailingStart + trailingLiteral.Length - leadingStart;
                return true;
            }

            searchLength = trailingRelative;
        }

        return false;
    }
}
