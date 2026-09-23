using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 anchor slice: `^`/`$` lower to owned zero-width Anchor programs with
// Multiline-scoped line edges (line-feed only, matching the oracle). Every
// row runs oracle parity with zero fallback-engine construction. Anchors are
// encoding-indifferent, so astral inputs stay owned unless a dot rides along.
public sealed class Utf8SliceAnchorTests
{
    public static IEnumerable<object?[]> SliceAnchorShapes()
    {
        yield return new object?[] { "^a.*b$" };
        yield return new object?[] { "^(a.c)+$" };
        yield return new object?[] { "^a|b$" };
        yield return new object?[] { "^$" };
        yield return new object?[] { "^" };
        yield return new object?[] { "$" };
        yield return new object?[] { "a^b" };
        yield return new object?[] { "^*" };
    }

    [Theory]
    [MemberData(nameof(SliceAnchorShapes))]
    public void AnchorShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.None,
            RegexOptions.CultureInvariant,
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            RegexOptions.Multiline | RegexOptions.CultureInvariant,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "^a.*b$", "a1b", false, true };
        yield return new object?[] { "^a.*b$", "abb", false, true };
        yield return new object?[] { "^a.*b$", "xa1b", false, false };
        yield return new object?[] { "^a.*b$", "a1bx", false, false };
        yield return new object?[] { "^(a.c)+$", "abcayc", false, true };
        yield return new object?[] { "^a|b$", "a", false, true };
        yield return new object?[] { "^a|b$", "b", false, true };
        yield return new object?[] { "^a|b$", "xax", false, false };
        yield return new object?[] { "^$", "", false, true };
        yield return new object?[] { "^$", "a", false, false };
        yield return new object?[] { "a^b", "a\nb", true, false };
        yield return new object?[] { "a^b", "a\nb", false, false };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(
        string pattern, string inputText, bool multiline, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var options = RegexOptions.CultureInvariant
            | (multiline ? RegexOptions.Multiline : RegexOptions.None);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var candidate in new[]
        {
            options,
            options | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, candidate);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, candidate.ToString());
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    // Simple literal-plus-anchor shapes stay on the pre-existing
    // AsciiSimple route (already owned, pinned by the route matrix). These
    // rows lock parity without slice-routing pins.
    public static IEnumerable<object?[]> SimpleAnchorCases()
    {
        yield return new object?[] { "a$", "xxa", false, true, 1 };
        yield return new object?[] { "a$", "xxb", false, false, 0 };
        yield return new object?[] { "^a", "axx", false, true, 1 };
        yield return new object?[] { "^a", "xax", false, false, 0 };
        yield return new object?[] { "a$", "a\r\nb", false, false, 0 };
        yield return new object?[] { "a$", "a\r\nb", true, false, 0 };
        yield return new object?[] { "^a", "ab a", false, true, 1 };
        yield return new object?[] { "a$", "a a", false, true, 1 };
        yield return new object?[] { "^b", "a\rb", true, false, 0 };
        yield return new object?[] { "^b", "a\r\nb", true, true, 1 };
    }

    [Theory]
    [MemberData(nameof(SimpleAnchorCases))]
    public void SimpleAnchorParityMatchesOracle(
        string pattern, string inputText, bool multiline, bool expectedSuccess, int expectedCount)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var options = RegexOptions.CultureInvariant
            | (multiline ? RegexOptions.Multiline : RegexOptions.None);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expectedSuccess, oracle.IsMatch(inputText));
        Assert.Equal(expectedCount, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, options);
        Assert.Equal(expectedSuccess, regex.IsMatch(input));
        Assert.Equal(expectedCount, regex.Count(input));
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "^", "abc\ndef", false, 1 };
        yield return new object?[] { "^", "abc\ndef", true, 2 };
        yield return new object?[] { "$", "abc\n", false, 2 };
        yield return new object?[] { "$", "abc", false, 1 };
        yield return new object?[] { "^$", "a\n\nb", true, 1 };
        yield return new object?[] { "^*", "abc", false, 4 };
        yield return new object?[] { "^(a.c)+$", "abcayc", false, 1 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedCountMatchesOracle(
        string pattern, string inputText, bool multiline, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var options = RegexOptions.CultureInvariant
            | (multiline ? RegexOptions.Multiline : RegexOptions.None);
        var oracle = new Regex(pattern, options, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, options);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^a.*b$", "a1b", 0, 3)]
    [InlineData("^(a.c)+$", "abcayc", 0, 6)]
    public void OwnedMatchProjectsCoordinates(
        string pattern, string inputText, int expectedStart, int expectedEnd)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match(inputText);
        Assert.True(oracle.Success);
        Assert.Equal(expectedStart, oracle.Index);
        Assert.Equal(expectedEnd, oracle.Index + oracle.Length);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        var actual = regex.Match(input);
        Assert.True(actual.Success);
        Assert.Equal(expectedStart, actual.IndexInUtf16);
        Assert.Equal(expectedEnd, actual.IndexInUtf16 + actual.LengthInUtf16);
        var detailed = regex.MatchDetailed(input);
        Assert.True(detailed.Success);
        foreach (var item in regex.EnumerateMatches(input))
        {
            Assert.Equal(expectedStart, item.IndexInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("a.*b$", "xxa1b", 2, true)]
    [InlineData("^(a.c)+$", "xabc", 1, false)]
    public void OwnedFromOffsetMatchesOracle(
        string pattern, string inputText, int offset, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText, offset).Success);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.IsMatchFromUtf16Offset(input, offset));
        Assert.Equal(expected, regex.MatchFromUtf16Offset(input, offset).Success);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^ab", "xxab", 2, false)]
    [InlineData("ab$", "xxab", 2, true)]
    public void SimpleAnchorFromOffsetMatchesOracle(
        string pattern, string inputText, int offset, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText, offset).Success);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.IsMatchFromUtf16Offset(input, offset));
        Assert.Equal(expected, regex.MatchFromUtf16Offset(input, offset).Success);
    }

    [Theory]
    [InlineData("^a.*b$", "a\U0001F600b", true)]
    [InlineData("^(a.c)+$", "xa\U0001F600c", false)]
    public void AstralDotAnchorDeclinesToFallback(
        string pattern, string inputText, bool expectedSuccess)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expectedSuccess, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "construction must stay lazy");
        Assert.Equal(expectedSuccess, regex.IsMatch(input));
        Assert.True(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("^(a|b)+$", "aba", true)]
    [InlineData("^(a|b)+$", "a\U0001F600", false)]
    [InlineData("^$", "\U0001F600", false)]
    public void AstralAnchorWithoutDotStaysOwned(
        string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.IsMatch(input));
        Assert.Equal(oracle.Matches(inputText).Count, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(^a.*b$)", "xxaa1bbyy", "<$1>")]
    [InlineData("(^a.*b$)", "aa1bb", "<$1>")]
    [InlineData("^a.*$", "aXa", "Z")]
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
}
