using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 atomic groups for the PCRE2 profile: native PCRE2 supports possessive
// groups with agreeing semantics, so atomic shapes route the shared slice
// runner with BCL-exact behavior, including the greedy-divergent corners.
public sealed class Pcre2SliceAtomicTests
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
    [InlineData("(?>a+)+b")]
    [InlineData("(?>a|ab)+c")]
    [InlineData("(?>(a+))b")]
    public void AtomicShapesRouteThroughSlice(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?>a+)ab", "aaab", false };
        yield return new object?[] { "(?>a|ab)c", "abc", false };
        yield return new object?[] { "(?>a+)b", "aaab", true };
        yield return new object?[] { "(?>(a+))b", "aaab", true };
        yield return new object?[] { "(?>a+)+b", "aaab", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?>a+)b", "aaab aaab", 0, 2)]
    [InlineData("(?>a+)ab", "aaab aaab", 0, 0)]
    public void OwnedCountMatchesOracle(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        Assert.Equal(expected, Bcl(pattern).Matches(inputText).Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedDetailedProjectsAtomicCaptures()
    {
        var regex = new Utf8Pcre2Regex("(?>(a+))b", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("aaab"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("aaa", detailed.GetGroup(1).GetValueString());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedReplaceMatchesOracle()
    {
        var regex = new Utf8Pcre2Regex("(?>a+)b", Pcre2CompileOptions.None);
        Assert.Equal(Bcl("(?>a+)b").Replace("aaab", "<$0>"), regex.ReplaceToString(Utf8("aaab"), "<$0>"));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }
}