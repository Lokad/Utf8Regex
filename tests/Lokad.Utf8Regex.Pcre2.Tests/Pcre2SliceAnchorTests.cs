using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 anchor slice for the PCRE2 profile: anchor-bearing compound shapes run
// Count, first-match, enumeration, and replacement on the shared slice runner
// (IsMatch keeps its specialized AsciiRegular program where one exists);
// simple anchor shapes keep their pre-existing Character programs. Non-LF
// newline conventions decline to backtracking.
public sealed class Pcre2SliceAnchorTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void CompoundAnchorShapesRouteCountAndMatchThroughSlice()
    {
        foreach (var pattern in new[] { "^a.*b$", "^(a.c)+$" })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
            Assert.Equal(
                "IsMatch=Pcre2AsciiRegularIsMatch, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
                regex.DebugDescribeExecutionPlan());
        }

        var alternation = new Utf8Pcre2Regex("^a|b$", Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            alternation.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void NonLfNewlineConventionDeclinesAnchorSliceRouting()
    {
        var regex = Utf8Pcre2Regex.Create(
            "^a|b$",
            Pcre2CompileOptions.None,
            new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Crlf },
            default,
            Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "^a.*b$", "a1b", true };
        yield return new object?[] { "^a.*b$", "xa1b", false };
        yield return new object?[] { "^(a.c)+$", "abcayc", true };
        yield return new object?[] { "^a|b$", "b", true };
        yield return new object?[] { "^a|b$", "xax", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesNativeSemantics(
        string pattern, string inputText, bool expected)
    {
        // Zero-arg IsMatch rides core reuse (slice-serving, fallback-free).
        // The explicit-startpoint driver keeps the pre-existing AsciiRegular
        // route, which builds the foundation fallback; that residual is
        // out of scope for the anchor slice and pinned only behaviorally.
        var reuse = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, reuse.IsMatch(Utf8(inputText)));
        Assert.False(reuse.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
        var explicitStart = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, explicitStart.IsMatch(Utf8(inputText), 0));
    }

    [Fact]
    public void OwnedIsMatchServesAstralAnchorScalar()
    {
        // The .NET oracle misses (unit-split dot); PCRE2-native scalar dots hit.
        const string inputText = "a\U0001F600c";
        Assert.False(
            new System.Text.RegularExpressions.Regex(
                "^a.c$",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant,
                System.Text.RegularExpressions.Regex.InfiniteMatchTimeout).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex("^a.c$", Pcre2CompileOptions.None);
        Assert.True(regex.IsMatch(Utf8(inputText), 0));
        var found = regex.Match(Utf8(inputText), 0);
        Assert.True(found.Success);
        Assert.Equal(0, found.StartOffsetInBytes);
        Assert.Equal(6, found.EndOffsetInBytes);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("^a|b$", "aXb", 0, 2)]
    [InlineData("^(a.c)+$", "abcayc", 0, 1)]
    [InlineData("^a|b$", "a\nb", 0, 2)]
    public void OwnedCountMatchesNativeSemantics(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^a|b$", "x\na", 0, true, 2, 3)]
    [InlineData("^a.*b$", "a1b", 0, true, 0, 3)]
    public void OwnedFirstMatchProjectsScalarBytes(
        string pattern, string inputText, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.Multiline);
        var found = regex.Match(Utf8(inputText), startOffsetInBytes);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, found.StartOffsetInBytes);
            Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        }

        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughAnchors()
    {
        var regex = new Utf8Pcre2Regex("^a|b$", Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aXb"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-1", "2-3"], ranges);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedDetailedProjectsAnchorGroup()
    {
        var regex = new Utf8Pcre2Regex("^(a.c)+$", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("abc"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("abc", detailed.Value.GetValueString());
        Assert.Equal("abc", detailed.GetGroup(1).GetValueString());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedReplaceExpandsAnchorGroup()
    {
        var regex = new Utf8Pcre2Regex("^(a.c)+$", Pcre2CompileOptions.None);
        Assert.Equal("<abc>", regex.ReplaceToString(Utf8("abc"), "<$1>"));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }
}
