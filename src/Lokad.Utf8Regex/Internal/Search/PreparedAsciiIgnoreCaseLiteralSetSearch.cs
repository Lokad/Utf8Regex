using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedAsciiIgnoreCaseLiteralSetSearch
{
    private const int PrefixProbeLength = 8;
    private const int CorrelatedPromotionTailLength = 256;
    private readonly PreparedAsciiIgnoreCaseLiteralSetSearchData? _data;

    public PreparedByteSearch FirstByteSearch => _data?.FirstByteSearch ?? PreparedByteSearch.FromOwnedValues([], null);

    public AsciiIgnoreCaseLiteralBucket[] Buckets => _data?.Buckets ?? [];

    public int[] BucketIndexMap => _data?.BucketIndexMap ?? [];

    public int ShortestLength => _data?.ShortestLength ?? int.MaxValue;

    private PreparedMultiLiteralPackedNibbleSimdPrefilter CorrelatedPrefilter => _data?.CorrelatedPrefilter ?? default;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int IndexOf(ReadOnlySpan<byte> input)
    {
        if (ShortestLength == int.MaxValue)
        {
            return -1;
        }

        if (ShortestLength == 0)
        {
            return 0;
        }

        var candidate = FirstByteSearch.IndexOf(input);
        if (candidate < 0 || candidate > input.Length - ShortestLength)
        {
            return -1;
        }

        if (TryGetMatchedLiteralLength(input, candidate, out _))
        {
            return candidate;
        }

        return TryFindAfterFailedCandidate(input, candidate + 1, out var index, out _)
            ? index
            : -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryFindFirstMatchWithLength(ReadOnlySpan<byte> input, out int index, out int matchedLength)
    {
        index = -1;
        matchedLength = 0;
        if (ShortestLength == int.MaxValue)
        {
            return false;
        }

        if (ShortestLength == 0)
        {
            index = 0;
            return true;
        }

        var candidate = FirstByteSearch.IndexOf(input);
        if (candidate < 0 || candidate > input.Length - ShortestLength)
        {
            return false;
        }

        if (TryGetMatchedLiteralLength(input, candidate, out matchedLength))
        {
            index = candidate;
            return true;
        }

        return TryFindAfterFailedCandidate(input, candidate + 1, out index, out matchedLength);
    }

    private bool TryFindAfterFailedCandidate(
        ReadOnlySpan<byte> input,
        int searchIndex,
        out int index,
        out int matchedLength)
    {
        index = -1;
        matchedLength = 0;
        var correlatedPrefilter = CorrelatedPrefilter;
        if (correlatedPrefilter.HasValue && input.Length - searchIndex >= CorrelatedPromotionTailLength)
        {
            var state = new PreparedMultiLiteralScanState(searchIndex, 0, 0);
            while (correlatedPrefilter.TryFindNextCandidate(input, ref state, out var candidate))
            {
                if (TryGetMatchedLiteralLength(input, candidate, out matchedLength))
                {
                    index = candidate;
                    return true;
                }
            }

            return false;
        }

        while (searchIndex <= input.Length - ShortestLength)
        {
            var relative = FirstByteSearch.IndexOf(input[searchIndex..]);
            if (relative < 0)
            {
                return false;
            }

            var candidate = searchIndex + relative;
            if (TryGetMatchedLiteralLength(input, candidate, out matchedLength))
            {
                index = candidate;
                return true;
            }

            searchIndex = candidate + 1;
        }

        return false;
    }

    public int LastIndexOf(ReadOnlySpan<byte> input)
    {
        if (ShortestLength == int.MaxValue)
        {
            return -1;
        }

        if (ShortestLength == 0)
        {
            return input.Length;
        }

        for (var end = input.Length - ShortestLength + 1; end > 0;)
        {
            var relative = FirstByteSearch.LastIndexOf(input[..end]);
            if (relative < 0)
            {
                return -1;
            }

            if (TryGetMatchedLiteralLength(input, relative, out _))
            {
                return relative;
            }

            end = relative;
        }

        return -1;
    }

    public bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, out int matchedLength)
    {
        matchedLength = 0;
        if ((uint)index >= (uint)input.Length || index > input.Length - ShortestLength)
        {
            return false;
        }

        var bucketIndex = BucketIndexMap[AsciiSearch.FoldCase(input[index])];
        if (bucketIndex < 0)
        {
            return false;
        }

        foreach (var literal in Buckets[bucketIndex].Literals)
        {
            if (literal.Length > input.Length - index)
            {
                continue;
            }

            var probeLength = literal.Length > PrefixProbeLength ? PrefixProbeLength : literal.Length;
            if (!AsciiSearch.MatchesFoldedIgnoreCase(input.Slice(index, probeLength), literal.AsSpan(0, probeLength)))
            {
                continue;
            }

            if (probeLength == literal.Length ||
                AsciiSearch.MatchesFoldedIgnoreCase(input.Slice(index + probeLength, literal.Length - probeLength), literal.AsSpan(probeLength)))
            {
                matchedLength = literal.Length;
                return true;
            }
        }

        return false;
    }

    public bool TryCountWithSelectiveCorrelatedPrefilter(ReadOnlySpan<byte> input, out int count)
    {
        count = 0;
        var prefilter = CorrelatedPrefilter;
        if (!prefilter.HasValue || prefilter.ShortestLength < 8)
        {
            return false;
        }

        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (prefilter.TryFindNextCandidate(input, ref state, out var candidate))
        {
            if (!TryGetMatchedLiteralLength(input, candidate, out var matchedLength))
            {
                continue;
            }

            count++;
            state = new PreparedMultiLiteralScanState(candidate + matchedLength, 0, 0);
        }

        return true;
    }

}

internal sealed class PreparedAsciiIgnoreCaseLiteralSetSearchData
{
    public PreparedAsciiIgnoreCaseLiteralSetSearchData(PreparedAsciiIgnoreCaseLiteralSetSearchDataState state)
    {
        FirstByteSearch = state.Search;
        Buckets = state.Buckets;
        BucketIndexMap = state.Map;
        ShortestLength = state.Shortest;
        CorrelatedPrefilter = state.Prefilter;
    }

    public PreparedByteSearch FirstByteSearch { get; }

    public AsciiIgnoreCaseLiteralBucket[] Buckets { get; }

    public int[] BucketIndexMap { get; }

    public int ShortestLength { get; }

    public PreparedMultiLiteralPackedNibbleSimdPrefilter CorrelatedPrefilter { get; }
}

internal readonly struct AsciiIgnoreCaseLiteralBucket
{
    public AsciiIgnoreCaseLiteralBucket(byte foldedFirstByte, byte[][] literals)
    {
        FoldedFirstByte = foldedFirstByte;
        Literals = literals;
    }

    public byte FoldedFirstByte { get; }

    public byte[][] Literals { get; }
}
