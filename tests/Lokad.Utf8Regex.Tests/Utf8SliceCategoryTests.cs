using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Tests;

// Owned scalar Unicode categories and named BMP blocks. BCL comparisons
// below cover BMP overlap; supplementary subjects use explicit scalar results.
public sealed class Utf8SliceCategoryTests
{
    public static IEnumerable<object?[]> SliceCategoryShapes()
    {
        yield return new object?[] { "\\p{L}+" };
        yield return new object?[] { "\\p{L}}" };
        yield return new object?[] { "\\p{Lu}+" };
        yield return new object?[] { "\\p{Nd}+" };
        yield return new object?[] { "\\P{L}+" };
        yield return new object?[] { "\\p{Ll}+" };
        yield return new object?[] { "\\p{M}+" };
        yield return new object?[] { "\\p{Zs}+" };
        yield return new object?[] { "\\p{Cc}+" };
        yield return new object?[] { "\\p{Cn}+" };
        yield return new object?[] { "\\p{Cs}+" };
        yield return new object?[] { "\\p{Po}+" };
        yield return new object?[] { "(\\p{L}+)" };
        yield return new object?[] { "[\\p{L}]+" };
        yield return new object?[] { "[^\\p{L}]+" };
        yield return new object?[] { "\\p{L}+\\s*\\p{Nd}+" };
    }

    [Theory]
    [MemberData(nameof(SliceCategoryShapes))]
    public void CategoryShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> SliceBlockShapes()
    {
        yield return new object?[] { "\\p{IsGreek}+" };
        yield return new object?[] { "\\p{IsBasicLatin}+" };
        yield return new object?[] { "\\p{IsLatin-1Supplement}+" };
        yield return new object?[] { "\\p{IsSpecials}+" };
        yield return new object?[] { "\\p{_xmlC}+" };
        yield return new object?[] { "[\\p{IsGreek}]+" };
        yield return new object?[] { "(\\p{IsGreek}+)" };
    }

    [Theory]
    [MemberData(nameof(SliceBlockShapes))]
    public void BlockShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> TurkicCategoryShapes()
    {
        // Category predicates use the program's construction-bound scalar folds.
        yield return new object?[] { "\\p{Ll}+", "ABC" };
        yield return new object?[] { "\\p{L}+", "ABC" };
        yield return new object?[] { "[\\p{L}a]+", "ABC" };
        yield return new object?[] { "\\p{IsBasicLatin}+", "abc" };
    }

