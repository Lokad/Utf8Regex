using System.Text;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Tests;

public sealed class PreparedSearchPrimitivesTests
{
    [Fact]
    public void PreparedByteSearchSupportsSingleTwoThreeAndSetCases()
    {
        var input = Encoding.UTF8.GetBytes("zzabczdef");

        Assert.Equal(2, PreparedByteSearch.Create((byte)'a').IndexOf(input));
        Assert.Equal(2, PreparedByteSearch.Create((byte)'a', (byte)'x').IndexOf(input));
        Assert.Equal(2, PreparedByteSearch.Create((byte)'x', (byte)'y', (byte)'a').IndexOf(input));
        Assert.Equal(6, PreparedByteSearch.Create((byte)'q', (byte)'d', (byte)'w', (byte)'e').IndexOf(input));
    }

    [Fact]
    public void PreparedByteSearchSupportsReverseSearch()
    {
        var input = Encoding.UTF8.GetBytes("zzabczdef");

        Assert.Equal(8, PreparedByteSearch.Create((byte)'f').LastIndexOf(input));
        Assert.Equal(6, PreparedByteSearch.Create((byte)'d', (byte)'x').LastIndexOf(input));
        Assert.Equal(7, PreparedByteSearch.Create((byte)'q', (byte)'d', (byte)'w', (byte)'e').LastIndexOf(input));
    }

    [Fact]
    public void PreparedSubstringSearchSupportsExactAndIgnoreCaseSearch()
    {
        var input = Encoding.UTF8.GetBytes("prefix-nEeDlE-suffix-needle");

        var exact = new PreparedSubstringSearch(Encoding.UTF8.GetBytes("needle"), ignoreCase: false);
        var ignoreCase = new PreparedSubstringSearch(Encoding.UTF8.GetBytes("Needle"), ignoreCase: true);

        Assert.Equal(21, exact.IndexOf(input));
        Assert.Equal(7, ignoreCase.IndexOf(input));
        Assert.Equal(21, exact.LastIndexOf(input));
        Assert.Equal(21, ignoreCase.LastIndexOf(input));
    }

    [Fact]
    public void PreparedSubstringSearchSupportsAnchoredExactAsciiSearch()
    {
        var input = Encoding.UTF8.GetBytes("hhhhhhxttpclienthttpclient-tail-httpclient");
        var exact = new PreparedSubstringSearch(Encoding.UTF8.GetBytes("httpclient"), ignoreCase: false);

        Assert.Equal(16, exact.IndexOf(input));
        Assert.Equal(32, exact.LastIndexOf(input));
    }

