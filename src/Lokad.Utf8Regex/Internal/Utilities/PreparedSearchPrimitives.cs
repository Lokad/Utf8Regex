using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
#endif

namespace Lokad.Utf8Regex.Internal.Utilities;

internal enum PreparedIgnoreCaseSearchTier : byte
{
    None = 0,
    ShortLiteral = 1,
    VectorAnchored = 2,
    PackedPair = 3,
}

internal enum PreparedExactSearchTier : byte
{
    None = 0,
    ShortFirstByte = 1,
    MediumLastByte = 2,
    SpanIndexOf = 3,
}

internal readonly record struct PreparedIgnoreCasePackedPair(byte FoldedByte1, int Index1, byte FoldedByte2, int Index2);

internal readonly struct PreparedByteSearch
{
    private readonly byte[] _values;

    private PreparedByteSearch(byte[] values)
    {
        _values = values;
        SearchValues = values.Length > 3 ? System.Buffers.SearchValues.Create(values) : null;
    }

    public int Count => _values?.Length ?? 0;

    public ReadOnlySpan<byte> Values => _values ?? [];

    public SearchValues<byte>? SearchValues { get; }

    public static PreparedByteSearch Create(params byte[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return new PreparedByteSearch([.. values]);
    }

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        return _values.Length switch
        {
            0 => -1,
            1 => input.IndexOf(_values[0]),
            2 => input.IndexOfAny(_values[0], _values[1]),
            3 => input.IndexOfAny(_values[0], _values[1], _values[2]),
            _ => input.IndexOfAny(SearchValues!),
        };
    }

    public int LastIndexOf(ReadOnlySpan<byte> input)
    {
        return _values.Length switch
        {
            0 => -1,
            1 => input.LastIndexOf(_values[0]),
            2 => input.LastIndexOfAny(_values[0], _values[1]),
            3 => input.LastIndexOfAny(_values[0], _values[1], _values[2]),
            _ => input.LastIndexOfAny(SearchValues!),
        };
    }
}

