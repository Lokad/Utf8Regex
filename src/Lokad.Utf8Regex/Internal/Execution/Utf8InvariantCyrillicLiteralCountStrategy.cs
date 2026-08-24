using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using System.Text;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Counts invariant-ignore-case literals whose only case-bearing scalars are
/// the one-to-one paired letters in the basic Cyrillic alphabet.
/// </summary>
internal sealed class Utf8InvariantCyrillicLiteralCountStrategy
{
    private const int MaximumAlternatives = 8;
    private readonly Literal[] _literals;
    private readonly SearchValues<byte>? _sharedAnchorSearchValues;
    private readonly byte[]? _sharedAnchorLiteralMasks;
    private readonly int _sharedAnchorByteOffset;
    private readonly PreparedMultiLiteralPackedNibbleSimdPrefilter _correlatedPrefilter;

    private Utf8InvariantCyrillicLiteralCountStrategy(Literal[] literals)
    {
        _literals = literals;
        _sharedAnchorSearchValues = null;
        _sharedAnchorLiteralMasks = null;
        _sharedAnchorByteOffset = -1;
        _correlatedPrefilter = literals.Length > 1
            ? PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateInvariantCyrillicIgnoreCase(
                literals.Select(static literal => literal.Bytes).ToArray())
            : default;
        if (literals.Length == 1)
        {
            return;
        }

        var sharedAnchorByteOffset = literals[0].AnchorByteOffset;
        for (var i = 1; i < literals.Length; i++)
        {
            if (literals[i].AnchorByteOffset != sharedAnchorByteOffset)
            {
                return;
            }
        }

        Span<bool> seen = stackalloc bool[256];
        Span<byte> anchorBytes = stackalloc byte[MaximumAlternatives * 2];
        var literalMasks = new byte[256];
        var anchorByteCount = 0;
        for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
        {
            ref readonly var literal = ref literals[literalIndex];
            AddSharedAnchor(literal.AnchorFirst, literalIndex, seen, anchorBytes, literalMasks, ref anchorByteCount);
            AddSharedAnchor(literal.AnchorSecond, literalIndex, seen, anchorBytes, literalMasks, ref anchorByteCount);
        }

        _sharedAnchorSearchValues = SearchValues.Create(anchorBytes[..anchorByteCount]);
        _sharedAnchorLiteralMasks = literalMasks;
        _sharedAnchorByteOffset = sharedAnchorByteOffset;
    }

    private static void AddSharedAnchor(
        byte value,
        int literalIndex,
        Span<bool> seen,
        Span<byte> anchorBytes,
        byte[] literalMasks,
        ref int anchorByteCount)
    {
        literalMasks[value] |= (byte)(1 << literalIndex);
        if (!seen[value])
        {
            seen[value] = true;
            anchorBytes[anchorByteCount++] = value;
        }
    }

