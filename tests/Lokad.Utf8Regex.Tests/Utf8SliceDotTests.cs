using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 dot slice: ASCII dot plus Singleline dot lower to owned Dot programs
// with scalar-atomic astral consumption and newline exclusion. Every row runs
// Scalar outcomes with zero fallback-engine construction; BMP cases retain BCL comparison.
public sealed class Utf8SliceDotTests
{
    public static IEnumerable<object?[]> SliceDotShapes()
    {
        yield return new object?[] { "a.*b" };
        yield return new object?[] { ".*b" };
        yield return new object?[] { "(a.c)+" };
        yield return new object?[] { "a.*?b" };
        yield return new object?[] { "x*|y." };
        yield return new object?[] { "a|.*" };
        yield return new object?[] { ".*" };
        yield return new object?[] { "(.+)" };
    }

    [Theory]
    [MemberData(nameof(SliceDotShapes))]
    public void DotShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.Singleline | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "a.*b", "xxa1bzz", true };
        yield return new object?[] { "a.*b", "xxayy", false };
        yield return new object?[] { "a.*b", "xxabyy", true };
        yield return new object?[] { "a.*b", "", false };
        yield return new object?[] { ".*b", "xxb", true };
        yield return new object?[] { "(a.c)+", "xxabcaycyy", true };
        yield return new object?[] { "(a.c)+", "xxayy", false };
        yield return new object?[] { "a.*?b", "xxa1b2byy", true };
        yield return new object?[] { "x*|y.", "ay", true };
        yield return new object?[] { "a|.*", "zz", true };
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
            Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
        }
    }

    [Theory]
    [InlineData("a.*b", "xxa\rbzz", true)]
    [InlineData("a.*b", "xxa\nbzz", false)]
    [InlineData("a.*b", "xxa\r\nbzz", false)]
    [InlineData("a.c", "xayc", true)]
    public void OwnedDotNewlineSemanticsMatchOracle(
        string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.IsMatch(input));
    }

    [Theory]
    [InlineData("a.*b", "xxa\nbzz", true)]
    [InlineData("(a.c)+", "xxa\ncaycyy", true)]
    public void OwnedSinglelineDotMatchesNewline(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var options = RegexOptions.Singleline | RegexOptions.CultureInvariant;
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    // Dot consumes one complete supplementary scalar.
    [Theory]
    [InlineData("(a.c)+", "xa\U0001F600c", true, 1)]
    public void NonAsciiDotUsesOwnedScalars(
        string pattern, string inputText, bool expectedSuccess, int expectedCount)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "construction must stay lazy");
        Assert.Equal(expectedSuccess, regex.IsMatch(input));
        Assert.Equal(expectedCount, regex.Count(input));
        Assert.Equal(expectedSuccess, regex.Match(input).Success);
        Utf8AsciiScalarOperationAssertions.Operations(pattern, RegexOptions.CultureInvariant, inputText, requireSlice: true);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    // Bare dot-star consumes the scalar and then matches empty at the end.
    [Theory]
    [InlineData(".*", "\U0001F600", true, 2)]
    public void NonAsciiDotStarServesOwned(
        string pattern, string inputText, bool expectedSuccess, int expectedCount)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expectedSuccess, oracle.IsMatch(inputText));
        Assert.Equal(expectedCount, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "construction must stay lazy");
        Assert.Equal(expectedSuccess, regex.IsMatch(input));
        Assert.Equal(expectedCount, regex.Count(input));
        Assert.Equal(expectedSuccess, regex.Match(input).Success);
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("a.*b", "x\u00E9ab", true)]
    [InlineData("(a.c)+", "xa\u00E9c", true)]
    public void BmpDotStaysOwned(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "a.*b", "aab aabb", 1 };
        yield return new object?[] { "(a.c)+", "abc ayc", 2 };
        yield return new object?[] { "a.*?b", "a1b a22b", 2 };
        yield return new object?[] { "a.*b", "xyz", 0 };
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
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a.*b)", "xxaa1bbyy", "aa1bb", "aa1bb")]
    [InlineData("(.+)", "xay", "xay", "xay")]
    public void OwnedMatchProjectsCoordinates(
        string pattern, string inputText, string expectedValue, string expectedGroup)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.Equal(oracle.Success, actual.Success);
        Assert.Equal(expectedValue, inputText.Substring(actual.IndexInUtf16, actual.LengthInUtf16));
        Assert.Equal(expectedGroup, oracle.Groups[1].Value);
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        Assert.Equal(expectedGroup, detailed.GetGroup(1).GetValueString());
        foreach (var item in regex.EnumerateMatches(input))
        {
            Assert.Equal(expectedValue, inputText.Substring(item.IndexInUtf16, item.LengthInUtf16));
        }

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("a.*b", "xxaaabbyy", 2)]
    [InlineData("(a.c)+", "xxabcaycyy", 2)]
    public void OwnedFromOffsetMatchesOracle(string pattern, string inputText, int offset)
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
            Assert.Equal(expected.Index, actual.IndexInUtf16);
            Assert.Equal(expected.Length, actual.LengthInUtf16);
        }

        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }
}
