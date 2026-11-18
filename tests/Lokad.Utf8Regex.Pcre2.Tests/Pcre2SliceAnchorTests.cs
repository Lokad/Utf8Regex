using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Shared scalar execution serves compatible anchors. PCRE2-specific newline,
// nullable multiline-caret and end-only-dollar behavior stays profile-owned.
public sealed class Pcre2SliceAnchorTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    [Fact]
    public void CompoundAnchorShapesRouteCountAndMatchThroughSlice()
    {
        foreach (var pattern in new[] { "^a.*b$", "^(a.c)+$" })
        {
            var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
            Assert.Equal(
                "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
                regex.DebugDescribeExecutionPlan());
        }

        var alternation = new Utf8Pcre2Regex("^a|b$", options: Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            alternation.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void NonLfNewlineConventionDeclinesAnchorSliceRouting()
    {
        var regex = new Utf8Pcre2Regex(            "^a|b$", options:             Pcre2CompileOptions.None, compileSettings:             new Utf8Pcre2CompileSettings { Newline = Pcre2NewlineConvention.Crlf }, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = default,
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
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
        // Default and explicit-start entry points share the same semantics.
        var reuse = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, reuse.IsMatch(Utf8(inputText)));
        Assert.True(reuse.DebugCompiledProgram is not null, "pattern=" + pattern);
        var explicitStart = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
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
        var regex = new Utf8Pcre2Regex("^a.c$", options: Pcre2CompileOptions.None);
        Assert.True(regex.IsMatch(Utf8(inputText), 0));
        var found = regex.Match(Utf8(inputText), startOffsetInBytes: 0);
        Assert.True(found.Success);
        Assert.Equal(0, found.StartOffsetInBytes);
        Assert.Equal(6, found.EndOffsetInBytes);
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Theory]
    [InlineData("^a|b$", "aXb", 0, 2)]
    [InlineData("^(a.c)+$", "abcayc", 0, 1)]
    [InlineData("^a|b$", "a\nb", 0, 2)]
    public void OwnedCountMatchesNativeSemantics(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^a|b$", "x\na", 0, true, 2, 3)]
    [InlineData("^a.*b$", "a1b", 0, true, 0, 3)]
    public void OwnedFirstMatchProjectsScalarBytes(
        string pattern, string inputText, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.Multiline);
        var found = regex.Match(Utf8(inputText), startOffsetInBytes: startOffsetInBytes);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, found.StartOffsetInBytes);
            Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        }

        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughAnchors()
    {
        var regex = new Utf8Pcre2Regex("^a|b$", options: Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aXb"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-1", "2-3"], ranges);
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void OwnedDetailedProjectsAnchorGroup()
    {
        var regex = new Utf8Pcre2Regex("^(a.c)+$", options: Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("abc"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(detailed.Value.GetValueBytes()));
        Assert.Equal("abc", System.Text.Encoding.UTF8.GetString(detailed.GetGroup(1).GetValueBytes()));
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void OwnedReplaceExpandsAnchorGroup()
    {
        var regex = new Utf8Pcre2Regex("^(a.c)+$", options: Pcre2CompileOptions.None);
        Assert.Equal("<abc>", System.Text.Encoding.UTF8.GetString(regex.Replace(Utf8("abc"), "<$1>")));
        Assert.True(regex.DebugCompiledProgram is not null);
    }
}
