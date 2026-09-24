using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 inline options for the PCRE2 profile: non-folded quantified shapes run
// every operation on the shared slice runner; any folded literal, class,
// hybrid, or backreference marks its program case-insensitive and declines
// slice routing because native caseless folds UCP-wide while the shared
// runner applies BCL-invariant per-scalar folding. Bare shapes retain their
// native precedence through the unchanged foundation programs.
public sealed class Pcre2SliceInlineTests
{
    private static byte[] Utf8(string value) => Encoding.UTF8.GetBytes(value);

    private static System.Text.RegularExpressions.Regex Bcl(string pattern)
    {
        return new System.Text.RegularExpressions.Regex(
            pattern,
            System.Text.RegularExpressions.RegexOptions.CultureInvariant,
            System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
    }

    [Fact]
    public void NonFoldedInlineShapesRouteThroughSlice()
    {
        foreach (var pattern in new[]
        {
            "(?m:^a)+", "(?s:a.c)+", "(?n:(a)(b))+", "(?m:a$)+", "(?-i:ab)+",
            "(?m)^(a+)+b$", "a(?m:b|c)+d",
        })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
            Assert.Equal(
                "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
                regex.DebugDescribeExecutionPlan());
        }
    }

    [Theory]
    [InlineData("(?i:ab)+")]
    [InlineData("(a)(?i:b)+")]
    [InlineData("(?im:ab)+")]
    [InlineData("(?s:(?i:a.c))+")]
    [InlineData("(?i:(a)(b))+")]
    [InlineData("(?i)a+")]
    public void FoldedInlineShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void InlineFoldedSigmaDivergenceStaysNative()
    {
        // Pinned divergence that forces the decline above: native caseless
        // matches final sigma against capital sigma while BCL-invariant
        // folding (and the owned slice) reports a miss.
        var pattern = "(?i:" + new string((char)0x03A3, 1) + ")+";
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch(Utf8(new string((char)0x03C2, 1))));
        Assert.False(Bcl(pattern).IsMatch(new string((char)0x03C2, 1)));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?m:^a)+", "x\na", true };
        yield return new object?[] { "(?m:^a)+", "xa", false };
        yield return new object?[] { "(?s:a.c)+", "xa\ncy", true };
        yield return new object?[] { "(?s:a.c)+", "xabcy", true };
        yield return new object?[] { "(?n:(a)(b))+", "xabab", true };
        yield return new object?[] { "(?m:a$)+", "a\nb", true };
        yield return new object?[] { "(?m:a$)+", "ab", false };
        yield return new object?[] { "(?-i:ab)+", "abAB", true };
        yield return new object?[] { "(?-i:ab)+", "ABab", true };
        yield return new object?[] { "a(?m:b|c)+d", "abd", true };
        yield return new object?[] { "a(?m:b|c)+d", "aBd", false };
        yield return new object?[] { "(?m)^(a+)+b$", "x\naab", true };
        yield return new object?[] { "(?m)^(a+)+b$", "x\naac", false };
        yield return new object?[] { "(?i:ab)+", "xxABxx", true };
        yield return new object?[] { "(?i)a+", "bbb", false };
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
        yield return new object?[] { "(?m:^a)+", "x\na\na", 2 };
        yield return new object?[] { "(?s:a.c)+", "xa\ncy", 1 };
        yield return new object?[] { "(?n:(a)(b))+", "xabab", 1 };
        yield return new object?[] { "(?-i:ab)+", "abAB", 1 };
        yield return new object?[] { "(?m)^(a+)+b$", "x\naab", 1 };
        yield return new object?[] { "(?i:ab)+", "xxABxx", 1 };
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

    public static IEnumerable<object?[]> FirstMatchCases()
    {
        yield return new object?[] { "(?m:^a)+", "x\na", 2, 3 };
        yield return new object?[] { "(?s:a.c)+", "xa\ncy", 1, 4 };
        yield return new object?[] { "(?n:(a)(b))+", "xabab", 1, 5 };
        yield return new object?[] { "(?-i:ab)+", "ABab", 2, 4 };
        yield return new object?[] { "(?m)^(a+)+b$", "x\naab", 2, 5 };
        yield return new object?[] { "(?i:ab)+", "xxABxx", 2, 4 };
    }

    [Theory]
    [MemberData(nameof(FirstMatchCases))]
    public void InlineFirstMatchProjectsBytes(string pattern, string inputText, int expectedStart, int expectedEnd)
    {
        var expected = Bcl(pattern).Match(inputText);
        Assert.True(expected.Success);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText), 0);
        Assert.True(found.Success);
        Assert.Equal(expected.Index, found.StartOffsetInBytes);
        Assert.Equal(expected.Index + expected.Length, found.EndOffsetInBytes);
        Assert.Equal(expectedStart, found.StartOffsetInBytes);
        Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughInlineMultiline()
    {
        var regex = new Utf8Pcre2Regex("(?m:^a)+", Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("x\na\na"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(enumerator.Current.StartOffsetInBytes + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["2-3", "4-5"], ranges);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedExplicitCaptureOmitsUnnamedGroups()
    {
        const string pattern = "(?n:(a)(b))+";
        var expected = Bcl(pattern).Match("xabab");
        Assert.True(expected.Success);
        Assert.Equal(1, expected.Groups.Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("xabab"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("abab", detailed.Value.GetValueString());
        Assert.Equal(1, detailed.CaptureSlotCount);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedDetailedProjectsInlineMultilineGroup()
    {
        const string pattern = "(?m)^(a+)+b$";
        var expected = Bcl(pattern).Match("x\naab");
        Assert.True(expected.Success);
        Assert.Equal(2, expected.Groups.Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("x\naab"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("aab", detailed.Value.GetValueString());
        Assert.Equal("aa", detailed.GetGroup(1).GetValueString());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> ReplaceCases()
    {
        yield return new object?[] { "(?m:^a)+", "x\na", "x\n<a>" };
        yield return new object?[] { "(?s:a.c)+", "xa\ncy", "x<a\nc>y" };
        yield return new object?[] { "(?n:(a)(b))+", "xabab", "x<abab>" };
        yield return new object?[] { "(?i:ab)+", "xxABxx", "xx<AB>xx" };
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

