using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Lokad.Utf8Regex.Internal.Utilities;

internal readonly struct PreparedSmallAsciiLiteralFamilySearch
{
    private readonly byte[][] _literals;
    private readonly AsciiExactLiteralSearchData _searchData;
    private readonly int _anchorOffset;
    private readonly SearchValues<byte> _anchorSearchValues;
    private readonly Filter[] _filters;
    private readonly TripleDispatch _tripleDispatch;
    private readonly PairDispatch _pairDispatch;

    private readonly record struct Filter(int Offset, byte[] Values, SearchValues<byte> SearchValues);

    private readonly record struct TripleDispatch(
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
        public bool HasValue => SecondaryOffset >= 0;
    }

    private readonly record struct PairDispatch(int PrimaryOffset, int SecondaryOffset, PairDispatchEntry[] Entries)
    {
        public bool HasValue => SecondaryOffset >= 0;
    }

    private readonly record struct PairDispatchEntry(byte PrimaryValue, byte SecondaryValue, byte[] Literal);

    private PreparedSmallAsciiLiteralFamilySearch(
        byte[][] literals,
        AsciiExactLiteralSearchData searchData,
        int anchorOffset,
        byte[] anchorValues,
        Filter[] filters,
        TripleDispatch tripleDispatch,
        PairDispatch pairDispatch)
    {
        _literals = literals;
        _searchData = searchData;
        _anchorOffset = anchorOffset;
        _anchorSearchValues = SearchValues.Create(anchorValues);
        _filters = filters;
        _tripleDispatch = tripleDispatch;
        _pairDispatch = pairDispatch;
    }

    public int ShortestLength => _searchData.ShortestLength;

    public static bool TryCreate(byte[][] literals, out PreparedSmallAsciiLiteralFamilySearch search)
    {
        search = default;
        if (literals is not { Length: >= 3 and <= 6 })
        {
            return false;
        }

        foreach (var literal in literals)
        {
            if (literal.Length < 2)
            {
                return false;
            }

            for (var i = 0; i < literal.Length; i++)
            {
                if (literal[i] >= 0x80)
                {
                    return false;
                }
            }
        }

        var searchData = AsciiSearch.CreateExactLiteralSearchData(literals);
        if (searchData.Buckets.Length is < 2 or > 3)
        {
            return false;
        }

        var (anchorOffset, anchorValues, filters) = CreateAnchorAndFilters(literals);
        search = new PreparedSmallAsciiLiteralFamilySearch(
            literals,
            searchData,
            anchorOffset,
            anchorValues,
            filters,
            CreateTripleDispatch(literals, anchorOffset),
            CreatePairDispatch(literals, anchorOffset));
        return true;
    }

    public int Count(ReadOnlySpan<byte> input) => Count(input, preferFastMatch: true);

    public int CountScalar(ReadOnlySpan<byte> input) => Count(input, preferFastMatch: false);

    public bool TryFindFirst(ReadOnlySpan<byte> input, out int index, out int matchedLength)
    {
        index = -1;
        matchedLength = 0;
        var startIndex = 0;
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

            var relative = input[anchorSearchIndex..].IndexOfAny(_anchorSearchValues);
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
                if (!filter.SearchValues.Contains(input[candidate + filter.Offset]))
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
            var map = bucket.PrefixDiscriminator.LiteralsByByte!;
            var discriminatorIndex = index + bucket.PrefixDiscriminator.Offset;
            if ((uint)discriminatorIndex < (uint)input.Length &&
                map[input[discriminatorIndex]] is { } literal &&
                LiteralMatchesAtKnownPrefix(input, index, literal, bucket.CommonPrefix.Length, preferFastMatch))
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

    private static (int AnchorOffset, byte[] AnchorValues, Filter[] Filters) CreateAnchorAndFilters(byte[][] literals)
    {
        var shortestLength = literals.Min(static literal => literal.Length);
        var offsets = new List<(int Offset, byte[] Values, int Score)>(shortestLength);
        var seenFlags = new bool[256];
        var valueBuffer = new byte[8];
        for (var offset = 0; offset < shortestLength; offset++)
        {
            Array.Clear(seenFlags);
            var count = 0;
            var overflow = false;
            foreach (var literal in literals)
            {
                var value = literal[offset];
                if (seenFlags[value])
                {
                    continue;
                }

                seenFlags[value] = true;
                if (count >= valueBuffer.Length)
                {
                    overflow = true;
                    break;
                }

                valueBuffer[count++] = value;
            }

            if (overflow || count is <= 1 or > 6)
            {
                continue;
            }

            var commonness = 0;
            for (var i = 0; i < count; i++)
            {
                commonness += PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(valueBuffer[i]);
            }

            offsets.Add((offset, valueBuffer[..count].ToArray(), commonness * 8 + count));
        }

        if (offsets.Count == 0)
        {
            return (0, GetFallbackAnchorValues(literals), []);
        }

        var ordered = offsets
            .OrderBy(static f => f.Score)
            .ThenByDescending(static f => f.Offset)
            .ToArray();

        var anchor = ordered[0];
        var filters = ordered
            .Skip(1)
            .Take(3)
            .Select(static f => new Filter(f.Offset, f.Values, SearchValues.Create(f.Values)))
            .ToArray();

        return (anchor.Offset, anchor.Values, filters);
    }

    private static byte[] GetFallbackAnchorValues(byte[][] literals)
    {
        var firstBytes = new byte[literals.Length];
        for (var i = 0; i < literals.Length; i++)
        {
            firstBytes[i] = literals[i][0];
        }

        Array.Sort(firstBytes);
        return firstBytes.Distinct().ToArray();
    }

    private static PairDispatch CreatePairDispatch(byte[][] literals, int primaryOffset)
    {
        if (literals.Length == 3)
        {
            return default;
        }

        var shortestLength = literals.Min(static literal => literal.Length);
        if (shortestLength < 2)
        {
            return default;
        }

        for (var secondaryOffset = 0; secondaryOffset < shortestLength; secondaryOffset++)
        {
            if (secondaryOffset == primaryOffset)
            {
                continue;
            }

            if (!HasUniquePairs(literals, primaryOffset, secondaryOffset))
            {
                continue;
            }

            var entries = new PairDispatchEntry[literals.Length];
            for (var i = 0; i < literals.Length; i++)
            {
                entries[i] = new PairDispatchEntry(
                    literals[i][primaryOffset],
                    literals[i][secondaryOffset],
                    literals[i]);
            }

            return new PairDispatch(primaryOffset, secondaryOffset, entries);
        }

        return default;
    }

    private static TripleDispatch CreateTripleDispatch(byte[][] literals, int primaryOffset)
    {
        if (literals.Length != 3)
        {
            return default;
        }

        var shortestLength = literals.Min(static literal => literal.Length);
        if (shortestLength < 2)
        {
            return default;
        }

        for (var secondaryOffset = 0; secondaryOffset < shortestLength; secondaryOffset++)
        {
            if (secondaryOffset == primaryOffset || !HasUniquePairs(literals, primaryOffset, secondaryOffset))
            {
                continue;
            }

            return new TripleDispatch(
                primaryOffset,
                secondaryOffset,
                literals[0][primaryOffset], literals[0][secondaryOffset], literals[0],
                literals[1][primaryOffset], literals[1][secondaryOffset], literals[1],
                literals[2][primaryOffset], literals[2][secondaryOffset], literals[2]);
        }

        return default;
    }

    private static bool HasUniquePairs(byte[][] literals, int primaryOffset, int secondaryOffset)
    {
        for (var i = 0; i < literals.Length; i++)
        {
            for (var j = i + 1; j < literals.Length; j++)
            {
                if (literals[i][primaryOffset] == literals[j][primaryOffset] &&
                    literals[i][secondaryOffset] == literals[j][secondaryOffset])
                {
                    return false;
                }
            }
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LiteralMatchesAt(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal, bool preferFastMatch)
    {
        return (uint)index <= (uint)(input.Length - literal.Length) &&
            (preferFastMatch
                ? FastLiteralEquals(input.Slice(index, literal.Length), literal)
                : input.Slice(index, literal.Length).SequenceEqual(literal));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LiteralMatchesAtKnownPrefix(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal, int knownPrefixLength, bool preferFastMatch)
    {
        return (uint)index <= (uint)(input.Length - literal.Length) &&
            (preferFastMatch
                ? FastLiteralEquals(
                    input.Slice(index + knownPrefixLength, literal.Length - knownPrefixLength),
                    literal[knownPrefixLength..])
                : input.Slice(index + knownPrefixLength, literal.Length - knownPrefixLength)
                    .SequenceEqual(literal[knownPrefixLength..]));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool FastLiteralEquals(ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> literal)
    {
        ref var candidateRef = ref MemoryMarshal.GetReference(candidate);
        ref var literalRef = ref MemoryMarshal.GetReference(literal);
        switch (literal.Length)
        {
            case 5:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) == Unsafe.ReadUnaligned<uint>(ref literalRef) &&
                       Unsafe.Add(ref candidateRef, 4) == Unsafe.Add(ref literalRef, 4);
            case 6:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) == Unsafe.ReadUnaligned<uint>(ref literalRef) &&
                       Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref candidateRef, 4)) ==
                       Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref literalRef, 4));
            case 7:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) == Unsafe.ReadUnaligned<uint>(ref literalRef) &&
                       Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref candidateRef, 4)) ==
                       Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref literalRef, 4)) &&
                       Unsafe.Add(ref candidateRef, 6) == Unsafe.Add(ref literalRef, 6);
        }

        if (literal.Length >= 8)
        {
            if (Unsafe.ReadUnaligned<ulong>(ref candidateRef) != Unsafe.ReadUnaligned<ulong>(ref literalRef))
            {
                return false;
            }

            var tailOffset = literal.Length - sizeof(ulong);
            return Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref candidateRef, tailOffset)) ==
                   Unsafe.ReadUnaligned<ulong>(ref Unsafe.Add(ref literalRef, tailOffset));
        }

        for (var i = 0; i < literal.Length; i++)
        {
            if (candidate[i] != literal[i])
            {
                return false;
            }
        }

        return true;
    }
}
