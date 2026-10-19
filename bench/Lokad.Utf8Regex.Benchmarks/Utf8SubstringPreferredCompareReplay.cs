using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Benchmarks;

// Compare-index alternatives are benchmark experiments. Shared search/verification
// stays in production; this replay adds no shipped selector, executor or state.
internal static class Utf8SubstringPreferredCompareReplay
{
    internal static int CountIgnoreCaseWithPreferredCompareIndex(this in PreparedSubstringSearch search, ReadOnlySpan<byte> input, int preferredCompareIndex, out int candidateCount, out int verifyCount)
    {
        candidateCount = 0;
        verifyCount = 0;

        if (!search.IgnoreCase)
        {
            return search.CountWithMetrics(input, out candidateCount, out verifyCount);
        }

        if (search.Needle.Length is >= 9 and <= 16 &&
            preferredCompareIndex > 0 &&
            search._foldedNeedle is { } foldedNeedle &&
            AsciiSearch.TryGetCaseVariants(foldedNeedle[preferredCompareIndex], out _, out _))
        {
            return CountIgnoreCasePreferredShortWithMetrics(search, input, foldedNeedle, preferredCompareIndex, ref candidateCount, ref verifyCount);
        }

        if (search.Needle.Length <= 10)
        {
            return search.CountWithMetrics(input, out candidateCount, out verifyCount);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - search.Length)
        {
            var found = search.IndexOfIgnoreCaseWithMetrics(input[index..], preferredCompareIndex, ref candidateCount, ref verifyCount);
            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + search.Length;
        }

        return count;
    }

    private static int CountIgnoreCasePreferredShortWithMetrics(
        in PreparedSubstringSearch search,
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> foldedNeedle,
        int preferredCompareIndex,
        ref int candidateCount,
        ref int verifyCount)
    {
        var count = 0;
        var index = 0;
        var probe = foldedNeedle[preferredCompareIndex];
        if (!AsciiSearch.TryGetCaseVariants(probe, out var lower, out var upper))
        {
            lower = probe;
            upper = probe;
        }

        while (index <= input.Length - search.Length)
        {
            var searchSlice = input[(index + preferredCompareIndex)..];
            var relative = lower == upper
                ? searchSlice.IndexOf(lower)
                : searchSlice.IndexOfAny(lower, upper);
            if (relative < 0)
            {
                return count;
            }

            var found = index + preferredCompareIndex + relative;
            var candidate = found - preferredCompareIndex;
            if (candidate <= input.Length - search.Length)
            {
                candidateCount++;
                if (PreparedSubstringSearch.MatchesFoldedTailPreferred(input, candidate, foldedNeedle, preferredCompareIndex))
                {
                    verifyCount++;
                    count++;
                    index = candidate + search.Length;
                    continue;
                }

                verifyCount++;
            }

            // The anchor may be inside a later valid match. Advance the
            // candidate start, so rejecting it cannot skip those starts.
            index = candidate + 1;
        }

        return count;
    }

}
