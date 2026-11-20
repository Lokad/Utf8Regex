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

        ref readonly var data = ref plan.Payload;
        return data.YieldKind switch
        {
            Utf8StructuralSearchYieldKind.Start => TryFindNextStartCandidate(input, in data, ref state, out candidate),
            Utf8StructuralSearchYieldKind.Window => TryFindNextWindowCandidate(input, in data, ref state, out candidate),
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

        ref readonly var data = ref plan.Payload;
        return data.YieldKind switch
        {
            Utf8StructuralSearchYieldKind.Start => TryFindLastStartCandidate(input, in data, endIndex, out candidate),
            _ => false,
        };
    }

    private static bool TryFindNextStartCandidate(
        ReadOnlySpan<byte> input,
        in Utf8StructuralSearchPlan.Data plan,
        ref Utf8StructuralSearchState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        ref readonly var searcher = ref plan.Searcher;
        var startTransform = plan.StartTransform;

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
                if (!TryFindNextFixedDistanceCandidate(input, plan.FixedDistanceSets!, searchState.NextStart, out rawStartIndex))
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
            if (!SatisfiesStartRequirements(input, in plan, requirementBaseIndex, matchedLength))
            {
                continue;
            }

            state = new Utf8StructuralSearchState(searchState, default);
            var endIndex = GetCandidateEnd(in plan, startIndex, input.Length);
            candidate = Utf8StructuralCandidate.Start(startIndex, endIndex, matchedLength, literalId);
            return true;
        }
    }

    private static bool TryFindNextWindowCandidate(
        ReadOnlySpan<byte> input,
        in Utf8StructuralSearchPlan.Data plan,
        ref Utf8StructuralSearchState state,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        ref readonly var windowSearch = ref plan.WindowSearch;
        var startTransform = plan.StartTransform;

        var windowState = state.WindowState;
        while (windowSearch.TryFindNextWindow(input, ref windowState, out var window))
        {
            if (!SatisfiesWindowRequirements(input, in plan, window))
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
        in Utf8StructuralSearchPlan.Data plan,
        int endIndex,
        out Utf8StructuralCandidate candidate)
    {
        candidate = default;
        ref readonly var searcher = ref plan.Searcher;
        var startTransform = plan.StartTransform;

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
                    if (SatisfiesStartRequirements(input, in plan, requirementBaseIndex, match.Length))
                    {
                        var candidateEndIndex = GetCandidateEnd(in plan, startIndex, input.Length);
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
        while (TryFindNextFixedDistanceCandidate(input[..endIndex], plan.FixedDistanceSets!, searchFrom, out var candidateStart))
        {
            var startIndex = startTransform.Apply(input, candidateStart);
            if (startIndex >= 0)
            {
                var requirementBaseIndex = GetRequirementBaseIndex(input, startIndex, startTransform);
                if (SatisfiesStartRequirements(input, in plan, requirementBaseIndex, 1))
                {
                    lastStart = startIndex;
                }
            }

            searchFrom = candidateStart + 1;
        }

        if (lastStart >= 0)
        {
            var candidateEndIndex = GetCandidateEnd(in plan, lastStart, input.Length);
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

    private static bool SatisfiesStartRequirements(
        ReadOnlySpan<byte> input, in Utf8StructuralSearchPlan.Data plan, int startIndex, int matchLength)
    {
        if (plan.LeadingBoundary != Utf8BoundaryRequirement.None &&
            !Utf8ScalarWordBoundary.MatchesRequirement(plan.LeadingBoundary, input, startIndex))
        {
            return false;
        }

        var trailingOffset = startIndex + matchLength;
        if (plan.TrailingBoundary != Utf8BoundaryRequirement.None &&
            !Utf8ScalarWordBoundary.MatchesRequirement(plan.TrailingBoundary, input, trailingOffset))
        {
            return false;
        }

        if (plan.TrailingLiteral is { Length: > 0 } literal &&
            (trailingOffset < 0 || trailingOffset > input.Length - literal.Length ||
             !input.Slice(trailingOffset, literal.Length).SequenceEqual(literal)))
        {
            return false;
        }

        return plan.ExactLength == 0 || startIndex <= input.Length - plan.ExactLength;
    }

    private static bool SatisfiesWindowRequirements(
        ReadOnlySpan<byte> input, in Utf8StructuralSearchPlan.Data plan, PreparedWindowMatch window)
    {
        var trailingEnd = window.Trailing.Index + window.Trailing.Length;
        if (plan.WindowSearch.MaxGap is > 0 and var maxSpan && trailingEnd - window.Leading.Index > maxSpan)
        {
            return false;
        }

        if (plan.MaxLines > 0 && !SatisfiesLineSpan(input, window.Leading.Index, window.Trailing.Index, plan.MaxLines))
        {
            return false;
        }

        return (plan.LeadingBoundary == Utf8BoundaryRequirement.None ||
                Utf8ScalarWordBoundary.MatchesRequirement(plan.LeadingBoundary, input, window.Leading.Index)) &&
               (plan.TrailingBoundary == Utf8BoundaryRequirement.None ||
                Utf8ScalarWordBoundary.MatchesRequirement(plan.TrailingBoundary, input, trailingEnd));
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

    private static int GetCandidateEnd(in Utf8StructuralSearchPlan.Data plan, int startIndex, int inputLength)
    {
        var available = inputLength - startIndex;
        if (plan.ExactLength > 0)
        {
            return plan.ExactLength <= available ? startIndex + plan.ExactLength : -1;
        }

        return plan.MaxLength > 0 ? startIndex + Math.Min(plan.MaxLength, available) : -1;
    }
}
