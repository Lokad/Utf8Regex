using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.InteropServices;

namespace Lokad.Utf8Regex.Internal.Utilities;

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

internal readonly struct PreparedMultiLiteralSearch
{
    private const int TrieThreshold = 8;
    private const int AutomatonThreshold = 5;

    public PreparedMultiLiteralSearch(byte[][] literals, bool ignoreCase)
    {
        if (ignoreCase)
        {
            Literals = literals;
            ExactSearch = default;
            TrieSearch = default;
            AutomatonSearch = default;
            IgnoreCaseSearch = new PreparedAsciiIgnoreCaseLiteralSetSearch(literals);
            Kind = PreparedMultiLiteralKind.AsciiIgnoreCase;
        }
        else
        {
            Literals = literals;
            var exactSearch = new PreparedLiteralSetSearch(literals);
            if (ShouldUsePacked(exactSearch))
            {
                ExactSearch = default;
                TrieSearch = default;
                AutomatonSearch = default;
                EarliestSearch = default;
                PackedSearch = PreparedMultiLiteralPackedSearch.Create(literals);
                Kind = PreparedMultiLiteralKind.ExactPacked;
            }
            else if (ShouldUseEarliestExact(exactSearch))
            {
                ExactSearch = default;
                TrieSearch = default;
                AutomatonSearch = default;
                EarliestSearch = new PreparedMultiLiteralEarliestExactSearch(literals);
                PackedSearch = default;
                Kind = PreparedMultiLiteralKind.ExactEarliest;
            }
            else if (ShouldUseAutomaton(exactSearch))
            {
                ExactSearch = default;
                TrieSearch = PreparedMultiLiteralTrieSearch.Create(literals);
                AutomatonSearch = PreparedMultiLiteralAutomatonSearch.Create(literals);
                EarliestSearch = new PreparedMultiLiteralEarliestExactSearch(literals);
                PackedSearch = default;
                Kind = PreparedMultiLiteralKind.ExactAutomaton;
            }
            else if (ShouldUseTrie(exactSearch))
            {
                ExactSearch = default;
                TrieSearch = PreparedMultiLiteralTrieSearch.Create(literals);
                AutomatonSearch = default;
                EarliestSearch = default;
                PackedSearch = default;
                Kind = PreparedMultiLiteralKind.ExactTrie;
            }
            else
            {
                ExactSearch = exactSearch;
                TrieSearch = default;
                AutomatonSearch = default;
                EarliestSearch = default;
                PackedSearch = default;
                Kind = PreparedMultiLiteralKind.ExactDirect;
            }

            IgnoreCaseSearch = default;
        }
    }

    public PreparedMultiLiteralSearch(PreparedLiteralSetSearch search)
    {
        Literals = search.SearchData.Buckets.SelectMany(static bucket => bucket.Literals).ToArray();
        if (ShouldUsePacked(search))
        {
            ExactSearch = default;
            TrieSearch = default;
            AutomatonSearch = default;
            EarliestSearch = default;
            PackedSearch = PreparedMultiLiteralPackedSearch.Create(Literals);
            Kind = PreparedMultiLiteralKind.ExactPacked;
        }
        else if (ShouldUseEarliestExact(search))
        {
            ExactSearch = default;
            TrieSearch = default;
            AutomatonSearch = default;
            EarliestSearch = new PreparedMultiLiteralEarliestExactSearch(Literals);
            PackedSearch = default;
            Kind = PreparedMultiLiteralKind.ExactEarliest;
        }
        else if (ShouldUseAutomaton(search))
        {
            ExactSearch = default;
            TrieSearch = PreparedMultiLiteralTrieSearch.Create(Literals);
            AutomatonSearch = PreparedMultiLiteralAutomatonSearch.Create(Literals);
            EarliestSearch = new PreparedMultiLiteralEarliestExactSearch(Literals);
            PackedSearch = default;
            Kind = PreparedMultiLiteralKind.ExactAutomaton;
        }
        else if (ShouldUseTrie(search))
        {
            ExactSearch = default;
            TrieSearch = PreparedMultiLiteralTrieSearch.Create(Literals);
            AutomatonSearch = default;
            EarliestSearch = default;
            PackedSearch = default;
            Kind = PreparedMultiLiteralKind.ExactTrie;
        }
        else
        {
            ExactSearch = search;
            TrieSearch = default;
            AutomatonSearch = default;
            EarliestSearch = default;
            PackedSearch = default;
            Kind = PreparedMultiLiteralKind.ExactDirect;
        }

        IgnoreCaseSearch = default;
    }

    public PreparedMultiLiteralSearch(PreparedAsciiIgnoreCaseLiteralSetSearch search)
    {
        Literals = search.Buckets.SelectMany(static bucket => bucket.Literals).ToArray();
        ExactSearch = default;
        TrieSearch = default;
        AutomatonSearch = default;
        EarliestSearch = default;
        PackedSearch = default;
        IgnoreCaseSearch = search;
        Kind = PreparedMultiLiteralKind.AsciiIgnoreCase;
    }

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

        if (Kind == PreparedMultiLiteralKind.ExactEarliest)
        {
            return EarliestSearch.TryFindNextNonOverlappingLength(input, ref state, out index, out matchedLength);
        }

        if (Kind == PreparedMultiLiteralKind.ExactDirect)
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
        state = new PreparedMultiLiteralScanState(index + 1, index + 1, 0);
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

    private static bool ShouldUsePacked(PreparedLiteralSetSearch search)
    {
        return ContainsNonAsciiLiteral(search.SearchData.Buckets) &&
            LiteralCount(search.SearchData.Buckets) >= 3 &&
            LiteralCount(search.SearchData.Buckets) < TrieThreshold &&
            search.SearchData.ShortestLength >= 4 &&
            PreparedMultiLiteralPackedSearch.TryCreate(search.SearchData.Buckets.SelectMany(static bucket => bucket.Literals).ToArray(), out _);
    }

    private static bool ShouldUseAutomaton(PreparedLiteralSetSearch search)
    {
        return search.Strategy != PreparedLiteralSetStrategy.SingleBucketPrefix &&
            LiteralCount(search.SearchData.Buckets) >= AutomatonThreshold;
    }

    private static bool ShouldUseTrie(PreparedLiteralSetSearch search)
    {
        return search.Strategy == PreparedLiteralSetStrategy.MultiLiteralTrie ||
            ShouldPreferTrieForSmallUniqueAnchorFamily(search) ||
            LiteralCount(search.SearchData.Buckets) >= TrieThreshold;
    }

    private static bool ShouldUseEarliestExact(PreparedLiteralSetSearch search)
    {
        var literalCount = LiteralCount(search.SearchData.Buckets);
        if (literalCount > 3 || search.SearchData.ShortestLength < 4)
        {
            return false;
        }

        var totalBytes = 0;
        for (var i = 0; i < search.SearchData.Buckets.Length; i++)
        {
            var literals = search.SearchData.Buckets[i].Literals;
            for (var j = 0; j < literals.Length; j++)
            {
                totalBytes += literals[j].Length;
            }
        }

        return totalBytes >= 24;
    }

    private static bool ShouldPreferTrieForSmallUniqueAnchorFamily(PreparedLiteralSetSearch search)
    {
        var literalCount = LiteralCount(search.SearchData.Buckets);
        return search.Strategy == PreparedLiteralSetStrategy.UniqueAnchorByte &&
            literalCount >= 3 &&
            literalCount < AutomatonThreshold &&
            search.SearchData.ShortestLength >= 4;
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

    private static bool ContainsNonAsciiLiteral(AsciiExactLiteralBucket[] buckets)
    {
        for (var i = 0; i < buckets.Length; i++)
        {
            var literals = buckets[i].Literals;
            for (var j = 0; j < literals.Length; j++)
            {
                var literal = literals[j];
                for (var k = 0; k < literal.Length; k++)
                {
                    if (literal[k] >= 0x80)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }
}

internal readonly struct PreparedMultiLiteralPackedSearch
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

    public static bool TryCreate(byte[][] literals, out PreparedMultiLiteralPackedSearch search)
    {
        search = default;
        ArgumentNullException.ThrowIfNull(literals);
        if (literals.Length == 0)
        {
            return false;
        }

        var shortestLength = int.MaxValue;
        for (var i = 0; i < literals.Length; i++)
        {
            shortestLength = Math.Min(shortestLength, literals[i].Length);
        }

        if (shortestLength <= 0)
        {
            return false;
        }

        for (var offset = 0; offset < shortestLength; offset++)
        {
            if (TryCreateAtOffset(literals, shortestLength, offset, out search))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool TryCreateAtDiscriminatorOffset(byte[][] literals, int offset, out PreparedMultiLiteralPackedSearch search)
    {
        search = default;
        ArgumentNullException.ThrowIfNull(literals);
        if (literals.Length == 0)
        {
            return false;
        }

        var shortestLength = int.MaxValue;
        for (var i = 0; i < literals.Length; i++)
        {
            shortestLength = Math.Min(shortestLength, literals[i].Length);
        }

        return shortestLength > 0 &&
            TryCreateAtOffset(literals, shortestLength, offset, out search);
    }

    private static bool TryCreateAtOffset(byte[][] literals, int shortestLength, int offset, out PreparedMultiLiteralPackedSearch search)
    {
        search = default;
        if ((uint)offset >= (uint)shortestLength)
        {
            return false;
        }

        Span<byte> discriminatorBytes = stackalloc byte[Math.Min(literals.Length, 256)];
        Span<bool> seen = stackalloc bool[256];
        seen.Clear();
        var count = 0;
        var duplicate = false;
        for (var i = 0; i < literals.Length; i++)
        {
            var value = literals[i][offset];
            if (seen[value])
            {
                duplicate = true;
                break;
            }

            seen[value] = true;
            discriminatorBytes[count++] = value;
        }

        if (duplicate)
        {
            return false;
        }

        var literalByDiscriminator = new byte[256][];
        var literalIdByDiscriminator = new int[256];
        var secondByteByDiscriminator = new byte[256];
        var thirdByteByDiscriminator = new byte[256];
        Array.Fill(literalIdByDiscriminator, -1);
        var hasSecondByteAnchor = offset + 1 < shortestLength;
        var hasThirdByteAnchor = offset + 2 < shortestLength;
        for (var i = 0; i < literals.Length; i++)
        {
            var discriminator = literals[i][offset];
            literalByDiscriminator[discriminator] = literals[i];
            literalIdByDiscriminator[discriminator] = i;
            if (hasSecondByteAnchor)
            {
                secondByteByDiscriminator[discriminator] = literals[i][offset + 1];
            }

            if (hasThirdByteAnchor)
            {
                thirdByteByDiscriminator[discriminator] = literals[i][offset + 2];
            }
        }

        search = new PreparedMultiLiteralPackedSearch(
            PreparedByteSearch.Create(discriminatorBytes[..count].ToArray()),
            literals,
            literalByDiscriminator,
            literalIdByDiscriminator,
            secondByteByDiscriminator,
            thirdByteByDiscriminator,
            hasSecondByteAnchor,
            hasThirdByteAnchor,
            offset,
            shortestLength);
        return true;
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

            end = relative - DiscriminatorOffset + 1;
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
        state = new PreparedMultiLiteralScanState(index + 1, index + 1, 0);
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
        ArgumentNullException.ThrowIfNull(literals);
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
        ArgumentNullException.ThrowIfNull(literals);
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
        ArgumentNullException.ThrowIfNull(literals);
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
        ArgumentNullException.ThrowIfNull(literals);
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
        ArgumentNullException.ThrowIfNull(literals);
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
        ArgumentNullException.ThrowIfNull(literals);
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
        ArgumentNullException.ThrowIfNull(literals);
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

internal readonly struct PreparedMultiLiteralLeadingUtf8SegmentPrefilter
{
    public PreparedMultiLiteralLeadingUtf8SegmentPrefilter(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        ShortestLength = literals.Length == 0 ? int.MaxValue : literals.Min(static literal => literal.Length);
        Buckets = [];

        if (literals.Length == 0)
        {
            return;
        }

        var buckets = new List<PreparedMultiLiteralLeadingUtf8SegmentBucket>();
        foreach (var literal in literals)
        {
            if (!TryGetLeadingSegmentLength(literal, out var segmentLength))
            {
                Buckets = [];
                return;
            }

            var segment = literal[..segmentLength].ToArray();
            var bucketIndex = -1;
            for (var i = 0; i < buckets.Count; i++)
            {
                if (buckets[i].Segment.AsSpan().SequenceEqual(segment))
                {
                    bucketIndex = i;
                    break;
                }
            }

            if (bucketIndex < 0)
            {
                buckets.Add(new PreparedMultiLiteralLeadingUtf8SegmentBucket(segment, [literal]));
                continue;
            }

            var existing = buckets[bucketIndex];
            var expanded = new byte[existing.Literals.Length + 1][];
            Array.Copy(existing.Literals, expanded, existing.Literals.Length);
            expanded[^1] = literal;
            buckets[bucketIndex] = new PreparedMultiLiteralLeadingUtf8SegmentBucket(existing.Segment, expanded);
        }

        Buckets = [.. buckets];
    }

    public int ShortestLength { get; }

    public PreparedMultiLiteralLeadingUtf8SegmentBucket[] Buckets { get; }

    public bool HasValue => Buckets.Length > 0;

    public bool TryFindNextCandidate(ReadOnlySpan<byte> input, ref PreparedMultiLiteralScanState state, out int candidateIndex)
    {
        candidateIndex = -1;
        if (!HasValue || (uint)state.NextStart > (uint)input.Length || ShortestLength == int.MaxValue)
        {
            return false;
        }

        var bestIndex = int.MaxValue;
        for (var i = 0; i < Buckets.Length; i++)
        {
            var relative = input[state.NextStart..].IndexOf(Buckets[i].Segment);
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

internal readonly record struct PreparedMultiLiteralLeadingUtf8SegmentBucket(byte[] Segment, byte[][] Literals);

internal readonly struct PreparedMultiLiteralPackedNibbleSimdPrefilter
{
    private const int MaxMaskLen = 4;
    private const int LaneCount = 16;

    public PreparedMultiLiteralPackedNibbleSimdPrefilter(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        ShortestLength = literals.Length == 0 ? int.MaxValue : literals.Min(static literal => literal.Length);
        MaskLength = 0;
        LowMaskVectors = [];
        HighMaskVectors = [];
        Search = default;

        if (!Ssse3.IsSupported || !Sse2.IsSupported || literals.Length == 0 || literals.Length > 8 || ShortestLength <= 0 || ShortestLength == int.MaxValue)
        {
            return;
        }

        MaskLength = Math.Min(MaxMaskLen, ShortestLength);
        LowMaskVectors = new Vector128<byte>[MaskLength];
        HighMaskVectors = new Vector128<byte>[MaskLength];

        Span<bool> firstSeen = stackalloc bool[256];
        Span<byte> firstBytes = stackalloc byte[256];
        Span<byte> lowTable = stackalloc byte[16];
        Span<byte> highTable = stackalloc byte[16];
        var firstCount = 0;

        for (var offset = 0; offset < MaskLength; offset++)
        {
            lowTable.Clear();
            highTable.Clear();
            for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
            {
                var bit = (byte)(1 << literalIndex);
                var value = literals[literalIndex][offset];
                lowTable[value & 0xF] |= bit;
                highTable[value >> 4] |= bit;
            }

            LowMaskVectors[offset] = Vector128.Create(
                lowTable[0], lowTable[1], lowTable[2], lowTable[3],
                lowTable[4], lowTable[5], lowTable[6], lowTable[7],
                lowTable[8], lowTable[9], lowTable[10], lowTable[11],
                lowTable[12], lowTable[13], lowTable[14], lowTable[15]);
            HighMaskVectors[offset] = Vector128.Create(
                highTable[0], highTable[1], highTable[2], highTable[3],
                highTable[4], highTable[5], highTable[6], highTable[7],
                highTable[8], highTable[9], highTable[10], highTable[11],
                highTable[12], highTable[13], highTable[14], highTable[15]);
        }

        for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
        {
            var first = literals[literalIndex][0];
            if (!firstSeen[first])
            {
                firstSeen[first] = true;
                firstBytes[firstCount++] = first;
            }
        }

        Search = firstCount == 0 ? default : PreparedByteSearch.Create(firstBytes[..firstCount].ToArray());
    }

    public int ShortestLength { get; }

    public int MaskLength { get; }

    public Vector128<byte>[] LowMaskVectors { get; }

    public Vector128<byte>[] HighMaskVectors { get; }

    public PreparedByteSearch Search { get; }

    public bool HasValue => Search.Count > 0;

    public bool TryFindNextCandidate(ReadOnlySpan<byte> input, ref PreparedMultiLiteralScanState state, out int candidateIndex)
    {
        candidateIndex = -1;
        if (!HasValue || (uint)state.NextStart > (uint)input.Length || ShortestLength == int.MaxValue)
        {
            return false;
        }

        if (state.ScanIndex != 0)
        {
            var pendingMask = state.ScanIndex;
            var lane = BitOperations.TrailingZeroCount((uint)pendingMask);
            pendingMask &= pendingMask - 1;
            candidateIndex = state.AutomatonState + lane;
            state = new PreparedMultiLiteralScanState(state.NextStart, pendingMask, state.AutomatonState);
            return true;
        }

        var maxStart = input.Length - ShortestLength;
        var vectorLimit = maxStart - (LaneCount - 1);
        var baseIndex = state.NextStart;
        var lowNibbleMask = Vector128.Create((byte)0x0F);
        var highNibbleMask = Vector128.Create((byte)0xF0);
        var zero = Vector128<byte>.Zero;
        ref var inputRef = ref MemoryMarshal.GetReference(input);

        while (baseIndex <= vectorLimit)
        {
            var candidates = Vector128.Create((byte)0xFF);
            for (var offset = 0; offset < MaskLength; offset++)
            {
                var window = Vector128.LoadUnsafe(ref Unsafe.Add(ref inputRef, baseIndex + offset));
                var low = Sse2.And(window, lowNibbleMask);
                var high = Sse2.And(Sse2.ShiftRightLogical(Sse2.And(window, highNibbleMask).AsUInt16(), 4).AsByte(), lowNibbleMask);
                var lowMask = Ssse3.Shuffle(LowMaskVectors[offset], low);
                var highMask = Ssse3.Shuffle(HighMaskVectors[offset], high);
                candidates = Sse2.And(candidates, Sse2.And(lowMask, highMask));
            }

            var zeroMask = Sse2.MoveMask(Sse2.CompareEqual(candidates, zero));
            var matchMask = (~zeroMask) & 0xFFFF;
            if (matchMask != 0)
            {
                var lane = BitOperations.TrailingZeroCount((uint)matchMask);
                candidateIndex = baseIndex + lane;
                state = new PreparedMultiLiteralScanState(baseIndex + LaneCount, matchMask & ~(1 << lane), baseIndex);
                return true;
            }

            baseIndex += LaneCount;
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
                var value = input[start + offset];
                var lowMask = LowMaskVectors[offset].GetElement(value & 0xF);
                var highMask = HighMaskVectors[offset].GetElement(value >> 4);
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
}

internal readonly struct PreparedMultiLiteralNibbleMaskPrefilter
{
    private const int MaxMaskLen = 4;

    public PreparedMultiLiteralNibbleMaskPrefilter(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        ShortestLength = literals.Length == 0 ? int.MaxValue : literals.Min(static literal => literal.Length);
        Search = default;
        LowMasks = [];
        HighMasks = [];
        MaskLength = 0;

        if (literals.Length == 0 || literals.Length > 64 || ShortestLength <= 0 || ShortestLength == int.MaxValue)
        {
            return;
        }

        MaskLength = Math.Min(MaxMaskLen, ShortestLength);
        LowMasks = new ulong[MaskLength][];
        HighMasks = new ulong[MaskLength][];
        for (var offset = 0; offset < MaskLength; offset++)
        {
            LowMasks[offset] = new ulong[16];
            HighMasks[offset] = new ulong[16];
        }

        Span<bool> firstSeen = stackalloc bool[256];
        Span<byte> firstBytes = stackalloc byte[256];
        var firstCount = 0;
        for (var literalIndex = 0; literalIndex < literals.Length; literalIndex++)
        {
            var literal = literals[literalIndex];
            var bit = 1UL << literalIndex;
            for (var offset = 0; offset < MaskLength; offset++)
            {
                var b = literal[offset];
                LowMasks[offset][b & 0xF] |= bit;
                HighMasks[offset][b >> 4] |= bit;
            }

            var first = literal[0];
            if (!firstSeen[first])
            {
                firstSeen[first] = true;
                firstBytes[firstCount++] = first;
            }
        }

        Search = firstCount == 0 ? default : PreparedByteSearch.Create(firstBytes[..firstCount].ToArray());
    }

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

internal readonly struct PreparedMultiLiteralOffsetMaskPrefilter
{
    private const int MaxOffsets = 3;

    public PreparedMultiLiteralOffsetMaskPrefilter(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        ShortestLength = literals.Length == 0 ? int.MaxValue : literals.Min(static literal => literal.Length);
        Offsets = [];
        Masks = [];
        Search = default;

        if (literals.Length == 0 || ShortestLength <= 0 || ShortestLength == int.MaxValue)
        {
            return;
        }

        var scores = new List<(int Offset, int Cardinality, int FrequencyScore)>(ShortestLength);
        var seenAtOffset = new bool[256];
        for (var offset = 0; offset < ShortestLength; offset++)
        {
            Array.Clear(seenAtOffset);
            var cardinality = 0;
            var frequencyScore = 0;
            for (var i = 0; i < literals.Length; i++)
            {
                var value = literals[i][offset];
                if (seenAtOffset[value])
                {
                    continue;
                }

                seenAtOffset[value] = true;
                cardinality++;
                frequencyScore += PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(value);
            }

            scores.Add((offset, cardinality, frequencyScore));
        }

        var selected = scores
            .OrderBy(static score => score.Cardinality)
            .ThenBy(static score => score.FrequencyScore)
            .ThenBy(static score => score.Offset)
            .Take(MaxOffsets)
            .ToArray();

        if (selected.Length == 0)
        {
            return;
        }

        Offsets = new int[selected.Length];
        Masks = new byte[selected.Length][];
        for (var i = 0; i < selected.Length; i++)
        {
            Offsets[i] = selected[i].Offset;
            var mask = new byte[256];
            for (var j = 0; j < literals.Length; j++)
            {
                mask[literals[j][selected[i].Offset]] = 1;
            }

            Masks[i] = mask;
        }

        Span<byte> searchBytes = stackalloc byte[256];
        var count = 0;
        for (var value = 0; value < 256; value++)
        {
            if (Masks[0][value] != 0)
            {
                searchBytes[count++] = (byte)value;
            }
        }

        Search = count == 0 ? default : PreparedByteSearch.Create(searchBytes[..count].ToArray());
    }

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

internal readonly struct PreparedMultiLiteralRareBytePrefilter
{
    public PreparedMultiLiteralRareBytePrefilter(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        ShortestLength = literals.Length == 0 ? int.MaxValue : literals.Min(static literal => literal.Length);
        OffsetByValue = new int[256];
        Array.Fill(OffsetByValue, -1);

        Span<bool> seen = stackalloc bool[256];
        Span<int> maxOffsets = stackalloc int[256];
        maxOffsets.Fill(-1);

        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            for (var j = 0; j < literal.Length; j++)
            {
                var value = literal[j];
                seen[value] = true;
                if (j > maxOffsets[value])
                {
                    maxOffsets[value] = j;
                }
            }
        }

        Span<byte> selected = stackalloc byte[3];
        var count = 0;
        for (var pick = 0; pick < selected.Length; pick++)
        {
            var bestValue = -1;
            var bestRank = int.MaxValue;
            for (var value = 0; value < 256; value++)
            {
                if (!seen[value] || OffsetByValue[value] >= 0)
                {
                    continue;
                }

                var rank = GetAsciiFrequencyRank((byte)value);
                if (rank < bestRank)
                {
                    bestRank = rank;
                    bestValue = value;
                }
            }

            if (bestValue < 0)
            {
                break;
            }

            selected[count++] = (byte)bestValue;
            OffsetByValue[bestValue] = maxOffsets[bestValue];
        }

        Search = count == 0
            ? default
            : PreparedByteSearch.Create(selected[..count].ToArray());
    }

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

internal readonly struct PreparedMultiLiteralEarliestExactSearch
{
    public PreparedMultiLiteralEarliestExactSearch(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        Literals = literals;
        ShortestLength = literals.Length == 0 ? int.MaxValue : literals.Min(static literal => literal.Length);
        LongestLength = literals.Length == 0 ? 0 : literals.Max(static literal => literal.Length);
    }

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

internal readonly struct PreparedMultiLiteralAutomatonSearch
{
    private PreparedMultiLiteralAutomatonSearch(int[] transitions, int[] bestOutputOrders, int[] bestOutputLengths, int[] shortestOutputLengths, int shortestLength)
    {
        Transitions = transitions;
        BestOutputOrders = bestOutputOrders;
        BestOutputLengths = bestOutputLengths;
        ShortestOutputLengths = shortestOutputLengths;
        ShortestLength = shortestLength;
    }

    public int[] Transitions { get; }

    public int[] BestOutputOrders { get; }

    public int[] BestOutputLengths { get; }

    public int[] ShortestOutputLengths { get; }

    public int ShortestLength { get; }

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

    public static PreparedMultiLiteralAutomatonSearch Create(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        if (literals.Length == 0)
        {
            return new PreparedMultiLiteralAutomatonSearch([], [], [], [], int.MaxValue);
        }

        var nodes = new List<MutablePreparedMultiLiteralAutomatonNode> { new() };
        var shortestLength = int.MaxValue;

        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            shortestLength = Math.Min(shortestLength, literal.Length);
            InsertLiteral(nodes, literal, i);
        }

        BuildFailures(nodes);

        var stateCount = nodes.Count;
        var transitions = new int[stateCount * 256];
        var bestOutputOrders = new int[stateCount];
        var bestOutputLengths = new int[stateCount];
        var shortestOutputLengths = new int[stateCount];
        Array.Fill(bestOutputOrders, -1);
        Array.Fill(shortestOutputLengths, int.MaxValue);

        for (var state = 0; state < stateCount; state++)
        {
            var node = nodes[state];
            bestOutputOrders[state] = node.BestOutputOrder;
            bestOutputLengths[state] = node.BestOutputLength;
            shortestOutputLengths[state] = node.ShortestOutputLength;
            var baseIndex = state * 256;
            for (var b = 0; b < 256; b++)
            {
                transitions[baseIndex + b] = node.Transitions[b];
            }
        }

        return new PreparedMultiLiteralAutomatonSearch(transitions, bestOutputOrders, bestOutputLengths, shortestOutputLengths, shortestLength);
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

        var state = 0;
        for (var i = 0; i < input.Length; i++)
        {
            state = Transitions[(state * 256) + input[i]];
            var bestLength = BestOutputLengths[state];
            if (bestLength > 0)
            {
                index = i - bestLength + 1;
                matchedLength = bestLength;
                literalId = BestOutputOrders[state];
                return true;
            }
        }

        return false;
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

        for (var i = scanIndex; i < input.Length; i++)
        {
            automatonState = Transitions[(automatonState * 256) + input[i]];
            var bestLength = BestOutputLengths[automatonState];
            if (bestLength > 0)
            {
                index = i - bestLength + 1;
                matchedLength = bestLength;
                literalId = BestOutputOrders[automatonState];

                var nextStart = index + matchedLength;
                state = new PreparedMultiLiteralScanState(nextStart, nextStart, 0);
                return true;
            }
        }

        state = new PreparedMultiLiteralScanState(input.Length, input.Length, automatonState);
        return false;
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
        state = new PreparedMultiLiteralScanState(index + 1, index + 1, 0);
        return true;
    }

    private static void InsertLiteral(List<MutablePreparedMultiLiteralAutomatonNode> nodes, byte[] literal, int order)
    {
        var state = 0;
        for (var i = 0; i < literal.Length; i++)
        {
            var key = literal[i];
            var next = nodes[state].Transitions[key];
            if (next == 0 && (state != 0 || !nodes[state].HasTransition(key)))
            {
                next = nodes.Count;
                nodes.Add(new MutablePreparedMultiLiteralAutomatonNode());
                nodes[state].RegisterTransition(key, next);
            }

            state = next;
        }

        nodes[state].RegisterOutput(order, literal.Length);
    }

    private static void BuildFailures(List<MutablePreparedMultiLiteralAutomatonNode> nodes)
    {
        var queue = new Queue<int>();
        for (var b = 0; b < 256; b++)
        {
            var next = nodes[0].Transitions[b];
            if (next != 0)
            {
                queue.Enqueue(next);
            }
        }

        while (queue.Count > 0)
        {
            var state = queue.Dequeue();
            for (var b = 0; b < 256; b++)
            {
                var next = nodes[state].Transitions[b];
                if (next != 0)
                {
                    var fail = nodes[state].Failure;
                    while (fail != 0 && nodes[fail].Transitions[b] == 0)
                    {
                        fail = nodes[fail].Failure;
                    }

                    var failureTarget = nodes[fail].Transitions[b];
                    nodes[next].Failure = failureTarget;
                    nodes[next].AbsorbFailureOutputs(nodes[failureTarget]);
                    queue.Enqueue(next);
                }
                else
                {
                    nodes[state].Transitions[b] = nodes[nodes[state].Failure].Transitions[b];
                }
            }
        }
    }
}

internal readonly record struct PreparedMultiLiteralScanState(int NextStart, int ScanIndex, int AutomatonState);

internal sealed class MutablePreparedMultiLiteralAutomatonNode
{
    private readonly bool[] _hasTransition = new bool[256];

    public int[] Transitions { get; } = new int[256];

    public int Failure { get; set; }

    public int BestOutputOrder { get; private set; } = -1;

    public int BestOutputLength { get; private set; }

    public int ShortestOutputLength { get; private set; } = int.MaxValue;

    public bool HasTransition(byte key) => _hasTransition[key];

    public void RegisterTransition(byte key, int state)
    {
        _hasTransition[key] = true;
        Transitions[key] = state;
    }

    public void RegisterOutput(int order, int length)
    {
        if (BestOutputLength == 0 || length > BestOutputLength || (length == BestOutputLength && (BestOutputOrder < 0 || order < BestOutputOrder)))
        {
            BestOutputOrder = order;
            BestOutputLength = length;
        }

        ShortestOutputLength = Math.Min(ShortestOutputLength, length);
    }

    public void AbsorbFailureOutputs(MutablePreparedMultiLiteralAutomatonNode failure)
    {
        if (failure.BestOutputOrder >= 0 &&
            (BestOutputLength == 0 ||
            failure.BestOutputLength > BestOutputLength ||
            (failure.BestOutputLength == BestOutputLength && (BestOutputOrder < 0 || failure.BestOutputOrder < BestOutputOrder))))
        {
            BestOutputOrder = failure.BestOutputOrder;
            BestOutputLength = failure.BestOutputLength;
        }

        if (failure.ShortestOutputLength < ShortestOutputLength)
        {
            ShortestOutputLength = failure.ShortestOutputLength;
        }
    }

    public int this[byte key]
    {
        get => Transitions[key];
        set
        {
            _hasTransition[key] = true;
            Transitions[key] = value;
        }
    }
}

internal readonly struct PreparedMultiLiteralTrieSearch
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

    public static PreparedMultiLiteralTrieSearch Create(byte[][] literals)
    {
        ArgumentNullException.ThrowIfNull(literals);
        if (literals.Length == 0)
        {
            return new PreparedMultiLiteralTrieSearch(PreparedByteSearch.Create(), CreateRootMap(Array.Empty<byte>(), Array.Empty<int>()), [], int.MaxValue);
        }

        var nodes = new List<MutablePreparedMultiLiteralTrieNode>();
        nodes.Add(new MutablePreparedMultiLiteralTrieNode());

        var shortestLength = int.MaxValue;
        for (var order = 0; order < literals.Length; order++)
        {
            var literal = literals[order];
            shortestLength = Math.Min(shortestLength, literal.Length);
            InsertLiteral(nodes, literal, nodes[0], literal.Length, order);
        }

        var flattened = new PreparedMultiLiteralTrieNode[nodes.Count];
        for (var i = 0; i < nodes.Count; i++)
        {
            flattened[i] = nodes[i].Freeze();
        }

        var rootBytes = flattened[0].Keys;
        var rootMap = CreateRootMap(rootBytes, flattened[0].Children);
        return new PreparedMultiLiteralTrieSearch(PreparedByteSearch.Create(rootBytes), rootMap, flattened, shortestLength);
    }

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

    private static void InsertLiteral(
        List<MutablePreparedMultiLiteralTrieNode> nodes,
        byte[] literal,
        MutablePreparedMultiLiteralTrieNode root,
        int length,
        int order)
    {
        var node = root;
        for (var i = 0; i < literal.Length; i++)
        {
            var key = literal[i];
            if (!node.Children.TryGetValue(key, out var next))
            {
                next = nodes.Count;
                node.Children.Add(key, next);
                nodes.Add(new MutablePreparedMultiLiteralTrieNode());
            }

            node = nodes[next];
        }

        node.SetTerminal(order, length);
    }

    private static int[] CreateRootMap(byte[] keys, int[] children)
    {
        var map = new int[256];
        Array.Fill(map, -1);
        for (var i = 0; i < keys.Length; i++)
        {
            map[keys[i]] = children[i];
        }

        return map;
    }
}

internal sealed class MutablePreparedMultiLiteralTrieNode
{
    public Dictionary<byte, int> Children { get; } = [];

    public int TerminalOrder { get; private set; } = -1;

    public int TerminalLength { get; private set; }

    public void SetTerminal(int order, int length)
    {
        if (TerminalOrder >= 0 && TerminalOrder <= order)
        {
            return;
        }

        TerminalOrder = order;
        TerminalLength = length;
    }

    public PreparedMultiLiteralTrieNode Freeze()
    {
        if (Children.Count == 0)
        {
            return new PreparedMultiLiteralTrieNode([], [], TerminalOrder, TerminalLength);
        }

        var ordered = Children.OrderBy(static pair => pair.Key).ToArray();
        var keys = new byte[ordered.Length];
        var children = new int[ordered.Length];
        for (var i = 0; i < ordered.Length; i++)
        {
            keys[i] = ordered[i].Key;
            children[i] = ordered[i].Value;
        }

        return new PreparedMultiLiteralTrieNode(keys, children, TerminalOrder, TerminalLength);
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