    public static bool TryCreate(
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout,
        [NotNullWhen(true)] out Utf8InvariantCyrillicLiteralCountStrategy? strategy)
    {
        const RegexOptions allowedOptions =
            RegexOptions.IgnoreCase |
            RegexOptions.CultureInvariant |
            RegexOptions.Compiled;
        if (pattern.Length == 0 ||
            (options & (RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)) !=
                (RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
            (options & ~allowedOptions) != 0 ||
            matchTimeout != Regex.InfiniteMatchTimeout)
        {
            strategy = null;
            return false;
        }

        var alternatives = pattern.Split('|');
        if (alternatives.Length is 0 or > MaximumAlternatives)
        {
            strategy = null;
            return false;
        }

        var literals = new Literal[alternatives.Length];
        for (var alternativeIndex = 0; alternativeIndex < alternatives.Length; alternativeIndex++)
        {
            if (!TryCreateLiteral(alternatives[alternativeIndex], out literals[alternativeIndex]))
            {
                strategy = null;
                return false;
            }
        }

        strategy = new Utf8InvariantCyrillicLiteralCountStrategy(literals);
        return true;
    }

    private static bool TryCreateLiteral(string literal, out Literal prepared)
    {
        if (literal.Length == 0)
        {
            prepared = default;
            return false;
        }

        var units = new Unit[literal.Length];
        var byteOffset = 0;
        var anchorPreviousAscii = -1;
        var anchorByteOffset = -1;
        var anchorFirst = (byte)0;
        var anchorSecond = (byte)0;
        for (var i = 0; i < literal.Length; i++)
        {
            var value = literal[i];
            if (value <= 0x7F)
            {
                if (char.IsAsciiLetter(value) || Utf8RegexSyntax.IsRegexMetaCharacter(value))
                {
                    prepared = default;
                    return false;
                }

                units[i] = Unit.CreateAscii(byteOffset, (byte)value);
                byteOffset++;
                continue;
            }

            if (!Utf8InvariantCyrillicCase.TryGetPair(value, out var upper, out var lower))
            {
                prepared = default;
                return false;
            }

            var upperUtf8 = Utf8InvariantCyrillicCase.EncodeTwoByteScalar(upper);
            var lowerUtf8 = Utf8InvariantCyrillicCase.EncodeTwoByteScalar(lower);
            units[i] = Unit.CreatePair(byteOffset, upperUtf8, lowerUtf8);
            var previousAscii = i > 0 && literal[i - 1] <= 0x7F
                ? literal[i - 1]
                : -1;
            // Prefer the first Cyrillic scalar following an ASCII separator.
            // Its adjacent byte supplies a cheap correlated rejection while the
            // first Cyrillic scalar remains the fallback for single-word literals.
            if (anchorByteOffset < 0 ||
                (anchorPreviousAscii < 0 && previousAscii >= 0))
            {
                anchorPreviousAscii = previousAscii;
                anchorByteOffset = byteOffset + 1;
                anchorFirst = (byte)(upperUtf8 >> 8);
                anchorSecond = (byte)(lowerUtf8 >> 8);
            }

            byteOffset += 2;
        }

        if (anchorByteOffset < 0)
        {
            prepared = default;
            return false;
        }

        prepared = new Literal(
            units,
            Encoding.UTF8.GetBytes(literal),
            byteOffset,
            anchorPreviousAscii,
            anchorByteOffset,
            anchorFirst,
            anchorSecond);
        return true;
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        if (_literals.Length == 1 &&
            TryCountSingleLiteralAndValidateTwoByte(input, _literals[0], out var fusedCount))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(
                Utf8ExecutionRoute.InvariantCyrillicFusedValidatedCount);
            return fusedCount;
        }

        Utf8Validation.ThrowIfInvalidOnly(input);
        if (_literals.Length == 1)
        {
            return CountSingleLiteral(input, _literals[0]);
        }

        if (_correlatedPrefilter.HasValue)
        {
            return CountWithCorrelatedPrefilter(input);
        }

        if (_sharedAnchorSearchValues is { } sharedAnchorSearchValues &&
            _sharedAnchorLiteralMasks is { } sharedAnchorLiteralMasks)
        {
            return CountWithSharedAnchor(input, sharedAnchorSearchValues, sharedAnchorLiteralMasks);
        }

        Span<int> candidates = stackalloc int[_literals.Length];
        candidates.Fill(-1);
        var count = 0;
        var nextStart = 0;
        while (nextStart <= input.Length)
        {
            var bestIndex = int.MaxValue;
            var bestLiteralIndex = -1;
            for (var literalIndex = 0; literalIndex < _literals.Length; literalIndex++)
            {
                if (candidates[literalIndex] < nextStart)
                {
                    candidates[literalIndex] = _literals[literalIndex].FindNext(input, nextStart);
                }

                if (candidates[literalIndex] < bestIndex)
                {
                    bestIndex = candidates[literalIndex];
                    bestLiteralIndex = literalIndex;
                }
            }

            if (bestLiteralIndex < 0)
            {
                break;
            }

            count++;
            nextStart = bestIndex + _literals[bestLiteralIndex].ByteLength;
        }

        return count;
    }

    private static bool TryCountSingleLiteralAndValidateTwoByte(
        ReadOnlySpan<byte> input,
        Literal literal,
        out int count)
    {
        count = 0;
        if (!Vector256.IsHardwareAccelerated || input.Length < Vector256<byte>.Count)
        {
            return false;
        }

        var nextMatchStart = 0;
        var maxStart = input.Length - literal.ByteLength;
        var offset = 0;
        var expectedContinuationCarry = 0u;
        ref var inputRef = ref MemoryMarshal.GetReference(input);
        var lastVectorStart = input.Length - Vector256<byte>.Count;
        var continuationTag = Vector256.Create((byte)0x80);
        var continuationMask = Vector256.Create((byte)0xC0);
        var twoByteLeadTag = Vector256.Create((byte)0xC0);
        var twoByteLeadMask = Vector256.Create((byte)0xE0);
        var overlongLeadTag = Vector256.Create((byte)0xC0);
        var overlongLeadMask = Vector256.Create((byte)0xFE);
        var anchorMaskByte = (byte)~(literal.AnchorFirst ^ literal.AnchorSecond);
        var anchorMask = Vector256.Create(anchorMaskByte);
        var maskedAnchor = Vector256.Create((byte)(literal.AnchorFirst & anchorMaskByte));

        // This intentionally stays monomorphic. It mirrors the lean ASCII/two-byte
        // validation proof while extracting the one literal anchor from the same
        // loaded block. Rare wider scalars resume through Rune below; structural
        // uncertainty returns to the canonical validator and direct search.
        while (offset <= lastVectorStart)
        {
            var current = Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, offset));
            var nonAsciiBits = current.ExtractMostSignificantBits();
            if (nonAsciiBits == 0 && expectedContinuationCarry == 0)
            {
                offset += Vector256<byte>.Count;
                continue;
            }

