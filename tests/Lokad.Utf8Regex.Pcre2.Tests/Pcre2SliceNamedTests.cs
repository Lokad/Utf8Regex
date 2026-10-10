using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 named captures for the PCRE2 profile: all-named shapes run on the
// shared slice runner (with literal-prefix IsMatch/Count/Enumerate where
// a specialized program exists); mixed named/unnamed shapes decline
// slice routing everywhere because native PCRE2 numbers captures
// positionally while BCL numbers unnamed groups first. Duplicate names
// fail construction natively; (?P<>) forms stay on native programs
// (the core slice declines Python syntax).
public sealed class Pcre2SliceNamedTests
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
    public void NamedShapesRouteThroughSlice()
    {
        foreach (var pattern in new[] { "(?<word>a)+", "(?<x>a|b)+" })
        {
            var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
            Assert.Equal(
                "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
                regex.DebugDescribeExecutionPlan());
        }

        var prefixed = new Utf8Pcre2Regex("(?<a>x)(?<b>y)+", Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2LiteralPrefixRepeat, Count=Pcre2LiteralPrefixRepeat, Enumerate=Pcre2LiteralPrefixRepeat, Match=Pcre2Slice, Replace=Pcre2Slice",
            prefixed.DebugDescribeExecutionPlan());
    }

    [Theory]
    [InlineData("(?<b>a)(c)+")]
    [InlineData("(?<a>x)(b)(?<c>y)")]
    [InlineData("(?P<x>a)")]
    public void NonSliceNamedShapesDeclineSliceRouting(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
    }

    [Fact]
    public void DuplicateNamesFailConstruction()
    {
        Assert.Throws<Pcre2CompileException>(() => new Utf8Pcre2Regex("(?<x>a)(?<x>b)", Pcre2CompileOptions.None));
    }

    [Fact]
    public void EnabledDuplicateNamesKeepDistinctProfileCaptureSlots()
    {
        var regex = new Utf8Pcre2Regex("(?<x>a)(?<x>b)",
            Pcre2CompileOptions.None, new Utf8Pcre2CompileSettings { AllowDuplicateNames = true },
            default(Utf8Pcre2ExecutionLimits), System.Text.RegularExpressions.Regex.InfiniteMatchTimeout);
        Assert.DoesNotContain("Pcre2Slice", regex.DebugDescribeExecutionPlan());
        var context = regex.MatchDetailed("ab"u8);
        Assert.True(context.Success);
        Assert.Equal("a", context.GetGroup(1).GetValueString());
        Assert.Equal("b", context.GetGroup(2).GetValueString());
        Span<int> numbers = stackalloc int[2];
        Assert.Equal(2, regex.CopyNumbersForName("x", numbers, out var isMore));
        Assert.False(isMore);
        Assert.Equal(new[] { 1, 2 }, numbers.ToArray());
    }

    [Fact]
    public void MixedNumberingFollowsNativePositionalRule()
    {
        // Native PCRE2 numbers left-to-right: group 1 is (b) here, while
        // BCL would number the unnamed (c) first. The slice decline above
        // keeps this shape on native numbering.
        var regex = new Utf8Pcre2Regex("(?<b>a)(c)+", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("acc"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("a", detailed.GetGroup(1).GetValueString());
        Assert.Equal("c", detailed.GetGroup(2).GetValueString());
        Assert.True(detailed.TryGetFirstSetGroup("b", out var bee));
        Assert.Equal("a", bee.GetValueString());
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?<word>a)+", "xaa", true };
        yield return new object?[] { "(?<word>a)+", "xb", false };
        yield return new object?[] { "(?<x>a|b)+", "abba", true };
        yield return new object?[] { "(?<a>x)(?<b>y)+", "xyy", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesNativeSemantics(
        string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var reuse = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, reuse.IsMatch(Utf8(inputText)));
        Assert.True(reuse.DebugCompiledProgram is not null, "pattern=" + pattern);
        var explicitStart = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, explicitStart.IsMatch(Utf8(inputText), 0));
    }

    [Theory]
    [InlineData("(?<word>a)+", "aa aa", 0, 2)]
    [InlineData("(?<x>a|b)+", "abba ab", 0, 2)]
    public void OwnedCountMatchesNativeSemantics(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<word>a)+", "xaa", 0, true, 1, 3)]
    [InlineData("(?<a>x)(?<b>y)+", "xxyy", 0, true, 1, 4)]
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

        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerationAdvancesThroughNamedGroups()
    {
        var regex = new Utf8Pcre2Regex("(?<word>a)+", Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aa aa"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-2", "3-5"], ranges);
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void OwnedDetailedProjectsNamedGroups()
    {
        var regex = new Utf8Pcre2Regex("(?<word>a)+", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("xaa"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("a", detailed.GetGroup(1).GetValueString());
        Assert.True(detailed.TryGetFirstSetGroup("word", out var word));
        Assert.Equal("a", word.GetValueString());
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void OwnedReplaceExpandsNamedGroup()
    {
        var regex = new Utf8Pcre2Regex("(?<word>a)+", Pcre2CompileOptions.None);
        Assert.Equal("x<a>", regex.ReplaceToString(Utf8("xaa"), "<$1>"));
        Assert.True(regex.DebugCompiledProgram is not null);
    }
}
