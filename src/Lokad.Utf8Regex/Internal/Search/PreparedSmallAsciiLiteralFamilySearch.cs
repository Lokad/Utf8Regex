using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Search;

internal readonly partial struct PreparedSmallAsciiLiteralFamilySearch
{
    private readonly AsciiExactLiteralSearchData _searchData;
    private readonly int _anchorOffset;
    private readonly PreparedByteSearch _anchorSearch;
    private readonly Filter[] _filters;
    private readonly TripleDispatch _tripleDispatch;
    private readonly PairDispatch _pairDispatch;

    private readonly partial record struct Filter(int Offset, byte[] Values);

    private readonly partial record struct TripleDispatch(
        int PrimaryOffset,
        int SecondaryOffset,
        byte PrimaryValue0,
        byte SecondaryValue0,
        byte[] Literal0,
        byte PrimaryValue1,
        byte SecondaryValue1,
        byte[] Literal1,
        byte PrimaryValue2,
        byte SecondaryValue2,
        byte[] Literal2)
    {
        public bool HasValue => Literal0 is not null;
    }

    private readonly partial record struct PairDispatch(int PrimaryOffset, int SecondaryOffset, PairDispatchEntry[] Entries)
    {
        public bool HasValue => Entries is not null;
    }

    private readonly partial record struct PairDispatchEntry(byte PrimaryValue, byte SecondaryValue, byte[] Literal);

    private PreparedSmallAsciiLiteralFamilySearch(
        AsciiExactLiteralSearchData searchData,
        int anchorOffset,
        PreparedByteSearch anchorSearch,
        Filter[] filters,
        TripleDispatch tripleDispatch,
        PairDispatch pairDispatch)
    {
        _searchData = searchData;
        _anchorOffset = anchorOffset;
        _anchorSearch = anchorSearch;
        _filters = filters;
        _tripleDispatch = tripleDispatch;
        _pairDispatch = pairDispatch;
    }

    public int ShortestLength => _searchData.ShortestLength;

    public int Count(ReadOnlySpan<byte> input) => Count(input, preferFastMatch: true);

    public int CountScalar(ReadOnlySpan<byte> input) => Count(input, preferFastMatch: false);

    public bool TryFindFirst(ReadOnlySpan<byte> input, out int index, out int matchedLength)
    {
        index = -1;
        matchedLength = 0;
        var startIndex = 0;
        while (startIndex <= input.Length - ShortestLength)
        {
            var relative = _searchData.FirstByteSearch.IndexOf(input[startIndex..]);
            if (relative < 0)
            {
                return false;
            }

            var candidate = startIndex + relative;
            if (TryMatchAt(input, candidate, out matchedLength, preferFastMatch: true))
            {
                index = candidate;
                return true;
            }

            startIndex = candidate + 1;
        }

        return false;
    }

    public bool TryFindNextNonOverlapping(ReadOnlySpan<byte> input, ref int startIndex, out int index, out int matchedLength)
    {
        index = -1;
        matchedLength = 0;
        while (startIndex <= input.Length - ShortestLength)
        {
            var candidate = FindNextCandidate(input, startIndex);
            if (candidate < 0)
            {
                return false;
            }

            if (TryMatchAt(input, candidate, out matchedLength, preferFastMatch: true))
            {
                index = candidate;
                startIndex = candidate + matchedLength;
                return true;
            }

            startIndex = candidate + 1;
        }

        return false;
    }

    private int Count(ReadOnlySpan<byte> input, bool preferFastMatch)
    {
        var count = 0;
        var startIndex = 0;
        while (startIndex <= input.Length - ShortestLength)
        {
            var candidate = FindNextCandidate(input, startIndex);
            if (candidate < 0)
            {
                break;
            }

            if (TryMatchAt(input, candidate, out var matchedLength, preferFastMatch))
            {
                count++;
                startIndex = candidate + matchedLength;
            }
            else
            {
                startIndex = candidate + 1;
            }
        }

        return count;
    }

    private int FindNextCandidate(ReadOnlySpan<byte> input, int startIndex)
    {
        if ((uint)startIndex >= (uint)input.Length || input.Length - startIndex < ShortestLength)
        {
            return -1;
        }

        var maxStart = input.Length - ShortestLength;
        var maxAnchorIndex = maxStart + _anchorOffset;
        while (startIndex <= maxStart)
        {
            var anchorSearchIndex = startIndex + _anchorOffset;
            if (anchorSearchIndex > maxAnchorIndex)
            {
                return -1;
            }

            var relative = _anchorSearch.IndexOf(input[anchorSearchIndex..]);
            if (relative < 0)
            {
                return -1;
            }

            var candidate = anchorSearchIndex + relative - _anchorOffset;
            if (candidate > maxStart)
            {
                return -1;
            }

            var passes = true;
            for (var i = 0; i < _filters.Length; i++)
            {
                var filter = _filters[i];
                if (!filter.Values.AsSpan().Contains(input[candidate + filter.Offset]))
                {
                    passes = false;
                    break;
                }
            }

            if (passes)
            {
                return candidate;
            }

            startIndex = candidate + 1;
        }

        return -1;
    }

    private bool TryMatchAt(ReadOnlySpan<byte> input, int index, out int matchedLength, bool preferFastMatch)
    {
        matchedLength = 0;
        if (_tripleDispatch.HasValue &&
            TryMatchTripleDispatch(input, index, out matchedLength, preferFastMatch))
        {
            return true;
        }

        if (_pairDispatch.HasValue &&
            TryMatchPairDispatch(input, index, out matchedLength, preferFastMatch))
        {
            return true;
        }

        var bucketIndex = _searchData.BucketIndexMap[input[index]];
        if (bucketIndex < 0)
        {
            return false;
        }

        var bucket = _searchData.Buckets[bucketIndex];
        if (bucket.Literals.Length == 1)
        {
            var literal = bucket.Literals[0];
            if (LiteralMatchesAt(input, index, literal, preferFastMatch))
            {
                matchedLength = literal.Length;
                return true;
            }

            return false;
        }

        if (bucket.PrefixDiscriminator.HasValue)
        {
            var map = bucket.PrefixDiscriminator.LiteralsByByte;
            var discriminatorIndex = index + bucket.PrefixDiscriminator.Offset;
            if ((uint)discriminatorIndex < (uint)input.Length &&
                map[input[discriminatorIndex]] is { } literal &&
                // A discriminator selects a literal; it does not prove the
                // common prefix. Anchor/filter checks may cover only part of it.
                LiteralMatchesAt(input, index, literal, preferFastMatch))
            {
                matchedLength = literal.Length;
                return true;
            }
        }

        foreach (var literal in bucket.Literals)
        {
            if (LiteralMatchesAt(input, index, literal, preferFastMatch))
            {
                matchedLength = literal.Length;
                return true;
            }
        }

        return false;
    }

    private bool TryMatchTripleDispatch(ReadOnlySpan<byte> input, int index, out int matchedLength, bool preferFastMatch)
    {
        matchedLength = 0;
        var primaryIndex = index + _tripleDispatch.PrimaryOffset;
        var secondaryIndex = index + _tripleDispatch.SecondaryOffset;
        if ((uint)primaryIndex >= (uint)input.Length || (uint)secondaryIndex >= (uint)input.Length)
        {
            return false;
        }

        var primaryValue = input[primaryIndex];
        var secondaryValue = input[secondaryIndex];
        if (primaryValue == _tripleDispatch.PrimaryValue0 &&
            secondaryValue == _tripleDispatch.SecondaryValue0 &&
            LiteralMatchesAt(input, index, _tripleDispatch.Literal0, preferFastMatch))
        {
            matchedLength = _tripleDispatch.Literal0.Length;
            return true;
        }

        if (primaryValue == _tripleDispatch.PrimaryValue1 &&
            secondaryValue == _tripleDispatch.SecondaryValue1 &&
            LiteralMatchesAt(input, index, _tripleDispatch.Literal1, preferFastMatch))
        {
            matchedLength = _tripleDispatch.Literal1.Length;
            return true;
        }

        if (primaryValue == _tripleDispatch.PrimaryValue2 &&
            secondaryValue == _tripleDispatch.SecondaryValue2 &&
            LiteralMatchesAt(input, index, _tripleDispatch.Literal2, preferFastMatch))
        {
            matchedLength = _tripleDispatch.Literal2.Length;
            return true;
        }

        return false;
    }

    private bool TryMatchPairDispatch(ReadOnlySpan<byte> input, int index, out int matchedLength, bool preferFastMatch)
    {
        matchedLength = 0;
        var primaryIndex = index + _pairDispatch.PrimaryOffset;
        var secondaryIndex = index + _pairDispatch.SecondaryOffset;
        if ((uint)primaryIndex >= (uint)input.Length || (uint)secondaryIndex >= (uint)input.Length)
        {
            return false;
        }

        var primaryValue = input[primaryIndex];
        var secondaryValue = input[secondaryIndex];
        foreach (var entry in _pairDispatch.Entries)
        {
            if (entry.PrimaryValue == primaryValue &&
                entry.SecondaryValue == secondaryValue &&
                LiteralMatchesAt(input, index, entry.Literal, preferFastMatch))
            {
                matchedLength = entry.Literal.Length;
                return true;
            }
        }

        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LiteralMatchesAt(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal, bool preferFastMatch)
        => Utf8LiteralEquality.EqualsAt(
            input,
            index,
            literal,
            preferFastMatch ? Utf8LiteralComparisonKind.FastShort : Utf8LiteralComparisonKind.Scalar);

}