    [Theory]
    [MemberData(nameof(TurkicCategoryShapes))]
    public void TurkicCategoryShapesUseOwnedScalarExecution(string pattern, string inputText)
    {
        var savedCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var options = RegexOptions.IgnoreCase;
            var input = Encoding.UTF8.GetBytes(inputText);
            var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
            Assert.Equal(oracle.IsMatch(inputText), regex.IsMatch(input));
            Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
            Assert.True(ReferenceEquals(regex.Inspection.PreparedRegex.SemanticRegex.CaptureCatalog, regex.Inspection.PreparedRegex.ScalarProgram!.CaptureCatalog));
        }
        finally
        {
            CultureInfo.CurrentCulture = savedCulture;
        }
    }

    [Theory]
    [InlineData("\\p{L}", "a", true)]
    [InlineData("\\p{L}", "1", false)]
    [InlineData("\\p{IsGreek}", "\u03B1", true)]
    [InlineData("\\p{IsGreek}", "a", false)]
    public void BareCategoryShapesKeepFallbackKind(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal("FallbackRegex", regex.Inspection.ExecutionKind.ToString());
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
    }

    public static IEnumerable<object?[]> ParserDeclines()
    {
        yield return new object?[] { "\\p" };
        yield return new object?[] { "\\pL" };
        yield return new object?[] { "\\p{" };
        yield return new object?[] { "\\p}" };
        yield return new object?[] { "\\p{}" };
        yield return new object?[] { "\\p{L" };
        yield return new object?[] { "\\p{xx}" };
        yield return new object?[] { "\\p{lu}" };
        yield return new object?[] { "\\p{Greek}" };
        yield return new object?[] { "\\p{Is Greek}" };
        yield return new object?[] { "\\p{IsGreek" };
        yield return new object?[] { "[\\p{L}" };

        yield return new object?[] { "\\P" };
    }

    [Theory]
    [MemberData(nameof(ParserDeclines))]
    public void InvalidCategoryShapesDeclineAtParse(string pattern)
    {
        Assert.Null(Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> ConstructionThrows()
    {
        yield return new object?[] { "\\p" };
        yield return new object?[] { "\\pL" };
        yield return new object?[] { "\\p{}" };
        yield return new object?[] { "\\p{xx}" };
        yield return new object?[] { "\\p{lu}" };
        yield return new object?[] { "\\p{Greek}" };
        yield return new object?[] { "\\p{Is Greek}" };
        yield return new object?[] { "[\\p{L}" };

    }

    [Theory]
    [MemberData(nameof(ConstructionThrows))]
    public void InvalidCategoryShapesFailOwnedConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(() => new Regex(pattern, RegexOptions.CultureInvariant));
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("\\p{Is Greek}+")]
    public void SpacedPropertyNamesFailOwnedConstruction(string pattern)
    {
        // The whitespace stripper keeps \p{...} spans verbatim so the raw
        // name still throws; stripping inside the braces would invent an
        // accepted name.
        var options = RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant;
        Assert.Throws<RegexParseException>(() => new Regex(pattern, options));
        Utf8PatternFailureAssert.Throws(() => new Utf8Regex(pattern, options));
    }

    public static IEnumerable<object?[]> ParserShapes()
    {
        yield return new object?[] { "\\p{L}+", 0 };
        yield return new object?[] { "[\\p{L}]", 0 };
        yield return new object?[] { "(\\p{IsGreek}+)", 1 };

        yield return new object?[] { "\\P{C}+", 0 };
    }

    [Theory]
    [MemberData(nameof(ParserShapes))]
    public void CategoryShapesParse(string pattern, int expectedGroups)
    {
        var program = Utf8SliceParser.TryParse(pattern, RegexOptions.CultureInvariant);
        Assert.NotNull(program);
        Assert.Equal(expectedGroups, program.GroupCount);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.GetGroupNumbers().Length - 1, program.GroupCount);
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "\\p{L}+", "abc", true };
        yield return new object?[] { "\\p{L}+", "a1", true };
        yield return new object?[] { "\\p{L}+", "123", false };
        yield return new object?[] { "\\p{Nd}+", "12", true };
        yield return new object?[] { "\\p{Nd}+", "a", false };
        yield return new object?[] { "\\P{L}+", "a1 ", true };
        yield return new object?[] { "\\P{L}+", "abc", false };
        yield return new object?[] { "\\p{Ll}+", "abc", true };
        yield return new object?[] { "\\p{Ll}+", "ABC", false };
        yield return new object?[] { "\\p{Lu}+", "ABC", true };
        yield return new object?[] { "\\p{M}+", "a\u0301b", true };
        yield return new object?[] { "\\p{Zs}+", "a b", true };
        yield return new object?[] { "\\p{Cc}+", "a\0b", true };
        yield return new object?[] { "\\p{Cn}+", "a\u0378b", true };
        yield return new object?[] { "\\p{C}+", "a\u0378b", true };
        yield return new object?[] { "\\p{Po}+", "a,b", true };
        yield return new object?[] { "\\p{Po}+", "ab", false };
        yield return new object?[] { "\\p{IsGreek}+", "\u03B1\u03B2", true };
        yield return new object?[] { "\\p{IsGreek}+", "ab", false };
        yield return new object?[] { "\\p{IsBasicLatin}+", "AB", true };
        yield return new object?[] { "\\p{IsBasicLatin}+", "\u00C9", false };
        yield return new object?[] { "\\p{IsLatin-1Supplement}+", "\u00C9", true };
        yield return new object?[] { "\\p{IsSpecials}+", "\uFFF0", true };
        yield return new object?[] { "[\\p{L}]+", "aα", true };
        yield return new object?[] { "[^\\p{L}]+", "a1", true };
        yield return new object?[] { "[^\\p{L}]+", "ab", false };
        yield return new object?[] { "(\\p{L}+?)x", "aabx", true };
        yield return new object?[] { "(\\p{L}*?)x", "aabx", true };
        yield return new object?[] { "(\\p{IsBasicLatin}+?)1", "ab1", true };
    }

    public static IEnumerable<object?[]> IsMatchIgnoreCaseCases()
    {
        // BCL maps Ll/Lu/Lt onto their cased union under IgnoreCase; every
        // other general category is already case-complete on its own.
        yield return new object?[] { "\\p{Ll}+", "ABC", true };
        yield return new object?[] { "\\p{Lu}+", "abc", true };
        yield return new object?[] { "\\p{Lt}+", "abcABC", true };
        yield return new object?[] { "\\P{Ll}+", "A", false };
        yield return new object?[] { "\\P{Ll}+", "5", true };
        yield return new object?[] { "\\p{L}+", "ABC", true };
        yield return new object?[] { "\\p{Nd}+", "12", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        OwnedIsMatchMatchesOptions(pattern, inputText, expected, RegexOptions.CultureInvariant);
    }

    [Theory]
    [MemberData(nameof(IsMatchIgnoreCaseCases))]
    public void OwnedIgnoreCaseIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        OwnedIsMatchMatchesOptions(pattern, inputText, expected, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private void OwnedIsMatchMatchesOptions(string pattern, string inputText, bool expected, RegexOptions options)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var candidate in new[]
        {
            options,
            options | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, candidate);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, candidate.ToString() + " pattern=" + pattern);
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, candidate.ToString() + " pattern=" + pattern);
        }
    }
    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "\\p{L}+", "a1b22", 2 };
        yield return new object?[] { "\\p{Nd}+", "a12b345", 2 };
        yield return new object?[] { "\\p{IsGreek}+", "a\u03B1\u03B2b", 1 };
        yield return new object?[] { "(\\p{L}+)", "a1b", 2 };
        yield return new object?[] { "\\p{Ll}+", "aAbB", 2 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        foreach (var options in new[]
        {
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString() + " pattern=" + pattern);
            Assert.Equal(expected, regex.Count(input));
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, options.ToString() + " pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> FirstMatchCases()
    {
        yield return new object?[] { "\\p{L}+", "1ab", 1, 2 };
        yield return new object?[] { "\\p{IsGreek}+", "x\u03B1y", 1, 1 };
        yield return new object?[] { "(\\p{Nd}+)", "ab12", 2, 2 };
    }

    [Theory]
    [MemberData(nameof(FirstMatchCases))]
    public void OwnedMatchMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        Assert.Equal(expectedStart, expected.Index);
        Assert.Equal(expectedLength, expected.Length);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.Match(input);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.IndexInUtf16);
        Assert.Equal(expectedLength, found.LengthInUtf16);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> DetailedCases()
    {
        yield return new object?[] { "(\\p{L}+)", "a1", 0, 1, new int[] { 0 }, new int[] { 1 } };
        yield return new object?[] { "(\\p{IsGreek}+)", "x\u03B1", 1, 1, new int[] { 1 }, new int[] { 1 } };
        yield return new object?[] { "(\\p{Ll}+)", "aB", 0, 1, new int[] { 0 }, new int[] { 1 } };
    }

    [Theory]
    [MemberData(nameof(DetailedCases))]
    public void OwnedDetailedMatchesOracle(string pattern, string inputText, int expectedStart, int expectedLength, int[] groupStarts, int[] groupLengths)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText);
        Assert.True(expected.Success);
        Assert.Equal(expected.Groups.Count - 1, groupStarts.Length);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        Assert.Equal(expectedStart, detailed.IndexInUtf16);
        Assert.Equal(expectedLength, detailed.LengthInUtf16);
        Assert.Equal(groupStarts.Length, detailed.GroupCount - 1);
        for (var group = 1; group <= groupStarts.Length; group++)
        {
            var actual = detailed.GetGroup(group);
            var wanted = expected.Groups[group];
            Assert.Equal(wanted.Success, actual.Success);
            Assert.Equal(groupStarts[group - 1], actual.IndexInUtf16);
            Assert.Equal(groupLengths[group - 1], actual.LengthInUtf16);
            Assert.Equal(wanted.Value, actual.GetValueString());
        }

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> EnumerateCases()
    {
        yield return new object?[] { "\\p{Ll}+", "aAbB", new int[] { 0, 1, 2, 1 } };
        yield return new object?[] { "\\p{Nd}+", "a1b22", new int[] { 1, 1, 3, 2 } };
    }

    [Theory]
    [MemberData(nameof(EnumerateCases))]
    public void OwnedEnumerateMatchesOracle(string pattern, string inputText, int[] expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var wanted = new List<string>();
        foreach (Match match in oracle.Matches(inputText))
        {
            wanted.Add(match.Index + ":" + match.Length);
        }

        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
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

    public static IEnumerable<object?[]> ReplaceCases()
    {
        yield return new object?[] { "\\p{L}+", "a1b", "a", "<a>1<b>" };
        yield return new object?[] { "\\p{IsGreek}+", "x\u03B1y", "\u03B1", "x<\u03B1>y" };
    }

    [Theory]
    [MemberData(nameof(ReplaceCases))]
    public void OwnedReplaceMatchesOracle(string pattern, string inputText, string matchText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(matchText, oracle.Match(inputText).Value);
        Assert.Equal(expected, oracle.Replace(inputText, "<$&>"));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Replace(input, "<$&>")));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> SplitCases()
    {
        yield return new object?[] { "\\p{Nd}+", "a1b22c", 0, new string[] { "a", "b", "c" } };
    }

    [Theory]
    [MemberData(nameof(SplitCases))]
    public void OwnedSplitMatchesOracle(string pattern, string inputText, int count, string[] expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Split(inputText, count));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actual = new List<string>();
        foreach (var split in regex.EnumerateSplits(input, count))
        {
            actual.Add(split.GetValueString());
        }

        Assert.Equal(expected, actual.ToArray());
    }

    [Theory]
    [InlineData("\\p{L}+", "xa1b", 1, true, 1, 1)]
    [InlineData("\\p{IsGreek}+", "x\u03B1y", 1, true, 1, 1)]
    public void OwnedFromOffsetMatchesOracle(string pattern, string inputText, int startAt, bool expectedSuccess, int expectedStart, int expectedLength)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var wanted = oracle.Match(inputText, startAt);
        Assert.Equal(expectedSuccess, wanted.Success);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
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

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> RtlCases()
    {
        yield return new object?[] { "\\p{L}+", "ab12", true, 0, 2 };
        yield return new object?[] { "\\p{Nd}+", "ab12", true, 2, 2 };
        yield return new object?[] { "\\p{IsGreek}+", "x\u03B1", true, 1, 1 };
        yield return new object?[] { "\\P{L}+", "ab12", true, 2, 2 };
    }

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void OwnedRightToLeftMatchesOracle(string pattern, string inputText, bool expectedSuccess, int expectedStart, int expectedLength)
    {
        var options = RegexOptions.RightToLeft | RegexOptions.CultureInvariant;
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        var wanted = oracle.Match(inputText);
        Assert.Equal(expectedSuccess, wanted.Success);
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var found = regex.Match(input);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, wanted.Index);
            Assert.Equal(expectedLength, wanted.Length);
            Assert.Equal(expectedStart, found.IndexInUtf16);
            Assert.Equal(expectedLength, found.LengthInUtf16);
        }

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> SurrogateCategoryCases()
    {
        yield return new object?[] { "\\p{IsHighSurrogates}+", "\U00010400" };
        yield return new object?[] { "\\p{Cs}+", "\U00010400" };
    }

    [Theory]
    [MemberData(nameof(SurrogateCategoryCases))]
    public void SurrogateCategoriesNeverMatchValidScalarSubjects(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        foreach (var options in new[] { RegexOptions.CultureInvariant, RegexOptions.CultureInvariant | RegexOptions.Compiled })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
            Assert.False(regex.IsMatch(input));
            Assert.Equal(0, regex.Count(input));
            Assert.False(regex.Match(input).Success);
            Assert.False(regex.MatchDetailed(input).Success);
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
        }
    }

    [Fact]
    public void AllBlockBoundariesMatchOracle()
    {
        // Equivalence gate for the owned block tables: every boundary edge
        // of every named set must agree with the runtime oracle pointwise.
        // Lone surrogates are skipped (unpaired halves encode to U+FFFD, so
        // BCL half semantics and UTF-8 bytes cannot share the probe; astral
        // divert is pinned by the dedicated astral tests).
        Assert.NotEmpty(Utf8SliceCategories.BlockNames);
        foreach (var name in Utf8SliceCategories.BlockNames)
        {
            // Private BCL XML sets changed in .NET 11. Their complete owned
            // BMP memberships are checked against frozen .NET 10 digests below.
            if (name.StartsWith("_xml", StringComparison.Ordinal)) continue;
            Assert.True(Utf8SliceCategories.TryResolve(name, out _, out var ranges, out var isBlock));
            Assert.True(isBlock);
            var probes = new HashSet<int> { 0, 0xFFFF };
            for (var range = 0; range + 1 < ranges.Length; range += 2)
            {
                foreach (var edge in new[] { ranges[range] - 1, ranges[range], ranges[range + 1] - 1, ranges[range + 1] })
                {
                    if (edge >= 0 && edge <= 0xFFFF && (edge < 0xD800 || edge > 0xDFFF))
                    {
                        probes.Add(edge);
                    }
                }
            }

            Assert.Equal(0, ranges.Length & 1); // Every block has complete scalar endpoint pairs.

            var oracle = new Regex("\\p{" + name + "}", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
            var regex = new Utf8Regex("\\p{" + name + "}+", RegexOptions.CultureInvariant);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + name);
            foreach (var probe in probes)
            {
                var text = ((char)probe).ToString();
                var input = Encoding.UTF8.GetBytes(text);
                Assert.True(oracle.IsMatch(text) == regex.IsMatch(input), "pattern=" + name + " probe=" + probe.ToString("X4"));
            }

            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + name);
        }
    }

    [Theory]
    [InlineData("_xmlC", "68BD1B0F5CBD12EBFD0E0C6380395A0D0D4FFD2DC7C28191A793FEA17BA51B9C")]
    [InlineData("_xmlD", "6FFD0190A565731EE3BD3D929D0775F2054F9992C960C9B5CFF83583B19ED5DA")]
    [InlineData("_xmlI", "85071210A90D61C9EBE18F917816BACA237EFC244C08341D5B33C3A1FA16EB32")]
    [InlineData("_xmlW", "3BDD6B253950F6D9ED8EDBDE3DC55558FA94852AE7C81C5B031AEC777B9E598A")]
    public void PrivateXmlClassesKeepFrozenMembership(string name, string expected)
    {
        // Generated from .NET 10.0.12: one byte per BMP code point (0/1),
        // with surrogate entries zero. Preserve this language on both runtimes.
        var regex = new Utf8Regex("\\p{" + name + "}", RegexOptions.CultureInvariant);
        var membership = new byte[65536];
        for (var codePoint = 0; codePoint < membership.Length; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF) continue;
            membership[codePoint] = regex.IsMatch(Encoding.UTF8.GetBytes(((char)codePoint).ToString())) ? (byte)1 : (byte)0;
        }
        Assert.Equal(expected, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(membership)));
    }

    [Fact]
    public void InvariantFibersMatchAuditedSets()
    {
        // Spot guard for the regenerated fiber tables: triples, 16.0-era
        // pairs, dotted/dotless-I empties, the i/I pair, and
        // singleton/unknown empties. Full 65,536-char equivalence against the
        // runtime oracle was verified during the rewrite audit (see PLAN.md);
        // these rows lock the edges.
        var cases = new (int Value, string Expected)[]
        {
            (0x004B, "004B,006B,212A,"),
            (0x212A, "004B,006B,212A,"),
            (0x00C5, "00C5,00E5,212B,"),
            (0x00DF, "00DF,1E9E,"),
            (0x1E9E, "00DF,1E9E,"),
            (0xAB70, "13A0,AB70,"),
            (0xA7DC, "019B,A7DC,"),
            (0x03A3, "03A3,03C3,"),
            (0x03C2, ""),
            (0x0049, "0049,0069,"),
            (0x0069, "0049,0069,"),
            (0x0130, ""),
            (0x0131, ""),
            (0x0041, "0041,0061,"),
            (0x0020, ""),
            (0xD800, ""),
        };
        foreach (var (value, expected) in cases)
        {
            var actual = new StringBuilder();
            foreach (var equivalent in Utf8SliceCaseEquivalences.GetEquivalents((char)value, Utf8ScalarCaseBehavior.Invariant))
            {
                actual.Append(((int)equivalent).ToString("X4"));
                actual.Append(",");
            }

            Assert.Equal(expected, actual.ToString());
        }
    }
}
