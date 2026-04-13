using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Utilities;

internal readonly struct PreparedAsciiIgnoreCaseLiteralSetSearch
{
    private const int PrefixProbeLength = 8;

    public PreparedAsciiIgnoreCaseLiteralSetSearch(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);

        if (literals.Length == 0)
        {
            FirstByteSearchValues = SearchValues.Create(Array.Empty<byte>());
            Buckets = [];
            BucketIndexMap = CreateBucketIndexMap([]);
            ShortestLength = int.MaxValue;
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
                FirstByteSearchValues = SearchValues.Create(Array.Empty<byte>());
                Buckets = [new AsciiIgnoreCaseLiteralBucket(0, [Array.Empty<byte>()])];
                BucketIndexMap = CreateBucketIndexMap(Buckets);
                ShortestLength = 0;
                return;
            }

            var normalized = literal.ToArray();
            for (var i = 0; i < normalized.Length; i++)
            {
                normalized[i] = AsciiSearch.FoldCase(normalized[i]);
            }

            shortestLength = Math.Min(shortestLength, normalized.Length);
            var firstByte = normalized[0];
            if (bucketLists[firstByte] is null)
            {
                bucketLists[firstByte] = [];
                distinctFoldedFirstBytes[distinctCount++] = firstByte;
            }

            bucketLists[firstByte]!.Add(normalized);
        }

        var searchBytes = BuildSearchBytes(distinctFoldedFirstBytes[..distinctCount]);
        var buckets = new AsciiIgnoreCaseLiteralBucket[distinctCount];
        for (var i = 0; i < distinctCount; i++)
        {
            var firstByte = distinctFoldedFirstBytes[i];
            buckets[i] = new AsciiIgnoreCaseLiteralBucket(firstByte, [.. bucketLists[firstByte]!]);
        }

        FirstByteSearchValues = SearchValues.Create(searchBytes);
        Buckets = buckets;
        BucketIndexMap = CreateBucketIndexMap(buckets);
        ShortestLength = shortestLength;
    }

    public SearchValues<byte> FirstByteSearchValues { get; }

    public AsciiIgnoreCaseLiteralBucket[] Buckets { get; }

    public int[] BucketIndexMap { get; }

    public int ShortestLength { get; }

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

        var index = 0;
        while (index <= input.Length - ShortestLength)
        {
            var relative = input[index..].IndexOfAny(FirstByteSearchValues);
            if (relative < 0)
            {
                return -1;
            }

            index += relative;
            if (TryGetMatchedLiteralLength(input, index, out _))
            {
                return index;
            }

            index++;
        }

        return -1;
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