    [Fact]
    public void PreparedMultiLiteralSearchSupportsExactAndIgnoreCaseFamilies()
    {
        var exactInput = Encoding.UTF8.GetBytes("scan:haystack;scan:needle;scan:zeta;");
        var ignoreCaseInput = Encoding.UTF8.GetBytes("scan:haystack;scan:nEeDlE;scan:zeta;");

        var exact = new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("alpha"),
            Encoding.UTF8.GetBytes("needle"),
            Encoding.UTF8.GetBytes("zeta"),
        ], ignoreCase: false);
        var ignoreCase = new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("Alpha"),
            Encoding.UTF8.GetBytes("Needle"),
            Encoding.UTF8.GetBytes("Zeta"),
        ], ignoreCase: true);

        Assert.Equal(19, exact.IndexOf(exactInput));
        Assert.Equal(19, ignoreCase.IndexOf(ignoreCaseInput));
        Assert.Equal(PreparedMultiLiteralKind.ExactDirect, exact.Kind);
        Assert.Equal(PreparedMultiLiteralKind.AsciiIgnoreCase, ignoreCase.Kind);
        Assert.True(exact.TryGetMatchedLiteralLength(exactInput, 19, out var exactLength));
        Assert.True(ignoreCase.TryGetMatchedLiteralLength(ignoreCaseInput, 19, out var ignoreCaseLength));
        Assert.Equal(6, exactLength);
        Assert.Equal(6, ignoreCaseLength);
    }

    [Fact]
    public void PreparedMultiLiteralSearchPromotesLargeExactSetsToAutomatonBackend()
    {
        var search = new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("abacus"),
            Encoding.UTF8.GetBytes("absorb"),
            Encoding.UTF8.GetBytes("accord"),
            Encoding.UTF8.GetBytes("acumen"),
            Encoding.UTF8.GetBytes("anchor"),
            Encoding.UTF8.GetBytes("anthem"),
            Encoding.UTF8.GetBytes("aspire"),
            Encoding.UTF8.GetBytes("aviate"),
            Encoding.UTF8.GetBytes("beacon"),
            Encoding.UTF8.GetBytes("binary"),
            Encoding.UTF8.GetBytes("bronze"),
            Encoding.UTF8.GetBytes("candid"),
            Encoding.UTF8.GetBytes("cobble"),
            Encoding.UTF8.GetBytes("cortex"),
            Encoding.UTF8.GetBytes("dynamo"),
            Encoding.UTF8.GetBytes("needle"),
        ], ignoreCase: false);
        var input = Encoding.UTF8.GetBytes("scan:haystack;scan:beacon;scan:needle;");

        Assert.Equal(PreparedMultiLiteralKind.ExactAutomaton, search.Kind);
        Assert.True(search.TryFindFirstMatch(input, out var matchIndex, out var matchLength, out var literalId));
        Assert.Equal(19, matchIndex);
        Assert.Equal(6, matchLength);
        Assert.Equal(8, literalId);
        Assert.Equal(19, search.IndexOf(input));
        Assert.Equal(31, search.LastIndexOf(input));
        Assert.True(search.TryGetMatchedLiteralLength(input, 19, out var matchedLength));
        Assert.Equal(6, matchedLength);
    }

    [Fact]
    public void PreparedMultiLiteralAutomatonPrefersEarliestStartAndStableLiteralId()
    {
        var search = new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("b"),
            Encoding.UTF8.GetBytes("ab"),
            Encoding.UTF8.GetBytes("zz0"),
            Encoding.UTF8.GetBytes("zz1"),
            Encoding.UTF8.GetBytes("zz2"),
            Encoding.UTF8.GetBytes("zz3"),
            Encoding.UTF8.GetBytes("zz4"),
            Encoding.UTF8.GetBytes("zz5"),
            Encoding.UTF8.GetBytes("zz6"),
            Encoding.UTF8.GetBytes("zz7"),
            Encoding.UTF8.GetBytes("zz8"),
            Encoding.UTF8.GetBytes("zz9"),
            Encoding.UTF8.GetBytes("zz10"),
            Encoding.UTF8.GetBytes("zz11"),
            Encoding.UTF8.GetBytes("zz12"),
            Encoding.UTF8.GetBytes("zz13"),
        ], ignoreCase: false);
        var input = Encoding.UTF8.GetBytes("xxabyy");

        Assert.Equal(PreparedMultiLiteralKind.ExactAutomaton, search.Kind);
        Assert.True(search.TryFindFirstMatch(input, out var matchIndex, out var matchLength, out var literalId));
        Assert.Equal(2, matchIndex);
        Assert.Equal(2, matchLength);
        Assert.Equal(1, literalId);
        Assert.True(search.TryGetMatchedLiteralInfo(input, 2, out var infoLength, out var infoLiteralId));
        Assert.Equal(2, infoLength);
        Assert.Equal(1, infoLiteralId);
    }

    [Fact]
    public void PreparedSearcherCanReturnFirstMatchCandidateWithLength()
    {
        var searcher = new PreparedSearcher(new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("alpha"),
            Encoding.UTF8.GetBytes("needle"),
            Encoding.UTF8.GetBytes("omega"),
        ], ignoreCase: false));
        var input = Encoding.UTF8.GetBytes("scan:haystack;scan:needle;scan:omega;");

        Assert.True(searcher.TryFindFirstMatch(input, out var match));
        Assert.Equal(19, match.Index);
        Assert.Equal(6, match.Length);
        Assert.Equal(1, match.LiteralId);
    }

    [Fact]
    public void PreparedLiteralSetSearchCanMatchSharedPrefixFamilyThroughPrefixDiscriminator()
    {
        var exact = new PreparedLiteralSetSearch(
        [
            Encoding.UTF8.GetBytes("LogTrace"),
            Encoding.UTF8.GetBytes("LogDebug"),
            Encoding.UTF8.GetBytes("LogInformation"),
            Encoding.UTF8.GetBytes("LogWarning"),
            Encoding.UTF8.GetBytes("LogError"),
        ]);
        var input = Encoding.UTF8.GetBytes("..LogWarning(..LogError(..");

        Assert.Equal(PreparedLiteralSetStrategy.SingleBucketPrefix, exact.Strategy);
        Assert.True(exact.SearchData.Buckets[0].PrefixDiscriminator.HasValue);
        Assert.True(exact.TryFindFirstMatchWithLength(input, out var index, out var matchedLength));
        Assert.Equal(2, index);
        Assert.Equal("LogWarning".Length, matchedLength);
        Assert.True(exact.TryGetMatchedLiteralLength(input, index, out var lengthAtIndex));
        Assert.Equal("LogWarning".Length, lengthAtIndex);
    }

    [Fact]
    public void PreparedMultiLiteralPackedSearchCanMatchSharedPrefixFamily()
    {
        Assert.True(PreparedMultiLiteralPackedSearch.TryCreate(
        [
            Encoding.UTF8.GetBytes("LogTrace"),
            Encoding.UTF8.GetBytes("LogDebug"),
            Encoding.UTF8.GetBytes("LogInformation"),
            Encoding.UTF8.GetBytes("LogWarning"),
            Encoding.UTF8.GetBytes("LogError"),
        ], out var search));
        var input = Encoding.UTF8.GetBytes("x LogWarning y LogError z");

        Assert.True(search.TryFindFirstMatch(input, out var matchIndex, out var matchLength, out var literalId));
        Assert.Equal(2, matchIndex);
        Assert.Equal("LogWarning".Length, matchLength);
        Assert.Equal(3, literalId);
        Assert.Equal(2, search.IndexOf(input));
        Assert.Equal(15, search.LastIndexOf(input));
    }

    [Fact]
    public void PreparedSearcherCanReturnLastMatchCandidateWithIdentity()
    {
        var searcher = new PreparedSearcher(new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("alpha"),
            Encoding.UTF8.GetBytes("needle"),
            Encoding.UTF8.GetBytes("omega"),
        ], ignoreCase: false));
        var input = Encoding.UTF8.GetBytes("scan:alpha;scan:needle;scan:omega;");

        Assert.True(searcher.TryFindLastMatch(input, out var match));
        Assert.Equal(28, match.Index);
        Assert.Equal(5, match.Length);
        Assert.Equal(2, match.LiteralId);
    }

    [Fact]
    public void PreparedAsciiFindPlanCanFindLiteralFamilyAnchors()
    {
        var plan = PreparedAsciiFindPlan.CreateLiteralFamily(
        [
            Encoding.UTF8.GetBytes("Watson"),
            Encoding.UTF8.GetBytes("Holmes"),
        ]);
        var input = Encoding.UTF8.GetBytes("Inspect Holmes beside Watson");

        Assert.True(Utf8AsciiFindExecutor.TryFindNextAnchor(input, plan, 0, out var anchorIndex, out var matchedLength));
        Assert.Equal(8, anchorIndex);
        Assert.Equal(6, matchedLength);
    }

    [Fact]
    public void PreparedAsciiFindPlanCanFindFixedDistanceLiteralCandidates()
    {
        var plan = PreparedAsciiFindPlan.CreateFixedDistanceLiteral(Encoding.UTF8.GetBytes("Holmes"), 2);
        var input = Encoding.UTF8.GetBytes("aaHolmes zz");

        Assert.True(Utf8AsciiFindExecutor.TryFindNextFixedDistanceCandidate(input, plan, 0, out var candidateIndex, out var matchedLength));
        Assert.Equal(0, candidateIndex);
        Assert.Equal(6, matchedLength);
    }

    [Fact]
    public void PreparedMultiLiteralSearchCanScanNonOverlappingMatches()
    {
        var search = new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("ab"),
            Encoding.UTF8.GetBytes("needle"),
            Encoding.UTF8.GetBytes("omega"),
            Encoding.UTF8.GetBytes("zz0"),
            Encoding.UTF8.GetBytes("zz1"),
            Encoding.UTF8.GetBytes("zz2"),
            Encoding.UTF8.GetBytes("zz3"),
            Encoding.UTF8.GetBytes("zz4"),
            Encoding.UTF8.GetBytes("zz5"),
            Encoding.UTF8.GetBytes("zz6"),
            Encoding.UTF8.GetBytes("zz7"),
            Encoding.UTF8.GetBytes("zz8"),
            Encoding.UTF8.GetBytes("zz9"),
            Encoding.UTF8.GetBytes("zz10"),
            Encoding.UTF8.GetBytes("zz11"),
            Encoding.UTF8.GetBytes("zz12"),
        ], ignoreCase: false);
        var input = Encoding.UTF8.GetBytes("ab--needle--omega");
        var state = new PreparedMultiLiteralScanState(0, 0, 0);

        Assert.True(search.TryFindNextNonOverlappingMatch(input, ref state, out var firstIndex, out var firstLength, out var firstLiteralId));
        Assert.Equal(0, firstIndex);
        Assert.Equal(2, firstLength);
        Assert.Equal(0, firstLiteralId);

        Assert.True(search.TryFindNextNonOverlappingMatch(input, ref state, out var secondIndex, out var secondLength, out var secondLiteralId));
        Assert.Equal(4, secondIndex);
        Assert.Equal(6, secondLength);
        Assert.Equal(1, secondLiteralId);

        Assert.True(search.TryFindNextNonOverlappingMatch(input, ref state, out var thirdIndex, out var thirdLength, out var thirdLiteralId));
        Assert.Equal(12, thirdIndex);
        Assert.Equal(5, thirdLength);
        Assert.Equal(2, thirdLiteralId);

        Assert.False(search.TryFindNextNonOverlappingMatch(input, ref state, out _, out _, out _));
    }

    [Fact]
    public void PreparedMultiLiteralRootByteFamilySearchCanScanNonOverlappingLengths()
    {
        var search = new PreparedMultiLiteralRootByteFamilySearch(
        [
            Encoding.UTF8.GetBytes("Sherlock Holmes"),
            Encoding.UTF8.GetBytes("John Watson"),
            Encoding.UTF8.GetBytes("Irene Adler"),
            Encoding.UTF8.GetBytes("Inspector Lestrade"),
            Encoding.UTF8.GetBytes("Professor Moriarty"),
        ]);
        var input = Encoding.UTF8.GetBytes("Sherlock Holmes and John Watson met Irene Adler.");
        var state = new PreparedMultiLiteralScanState(0, 0, 0);

        Assert.True(search.TryFindNextNonOverlappingLength(input, ref state, out var firstIndex, out var firstLength));
        Assert.Equal(0, firstIndex);
        Assert.Equal("Sherlock Holmes".Length, firstLength);

        Assert.True(search.TryFindNextNonOverlappingLength(input, ref state, out var secondIndex, out var secondLength));
        Assert.Equal(20, secondIndex);
        Assert.Equal("John Watson".Length, secondLength);

        Assert.True(search.TryFindNextNonOverlappingLength(input, ref state, out var thirdIndex, out var thirdLength));
        Assert.Equal(36, thirdIndex);
        Assert.Equal("Irene Adler".Length, thirdLength);
    }

    [Fact]
    public void PreparedMultiLiteralEarliestExactSearchCanScanNonOverlappingLengths()
    {
        var search = new PreparedMultiLiteralEarliestExactSearch(
        [
            Encoding.UTF8.GetBytes("Sherlock Holmes"),
            Encoding.UTF8.GetBytes("John Watson"),
            Encoding.UTF8.GetBytes("Irene Adler"),
            Encoding.UTF8.GetBytes("Inspector Lestrade"),
            Encoding.UTF8.GetBytes("Professor Moriarty"),
        ]);
        var input = Encoding.UTF8.GetBytes("Sherlock Holmes and John Watson met Irene Adler.");
        var state = new PreparedMultiLiteralScanState(0, 0, 0);

        Assert.True(search.TryFindNextNonOverlappingLength(input, ref state, out var firstIndex, out var firstLength));
        Assert.Equal(0, firstIndex);
        Assert.Equal("Sherlock Holmes".Length, firstLength);

        Assert.True(search.TryFindNextNonOverlappingLength(input, ref state, out var secondIndex, out var secondLength));
        Assert.Equal(20, secondIndex);
        Assert.Equal("John Watson".Length, secondLength);

        Assert.True(search.TryFindNextNonOverlappingLength(input, ref state, out var thirdIndex, out var thirdLength));
        Assert.Equal(36, thirdIndex);
        Assert.Equal("Irene Adler".Length, thirdLength);
    }

    [Fact]
    public void PreparedSmallAsciiLiteralFamilySearchCountsMixedFamily()
    {
        byte[][] literals =
        [
            Encoding.UTF8.GetBytes("Sherlock Holmes"),
            Encoding.UTF8.GetBytes("John Watson"),
            Encoding.UTF8.GetBytes("Mycroft Holmes"),
            Encoding.UTF8.GetBytes("Mary Morstan"),
            Encoding.UTF8.GetBytes("Mrs Hudson"),
        ];
        var input = Encoding.UTF8.GetBytes("Sherlock Holmes xx Mary Morstan yy Mrs Hudson");

        Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var search));
        Assert.Equal(3, search.CountScalar(input));
        Assert.Equal(3, search.Count(input));
    }

    [Fact]
    public void PreparedSmallAsciiLiteralFamilySearchFindsNonOverlappingMatches()
    {
        byte[][] literals =
        [
            Encoding.UTF8.GetBytes("Sherlock Holmes"),
            Encoding.UTF8.GetBytes("John Watson"),
            Encoding.UTF8.GetBytes("Mycroft Holmes"),
            Encoding.UTF8.GetBytes("Mary Morstan"),
            Encoding.UTF8.GetBytes("Mrs Hudson"),
        ];
        var input = Encoding.UTF8.GetBytes("John Watson and Mrs Hudson");

        Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var search));
        var start = 0;
        Assert.True(search.TryFindNextNonOverlapping(input, ref start, out var index0, out var length0));
        Assert.Equal(0, index0);
        Assert.Equal("John Watson".Length, length0);
        Assert.True(search.TryFindNextNonOverlapping(input, ref start, out var index1, out var length1));
        Assert.Equal("John Watson and ".Length, index1);
        Assert.Equal("Mrs Hudson".Length, length1);
        Assert.False(search.TryFindNextNonOverlapping(input, ref start, out _, out _));
    }

    [Fact]
    public void PreparedSmallAsciiLiteralFamilySearchFindsFirstMatch()
    {
        byte[][] literals =
        [
            Encoding.UTF8.GetBytes("tempus"),
            Encoding.UTF8.GetBytes("magna"),
            Encoding.UTF8.GetBytes("semper"),
        ];
        var input = Encoding.UTF8.GetBytes("lorem magna ipsum semper");

        Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var search));
        Assert.True(search.TryFindFirst(input, out var index, out var matchedLength));
        Assert.Equal("lorem ".Length, index);
        Assert.Equal("magna".Length, matchedLength);
    }

    [Fact]
    public void PreparedSmallAsciiLiteralFamilySearchSkipsFalseCandidateBeforeFirstMatch()
    {
        byte[][] literals =
        [
            Encoding.UTF8.GetBytes("tempus"),
            Encoding.UTF8.GetBytes("magna"),
            Encoding.UTF8.GetBytes("semper"),
        ];
        var input = Encoding.UTF8.GetBytes("temxus magna");

        Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var search));
        Assert.True(search.TryFindFirst(input, out var index, out var matchedLength));
        Assert.Equal("temxus ".Length, index);
        Assert.Equal("magna".Length, matchedLength);
    }

    [Fact]
    public void PreparedShortAsciiLiteralFamilyCounterCountsThreeLiteralMatchesWordsShape()
    {
        byte[][] literals =
        [
            Encoding.UTF8.GetBytes("tempus"),
            Encoding.UTF8.GetBytes("magna"),
            Encoding.UTF8.GetBytes("semper"),
        ];
        var input = Encoding.UTF8.GetBytes("tempus lorem magna ipsum semper magna");

        Assert.True(PreparedShortAsciiLiteralFamilyCounter.TryCreate(literals, out var counter));
        Assert.Equal(4, counter.Count(input));
    }

    [Fact]
    public void PreparedShortAsciiLiteralFamilyCounterCountsTwoLiteralFamily()
    {
        byte[][] literals =
        [
            Encoding.UTF8.GetBytes("alpha"),
            Encoding.UTF8.GetBytes("omega"),
        ];
        var input = Encoding.UTF8.GetBytes("alpha x omega alpha");

        Assert.True(PreparedShortAsciiLiteralFamilyCounter.TryCreate(literals, out var counter));
        Assert.Equal(3, counter.Count(input));
    }

    [Fact]
    public void PreparedShortAsciiLiteralFamilyCounterCountsFourLiteralFamily()
    {
        byte[][] literals =
        [
            Encoding.UTF8.GetBytes("crown"),
            Encoding.UTF8.GetBytes("flame"),
            Encoding.UTF8.GetBytes("ghost"),
            Encoding.UTF8.GetBytes("pride"),
        ];
        var input = Encoding.UTF8.GetBytes("crown pride flame ghost crown");

        Assert.True(PreparedShortAsciiLiteralFamilyCounter.TryCreate(literals, out var counter));
        Assert.Equal(5, counter.Count(input));
    }

    [Fact]
    public void PreparedSmallAsciiLiteralFamilySearchScalarAndSimdStayAlignedOnMisses()
    {
        byte[][] literals =
        [
            Encoding.UTF8.GetBytes("Sherlock Holmes"),
            Encoding.UTF8.GetBytes("John Watson"),
            Encoding.UTF8.GetBytes("Mycroft Holmes"),
            Encoding.UTF8.GetBytes("Mary Morstan"),
            Encoding.UTF8.GetBytes("Mrs Hudson"),
        ];
        var input = Encoding.UTF8.GetBytes(new string('x', 4096));

        Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var search));
        Assert.Equal(search.CountScalar(input), search.Count(input));
    }

    [Fact]
    public void PreparedSmallAsciiLiteralFamilySearchIgnoresTrailingPartialCandidates()
    {
        byte[][] literals =
        [
            Encoding.UTF8.GetBytes("Mycroft Holmes"),
            Encoding.UTF8.GetBytes("Mary Morstan"),
            Encoding.UTF8.GetBytes("Mrs Hudson"),
            Encoding.UTF8.GetBytes("Sebastian Moran"),
            Encoding.UTF8.GetBytes("Charles Augustus Milverton"),
        ];
        var input = Encoding.UTF8.GetBytes(new string('x', 1024) + "Mary");

        Assert.True(PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out var search));
        Assert.Equal(0, search.CountScalar(input));
        Assert.Equal(0, search.Count(input));
    }

    [Fact]
    public void PreparedSearcherCanScanOverlappingMatches()
    {
        var searcher = new PreparedSearcher(new PreparedMultiLiteralSearch(
        [
            Encoding.UTF8.GetBytes("aba"),
            Encoding.UTF8.GetBytes("ba"),
        ], ignoreCase: false));
        var input = Encoding.UTF8.GetBytes("ababa");
        var state = new PreparedSearchScanState(0, default);

        Assert.True(searcher.TryFindNextOverlappingMatch(input, ref state, out var first));
        Assert.Equal(0, first.Index);
        Assert.Equal(3, first.Length);
        Assert.Equal(0, first.LiteralId);

        Assert.True(searcher.TryFindNextOverlappingMatch(input, ref state, out var second));
        Assert.Equal(1, second.Index);
        Assert.Equal(2, second.Length);
        Assert.Equal(1, second.LiteralId);

        Assert.True(searcher.TryFindNextOverlappingMatch(input, ref state, out var third));
        Assert.Equal(2, third.Index);
        Assert.Equal(3, third.Length);
        Assert.Equal(0, third.LiteralId);

        Assert.True(searcher.TryFindNextOverlappingMatch(input, ref state, out var fourth));
        Assert.Equal(3, fourth.Index);
        Assert.Equal(2, fourth.Length);
        Assert.Equal(1, fourth.LiteralId);

        Assert.False(searcher.TryFindNextOverlappingMatch(input, ref state, out _));
    }

    [Fact]
    public void PreparedWindowSearchCanScanStatefully()
    {
        var window = new PreparedWindowSearch(
            new PreparedSearcher(new PreparedMultiLiteralSearch(
            [
                Encoding.UTF8.GetBytes("using var"),
                Encoding.UTF8.GetBytes("await using var"),
            ], ignoreCase: false)),
            new PreparedSearcher(new PreparedSubstringSearch(Encoding.UTF8.GetBytes("await"), ignoreCase: false), ignoreCase: false));
        var input = Encoding.UTF8.GetBytes("using var x = await;\nawait using var y = await;");
        var state = new PreparedWindowScanState(0, new PreparedSearchScanState(0, default));

        Assert.True(window.TryFindNextWindow(input, ref state, out var first));
        Assert.Equal("using var", Encoding.UTF8.GetString(input[first.Leading.Index..(first.Leading.Index + first.Leading.Length)]));
        Assert.Equal("await", Encoding.UTF8.GetString(input[first.Trailing.Index..(first.Trailing.Index + first.Trailing.Length)]));

        Assert.True(window.TryFindNextWindow(input, ref state, out var second));
        Assert.Equal("await using var", Encoding.UTF8.GetString(input[second.Leading.Index..(second.Leading.Index + second.Leading.Length)]));
        Assert.Equal("await", Encoding.UTF8.GetString(input[second.Trailing.Index..(second.Trailing.Index + second.Trailing.Length)]));

        Assert.True(window.TryFindNextWindow(input, ref state, out var third));
        Assert.Equal("using var", Encoding.UTF8.GetString(input[third.Leading.Index..(third.Leading.Index + third.Leading.Length)]));
        Assert.Equal("await", Encoding.UTF8.GetString(input[third.Trailing.Index..(third.Trailing.Index + third.Trailing.Length)]));

        Assert.False(window.TryFindNextWindow(input, ref state, out _));
    }

    [Fact]
    public void PreparedFallbackWindowCandidatesCarryTrailingEnd()
    {
        var source = new PreparedFallbackCandidateSource(
            new PreparedWindowSearch(
                new PreparedSearcher(new PreparedMultiLiteralSearch(
                [
                    Encoding.UTF8.GetBytes("using var"),
                    Encoding.UTF8.GetBytes("await using var"),
                ], ignoreCase: false)),
                new PreparedSearcher(new PreparedSubstringSearch(Encoding.UTF8.GetBytes("await"), ignoreCase: false), ignoreCase: false)));
        var input = Encoding.UTF8.GetBytes("using var x = await;");
        var state = new PreparedFallbackCandidateState(default, new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.True(source.TryFindNextCandidate(input, ref state, out var candidate));
        Assert.Equal(0, candidate.StartIndex);
        Assert.Equal(19, candidate.EndIndex);
    }

    [Fact]
    public void PreparedWindowSearchFindsAnchorsWithoutApplyingLineConstraints()
    {
        var window = new PreparedWindowSearch(
            new PreparedSearcher(new PreparedMultiLiteralSearch([Encoding.UTF8.GetBytes("using var")], ignoreCase: false)),
            new PreparedSearcher(new PreparedSubstringSearch(Encoding.UTF8.GetBytes("await"), ignoreCase: false), ignoreCase: false),
            maxGap: 64,
            sameLine: true);
        var input = Encoding.UTF8.GetBytes("using var value =\nawait");
        var state = new PreparedWindowScanState(0, new PreparedSearchScanState(0, default));

        Assert.True(window.TryFindNextWindow(input, ref state, out var match));
        Assert.Equal(0, match.Leading.Index);
        Assert.Equal(input.Length - "await".Length, match.Trailing.Index);
    }

    [Fact]
    public void PreparedFallbackWindowCandidatesStillApplyLegacyLineConstraints()
    {
        var source = new PreparedFallbackCandidateSource(
            new PreparedWindowSearch(
                new PreparedSearcher(new PreparedMultiLiteralSearch([Encoding.UTF8.GetBytes("using var")], ignoreCase: false)),
                new PreparedSearcher(new PreparedSubstringSearch(Encoding.UTF8.GetBytes("await"), ignoreCase: false), ignoreCase: false),
                maxGap: 64,
                sameLine: true));
        var input = Encoding.UTF8.GetBytes("using var value =\nawait");
        var state = new PreparedFallbackCandidateState(default, new PreparedWindowScanState(0, new PreparedSearchScanState(0, default)));

        Assert.False(source.TryFindNextCandidate(input, ref state, out _));
    }

    [Fact]
    public void PreparedQuotedAsciiRunSearchCanFindQuotedRun()
    {
        var search = new PreparedQuotedAsciiRunSearch(Lokad.Utf8Regex.Internal.FrontEnd.Runtime.RegexCharClass.AsciiLetterOrDigitClass, 4);
        var input = Encoding.UTF8.GetBytes("xx \"ab12\" yy");

        Assert.Equal(3, search.IndexOf(input));
        Assert.True(search.IsMatchAt(input, 3));
        Assert.Equal(6, search.MatchLength);
    }

    [Fact]
    public void PreparedQuotedAsciiRunSearchCanFindLastQuotedRun()
    {
        var search = new PreparedQuotedAsciiRunSearch(Lokad.Utf8Regex.Internal.FrontEnd.Runtime.RegexCharClass.AsciiLetterOrDigitClass, 4);
        var input = Encoding.UTF8.GetBytes("'ab12' xx \"xy99\"");

        Assert.Equal(10, search.LastIndexOf(input));
    }
}
