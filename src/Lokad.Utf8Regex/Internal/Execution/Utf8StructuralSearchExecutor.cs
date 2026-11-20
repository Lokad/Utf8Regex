using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8StructuralSearchExecutor
{
    public static bool TryFindNextCandidate(
        this Utf8StructuralSearchPlan plan,
        ReadOnlySpan<byte> input,
        ref Utf8StructuralSearchState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;

        if (plan.Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        return plan.YieldKind switch
        {
            Utf8StructuralSearchYieldKind.Start => TryFindNextStartCandidate(input, stages, ref state, out candidate),
            Utf8StructuralSearchYieldKind.Window => TryFindNextWindowCandidate(input, stages, ref state, out candidate),
            _ => false,
        };
    }

    public static bool TryFindLastCandidate(
        this Utf8StructuralSearchPlan plan,
        ReadOnlySpan<byte> input,
        int endIndex,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;

        if ((uint)endIndex > (uint)input.Length)
        {
            endIndex = input.Length;
        }

        if (plan.Stages is not { Length: > 0 } stages)
        {
            return false;
        }

        return plan.YieldKind switch
        {
            Utf8StructuralSearchYieldKind.Start => TryFindLastStartCandidate(input, stages, endIndex, out candidate),
            _ => false,
        };
    }

    private static bool TryFindNextStartCandidate(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages,
        ref Utf8StructuralSearchState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        var searcher = default(PreparedSearcher);
        Utf8FixedDistanceSet[] fixedDistanceSets = [];
        var startTransform = default(Utf8FallbackStartTransform);

        foreach (var stage in stages)
        {
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.FindLiteralFamily:
                    searcher = stage.Searcher;
                    break;
                case Utf8StructuralSearchStageKind.FindAscii:
                    fixedDistanceSets = stage.FixedDistanceSets;
                    break;
                case Utf8StructuralSearchStageKind.TransformCandidateStart:
                    startTransform = stage.StartTransform;
                    break;
            }
        }

        if (!searcher.HasValue && fixedDistanceSets.Length == 0)
        {
            return false;
        }

        var searchState = state.SearchState;
        while (true)
        {
            int rawStartIndex;
            int matchedLength;
            int literalId;
            if (searcher.HasValue)
            {
                if (!searcher.TryFindNextOverlappingMatch(input, ref searchState, out var match))
                {
                    state = new Utf8StructuralSearchState(searchState, default);
                    return false;
                }

                rawStartIndex = match.Index;
                matchedLength = match.Length;
                literalId = match.LiteralId;
            }
            else
            {
                if (!TryFindNextFixedDistanceCandidate(input, fixedDistanceSets, searchState.NextStart, out rawStartIndex))
                {
                    state = new Utf8StructuralSearchState(searchState, default);
                    return false;
                }

                matchedLength = 1;
                literalId = 0;
                searchState = new PreparedSearchScanState(rawStartIndex + 1, default);
            }

            var startIndex = startTransform.Apply(input, rawStartIndex);
            if (startIndex < 0)
            {
                continue;
            }

            var requirementBaseIndex = GetRequirementBaseIndex(input, startIndex, startTransform);
            if (!SatisfiesStartRequirements(input, stages, requirementBaseIndex, matchedLength))
            {
                continue;
            }

            state = new Utf8StructuralSearchState(searchState, default);
            var endIndex = GetExactCandidateEnd(stages, startIndex, input.Length);
            candidate = Utf8StructuralCandidate.Start(startIndex, endIndex, matchedLength, literalId);
            return true;
        }
    }

    private static bool TryFindNextWindowCandidate(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages,
        ref Utf8StructuralSearchState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        var windowSearch = default(PreparedWindowSearch);
        var startTransform = default(Utf8FallbackStartTransform);

        foreach (var stage in stages)
        {
            if (stage.Kind == Utf8StructuralSearchStageKind.FindWindow)
            {
                windowSearch = stage.WindowSearch;
                continue;
            }

            if (stage.Kind == Utf8StructuralSearchStageKind.TransformCandidateStart)
            {
                startTransform = stage.StartTransform;
            }
        }

        if (!windowSearch.HasValue)
        {
            return false;
        }

        var windowState = state.WindowState;
        while (windowSearch.TryFindNextWindow(input, ref windowState, out var window))
        {
            if (!SatisfiesWindowRequirements(input, stages, window))
            {
                continue;
            }

            var startIndex = startTransform.Apply(input, window.Leading.Index);
            if (startIndex < 0)
            {
                continue;
            }

            state = new Utf8StructuralSearchState(default, windowState);
            candidate = Utf8StructuralCandidate.Window(
                startIndex,
                window.Trailing.Index + window.Trailing.Length,
                window.Leading.Length,
                window.Leading.LiteralId,
                window.Trailing.Index,
                window.Trailing.Length,
                window.Trailing.LiteralId);
            return true;
        }

        state = new Utf8StructuralSearchState(default, windowState);
        return false;
    }

    private static bool TryFindLastStartCandidate(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages,
        int endIndex,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        var searcher = default(PreparedSearcher);
        Utf8FixedDistanceSet[] fixedDistanceSets = [];
        var startTransform = default(Utf8FallbackStartTransform);

        foreach (var stage in stages)
        {
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.FindLiteralFamily:
                    searcher = stage.Searcher;
                    break;
                case Utf8StructuralSearchStageKind.FindAscii:
                    fixedDistanceSets = stage.FixedDistanceSets;
                    break;
                case Utf8StructuralSearchStageKind.TransformCandidateStart:
                    startTransform = stage.StartTransform;
                    break;
            }
        }

        if (!searcher.HasValue && fixedDistanceSets.Length == 0)
        {
            return false;
        }

        if (searcher.HasValue)
        {
            var searchLength = endIndex;
            while (searchLength > 0)
            {
                if (!searcher.TryFindLastMatch(input[..searchLength], out var match))
                {
                    return false;
                }

                var startIndex = startTransform.Apply(input, match.Index);
                if (startIndex >= 0)
                {
                    var requirementBaseIndex = GetRequirementBaseIndex(input, startIndex, startTransform);
                    if (SatisfiesStartRequirements(input, stages, requirementBaseIndex, match.Length))
                    {
                        var candidateEndIndex = GetExactCandidateEnd(stages, startIndex, input.Length);
                        candidate = Utf8StructuralCandidate.Start(startIndex, candidateEndIndex, match.Length, match.LiteralId);
                        return true;
                    }
                }

                // A rejected fixed literal narrows the next start by one byte. A
                // prefix ending at the rejected start would also exclude overlaps.
                searchLength = searcher.Kind == PreparedSearcherKind.ExactLiteral && match.Length > 0
                    ? match.Index + match.Length - 1 : match.Index;
            }

            return false;
        }

        var lastStart = -1;
        var searchFrom = 0;
        while (TryFindNextFixedDistanceCandidate(input[..endIndex], fixedDistanceSets, searchFrom, out var candidateStart))
        {
            var startIndex = startTransform.Apply(input, candidateStart);
            if (startIndex >= 0)
            {
                var requirementBaseIndex = GetRequirementBaseIndex(input, startIndex, startTransform);
                if (SatisfiesStartRequirements(input, stages, requirementBaseIndex, 1))
                {
                    lastStart = startIndex;
                }
            }

            searchFrom = candidateStart + 1;
        }

        if (lastStart >= 0)
        {
            var candidateEndIndex = GetExactCandidateEnd(stages, lastStart, input.Length);
            candidate = Utf8StructuralCandidate.Start(lastStart, candidateEndIndex, 1, 0);
            return true;
        }

        return false;
    }

    private static bool TryFindNextFixedDistanceCandidate(
        ReadOnlySpan<byte> input,
        Utf8FixedDistanceSet[] sets,
        int startIndex,
        out int candidateIndex)
    {
        candidateIndex = -1;

        if (sets.Length == 0 || (uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        var primary = sets[0];
        var anchorStart = Math.Max(0, startIndex + primary.Distance);
        var search = primary.Search;
        var position = anchorStart;
        while (position < input.Length)
        {
            var relative = search.IndexOf(input[position..]);
            if (relative < 0)
            {
                return false;
            }

            var anchorIndex = position + relative;
            var start = anchorIndex - primary.Distance;
            if (start >= startIndex && MatchesAllSets(input, start, sets))
            {
                candidateIndex = start;
                return true;
            }

            position = anchorIndex + 1;
        }

        return false;
    }

    private static bool MatchesAllSets(ReadOnlySpan<byte> input, int startIndex, Utf8FixedDistanceSet[] sets)
    {
        foreach (var set in sets)
        {
            var index = startIndex + set.Distance;
            if ((uint)index >= (uint)input.Length || !set.Contains(input[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SatisfiesStartRequirements(ReadOnlySpan<byte> input, Utf8StructuralSearchStage[] stages, int startIndex, int matchLength)
    {
        foreach (var stage in stages)
        {
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.RequireLeadingBoundary:
                    if (!Utf8ScalarWordBoundary.MatchesRequirement(stage.BoundaryRequirement, input, startIndex))
                    {
                        return false;
                    }

                    break;

                case Utf8StructuralSearchStageKind.RequireTrailingBoundary:
                    if (!Utf8ScalarWordBoundary.MatchesRequirement(stage.BoundaryRequirement, input, startIndex + matchLength))
                    {
                        return false;
                    }

                    break;

                case Utf8StructuralSearchStageKind.RequireTrailingLiteral:
                    if (stage.LiteralUtf8 is not { Length: > 0 } trailingLiteralUtf8)
                    {
                        break;
                    }

                    var trailingOffset = startIndex + matchLength;
                    if (trailingOffset < 0 || trailingOffset > input.Length - trailingLiteralUtf8.Length)
                    {
                        return false;
                    }

                    if (!input.Slice(trailingOffset, trailingLiteralUtf8.Length).SequenceEqual(trailingLiteralUtf8))
                    {
                        return false;
                    }

                    break;

                case Utf8StructuralSearchStageKind.RequireExactLength:
                    if (startIndex > input.Length - stage.MinLength)
                    {
                        return false;
                    }

                    break;
            }
        }

        return true;
    }

    private static bool SatisfiesWindowRequirements(
        ReadOnlySpan<byte> input,
        Utf8StructuralSearchStage[] stages,
        PreparedWindowMatch window)
    {
        var span = window.Trailing.Index + window.Trailing.Length - window.Leading.Index;
        foreach (var stage in stages)
        {
            switch (stage.Kind)
            {
                case Utf8StructuralSearchStageKind.RequireWithinByteSpan:
                    if (span > stage.MaxSpan)
                    {
                        return false;
                    }
                    break;

                case Utf8StructuralSearchStageKind.RequireWithinLineSpan:
                    if (!SatisfiesLineSpan(input, window.Leading.Index, window.Trailing.Index, stage.MaxLines))
                    {
                        return false;
                    }
                    break;

                case Utf8StructuralSearchStageKind.RequireLeadingBoundary:
                    if (!Utf8ScalarWordBoundary.MatchesRequirement(stage.BoundaryRequirement, input, window.Leading.Index))
                    {
                        return false;
                    }
                    break;

                case Utf8StructuralSearchStageKind.RequireTrailingBoundary:
                    if (!Utf8ScalarWordBoundary.MatchesRequirement(stage.BoundaryRequirement, input, window.Trailing.Index + window.Trailing.Length))
                    {
                        return false;
                    }
                    break;
            }
        }

        return true;
    }

    private static bool SatisfiesLineSpan(ReadOnlySpan<byte> input, int startIndex, int endIndex, int maxLines)
    {
        if (maxLines <= 0)
        {
            return false;
        }

        var lineCount = 1;
        for (var i = startIndex; i < endIndex; i++)
        {
            if (input[i] is (byte)'\r' or (byte)'\n')
            {
                lineCount++;
                if (lineCount > maxLines)
                {
                    return false;
                }

                if (input[i] == (byte)'\r' &&
                    i + 1 < endIndex &&
                    input[i + 1] == (byte)'\n')
                {
                    i++;
                }
            }
        }

        return true;
    }

    private static int GetRequirementBaseIndex(ReadOnlySpan<byte> input, int startIndex, Utf8FallbackStartTransform startTransform)
    {
        if (startTransform.Kind != Utf8FallbackStartTransformKind.TrimLeadingAsciiWhitespace)
        {
            return startIndex;
        }

        while ((uint)startIndex < (uint)input.Length &&
               input[startIndex] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n' or 0x0B or 0x0C)
        {
            startIndex++;
        }

        return startIndex;
    }

    private static int GetExactCandidateEnd(Utf8StructuralSearchStage[] stages, int startIndex, int inputLength)
    {
        foreach (var stage in stages)
        {
            if (stage.Kind == Utf8StructuralSearchStageKind.RequireExactLength)
            {
                var endIndex = startIndex + stage.MinLength;
                return endIndex <= inputLength ? endIndex : -1;
            }

            if (stage.Kind == Utf8StructuralSearchStageKind.BoundMaxLength)
            {
                var boundedEndIndex = startIndex + stage.MaxSpan;
                return boundedEndIndex <= inputLength ? boundedEndIndex : inputLength;
            }
        }

        return -1;
    }

}
