using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 transparent groups for the PCRE2 profile: (?:sub) and (?#comment) are
// native PCRE2 syntax, so shapes route by the existing precedence (slice
// where the core slice claims them, specialized or VM elsewhere) with
// identical behavior on every route.
public sealed class Pcre2SliceTransparentTests
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
    [InlineData("(?:^a|b)c")]
    [InlineData("(?:^a)?b")]
    public void AnchoredTransparentShapesRouteThroughSlice(string pattern)
    {
        // Formerly backtracking-VM shapes: the core slice owns anchors plus
        // transparent groups, so the overlay claims every operation.
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        // expectOwned marks shapes the core slice claims: anything else may
        // ride a native program or the core BCL fallback with identical behavior.
        yield return new object?[] { "(?:a)+", "xaa", true, true };
        yield return new object?[] { "(?:a|b)+c", "abac", true, true };
        yield return new object?[] { "(a)(?:b)(c)", "abc", true, false };
        yield return new object?[] { "(a)(?:b)(c)", "axc", false, false };
        yield return new object?[] { "a(?#middle)b", "ab", true, false };
        yield return new object?[] { "(?:a)(?#c)(?:b)", "ab", true, false };
        yield return new object?[] { "(?:^a|b)c", "abc", true, true };
        yield return new object?[] { "(?:^a|b)c", "ac", true, true };
        yield return new object?[] { "(?:^a)?b", "ab", true, true };
        yield return new object?[] { "(?:^a)?b", "b", true, true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void TransparentIsMatchMatchesOracle(string pattern, string inputText, bool expected, bool expectOwned)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        if (expectOwned)
        {
            Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    [Theory]
    [InlineData("(?:a)+", "aa aa", 0, 2)]
    [InlineData("(a)(?:b)(c)", "abc abc", 0, 2)]
    public void TransparentCountMatchesOracle(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        Assert.Equal(expected, Bcl(pattern).Matches(inputText).Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void TransparentDetailedSkipsNonCapturingGroups()
    {
        var regex = new Utf8Pcre2Regex("(a)(?:b)(c)", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("abc"), 0);
        Assert.True(detailed.Success);
        Assert.Equal("a", detailed.GetGroup(1).GetValueString());
        Assert.Equal("c", detailed.GetGroup(2).GetValueString());
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }

    [Fact]
    public void TransparentReplaceMatchesOracle()
    {
        var regex = new Utf8Pcre2Regex("(a)(?:b)(c)", Pcre2CompileOptions.None);
        Assert.Equal(Bcl("(a)(?:b)(c)").Replace("abc", "<$1$2>"), regex.ReplaceToString(Utf8("abc"), "<$1$2>"));
        Assert.False(regex.DebugFoundationCoreFallbackRegexIsCreated);
    }
}