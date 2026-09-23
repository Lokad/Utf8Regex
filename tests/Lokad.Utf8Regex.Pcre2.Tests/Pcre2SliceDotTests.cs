using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 dot slice for the PCRE2 profile: dot shapes run the shared slice
// runner with PCRE2-owned scalar progression (whole-scalar dots, line-feed
// newline rule), so astral inputs stay sliced where the .NET oracle would
// split units. Non-LF newline conventions decline to backtracking.
public sealed class Pcre2SliceDotTests
{
    public static IEnumerable<object?[]> SliceDotShapes()
    {
        yield return new object?[] { "a.*b" };
        yield return new object?[] { "(a.c)+" };
        yield return new object?[] { "a.*?b" };
        yield return new object?[] { "x*|y." };
        yield return new object?[] { "a|.*" };
        yield return new object?[] { "(.+)" };
    }

    // Bare leading-dotstar shapes keep their pre-existing specialized owned
    // programs, which take precedence over the slice overlay. They stay
    // owned (no backtracking, no foundation fallback) with native behavior.
    [Theory]
    [InlineData(".*", "IsMatch=Pcre2SingleTokenRepeat")]
    [InlineData(".*b", "IsMatch=Pcre2LeadingDotStarLiteralIsMatch")]
    public void LeadingDotStarShapesKeepSpecializedOwnedPrograms(string pattern, string expectedPrefix)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.StartsWith(expectedPrefix, regex.DebugDescribeExecutionPlan());
        Assert.DoesNotContain("Pcre2Backtracking", regex.DebugDescribeExecutionPlan());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [MemberData(nameof(SliceDotShapes))]
    public void DotShapesRouteThroughSlice(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, " +
            "Count=Pcre2Slice, " +
            "Enumerate=Pcre2Slice, " +
            "Match=Pcre2Slice, " +
            "Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void DotAllStaysSliced()
    {
        var regex = new Utf8Pcre2Regex("a.*b", Pcre2CompileOptions.DotAll);
        Assert.Contains("IsMatch=Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch(Encoding.UTF8.GetBytes("xxa\nbzz"), 0));
    }

    public static IEnumerable<object?[]> DeclinedNewlines()
    {
        yield return new object?[] { Pcre2NewlineConvention.Cr };
        yield return new object?[] { Pcre2NewlineConvention.Crlf };
        yield return new object?[] { Pcre2NewlineConvention.Any };
        yield return new object?[] { Pcre2NewlineConvention.AnyCrlf };
        yield return new object?[] { Pcre2NewlineConvention.Nul };
    }

    [Theory]
    [MemberData(nameof(DeclinedNewlines))]
    public void NonLfNewlineConventionsDeclineSliceRouting(Pcre2NewlineConvention newline)
    {
        var regex = Utf8Pcre2Regex.Create(
            "a.*b",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { Newline = newline },
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "a.*b", "xxa1bzz", true };
        yield return new object?[] { "a.*b", "xxayy", false };
        yield return new object?[] { "(a.c)+", "xxabcaycyy", true };
        yield return new object?[] { "a.*?b", "xxa1b2byy", true };
        yield return new object?[] { "x*|y.", "ay", true };
        yield return new object?[] { "a|.*", "zz", true };
        yield return new object?[] { "a.*b", "xxa\nbzz", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesNativeSemantics(string pattern, string inputText, bool expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText), 0));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedIsMatchServesAstralDotScalar()
    {
        // The .NET oracle misses: its dots split the astral scalar while the
        // scalar dot matches it whole. PCRE2-native agrees with the scalar hit.
        const string inputText = "xa\U0001F600c";
        Assert.False(
            new System.Text.RegularExpressions.Regex(
                "(a.c)+",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                System.Text.RegularExpressions.Regex.InfiniteMatchTimeout).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex("(a.c)+", Pcre2CompileOptions.None);
        Assert.True(regex.IsMatch(Utf8(inputText), 0));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("(a.c)+", "xa\U0001F600c", 1)]
    [InlineData("a.*b", "a\U0001F600b", 1)]
    [InlineData("..", "\U0001F600", 0)]
    [InlineData("..", "\U0001F600\U0001F600", 1)]
    [InlineData("(a.c)+", "abc ayc", 2)]
    public void OwnedCountMatchesNativeSemantics(string pattern, string inputText, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), 0));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData(".", "\U0001F600", 0, 4)]
    [InlineData("..", "\U0001F600\U0001F600", 0, 8)]
    [InlineData("(a.c)+", "xa\U0001F600c", 1, 7)]
    [InlineData("a.*b", "xxa1bzz", 2, 5)]
    public void OwnedFirstMatchProjectsScalarBytes(
        string pattern, string inputText, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText), 0);
        Assert.True(found.Success);
        Assert.Equal(expectedStart, found.StartOffsetInBytes);
        Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerationAdvancesByScalarsOnAstralInput()
    {
        var regex = new Utf8Pcre2Regex("a.c", Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("xa\U0001F600cxaXc"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["1-7", "8-11"], ranges);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedDetailedProjectsDotGroupOnAstralInput()
    {
        var regex = new Utf8Pcre2Regex("(.+)", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("x\U0001F600y"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("x\U0001F600y", detailed.Value.GetValueString());
        Assert.Equal("x\U0001F600y", detailed.GetGroup(1).GetValueString());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedReplaceExpandsDotGroup()
    {
        var regex = new Utf8Pcre2Regex("(a.*b)", Pcre2CompileOptions.None);
        Assert.Equal("xx<aabb>YY", regex.ReplaceToString(Utf8("xxaabbYY"), "<$1>"));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }
}
