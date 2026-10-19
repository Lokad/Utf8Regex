using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
#if NET
using System.Runtime.Intrinsics;
#endif

namespace Lokad.Utf8Regex.Internal.Search;

internal enum PreparedIgnoreCaseSearchTier : byte
{
    None = 0,
    ShortLiteral = 1,
    VectorAnchored = 2,
}

internal enum PreparedExactSearchTier : byte
{
    None = 0,
    ShortFirstByte = 1,
    MediumLastByte = 2,
    SpanIndexOf = 3,
}


internal readonly struct PreparedByteSearch
{
    private readonly byte[] _values;

    private PreparedByteSearch(byte[] values, PreparedByteSet? byteSet)
    {
        _values = values;
        ByteSet = byteSet;
    }

    public int Count => _values?.Length ?? 0;

    public ReadOnlySpan<byte> Values => _values ?? [];

    private PreparedByteSet? ByteSet { get; }

    public static PreparedByteSearch Create(params byte[] values)
        => Create(values.AsSpan());

    internal static PreparedByteSearch Create(ReadOnlySpan<byte> values)
    {
        if (values.IsEmpty) return FromOwnedValues([], null);
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var owned = budget.AllocateArray<byte>(values.Length, false);
            budget.ChargeWork((ulong)values.Length);
            values.CopyTo(owned);
            var result = FromOwnedValues(owned, owned.Length > 3 ? PreparedByteSet.CreateOwned(owned) : null);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    // Owned preparation supplies an immutable array with independent admission.
    // The reusable kernel has no dependency on a flavor's construction owner.
    internal static PreparedByteSearch FromOwnedValues(byte[] values, PreparedByteSet? byteSet) => new(values, byteSet);

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        return _values.Length switch
        {
            0 => -1,
            1 => input.IndexOf(_values[0]),
            2 => input.IndexOfAny(_values[0], _values[1]),
            3 => input.IndexOfAny(_values[0], _values[1], _values[2]),
            _ => GetRequiredByteSet().IndexOf(input),
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
            _ => GetRequiredByteSet().LastIndexOf(input),
        };
    }

    private PreparedByteSet GetRequiredByteSet()
        => ByteSet ?? throw new InvalidOperationException("A large byte search requires a prepared byte set.");
}

