using System.Buffers;

namespace Lokad.Utf8Regex.Internal.Utilities;

internal enum PreparedLiteralSetStrategy : byte
{
    Empty = 0,
    UniqueAnchorByte = 1,
    SingleLiteralBuckets = 2,
    SingleBucketPrefix = 3,
    SingleBucketSecondByte = 4,
    MultiLiteralTrie = 5,
    Bucketed = 6,
}

internal readonly struct PreparedLiteralSetSearch
{
    private const int MultiLiteralThreshold = 8;

    public PreparedLiteralSetSearch(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        SearchData = AsciiSearch.CreateExactLiteralSearchData(literals);
        UniqueAnchor = CreateUniqueAnchor(SearchData);
        MultiLiteral = SearchData.ShortestLength == 0 || literals.Length < MultiLiteralThreshold
            ? default
            : PreparedMultiLiteralTrieSearch.Create(literals);
        Strategy = SelectStrategy(SearchData, UniqueAnchor);
    }

    public PreparedLiteralSetSearch(AsciiExactLiteralSearchData searchData)
    {
        SearchData = searchData;
        UniqueAnchor = CreateUniqueAnchor(searchData);
        MultiLiteral = SearchData.ShortestLength == 0 || LiteralCount(searchData.Buckets) < MultiLiteralThreshold
            ? default
            : PreparedMultiLiteralTrieSearch.Create(searchData.Buckets.SelectMany(static bucket => bucket.Literals).ToArray());
        Strategy = SelectStrategy(searchData, UniqueAnchor);
    }

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
            PreparedLiteralSetStrategy.SingleBucketSecondByte => IndexOfSingleBucketSecondByte(input, SearchData),
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
            PreparedLiteralSetStrategy.SingleBucketSecondByte => LastIndexOfSingleBucketSecondByte(input, SearchData),
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
            or PreparedLiteralSetStrategy.SingleLiteralBuckets
            or PreparedLiteralSetStrategy.SingleBucketSecondByte)
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
            var relative = input[offset..].IndexOfAny(searchData.FirstByteSearchValues);
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

    private static PreparedLiteralSetStrategy SelectStrategy(AsciiExactLiteralSearchData searchData, PreparedLiteralSetUniqueAnchor? uniqueAnchor)
    {
        if (searchData.ShortestLength == int.MaxValue)
        {
            return PreparedLiteralSetStrategy.Empty;
        }

        if (searchData.Buckets.Length == 1 && searchData.Buckets[0].CommonPrefix.Length > 1)
        {
            return PreparedLiteralSetStrategy.SingleBucketPrefix;
        }

        // For single-bucket families (shared first byte), prefer first-byte search via
        // Bucketed — IndexOf on 1 byte is faster than IndexOfAny on N second bytes when
        // the first byte is rarer than the combined second bytes.
        if (searchData.Buckets.Length == 1 && searchData.Buckets[0].SecondByteSearchValues is not null)
        {
            return PreparedLiteralSetStrategy.Bucketed;
        }

        if (AllBucketsContainSingleLiteral(searchData.Buckets))
        {
            return PreparedLiteralSetStrategy.SingleLiteralBuckets;
        }

        if (uniqueAnchor.HasValue)
        {
            return PreparedLiteralSetStrategy.UniqueAnchorByte;
        }

        if (LiteralCount(searchData.Buckets) >= MultiLiteralThreshold)
        {
            return PreparedLiteralSetStrategy.MultiLiteralTrie;
        }

        return PreparedLiteralSetStrategy.Bucketed;
    }

    private static PreparedLiteralSetUniqueAnchor? CreateUniqueAnchor(AsciiExactLiteralSearchData searchData)
    {
        if (searchData.ShortestLength <= 1)
        {
            return null;
        }

        var literals = searchData.Buckets.SelectMany(static bucket => bucket.Literals).ToArray();
        if (literals.Length <= 1)
        {
            return null;
        }

        Span<byte> anchorBytes = stackalloc byte[Math.Min(literals.Length, 256)];
        Span<bool> seen = stackalloc bool[256];
        for (var offset = 1; offset < searchData.ShortestLength; offset++)
        {
            seen.Clear();
            var count = 0;
            var duplicate = false;

            foreach (var literal in literals)
            {
                var value = literal[offset];
                if (seen[value])
                {
                    duplicate = true;
                    break;
                }

                seen[value] = true;
                anchorBytes[count++] = value;
            }

            if (!duplicate)
            {
                return new PreparedLiteralSetUniqueAnchor(offset, anchorBytes[..count].ToArray(), literals);
            }
        }

        return null;
    }

    private static bool AllBucketsContainSingleLiteral(AsciiExactLiteralBucket[] buckets)
    {
        if (buckets.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < buckets.Length; i++)
        {
            if (buckets[i].Literals.Length != 1)
            {
                return false;
            }
        }

        return true;
    }

    private static int LiteralCount(AsciiExactLiteralBucket[] buckets)
    {
        var count = 0;
        for (var i = 0; i < buckets.Length; i++)
        {
            count += buckets[i].Literals.Length;
        }

        return count;
    }

    private static int IndexOfSingleLiteralBuckets(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        var index = 0;
        while (index <= input.Length - searchData.ShortestLength)
        {
            var relative = input[index..].IndexOfAny(searchData.FirstByteSearchValues);
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

            var relative = input[anchorStart..].IndexOfAny(anchor.AnchorSearchValues);
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
            var relative = input[..end].LastIndexOfAny(searchData.FirstByteSearchValues);
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
            var relative = input[..searchEnd].LastIndexOfAny(anchor.AnchorSearchValues);
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
            var relative = input[index..].IndexOfAny(searchData.FirstByteSearchValues);
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
            var relative = input[..end].LastIndexOfAny(searchData.FirstByteSearchValues);
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
            var relative = input[..end].LastIndexOf(prefix);
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

    private static int IndexOfSingleBucketSecondByte(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        var bucket = searchData.Buckets[0];
        var secondByteSearchValues = bucket.SecondByteSearchValues;
        if (secondByteSearchValues is null)
        {
            return -1;
        }

        var index = 0;
        while (index <= input.Length - searchData.ShortestLength)
        {
            var secondByteStart = index + 1;
            if (secondByteStart >= input.Length)
            {
                return -1;
            }

            var relative = input[secondByteStart..].IndexOfAny(secondByteSearchValues);
            if (relative < 0)
            {
                return -1;
            }

            var candidate = index + relative;
            if (input[candidate] == bucket.FirstByte && MatchesBucketAt(input, candidate, bucket))
            {
                return candidate;
            }

            index = candidate + 1;
        }

        return -1;
    }

    private static int LastIndexOfSingleBucketSecondByte(ReadOnlySpan<byte> input, AsciiExactLiteralSearchData searchData)
    {
        var bucket = searchData.Buckets[0];
        var secondByteSearchValues = bucket.SecondByteSearchValues;
        if (secondByteSearchValues is null)
        {
            return -1;
        }

        for (var searchEnd = input.Length - searchData.ShortestLength + 2; searchEnd > 1;)
        {
            var relative = input[..searchEnd].LastIndexOfAny(secondByteSearchValues);
            if (relative < 1)
            {
                return -1;
            }

            var candidate = relative - 1;
            if (input[candidate] == bucket.FirstByte && MatchesBucketAt(input, candidate, bucket))
            {
                return candidate;
            }

            searchEnd = relative;
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

            var literal = bucket.PrefixDiscriminator.LiteralsByByte![input[index + offset]];
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

internal readonly struct PreparedLiteralSetUniqueAnchor
{
    public PreparedLiteralSetUniqueAnchor(int offset, byte[] anchorBytes, byte[][] literals)
    {
        Offset = offset;
        AnchorBytes = anchorBytes;
        AnchorSearchValues = SearchValues.Create(anchorBytes);
        LiteralByAnchorByte = CreateLiteralMap(offset, literals);
        ShortestLength = literals.Min(static literal => literal.Length);
    }

    public int Offset { get; }

    public byte[] AnchorBytes { get; }

    public SearchValues<byte> AnchorSearchValues { get; }

    public byte[][] LiteralByAnchorByte { get; }

    public int ShortestLength { get; }

    private static byte[][] CreateLiteralMap(int offset, byte[][] literals)
    {
        var map = new byte[256][];
        foreach (var literal in literals)
        {
            map[literal[offset]] = literal;
        }

        return map;
    }
}
