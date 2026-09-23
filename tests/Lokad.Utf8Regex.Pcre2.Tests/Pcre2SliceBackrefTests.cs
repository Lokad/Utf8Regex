using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 backreference slice for the PCRE2 profile: single-digit backward
// references over slice-owned shapes run first-match, detailed projection,
// and replacement on the shared slice runner (single-group shapes keep the
// specialized literal-family IsMatch/Count/Enumerate; the repeated-capture
// shape rides the slice for every operation). Ordinal byte comparison
// agrees with native PCRE2 on case-sensitive ASCII shapes (unset groups
// fail in both, the last iteration wins, empty-but-set groups match
// empty), so expectations below are BCL-computed ground truths on shapes
// where BCL and native PCRE2 agree. Multi-digit, forward, named, and \g
// forms decline to the generic backtracking VM, as do backreferences over
// non-ASCII literals (the core slice accepts ASCII atoms only).
public sealed class Pcre2SliceBackrefTests
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
    public void BackrefShapesRouteThroughSlice()
    {
        // Single-group backrefs keep the specialized literal-family
        // IsMatch/Count/Enumerate (substring search plus verification);
        // first-match and replacement ride the shared slice runner. The
        // repeated-capture shape has no literal-family program, so every
        // operation rides the slice.
        foreach (var pattern in new[] { "(a)\\1", "(a|ab)\\1" })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
            Assert.Equal(
                "IsMatch=Pcre2LiteralFamily, Count=Pcre2LiteralFamily, Enumerate=Pcre2LiteralFamily, Match=Pcre2Slice, Replace=Pcre2Slice",
                regex.DebugDescribeExecutionPlan());
        }

        var repeated = new Utf8Pcre2Regex("(ab|a)+\\1z", Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            repeated.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData("(a)\\11")]
    [InlineData("(\\2a|(b))+")]
    [InlineData("(?<x>a)\\k<x>")]
    [InlineData("(a)\\g{1}")]
    public void NonSliceBackrefShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void NonAsciiLiteralBackrefDeclinesSliceRouting()
    {
        var pattern = "(" + ((char)0xE9) + ")\\1";
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        Assert.True(regex.IsMatch(Utf8(new string((char)0xE9, 2))));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(a)\\1", "aa", true };
        yield return new object?[] { "(a)\\1", "ab", false };
        yield return new object?[] { "(a|b)+\\1", "abb", true };
        yield return new object?[] { "(a|b)+\\1", "aba", false };
        yield return new object?[] { "(a*)\\1", "aaa", true };
        yield return new object?[] { "(x)?b\\1", "b", false };
        yield return new object?[] { "(x)?b\\1", "bxb", false };
        yield return new object?[] { "(a)(b)\\2\\1", "abba", true };
        yield return new object?[] { "(ab|a)+\\1z", "ababz", true };
        yield return new object?[] { "(ab|a)+\\1z", "ababaz", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesNativeSemantics(
        string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var reuse = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, reuse.IsMatch(Utf8(inputText)));
        Assert.False(reuse.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
        var explicitStart = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, explicitStart.IsMatch(Utf8(inputText), 0));
    }

    [Theory]
    [InlineData("(a)\\1", "aa aa", 0, 2)]
    [InlineData("(a|b)+\\1", "abb abb", 0, 2)]
    [InlineData("(x)?b\\1", "bbb", 0, 0)]
    [InlineData("(a)\\1", "aa aa", 3, 1)]
    [InlineData("(ab|a)+\\1z", "ababz ababz", 0, 2)]
    public void OwnedCountMatchesNativeSemantics(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        if (startOffsetInBytes == 0)
        {
            Assert.Equal(expected, Bcl(pattern).Matches(inputText).Count);
        }

        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a|ab)\\1", "xabab", 0, true, 1, 5)]
    [InlineData("(a)(b)\\2\\1", "xxabbayy", 0, true, 2, 6)]
    [InlineData("(a)\\1", "ab", 0, false, 0, 0)]
    public void OwnedFirstMatchProjectsBytes(
        string pattern, string inputText, int startOffsetInBytes, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var expected = Bcl(pattern).Match(inputText);
        Assert.Equal(expectedSuccess, expected.Success);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        var found = regex.Match(Utf8(inputText), startOffsetInBytes);
        Assert.Equal(expectedSuccess, found.Success);
        if (expectedSuccess)
        {
            Assert.Equal(expected.Index, found.StartOffsetInBytes);
            Assert.Equal(expected.Index + expected.Length, found.EndOffsetInBytes);
            Assert.Equal(expectedStart, found.StartOffsetInBytes);
            Assert.Equal(expectedEnd, found.EndOffsetInBytes);
        }

        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughBackrefs()
    {
        var regex = new Utf8Pcre2Regex("(a)\\1", Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aa aa"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-2", "3-5"], ranges);
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedDetailedProjectsBackrefGroups()
    {
        var regex = new Utf8Pcre2Regex("(a)(b)\\2\\1", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("xxabbayy"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("abba", detailed.Value.GetValueString());
        Assert.Equal("a", detailed.GetGroup(1).GetValueString());
        Assert.Equal("b", detailed.GetGroup(2).GetValueString());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedReplaceExpandsBackrefMatch()
    {
        var regex = new Utf8Pcre2Regex("(a)\\1", Pcre2CompileOptions.None);
        Assert.Equal("<aa> ab", regex.ReplaceToString(Utf8("aa ab"), "<$0>"));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }
}
