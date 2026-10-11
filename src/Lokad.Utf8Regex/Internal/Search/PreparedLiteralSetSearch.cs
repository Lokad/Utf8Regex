
namespace Lokad.Utf8Regex.Internal.Search;

internal enum PreparedLiteralSetStrategy : byte
{
    Empty = 0,
    UniqueAnchorByte = 1,
    SingleLiteralBuckets = 2,
    SingleBucketPrefix = 3,
    MultiLiteralTrie = 5,
    Bucketed = 6,
}

internal readonly partial struct PreparedLiteralSetSearch
{
    private const int MultiLiteralThreshold = 8;

    public AsciiExactLiteralSearchData SearchData { get; }

    private PreparedLiteralSetUniqueAnchor? UniqueAnchor { get; }

    private PreparedMultiLiteralTrieSearch MultiLiteral { get; }

    public PreparedLiteralSetStrategy Strategy { get; }

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        if (SearchData.ShortestLength == int.MaxValue)
        {
            return -1;
        }

        if (SearchData.ShortestLength == 0)
        {
            return 0;
        }

        return Strategy switch
        {
            PreparedLiteralSetStrategy.UniqueAnchorByte when UniqueAnchor.HasValue => IndexOfUniqueAnchorByte(input, UniqueAnchor.Value),
            PreparedLiteralSetStrategy.SingleLiteralBuckets => IndexOfSingleLiteralBuckets(input, SearchData),
            PreparedLiteralSetStrategy.SingleBucketPrefix => IndexOfSingleBucketPrefix(input, SearchData),
            PreparedLiteralSetStrategy.MultiLiteralTrie => MultiLiteral.IndexOf(input),
            _ => IndexOfBucketed(input, SearchData),
        };
    }

    public int LastIndexOf(ReadOnlySpan<byte> input)
    {
        if (SearchData.ShortestLength == int.MaxValue)
        {
            return -1;
        }

        if (SearchData.ShortestLength == 0)
        {
            return input.Length;
        }

        return Strategy switch
        {
            PreparedLiteralSetStrategy.UniqueAnchorByte when UniqueAnchor.HasValue => LastIndexOfUniqueAnchorByte(input, UniqueAnchor.Value),
            PreparedLiteralSetStrategy.SingleLiteralBuckets => LastIndexOfSingleLiteralBuckets(input, SearchData),
            PreparedLiteralSetStrategy.SingleBucketPrefix => LastIndexOfSingleBucketPrefix(input, SearchData),
            PreparedLiteralSetStrategy.MultiLiteralTrie => MultiLiteral.LastIndexOf(input),
            _ => LastIndexOfBucketed(input, SearchData),
        };
    }

    public bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, out int matchedLength)
    {
        matchedLength = 0;
        if ((uint)index >= (uint)input.Length || SearchData.ShortestLength == int.MaxValue)
        {
            return false;
        }

        if (Strategy == PreparedLiteralSetStrategy.MultiLiteralTrie)
        {
            return MultiLiteral.TryGetMatchedLiteralLength(input, index, out matchedLength);
        }

        var bucketIndex = SearchData.BucketIndexMap[input[index]];
        if (bucketIndex < 0)
        {
            return false;
        }

        foreach (var literal in SearchData.Buckets[bucketIndex].Literals)
        {
            if (LiteralMatchesAt(input, index, literal))
            {
                matchedLength = literal.Length;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Fused IndexOf + TryGetMatchedLiteralLength for strategies that can combine
    /// position search and literal identification in a single pass.
    /// </summary>
    public bool TryFindFirstMatchWithLength(ReadOnlySpan<byte> input, out int index, out int matchedLength)
    {
        index = -1;
        matchedLength = 0;

        if (SearchData.ShortestLength == int.MaxValue || SearchData.ShortestLength == 0)
        {
            if (SearchData.ShortestLength == 0)
            {
                index = 0;
                return true;
            }

            return false;
        }

        if (Strategy == PreparedLiteralSetStrategy.SingleBucketPrefix)
        {
            return TryFindFirstMatchSingleBucketPrefix(input, SearchData, out index, out matchedLength);
        }

        if (Strategy is PreparedLiteralSetStrategy.Bucketed
            or PreparedLiteralSetStrategy.UniqueAnchorByte
            or PreparedLiteralSetStrategy.SingleLiteralBuckets)
        {
            return TryFindFirstMatchBucketed(input, SearchData, out index, out matchedLength);
        }

        index = IndexOf(input);
        return index >= 0 && TryGetMatchedLiteralLength(input, index, out matchedLength);
    }

    private static bool TryFindFirstMatchBucketed(
        ReadOnlySpan<byte> input,
        AsciiExactLiteralSearchData searchData,
        out int index,
        out int matchedLength)
    {
        index = -1;
        matchedLength = 0;
        var offset = 0;
        while (offset <= input.Length - searchData.ShortestLength)
        {
            var relative = searchData.FirstByteSearch.IndexOf(input[offset..]);
            if (relative < 0)
            {
                return false;
            }

            offset += relative;
            var bucketIndex = searchData.BucketIndexMap[input[offset]];
            if (bucketIndex >= 0)
            {
                var bucket = searchData.Buckets[bucketIndex];
                foreach (var literal in bucket.Literals)
                {
                    if (LiteralMatchesAt(input, offset, literal))
                    {
                        index = offset;
                        matchedLength = literal.Length;
                        return true;
                    }
                }
            }

            offset++;
        }

        return false;
    }

    private static bool TryFindFirstMatchSingleBucketPrefix(
        ReadOnlySpan<byte> input,
        AsciiExactLiteralSearchData searchData,
        out int index,
        out int matchedLength)
    {
        index = -1;
        matchedLength = 0;
        var bucket = searchData.Buckets[0];
        var prefix = bucket.CommonPrefix;
        var offset = 0;
        while (offset <= input.Length - searchData.ShortestLength)
        {
            var relative = input[offset..].IndexOf(prefix);
            if (relative < 0)
            {
                return false;
            }

            offset += relative;

            if (TryMatchSingleBucketPrefixCandidate(input, offset, bucket, out matchedLength))
            {
                index = offset;
                return true;
            }

            offset++;
        }

        return false;
    }

    private static int IndexOfSingleLiteralBuckets(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        var index = 0;
        while (index <= input.Length - searchData.ShortestLength)
        {
            var relative = searchData.FirstByteSearch.IndexOf(input[index..]);
            if (relative < 0)
            {
                return -1;
            }

            index += relative;
            var bucketIndex = searchData.BucketIndexMap[input[index]];
            if (bucketIndex >= 0 && LiteralMatchesAt(input, index, searchData.Buckets[bucketIndex].Literals[0]))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static int IndexOfUniqueAnchorByte(ReadOnlySpan<byte> input, PreparedLiteralSetUniqueAnchor anchor)
    {
        var searchIndex = 0;
        while (searchIndex <= input.Length - anchor.ShortestLength)
        {
            var anchorStart = searchIndex + anchor.Offset;
            if (anchorStart >= input.Length)
            {
                return -1;
            }

            var relative = anchor.AnchorSearch.IndexOf(input[anchorStart..]);
            if (relative < 0)
            {
                return -1;
            }

            var anchorIndex = anchorStart + relative;
            var candidate = anchorIndex - anchor.Offset;
            var literal = anchor.LiteralByAnchorByte[input[anchorIndex]];
            if (literal is not null && LiteralMatchesAt(input, candidate, literal))
            {
                return candidate;
            }

            searchIndex = candidate + 1;
        }

        return -1;
    }

    private static int LastIndexOfSingleLiteralBuckets(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        for (var end = input.Length - searchData.ShortestLength + 1; end > 0;)
        {
            var relative = searchData.FirstByteSearch.LastIndexOf(input[..end]);
            if (relative < 0)
            {
                return -1;
            }

            var bucketIndex = searchData.BucketIndexMap[input[relative]];
            if (bucketIndex >= 0 && LiteralMatchesAt(input, relative, searchData.Buckets[bucketIndex].Literals[0]))
            {
                return relative;
            }

            end = relative;
        }

        return -1;
    }

    private static int LastIndexOfUniqueAnchorByte(ReadOnlySpan<byte> input, PreparedLiteralSetUniqueAnchor anchor)
    {
        for (var searchEnd = input.Length - anchor.ShortestLength + anchor.Offset + 1; searchEnd > anchor.Offset;)
        {
            var relative = anchor.AnchorSearch.LastIndexOf(input[..searchEnd]);
            if (relative < anchor.Offset)
            {
                return -1;
            }

            var candidate = relative - anchor.Offset;
            var literal = anchor.LiteralByAnchorByte[input[relative]];
            if (literal is not null && LiteralMatchesAt(input, candidate, literal))
            {
                return candidate;
            }

            searchEnd = relative;
        }

        return -1;
    }

    private static int IndexOfBucketed(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        var index = 0;
        while (index <= input.Length - searchData.ShortestLength)
        {
            var relative = searchData.FirstByteSearch.IndexOf(input[index..]);
            if (relative < 0)
            {
                return -1;
            }

            index += relative;
            var bucketIndex = searchData.BucketIndexMap[input[index]];
            if (bucketIndex >= 0 && MatchesBucketAt(input, index, searchData.Buckets[bucketIndex]))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static int LastIndexOfBucketed(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        for (var end = input.Length - searchData.ShortestLength + 1; end > 0;)
        {
            var relative = searchData.FirstByteSearch.LastIndexOf(input[..end]);
            if (relative < 0)
            {
                return -1;
            }

            var bucketIndex = searchData.BucketIndexMap[input[relative]];
            if (bucketIndex >= 0 && MatchesBucketAt(input, relative, searchData.Buckets[bucketIndex]))
            {
                return relative;
            }

            end = relative;
        }

        return -1;
    }

    private static int IndexOfSingleBucketPrefix(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        var bucket = searchData.Buckets[0];
        var prefix = bucket.CommonPrefix;
        var index = 0;
        while (index <= input.Length - searchData.ShortestLength)
        {
            var relative = input[index..].IndexOf(prefix);
            if (relative < 0)
            {
                return -1;
            }

            index += relative;
            if (TryMatchSingleBucketPrefixCandidate(input, index, bucket, out _))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    private static int LastIndexOfSingleBucketPrefix(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        var bucket = searchData.Buckets[0];
        var prefix = bucket.CommonPrefix;
        for (var end = input.Length - searchData.ShortestLength + 1; end > 0;)
        {
            // end bounds candidate starts. Retain the complete prefix of the
            // last permitted start, including after a rejected candidate.
            var relative = input[..(end + prefix.Length - 1)].LastIndexOf(prefix);
            if (relative < 0)
            {
                return -1;
            }

            if (TryMatchSingleBucketPrefixCandidate(input, relative, bucket, out _))
            {
                return relative;
            }

            end = relative;
        }

        return -1;
    }

    private static bool MatchesBucketAt(ReadOnlySpan<byte> input, int index, AsciiExactLiteralBucket bucket)
    {
        if (bucket.Literals.Length == 1)
        {
            return LiteralMatchesAt(input, index, bucket.Literals[0]);
        }

        if (bucket.CommonPrefix.Length > 1)
        {
            if (index + bucket.CommonPrefix.Length > input.Length ||
                !input.Slice(index, bucket.CommonPrefix.Length).SequenceEqual(bucket.CommonPrefix))
            {
                return false;
            }
        }
        else if (bucket.SecondByteCandidates is { Length: > 0 } secondByteCandidates)
        {
            if (index + 1 >= input.Length || !ContainsByte(secondByteCandidates, input[index + 1]))
            {
                return false;
            }
        }

        foreach (var literal in bucket.Literals)
        {
            if (LiteralMatchesAt(input, index, literal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryMatchSingleBucketPrefixCandidate(
        ReadOnlySpan<byte> input,
        int index,
        AsciiExactLiteralBucket bucket,
        out int matchedLength)
    {
        matchedLength = 0;
        if (bucket.PrefixDiscriminator.HasValue)
        {
            var offset = bucket.PrefixDiscriminator.Offset;
            if (index + offset >= input.Length)
            {
                return false;
            }

            var literal = bucket.PrefixDiscriminator.LiteralsByByte[input[index + offset]];
            if (literal is not null && LiteralMatchesAtKnownPrefix(input, index, literal, offset + 1))
            {
                matchedLength = literal.Length;
                return true;
            }
        }

        foreach (var literal in bucket.Literals)
        {
            if (LiteralMatchesAtKnownPrefix(input, index, literal, bucket.CommonPrefix.Length))
            {
                matchedLength = literal.Length;
                return true;
            }
        }

        return false;
    }

    private static bool ContainsByte(ReadOnlySpan<byte> values, byte value)
    {
        for (var i = 0; i < values.Length; i++)
        {
            if (values[i] == value)
            {
                return true;
            }
        }

        return false;
    }

    private static bool LiteralMatchesAt(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal)
    {
        return LiteralMatchesAtKnownPrefix(input, index, literal, knownMatchedPrefixLength: 0);
    }

    private static bool LiteralMatchesAtKnownPrefix(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal, int knownMatchedPrefixLength)
    {
        if (index + literal.Length > input.Length)
        {
            return false;
        }

        if (knownMatchedPrefixLength < 0)
        {
            knownMatchedPrefixLength = 0;
        }

        if (knownMatchedPrefixLength > literal.Length)
        {
            knownMatchedPrefixLength = literal.Length;
        }

        if (literal.Length > 1 && input[index + 1] != literal[1])
        {
            return false;
        }

        if (literal.Length > 2 && input[index + literal.Length - 1] != literal[^1])
        {
            return false;
        }

        return knownMatchedPrefixLength == literal.Length ||
            input.Slice(index + knownMatchedPrefixLength, literal.Length - knownMatchedPrefixLength)
                .SequenceEqual(literal[knownMatchedPrefixLength..]);
    }
}

internal readonly partial struct PreparedLiteralSetUniqueAnchor
{
    public PreparedLiteralSetUniqueAnchor(int offset, PreparedByteSearch anchorSearch, byte[][] literalByAnchorByte, int shortestLength)
    { Offset = offset; AnchorSearch = anchorSearch; LiteralByAnchorByte = literalByAnchorByte; ShortestLength = shortestLength; }
    public int Offset { get; }
    public PreparedByteSearch AnchorSearch { get; }
    public byte[][] LiteralByAnchorByte { get; }
    public int ShortestLength { get; }
}
