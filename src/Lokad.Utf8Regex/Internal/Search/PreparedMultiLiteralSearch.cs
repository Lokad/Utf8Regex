using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Search;

internal enum PreparedMultiLiteralKind : byte
{
    None = 0,
    ExactDirect = 1,
    ExactTrie = 2,
    ExactAutomaton = 3,
    ExactPacked = 4,
    AsciiIgnoreCase = 5,
    ExactEarliest = 6,
}

internal readonly partial struct PreparedMultiLiteralSearch
{
    private const int TrieThreshold = 8;
    internal const int AutomatonThreshold = 5;

    public PreparedMultiLiteralKind Kind { get; }

    public bool HasValue => Kind != PreparedMultiLiteralKind.None;

    public byte[][] Literals { get; }

    public int ShortestLength => Kind switch
    {
        PreparedMultiLiteralKind.ExactDirect => ExactSearch.SearchData.ShortestLength,
        PreparedMultiLiteralKind.ExactTrie => TrieSearch.ShortestLength,
        PreparedMultiLiteralKind.ExactAutomaton => AutomatonSearch.ShortestLength,
        PreparedMultiLiteralKind.ExactPacked => PackedSearch.ShortestLength,
        PreparedMultiLiteralKind.ExactEarliest => EarliestSearch.ShortestLength,
        PreparedMultiLiteralKind.AsciiIgnoreCase => IgnoreCaseSearch.ShortestLength,
        _ => int.MaxValue,
    };

    public PreparedLiteralSetSearch ExactSearch { get; }

    public PreparedMultiLiteralTrieSearch TrieSearch { get; }

    public PreparedMultiLiteralAutomatonSearch AutomatonSearch { get; }

    public PreparedMultiLiteralEarliestExactSearch EarliestSearch { get; }

    public PreparedMultiLiteralPackedSearch PackedSearch { get; }

    public PreparedAsciiIgnoreCaseLiteralSetSearch IgnoreCaseSearch { get; }

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        return Kind switch
        {
            PreparedMultiLiteralKind.ExactDirect => ExactSearch.IndexOf(input),
            PreparedMultiLiteralKind.ExactTrie => TrieSearch.IndexOf(input),
            PreparedMultiLiteralKind.ExactAutomaton => AutomatonSearch.IndexOf(input),
            PreparedMultiLiteralKind.ExactPacked => PackedSearch.IndexOf(input),
            PreparedMultiLiteralKind.ExactEarliest => EarliestSearch.IndexOf(input),
            PreparedMultiLiteralKind.AsciiIgnoreCase => IgnoreCaseSearch.IndexOf(input),
            _ => -1,
        };
    }

    public bool TryFindFirstMatch(ReadOnlySpan<byte> input, out int index, out int matchedLength, out int literalId)
    {
        index = -1;
        matchedLength = 0;
        literalId = -1;

        switch (Kind)
        {
            case PreparedMultiLiteralKind.ExactAutomaton:
                return AutomatonSearch.TryFindFirstMatch(input, out index, out matchedLength, out literalId);

            case PreparedMultiLiteralKind.ExactPacked:
                return PackedSearch.TryFindFirstMatch(input, out index, out matchedLength, out literalId);

            case PreparedMultiLiteralKind.ExactEarliest:
            case PreparedMultiLiteralKind.ExactDirect:
            case PreparedMultiLiteralKind.ExactTrie:
            case PreparedMultiLiteralKind.AsciiIgnoreCase:
                index = IndexOf(input);
                return index >= 0 && TryGetMatchedLiteralInfo(input, index, out matchedLength, out literalId);

            default:
                return false;
        }
    }

    public bool TryFindLastMatch(ReadOnlySpan<byte> input, out int index, out int matchedLength, out int literalId)
    {
        index = LastIndexOf(input);
        matchedLength = 0;
        literalId = -1;

        return index >= 0 && TryGetMatchedLiteralInfo(input, index, out matchedLength, out literalId);
    }

    public bool TryFindNextNonOverlappingMatch(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength,
        out int literalId)
    {
        index = -1;
        matchedLength = 0;
        literalId = -1;

        if ((uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        if (Kind == PreparedMultiLiteralKind.ExactAutomaton)
        {
            return AutomatonSearch.TryFindNextNonOverlappingMatch(input, ref state, out index, out matchedLength, out literalId);
        }

        if (Kind == PreparedMultiLiteralKind.ExactPacked)
        {
            return PackedSearch.TryFindNextNonOverlappingMatch(input, ref state, out index, out matchedLength, out literalId);
        }

        if (Kind == PreparedMultiLiteralKind.ExactDirect &&
            ExactSearch.TryFindFirstMatchWithLength(input[state.NextStart..], out var fusedRelative, out matchedLength))
        {
            index = state.NextStart + fusedRelative;
            // R03: the fused search reports no identity, so resolve the winning branch in
            // alternation order; a miss keeps -1 and declines branch-sensitive consumers.
            if (!TryGetMatchedLiteralInfo(input, index, out _, out literalId))
            {
                literalId = -1;
            }

            state = new PreparedMultiLiteralScanState(index + matchedLength, index + matchedLength, 0);
            return true;
        }

        if (Kind == PreparedMultiLiteralKind.ExactDirect)
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        if (!TryFindFirstMatch(input[state.NextStart..], out var relativeIndex, out matchedLength, out literalId))
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        index = state.NextStart + relativeIndex;
        state = new PreparedMultiLiteralScanState(index + matchedLength, index + matchedLength, 0);
        return true;
    }

    public bool TryFindNextNonOverlappingLength(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength)
    {
        index = -1;
        matchedLength = 0;

        if ((uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        if (Kind == PreparedMultiLiteralKind.ExactAutomaton)
        {
            return AutomatonSearch.TryFindNextNonOverlappingMatch(input, ref state, out index, out matchedLength, out _);
        }

        if (Kind == PreparedMultiLiteralKind.ExactPacked)
        {
            return PackedSearch.TryFindNextNonOverlappingLength(input, ref state, out index, out matchedLength);
        }

        if (Kind == PreparedMultiLiteralKind.ExactDirect &&
            ExactSearch.TryFindFirstMatchWithLength(input[state.NextStart..], out var fusedRelative, out matchedLength))
        {
            index = state.NextStart + fusedRelative;
            state = new PreparedMultiLiteralScanState(index + matchedLength, index + matchedLength, 0);
            return true;
        }

        if (Kind == PreparedMultiLiteralKind.AsciiIgnoreCase &&
            IgnoreCaseSearch.TryFindFirstMatchWithLength(input[state.NextStart..], out var foldedRelative, out matchedLength))
        {
            index = state.NextStart + foldedRelative;
            state = new PreparedMultiLiteralScanState(index + matchedLength, index + matchedLength, 0);
            return true;
        }

        if (Kind == PreparedMultiLiteralKind.ExactEarliest)
        {
            return EarliestSearch.TryFindNextNonOverlappingLength(input, ref state, out index, out matchedLength);
        }

        if (Kind is PreparedMultiLiteralKind.ExactDirect or PreparedMultiLiteralKind.AsciiIgnoreCase)
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        var slice = input[state.NextStart..];
        var relativeIndex = IndexOf(slice);
        if (relativeIndex < 0)
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        index = state.NextStart + relativeIndex;
        if (!TryGetMatchedLiteralLength(input, index, out matchedLength))
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            index = -1;
            return false;
        }

        state = new PreparedMultiLiteralScanState(index + matchedLength, index + matchedLength, 0);
        return true;
    }

    public bool TryFindNextOverlappingMatch(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength,
        out int literalId)
    {
        index = -1;
        matchedLength = 0;
        literalId = -1;

        if ((uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        if (Kind == PreparedMultiLiteralKind.ExactAutomaton)
        {
            return AutomatonSearch.TryFindNextOverlappingMatch(input, ref state, out index, out matchedLength, out literalId);
        }

        if (Kind == PreparedMultiLiteralKind.ExactPacked)
        {
            return PackedSearch.TryFindNextOverlappingMatch(input, ref state, out index, out matchedLength, out literalId);
        }

        if (!TryFindFirstMatch(input[state.NextStart..], out var relativeIndex, out matchedLength, out literalId))
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        index = state.NextStart + relativeIndex;
        state = PreparedMultiLiteralScanState.AfterOverlappingMatch(index);
        return true;
    }

    public int LastIndexOf(ReadOnlySpan<byte> input)
    {
        return Kind switch
        {
            PreparedMultiLiteralKind.ExactDirect => ExactSearch.LastIndexOf(input),
            PreparedMultiLiteralKind.ExactTrie => TrieSearch.LastIndexOf(input),
            PreparedMultiLiteralKind.ExactAutomaton => AutomatonSearch.LastIndexOf(input),
            PreparedMultiLiteralKind.ExactPacked => PackedSearch.LastIndexOf(input),
            PreparedMultiLiteralKind.ExactEarliest => EarliestSearch.LastIndexOf(input),
            PreparedMultiLiteralKind.AsciiIgnoreCase => IgnoreCaseSearch.LastIndexOf(input),
            _ => -1,
        };
    }

    public bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, out int matchedLength)
    {
        matchedLength = 0;
        return Kind switch
        {
            PreparedMultiLiteralKind.ExactDirect => ExactSearch.TryGetMatchedLiteralLength(input, index, out matchedLength),
            PreparedMultiLiteralKind.ExactTrie => TrieSearch.TryGetMatchedLiteralLength(input, index, out matchedLength),
            PreparedMultiLiteralKind.ExactAutomaton => TrieSearch.TryGetMatchedLiteralLength(input, index, out matchedLength),
            PreparedMultiLiteralKind.ExactPacked => PackedSearch.TryGetMatchedLiteralLength(input, index, out matchedLength),
            PreparedMultiLiteralKind.ExactEarliest => EarliestSearch.TryGetMatchedLiteralLength(input, index, out matchedLength),
            PreparedMultiLiteralKind.AsciiIgnoreCase => IgnoreCaseSearch.TryGetMatchedLiteralLength(input, index, out matchedLength),
            _ => false,
        };
    }

    public bool TryGetMatchedLiteralInfo(ReadOnlySpan<byte> input, int index, out int matchedLength, out int literalId)
    {
        matchedLength = 0;
        literalId = -1;

        switch (Kind)
        {
            case PreparedMultiLiteralKind.ExactDirect:
            case PreparedMultiLiteralKind.ExactEarliest:
            case PreparedMultiLiteralKind.AsciiIgnoreCase:
                for (var i = 0; i < Literals.Length; i++)
                {
                    var literal = Literals[i];
                    if ((uint)index >= (uint)input.Length || literal.Length > input.Length - index)
                    {
                        continue;
                    }

                    var matches = Kind == PreparedMultiLiteralKind.AsciiIgnoreCase
                        ? AsciiSearch.MatchesIgnoreCase(input.Slice(index, literal.Length), literal)
                        : input.Slice(index, literal.Length).SequenceEqual(literal);
                    if (matches)
                    {
                        matchedLength = literal.Length;
                        literalId = i;
                        return true;
                    }
                }

                return false;

            case PreparedMultiLiteralKind.ExactTrie:
                return TrieSearch.TryGetMatchedLiteralInfo(input, index, out matchedLength, out literalId);

            case PreparedMultiLiteralKind.ExactAutomaton:
                if (AutomatonSearch.TryGetMatchedLiteralInfo(input, index, out matchedLength, out literalId))
                {
                    return true;
                }

                return TrieSearch.TryGetMatchedLiteralInfo(input, index, out matchedLength, out literalId);

            case PreparedMultiLiteralKind.ExactPacked:
                return PackedSearch.TryGetMatchedLiteralInfo(input, index, out matchedLength, out literalId);

            default:
                return false;
        }
    }


}

internal readonly partial struct PreparedMultiLiteralPackedSearch
{
    private PreparedMultiLiteralPackedSearch(
        PreparedByteSearch discriminatorSearch,
        byte[][] literals,
        byte[][] literalByDiscriminator,
        int[] literalIdByDiscriminator,
        byte[] secondByteByDiscriminator,
        byte[] thirdByteByDiscriminator,
        bool hasSecondByteAnchor,
        bool hasThirdByteAnchor,
        int discriminatorOffset,
        int shortestLength)
    {
        DiscriminatorSearch = discriminatorSearch;
        Literals = literals;
        LiteralByDiscriminator = literalByDiscriminator;
        LiteralIdByDiscriminator = literalIdByDiscriminator;
        SecondByteByDiscriminator = secondByteByDiscriminator;
        ThirdByteByDiscriminator = thirdByteByDiscriminator;
        HasSecondByteAnchor = hasSecondByteAnchor;
        HasThirdByteAnchor = hasThirdByteAnchor;
        DiscriminatorOffset = discriminatorOffset;
        ShortestLength = shortestLength;
    }

    public PreparedByteSearch DiscriminatorSearch { get; }

    public byte[][] Literals { get; }

    public byte[][] LiteralByDiscriminator { get; }

    public int[] LiteralIdByDiscriminator { get; }

    public byte[] SecondByteByDiscriminator { get; }

    public byte[] ThirdByteByDiscriminator { get; }

    public bool HasSecondByteAnchor { get; }

    public bool HasThirdByteAnchor { get; }

    public int DiscriminatorOffset { get; }

    public int ShortestLength { get; }

    public static PreparedMultiLiteralPackedSearch Create(byte[][] literals)
    {
        if (!TryCreate(literals, out var search))
        {
            throw new InvalidOperationException("Packed literal search requires a unique discriminator offset.");
        }

        return search;
    }

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        return TryFindFirstMatch(input, out var index, out _, out _) ? index : -1;
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
            var discriminatorEnd = end + DiscriminatorOffset;
            if (discriminatorEnd <= 0)
            {
                return -1;
            }

            var relative = DiscriminatorSearch.LastIndexOf(input[..discriminatorEnd]);
            if (relative < 0)
            {
                return -1;
            }

            var candidate = relative - DiscriminatorOffset;
            if (candidate >= 0)
            {
                var literal = LiteralByDiscriminator[input[relative]];
                if (literal is not null && MatchesAnchorAndLiteral(input, relative, candidate, literal))
                {
                    return candidate;
                }
            }

            // Exclude the rejected start. Keeping it in the next window can
            // rediscover the same discriminator indefinitely on a miss.
            end = relative - DiscriminatorOffset;
        }

        return -1;
    }

    public bool TryFindFirstMatch(ReadOnlySpan<byte> input, out int index, out int matchedLength, out int literalId)
    {
        index = -1;
        matchedLength = 0;
        literalId = -1;

        if (ShortestLength == int.MaxValue)
        {
            return false;
        }

        if (ShortestLength == 0)
        {
            index = 0;
            return true;
        }

        var searchIndex = 0;
        while (searchIndex <= input.Length - ShortestLength)
        {
            var discriminatorStart = searchIndex + DiscriminatorOffset;
            if (discriminatorStart >= input.Length)
            {
                return false;
            }

            var relative = DiscriminatorSearch.IndexOf(input[discriminatorStart..]);
            if (relative < 0)
            {
                return false;
            }

            var discriminatorIndex = discriminatorStart + relative;
            var candidate = discriminatorIndex - DiscriminatorOffset;
            var discriminator = input[discriminatorIndex];
            var literal = LiteralByDiscriminator[discriminator];
            if (literal is not null && MatchesAnchorAndLiteral(input, discriminatorIndex, candidate, literal))
            {
                index = candidate;
                matchedLength = literal.Length;
                literalId = LiteralIdByDiscriminator[discriminator];
                return true;
            }

            searchIndex = candidate + 1;
        }

        return false;
    }

    public bool TryFindNextNonOverlappingMatch(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength,
        out int literalId)
    {
        index = -1;
        matchedLength = 0;
        literalId = -1;

        if ((uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        if (!TryFindFirstMatch(input[state.NextStart..], out var relativeIndex, out matchedLength, out literalId))
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        index = state.NextStart + relativeIndex;
        state = new PreparedMultiLiteralScanState(index + matchedLength, index + matchedLength, 0);
        return true;
    }

    public bool TryFindNextNonOverlappingLength(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength)
    {
        index = -1;
        matchedLength = 0;

        if ((uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        if (!TryFindFirstMatch(input[state.NextStart..], out var relativeIndex, out matchedLength, out _))
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        index = state.NextStart + relativeIndex;
        state = new PreparedMultiLiteralScanState(index + matchedLength, index + matchedLength, 0);
        return true;
    }

    public bool TryFindNextOverlappingMatch(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength,
        out int literalId)
    {
        index = -1;
        matchedLength = 0;
        literalId = -1;

        if ((uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        if (!TryFindFirstMatch(input[state.NextStart..], out var relativeIndex, out matchedLength, out literalId))
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        index = state.NextStart + relativeIndex;
        state = PreparedMultiLiteralScanState.AfterOverlappingMatch(index);
        return true;
    }

    public bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, out int matchedLength)
    {
        return TryGetMatchedLiteralInfo(input, index, out matchedLength, out _);
    }

    public bool TryGetMatchedLiteralInfo(ReadOnlySpan<byte> input, int index, out int matchedLength, out int literalId)
    {
        matchedLength = 0;
        literalId = -1;
        if ((uint)index >= (uint)input.Length || index > input.Length - ShortestLength)
        {
            return false;
        }

        var discriminatorIndex = index + DiscriminatorOffset;
        if ((uint)discriminatorIndex >= (uint)input.Length)
        {
            return false;
        }

        var discriminator = input[discriminatorIndex];
        var literal = LiteralByDiscriminator[discriminator];
        if (literal is null || !MatchesAnchorAndLiteral(input, discriminatorIndex, index, literal))
        {
            return false;
        }

        matchedLength = literal.Length;
        literalId = LiteralIdByDiscriminator[discriminator];
        return true;
    }

    private static bool LiteralMatchesAt(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal)
    {
        return (uint)index <= (uint)input.Length &&
            literal.Length <= input.Length - index &&
            input.Slice(index, literal.Length).SequenceEqual(literal);
    }

    private bool MatchesAnchorAndLiteral(ReadOnlySpan<byte> input, int discriminatorIndex, int candidate, ReadOnlySpan<byte> literal)
    {
        if (HasSecondByteAnchor)
        {
            var secondByteIndex = discriminatorIndex + 1;
            if ((uint)secondByteIndex >= (uint)input.Length ||
                input[secondByteIndex] != SecondByteByDiscriminator[input[discriminatorIndex]])
            {
                return false;
            }
        }

        if (HasThirdByteAnchor)
        {
            var thirdByteIndex = discriminatorIndex + 2;
            if ((uint)thirdByteIndex >= (uint)input.Length ||
                input[thirdByteIndex] != ThirdByteByDiscriminator[input[discriminatorIndex]])
            {
                return false;
            }
        }

        return LiteralMatchesAt(input, candidate, literal);
    }
}

internal readonly struct PreparedMultiLiteralRootByteFamilySearch
{
    public PreparedMultiLiteralRootByteFamilySearch(byte[][] literals)
    {
        ExactSearch = new PreparedLiteralSetSearch(literals);
        ShortestLength = ExactSearch.SearchData.ShortestLength;
    }

    public PreparedLiteralSetSearch ExactSearch { get; }

    public int ShortestLength { get; }

    public bool TryFindNextNonOverlappingLength(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength)
    {
        index = -1;
        matchedLength = 0;

        if ((uint)state.NextStart > (uint)input.Length || ShortestLength == int.MaxValue)
        {
            return false;
        }

        if (!ExactSearch.TryFindFirstMatchWithLength(input[state.NextStart..], out var relativeIndex, out matchedLength))
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        index = state.NextStart + relativeIndex;
        state = new PreparedMultiLiteralScanState(index + matchedLength, index + matchedLength, 0);
        return true;
    }
    public bool TryFindNextCandidate(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int candidateIndex)
    {
        candidateIndex = -1;
        if (!TryFindNextNonOverlappingLength(input, ref state, out var index, out _))
        {
            return false;
        }

        candidateIndex = index;
        return true;
    }
}

internal enum PreparedMultiLiteralCandidatePrefilterKind : byte
{
    None = 0,
    RootByte = 1,
    Earliest = 2,
    RareByte = 3,
    OffsetMask = 4,
    NibbleMask = 5,
    PackedNibbleSimd = 6,
    LeadingUtf8Segment = 7,
}

internal readonly struct PreparedMultiLiteralCandidatePrefilter
{
    private PreparedMultiLiteralCandidatePrefilter(
        PreparedMultiLiteralCandidatePrefilterKind kind,
        byte[][] literals,
        int shortestLength,
        int longestLength,
        PreparedMultiLiteralRootByteFamilySearch rootByte,
        PreparedMultiLiteralEarliestExactSearch earliest,
        PreparedMultiLiteralRareBytePrefilter rareByte,
        PreparedMultiLiteralOffsetMaskPrefilter offsetMask,
        PreparedMultiLiteralNibbleMaskPrefilter nibbleMask,
        PreparedMultiLiteralPackedNibbleSimdPrefilter packedNibbleSimd,
        PreparedMultiLiteralLeadingUtf8SegmentPrefilter leadingUtf8Segment)
    {
        Kind = kind;
        Literals = literals;
        ShortestLength = shortestLength;
        LongestLength = longestLength;
        RootByte = rootByte;
        Earliest = earliest;
        RareByte = rareByte;
        OffsetMask = offsetMask;
        NibbleMask = nibbleMask;
        PackedNibbleSimd = packedNibbleSimd;
        LeadingUtf8Segment = leadingUtf8Segment;
    }

    public PreparedMultiLiteralCandidatePrefilterKind Kind { get; }

    public byte[][] Literals { get; }

    public int ShortestLength { get; }

    public int LongestLength { get; }

    public PreparedMultiLiteralRootByteFamilySearch RootByte { get; }

    public PreparedMultiLiteralEarliestExactSearch Earliest { get; }

    public PreparedMultiLiteralRareBytePrefilter RareByte { get; }

    public PreparedMultiLiteralOffsetMaskPrefilter OffsetMask { get; }

    public PreparedMultiLiteralNibbleMaskPrefilter NibbleMask { get; }

    public PreparedMultiLiteralPackedNibbleSimdPrefilter PackedNibbleSimd { get; }

    public PreparedMultiLiteralLeadingUtf8SegmentPrefilter LeadingUtf8Segment { get; }

    public bool HasValue => Kind != PreparedMultiLiteralCandidatePrefilterKind.None;

    public static PreparedMultiLiteralCandidatePrefilter CreateRootByte(byte[][] literals)
    {
        return new PreparedMultiLiteralCandidatePrefilter(
            PreparedMultiLiteralCandidatePrefilterKind.RootByte,
            literals,
            GetShortestLength(literals),
            GetLongestLength(literals),
            new PreparedMultiLiteralRootByteFamilySearch(literals),
            default,
            default,
            default,
            default,
            default,
            default);
    }

    public static PreparedMultiLiteralCandidatePrefilter CreateEarliest(byte[][] literals)
    {
        return new PreparedMultiLiteralCandidatePrefilter(
            PreparedMultiLiteralCandidatePrefilterKind.Earliest,
            literals,
            GetShortestLength(literals),
            GetLongestLength(literals),
            default,
            new PreparedMultiLiteralEarliestExactSearch(literals),
            default,
            default,
            default,
            default,
            default);
    }

    public static PreparedMultiLiteralCandidatePrefilter CreateRareByte(byte[][] literals)
    {
        var rareByte = new PreparedMultiLiteralRareBytePrefilter(literals);
        return new PreparedMultiLiteralCandidatePrefilter(
            rareByte.HasValue ? PreparedMultiLiteralCandidatePrefilterKind.RareByte : PreparedMultiLiteralCandidatePrefilterKind.None,
            literals,
            rareByte.ShortestLength,
            GetLongestLength(literals),
            default,
            default,
            rareByte,
            default,
            default,
            default,
            default);
    }

    public static PreparedMultiLiteralCandidatePrefilter CreateOffsetMask(byte[][] literals)
    {
        var offsetMask = new PreparedMultiLiteralOffsetMaskPrefilter(literals);
        return new PreparedMultiLiteralCandidatePrefilter(
            offsetMask.HasValue ? PreparedMultiLiteralCandidatePrefilterKind.OffsetMask : PreparedMultiLiteralCandidatePrefilterKind.None,
            literals,
            offsetMask.ShortestLength,
            GetLongestLength(literals),
            default,
            default,
            default,
            offsetMask,
            default,
            default,
            default);
    }

    public static PreparedMultiLiteralCandidatePrefilter CreateNibbleMask(byte[][] literals)
    {
        var nibbleMask = new PreparedMultiLiteralNibbleMaskPrefilter(literals);
        return new PreparedMultiLiteralCandidatePrefilter(
            nibbleMask.HasValue ? PreparedMultiLiteralCandidatePrefilterKind.NibbleMask : PreparedMultiLiteralCandidatePrefilterKind.None,
            literals,
            nibbleMask.ShortestLength,
            GetLongestLength(literals),
            default,
            default,
            default,
            default,
            nibbleMask,
            default,
            default);
    }

    public static PreparedMultiLiteralCandidatePrefilter CreatePackedNibbleSimd(byte[][] literals)
    {
        var packedNibbleSimd = new PreparedMultiLiteralPackedNibbleSimdPrefilter(literals);
        return new PreparedMultiLiteralCandidatePrefilter(
            packedNibbleSimd.HasValue ? PreparedMultiLiteralCandidatePrefilterKind.PackedNibbleSimd : PreparedMultiLiteralCandidatePrefilterKind.None,
            literals,
            packedNibbleSimd.ShortestLength,
            GetLongestLength(literals),
            default,
            default,
            default,
            default,
            default,
            packedNibbleSimd,
            default);
    }

    public static PreparedMultiLiteralCandidatePrefilter CreateLeadingUtf8Segment(byte[][] literals)
    {
        var leadingUtf8Segment = new PreparedMultiLiteralLeadingUtf8SegmentPrefilter(literals);
        return new PreparedMultiLiteralCandidatePrefilter(
            leadingUtf8Segment.HasValue ? PreparedMultiLiteralCandidatePrefilterKind.LeadingUtf8Segment : PreparedMultiLiteralCandidatePrefilterKind.None,
            literals,
            leadingUtf8Segment.ShortestLength,
            GetLongestLength(literals),
            default,
            default,
            default,
            default,
            default,
            default,
            leadingUtf8Segment);
    }

    public bool TryFindNextCandidate(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int candidateIndex)
    {
        candidateIndex = -1;
        return Kind switch
        {
            PreparedMultiLiteralCandidatePrefilterKind.RootByte => RootByte.TryFindNextCandidate(input, ref state, out candidateIndex),
            PreparedMultiLiteralCandidatePrefilterKind.Earliest => Earliest.TryFindNextCandidate(input, ref state, out candidateIndex),
            PreparedMultiLiteralCandidatePrefilterKind.RareByte => RareByte.TryFindNextCandidate(input, ref state, out candidateIndex),
            PreparedMultiLiteralCandidatePrefilterKind.OffsetMask => OffsetMask.TryFindNextCandidate(input, ref state, out candidateIndex),
            PreparedMultiLiteralCandidatePrefilterKind.NibbleMask => NibbleMask.TryFindNextCandidate(input, ref state, out candidateIndex),
            PreparedMultiLiteralCandidatePrefilterKind.PackedNibbleSimd => PackedNibbleSimd.TryFindNextCandidate(input, ref state, out candidateIndex),
            PreparedMultiLiteralCandidatePrefilterKind.LeadingUtf8Segment => LeadingUtf8Segment.TryFindNextCandidate(input, ref state, out candidateIndex),
            _ => false,
        };
    }

    public bool TryGetMatchedLength(ReadOnlySpan<byte> input, int candidateIndex, out int matchedLength)
    {
        matchedLength = 0;
        switch (Kind)
        {
            case PreparedMultiLiteralCandidatePrefilterKind.RootByte:
                return RootByte.ExactSearch.TryGetMatchedLiteralLength(input, candidateIndex, out matchedLength);
            case PreparedMultiLiteralCandidatePrefilterKind.Earliest:
                return Earliest.TryGetMatchedLiteralLength(input, candidateIndex, out matchedLength);
            case PreparedMultiLiteralCandidatePrefilterKind.RareByte:
            case PreparedMultiLiteralCandidatePrefilterKind.OffsetMask:
            case PreparedMultiLiteralCandidatePrefilterKind.NibbleMask:
            case PreparedMultiLiteralCandidatePrefilterKind.PackedNibbleSimd:
            case PreparedMultiLiteralCandidatePrefilterKind.LeadingUtf8Segment:
                for (var i = 0; i < Literals.Length; i++)
                {
                    var literal = Literals[i];
                    if ((uint)candidateIndex > (uint)input.Length || literal.Length > input.Length - candidateIndex)
                    {
                        continue;
                    }

                    if (input.Slice(candidateIndex, literal.Length).SequenceEqual(literal))
                    {
                        matchedLength = literal.Length;
                        return true;
                    }
                }
                return false;
            default:
                return false;
        }
    }

    private static int GetShortestLength(byte[][] literals)
    {
        if (literals.Length == 0)
        {
            return int.MaxValue;
        }

        var shortest = int.MaxValue;
        for (var i = 0; i < literals.Length; i++)
        {
            shortest = Math.Min(shortest, literals[i].Length);
        }

        return shortest;
    }

    private static int GetLongestLength(byte[][] literals)
    {
        var longest = 0;
        for (var i = 0; i < literals.Length; i++)
        {
            longest = Math.Max(longest, literals[i].Length);
        }

        return longest;
    }
}

internal readonly partial struct PreparedMultiLiteralLeadingUtf8SegmentPrefilter
{
    public int ShortestLength { get; }

    public byte[][] Segments { get; }

    public bool HasValue => Segments.Length > 0;

    public bool TryFindNextCandidate(ReadOnlySpan<byte> input, ref PreparedMultiLiteralScanState state, out int candidateIndex)
    {
        candidateIndex = -1;
        if (!HasValue || (uint)state.NextStart > (uint)input.Length || ShortestLength == int.MaxValue)
        {
            return false;
        }

        var bestIndex = int.MaxValue;
        for (var i = 0; i < Segments.Length; i++)
        {
            var relative = input[state.NextStart..].IndexOf(Segments[i]);
            if (relative < 0)
            {
                continue;
            }

            var absolute = state.NextStart + relative;
            if (absolute < bestIndex)
            {
                bestIndex = absolute;
            }
        }

        if (bestIndex == int.MaxValue)
        {
            state = new PreparedMultiLiteralScanState(input.Length, 0, 0);
            return false;
        }

        candidateIndex = bestIndex;
        state = new PreparedMultiLiteralScanState(bestIndex + 1, 0, 0);
        return true;
    }

    private static bool TryGetLeadingSegmentLength(ReadOnlySpan<byte> literal, out int segmentLength)
    {
        segmentLength = 0;
        if (literal.Length <= 2)
        {
            return false;
        }

        var first = literal[0];
        if (first is >= 0xE0 and < 0xF0)
        {
            if (literal.Length >= 6 && literal[3] is >= 0xE0 and < 0xF0)
            {
                segmentLength = 6;
                return true;
            }

            segmentLength = 3;
            return true;
        }

        if (first is >= 0xC2 and < 0xE0)
        {
            segmentLength = 2;
            return true;
        }

        return false;
    }
}

internal readonly partial struct PreparedMultiLiteralPackedNibbleSimdPrefilter
{
    private const int MaxMaskLen = 4;
    private const int SseLaneCount = 16;
    private const int AvxLaneCount = 32;

    public static PreparedMultiLiteralPackedNibbleSimdPrefilter CreateAsciiIgnoreCase(byte[][] foldedLiterals)
        => new(foldedLiterals, asciiIgnoreCase: true, invariantCyrillicIgnoreCase: false);

    public static PreparedMultiLiteralPackedNibbleSimdPrefilter CreateInvariantCyrillicIgnoreCase(byte[][] literals)
        => new(literals, asciiIgnoreCase: false, invariantCyrillicIgnoreCase: true);

    public int ShortestLength { get; }

    public int MaskLength { get; }

    public int[] MaskOffsets { get; }

    public Vector256<byte>[] LowMaskVectors { get; }

    public Vector256<byte>[] HighMaskVectors { get; }

    public PreparedByteSearch Search { get; }

    public bool HasValue => Search.Count > 0;

    private static bool IsFoldedAsciiLetter(byte value) => (uint)(value - (byte)'a') <= 'z' - 'a';

    private static void AddMaskValue(Span<byte> lowTable, Span<byte> highTable, byte value, byte bit)
    {
        lowTable[value & 0xF] |= bit;
        highTable[value >> 4] |= bit;
    }

    public bool TryFindNextCandidate(ReadOnlySpan<byte> input, ref PreparedMultiLiteralScanState state, out int candidateIndex)
    {
        candidateIndex = -1;
        if (!HasValue || (uint)state.NextStart > (uint)input.Length || ShortestLength == int.MaxValue)
        {
            return false;
        }

        if (state.ScanIndex != 0)
        {
            var pendingMask = (uint)state.ScanIndex;
            var lane = BitOperations.TrailingZeroCount(pendingMask);
            pendingMask &= pendingMask - 1;
            candidateIndex = state.AutomatonState + lane;
            state = new PreparedMultiLiteralScanState(state.NextStart, (int)pendingMask, state.AutomatonState);
            return true;
        }

        var maxStart = input.Length - ShortestLength;
        var baseIndex = state.NextStart;
        ref var inputRef = ref MemoryMarshal.GetReference(input);

        if (Avx2.IsSupported)
        {
            var vectorLimit = maxStart - (AvxLaneCount - 1);
            var lowNibbleMask = Vector256.Create((byte)0x0F);
            var zero = Vector256<byte>.Zero;
            while (baseIndex <= vectorLimit)
            {
                Vector256<byte> candidates;
                // Long folded families select exactly three rare offsets. Keep that
                // dominant AVX2 path free of the per-block mask loop and repeated array indexing.
                if (MaskLength == 3)
                {
                    candidates = CreateAvxMask(
                        ref inputRef,
                        baseIndex + MaskOffsets[0],
                        LowMaskVectors[0],
                        HighMaskVectors[0],
                        lowNibbleMask);
                    candidates = Avx2.And(candidates, CreateAvxMask(
                        ref inputRef,
                        baseIndex + MaskOffsets[1],
                        LowMaskVectors[1],
                        HighMaskVectors[1],
                        lowNibbleMask));
                    candidates = Avx2.And(candidates, CreateAvxMask(
                        ref inputRef,
                        baseIndex + MaskOffsets[2],
                        LowMaskVectors[2],
                        HighMaskVectors[2],
                        lowNibbleMask));
                }
                else
                {
                    candidates = Vector256.Create((byte)0xFF);
                    for (var offset = 0; offset < MaskLength; offset++)
                    {
                        candidates = Avx2.And(candidates, CreateAvxMask(
                            ref inputRef,
                            baseIndex + MaskOffsets[offset],
                            LowMaskVectors[offset],
                            HighMaskVectors[offset],
                            lowNibbleMask));
                    }
                }

                var zeroMask = Avx2.MoveMask(Avx2.CompareEqual(candidates, zero));
                var matchMask = ~(uint)zeroMask;
                if (matchMask != 0)
                {
                    var lane = BitOperations.TrailingZeroCount(matchMask);
                    candidateIndex = baseIndex + lane;
                    state = new PreparedMultiLiteralScanState(
                        baseIndex + AvxLaneCount,
                        (int)(matchMask & ~(1U << lane)),
                        baseIndex);
                    return true;
                }

                baseIndex += AvxLaneCount;
            }
        }

        var sseVectorLimit = maxStart - (SseLaneCount - 1);
        var sseLowNibbleMask = Vector128.Create((byte)0x0F);
        var sseZero = Vector128<byte>.Zero;
        while (baseIndex <= sseVectorLimit)
        {
            var candidates = Vector128.Create((byte)0xFF);
            for (var offset = 0; offset < MaskLength; offset++)
            {
                var window = Vector128.LoadUnsafe(ref Unsafe.Add(ref inputRef, baseIndex + MaskOffsets[offset]));
                var low = Sse2.And(window, sseLowNibbleMask);
                var high = Sse2.And(
                    Sse2.ShiftRightLogical(window.AsUInt16(), 4).AsByte(),
                    sseLowNibbleMask);
                var lowMask = Ssse3.Shuffle(LowMaskVectors[offset].GetLower(), low);
                var highMask = Ssse3.Shuffle(HighMaskVectors[offset].GetLower(), high);
                candidates = Sse2.And(candidates, Sse2.And(lowMask, highMask));
            }

            var zeroMask = Sse2.MoveMask(Sse2.CompareEqual(candidates, sseZero));
            var matchMask = (~zeroMask) & 0xFFFF;
            if (matchMask != 0)
            {
                var lane = BitOperations.TrailingZeroCount((uint)matchMask);
                candidateIndex = baseIndex + lane;
                state = new PreparedMultiLiteralScanState(
                    baseIndex + SseLaneCount,
                    matchMask & ~(1 << lane),
                    baseIndex);
                return true;
            }

            baseIndex += SseLaneCount;
        }

        while (baseIndex <= maxStart)
        {
            var relative = Search.IndexOf(input[baseIndex..]);
            if (relative < 0)
            {
                state = new PreparedMultiLiteralScanState(input.Length, 0, 0);
                return false;
            }

            var start = baseIndex + relative;
            if ((uint)start > (uint)maxStart)
            {
                state = new PreparedMultiLiteralScanState(input.Length, 0, 0);
                return false;
            }

            ulong candidates = ulong.MaxValue;
            for (var offset = 0; offset < MaskLength; offset++)
            {
                var value = input[start + MaskOffsets[offset]];
                var lowMask = LowMaskVectors[offset].GetLower().GetElement(value & 0xF);
                var highMask = HighMaskVectors[offset].GetLower().GetElement(value >> 4);
                candidates &= (ulong)(lowMask & highMask);
                if (candidates == 0)
                {
                    break;
                }
            }

            if (candidates != 0)
            {
                candidateIndex = start;
                state = new PreparedMultiLiteralScanState(start + 1, 0, 0);
                return true;
            }

            baseIndex = start + 1;
        }

        state = new PreparedMultiLiteralScanState(input.Length, 0, 0);
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<byte> CreateAvxMask(
        ref byte inputRef,
        int inputOffset,
        Vector256<byte> lowMaskVector,
        Vector256<byte> highMaskVector,
        Vector256<byte> lowNibbleMask)
    {
        var window = Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, inputOffset));
        var low = Avx2.And(window, lowNibbleMask);
        var high = Avx2.And(
            Avx2.ShiftRightLogical(window.AsUInt16(), 4).AsByte(),
            lowNibbleMask);
        var lowMask = Avx2.Shuffle(lowMaskVector, low);
        var highMask = Avx2.Shuffle(highMaskVector, high);
        return Avx2.And(lowMask, highMask);
    }
}

internal readonly partial struct PreparedMultiLiteralNibbleMaskPrefilter
{
    private const int MaxMaskLen = 4;

    public int ShortestLength { get; }

    public int MaskLength { get; }

    public ulong[][] LowMasks { get; }

    public ulong[][] HighMasks { get; }

    public PreparedByteSearch Search { get; }

    public bool HasValue => Search.Count > 0;

    public bool TryFindNextCandidate(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int candidateIndex)
    {
        candidateIndex = -1;
        if (!HasValue || (uint)state.NextStart > (uint)input.Length || ShortestLength == int.MaxValue)
        {
            return false;
        }

        var searchFrom = state.NextStart;
        while (searchFrom <= input.Length - ShortestLength)
        {
            var relative = Search.IndexOf(input[searchFrom..]);
            if (relative < 0)
            {
                state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
                return false;
            }

            var start = searchFrom + relative;
            if ((uint)start > (uint)(input.Length - ShortestLength))
            {
                state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
                return false;
            }

            ulong candidates = ulong.MaxValue;
            for (var offset = 0; offset < MaskLength; offset++)
            {
                var value = input[start + offset];
                candidates &= LowMasks[offset][value & 0xF] & HighMasks[offset][value >> 4];
                if (candidates == 0)
                {
                    break;
                }
            }

            if (candidates != 0)
            {
                candidateIndex = start;
                state = new PreparedMultiLiteralScanState(start + 1, start + 1, 0);
                return true;
            }

            searchFrom = start + 1;
        }

        state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
        return false;
    }
}

internal readonly partial struct PreparedMultiLiteralOffsetMaskPrefilter
{
    private const int MaxOffsets = 3;

    private readonly record struct OffsetScore(int Offset, int Cardinality, int FrequencyScore);

    public int ShortestLength { get; }

    public int[] Offsets { get; }

    public byte[][] Masks { get; }

    public PreparedByteSearch Search { get; }

    public bool HasValue => Search.Count > 0;

    public bool TryFindNextCandidate(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int candidateIndex)
    {
        candidateIndex = -1;
        if (!HasValue || (uint)state.NextStart > (uint)input.Length || ShortestLength == int.MaxValue)
        {
            return false;
        }

        var searchFrom = state.NextStart + Offsets[0];
        while (searchFrom < input.Length)
        {
            var relative = Search.IndexOf(input[searchFrom..]);
            if (relative < 0)
            {
                state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
                return false;
            }

            var foundIndex = searchFrom + relative;
            var start = foundIndex - Offsets[0];
            if ((uint)start <= (uint)(input.Length - ShortestLength))
            {
                var matches = true;
                for (var i = 1; i < Offsets.Length; i++)
                {
                    if (Masks[i][input[start + Offsets[i]]] == 0)
                    {
                        matches = false;
                        break;
                    }
                }

                if (matches)
                {
                    candidateIndex = start;
                    state = new PreparedMultiLiteralScanState(foundIndex + 1, foundIndex + 1, 0);
                    return true;
                }
            }

            searchFrom = foundIndex + 1;
        }

        state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
        return false;
    }
}

internal readonly partial struct PreparedMultiLiteralRareBytePrefilter
{
    public PreparedByteSearch Search { get; }

    public int[] OffsetByValue { get; }

    public int ShortestLength { get; }

    public bool HasValue => Search.Count > 0;

    public bool TryFindNextCandidate(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int candidateIndex)
    {
        candidateIndex = -1;

        if (!HasValue || (uint)state.NextStart > (uint)input.Length || ShortestLength == int.MaxValue)
        {
            return false;
        }

        var searchFrom = state.NextStart;
        while (searchFrom < input.Length)
        {
            var relative = Search.IndexOf(input[searchFrom..]);
            if (relative < 0)
            {
                state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
                return false;
            }

            var foundIndex = searchFrom + relative;
            var shift = OffsetByValue[input[foundIndex]];
            if (shift < 0)
            {
                searchFrom = foundIndex + 1;
                continue;
            }

            candidateIndex = Math.Max(0, foundIndex - shift);
            // Advance past the triggering discriminator byte, not the backshifted
            // candidate start, otherwise the same hit can be rediscovered forever.
            state = new PreparedMultiLiteralScanState(foundIndex + 1, foundIndex + 1, 0);
            return true;
        }

        state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
        return false;
    }

    internal static int GetAsciiFrequencyRank(byte value)
    {
        return value switch
        {
            (byte)' ' => 255,
            (byte)'e' or (byte)'E' => 240,
            (byte)'t' or (byte)'T' => 230,
            (byte)'a' or (byte)'A' => 220,
            (byte)'o' or (byte)'O' => 210,
            (byte)'i' or (byte)'I' => 200,
            (byte)'n' or (byte)'N' => 190,
            (byte)'s' or (byte)'S' => 180,
            (byte)'h' or (byte)'H' => 170,
            (byte)'r' or (byte)'R' => 160,
            (byte)'d' or (byte)'D' => 150,
            (byte)'l' or (byte)'L' => 140,
            (byte)'c' or (byte)'C' => 130,
            (byte)'u' or (byte)'U' => 120,
            (byte)'m' or (byte)'M' => 110,
            (byte)'w' or (byte)'W' => 100,
            (byte)'f' or (byte)'F' => 90,
            (byte)'g' or (byte)'G' => 80,
            (byte)'y' or (byte)'Y' => 70,
            (byte)'p' or (byte)'P' => 60,
            (byte)'b' or (byte)'B' => 50,
            (byte)'v' or (byte)'V' => 40,
            (byte)'k' or (byte)'K' => 30,
            (byte)'j' or (byte)'J' => 20,
            (byte)'x' or (byte)'X' => 10,
            (byte)'q' or (byte)'Q' => 5,
            (byte)'z' or (byte)'Z' => 0,
            _ => 15,
        };
    }
}

internal readonly partial struct PreparedMultiLiteralEarliestExactSearch
{
    public byte[][] Literals { get; }

    public int ShortestLength { get; }

    public int LongestLength { get; }

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        var best = int.MaxValue;
        for (var i = 0; i < Literals.Length; i++)
        {
            var found = AsciiSearch.IndexOfExact(input, Literals[i]);
            if ((uint)found >= (uint)best)
            {
                continue;
            }

            if (found < 0)
            {
                continue;
            }

            best = found;
            if (best == 0)
            {
                break;
            }
        }

        return best == int.MaxValue ? -1 : best;
    }

    public int LastIndexOf(ReadOnlySpan<byte> input)
    {
        var best = -1;
        for (var i = 0; i < Literals.Length; i++)
        {
            var found = input.LastIndexOf(Literals[i]);
            if (found > best)
            {
                best = found;
            }
        }

        return best;
    }

    public bool TryGetMatchedLiteralLength(ReadOnlySpan<byte> input, int index, out int matchedLength)
        => TryGetMatchedLiteralInfo(input, index, out matchedLength, out _);

    public bool TryGetMatchedLiteralInfo(ReadOnlySpan<byte> input, int index, out int matchedLength, out int literalId)
    {
        matchedLength = 0;
        literalId = -1;

        for (var i = 0; i < Literals.Length; i++)
        {
            var literal = Literals[i];
            if ((uint)index > (uint)input.Length || literal.Length > input.Length - index)
            {
                continue;
            }

            if (input.Slice(index, literal.Length).SequenceEqual(literal))
            {
                matchedLength = literal.Length;
                literalId = i;
                return true;
            }
        }

        return false;
    }

    public bool TryFindNextNonOverlappingLength(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength)
    {
        index = -1;
        matchedLength = 0;

        if ((uint)state.NextStart > (uint)input.Length || ShortestLength == int.MaxValue)
        {
            return false;
        }

        var slice = input[state.NextStart..];
        var bestRelative = int.MaxValue;
        var bestLength = 0;

        for (var i = 0; i < Literals.Length; i++)
        {
            var literal = Literals[i];
            var relative = AsciiSearch.IndexOfExact(slice, literal);
            if ((uint)relative >= (uint)bestRelative)
            {
                continue;
            }

            if (relative < 0)
            {
                continue;
            }

            bestRelative = relative;
            bestLength = literal.Length;
            if (bestRelative == 0)
            {
                break;
            }
        }

        if (bestRelative == int.MaxValue)
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        index = state.NextStart + bestRelative;
        matchedLength = bestLength;
        state = new PreparedMultiLiteralScanState(index + matchedLength, index + matchedLength, 0);
        return true;
    }

    public bool TryFindNextCandidate(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int candidateIndex)
    {
        candidateIndex = -1;
        if (!TryFindNextNonOverlappingLength(input, ref state, out var index, out _))
        {
            return false;
        }

        candidateIndex = index;
        return true;
    }
}

internal readonly partial struct PreparedMultiLiteralAutomatonSearch
{
    private PreparedMultiLiteralAutomatonSearch(int[] transitions, int[] bestOutputOrders, int[] bestOutputLengths, int[] shortestOutputLengths, int shortestLength, int longestLength)
    {
        Transitions = transitions;
        BestOutputOrders = bestOutputOrders;
        BestOutputLengths = bestOutputLengths;
        ShortestOutputLengths = shortestOutputLengths;
        ShortestLength = shortestLength;
        LongestLength = longestLength;
    }

    public int[] Transitions { get; }

    public int[] BestOutputOrders { get; }

    public int[] BestOutputLengths { get; }

    public int[] ShortestOutputLengths { get; }

    public int ShortestLength { get; }

    public int LongestLength { get; }

    public int StateCount => BestOutputOrders.Length;

    public int RootFanout
    {
        get
        {
            if (Transitions.Length == 0)
            {
                return 0;
            }

            var count = 0;
            for (var b = 0; b < 256; b++)
            {
                if (Transitions[b] != 0)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int OutputStateCount
    {
        get
        {
            var count = 0;
            for (var i = 0; i < BestOutputOrders.Length; i++)
            {
                if (BestOutputOrders[i] >= 0)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int IndexOf(ReadOnlySpan<byte> input)
    {
        return TryFindFirstMatch(input, out var index, out _, out _) ? index : -1;
    }

    public bool TryFindFirstMatch(ReadOnlySpan<byte> input, out int index, out int matchedLength, out int literalId)
    {
        index = -1;
        matchedLength = 0;
        literalId = -1;

        if (ShortestLength == int.MaxValue)
        {
            return false;
        }

        if (ShortestLength == 0)
        {
            index = 0;
            return true;
        }

        // Ordered alternation reports the lowest-order branch at the leftmost start, not the
        // first-ending match: keep scanning while a better match can still emerge, bounded by
        // the longest literal.
        var bestStart = int.MaxValue;
        var bestOrder = int.MaxValue;
        var bestReportedLength = 0;
        var bestLiteralId = -1;
        var state = 0;
        for (var i = 0; i < input.Length; i++)
        {
            state = Transitions[(state * 256) + input[i]];
            var bestLength = BestOutputLengths[state];
            if (bestLength > 0)
            {
                var candidateStart = i - bestLength + 1;
                var candidateOrder = BestOutputOrders[state];
                if (candidateStart < bestStart ||
                    (candidateStart == bestStart && candidateOrder < bestOrder))
                {
                    bestStart = candidateStart;
                    bestOrder = candidateOrder;
                    bestReportedLength = bestLength;
                    bestLiteralId = candidateOrder;
                }
            }

            if (bestOrder >= 0 && i - bestStart >= LongestLength - 1)
            {
                break;
            }
        }

        if (bestLiteralId < 0)
        {
            return false;
        }

        index = bestStart;
        matchedLength = bestReportedLength;
        literalId = bestLiteralId;
        return true;
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

        var lastIndex = -1;
        var state = 0;
        for (var i = 0; i < input.Length; i++)
        {
            state = Transitions[(state * 256) + input[i]];
            var shortest = ShortestOutputLengths[state];
            if (shortest != int.MaxValue)
            {
                var candidate = i - shortest + 1;
                if (candidate > lastIndex)
                {
                    lastIndex = candidate;
                }
            }
        }

        return lastIndex;
    }

    public bool TryGetMatchedLiteralInfo(ReadOnlySpan<byte> input, int index, out int matchedLength, out int literalId)
    {
        matchedLength = 0;
        literalId = -1;

        if (TryFindFirstMatch(input[index..], out var relative, out matchedLength, out literalId) && relative == 0)
        {
            return true;
        }

        matchedLength = 0;
        literalId = -1;
        return false;
    }

    public bool TryFindNextNonOverlappingMatch(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength,
        out int literalId)
    {
        index = -1;
        matchedLength = 0;
        literalId = -1;

        if (ShortestLength == int.MaxValue || state.NextStart > input.Length)
        {
            return false;
        }

        if (ShortestLength == 0)
        {
            index = state.NextStart;
            state = new PreparedMultiLiteralScanState(Math.Min(input.Length, state.NextStart + 1), Math.Min(input.Length, state.NextStart + 1), 0);
            return true;
        }

        var scanIndex = state.ScanIndex;
        var automatonState = state.AutomatonState;
        if (scanIndex < state.NextStart)
        {
            scanIndex = state.NextStart;
            automatonState = 0;
        }

        // Ordered alternation reports the lowest-order branch at the leftmost start, not the
        // first-ending match: keep scanning while a better match can still emerge, bounded by
        // the longest literal.
        var bestStart = int.MaxValue;
        var bestOrder = int.MaxValue;
        var bestReportedLength = 0;
        var bestLiteralId = -1;
        for (var i = scanIndex; i < input.Length; i++)
        {
            automatonState = Transitions[(automatonState * 256) + input[i]];
            var bestLength = BestOutputLengths[automatonState];
            if (bestLength > 0)
            {
                var candidateStart = i - bestLength + 1;
                var candidateOrder = BestOutputOrders[automatonState];
                if (candidateStart < bestStart ||
                    (candidateStart == bestStart && candidateOrder < bestOrder))
                {
                    bestStart = candidateStart;
                    bestOrder = candidateOrder;
                    bestReportedLength = bestLength;
                    bestLiteralId = candidateOrder;
                }
            }

            if (bestOrder >= 0 && i - bestStart >= LongestLength - 1)
            {
                break;
            }
        }

        if (bestLiteralId < 0)
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, automatonState);
            return false;
        }

        index = bestStart;
        matchedLength = bestReportedLength;
        literalId = bestLiteralId;
        var nextStart = index + matchedLength;
        state = new PreparedMultiLiteralScanState(nextStart, nextStart, 0);
        return true;
    }

    public bool TryFindNextOverlappingMatch(
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        out int index,
        out int matchedLength,
        out int literalId)
    {
        index = -1;
        matchedLength = 0;
        literalId = -1;

        if ((uint)state.NextStart > (uint)input.Length)
        {
            return false;
        }

        if (!TryFindFirstMatch(input[state.NextStart..], out var relativeIndex, out matchedLength, out literalId))
        {
            state = new PreparedMultiLiteralScanState(input.Length, input.Length, 0);
            return false;
        }

        index = state.NextStart + relativeIndex;
        state = PreparedMultiLiteralScanState.AfterOverlappingMatch(index);
        return true;
    }

}

internal readonly record struct PreparedMultiLiteralScanState(int NextStart, int ScanIndex, int AutomatonState)
{
    /// <summary>
    /// Advances one byte from the match start, rather than from its end, and resets backend scan state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PreparedMultiLiteralScanState AfterOverlappingMatch(int matchIndex) =>
        new(matchIndex + 1, matchIndex + 1, 0);
}

internal readonly partial struct PreparedMultiLiteralTrieSearch
{
    private readonly PreparedMultiLiteralTrieNode[] _nodes;

    private PreparedMultiLiteralTrieSearch(
        PreparedByteSearch rootSearch,
        int[] rootMap,
        PreparedMultiLiteralTrieNode[] nodes,
        int shortestLength)
    {
        RootSearch = rootSearch;
        RootMap = rootMap;
        _nodes = nodes;
        ShortestLength = shortestLength;
    }

    public PreparedByteSearch RootSearch { get; }

    public int[] RootMap { get; }

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
            var relative = RootSearch.IndexOf(input[index..]);
            if (relative < 0)
            {
                return -1;
            }

            var candidate = index + relative;
            if (TryGetMatchedLiteralLength(input, candidate, out _))
            {
                return candidate;
            }

            index = candidate + 1;
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
            var relative = RootSearch.LastIndexOf(input[..end]);
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
        return TryGetMatchedLiteralInfo(input, index, out matchedLength, out _);
    }

    public bool TryGetMatchedLiteralInfo(ReadOnlySpan<byte> input, int index, out int matchedLength, out int literalId)
    {
        matchedLength = 0;
        literalId = -1;
        if ((uint)index >= (uint)input.Length || index > input.Length - ShortestLength)
        {
            return false;
        }

        var rootChild = RootMap[input[index]];
        if (rootChild < 0)
        {
            return false;
        }

        var nodeIndex = rootChild;
        var bestOrder = -1;
        var bestLength = 0;
        var offset = 1;
        while (true)
        {
            var node = _nodes[nodeIndex];
            if (node.TerminalOrder >= 0 && (bestOrder < 0 || node.TerminalOrder < bestOrder))
            {
                bestOrder = node.TerminalOrder;
                bestLength = node.TerminalLength;
            }

            if (index + offset >= input.Length)
            {
                break;
            }

            var next = node.FindChild(input[index + offset]);
            if (next < 0)
            {
                break;
            }

            nodeIndex = next;
            offset++;
        }

        if (bestOrder < 0)
        {
            return false;
        }

        matchedLength = bestLength;
        literalId = bestOrder;
        return true;
    }

}

internal readonly struct PreparedMultiLiteralTrieNode
{
    public PreparedMultiLiteralTrieNode(byte[] keys, int[] children, int terminalOrder, int terminalLength)
    {
        Keys = keys;
        Children = children;
        TerminalOrder = terminalOrder;
        TerminalLength = terminalLength;
    }

    public byte[] Keys { get; }

    public int[] Children { get; }

    public int TerminalOrder { get; }

    public int TerminalLength { get; }

    public int FindChild(byte key)
    {
        var index = Array.BinarySearch(Keys, key);
        return index >= 0 ? Children[index] : -1;
    }
}
