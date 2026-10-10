using System.Text;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R03 contiguous matches for the PCRE2 profile: native \G agrees with the
// BCL startat-pinned semantics (probed, including stop-on-miss chains and
// trailing empties), so \G shapes route the shared slice runner with
// BCL-exact behavior.
public sealed class Pcre2SliceContiguousTests
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
    [InlineData("\\Ga+")]
    [InlineData("\\Ga*")]
    [InlineData("\\Gx*|y")]
    [InlineData("\\G(a+)+b$")]
    public void ContiguousShapesRouteThroughSlice(string pattern)
    {
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(
            "IsMatch=Pcre2Slice, Count=Pcre2Slice, Enumerate=Pcre2Slice, Match=Pcre2Slice, Replace=Pcre2Slice",
            regex.DebugDescribeExecutionPlan());
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "\\Ga+", "aabaa", true };
        yield return new object?[] { "\\Ga+", "baa", false };
        yield return new object?[] { "a\\Gb", "aab", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        Assert.Equal(expected, Bcl(pattern).IsMatch(inputText));
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.IsMatch(Utf8(inputText)));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\Ga+", "aabaa", 0, 1)]
    [InlineData("\\Gx*", "abc", 0, 1)]
    [InlineData("\\Ga*", "aaa", 0, 2)]
    public void OwnedCountMatchesOracle(
        string pattern, string inputText, int startOffsetInBytes, int expected)
    {
        Assert.Equal(expected, Bcl(pattern).Matches(inputText).Count);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None);
        Assert.Equal(expected, regex.Count(Utf8(inputText), startOffsetInBytes));
        Assert.True(regex.DebugCompiledProgram is not null, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedEnumerationStopsAtFirstMiss()
    {
        var regex = new Utf8Pcre2Regex("\\Ga+", Pcre2CompileOptions.None);
        var enumerator = regex.EnumerateMatches(Utf8("aabaa"), 0);
        var ranges = new List<string>();
        while (enumerator.MoveNext())
        {
            ranges.Add(
                enumerator.Current.StartOffsetInBytes
                + "-" + enumerator.Current.EndOffsetInBytes);
        }

        Assert.Equal(["0-2"], ranges);
        Assert.True(regex.DebugCompiledProgram is not null);
    }

    [Fact]
    public void OwnedDetailedPinsToSearchStart()
    {
        var regex = new Utf8Pcre2Regex("\\Ga+", Pcre2CompileOptions.None);
        var detailed = regex.MatchDetailed(Utf8("aab"), 1);
        Assert.True(detailed.Success);
        Assert.Equal("a", detailed.GetGroup(0).GetValueString());
        Assert.True(regex.DebugCompiledProgram is not null);
    }
}