using System.Buffers;
using Lokad.Utf8Regex.Internal.Utilities;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiStructuralIdentifierFamilyExecutor
{
    internal readonly record struct SharedPrefixSuffixKernelDiagnostics(
        int BucketCount,
        int CommonPrefixLength,
        bool HasPrefixDiscriminator,
        int? PrefixDiscriminatorOffset,
        int SuffixLiteralLength,
        int SeparatorMinCount,
        bool HasAsciiWhitespaceSeparatorClass,
        Utf8BoundaryRequirement LeadingBoundary,
        Utf8BoundaryRequirement TrailingBoundary,
        bool CanUseSharedPrefixSuffixKernelSpec,
        bool CanUseSharedPrefixSuffixLiteralFamilyKernelSpec,
        bool CanUseAsciiWhitespaceSingleByteSuffixKernel);


    public static int FindNext(
        ReadOnlySpan<byte> input,
        AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8StructuralVerifierRuntime verifierRuntime,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        var searchData = searchPlan.AlternateLiteralSearchData;
        if (!searchData.HasValue)
        {
            return -1;
        }

        if (CanUseUpperWordIdentifierKernel(familyPlan))
        {
            return FindNextUpperWordIdentifier(
                input,
                familyPlan,
                searchPlan,
                startIndex,
                budget,
                out matchedLength);
        }

        if (searchPlan.NativeSearch.PreparedSearcher.Kind == PreparedSearcherKind.MultiLiteral &&
            CanUseIdentifierTailOnlyCountKernel(familyPlan))
        {
            return FindNextIdentifierTailOnlyViaPreparedSearcher(
                input,
                familyPlan,
                searchPlan,
                startIndex,
                budget,
                out matchedLength);
        }

        if (CanUseTrailingLiteralIdentifierKernel(familyPlan))
        {
            return FindNextTrailingLiteralIdentifier(
                input,
                familyPlan,
                startIndex,
                budget,
                out matchedLength);
        }

        if (structuralSearchPlan.HasValue && structuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start)
        {
            var state = new Utf8StructuralSearchState(new PreparedSearchScanState(startIndex, default), default);
            while (structuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate))
            {
                budget?.Step(input);
                var prefixLength = candidate.MatchLength;
                if (prefixLength <= 0 &&
                    !AsciiSearch.TryGetMatchedLiteralLength(input, candidate.StartIndex, searchData.Value, out prefixLength))
                {
                    continue;
                }

                Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
                if (TryMatchAt(input, candidate.StartIndex, prefixLength, familyPlan, verifierRuntime, budget, out matchedLength))
                {
                    Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                    return candidate.StartIndex;
                }
            }

            return -1;
        }

        for (var candidate = Utf8SearchExecutor.FindNext(searchPlan, input, startIndex);
            candidate >= 0;
            candidate = Utf8SearchExecutor.FindNext(searchPlan, input, candidate + 1))
        {
            budget?.Step(input);
            if (!AsciiSearch.TryGetMatchedLiteralLength(input, candidate, searchData.Value, out var prefixLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (TryMatchAt(input, candidate, prefixLength, familyPlan, verifierRuntime, budget, out matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                return candidate;
            }
        }

        return -1;
    }

    private static int FindNextIdentifierTailOnlyViaPreparedSearcher(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;

        if (searchPlan.AlternateLiteralSearch is { } literalSearch &&
            budget is null)
        {
            return FindNextIdentifierTailOnlyFused(input, familyPlan, literalSearch, startIndex, out matchedLength);
        }

        if (familyPlan.LeadingBoundary == Utf8BoundaryRequirement.Boundary)
        {
            return FindNextIdentifierTailOnlyNonOverlapping(input, familyPlan, searchPlan, startIndex, budget, out matchedLength);
        }

        return FindNextIdentifierTailOnlyOverlapping(input, familyPlan, searchPlan, startIndex, budget, out matchedLength);
    }

    public static int Count(
        ReadOnlySpan<byte> input,
        AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8StructuralVerifierRuntime verifierRuntime,
        Utf8ExecutionBudget? budget)
    {
        var searchData = searchPlan.AlternateLiteralSearchData;
        if (searchData.HasValue &&
            structuralSearchPlan.HasValue &&
            structuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Start)
        {
            if (CanUseUpperWordIdentifierKernel(familyPlan))
            {
                return CountUpperWordIdentifier(input, familyPlan, searchPlan, budget);
            }

            return CountStateful(input, familyPlan, searchPlan, searchData.Value, structuralSearchPlan, budget);
        }

        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length)
        {
            var matchIndex = FindNext(input, familyPlan, searchPlan, structuralSearchPlan, verifierRuntime, startIndex, budget, out var matchedLength);
            if (matchIndex < 0)
            {
                break;
            }

            count++;
            startIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int CountStateful(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        AsciiExactLiteralSearchData searchData,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8ExecutionBudget? budget)
    {
        if (searchPlan.NativeSearch.PreparedSearcher.Kind == PreparedSearcherKind.MultiLiteral)
        {
            return CountStatefulViaPreparedSearcher(input, familyPlan, searchPlan, budget);
        }

        var count = 0;
        var state = new Utf8StructuralSearchState(new PreparedSearchScanState(0, default), default);
        var minStartIndex = 0;

        while (structuralSearchPlan.TryFindNextCandidate(input, ref state, out var candidate))
        {
            if (candidate.StartIndex < minStartIndex)
            {
                continue;
            }

            budget?.Step(input);
            var prefixLength = candidate.MatchLength;
            if (prefixLength <= 0 &&
                !AsciiSearch.TryGetMatchedLiteralLength(input, candidate.StartIndex, searchData, out prefixLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, candidate.StartIndex, prefixLength, familyPlan, out var matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            minStartIndex = candidate.StartIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int CountStatefulViaPreparedSearcher(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8ExecutionBudget? budget)
    {
        if (CanUseUpperWordIdentifierKernel(familyPlan))
        {
            return CountUpperWordIdentifier(input, familyPlan, searchPlan, budget);
        }

        if (CanUseSuffixOnlyCountKernel(familyPlan))
        {
            return CountSuffixOnlyViaPreparedSearcher(input, familyPlan, searchPlan, budget);
        }

        if (CanUsePreparedSearcherNonOverlappingCountKernel(familyPlan))
        {
            return CountStatefulViaPreparedSearcherNonOverlapping(input, familyPlan, searchPlan, budget);
        }

        if (CanUseIdentifierTailOnlyCountKernel(familyPlan))
        {
            return CountStatefulViaPreparedSearcherIdentifierTailOnly(input, familyPlan, searchPlan, budget);
        }

        var count = 0;
        var state = new PreparedSearchScanState(0, default);
        var minStartIndex = 0;

        while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            if (match.Index < minStartIndex ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.LeadingBoundary, input, match.Index))
            {
                continue;
            }

            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, match.Index, match.Length, familyPlan, out var matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            minStartIndex = match.Index + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int CountStatefulViaPreparedSearcherNonOverlapping(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var minStartIndex = 0;

        while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var matchIndex, out var matchLength))
        {
            if (matchIndex < minStartIndex ||
                !AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.LeadingBoundary, input, matchIndex))
            {
                continue;
            }

            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, matchIndex, matchLength, familyPlan, out var matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            minStartIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int CountStatefulViaPreparedSearcherIdentifierTailOnly(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8ExecutionBudget? budget)
    {
        if (searchPlan.AlternateLiteralSearch is { } literalSearch &&
            budget is null)
        {
            return CountIdentifierTailOnlyFused(input, familyPlan, literalSearch);
        }

        if (familyPlan.LeadingBoundary == Utf8BoundaryRequirement.Boundary)
        {
            return CountStatefulViaPreparedSearcherIdentifierTailOnlyNonOverlapping(input, familyPlan, searchPlan, budget);
        }

        var count = 0;
        var state = new PreparedSearchScanState(0, default);
        var minStartIndex = 0;

        while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            if (match.Index < minStartIndex ||
                !MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, match.Index))
            {
                continue;
            }

            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatchIdentifierTailOnly(input, match.Index, match.Length, familyPlan, out var matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            minStartIndex = match.Index + Math.Max(matchedLength, 1);
        }

        return count;
    }

    internal static bool CanUseSuffixOnlyCountKernel(in AsciiStructuralIdentifierFamilyPlan familyPlan)
    {
        return string.IsNullOrEmpty(familyPlan.IdentifierStartSet) &&
            familyPlan.CompiledSuffixParts.Length > 0;
    }

    internal static int CountSuffixOnlyViaPreparedSearcher(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8ExecutionBudget? budget)
    {
        // Prefer the reverse suffix kernel when it is available; it is usually a better
        // shape for `prefix \s+ literal\b` than repeatedly restarting from each prefix hit.
        if (budget is null)
        {
            if (searchPlan.AlternateLiteralSearchData is { } searchData &&
                TryGetSharedPrefixSuffixOnlyKernel(familyPlan, searchData, out var sharedPrefixBucket, out var sharedPrefixSuffixLiteral, out var sharedPrefixSeparatorMinCount))
            {
                return CountSuffixOnlySharedPrefixLiteral(input, familyPlan, sharedPrefixBucket, sharedPrefixSuffixLiteral, sharedPrefixSeparatorMinCount);
            }

            if (TryGetReverseSuffixLiteralKernel(familyPlan, out var reverseSuffixLiteral, out var reverseSeparatorMinCount))
            {
                return CountSuffixOnlyReverseLiteral(input, familyPlan, reverseSuffixLiteral, reverseSeparatorMinCount);
            }

            if (TryGetReverseSuffixByteKernel(familyPlan, out var reverseSuffixByte, out var reverseByteSeparatorMinCount))
            {
                return CountSuffixOnlyReverseByte(input, familyPlan, reverseSuffixByte, reverseByteSeparatorMinCount);
            }

            if (searchPlan.AlternateLiteralSearch is { } literalSearch)
            {
                if (TryGetSimpleSuffixLiteralKernel(familyPlan, out var suffixLiteral, out var separatorMinCount))
                {
                    return CountSuffixOnlyFusedLiteral(input, familyPlan, literalSearch, suffixLiteral, separatorMinCount);
                }

                return CountSuffixOnlyFused(input, familyPlan, literalSearch);
            }
        }

        if (familyPlan.LeadingBoundary == Utf8BoundaryRequirement.Boundary)
        {
            return CountSuffixOnlyViaPreparedSearcherNonOverlapping(input, familyPlan, searchPlan, budget);
        }

        var count = 0;
        var state = new PreparedSearchScanState(0, default);
        var minStartIndex = 0;

        while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            if (match.Index < minStartIndex ||
                !MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, match.Index))
            {
                continue;
            }

            budget?.Step(input);
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatchSimpleSuffix(
                    input, match.Index, match.Index + match.Length, familyPlan, out var matchedLength))
            {
                continue;
            }

            count++;
            minStartIndex = match.Index + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int CountIdentifierTailOnlyFused(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        PreparedLiteralSetSearch literalSearch)
    {
        var count = 0;
        var offset = 0;
        var minStartIndex = 0;

        while (offset < input.Length)
        {
            if (!literalSearch.TryFindFirstMatchWithLength(input[offset..], out var relativeIndex, out var matchLength))
            {
                return count;
            }

            var matchIndex = offset + relativeIndex;
            offset = matchIndex + 1;

            if (matchIndex < minStartIndex ||
                !MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, matchIndex))
            {
                continue;
            }

            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatchIdentifierTailOnly(input, matchIndex, matchLength, familyPlan, out var matchedLength))
            {
                continue;
            }

            count++;
            var nextStart = matchIndex + Math.Max(matchedLength, 1);
            minStartIndex = nextStart;
            offset = Math.Max(offset, nextStart);
        }

        return count;
    }

    private static int FindNextUpperWordIdentifier(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        return FindNextUpperWordIdentifierDirect(input, familyPlan, startIndex, budget, out matchedLength);
    }

    internal static int CountUpperWordIdentifier(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8ExecutionBudget? budget)
    {
        if (!TryGetUpperWordIdentifierKernelSpec(familyPlan, out var anchorOffset, out var anchorBytes))
        {
            return 0;
        }

        var count = 0;
        var anchorSearchFrom = anchorOffset;

        while (anchorSearchFrom < input.Length)
        {
            var relativeIndex = input[anchorSearchFrom..].IndexOfAny(anchorBytes);
            if (relativeIndex < 0)
            {
                return count;
            }

            var anchorIndex = anchorSearchFrom + relativeIndex;
            anchorSearchFrom = anchorIndex + 1;
            if (!TryGetUpperWordIdentifierPrefixAtAnchor(input, familyPlan, anchorIndex, anchorOffset, out var matchIndex, out var prefixLength))
            {
                continue;
            }

            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!TryMatchUpperWordIdentifierAt(input, familyPlan, matchIndex, prefixLength, out var matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            anchorSearchFrom = Math.Max(anchorSearchFrom, matchIndex + matchedLength + 3);
        }

        return count;
    }

    internal static int FindNextUpperWordIdentifierDirect(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        if (!TryGetUpperWordIdentifierKernelSpec(familyPlan, out var anchorOffset, out var anchorBytes))
        {
            matchedLength = 0;
            return -1;
        }

        matchedLength = 0;
        var anchorSearchFrom = Math.Max(startIndex + anchorOffset, anchorOffset);

        while (anchorSearchFrom < input.Length)
        {
            var relativeIndex = input[anchorSearchFrom..].IndexOfAny(anchorBytes);
            if (relativeIndex < 0)
            {
                return -1;
            }

            var anchorIndex = anchorSearchFrom + relativeIndex;
            anchorSearchFrom = anchorIndex + 1;
            if (!TryGetUpperWordIdentifierPrefixAtAnchor(input, familyPlan, anchorIndex, anchorOffset, out var matchIndex, out var prefixLength) ||
                matchIndex < startIndex)
            {
                continue;
            }

            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!TryMatchUpperWordIdentifierAt(input, familyPlan, matchIndex, prefixLength, out matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            return matchIndex;
        }

        return -1;
    }

    private static int FindNextIdentifierTailOnlyFused(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        PreparedLiteralSetSearch literalSearch,
        int startIndex,
        out int matchedLength)
    {
        matchedLength = 0;
        var offset = startIndex;

        while (offset < input.Length)
        {
            if (!literalSearch.TryFindFirstMatchWithLength(input[offset..], out var relativeIndex, out var matchLength))
            {
                return -1;
            }

            var matchIndex = offset + relativeIndex;
            offset = matchIndex + 1;

            if (!MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, matchIndex))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatchIdentifierTailOnly(input, matchIndex, matchLength, familyPlan, out matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            return matchIndex;
        }

        return -1;
    }

    private static int CountSuffixOnlyReverseLiteral(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        byte[] suffixLiteral,
        int separatorMinCount)
    {
        var count = 0;
        var search = new PreparedSubstringSearch(suffixLiteral, ignoreCase: false);
        var offset = 0;

        while (offset <= input.Length - suffixLiteral.Length)
        {
            var relative = search.IndexOf(input[offset..]);
            if (relative < 0)
            {
                return count;
            }

            var trailingStart = offset + relative;
            var trailingEnd = trailingStart + suffixLiteral.Length;
            offset = trailingStart + 1;

            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.TrailingBoundary, input, trailingEnd))
            {
                continue;
            }

            var separatorEnd = trailingStart;
            var separatorStart = separatorEnd;
            while (separatorStart > 0 &&
                   familyPlan.SeparatorCharClass!.Contains(input[separatorStart - 1]))
            {
                separatorStart--;
            }

            if (separatorEnd - separatorStart < separatorMinCount)
            {
                continue;
            }

            if (!TryMatchPrefixEndingAt(input, familyPlan, separatorStart, out var prefixStart))
            {
                continue;
            }

            count++;
            offset = Math.Max(offset, trailingEnd);
        }

        return count;
    }

    private static int CountSuffixOnlyReverseByte(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        byte suffixByte,
        int separatorMinCount)
    {
        var count = 0;
        var offset = 0;

        while (offset < input.Length)
        {
            var relative = input[offset..].IndexOf(suffixByte);
            if (relative < 0)
            {
                return count;
            }

            var trailingStart = offset + relative;
            var trailingEnd = trailingStart + 1;
            offset = trailingStart + 1;

            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.TrailingBoundary, input, trailingEnd))
            {
                continue;
            }

            var separatorEnd = trailingStart;
            var separatorStart = separatorEnd;
            while (separatorStart > 0 &&
                   familyPlan.SeparatorCharClass!.Contains(input[separatorStart - 1]))
            {
                separatorStart--;
            }

            if (separatorEnd - separatorStart < separatorMinCount)
            {
                continue;
            }

            if (!TryMatchPrefixEndingAt(input, familyPlan, separatorStart, out _))
            {
                continue;
            }

            count++;
            offset = Math.Max(offset, trailingEnd);
        }

        return count;
    }

    private static int CountSuffixOnlyViaPreparedSearcherNonOverlapping(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var minStartIndex = 0;

        while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var matchIndex, out var matchLength))
        {
            if (matchIndex < minStartIndex ||
                !MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, matchIndex))
            {
                continue;
            }

            budget?.Step(input);
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatchSimpleSuffix(
                    input, matchIndex, matchIndex + matchLength, familyPlan, out var matchedLength))
            {
                continue;
            }

            count++;
            minStartIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int CountSuffixOnlyFused(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        PreparedLiteralSetSearch literalSearch)
    {
        if (TryGetSimpleSuffixLiteralKernel(familyPlan, out var suffixLiteral, out var separatorMinCount))
        {
            return CountSuffixOnlyFusedLiteral(input, familyPlan, literalSearch, suffixLiteral, separatorMinCount);
        }

        var count = 0;
        var offset = 0;
        var hasLeadingBoundary = familyPlan.LeadingBoundary == Utf8BoundaryRequirement.Boundary;

        while (offset < input.Length)
        {
            // Find next literal from the family.
            if (!literalSearch.TryFindFirstMatchWithLength(input[offset..], out var relativeIndex, out var matchedLength))
            {
                break;
            }

            var matchIndex = offset + relativeIndex;
            offset = matchIndex + 1;

            // Check leading word boundary (fast inline).
            if (hasLeadingBoundary && matchIndex > 0 &&
                AsciiStructuralIdentifierFamilyMatcher.IsAsciiWordByte(input[matchIndex - 1]))
            {
                continue;
            }

            // Check suffix (separator + suffix parts) via the existing matcher.
            var afterPrefix = matchIndex + matchedLength;
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatchSimpleSuffix(
                    input, matchIndex, afterPrefix, familyPlan, out var fullMatchedLength))
            {
                continue;
            }

            count++;
            offset = matchIndex + Math.Max(fullMatchedLength, 1);
        }

        return count;
    }

    private static int CountSuffixOnlyFusedLiteral(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        PreparedLiteralSetSearch literalSearch,
        byte[] suffixLiteral,
        int separatorMinCount)
    {
        var count = 0;
        var offset = 0;
        var hasLeadingBoundary = familyPlan.LeadingBoundary == Utf8BoundaryRequirement.Boundary;

        while (offset < input.Length)
        {
            if (!literalSearch.TryFindFirstMatchWithLength(input[offset..], out var relativeIndex, out var matchedLength))
            {
                break;
            }

            var matchIndex = offset + relativeIndex;
            offset = matchIndex + 1;

            if (hasLeadingBoundary && matchIndex > 0 &&
                AsciiStructuralIdentifierFamilyMatcher.IsAsciiWordByte(input[matchIndex - 1]))
            {
                continue;
            }

            var index = matchIndex + matchedLength;
            if (!TryConsumeFastSeparatorLoop(input, ref index, familyPlan.SeparatorCharClass, separatorMinCount))
            {
                continue;
            }

            if (input.Length - index < suffixLiteral.Length ||
                !input.Slice(index, suffixLiteral.Length).SequenceEqual(suffixLiteral))
            {
                continue;
            }

            index += suffixLiteral.Length;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.TrailingBoundary, input, index))
            {
                continue;
            }

            count++;
            offset = matchIndex + Math.Max(index - matchIndex, 1);
        }

        return count;
    }

    private static int CountStatefulViaPreparedSearcherIdentifierTailOnlyNonOverlapping(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        var minStartIndex = 0;

        while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var matchIndex, out var matchLength))
        {
            if (matchIndex < minStartIndex ||
                !MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, matchIndex))
            {
                continue;
            }

            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatchIdentifierTailOnly(input, matchIndex, matchLength, familyPlan, out var matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            minStartIndex = matchIndex + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private static int CountSuffixOnlySharedPrefixLiteral(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        AsciiExactLiteralBucket bucket,
        byte[] suffixLiteral,
        int separatorMinCount)
    {
        var count = 0;
        var offset = 0;
        var hasLeadingBoundary = familyPlan.LeadingBoundary == Utf8BoundaryRequirement.Boundary;
        var commonPrefix = bucket.CommonPrefix;
        var discriminator = bucket.PrefixDiscriminator;
        var literalsByByte = discriminator.LiteralsByByte!;

        while (offset <= input.Length - commonPrefix.Length)
        {
            var relative = input[offset..].IndexOf(commonPrefix);
            if (relative < 0)
            {
                break;
            }

            var matchIndex = offset + relative;
            offset = matchIndex + 1;

            if (hasLeadingBoundary && matchIndex > 0 &&
                AsciiStructuralIdentifierFamilyMatcher.IsAsciiWordByte(input[matchIndex - 1]))
            {
                continue;
            }

            var discriminatorIndex = matchIndex + discriminator.Offset;
            if ((uint)discriminatorIndex >= (uint)input.Length)
            {
                continue;
            }

            var literal = literalsByByte[input[discriminatorIndex]];
            if (literal is null ||
                input.Length - matchIndex < literal.Length ||
                (literal.Length > commonPrefix.Length &&
                 !input.Slice(matchIndex + commonPrefix.Length, literal.Length - commonPrefix.Length)
                       .SequenceEqual(literal.AsSpan(commonPrefix.Length))))
            {
                continue;
            }

            var index = matchIndex + literal.Length;
            if (!TryConsumeFastSeparatorLoop(input, ref index, familyPlan.SeparatorCharClass, separatorMinCount))
            {
                continue;
            }

            if (input.Length - index < suffixLiteral.Length ||
                !input.Slice(index, suffixLiteral.Length).SequenceEqual(suffixLiteral))
            {
                continue;
            }

            index += suffixLiteral.Length;
            if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.TrailingBoundary, input, index))
            {
                continue;
            }

            count++;
            offset = matchIndex + Math.Max(index - matchIndex, 1);
        }

        return count;
    }

    private static int FindNextIdentifierTailOnlyNonOverlapping(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        var state = new PreparedMultiLiteralScanState(startIndex, startIndex, 0);

        while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextNonOverlappingLength(input, ref state, out var matchIndex, out var matchLength))
        {
            if (!MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, matchIndex))
            {
                continue;
            }

            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatchIdentifierTailOnly(input, matchIndex, matchLength, familyPlan, out matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            return matchIndex;
        }

        return -1;
    }

    private static int FindNextIdentifierTailOnlyOverlapping(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        var state = new PreparedSearchScanState(startIndex, default);

        while (searchPlan.NativeSearch.PreparedSearcher.TryFindNextOverlappingMatch(input, ref state, out var match))
        {
            if (!MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, match.Index))
            {
                continue;
            }

            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (!AsciiStructuralIdentifierFamilyMatcher.TryMatchIdentifierTailOnly(input, match.Index, match.Length, familyPlan, out matchedLength))
            {
                continue;
            }

            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            return match.Index;
        }

        return -1;
    }

    private static int FindNextTrailingLiteralIdentifier(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        int startIndex,
        Utf8ExecutionBudget? budget,
        out int matchedLength)
    {
        matchedLength = 0;
        if (!TryGetTrailingLiteralIdentifierKernel(familyPlan, out var trailingLiteral, out var trailingLiteralPartIndex))
        {
            return -1;
        }

        var searchFrom = Math.Max(startIndex, 0);
        while (searchFrom <= input.Length - trailingLiteral.Length)
        {
            var relativeIndex = input[searchFrom..].IndexOf(trailingLiteral);
            if (relativeIndex < 0)
            {
                return -1;
            }

            var trailingLiteralStart = searchFrom + relativeIndex;
            budget?.Step(input);
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
            if (TryMatchTrailingLiteralIdentifierAt(
                    input,
                    familyPlan,
                    trailingLiteralStart,
                    trailingLiteral,
                    trailingLiteralPartIndex,
                    startIndex,
                    out var matchIndex,
                    out matchedLength))
            {
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                return matchIndex;
            }

            searchFrom = trailingLiteralStart + 1;
        }

        return -1;
    }

    private static bool TryGetSimpleSuffixLiteralKernel(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        out byte[] suffixLiteral,
        out int separatorMinCount)
    {
        suffixLiteral = [];
        separatorMinCount = 0;

        if (!string.IsNullOrEmpty(familyPlan.IdentifierStartSet) ||
            familyPlan.CompiledSuffixParts.Length != 1 ||
            !familyPlan.CompiledSuffixParts[0].IsLiteral ||
            familyPlan.CompiledSuffixParts[0].LiteralUtf8 is not { Length: > 0 } literal ||
            familyPlan.SeparatorCharClass is null)
        {
            return false;
        }

        suffixLiteral = literal;
        separatorMinCount = familyPlan.SeparatorMinCount;
        return true;
    }

    private static bool TryGetReverseSuffixLiteralKernel(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        out byte[] suffixLiteral,
        out int separatorMinCount)
    {
        suffixLiteral = [];
        separatorMinCount = 0;

        if (!TryGetSimpleSuffixLiteralKernel(familyPlan, out suffixLiteral, out separatorMinCount) ||
            familyPlan.SeparatorCharClass is null ||
            familyPlan.Prefixes is not { Length: > 0 } ||
            familyPlan.LeadingBoundary != Utf8BoundaryRequirement.Boundary ||
            suffixLiteral.Length < 2)
        {
            return false;
        }

        return true;
    }

    private static bool TryGetReverseSuffixByteKernel(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        out byte suffixByte,
        out int separatorMinCount)
    {
        suffixByte = 0;
        separatorMinCount = 0;

        if (!TryGetSimpleSuffixLiteralKernel(familyPlan, out var suffixLiteral, out separatorMinCount) ||
            familyPlan.SeparatorCharClass is null ||
            familyPlan.Prefixes is not { Length: > 0 } ||
            familyPlan.LeadingBoundary != Utf8BoundaryRequirement.Boundary ||
            suffixLiteral.Length != 1)
        {
            return false;
        }

        suffixByte = suffixLiteral[0];
        return true;
    }

    private static bool TryGetSharedPrefixSuffixOnlyKernel(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        AsciiExactLiteralSearchData searchData,
        out AsciiExactLiteralBucket bucket,
        out byte[] suffixLiteral,
        out int separatorMinCount)
    {
        bucket = default;
        suffixLiteral = [];
        separatorMinCount = 0;

        if (!TryGetSimpleSuffixLiteralKernel(familyPlan, out suffixLiteral, out separatorMinCount) ||
            searchData.Buckets is not [var onlyBucket] ||
            onlyBucket.CommonPrefix.Length < 4 ||
            !onlyBucket.PrefixDiscriminator.HasValue)
        {
            return false;
        }

        bucket = onlyBucket;
        return true;
    }

    private static bool TryConsumeFastSeparatorLoop(
        ReadOnlySpan<byte> input,
        ref int index,
        AsciiCharClass? separatorCharClass,
        int separatorMinCount)
    {
        if (separatorCharClass is null)
        {
            return separatorMinCount == 0;
        }

        var count = 0;
        while ((uint)index < (uint)input.Length &&
               separatorCharClass.Contains(input[index]))
        {
            index++;
            count++;
        }

        return count >= separatorMinCount;
    }

    private static bool TryMatchPrefixEndingAt(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        int prefixEnd,
        out int prefixStart)
    {
        prefixStart = -1;
        foreach (var prefix in familyPlan.Prefixes)
        {
            var start = prefixEnd - prefix.Length;
            if (start < 0)
            {
                continue;
            }

            if (!input.Slice(start, prefix.Length).SequenceEqual(prefix) ||
                !MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, start))
            {
                continue;
            }

            prefixStart = start;
            return true;
        }

        return false;
    }

    private static bool TryGetTrailingLiteralIdentifierKernel(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        out byte[] trailingLiteral,
        out int trailingLiteralPartIndex)
    {
        trailingLiteral = [];
        trailingLiteralPartIndex = -1;

        if (string.IsNullOrEmpty(familyPlan.IdentifierStartSet) ||
            familyPlan.IdentifierStartCharClass is null ||
            familyPlan.IdentifierTailCharClass is null ||
            familyPlan.Prefixes.Length == 0 ||
            familyPlan.CompiledSuffixParts.Length != 1 ||
            !familyPlan.CompiledSuffixParts[^1].IsLiteral ||
            familyPlan.CompiledSuffixParts[^1].LiteralUtf8 is not { Length: > 0 } literal)
        {
            return false;
        }

        trailingLiteral = literal;
        trailingLiteralPartIndex = familyPlan.CompiledSuffixParts.Length - 1;
        return true;
    }

    private static bool CanUseTrailingLiteralIdentifierKernel(in AsciiStructuralIdentifierFamilyPlan familyPlan)
    {
        return TryGetTrailingLiteralIdentifierKernel(familyPlan, out _, out _);
    }

    private static bool CanUseIdentifierTailOnlyCountKernel(in AsciiStructuralIdentifierFamilyPlan familyPlan)
    {
        return !string.IsNullOrEmpty(familyPlan.IdentifierStartSet) &&
            familyPlan.CompiledSuffixParts.Length == 0;
    }

    internal static bool CanUseUpperWordIdentifierKernel(in AsciiStructuralIdentifierFamilyPlan familyPlan)
    {
        if (!familyPlan.HasAsciiUpperWordTailKernel ||
            familyPlan.SeparatorCharClass is null ||
            familyPlan.SeparatorMinCount != 1 ||
            familyPlan.LeadingBoundary != Utf8BoundaryRequirement.Boundary ||
            familyPlan.TrailingBoundary != Utf8BoundaryRequirement.None ||
            familyPlan.Prefixes.Length == 0)
        {
            return false;
        }

        return TryGetUpperWordIdentifierKernelSpec(familyPlan, out _, out _);
    }

    internal static bool CanUseSharedPrefixSuffixKernel(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan)
    {
        return TryGetSharedPrefixSuffixKernelSpec(familyPlan, searchPlan, out _, out _);
    }

    internal static bool TryGetUpperWordIdentifierKernelSpec(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        out int anchorOffset,
        out byte[] anchorBytes)
    {
        anchorOffset = 0;
        anchorBytes = [];

        if (!familyPlan.HasAsciiUpperWordTailKernel ||
            familyPlan.SeparatorCharClass is null ||
            familyPlan.SeparatorMinCount != 1 ||
            familyPlan.LeadingBoundary != Utf8BoundaryRequirement.Boundary ||
            familyPlan.TrailingBoundary != Utf8BoundaryRequirement.None ||
            familyPlan.Prefixes.Length == 0)
        {
            return false;
        }

        var minPrefixLength = int.MaxValue;
        foreach (var prefix in familyPlan.Prefixes)
        {
            if (prefix.Length == 0)
            {
                return false;
            }

            minPrefixLength = Math.Min(minPrefixLength, prefix.Length);
        }

        if (minPrefixLength <= 0)
        {
            return false;
        }

        var bestDistinctCount = 0;
        var bestRarityScore = int.MinValue;
        byte[]? bestAnchorBytes = null;
        for (var offset = 0; offset < minPrefixLength; offset++)
        {
            var seen = new bool[256];
            var distinctCount = 0;
            var rarityScore = 0;
            foreach (var prefix in familyPlan.Prefixes)
            {
                var value = prefix[offset];
                if (!AsciiStructuralIdentifierFamilyMatcher.IsAsciiWordByte(value))
                {
                    distinctCount = 0;
                    break;
                }

                if (!seen[value])
                {
                    seen[value] = true;
                    distinctCount++;
                }

                rarityScore += GetAsciiAnchorRarityScore(value);
            }

            if (distinctCount < 2 ||
                distinctCount < bestDistinctCount ||
                (distinctCount == bestDistinctCount && rarityScore < bestRarityScore) ||
                (distinctCount == bestDistinctCount && rarityScore == bestRarityScore && bestAnchorBytes is not null && offset < anchorOffset))
            {
                continue;
            }

            var anchors = new byte[distinctCount];
            var anchorIndex = 0;
            for (var value = 0; value < seen.Length; value++)
            {
                if (seen[value])
                {
                    anchors[anchorIndex++] = (byte)value;
                }
            }

            bestDistinctCount = distinctCount;
            bestRarityScore = rarityScore;
            bestAnchorBytes = anchors;
            anchorOffset = offset;
        }

        if (bestAnchorBytes is null)
        {
            return false;
        }

        anchorBytes = bestAnchorBytes;
        return true;
    }

    internal static bool TryGetSharedPrefixSuffixKernelSpec(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        out AsciiExactLiteralBucket bucket,
        out byte requiredSuffixByte)
    {
        bucket = default;
        requiredSuffixByte = 0;

        if (searchPlan.AlternateLiteralSearchData is not { } searchData ||
            !TryGetSharedPrefixSuffixOnlyKernel(familyPlan, searchData, out bucket, out var suffixLiteral, out var separatorMinCount) ||
            !TryUseAsciiWhitespaceSingleByteSuffixKernel(familyPlan, suffixLiteral, separatorMinCount, out requiredSuffixByte))
        {
            bucket = default;
            requiredSuffixByte = 0;
            return false;
        }

        return true;
    }

    internal static bool TryGetSharedPrefixSuffixLiteralFamilyKernelSpec(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        out AsciiExactLiteralBucket bucket,
        out byte requiredSuffixByte)
    {
        bucket = default;
        requiredSuffixByte = 0;

        if (searchPlan.AlternateLiteralSearchData is not { Buckets: [var onlyBucket] } ||
            !TryGetSimpleSuffixLiteralKernel(familyPlan, out var suffixLiteral, out var separatorMinCount) ||
            !TryUseAsciiWhitespaceSingleByteSuffixKernel(familyPlan, suffixLiteral, separatorMinCount, out requiredSuffixByte) ||
            onlyBucket.CommonPrefix.Length < 3 ||
            onlyBucket.Literals.Length is < 2 or > 4)
        {
            requiredSuffixByte = 0;
            return false;
        }

        bucket = onlyBucket;
        return true;
    }

    internal static SharedPrefixSuffixKernelDiagnostics GetSharedPrefixSuffixKernelDiagnostics(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan)
    {
        var searchData = searchPlan.AlternateLiteralSearchData;
        var bucketCount = searchData?.Buckets.Length ?? 0;
        var commonPrefixLength = 0;
        var hasPrefixDiscriminator = false;
        int? prefixDiscriminatorOffset = null;
        var suffixLiteralLength = 0;
        var separatorMinCount = 0;
        var hasAsciiWhitespaceSeparatorClass =
            familyPlan.SeparatorCharClass is not null &&
            IsAsciiWhitespaceCharClass(familyPlan.SeparatorCharClass);
        var canUseSharedPrefixSuffixKernelSpec = false;
        var canUseSharedPrefixSuffixLiteralFamilyKernelSpec = false;
        var canUseAsciiWhitespaceSingleByteSuffixKernel = false;

        if (searchData is { Buckets: [var onlyBucket] })
        {
            commonPrefixLength = onlyBucket.CommonPrefix.Length;
            hasPrefixDiscriminator = onlyBucket.PrefixDiscriminator.HasValue;
            if (hasPrefixDiscriminator)
            {
                prefixDiscriminatorOffset = onlyBucket.PrefixDiscriminator.Offset;
            }
        }

        if (TryGetSimpleSuffixLiteralKernel(familyPlan, out var suffixLiteral, out separatorMinCount))
        {
            suffixLiteralLength = suffixLiteral.Length;
            canUseAsciiWhitespaceSingleByteSuffixKernel =
                TryUseAsciiWhitespaceSingleByteSuffixKernel(familyPlan, suffixLiteral, separatorMinCount, out _);
        }

        canUseSharedPrefixSuffixKernelSpec =
            TryGetSharedPrefixSuffixKernelSpec(familyPlan, searchPlan, out _, out _);
        canUseSharedPrefixSuffixLiteralFamilyKernelSpec =
            TryGetSharedPrefixSuffixLiteralFamilyKernelSpec(familyPlan, searchPlan, out _, out _);

        return new SharedPrefixSuffixKernelDiagnostics(
            bucketCount,
            commonPrefixLength,
            hasPrefixDiscriminator,
            prefixDiscriminatorOffset,
            suffixLiteralLength,
            separatorMinCount,
            hasAsciiWhitespaceSeparatorClass,
            familyPlan.LeadingBoundary,
            familyPlan.TrailingBoundary,
            canUseSharedPrefixSuffixKernelSpec,
            canUseSharedPrefixSuffixLiteralFamilyKernelSpec,
            canUseAsciiWhitespaceSingleByteSuffixKernel);
    }

    private static bool TryUseAsciiWhitespaceSingleByteSuffixKernel(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        byte[] suffixLiteral,
        int separatorMinCount,
        out byte requiredSuffixByte)
    {
        requiredSuffixByte = 0;

        if (separatorMinCount != 0 ||
            familyPlan.LeadingBoundary != Utf8BoundaryRequirement.Boundary ||
            familyPlan.TrailingBoundary != Utf8BoundaryRequirement.None ||
            suffixLiteral.Length != 1 ||
            familyPlan.SeparatorCharClass is null ||
            !IsAsciiWhitespaceCharClass(familyPlan.SeparatorCharClass))
        {
            return false;
        }

        requiredSuffixByte = suffixLiteral[0];
        return true;
    }

    private static bool IsAsciiWhitespaceCharClass(AsciiCharClass charClass)
    {
        if (charClass.Negated)
        {
            return false;
        }

        return charClass.Contains((byte)' ') &&
            charClass.Contains((byte)'\t') &&
            charClass.Contains((byte)'\r') &&
            charClass.Contains((byte)'\n') &&
            charClass.Contains(0x0B) &&
            charClass.Contains(0x0C) &&
            !charClass.Contains((byte)'(') &&
            !charClass.Contains((byte)'A');
    }

    private static int GetAsciiAnchorRarityScore(byte value)
    {
        value = (byte)char.ToUpperInvariant((char)value);
        return value switch
        {
            (byte)'Q' or (byte)'J' or (byte)'X' or (byte)'Z' => 10,
            (byte)'K' or (byte)'V' or (byte)'B' or (byte)'P' or (byte)'Y' or (byte)'G' or (byte)'W' => 8,
            (byte)'F' or (byte)'M' or (byte)'U' or (byte)'C' or (byte)'L' or (byte)'D' => 6,
            (byte)'R' or (byte)'H' or (byte)'S' or (byte)'N' or (byte)'I' or (byte)'O' => 4,
            (byte)'A' or (byte)'T' or (byte)'E' => 2,
            _ when value is >= (byte)'0' and <= (byte)'9' => 5,
            (byte)'_' => 3,
            _ => 1,
        };
    }

    private static bool CanUsePreparedSearcherNonOverlappingCountKernel(in AsciiStructuralIdentifierFamilyPlan familyPlan)
    {
        return familyPlan.LeadingBoundary == Utf8BoundaryRequirement.Boundary;
    }

    private static bool MatchesLeadingBoundaryForWordPrefix(
        Utf8BoundaryRequirement requirement,
        ReadOnlySpan<byte> input,
        int matchIndex)
    {
        return requirement switch
        {
            Utf8BoundaryRequirement.None => true,
            Utf8BoundaryRequirement.Boundary => matchIndex == 0 ||
                !AsciiStructuralIdentifierFamilyMatcher.IsAsciiWordByte(input[matchIndex - 1]),
            Utf8BoundaryRequirement.NonBoundary => matchIndex > 0 &&
                AsciiStructuralIdentifierFamilyMatcher.IsAsciiWordByte(input[matchIndex - 1]),
            _ => AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(requirement, input, matchIndex),
        };
    }

    private static bool TryMatchAt(ReadOnlySpan<byte> input, int matchIndex, int prefixLength, in AsciiStructuralIdentifierFamilyPlan familyPlan, Utf8StructuralVerifierRuntime verifierRuntime, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        return AsciiStructuralIdentifierFamilyMatcher.TryMatch(input, matchIndex, prefixLength, familyPlan, out matchedLength);
    }

    private static bool TryMatchTrailingLiteralIdentifierAt(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        int trailingLiteralStart,
        ReadOnlySpan<byte> trailingLiteral,
        int trailingLiteralPartIndex,
        int minMatchIndex,
        out int matchIndex,
        out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;

        var trailingEnd = trailingLiteralStart + trailingLiteral.Length;
        if (!AsciiStructuralIdentifierFamilyMatcher.MatchesBoundaryRequirement(familyPlan.TrailingBoundary, input, trailingEnd))
        {
            return false;
        }

        var index = trailingLiteralStart;
        if (!TryReverseMatchSuffixParts(input, ref index, familyPlan.CompiledSuffixParts[..trailingLiteralPartIndex]) ||
            !TryReverseMatchIdentifierAndPrefix(input, familyPlan, ref index, out matchIndex) ||
            matchIndex < minMatchIndex)
        {
            matchIndex = -1;
            return false;
        }

        matchedLength = trailingEnd - matchIndex;
        return true;
    }

    private static bool TryMatchUpperWordIdentifierAt(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        int matchIndex,
        int prefixLength,
        out int matchedLength)
    {
        matchedLength = 0;

        if (!MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, matchIndex))
        {
            return false;
        }

        var index = matchIndex + prefixLength;
        if ((uint)index >= (uint)input.Length ||
            !familyPlan.SeparatorCharClass!.Contains(input[index]))
        {
            return false;
        }

        while ((uint)index < (uint)input.Length &&
               familyPlan.SeparatorCharClass.Contains(input[index]))
        {
            index++;
        }

        if ((uint)index >= (uint)input.Length ||
            input[index] is < (byte)'A' or > (byte)'Z')
        {
            return false;
        }

        index++;
        while ((uint)index < (uint)input.Length &&
               AsciiStructuralIdentifierFamilyMatcher.IsAsciiWordByte(input[index]))
        {
            index++;
        }

        matchedLength = index - matchIndex;
        return true;
    }

    private static bool TryReverseMatchSuffixParts(
        ReadOnlySpan<byte> input,
        ref int index,
        ReadOnlySpan<AsciiStructuralCompiledSuffixPart> suffixParts)
    {
        for (var i = suffixParts.Length - 1; i >= 0; i--)
        {
            var part = suffixParts[i];
            if (part.IsLiteral)
            {
                var literal = part.LiteralUtf8;
                if (literal is null ||
                    index < literal.Length ||
                    !input.Slice(index - literal.Length, literal.Length).SequenceEqual(literal))
                {
                    return false;
                }

                index -= literal.Length;
                continue;
            }

            if (!TryReverseConsumeFastSeparatorLoop(input, ref index, part.SeparatorCharClass, part.SeparatorMinCount))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReverseMatchIdentifierAndPrefix(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        ref int index,
        out int matchIndex)
    {
        matchIndex = -1;
        var tailEnd = index;
        var maxTailCount = 0;
        while (index > 0 &&
               familyPlan.IdentifierTailCharClass!.Contains(input[index - 1]) &&
               maxTailCount < familyPlan.IdentifierTailMaxCount)
        {
            index--;
            maxTailCount++;
        }

        if (maxTailCount < familyPlan.IdentifierTailMinCount)
        {
            return false;
        }

        var earliestTailStart = tailEnd - maxTailCount;
        var latestTailStart = tailEnd - familyPlan.IdentifierTailMinCount;
        for (var tailStart = latestTailStart; tailStart >= earliestTailStart; tailStart--)
        {
            if (tailStart <= 0 ||
                !familyPlan.IdentifierStartCharClass!.Contains(input[tailStart - 1]))
            {
                continue;
            }

            var prefixEnd = tailStart - 1;
            if (!TryReverseConsumeFastSeparatorLoop(input, ref prefixEnd, familyPlan.SeparatorCharClass, familyPlan.SeparatorMinCount) ||
                !TryMatchPrefixEndingAt(input, familyPlan, prefixEnd, out matchIndex))
            {
                continue;
            }

            index = prefixEnd;
            return true;
        }

        index = tailEnd;
        return false;
    }

    private static bool TryReverseConsumeFastSeparatorLoop(
        ReadOnlySpan<byte> input,
        ref int index,
        AsciiCharClass? separatorCharClass,
        int separatorMinCount)
    {
        if (separatorCharClass is null)
        {
            return separatorMinCount == 0;
        }

        var count = 0;
        while (index > 0 &&
               separatorCharClass.Contains(input[index - 1]))
        {
            index--;
            count++;
        }

        return count >= separatorMinCount;
    }

    private static bool TryGetUpperWordIdentifierPrefixAtAnchor(
        ReadOnlySpan<byte> input,
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        int anchorIndex,
        int anchorOffset,
        out int matchIndex,
        out int prefixLength)
    {
        matchIndex = -1;
        prefixLength = 0;
        foreach (var prefix in familyPlan.Prefixes)
        {
            if (anchorOffset >= prefix.Length)
            {
                continue;
            }

            var candidateStart = anchorIndex - anchorOffset;
            if (candidateStart < 0 ||
                input.Length - candidateStart < prefix.Length ||
                prefix[anchorOffset] != input[anchorIndex] ||
                !input.Slice(candidateStart, prefix.Length).SequenceEqual(prefix) ||
                !MatchesLeadingBoundaryForWordPrefix(familyPlan.LeadingBoundary, input, candidateStart))
            {
                continue;
            }

            matchIndex = candidateStart;
            prefixLength = prefix.Length;
            return true;
        }

        return false;
    }

}