            var continuationBits = Vector256.Equals(
                Vector256.BitwiseAnd(current, continuationMask),
                continuationTag).ExtractMostSignificantBits();
            var twoByteLeadBits = Vector256.Equals(
                Vector256.BitwiseAnd(current, twoByteLeadMask),
                twoByteLeadTag).ExtractMostSignificantBits();
            var overlongLeadBits = Vector256.Equals(
                Vector256.BitwiseAnd(current, overlongLeadMask),
                overlongLeadTag).ExtractMostSignificantBits();
            var unsupportedBits = nonAsciiBits & ~(continuationBits | twoByteLeadBits);
            var expectedContinuationWide = ((ulong)twoByteLeadBits << 1) | expectedContinuationCarry;
            var expectedContinuationBits = (uint)expectedContinuationWide;
            var candidateBits = Vector256.Equals(
                Vector256.BitwiseAnd(current, anchorMask),
                maskedAnchor).ExtractMostSignificantBits();

            if (overlongLeadBits == 0 &&
                unsupportedBits == 0 &&
                continuationBits == expectedContinuationBits)
            {
                CountCandidates(
                    input,
                    literal,
                    offset,
                    candidateBits,
                    maxStart,
                    ref nextMatchStart,
                    ref count);
                expectedContinuationCarry = (uint)(expectedContinuationWide >> 32);
                offset += Vector256<byte>.Count;
                continue;
            }

            var stop = unsupportedBits == 0
                ? Vector256<byte>.Count
                : BitOperations.TrailingZeroCount(unsupportedBits);
            var prefixBits = stop == Vector256<byte>.Count
                ? uint.MaxValue
                : (1u << stop) - 1u;
            var invalidBits = (overlongLeadBits | (continuationBits ^ expectedContinuationBits)) & prefixBits;
            if (invalidBits != 0 || stop == Vector256<byte>.Count)
            {
                return false;
            }

            CountCandidates(
                input,
                literal,
                offset,
                candidateBits & prefixBits,
                maxStart,
                ref nextMatchStart,
                ref count);
            offset += stop;
            if ((expectedContinuationBits & (1u << stop)) != 0 ||
                Rune.DecodeFromUtf8(input[offset..], out _, out var bytesConsumed) != OperationStatus.Done)
            {
                return false;
            }