internal readonly struct PreparedSubstringSearch
{
    internal readonly byte[]? _foldedNeedle;
    private readonly int[]? _ignoreCaseShiftTable;
    internal readonly int _ignoreCasePreferredCompareIndex;
    private readonly PreparedExactSearchTier _exactTier;
    private readonly AsciiAnchorSelection _exactAnchors;

    public PreparedSubstringSearch(byte[] needle, bool ignoreCase)
    {
        this = default;
        Needle = needle;
        IgnoreCase = ignoreCase;
        _exactTier = !ignoreCase ? DetermineExactTier(needle.Length) : PreparedExactSearchTier.None;
        _ignoreCasePreferredCompareIndex = -1;

        // These kernels only borrow the needle; they perform no preparation scans.
        if (needle.Length <= 1 || !ignoreCase && needle.Length is not (>= 4 and <= 16))
            return;

        using var construction = Utf8ConstructionScope.Enter();
        var owner = construction.Budget; owner.EnterDepth();
        try
        {
            if (!ignoreCase)
            {
                owner.ChargeWork(3ul * (ulong)needle.Length);
                if (AsciiSearch.TryGetDotNetLikeAsciiAnchorSelection(needle, false, out var anchors))
                    _exactAnchors = anchors;
                construction.Complete();
                return;
            }

            // The caller already owns the immutable needle. A canonical folded
            // needle can be borrowed; uppercase ASCII needs one final owned copy.
            owner.ChargeWork((ulong)needle.Length);
            var foldedNeedle = needle;
            if (needle.AsSpan().IndexOfAnyInRange((byte)'A', (byte)'Z') >= 0)
            {
                foldedNeedle = owner.AllocateArray<byte>(needle.Length, false);
                owner.ChargeWork((ulong)needle.Length);
                for (var i = 0; i < needle.Length; i++)
                    foldedNeedle[i] = AsciiSearch.FoldCase(needle[i]);
            }
            _foldedNeedle = foldedNeedle;

            // Forward searches of up to eight bytes use the short kernel.
            // Reverse searches still need folded bytes, but never the shift table.
            if (needle.Length >= 9)
            {
                var shifts = owner.AllocateArray<int>(256, false);
                owner.ChargeWork(256);
                shifts.AsSpan().Fill(needle.Length);
                owner.ChargeWork((ulong)needle.Length - 1);
                for (var i = 0; i < needle.Length - 1; i++)
                    shifts[foldedNeedle[i]] = needle.Length - 1 - i;
                _ignoreCaseShiftTable = shifts;
                _ignoreCasePreferredCompareIndex = GetPreferredCompareIndex(foldedNeedle, foldedNeedle[0], needle.Length - 1, owner);
            }
            construction.Complete();
        }
        finally { owner.LeaveDepth(); }
    }

    public byte[] Needle { get; }

    public bool IgnoreCase { get; }

    public int Length => Needle.Length;

    public PreparedIgnoreCaseSearchTier IgnoreCaseTier => !IgnoreCase
        ? PreparedIgnoreCaseSearchTier.None
        : Needle.Length <= 8 ? PreparedIgnoreCaseSearchTier.ShortLiteral : PreparedIgnoreCaseSearchTier.VectorAnchored;

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

        if (tier == PreparedIgnoreCaseSearchTier.ShortLiteral || Needle.Length <= 8)
        {
            return CountWithMetrics(input, out candidateCount, out verifyCount);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - Length)
        {
            var found = IndexOfIgnoreCaseWithMetrics(input[index..], _ignoreCasePreferredCompareIndex, ref candidateCount, ref verifyCount);

            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + Length;
        }

        return count;
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

        if (Needle.Length <= 8)
        {
            return AsciiSearch.IndexOfIgnoreCase(input, Needle);
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

        if (Needle.Length <= 8)
        {
            return AsciiSearch.IndexOfIgnoreCase(input, Needle);
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

    internal int IndexOfIgnoreCaseWithMetrics(ReadOnlySpan<byte> input, int preferredCompareIndex, ref int candidateCount, ref int verifyCount)
    {
        if (Needle.Length == 0)
        {
            return 0;
        }

        if (Needle.Length <= 8)
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
            preferredCompareIndex = GetPreferredCompareIndex(foldedNeedle, foldedNeedle[0], lastIndex, null);
        }

        var firstFolded = foldedNeedle[0];
        var compareFolded = foldedNeedle[preferredCompareIndex];
        if (!CanUseCaseBitVectorAnchors(firstFolded, compareFolded))
        {
            return false;
        }

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
                    Vector256.BitwiseOr(MemoryMarshal.Read<Vector256<byte>>(input[(int)offset..]), foldMask256));
                var preferredCompare = Vector256.Equals(
                    compareVector,
                    Vector256.BitwiseOr(MemoryMarshal.Read<Vector256<byte>>(input[((int)offset + preferredCompareIndex)..]), foldMask256));
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
                    Vector128.BitwiseOr(MemoryMarshal.Read<Vector128<byte>>(input[(int)offset..]), foldMask128));
                var preferredCompare = Vector128.Equals(
                    compareVector,
                    Vector128.BitwiseOr(MemoryMarshal.Read<Vector128<byte>>(input[((int)offset + preferredCompareIndex)..]), foldMask128));
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

    internal static bool MatchesFoldedTailPreferred(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> foldedNeedle, int preferredIndex)
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

    private static int GetPreferredCompareIndex(ReadOnlySpan<byte> foldedNeedle, byte firstFolded, int lastIndex,
        Utf8ConstructionBudget? owner)
    {
        owner?.ChargeWork(3ul * (ulong)foldedNeedle.Length);
        if (AsciiSearch.TryGetDotNetLikeAsciiAnchorSelection(foldedNeedle, ignoreCase: true, out var anchors) &&
            anchors.Anchor2 > 0)
        {
            return anchors.Anchor2;
        }

        owner?.ChargeWork(256);
        Span<int> counts = stackalloc int[256];
        counts.Clear();
        owner?.ChargeWork((ulong)foldedNeedle.Length);
        for (var i = 0; i < foldedNeedle.Length; i++)
        {
            counts[foldedNeedle[i]]++;
        }

        var compareIndex = -1;
        var rarestCount = int.MaxValue;
        var bestRarityScore = int.MinValue;
        owner?.ChargeWork((ulong)foldedNeedle.Length - 1);
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

    // The vector fold sets the ASCII case bit, so both correlated anchors must be letters.
    private static bool CanUseCaseBitVectorAnchors(byte firstFolded, byte compareFolded)
        => TryGetCaseVariants(firstFolded, out _, out _) &&
           TryGetCaseVariants(compareFolded, out _, out _);

}
