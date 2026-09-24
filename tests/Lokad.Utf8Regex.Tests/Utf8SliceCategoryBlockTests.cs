using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// R03 blocks under IgnoreCase: BCL expands named-block ranges with
// per-character case equivalents (RegexCharClass.AddCaseEquivalenceRange),
// so the slice matches block ranges through the invariant case-equivalence
// table (candidate-side lookup, no parse-time expansion) instead of declining.
// The negated test keeps the BCL overlap: a scalar whose equivalents straddle
// the ranges hits both parities (U+212A against IsBasicLatin), so negation is
// decided inside the matcher rather than XORed outside. Class-negated block
// atoms ([^\p{...}]) and double-negated ones ([^\P{...}]) fall through to the
// general class path, which keeps both negations; general categories negate
// cleanly and still fold to atoms. Only the invariant i-branch applies (slice
// IgnoreCase folds invariantly; Turkic-style cultures stay on legacy routing).
public sealed class Utf8SliceCategoryBlockTests
{
    private const RegexOptions IgnoreCaseOptions = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static IEnumerable<object?[]> OwnedBlockShapes()
    {
        yield return new object?[] { "\\p{IsBasicLatin}+" };
        yield return new object?[] { "\\P{IsBasicLatin}+" };
        yield return new object?[] { "\\p{IsCherokee}+" };
        yield return new object?[] { "\\P{IsCherokee}+" };
        yield return new object?[] { "\\p{IsLatinExtended-A}+" };
        yield return new object?[] { "\\p{IsGreekandCoptic}+" };
        yield return new object?[] { "[\\p{IsBasicLatin}]+" };
        yield return new object?[] { "[^\\p{IsBasicLatin}]+" };
        yield return new object?[] { "[\\P{IsBasicLatin}]+" };
        yield return new object?[] { "[\\p{IsBasicLatin}a]+" };
        yield return new object?[] { "[^\\p{IsBasicLatin}a]+" };
        yield return new object?[] { "[\\P{IsBasicLatin}a]+" };
        yield return new object?[] { "[^\\P{IsBasicLatin}]+" };
        yield return new object?[] { "[\\p{IsCherokee}ab]+" };
        yield return new object?[] { "[^\\p{IsCherokee}ab]+" };
        yield return new object?[] { "[\\p{IsBasicLatin}\\d]+" };
        yield return new object?[] { "[\\p{IsBasicLatin}-x]+" };
        yield return new object?[] { "\\p{_xmlC}+" };
        yield return new object?[] { "\\P{_xmlC}+" };
        yield return new object?[] { "\\p{_xmlD}+" };
        yield return new object?[] { "[\\p{_xmlC}a]+" };
        yield return new object?[] { "(?i:\\p{IsBasicLatin}+)" };
    }

