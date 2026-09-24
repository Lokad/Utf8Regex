using System.Text;

using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 Unicode properties for the PCRE2 profile: general categories keep
// their native single-token programs (UCP semantics agree with BCL on the
// pinned shapes) while named blocks delegate to the core library with BCL
// parity (native PCRE2 leaves them uncompilable; unknown names keep the
// explicit None/NotSupported vocabulary). The shared slice never serves
// PCRE2 categories: RequiresUnicodeCategory declines every category program
// in the slice overlay.
public sealed class Pcre2SliceCategoryTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static System.Text.RegularExpressions.Regex Bcl(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [Theory]
    [InlineData(@"\p{L}+")]
    [InlineData(@"\p{Lu}+")]
    [InlineData(@"\p{Cs}+")]
    [InlineData(@"\p{Cn}+")]
    [InlineData(@"\P{L}+")]
    [InlineData(@"[\p{L}]+")]
    [InlineData(@"[^\p{L}]+")]
    public void GeneralCategoriesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData(@"\p{IsGreek}+")]
    [InlineData(@"\p{IsBasicLatin}+")]
    [InlineData(@"\p{IsHighSurrogates}+")]
    [InlineData(@"\p{_xmlC}+")]
    public void BlockShapesDelegateToCore(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Utf8Regex, Count=Utf8Regex, Enumerate=Utf8Regex, Match=Utf8Regex, Replace=Utf8Regex",
            regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData(@"\p{Greek}+")]
    [InlineData(@"\p{xx}")]
    public void UnknownNamesKeepNoneProgramVocabulary(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=None, Count=None, Enumerate=None, Match=None, Replace=None",
            regex.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData(@"\p{Greek}+", "ab")]
    [InlineData(@"\p{xx}", "ab")]
    public void UnknownNamesThrowNotSupported(string pattern, string inputText)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Throws<NotSupportedException>(() => regex.IsMatch(Utf8(inputText)));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { @"\p{L}+", "a\u03B1", true };
        yield return new object?[] { @"\p{L}+", "a1", true };
        yield return new object?[] { @"\p{Ll}+", "ABC", false };
        yield return new object?[] { @"\p{Lu}+", "aAB", true };
        yield return new object?[] { @"\p{Cn}+", "a\u0378b", true };
        yield return new object?[] { @"\P{L}+", "a1 ", true };
        yield return new object?[] { @"\P{L}+", "abc", false };
        yield return new object?[] { @"\p{IsGreek}+", "\u03B1x", true };
        yield return new object?[] { @"\p{IsGreek}+", "ax", false };
        yield return new object?[] { @"\p{IsBasicLatin}+", "AB\u00C9", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void InlineIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { @"\p{L}+", "a\u03B1", 1 };
        yield return new object?[] { @"\p{Lu}+", "aAB", 1 };
        yield return new object?[] { @"\P{L}+", "a1 ", 1 };
        yield return new object?[] { @"\p{IsGreek}+", "\u03B11\u03B2", 2 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void InlineCountMatchesOracle(string pattern, string inputText, int expected)
    {
        Assert.Equal(expected, Bcl(pattern).Matches(inputText).Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData(@"\p{L}+", "xa\u03B1", 0, 4)]
    [InlineData(@"\p{Cn}+", "xa\u0378b", 2, 4)]
    [InlineData(@"\p{IsGreek}+", "x\u03B1\u03B2y", 1, 5)]
    public void InlineFirstMatchProjectsBytes(string pattern, string inputText, int expectedStart, int expectedEnd)
    {
        var expected = Bcl(pattern).Match(inputText);
        Assert.True(expected.Success);
        Assert.Equal(expectedStart, expected.Index);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText), 0);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.StartOffsetInBytes);
        Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void DelegatedDetailedProjectsBlockGroup()
    {
        const string pattern = @"(\p{IsGreek}+)";
        var expected = Bcl(pattern).Match("x\u03B1");
        Assert.True(expected.Success);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("x\u03B1"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("\u03B1", detailed.Value.GetValueString());
        Assert.Equal("\u03B1", detailed.GetGroup(1).GetValueString());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ReplaceCases()
    {
        yield return new object?[] { @"\p{L}+", "xa1", "<xa>1" };
        yield return new object?[] { @"\p{IsGreek}+", "x\u03B1y", "x<\u03B1>y" };
    }

    [Theory]
    [MemberData(nameof(ReplaceCases))]
    public void InlineReplaceMatchesOracle(string pattern, string inputText, string expected)
    {
        Assert.Equal(expected, Bcl(pattern).Replace(inputText, "<$0>"));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.ReplaceToString(Utf8(inputText), "<$0>"));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }
}
