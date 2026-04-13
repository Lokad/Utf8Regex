using System.Buffers;
using System.Linq;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8AsciiFindExecutor
{
    public static bool TryFindNextAnchor(
        ReadOnlySpan<byte> input,
        PreparedAsciiFindPlan plan,
        int startIndex,
        out int anchorIndex,
        out int matchedLength)
    {
        anchorIndex = -1;
        matchedLength = 0;

        if (!plan.HasValue || (uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        return plan.Mode switch
        {
            PreparedAsciiFindMode.Literal or PreparedAsciiFindMode.LiteralAfterLoop
                => TryFindNextLiteralAnchor(input, plan.LiteralSearch, startIndex, out anchorIndex, out matchedLength),
            PreparedAsciiFindMode.LiteralFamily
                => TryFindNextLiteralFamilyAnchor(input, plan.LiteralFamilySearch, startIndex, out anchorIndex, out matchedLength),
            _ => false,
        };
    }

    public static bool TryFindNextFixedDistanceCandidate(
        ReadOnlySpan<byte> input,
        PreparedAsciiFindPlan plan,
        int startIndex,
        out int candidateIndex,
        out int matchedLength)
    {
        candidateIndex = -1;
        matchedLength = 0;

        if (!plan.HasValue || (uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        return plan.Mode switch
        {
            PreparedAsciiFindMode.FixedDistanceLiteral
                => TryFindNextFixedDistanceLiteralCandidate(input, plan, startIndex, out candidateIndex, out matchedLength),
            PreparedAsciiFindMode.FixedDistanceSet
                => TryFindNextFixedDistanceSetCandidate(input, plan, startIndex, out candidateIndex, out matchedLength),
            _ => false,
        };
    }

    private static bool TryFindNextLiteralAnchor(
        ReadOnlySpan<byte> input,
        PreparedSubstringSearch literalSearch,
        int startIndex,
        out int anchorIndex,
        out int matchedLength)
    {
        anchorIndex = -1;
        matchedLength = 0;

        if ((uint)startIndex > (uint)input.Length)
        {
            return false;
        }

        var relative = literalSearch.IndexOf(input[startIndex..]);
        if (relative < 0)
        {
            return false;
        }

        anchorIndex = startIndex + relative;
        matchedLength = literalSearch.Length;
        return true;
    }

    private static bool TryFindNextLiteralFamilyAnchor(
        ReadOnlySpan<byte> input,
        PreparedLiteralSetSearch literalFamilySearch,
        int startIndex,
        out int anchorIndex,
        out int matchedLength)
    {
        anchorIndex = -1;
        matchedLength = 0;

        if ((uint)startIndex > (uint)input.Length ||
            !literalFamilySearch.TryFindFirstMatchWithLength(input[startIndex..], out var relativeIndex, out matchedLength))
        {
            return false;
        }

        anchorIndex = startIndex + relativeIndex;
        return true;
    }

    private static bool TryFindNextFixedDistanceLiteralCandidate(
        ReadOnlySpan<byte> input,
        PreparedAsciiFindPlan plan,
        int startIndex,
        out int candidateIndex,
        out int matchedLength)
    {
        candidateIndex = -1;
        matchedLength = 0;

        var anchorStart = Math.Max(0, startIndex + plan.Distance);
        if (!TryFindNextLiteralAnchor(input, plan.LiteralSearch, anchorStart, out var anchorIndex, out matchedLength))
        {
            return false;
        }

        candidateIndex = anchorIndex - plan.Distance;
        return candidateIndex >= startIndex;
    }

    private static bool TryFindNextFixedDistanceSetCandidate(
        ReadOnlySpan<byte> input,
        PreparedAsciiFindPlan plan,
        int startIndex,
        out int candidateIndex,
        out int matchedLength)
    {
        candidateIndex = -1;
        matchedLength = 1;

        if (plan.FixedDistanceSets is not { Length: > 0 } sets)
        {
            return false;
        }

        var primary = sets[0];
        var anchorStart = Math.Max(0, startIndex + primary.Distance);
        var searchValues = GetSearchValues(primary);
        var position = anchorStart;
        while (position < input.Length)
        {
            var relative = input[position..].IndexOfAny(searchValues);
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

    private static SearchValues<byte> GetSearchValues(Utf8FixedDistanceSet set)
    {
        if (set.Chars is { Length: > 0 } chars)
        {
            return SearchValues.Create(chars);
        }

        if (set.HasRange)
        {
            var values = Enumerable.Range(set.RangeLow, set.RangeHigh - set.RangeLow + 1)
                .Select(static value => (byte)value)
                .ToArray();
            return SearchValues.Create(values);
        }

        return SearchValues.Create(new byte[] { 0 });
    }

    private static bool MatchesAllSets(ReadOnlySpan<byte> input, int startIndex, Utf8FixedDistanceSet[] sets)
    {
        foreach (var set in sets)
        {
            var index = startIndex + set.Distance;
            if ((uint)index >= (uint)input.Length || !MatchesSet(input[index], set))
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesSet(byte value, Utf8FixedDistanceSet set)
    {
        var inRange = set.HasRange && value >= set.RangeLow && value <= set.RangeHigh;
        var inChars = set.Chars is { Length: > 0 } chars && chars.AsSpan().Contains(value);
        var isMatch = inRange || inChars;
        return set.Negated ? !isMatch : isMatch;
    }
}