    [Theory]
    [MemberData(nameof(OwnedBlockShapes))]
    public void BlockFoldShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            IgnoreCaseOptions,
            RegexOptions.Compiled | IgnoreCaseOptions,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> BlockFoldIsMatchCases()
    {
        var ic = IgnoreCaseOptions;
        var cs = RegexOptions.CultureInvariant;
        yield return new object?[] { "\\p{IsBasicLatin}+", "a", ic, true };
        yield return new object?[] { "\\p{IsBasicLatin}+", "\u212A", ic, true };
        yield return new object?[] { "\\p{IsBasicLatin}+", "\u017F", ic, false };
        yield return new object?[] { "\\p{IsBasicLatin}+", "1", ic, true };
        yield return new object?[] { "\\p{IsBasicLatin}+", "\u212A", cs, false };
        yield return new object?[] { "\\p{IsBasicLatin}+", "\u017F", cs, false };
        yield return new object?[] { "\\p{IsBasicLatin}+", "\u00E9", ic, false };
        yield return new object?[] { "\\p{IsBasicLatin}+", "\u0130", ic, false };
        yield return new object?[] { "\\p{IsBasicLatin}+", "a", cs, true };
        yield return new object?[] { "\\p{IsCherokee}+", "\u13A0", ic, true };
        yield return new object?[] { "\\p{IsCherokee}+", "\uAB70", ic, true };
        yield return new object?[] { "\\p{IsCherokee}+", "\uAB70", cs, false };
        yield return new object?[] { "\\p{IsLatinExtended-A}+", "\u0131", ic, true };
        yield return new object?[] { "\\p{IsLatinExtended-A}+", "I", ic, false };
        yield return new object?[] { "\\p{IsGreekandCoptic}+", "\u03A3", ic, true };
        yield return new object?[] { "\\p{IsGreekandCoptic}+", "\u03C2", ic, true };
        yield return new object?[] { "\\P{IsBasicLatin}+", "\u212A", ic, true };
        yield return new object?[] { "\\P{IsBasicLatin}+", "k", ic, true };
        yield return new object?[] { "\\P{IsBasicLatin}+", "a", ic, false };
        yield return new object?[] { "\\P{IsBasicLatin}+", "\u00E9", ic, true };
        yield return new object?[] { "\\P{IsBasicLatin}+", "\u017F", ic, true };
        yield return new object?[] { "\\P{IsBasicLatin}+", "1", ic, false };
        yield return new object?[] { "\\P{IsBasicLatin}+", "\u212A", cs, true };
        yield return new object?[] { "\\P{IsBasicLatin}+", "k", cs, false };
        yield return new object?[] { "\\P{IsCherokee}+", "\u13A0", ic, true };
        yield return new object?[] { "\\P{IsCherokee}+", "\uAB70", ic, true };
        yield return new object?[] { "[\\p{IsBasicLatin}a]+", "\u212A", ic, true };
        yield return new object?[] { "[\\p{IsBasicLatin}a]+", "Q", ic, true };
        yield return new object?[] { "[\\p{IsBasicLatin}a]+", "\u00E9", ic, false };
        yield return new object?[] { "[^\\p{IsBasicLatin}a]+", "k", ic, false };
        yield return new object?[] { "[^\\p{IsBasicLatin}a]+", "\u00E9", ic, true };
        yield return new object?[] { "[^\\p{IsBasicLatin}a]+", "\u212A", ic, false };
        yield return new object?[] { "[\\P{IsBasicLatin}a]+", "k", ic, true };
        yield return new object?[] { "[\\P{IsBasicLatin}a]+", "\u00E9", ic, true };
        yield return new object?[] { "[\\P{IsBasicLatin}a]+", "1", ic, false };
        yield return new object?[] { "[^\\P{IsBasicLatin}]+", "1", ic, true };
        yield return new object?[] { "[^\\P{IsBasicLatin}]+", "k", ic, false };
        yield return new object?[] { "[^\\P{IsBasicLatin}]+", "\u00E9", ic, false };
        yield return new object?[] { "[\\p{IsCherokee}ab]+", "\uAB70", ic, true };
        yield return new object?[] { "[^\\p{IsCherokee}ab]+", "\uAB70", ic, false };
        yield return new object?[] { "[^\\p{IsCherokee}ab]+", "1", ic, true };
        yield return new object?[] { "[\\p{IsBasicLatin}\\d]+", "\u212A", ic, true };
        yield return new object?[] { "(?i:\\p{IsBasicLatin}+)", "\u212A", cs, true };
        yield return new object?[] { "[\\p{IsBasicLatin}-x]+", "-", ic, true };
        yield return new object?[] { "[\\p{IsBasicLatin}-x]+", "\u212A", ic, true };
        yield return new object?[] { "[\\p{IsBasicLatin}]+", "a", ic, true };
        yield return new object?[] { "[^\\p{IsBasicLatin}]+", "k", ic, false };
        yield return new object?[] { "[\\P{IsBasicLatin}]+", "k", ic, true };
        yield return new object?[] { "\\p{_xmlC}+", "a", ic, true };
        yield return new object?[] { "\\p{_xmlC}+", "-", ic, true };
        yield return new object?[] { "\\p{_xmlC}+", "\u212A", ic, true };
        yield return new object?[] { "\\p{_xmlC}+", "\u00E9", ic, true };
        yield return new object?[] { "\\P{_xmlC}+", "\u212A", ic, false };
        yield return new object?[] { "\\P{_xmlC}+", "a", ic, false };
        yield return new object?[] { "[\\p{_xmlC}a]+", "\u212A", ic, true };
        yield return new object?[] { "[\\p{_xmlC}a]+", "\u00E9", ic, true };
        yield return new object?[] { "\\p{_xmlD}+", "5", ic, true };
        yield return new object?[] { "\\p{_xmlD}+", "a", ic, false };
    }

