using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8StructuralSearchExecutor
{
    public static bool TryFindLastCandidate(
        this Utf8StructuralSearchPlan plan,
        ReadOnlySpan<byte> input,
        int endIndex,
        out PreparedSearchMatch candidate)
    {
        candidate = default;

        if ((uint)endIndex > (uint)input.Length)
        {
            endIndex = input.Length;
        }

        ref readonly var data = ref plan.Payload;
        return plan.HasValue && TryFindLastStartCandidate(input, in data, endIndex, out candidate);
    }

    public static bool TryFindNextCandidate(
        this Utf8StructuralSearchPlan plan,
        ReadOnlySpan<byte> input,
        ref PreparedSearchScanState searchState,
        out PreparedSearchMatch candidate)
    {
        candidate = default;
        if (!plan.HasValue)
        {
            return false;
        }

        ref readonly var data = ref plan.Payload;
        ref readonly var searcher = ref data.Searcher;
        var startTransform = data.StartTransform;
        while (true)
        {
            int rawStartIndex;
            int matchedLength;
            int literalId;
            if (searcher.HasValue)
            {
                if (!searcher.TryFindNextOverlappingMatch(input, ref searchState, out var match))
                {
                    return false;
                }

                rawStartIndex = match.Index;
                matchedLength = match.Length;
                literalId = match.LiteralId;
            }
            else
            {
                if (!TryFindNextFixedDistanceCandidate(input, data.FixedDistanceSets!, searchState.NextStart, out rawStartIndex))
                {
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
            if (!SatisfiesStartRequirements(input, in data, requirementBaseIndex, matchedLength))
            {
                continue;
            }

            candidate = new(startIndex, matchedLength, literalId);
            return true;
        }
    }

    private static bool TryFindLastStartCandidate(
        ReadOnlySpan<byte> input,
        in Utf8StructuralSearchPlan.Data plan,
        int endIndex,
        out PreparedSearchMatch candidate)
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
                        candidate = new(startIndex, match.Length, match.LiteralId);
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
            candidate = new(lastStart, 1, 0);
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
}