            offset += bytesConsumed;
            expectedContinuationCarry = 0;
        }

        while (expectedContinuationCarry != 0)
        {
            if ((uint)offset >= (uint)input.Length || (input[offset] & 0xC0) != 0x80)
            {
                return false;
            }

            CountCandidate(
                input,
                literal,
                offset,
                maxStart,
                ref nextMatchStart,
                ref count);
            offset++;
            expectedContinuationCarry >>= 1;
        }

        while (offset < input.Length)
        {
            var value = input[offset];
            if (value < 0x80)
            {
                offset++;
                continue;
            }

            if (Rune.DecodeFromUtf8(input[offset..], out _, out var bytesConsumed) != OperationStatus.Done)
            {
                return false;
            }

            if (bytesConsumed == 2)
            {
                CountCandidate(
                    input,
                    literal,
                    offset + 1,
                    maxStart,
                    ref nextMatchStart,
                    ref count);
            }

            offset += bytesConsumed;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CountCandidates(
        ReadOnlySpan<byte> input,
        Literal literal,
        int blockStart,
        uint candidateBits,
        int maxStart,
        ref int nextMatchStart,
        ref int count)
    {
        while (candidateBits != 0)
        {
            var anchorIndex = blockStart + BitOperations.TrailingZeroCount(candidateBits);
            CountCandidate(
                input,
                literal,
                anchorIndex,
                maxStart,
                ref nextMatchStart,
                ref count);
            candidateBits &= candidateBits - 1;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void CountCandidate(
        ReadOnlySpan<byte> input,
        Literal literal,
        int anchorIndex,
        int maxStart,
        ref int nextMatchStart,
        ref int count)
    {
        var candidate = anchorIndex - literal.AnchorByteOffset;
        if (candidate < nextMatchStart ||
            (uint)candidate > (uint)maxStart ||
            (literal.AnchorPreviousAscii >= 0 &&
             input[anchorIndex - 2] != (byte)literal.AnchorPreviousAscii) ||
            !literal.MatchesAt(input, candidate))
        {
            return;
        }

        count++;
        nextMatchStart = candidate + literal.ByteLength;
    }

    private static int CountSingleLiteral(ReadOnlySpan<byte> input, Literal literal)
    {
        var count = 0;
        var nextStart = 0;
        while (nextStart <= input.Length - literal.ByteLength)
        {
            var candidate = literal.FindNext(input, nextStart);
            if (candidate == int.MaxValue)
            {
                break;
            }

            count++;
            nextStart = candidate + literal.ByteLength;
        }

        return count;
    }

    public bool UsesCorrelatedPrefilter => _correlatedPrefilter.HasValue;

    public bool UsesDirectSingleLiteralSearch => _literals.Length == 1;

    private int CountWithCorrelatedPrefilter(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var state = new PreparedMultiLiteralScanState(0, 0, 0);
        while (_correlatedPrefilter.TryFindNextCandidate(input, ref state, out var candidate))
        {
            for (var literalIndex = 0; literalIndex < _literals.Length; literalIndex++)
            {
                ref readonly var literal = ref _literals[literalIndex];
                if (candidate > input.Length - literal.ByteLength || !literal.MatchesAt(input, candidate))
                {
                    continue;
                }

                count++;
                state = new PreparedMultiLiteralScanState(candidate + literal.ByteLength, 0, 0);
                break;
            }
        }

        return count;
    }

    private int CountWithSharedAnchor(
        ReadOnlySpan<byte> input,
        SearchValues<byte> anchorSearchValues,
        byte[] anchorLiteralMasks)
    {
        var count = 0;
        var nextStart = 0;
        var searchIndex = _sharedAnchorByteOffset;
        while (searchIndex < input.Length)
        {
            var relative = input[searchIndex..].IndexOfAny(anchorSearchValues);
            if (relative < 0)
            {
                break;
            }

            var anchorIndex = searchIndex + relative;
            searchIndex = anchorIndex + 1;
            var candidate = anchorIndex - _sharedAnchorByteOffset;
            if (candidate < nextStart)
            {
                continue;
            }

            var literalMask = anchorLiteralMasks[input[anchorIndex]];
            while (literalMask != 0)
            {
                var literalIndex = BitOperations.TrailingZeroCount((uint)literalMask);
                ref readonly var literal = ref _literals[literalIndex];
                if (candidate <= input.Length - literal.ByteLength && literal.MatchesAt(input, candidate))
                {
                    count++;
                    nextStart = candidate + literal.ByteLength;
                    searchIndex = nextStart + _sharedAnchorByteOffset;
                    break;
                }

                literalMask &= (byte)(literalMask - 1);
            }
        }

        return count;
    }

    private readonly record struct Literal(
        Unit[] Units,
        byte[] Bytes,
        int ByteLength,
        int AnchorPreviousAscii,
        int AnchorByteOffset,
        byte AnchorFirst,
        byte AnchorSecond)
    {
        public int FindNext(ReadOnlySpan<byte> input, int startIndex)
        {
            var maxStart = input.Length - ByteLength;
            if ((uint)startIndex > (uint)maxStart)
            {
                return int.MaxValue;
            }

            var searchIndex = startIndex + AnchorByteOffset;
            while (searchIndex < input.Length)
            {
                var relative = input[searchIndex..].IndexOfAny(AnchorFirst, AnchorSecond);
                if (relative < 0)
                {
                    return int.MaxValue;
                }

                var anchorIndex = searchIndex + relative;
                var candidate = anchorIndex - AnchorByteOffset;
                searchIndex = anchorIndex + 1;
                if ((uint)candidate <= (uint)maxStart && MatchesAt(input, candidate))
                {
                    return candidate;
                }
            }

            return int.MaxValue;
        }

        public bool MatchesAt(ReadOnlySpan<byte> input, int candidate)
        {
            for (var i = 0; i < Units.Length; i++)
            {
                ref readonly var unit = ref Units[i];
                var offset = candidate + unit.ByteOffset;
                if (unit.IsAscii)
                {
                    if (input[offset] != (byte)unit.First)
                    {
                        return false;
                    }

                    continue;
                }

                var actual = BinaryPrimitives.ReadUInt16LittleEndian(input[offset..]);
                if (actual != unit.First && actual != unit.Second)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private readonly record struct Unit(int ByteOffset, ushort First, ushort Second, bool IsAscii)
    {
        public static Unit CreateAscii(int byteOffset, byte value) =>
            new(byteOffset, value, value, IsAscii: true);

        public static Unit CreatePair(int byteOffset, ushort first, ushort second) =>
            new(byteOffset, first, second, IsAscii: false);
    }
}