    [Theory]
    [MemberData(nameof(BlockFoldIsMatchCases))]
    public void OwnedBlockFoldIsMatchMatchesOracle(string pattern, string inputText, RegexOptions options, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var compiled in new[] { false, true })
        {
            var effective = compiled ? options | RegexOptions.Compiled : options;
            var regex = new Utf8Regex(pattern, effective);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, effective.ToString() + " pattern=" + pattern);
            Assert.Equal(expected, regex.IsMatch(input));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, effective.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> BlockFoldCountCases()
    {
        yield return new object?[] { "\\p{IsBasicLatin}+", "aK1", 1 };
        yield return new object?[] { "\\P{IsBasicLatin}+", "aK", 1 };
        yield return new object?[] { "\\p{IsCherokee}+", "x\u13A0\uAB70y", 1 };
        yield return new object?[] { "[\\p{IsBasicLatin}a]+", "1aKB\u00E9", 1 };
        yield return new object?[] { "[^\\p{IsBasicLatin}a]+", "aKB\u00E9", 1 };
        yield return new object?[] { "[\\P{IsBasicLatin}a]+", "1\u00E9", 1 };
    }

    [Theory]
    [MemberData(nameof(BlockFoldCountCases))]
    public void OwnedBlockFoldCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, IgnoreCaseOptions);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> BlockFoldMatchCases()
    {
        yield return new object?[] { "([\\p{IsBasicLatin}a]+)", "1aKB", 0, 4 };
        yield return new object?[] { "([^\\p{IsBasicLatin}a]+)", "aKB\u00E9", 3, 1 };
        yield return new object?[] { "(\\p{IsCherokee}+)", "x\u13A0\uAB70y", 1, 2 };
        yield return new object?[] { "(\\P{IsBasicLatin}+)", "aK\u00E9", 1, 2 };
    }

    [Theory]
    [MemberData(nameof(BlockFoldMatchCases))]
    public void OwnedBlockFoldMatchMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        Assert.Equal(expectedStart, expected.Index);
        Assert.Equal(expectedLength, expected.Length);
        var regex = new Utf8Regex(pattern, IgnoreCaseOptions);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.Match(input);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.IndexInUtf16);
        Assert.Equal(expectedLength, found.LengthInUtf16);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [MemberData(nameof(BlockFoldMatchCases))]
    public void OwnedBlockFoldDetailedMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        var regex = new Utf8Regex(pattern, IgnoreCaseOptions);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        Assert.Equal(expectedStart, detailed.IndexInUtf16);
        Assert.Equal(expectedLength, detailed.LengthInUtf16);
        Assert.Equal(1, detailed.GroupCount - 1);
        var group = detailed.GetGroup(1);
        Assert.True(group.Success);
        Assert.Equal(expectedStart, group.IndexInUtf16);
        Assert.Equal(expectedLength, group.LengthInUtf16);
        Assert.Equal(expected.Groups[1].Value, group.GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> BlockFoldEnumerateCases()
    {
        yield return new object?[] { "\\P{IsBasicLatin}+", "aK B", new int[] { 1, 1 } };
        yield return new object?[] { "[^\\p{IsBasicLatin}a]+", "aKB\u00E9", new int[] { 3, 1 } };
    }

    [Theory]
    [MemberData(nameof(BlockFoldEnumerateCases))]
    public void OwnedBlockFoldEnumerateMatchesOracle(string pattern, string inputText, int[] expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        var wanted = new List<string>();
        foreach (Match match in oracle.Matches(inputText))
        {
            wanted.Add(match.Index + ":" + match.Length);
        }

        var regex = new Utf8Regex(pattern, IgnoreCaseOptions);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actual = new List<string>();
        foreach (var match in regex.EnumerateMatches(input))
        {
            actual.Add(match.IndexInUtf16 + ":" + match.LengthInUtf16);
        }

        Assert.Equal(wanted, actual);
        var flat = new List<int>();
        foreach (var item in actual)
        {
            var parts = item.Split((char)58);
            flat.Add(int.Parse(parts[0]));
            flat.Add(int.Parse(parts[1]));
        }

        Assert.Equal(expected, flat.ToArray());
    }

    public static IEnumerable<object?[]> BlockFoldReplaceCases()
    {
        yield return new object?[] { "[\\p{IsBasicLatin}a]+", "1aKB", "<1aKB>" };
        yield return new object?[] { "[^\\p{IsBasicLatin}a]+", "aKB\u00E9", "aKB<\u00E9>" };
        yield return new object?[] { "\\P{IsBasicLatin}+", "aK", "a<K>" };
    }

    [Theory]
    [MemberData(nameof(BlockFoldReplaceCases))]
    public void OwnedBlockFoldReplaceMatchesOracle(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Replace(inputText, "<$&>"));
        var regex = new Utf8Regex(pattern, IgnoreCaseOptions);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Replace(input, "<$&>")));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> BlockFoldSplitCases()
    {
        yield return new object?[] { "[\\p{IsBasicLatin},]+", "aK,x", 0, new string[] { "", "" } };
    }

    [Theory]
    [MemberData(nameof(BlockFoldSplitCases))]
    public void OwnedBlockFoldSplitMatchesOracle(string pattern, string inputText, int count, string[] expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Split(inputText, count));
        var regex = new Utf8Regex(pattern, IgnoreCaseOptions);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actual = new List<string>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add(split.GetValueString());
        }

        Assert.Equal(expected, actual.ToArray());
    }

    [Theory]
    [InlineData("[\\p{IsBasicLatin}a]+", "1aKB", 1, true, 1, 3)]
    [InlineData("[^\\p{IsBasicLatin}a]+", "aKB\u00E9", 3, true, 3, 1)]
    public void OwnedBlockFoldFromOffsetMatchesOracle(string pattern, string inputText, int startAt, bool expectedSuccess, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        var wanted = oracle.Match(inputText, startAt);
        Assert.Equal(expectedSuccess, wanted.Success);
        var regex = new Utf8Regex(pattern, IgnoreCaseOptions);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.MatchFromUtf16Offset(input, startAt);
        Assert.Equal(expectedSuccess, found.Success);
        Assert.Equal(oracle.IsMatch(inputText, startAt), regex.IsMatchFromUtf16Offset(input, startAt));
        Assert.Equal(oracle.Matches(inputText, startAt).Count, regex.CountFromUtf16Offset(input, startAt));
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, wanted.Index);
            Assert.Equal(expectedLength, wanted.Length);
            Assert.Equal(expectedStart, found.IndexInUtf16);
            Assert.Equal(expectedLength, found.LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> BlockFoldRtlCases()
    {
        yield return new object?[] { "\\p{IsBasicLatin}+", "aK\u00E9", true, 0, 2 };
        yield return new object?[] { "[\\P{IsBasicLatin}a]+", "1\u00E9", true, 1, 1 };
        yield return new object?[] { "[^\\p{IsBasicLatin}a]+", "aKB\u00E9", true, 3, 1 };
        yield return new object?[] { "\\P{IsBasicLatin}+", "aK", true, 1, 1 };
        yield return new object?[] { "\\p{IsCherokee}+", "x\u13A0\uAB70y", true, 1, 2 };
    }

    [Theory]
    [MemberData(nameof(BlockFoldRtlCases))]
    public void OwnedBlockFoldRightToLeftMatchesOracle(string pattern, string inputText, bool expectedSuccess, int expectedStart, int expectedLength)
    {
        var options = RegexOptions.RightToLeft | IgnoreCaseOptions;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var wanted = oracle.Match(inputText);
        Assert.Equal(expectedSuccess, wanted.Success);
        Assert.Equal(expectedStart, wanted.Index);
        Assert.Equal(expectedLength, wanted.Length);
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.Match(input);
        Assert.Equal(expectedSuccess, found.Success);
        Assert.Equal(expectedStart, found.IndexInUtf16);
        Assert.Equal(expectedLength, found.LengthInUtf16);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> BlockFoldAstralCases()
    {
        yield return new object?[] { "\\p{IsBasicLatin}+", "a\U0001F600" };
        yield return new object?[] { "[\\p{IsBasicLatin}a]+", "a\U0001F600" };
        yield return new object?[] { "\\P{IsBasicLatin}+", "\U0001F600" };
    }

    [Theory]
    [MemberData(nameof(BlockFoldAstralCases))]
    public void BlockFoldAstralInputsDeclineToLegacy(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        // The program stays owned; astral inputs divert per operation while the
        // oracle tests astral halves, and the fallback answers unit-exact.
        var regex = new Utf8Regex(pattern, IgnoreCaseOptions);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
    }

    public static IEnumerable<object?[]> BlockFoldParserShapes()
    {
        yield return new object?[] { "[\\p{IsBasicLatin}a]+", 0 };
        yield return new object?[] { "([^\\P{IsBasicLatin}]+)", 1 };
        yield return new object?[] { "(?i:\\p{IsBasicLatin}+)", 0 };
        yield return new object?[] { "[^\\p{IsBasicLatin}]", 0 };
    }

    [Theory]
    [MemberData(nameof(BlockFoldParserShapes))]
    public void BlockFoldShapesParse(string pattern, int expectedGroups)
    {
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        Assert.Equal(expectedGroups, program.GroupCount);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.GetGroupNumbers().Length - 1, program.GroupCount);
    }

    [Fact]
    public void NegatedBlockOverlapIsExplicit()
    {
        // BCL expands the baked complement of \\P{...} under IgnoreCase, so a
        // scalar whose equivalents straddle the block hits both parities: the
        // negated test is not the complement of the positive test.
        var input = Encoding.UTF8.GetBytes("K");
        var positive = new Utf8Regex("\\p{IsBasicLatin}+", IgnoreCaseOptions);
        var negated = new Utf8Regex("\\P{IsBasicLatin}+", IgnoreCaseOptions);
        Assert.True(positive.Inspection.DebugUsesOwnedSliceProgram, "positive");
        Assert.True(negated.Inspection.DebugUsesOwnedSliceProgram, "negated");
        Assert.True(positive.IsMatch(input), "positive");
        Assert.True(negated.IsMatch(input), "negated");
        var oraclePositive = new Regex("\\p{IsBasicLatin}+", IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        var oracleNegated = new Regex("\\P{IsBasicLatin}+", IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        Assert.True(oraclePositive.IsMatch("K"), "oracle positive");
        Assert.True(oracleNegated.IsMatch("K"), "oracle negated");
    }

    [Fact]
    public void ClassNegatedBlockAtomsKeepBothNegations()
    {
        // [^\\p{...}] expands the positive ranges before the class negation
        // while [^\\P{...}] negates the baked complement: both miss 'k' even
        // though the bare \\P{...} hits it. The single-category fold cannot
        // keep those apart in one atom bit, so class-negated block atoms ride
        // the general class path.
        var input = Encoding.UTF8.GetBytes("k");
        var oraclePlain = new Regex("[^\\p{IsBasicLatin}]+", IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        var oracleDouble = new Regex("[^\\P{IsBasicLatin}]+", IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        var oracleBare = new Regex("[\\P{IsBasicLatin}]+", IgnoreCaseOptions, Regex.InfiniteMatchTimeout);
        Assert.False(oraclePlain.IsMatch("k"), "oracle plain");
        Assert.False(oracleDouble.IsMatch("k"), "oracle double");
        Assert.True(oracleBare.IsMatch("k"), "oracle bare");
        var plain = new Utf8Regex("[^\\p{IsBasicLatin}]+", IgnoreCaseOptions);
        var doubled = new Utf8Regex("[^\\P{IsBasicLatin}]+", IgnoreCaseOptions);
        var bare = new Utf8Regex("[\\P{IsBasicLatin}]+", IgnoreCaseOptions);
        Assert.True(plain.Inspection.DebugUsesOwnedSliceProgram, "plain");
        Assert.True(doubled.Inspection.DebugUsesOwnedSliceProgram, "doubled");
        Assert.True(bare.Inspection.DebugUsesOwnedSliceProgram, "bare");
        Assert.False(plain.IsMatch(input), "plain");
        Assert.False(doubled.IsMatch(input), "doubled");
        Assert.True(bare.IsMatch(input), "bare");
        Assert.False(plain.Inspection.DebugFallbackRegexIsCreated, "plain");
        Assert.False(doubled.Inspection.DebugFallbackRegexIsCreated, "doubled");
        Assert.False(bare.Inspection.DebugFallbackRegexIsCreated, "bare");
    }
}
