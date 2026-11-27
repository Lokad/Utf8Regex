using System.Buffers;
using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
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
    private readonly PreparedByteSet? _sharedAnchorSearch;
    private readonly byte[]? _sharedAnchorLiteralMasks;
    private readonly int _sharedAnchorByteOffset;
    private readonly PreparedMultiLiteralPackedNibbleSimdPrefilter _correlatedPrefilter;

    private readonly record struct ConstructionState(Literal[] Literals, PreparedByteSet? AnchorSearch,
        byte[]? AnchorLiteralMasks, int AnchorByteOffset, PreparedMultiLiteralPackedNibbleSimdPrefilter CorrelatedPrefilter);

    private Utf8InvariantCyrillicLiteralCountStrategy(ConstructionState state)
    {
        _literals = state.Literals;
        _sharedAnchorSearch = state.AnchorSearch;
        _sharedAnchorLiteralMasks = state.AnchorLiteralMasks;
        _sharedAnchorByteOffset = state.AnchorByteOffset;
        _correlatedPrefilter = state.CorrelatedPrefilter;
    }

    private static ConstructionState PrepareState(Literal[] literals, Utf8ConstructionBudget budget)
    {
        var prefilter = default(PreparedMultiLiteralPackedNibbleSimdPrefilter);
        if (literals.Length > 1)
        {
            // Only the prefilter consumes this borrowed reference vector.
            var bytes = budget.AllocateArray<byte[]>(literals.Length, false);
            try
            {
                budget.Checkpoint();
                for (var i = 0; i < literals.Length; i++) bytes[i] = literals[i].Bytes;
                prefilter = PreparedMultiLiteralPackedNibbleSimdPrefilter.CreateInvariantCyrillicIgnoreCase(bytes);
            }
            finally { budget.ReleaseArray(bytes, false); }
        }
        var state = new ConstructionState(literals, null, null, -1, prefilter);
        // SIMD preparation can decline on other CPUs. Only then is the shared
        // anchor fallback consumed; do not retain a second unused search table.
        if (literals.Length == 1 || prefilter.HasValue) return state;

        var sharedAnchorByteOffset = literals[0].AnchorByteOffset;
        budget.Checkpoint();
        for (var i = 1; i < literals.Length; i++)
        {
            if (literals[i].AnchorByteOffset != sharedAnchorByteOffset)
            {
                return state;
            }
        }

        budget.Checkpoint();
        Span<bool> seen = stackalloc bool[256];
        seen.Clear();
        Span<byte> anchorBytes = stackalloc byte[MaximumAlternatives * 2];
        var literalMasks = budget.AllocateArray<byte>(256, false);
        var anchorByteCount = 0;
        budget.Checkpoint();
        for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
        {
            ref readonly var literal = ref literals[literalIndex];
            AddSharedAnchor(literal.AnchorFirst, literalIndex, seen, anchorBytes, literalMasks, ref anchorByteCount);
            AddSharedAnchor(literal.AnchorSecond, literalIndex, seen, anchorBytes, literalMasks, ref anchorByteCount);
        }

        return new(literals, PreparedByteSet.CreateOwned(anchorBytes[..anchorByteCount]),
            literalMasks, sharedAnchorByteOffset, prefilter);
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
        => TryCreate(new Utf8ConstructionPattern(pattern), options, matchTimeout, out strategy);

    internal static bool TryCreate(
        Utf8ConstructionPattern pattern,
        RegexOptions options,
        TimeSpan matchTimeout,
        [NotNullWhen(true)] out Utf8InvariantCyrillicLiteralCountStrategy? strategy)
    {
        strategy = null;
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
            return false;
        }

        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget; budget.EnterDepth();
        try
        {
            budget.Checkpoint();
            var count = 1;
            for (var i = 0; i < pattern.Length; i++) if (pattern[i] == '|') count++;
            if (count > MaximumAlternatives) return false;
            var literals = budget.AllocateArray<Literal>(count, false);
            var start = 0;
            for (var index = 0; index < count; index++)
            {
                var end = pattern.IndexOf('|', start);
                var length = (end < 0 ? pattern.Length : end) - start;
                // The delimiter scan is bounded by the full-pattern admission.
                // Literal slices borrow text; no Split strings or array survive.
                if (!TryCreateLiteral(pattern, start, length, budget, out literals[index])) return false;
                start += length + 1;
            }
            var state = PrepareState(literals, budget);
            strategy = budget.AllocateObject(state, static value => new Utf8InvariantCyrillicLiteralCountStrategy(value));
            scope.Complete();
            return true;
        }
        finally { budget.LeaveDepth(); }
    }

    private static bool TryCreateLiteral(Utf8ConstructionPattern literal, int start, int length,
        Utf8ConstructionBudget budget, out Literal prepared)
    {
        if (length == 0)
        {
            prepared = default;
            return false;
        }

        var unitCount = length;
        if (literal.IsUtf8)
        {
            unitCount = 0;
            for (var offset = start; offset < start + length; unitCount++)
            { budget.Checkpoint(); literal.ReadScalar(ref offset); }
        }
        var units = budget.AllocateArray<Unit>(unitCount, false);
        var byteOffset = 0;
        var anchorPreviousAscii = -1;
        var anchorByteOffset = -1;
        var anchorFirst = (byte)0;
        var anchorSecond = (byte)0;
        budget.Checkpoint();
        var sourceOffset = start;
        var previousValue = -1;
        for (var i = 0; i < units.Length; i++)
        {
            var previous = previousValue;
            var value = literal.ReadScalar(ref sourceOffset);
            previousValue = value;
            if (value <= 0x7F)
            {
                if (char.IsAsciiLetter((char)value) || Utf8RegexSyntax.IsRegexMetaCharacter((char)value))
                {
                    prepared = default;
                    return false;
                }

                units[i] = Unit.CreateAscii(byteOffset, (byte)value);
                byteOffset++;
                continue;
            }

            if (value > char.MaxValue || !Utf8InvariantCyrillicCase.TryGetPair((char)value, out var upper, out var lower))
            {
                prepared = default;
                return false;
            }

            var upperUtf8 = Utf8InvariantCyrillicCase.EncodeTwoByteScalar(upper);
            var lowerUtf8 = Utf8InvariantCyrillicCase.EncodeTwoByteScalar(lower);
            units[i] = Unit.CreatePair(byteOffset, upperUtf8, lowerUtf8);
            var previousAscii = i > 0 && previous <= 0x7F
                ? previous
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

        var bytes = literal.IsUtf8 ? budget.AllocateArray<byte>(length, false) : budget.EncodeUtf8(literal.Text.AsSpan(start, length));
        if (literal.IsUtf8) { budget.Checkpoint(); literal.Utf8.Slice(start, length).CopyTo(bytes); }
        prepared = new Literal(
            units,
            bytes,
            byteOffset,
            anchorPreviousAscii,
            anchorByteOffset,
            anchorFirst,
            anchorSecond);
        return true;
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        if (_literals.Length == 1)
        {
            return CountSingleLiteral(input, _literals[0]);
        }

        if (_correlatedPrefilter.HasValue)
        {
            return CountWithCorrelatedPrefilter(input);
        }

        if (_sharedAnchorSearch is { } sharedAnchorSearch &&
            _sharedAnchorLiteralMasks is { } sharedAnchorLiteralMasks)
        {
            return CountWithSharedAnchor(input, sharedAnchorSearch, sharedAnchorLiteralMasks);
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
        PreparedByteSet anchorSearch,
        byte[] anchorLiteralMasks)
    {
        var count = 0;
        var nextStart = 0;
        var searchIndex = _sharedAnchorByteOffset;
        while (searchIndex < input.Length)
        {
            var relative = anchorSearch.IndexOf(input[searchIndex..]);
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
