using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 named captures: (?<name>...) and (?'name'...) bind like numbered
// captures; BCL numbers unnamed groups left-to-right first and named
// groups after, so temporary open-order slots renumber once the full
// parse lands (backreference digits already denote final numbers and
// need no rewrite). Bare shapes keep their native routes; quantified
// shapes route owned. Explicit numbers, digit starts, empty names,
// mismatched closers, duplicates, and balancing subtraction decline to
// legacy-exact routing (invalid spellings fail construction).
public sealed class Utf8SliceNamedTests
{
    public static IEnumerable<object?[]> SliceNamedShapes()
    {
        yield return new object?[] { "(?<word>a)+" };
        yield return new object?[] { "(?<a>x)+" };
        yield return new object?[] { "(?<a>x)(?<b>y)+" };
        yield return new object?[] { "(?<b>a)(c)+" };
        yield return new object?[] { "((?<a>x)+)" };
        yield return new object?[] { "(?<word>\\d+)+" };
        yield return new object?[] { "(?<b>a)(c)\\2" };
        yield return new object?[] { "(?<a>ab|cd)+" };
    }

    [Theory]
    [MemberData(nameof(SliceNamedShapes))]
    public void NamedShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    [Theory]
    [InlineData("(?<word>a)", "a1", 1)]
    [InlineData("(?<a>x)(b)(?<c>y)", "xby", 1)]
    public void BareNamedShapesKeepNativeRoute(string pattern, string inputText, int expected)
    {
        // Simple named shapes stay native; only quantified shapes reach
        // the slice (FallbackRegex construction kind).
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?:a)")]
    [InlineData("(?<name>a)")]
    [InlineData("(?'name'a)")]
    [InlineData("(?>a)")]
    [InlineData("(?<a>b)(?<a>c)")]
    [InlineData("(?<1>a)")]
    public void NonSliceNamedShapesStayOffSlice(string pattern)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.False(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<>x)")]
    [InlineData("(?<-open>b)")]
    [InlineData("(?<a-b>x)")]
    [InlineData("(?P<x>a)")]
    [InlineData("(?<a>x")]
    [InlineData("(?<a>x]")]
    public void InvalidNamedShapesFailConstruction(string pattern)
    {
        Assert.Throws<RegexParseException>(
            () => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(?<word>a)+", "xaa", true };
        yield return new object?[] { "(?<word>a)+", "xb", false };
        yield return new object?[] { "(?<a>x)+", "xx", true };
        yield return new object?[] { "(?<a>x)(?<b>y)+", "xyy", true };
        yield return new object?[] { "(?<b>a)(c)+", "acc", true };
        yield return new object?[] { "((?<a>x)+)", "xx", true };
        yield return new object?[] { "(?<word>\\d+)+", "12", true };
        yield return new object?[] { "(?<b>a)(c)\\2", "aca", true };
        yield return new object?[] { "(?<b>a)(c)\\1", "aca", false };
        yield return new object?[] { "(?<a>ab|cd)+", "abcd", true };
        yield return new object?[] { "(?<a>ab|cd)+", "ax", false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "(?<v>a)+", "aa aa", 2 };
        yield return new object?[] { "(?<a>x)+", "xxx", 1 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<word>a)+", "xaa", "aa", "a")]
    [InlineData("(?<b>a)(c)+", "acc", "acc", "c")]
    [InlineData("((?<a>x)+)", "xx", "xx", "xx")]
    public void OwnedMatchProjectsNamedGroups(
        string pattern, string inputText, string expectedValue, string expectedGroup1)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
        Assert.True(actual.Success);
        Assert.Equal(expectedValue, inputText.Substring(actual.IndexInUtf16, actual.LengthInUtf16));
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        Assert.Equal(oracle.Groups[1].Value, detailed.GetGroup(1).GetValueString());
        Assert.Equal(expectedGroup1, detailed.GetGroup(1).GetValueString());
        foreach (var item in regex.EnumerateMatches(input))
        {
            Assert.Equal(expectedValue, inputText.Substring(item.IndexInUtf16, item.LengthInUtf16));
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedNumberingFollowsUnnamedFirstRule()
    {
        var regex = new Utf8Regex("(?<b>a)(c)+", RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Equal(new[] { "0", "1", "b" }, regex.GetGroupNames());
        Assert.Equal(new[] { 0, 1, 2 }, regex.GetGroupNumbers());
        Assert.Equal(1, regex.GroupNumberFromName("1"));
        Assert.Equal(2, regex.GroupNumberFromName("b"));
        Assert.Equal(-1, regex.GroupNumberFromName("zzz"));
        Assert.Equal("1", regex.GroupNameFromNumber(1));
        Assert.Equal("b", regex.GroupNameFromNumber(2));
        Assert.Equal(string.Empty, regex.GroupNameFromNumber(9));
        var detailed = regex.MatchDetailed(Encoding.UTF8.GetBytes("acc"));
        Assert.True(detailed.Success);
        Assert.Equal("c", detailed.GetGroup(1).GetValueString());
        Assert.Equal("a", detailed.GetGroup(2).GetValueString());
        Assert.Equal("a", detailed.GetGroup("b").GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("(?<word>a)+", "xaa", 1, 3)]
    public void OwnedFromOffsetMatchesOracle(
        string pattern, string inputText, int offset, int expectedEnd)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = oracle.Match(inputText, offset);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected.Success, regex.IsMatchFromUtf16Offset(input, offset));
        var actual = regex.MatchFromUtf16Offset(input, offset);
        Assert.Equal(expected.Success, actual.Success);
        if (expected.Success)
        {
            Assert.Equal(expectedEnd, actual.IndexInUtf16 + actual.LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(?<word>a)+", "xaa", "<$1>")]
    [InlineData("(?<b>a)(c)+", "acc", "<$2$1>")]
    public void OwnedReplaceMatchesOracle(
        string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Replace(inputText, replacement);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, replacement)));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> AstralInputCases()
    {
        var astral = char.ConvertFromUtf32(0x1F600);
        yield return new object?[] { "(?<word>a)+", astral + "a", true };
        yield return new object?[] { "(?<word>a)+", astral + "b", false };
    }

    [Theory]
    [MemberData(nameof(AstralInputCases))]
    public void OwnedAstralInputMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}