internal readonly struct PreparedSubstringSearch
{
    private readonly byte[]? _foldedNeedle;
    private readonly int[]? _ignoreCaseShiftTable;
    private readonly int _ignoreCasePreferredCompareIndex;
    private readonly PreparedIgnoreCasePackedPair _ignoreCasePackedPair;
    private readonly PreparedExactSearchTier _exactTier;
    private readonly AsciiAnchorSelection _exactAnchors;

    public PreparedSubstringSearch(byte[] needle, bool ignoreCase)
    {
        ArgumentNullException.ThrowIfNull(needle);
        Needle = needle;
        IgnoreCase = ignoreCase;
        _exactTier = !ignoreCase ? DetermineExactTier(needle.Length) : PreparedExactSearchTier.None;
        _exactAnchors = !ignoreCase &&
                        needle.Length is >= 4 and <= 16 &&
                        AsciiSearch.TryGetDotNetLikeAsciiAnchorSelection(needle, ignoreCase: false, out var exactAnchors)
            ? exactAnchors
            : default;
        var foldedNeedle = ignoreCase ? CreateFoldedNeedle(needle) : null;
        _foldedNeedle = foldedNeedle;
        _ignoreCaseShiftTable = ignoreCase && needle.Length >= 3 && foldedNeedle is not null
            ? CreateIgnoreCaseShiftTable(foldedNeedle)
            : null;
        _ignoreCasePreferredCompareIndex = ignoreCase && needle.Length > 10 && foldedNeedle is not null
            ? GetPreferredCompareIndex(foldedNeedle, foldedNeedle[0], foldedNeedle.Length - 1)
            : -1;
        if (ignoreCase && needle.Length > 10 && foldedNeedle is not null && TryCreateIgnoreCasePackedPair(foldedNeedle, out var packedPair))
        {
            _ignoreCasePackedPair = packedPair;
        }
        else
        {
            _ignoreCasePackedPair = default;
        }

        IgnoreCaseTier = DetermineIgnoreCaseTier();
    }

    public byte[] Needle { get; }

    public bool IgnoreCase { get; }

    public int Length => Needle.Length;

    public PreparedIgnoreCaseSearchTier IgnoreCaseTier { get; }

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        return IgnoreCase
            ? IndexOfIgnoreCase(input)
            : IndexOfExactPrepared(input);
    }

    public int LastIndexOf(ReadOnlySpan<byte> input)
    {
        return IgnoreCase
            ? LastIndexOfIgnoreCase(input)
            : LastIndexOfExactPrepared(input);
    }

    internal int CountWithMetrics(ReadOnlySpan<byte> input, out int candidateCount, out int verifyCount)
    {
        candidateCount = 0;
        verifyCount = 0;

        var count = 0;
        var index = 0;
        while (index <= input.Length - Length)
        {
            var found = IgnoreCase
                ? IndexOfIgnoreCaseWithMetrics(input[index..], ref candidateCount, ref verifyCount)
                : _exactAnchors.Anchor2 > 0
                    ? IndexOfExactPrepared(input[index..])
                    : AsciiSearch.IndexOfExact(input[index..], Needle);
            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + Length;
        }

        return count;
    }

    internal int CountIgnoreCaseWithPreferredCompareIndex(ReadOnlySpan<byte> input, int preferredCompareIndex, out int candidateCount, out int verifyCount)
    {
        candidateCount = 0;
        verifyCount = 0;

        if (!IgnoreCase)
        {
            return CountWithMetrics(input, out candidateCount, out verifyCount);
        }

        if (Needle.Length is >= 9 and <= 16 &&
            preferredCompareIndex > 0 &&
            _foldedNeedle is { } foldedNeedle &&
            TryGetCaseVariants(foldedNeedle[preferredCompareIndex], out _, out _))
        {
            return CountIgnoreCasePreferredShortWithMetrics(input, foldedNeedle, preferredCompareIndex, ref candidateCount, ref verifyCount);
        }

        if (Needle.Length <= 10)
        {
            return CountWithMetrics(input, out candidateCount, out verifyCount);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - Length)
        {
            var found = IndexOfIgnoreCaseWithMetrics(input[index..], preferredCompareIndex, ref candidateCount, ref verifyCount);
            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + Length;
        }

        return count;
    }

    private int CountIgnoreCasePreferredShortWithMetrics(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> foldedNeedle,
        int preferredCompareIndex,
        ref int candidateCount,
        ref int verifyCount)
    {
        var count = 0;
        var index = 0;
        var probe = foldedNeedle[preferredCompareIndex];
        if (!TryGetCaseVariants(probe, out var lower, out var upper))
        {
            lower = probe;
            upper = probe;
        }

        while (index <= input.Length - Length)
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
            if (candidate <= input.Length - Length)
            {
                candidateCount++;
                if (MatchesFoldedTailPreferred(input, candidate, foldedNeedle, preferredCompareIndex))
                {
                    verifyCount++;
                    count++;
                    index = candidate + Length;
                    continue;
                }

                verifyCount++;
            }

            index = found + 1;
        }

        return count;
    }

    internal int CountIgnoreCaseWithTier(ReadOnlySpan<byte> input, PreparedIgnoreCaseSearchTier tier, out int candidateCount, out int verifyCount)
    {
        candidateCount = 0;
        verifyCount = 0;

        if (!IgnoreCase)
        {
            return CountWithMetrics(input, out candidateCount, out verifyCount);
        }

        if (tier == PreparedIgnoreCaseSearchTier.None)
        {
            tier = IgnoreCaseTier;
        }

        if (tier == PreparedIgnoreCaseSearchTier.ShortLiteral || Needle.Length <= 10)
        {
            return CountWithMetrics(input, out candidateCount, out verifyCount);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - Length)
        {
            int found;
            switch (tier)
            {
                case PreparedIgnoreCaseSearchTier.PackedPair:
                    found = IndexOfIgnoreCasePackedPairWithMetrics(input[index..], ref candidateCount, ref verifyCount);
                    break;
                default:
                    found = IndexOfIgnoreCaseWithMetrics(input[index..], _ignoreCasePreferredCompareIndex, ref candidateCount, ref verifyCount);
                    break;
            }

            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + Length;
        }

        return count;
    }

    internal int GetIgnoreCasePreferredCompareIndex()
    {
        if (!IgnoreCase || Needle.Length <= 8)
        {
            return -1;
        }

        return _ignoreCasePreferredCompareIndex;
    }

    private static PreparedExactSearchTier DetermineExactTier(int length)
    {
        return length switch
        {
            <= 2 => PreparedExactSearchTier.None,
            <= 8 => PreparedExactSearchTier.ShortFirstByte,
            <= 32 => PreparedExactSearchTier.MediumLastByte,
            _ => PreparedExactSearchTier.SpanIndexOf,
        };
    }

    private int IndexOfExactPrepared(ReadOnlySpan<byte> input)
    {
        if (_exactAnchors.Anchor2 > 0)
        {
            return IndexOfExactAnchored(input);
        }

        return _exactTier switch
        {
            PreparedExactSearchTier.ShortFirstByte => IndexOfExactFirstByteThenTail(input),
            PreparedExactSearchTier.MediumLastByte => IndexOfExactLastByteThenHead(input),
            PreparedExactSearchTier.SpanIndexOf => input.IndexOf(Needle),
            _ => AsciiSearch.IndexOfExact(input, Needle),
        };
    }

    private int LastIndexOfExactPrepared(ReadOnlySpan<byte> input)
    {
        if (_exactAnchors.Anchor2 > 0)
        {
            return LastIndexOfExactAnchored(input);
        }

        return _exactTier switch
        {
            PreparedExactSearchTier.ShortFirstByte => LastIndexOfExactFirstByteThenTail(input),
            PreparedExactSearchTier.MediumLastByte => LastIndexOfExactLastByteThenHead(input),
            PreparedExactSearchTier.SpanIndexOf => input.LastIndexOf(Needle),
            _ => AsciiSearch.LastIndexOfExact(input, Needle),
        };
    }

    private int IndexOfExactAnchored(ReadOnlySpan<byte> input)
    {
        if (input.Length < Needle.Length)
        {
            return -1;
        }

        var anchor2 = _exactAnchors.Anchor2;
        var anchor2Byte = Needle[anchor2];
        var anchor3 = _exactAnchors.Anchor3;
        var maxStart = input.Length - Needle.Length;
        var searchFrom = anchor2;

        while (searchFrom < input.Length)
        {
            var relative = input[searchFrom..].IndexOf(anchor2Byte);
            if (relative < 0)
            {
                return -1;
            }

            var anchorPosition = searchFrom + relative;
            var candidate = anchorPosition - anchor2;
            searchFrom = anchorPosition + 1;

            if ((uint)candidate > (uint)maxStart)
            {
                continue;
            }

            if (anchor3 > 0 && input[candidate + anchor3] != Needle[anchor3])
            {
                continue;
            }

            if (input.Slice(candidate, Needle.Length).SequenceEqual(Needle))
            {
                return candidate;
            }
        }

        return -1;
    }

    private int LastIndexOfExactAnchored(ReadOnlySpan<byte> input)
    {
        if (input.Length < Needle.Length)
        {
            return -1;
        }

        var anchor2 = _exactAnchors.Anchor2;
        var anchor2Byte = Needle[anchor2];
        var anchor3 = _exactAnchors.Anchor3;
        var maxStart = input.Length - Needle.Length;
        var maxAnchorPosition = maxStart + anchor2;
        var endExclusive = maxAnchorPosition + 1;

        while (endExclusive > anchor2)
        {
            var anchorPosition = input[..endExclusive].LastIndexOf(anchor2Byte);
            if (anchorPosition < 0)
            {
                return -1;
            }

            var candidate = anchorPosition - anchor2;
            endExclusive = anchorPosition;

            if ((uint)candidate > (uint)maxStart)
            {
                continue;
            }

            if (anchor3 > 0 && input[candidate + anchor3] != Needle[anchor3])
            {
                continue;
            }

            if (input.Slice(candidate, Needle.Length).SequenceEqual(Needle))
            {
                return candidate;
            }
        }

        return -1;
    }

    private int IndexOfExactFirstByteThenTail(ReadOnlySpan<byte> input)
    {
        if (input.Length < Needle.Length)
        {
            return -1;
        }

        var searchFrom = 0;
        var firstByte = Needle[0];
        while (searchFrom <= input.Length - Needle.Length)
        {
            var relative = input[searchFrom..].IndexOf(firstByte);
            if (relative < 0)
            {
                return -1;
            }

            var candidate = searchFrom + relative;
            if (candidate <= input.Length - Needle.Length &&
                input.Slice(candidate, Needle.Length).SequenceEqual(Needle))
            {
                return candidate;
            }

            searchFrom = candidate + 1;
        }

        return -1;
    }

    private int IndexOfExactLastByteThenHead(ReadOnlySpan<byte> input)
    {
        if (input.Length < Needle.Length)
        {
            return -1;
        }

        var lastOffset = Needle.Length - 1;
        var lastByte = Needle[lastOffset];
        var searchFrom = lastOffset;
        while (searchFrom < input.Length)
        {
            var relative = input[searchFrom..].IndexOf(lastByte);
            if (relative < 0)
            {
                return -1;
            }

            var candidateEnd = searchFrom + relative;
            var candidateStart = candidateEnd - lastOffset;
            if (input.Slice(candidateStart, Needle.Length).SequenceEqual(Needle))
            {
                return candidateStart;
            }

            searchFrom = candidateEnd + 1;
        }

        return -1;
    }

    private int LastIndexOfExactFirstByteThenTail(ReadOnlySpan<byte> input)
    {
        if (input.Length < Needle.Length)
        {
            return -1;
        }

        var firstByte = Needle[0];
        var endExclusive = input.Length - Needle.Length + 1;
        while (endExclusive > 0)
        {
            var relative = input[..endExclusive].LastIndexOf(firstByte);
            if (relative < 0)
            {
                return -1;
            }

            if (input.Slice(relative, Needle.Length).SequenceEqual(Needle))
            {
                return relative;
            }

            endExclusive = relative;
        }

        return -1;
    }

    private int LastIndexOfExactLastByteThenHead(ReadOnlySpan<byte> input)
    {
        if (input.Length < Needle.Length)
        {
            return -1;
        }

        var lastOffset = Needle.Length - 1;
        var lastByte = Needle[lastOffset];
        var endExclusive = input.Length;
        while (endExclusive >= Needle.Length)
        {
            var relative = input[..endExclusive].LastIndexOf(lastByte);
            if (relative < 0)
            {
                return -1;
            }

            var candidateStart = relative - lastOffset;
            if (candidateStart >= 0 &&
                input.Slice(candidateStart, Needle.Length).SequenceEqual(Needle))
            {
                return candidateStart;
            }

            endExclusive = relative;
        }

        return -1;
    }

    private int IndexOfIgnoreCase(ReadOnlySpan<byte> input)
    {
        if (Needle.Length == 0)
        {
            return 0;
        }

        if (Needle.Length <= 10)
        {
            return AsciiSearch.IndexOfIgnoreCase(input, Needle);
        }

        if (IgnoreCaseTier == PreparedIgnoreCaseSearchTier.PackedPair)
        {
            return IndexOfIgnoreCasePackedPair(input);
        }

        var foldedNeedle = _foldedNeedle ?? Needle;
        if (Needle.Length >= 4 && TryIndexOfIgnoreCaseFirstLastVector(input, foldedNeedle, _ignoreCasePreferredCompareIndex, out var vectorIndex))
        {
            return vectorIndex;
        }

        if (_ignoreCaseShiftTable is not null)
        {
            return IndexOfIgnoreCaseHorspool(input, foldedNeedle, _ignoreCaseShiftTable);
        }

        if (!AsciiSearch.TryGetCaseVariants(Needle[0], out var firstLower, out var firstUpper))
        {
            var index = 0;
            while (index <= input.Length - Needle.Length)
            {
                var relative = input[index..].IndexOf(firstLower);
                if (relative < 0)
                {
                    return -1;
                }

                index += relative;
                if (MatchesFoldedTail(input, index, foldedNeedle))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        var start = 0;
        while (start <= input.Length - Needle.Length)
        {
            var relative = input[start..].IndexOfAny(firstLower, firstUpper);
            if (relative < 0)
            {
                return -1;
            }

            start += relative;
            if (MatchesFoldedTail(input, start, foldedNeedle))
            {
                return start;
            }

            start++;
        }

        return -1;
    }

    private int IndexOfIgnoreCaseWithMetrics(ReadOnlySpan<byte> input, ref int candidateCount, ref int verifyCount)
    {
        if (Needle.Length == 0)
        {
            return 0;
        }

        if (Needle.Length <= 10)
        {
            return AsciiSearch.IndexOfIgnoreCase(input, Needle);
        }

        if (IgnoreCaseTier == PreparedIgnoreCaseSearchTier.PackedPair)
        {
            return IndexOfIgnoreCasePackedPairWithMetrics(input, ref candidateCount, ref verifyCount);
        }

        var foldedNeedle = _foldedNeedle ?? Needle;
        if (Needle.Length >= 4 && TryIndexOfIgnoreCaseFirstLastVector(input, foldedNeedle, _ignoreCasePreferredCompareIndex, out var vectorIndex, ref candidateCount, ref verifyCount))
        {
            return vectorIndex;
        }

        if (_ignoreCaseShiftTable is not null)
        {
            return IndexOfIgnoreCaseHorspool(input, foldedNeedle, _ignoreCaseShiftTable, ref candidateCount, ref verifyCount);
        }

        if (!AsciiSearch.TryGetCaseVariants(Needle[0], out var firstLower, out var firstUpper))
        {
            var index = 0;
            while (index <= input.Length - Needle.Length)
            {
                var relative = input[index..].IndexOf(firstLower);
                if (relative < 0)
                {
                    return -1;
                }

                index += relative;
                candidateCount++;
                if (MatchesFoldedTail(input, index, foldedNeedle, ref verifyCount))
                {
                    return index;
                }

                index++;
            }

            return -1;
        }

        var start = 0;
        while (start <= input.Length - Needle.Length)
        {
            var relative = input[start..].IndexOfAny(firstLower, firstUpper);
            if (relative < 0)
            {
                return -1;
            }

            start += relative;
            candidateCount++;
            if (MatchesFoldedTail(input, start, foldedNeedle, ref verifyCount))
            {
                return start;
            }

            start++;
        }

        return -1;
    }

    private int IndexOfIgnoreCaseWithMetrics(ReadOnlySpan<byte> input, int preferredCompareIndex, ref int candidateCount, ref int verifyCount)
    {
        if (Needle.Length == 0)
        {
            return 0;
        }

        if (Needle.Length <= 10)
        {
            return AsciiSearch.IndexOfIgnoreCase(input, Needle);
        }

        var foldedNeedle = _foldedNeedle ?? Needle;
        if (Needle.Length >= 4 && TryIndexOfIgnoreCaseFirstLastVector(input, foldedNeedle, preferredCompareIndex, out var vectorIndex, ref candidateCount, ref verifyCount))
        {
            return vectorIndex;
        }

        return IndexOfIgnoreCaseWithMetrics(input, ref candidateCount, ref verifyCount);
    }

    private int IndexOfIgnoreCasePackedPair(ReadOnlySpan<byte> input)
    {
        var foldedNeedle = _foldedNeedle ?? Needle;
        if (!TryGetCaseVariants(_ignoreCasePackedPair.FoldedByte1, out var lower, out var upper))
        {
            lower = _ignoreCasePackedPair.FoldedByte1;
            upper = _ignoreCasePackedPair.FoldedByte1;
        }

        var searchIndex = 0;
        while (searchIndex <= input.Length - Length)
        {
            var relative = lower == upper
                ? input[searchIndex..].IndexOf(lower)
                : input[searchIndex..].IndexOfAny(lower, upper);
            if (relative < 0)
            {
                return -1;
            }

            var found = searchIndex + relative;
            var candidate = found - _ignoreCasePackedPair.Index1;
            if (candidate >= 0 &&
                candidate <= input.Length - Length &&
                AsciiSearch.FoldCase(input[candidate + _ignoreCasePackedPair.Index2]) == _ignoreCasePackedPair.FoldedByte2 &&
                MatchesFoldedTailSkippingIndexes(input, candidate, foldedNeedle, _ignoreCasePackedPair.Index1, _ignoreCasePackedPair.Index2))
            {
                return candidate;
            }

            searchIndex = found + 1;
        }

        return -1;
    }

    private int IndexOfIgnoreCasePackedPairWithMetrics(ReadOnlySpan<byte> input, ref int candidateCount, ref int verifyCount)
    {
        var foldedNeedle = _foldedNeedle ?? Needle;
        if (!TryGetCaseVariants(_ignoreCasePackedPair.FoldedByte1, out var lower, out var upper))
        {
            lower = _ignoreCasePackedPair.FoldedByte1;
            upper = _ignoreCasePackedPair.FoldedByte1;
        }

        var searchIndex = 0;
        while (searchIndex <= input.Length - Length)
        {
            var relative = lower == upper
                ? input[searchIndex..].IndexOf(lower)
                : input[searchIndex..].IndexOfAny(lower, upper);
            if (relative < 0)
            {
                return -1;
            }

            var found = searchIndex + relative;
            var candidate = found - _ignoreCasePackedPair.Index1;
            if (candidate >= 0 && candidate <= input.Length - Length)
            {
                candidateCount++;
                if (AsciiSearch.FoldCase(input[candidate + _ignoreCasePackedPair.Index2]) == _ignoreCasePackedPair.FoldedByte2 &&
                    MatchesFoldedTailSkippingIndexes(input, candidate, foldedNeedle, _ignoreCasePackedPair.Index1, _ignoreCasePackedPair.Index2))
                {
                    verifyCount++;
                    return candidate;
                }

                verifyCount++;
            }

            searchIndex = found + 1;
        }

        return -1;
    }

    private static bool TryIndexOfIgnoreCaseFirstLastVector(ReadOnlySpan<byte> input, ReadOnlySpan<byte> foldedNeedle, out int index)
    {
        index = -1;

#if NET
        var needleLength = foldedNeedle.Length;
        if (needleLength > input.Length)
        {
            return true;
        }

        var lastIndex = needleLength - 1;
        ref var searchSpace = ref MemoryMarshal.GetReference(input);
        var firstFolded = foldedNeedle[0];
        var compareIndex = GetPreferredCompareIndex(foldedNeedle, firstFolded, lastIndex);

        var lastFolded = foldedNeedle[compareIndex];
        var maxCandidateStart = input.Length - needleLength;
        var foldMask128 = Vector128.Create((byte)0x20);
        if (Vector256.IsHardwareAccelerated && input.Length - lastIndex >= Vector256<byte>.Count)
        {
            var foldMask256 = Vector256.Create((byte)0x20);
            var firstVector = Vector256.Create(firstFolded);
            var lastVector = Vector256.Create(lastFolded);
            nint offset = 0;
            var searchLimit = input.Length - compareIndex;
            var finalVectorOffset = searchLimit - Vector256<byte>.Count;

            while (offset <= finalVectorOffset)
            {
                var firstCompare = Vector256.Equals(
                    firstVector,
                    Vector256.BitwiseOr(Vector256.LoadUnsafe(ref searchSpace, (nuint)offset), foldMask256));
                var lastCompare = Vector256.Equals(
                    lastVector,
                    Vector256.BitwiseOr(Vector256.LoadUnsafe(ref searchSpace, (nuint)(offset + compareIndex)), foldMask256));
                var candidates = (firstCompare & lastCompare).ExtractMostSignificantBits();
                while (candidates != 0)
                {
                    var candidateOffset = BitOperations.TrailingZeroCount(candidates);
                    var candidate = (int)(offset + candidateOffset);
                    if (candidate <= maxCandidateStart &&
                        MatchesFoldedTailPreferred(input, candidate, foldedNeedle, compareIndex))
                    {
                        index = candidate;
                        return true;
                    }

                    candidates &= candidates - 1;
                }

                offset += Vector256<byte>.Count;
                if (offset > finalVectorOffset && offset < searchLimit)
                {
                    offset = finalVectorOffset;
                }
            }

            return true;
        }

        if (Vector128.IsHardwareAccelerated && input.Length - lastIndex >= Vector128<byte>.Count)
        {
            var firstVector = Vector128.Create(firstFolded);
            var lastVector = Vector128.Create(lastFolded);
            nint offset = 0;
            var searchLimit = input.Length - compareIndex;
            var finalVectorOffset = searchLimit - Vector128<byte>.Count;

            while (offset <= finalVectorOffset)
            {
                var firstCompare = Vector128.Equals(
                    firstVector,
                    Vector128.BitwiseOr(Vector128.LoadUnsafe(ref searchSpace, (nuint)offset), foldMask128));
                var lastCompare = Vector128.Equals(
                    lastVector,
                    Vector128.BitwiseOr(Vector128.LoadUnsafe(ref searchSpace, (nuint)(offset + compareIndex)), foldMask128));
                uint candidates = (firstCompare & lastCompare).ExtractMostSignificantBits();
                while (candidates != 0)
                {
                    var candidateOffset = BitOperations.TrailingZeroCount(candidates);
                    var candidate = (int)(offset + candidateOffset);
                    if (candidate <= maxCandidateStart &&
                        MatchesFoldedTailPreferred(input, candidate, foldedNeedle, compareIndex))
                    {
                        index = candidate;
                        return true;
                    }

                    candidates &= candidates - 1;
                }

                offset += Vector128<byte>.Count;
                if (offset > finalVectorOffset && offset < searchLimit)
                {
                    offset = finalVectorOffset;
                }
            }

            return true;
        }
#endif

        return false;
    }

    private static bool TryIndexOfIgnoreCaseFirstLastVector(ReadOnlySpan<byte> input, ReadOnlySpan<byte> foldedNeedle, out int index, ref int candidateCount, ref int verifyCount)
    {
        index = -1;

#if NET
        var needleLength = foldedNeedle.Length;
        if (needleLength > input.Length)
        {
            return true;
        }

        var lastIndex = needleLength - 1;
        ref var searchSpace = ref MemoryMarshal.GetReference(input);
        var firstFolded = foldedNeedle[0];
        var compareIndex = GetPreferredCompareIndex(foldedNeedle, firstFolded, lastIndex);

        var lastFolded = foldedNeedle[compareIndex];
        var maxCandidateStart = input.Length - needleLength;
        var foldMask128 = Vector128.Create((byte)0x20);
        if (Vector256.IsHardwareAccelerated && input.Length - lastIndex >= Vector256<byte>.Count)
        {
            var foldMask256 = Vector256.Create((byte)0x20);
            var firstVector = Vector256.Create(firstFolded);
            var lastVector = Vector256.Create(lastFolded);
            nint offset = 0;
            var searchLimit = input.Length - compareIndex;
            var finalVectorOffset = searchLimit - Vector256<byte>.Count;

            while (offset <= finalVectorOffset)
            {
                var firstCompare = Vector256.Equals(
                    firstVector,
                    Vector256.BitwiseOr(Vector256.LoadUnsafe(ref searchSpace, (nuint)offset), foldMask256));
                var lastCompare = Vector256.Equals(
                    lastVector,
                    Vector256.BitwiseOr(Vector256.LoadUnsafe(ref searchSpace, (nuint)(offset + compareIndex)), foldMask256));
                var candidates = (firstCompare & lastCompare).ExtractMostSignificantBits();
                while (candidates != 0)
                {
                    var candidateOffset = BitOperations.TrailingZeroCount(candidates);
                    var candidate = (int)(offset + candidateOffset);
                    if (candidate <= maxCandidateStart)
                    {
                        candidateCount++;
                        if (MatchesFoldedTailPreferred(input, candidate, foldedNeedle, compareIndex))
                        {
                            verifyCount++;
                            index = candidate;
                            return true;
                        }

                        verifyCount++;
                    }

                    candidates &= candidates - 1;
                }

                offset += Vector256<byte>.Count;
                if (offset > finalVectorOffset && offset < searchLimit)
                {
                    offset = finalVectorOffset;
                }
            }

            return true;
        }

        if (Vector128.IsHardwareAccelerated && input.Length - lastIndex >= Vector128<byte>.Count)
        {
            var firstVector = Vector128.Create(firstFolded);
            var lastVector = Vector128.Create(lastFolded);
            nint offset = 0;
            var searchLimit = input.Length - compareIndex;
            var finalVectorOffset = searchLimit - Vector128<byte>.Count;

            while (offset <= finalVectorOffset)
            {
                var firstCompare = Vector128.Equals(
                    firstVector,
                    Vector128.BitwiseOr(Vector128.LoadUnsafe(ref searchSpace, (nuint)offset), foldMask128));
                var lastCompare = Vector128.Equals(
                    lastVector,
                    Vector128.BitwiseOr(Vector128.LoadUnsafe(ref searchSpace, (nuint)(offset + compareIndex)), foldMask128));
                uint candidates = (firstCompare & lastCompare).ExtractMostSignificantBits();
                while (candidates != 0)
                {
                    var candidateOffset = BitOperations.TrailingZeroCount(candidates);
                    var candidate = (int)(offset + candidateOffset);
                    if (candidate <= maxCandidateStart)
                    {
                        candidateCount++;
                        if (MatchesFoldedTailPreferred(input, candidate, foldedNeedle, compareIndex))
                        {
                            verifyCount++;
                            index = candidate;
                            return true;
                        }

                        verifyCount++;
                    }

                    candidates &= candidates - 1;
                }

                offset += Vector128<byte>.Count;
                if (offset > finalVectorOffset && offset < searchLimit)
                {
                    offset = finalVectorOffset;
                }
            }

            return true;
        }
#endif

        return false;
    }

    private static bool TryIndexOfIgnoreCaseFirstLastVector(ReadOnlySpan<byte> input, ReadOnlySpan<byte> foldedNeedle, int preferredCompareIndex, out int index)
    {
        var candidateCount = 0;
        var verifyCount = 0;
        return TryIndexOfIgnoreCaseFirstLastVector(input, foldedNeedle, preferredCompareIndex, out index, ref candidateCount, ref verifyCount);
    }

    private static bool TryIndexOfIgnoreCaseFirstLastVector(ReadOnlySpan<byte> input, ReadOnlySpan<byte> foldedNeedle, int preferredCompareIndex, out int index, ref int candidateCount, ref int verifyCount)
    {
        index = -1;

#if NET
        var needleLength = foldedNeedle.Length;
        if (needleLength > input.Length)
        {
            return true;
        }

        var lastIndex = needleLength - 1;
        if ((uint)preferredCompareIndex >= (uint)needleLength || preferredCompareIndex == 0)
        {
            preferredCompareIndex = GetPreferredCompareIndex(foldedNeedle, foldedNeedle[0], lastIndex);
        }

        ref var searchSpace = ref MemoryMarshal.GetReference(input);
        var firstFolded = foldedNeedle[0];
        var compareFolded = foldedNeedle[preferredCompareIndex];
        var maxCandidateStart = input.Length - needleLength;
        var foldMask128 = Vector128.Create((byte)0x20);
        if (Vector256.IsHardwareAccelerated && input.Length - lastIndex >= Vector256<byte>.Count)
        {
            var foldMask256 = Vector256.Create((byte)0x20);
            var firstVector = Vector256.Create(firstFolded);
            var compareVector = Vector256.Create(compareFolded);
            nint offset = 0;
            var searchLimit = input.Length - preferredCompareIndex;
            var finalVectorOffset = searchLimit - Vector256<byte>.Count;

            while (offset <= finalVectorOffset)
            {
                var firstCompare = Vector256.Equals(
                    firstVector,
                    Vector256.BitwiseOr(Vector256.LoadUnsafe(ref searchSpace, (nuint)offset), foldMask256));
                var preferredCompare = Vector256.Equals(
                    compareVector,
                    Vector256.BitwiseOr(Vector256.LoadUnsafe(ref searchSpace, (nuint)(offset + preferredCompareIndex)), foldMask256));
                var candidates = (firstCompare & preferredCompare).ExtractMostSignificantBits();
                while (candidates != 0)
                {
                    var candidateOffset = BitOperations.TrailingZeroCount(candidates);
                    var candidate = (int)(offset + candidateOffset);
                    if (candidate <= maxCandidateStart)
                    {
                        candidateCount++;
                        if (MatchesFoldedTailPreferred(input, candidate, foldedNeedle, preferredCompareIndex))
                        {
                            verifyCount++;
                            index = candidate;
                            return true;
                        }

                        verifyCount++;
                    }

                    candidates &= candidates - 1;
                }

                offset += Vector256<byte>.Count;
                if (offset > finalVectorOffset && offset < searchLimit)
                {
                    offset = finalVectorOffset;
                }
            }

            return true;
        }

        if (Vector128.IsHardwareAccelerated && input.Length - lastIndex >= Vector128<byte>.Count)
        {
            var firstVector = Vector128.Create(firstFolded);
            var compareVector = Vector128.Create(compareFolded);
            nint offset = 0;
            var searchLimit = input.Length - preferredCompareIndex;
            var finalVectorOffset = searchLimit - Vector128<byte>.Count;

            while (offset <= finalVectorOffset)
            {
                var firstCompare = Vector128.Equals(
                    firstVector,
                    Vector128.BitwiseOr(Vector128.LoadUnsafe(ref searchSpace, (nuint)offset), foldMask128));
                var preferredCompare = Vector128.Equals(
                    compareVector,
                    Vector128.BitwiseOr(Vector128.LoadUnsafe(ref searchSpace, (nuint)(offset + preferredCompareIndex)), foldMask128));
                uint candidates = (firstCompare & preferredCompare).ExtractMostSignificantBits();
                while (candidates != 0)
                {
                    var candidateOffset = BitOperations.TrailingZeroCount(candidates);
                    var candidate = (int)(offset + candidateOffset);
                    if (candidate <= maxCandidateStart)
                    {
                        candidateCount++;
                        if (MatchesFoldedTailPreferred(input, candidate, foldedNeedle, preferredCompareIndex))
                        {
                            verifyCount++;
                            index = candidate;
                            return true;
                        }

                        verifyCount++;
                    }

                    candidates &= candidates - 1;
                }

                offset += Vector128<byte>.Count;
                if (offset > finalVectorOffset && offset < searchLimit)
                {
                    offset = finalVectorOffset;
                }
            }

            return true;
        }
#endif

        return false;
    }

    private static int IndexOfIgnoreCaseHorspool(ReadOnlySpan<byte> input, ReadOnlySpan<byte> foldedNeedle, int[] shiftTable)
    {
        var needleLength = foldedNeedle.Length;
        var lastIndex = needleLength - 1;
        var index = 0;
        while (index <= input.Length - needleLength)
        {
            var foldedTail = AsciiSearch.FoldCase(input[index + lastIndex]);
            if (foldedTail == foldedNeedle[lastIndex] &&
                AsciiSearch.MatchesFoldedIgnoreCase(input.Slice(index, needleLength), foldedNeedle))
            {
                return index;
            }

            index += shiftTable[foldedTail];
        }

        return -1;
    }

    private static int IndexOfIgnoreCaseHorspool(ReadOnlySpan<byte> input, ReadOnlySpan<byte> foldedNeedle, int[] shiftTable, ref int candidateCount, ref int verifyCount)
    {
        var needleLength = foldedNeedle.Length;
        var lastIndex = needleLength - 1;
        var index = 0;
        while (index <= input.Length - needleLength)
        {
            var foldedTail = AsciiSearch.FoldCase(input[index + lastIndex]);
            if (foldedTail == foldedNeedle[lastIndex])
            {
                candidateCount++;
                if (AsciiSearch.MatchesFoldedIgnoreCase(input.Slice(index, needleLength), foldedNeedle))
                {
                    verifyCount++;
                    return index;
                }

                verifyCount++;
            }

            index += shiftTable[foldedTail];
        }

        return -1;
    }

    private int LastIndexOfIgnoreCase(ReadOnlySpan<byte> input)
    {
        if (Needle.Length == 0)
        {
            return input.Length;
        }

        if (Needle.Length == 1)
        {
            return AsciiSearch.LastIndexOfIgnoreCase(input, Needle);
        }

        var foldedNeedle = _foldedNeedle ?? Needle;
        if (!AsciiSearch.TryGetCaseVariants(Needle[0], out var firstLower, out var firstUpper))
        {
            for (var end = input.Length - Needle.Length + 1; end > 0;)
            {
                var relative = input[..end].LastIndexOf(firstLower);
                if (relative < 0)
                {
                    return -1;
                }

                if (MatchesFoldedTail(input, relative, foldedNeedle))
                {
                    return relative;
                }

                end = relative;
            }

            return -1;
        }

        for (var end = input.Length - Needle.Length + 1; end > 0;)
        {
            var relative = input[..end].LastIndexOfAny(firstLower, firstUpper);
            if (relative < 0)
            {
                return -1;
            }

            if (MatchesFoldedTail(input, relative, foldedNeedle))
            {
                return relative;
            }

            end = relative;
        }

        return -1;
    }

    private static byte[] CreateFoldedNeedle(ReadOnlySpan<byte> needle)
    {
        var folded = new byte[needle.Length];
        for (var i = 0; i < needle.Length; i++)
        {
            folded[i] = AsciiSearch.FoldCase(needle[i]);
        }

        return folded;
    }

    private static int[] CreateIgnoreCaseShiftTable(ReadOnlySpan<byte> foldedNeedle)
    {
        var shiftTable = new int[256];
        shiftTable.AsSpan().Fill(foldedNeedle.Length);
        for (var i = 0; i < foldedNeedle.Length - 1; i++)
        {
            shiftTable[foldedNeedle[i]] = foldedNeedle.Length - 1 - i;
        }

        return shiftTable;
    }

    private static bool MatchesFoldedTail(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> foldedNeedle)
    {
        if (index + foldedNeedle.Length > input.Length)
        {
            return false;
        }

        if (foldedNeedle.Length is >= 9 and <= 16)
        {
            return AsciiSearch.MatchesFoldedIgnoreCaseLength9To16(input.Slice(index, foldedNeedle.Length), foldedNeedle);
        }

        for (var i = 1; i < foldedNeedle.Length; i++)
        {
            if (AsciiSearch.FoldCase(input[index + i]) != foldedNeedle[i])
            {
                return false;
            }
        }

        return true;
    }

    private static bool MatchesFoldedTail(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> foldedNeedle, ref int verifyCount)
    {
        verifyCount++;
        return MatchesFoldedTail(input, index, foldedNeedle);
    }

    private static bool MatchesFoldedTailPreferred(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> foldedNeedle, int preferredIndex)
    {
        if (index + foldedNeedle.Length > input.Length)
        {
            return false;
        }

        if (foldedNeedle.Length is >= 9 and <= 16)
        {
            return AsciiSearch.MatchesFoldedIgnoreCaseLength9To16(input.Slice(index, foldedNeedle.Length), foldedNeedle);
        }

        var suffixIndex = preferredIndex + 1;
        if (suffixIndex < foldedNeedle.Length &&
            !AsciiSearch.MatchesFoldedIgnoreCase(
                input.Slice(index + suffixIndex, foldedNeedle.Length - suffixIndex),
                foldedNeedle[suffixIndex..]))
        {
            return false;
        }

        if (preferredIndex > 1 &&
            !AsciiSearch.MatchesFoldedIgnoreCase(
                input.Slice(index + 1, preferredIndex - 1),
                foldedNeedle[1..preferredIndex]))
        {
            return false;
        }

        return true;
    }

    private static int GetPreferredCompareIndex(ReadOnlySpan<byte> foldedNeedle, byte firstFolded, int lastIndex)
    {
        if (AsciiSearch.TryGetDotNetLikeAsciiAnchorSelection(foldedNeedle, ignoreCase: true, out var anchors) &&
            anchors.Anchor2 > 0)
        {
            return anchors.Anchor2;
        }

        Span<int> counts = stackalloc int[256];
        for (var i = 0; i < foldedNeedle.Length; i++)
        {
            counts[foldedNeedle[i]]++;
        }

        var compareIndex = -1;
        var rarestCount = int.MaxValue;
        var bestRarityScore = int.MinValue;
        for (var i = 1; i < foldedNeedle.Length; i++)
        {
            if (foldedNeedle[i] == firstFolded)
            {
                continue;
            }

            var count = counts[foldedNeedle[i]];
            var rarityScore = GetStaticIgnoreCaseRarityScore(foldedNeedle[i]);
            if (count < rarestCount ||
                (count == rarestCount && rarityScore > bestRarityScore) ||
                (count == rarestCount && rarityScore == bestRarityScore && i > compareIndex))
            {
                rarestCount = count;
                bestRarityScore = rarityScore;
                compareIndex = i;
            }
        }

        return compareIndex >= 0 ? compareIndex : lastIndex;
    }

    private static int GetStaticIgnoreCaseRarityScore(byte folded)
    {
        return (int)(AsciiSearch.GetDotNetLikeAsciiFrequency(folded) * -1000f);
    }

    private PreparedIgnoreCaseSearchTier DetermineIgnoreCaseTier()
    {
        if (!IgnoreCase)
        {
            return PreparedIgnoreCaseSearchTier.None;
        }

        if (Needle.Length <= 10)
        {
            return PreparedIgnoreCaseSearchTier.ShortLiteral;
        }

        if (ShouldUsePackedPairTier())
        {
            return PreparedIgnoreCaseSearchTier.PackedPair;
        }

        return PreparedIgnoreCaseSearchTier.VectorAnchored;
    }

    private bool ShouldUsePackedPairTier()
    {
        if (_ignoreCasePackedPair.Index1 <= 0 ||
            _ignoreCasePackedPair.Index2 <= 0 ||
            _ignoreCasePackedPair.Index1 == _ignoreCasePreferredCompareIndex)
        {
            return false;
        }

        var primaryScore = GetStaticIgnoreCaseRarityScore(_ignoreCasePackedPair.FoldedByte1);
        var secondaryScore = GetStaticIgnoreCaseRarityScore(_ignoreCasePackedPair.FoldedByte2);
        return Needle.Length >= 12 && primaryScore >= 18 && secondaryScore >= 12;
    }

    private static bool TryCreateIgnoreCasePackedPair(ReadOnlySpan<byte> foldedNeedle, out PreparedIgnoreCasePackedPair pair)
    {
        pair = default;
        if (foldedNeedle.Length <= 2)
        {
            return false;
        }

        Span<int> counts = stackalloc int[256];
        for (var i = 0; i < foldedNeedle.Length; i++)
        {
            counts[foldedNeedle[i]]++;
        }

        var bestIndex1 = -1;
        var bestIndex2 = -1;
        var bestScore1 = int.MinValue;
        var bestScore2 = int.MinValue;
        for (var i = 1; i < foldedNeedle.Length; i++)
        {
            var score = GetStaticIgnoreCaseRarityScore(foldedNeedle[i]) * 100 - counts[foldedNeedle[i]] * 10 + i;
            if (score > bestScore1)
            {
                bestScore2 = bestScore1;
                bestIndex2 = bestIndex1;
                bestScore1 = score;
                bestIndex1 = i;
            }
            else if (i != bestIndex1 && score > bestScore2)
            {
                bestScore2 = score;
                bestIndex2 = i;
            }
        }

        if (bestIndex1 < 0 || bestIndex2 < 0)
        {
            return false;
        }

        if (bestIndex2 < bestIndex1)
        {
            (bestIndex1, bestIndex2) = (bestIndex2, bestIndex1);
        }

        pair = new PreparedIgnoreCasePackedPair(
            foldedNeedle[bestIndex1],
            bestIndex1,
            foldedNeedle[bestIndex2],
            bestIndex2);
        return true;
    }

    private static bool TryGetCaseVariants(byte folded, out byte lower, out byte upper)
    {
        lower = folded;
        upper = folded;
        if (folded is >= (byte)'a' and <= (byte)'z')
        {
            upper = (byte)(folded - 32);
            return true;
        }

        if (folded is >= (byte)'A' and <= (byte)'Z')
        {
            lower = (byte)(folded + 32);
            return true;
        }

        return false;
    }

    private static bool MatchesFoldedTailSkippingIndexes(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> foldedNeedle, int skipIndex1, int skipIndex2)
    {
        if (index + foldedNeedle.Length > input.Length)
        {
            return false;
        }

        for (var i = 0; i < foldedNeedle.Length; i++)
        {
            if (i == skipIndex1 || i == skipIndex2)
            {
                continue;
            }

            if (AsciiSearch.FoldCase(input[index + i]) != foldedNeedle[i])
            {
                return false;
            }
        }

        return true;
    }
}
