using System.Buffers;
using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly struct PreparedAsciiIgnoreCaseLiteralSetSearch
{
    private const int PrefixProbeLength = 8;
    private const int CorrelatedPromotionTailLength = 256;
    private readonly PreparedAsciiIgnoreCaseLiteralSetSearchData _data;

    public PreparedAsciiIgnoreCaseLiteralSetSearch(byte[][] literals)
    {
        if (literals.Length == 0)
        {
            var emptyBuckets = Array.Empty<AsciiIgnoreCaseLiteralBucket>();
            _data = new PreparedAsciiIgnoreCaseLiteralSetSearchData(
                SearchValues.Create(Array.Empty<byte>()),
                emptyBuckets,
                CreateBucketIndexMap(emptyBuckets),
                int.MaxValue,
                default);
            return;
        }

        var shortestLength = int.MaxValue;
        Span<byte> distinctFoldedFirstBytes = stackalloc byte[Math.Min(literals.Length, 256)];
        var distinctCount = 0;
        var bucketLists = new List<byte[]>[256];

        foreach (var literal in literals)
        {
            if (literal.Length == 0)
            {
                AsciiIgnoreCaseLiteralBucket[] emptyLiteralBuckets =
                    [new AsciiIgnoreCaseLiteralBucket(0, [Array.Empty<byte>()])];
                _data = new PreparedAsciiIgnoreCaseLiteralSetSearchData(
                    SearchValues.Create(Array.Empty<byte>()),
                    emptyLiteralBuckets,
                    CreateBucketIndexMap(emptyLiteralBuckets),
                    0,
                    default);
                return;
            }

            var normalized = literal.ToArray();
            for (var i = 0; i < normalized.Length; i++)
            {
                normalized[i] = AsciiSearch.FoldCase(normalized[i]);
            }

            shortestLength = Math.Min(shortestLength, normalized.Length);
            var firstByte = normalized[0];
            var bucket = bucketLists[firstByte];
            if (bucket is null)
            {
                bucket = [];
                bucketLists[firstByte] = bucket;
                distinctFoldedFirstBytes[distinctCount++] = firstByte;
            }

            bucket.Add(normalized);
        }

        var searchBytes = BuildSearchBytes(distinctFoldedFirstBytes[..distinctCount]);
        var buckets = new AsciiIgnoreCaseLiteralBucket[distinctCount];
        for (var i = 0; i < distinctCount; i++)
        {
            var firstByte = distinctFoldedFirstBytes[i];
            var bucket = bucketLists[firstByte] ??
                throw new InvalidOperationException("A recorded literal bucket must have storage.");
            buckets[i] = new AsciiIgnoreCaseLiteralBucket(firstByte, [.. bucket]);
        }

        var correlatedPrefilter = PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateAsciiIgnoreCase(
            buckets.SelectMany(static bucket => bucket.Literals).ToArray());
        _data = new PreparedAsciiIgnoreCaseLiteralSetSearchData(
            SearchValues.Create(searchBytes),
            buckets,
            CreateBucketIndexMap(buckets),
            shortestLength,
            correlatedPrefilter);
    }

    public SearchValues<byte> FirstByteSearchValues => _data.FirstByteSearchValues;

    public AsciiIgnoreCaseLiteralBucket[] Buckets => _data.Buckets;

    public int[] BucketIndexMap => _data.BucketIndexMap;

    public int ShortestLength => _data.ShortestLength;

    private PreparedMultiLiteralPackedNibbleSimdPrefilter CorrelatedPrefilter => _data.CorrelatedPrefilter;

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

        var candidate = input.IndexOfAny(FirstByteSearchValues);
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

        var candidate = input.IndexOfAny(FirstByteSearchValues);
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
            var relative = input[searchIndex..].IndexOfAny(FirstByteSearchValues);
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
            var relative = input[..end].LastIndexOfAny(FirstByteSearchValues);
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

    private static byte[] BuildSearchBytes(ReadOnlySpan<byte> foldedFirstBytes)
    {
        Span<bool> seen = stackalloc bool[256];
        Span<byte> values = stackalloc byte[Math.Min(foldedFirstBytes.Length * 2, 256)];
        var count = 0;
        foreach (var folded in foldedFirstBytes)
        {
            if (!seen[folded])
            {
                seen[folded] = true;
                values[count++] = folded;
            }

            if ((uint)(folded - (byte)'a') <= 'z' - 'a')
            {
                var upper = (byte)(folded - 32);
                if (!seen[upper])
                {
                    seen[upper] = true;
                    values[count++] = upper;
                }
            }
        }

        return values[..count].ToArray();
    }

    private static int[] CreateBucketIndexMap(AsciiIgnoreCaseLiteralBucket[] buckets)
    {
        var map = new int[256];
        Array.Fill(map, -1);
        for (var i = 0; i < buckets.Length; i++)
        {
            map[buckets[i].FoldedFirstByte] = i;
        }

        return map;
    }
}

internal sealed class PreparedAsciiIgnoreCaseLiteralSetSearchData
{
    public PreparedAsciiIgnoreCaseLiteralSetSearchData(
        SearchValues<byte> firstByteSearchValues,
        AsciiIgnoreCaseLiteralBucket[] buckets,
        int[] bucketIndexMap,
        int shortestLength,
        PreparedMultiLiteralPackedNibbleSimdPrefilter correlatedPrefilter)
    {
        FirstByteSearchValues = firstByteSearchValues;
        Buckets = buckets;
        BucketIndexMap = bucketIndexMap;
        ShortestLength = shortestLength;
        CorrelatedPrefilter = correlatedPrefilter;
    }

    public SearchValues<byte> FirstByteSearchValues { get; }

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
