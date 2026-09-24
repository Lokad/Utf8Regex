using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 contiguous matches: \G pins to the search start (BCL startat). Each
// global scan re-pins to its own start, stops at the first scan miss, and
// advances past empty matches by one unit (later positions all miss the pin,
// so an empty match ends the chain). The origin travels in the invocation,
// so nested lookbehind runs read the outer origin. Bare shapes keep native
// precedence; quantified shapes route owned.
public sealed class Utf8SliceContiguousTests
{
    public static IEnumerable<object?[]> SliceContiguousShapes()
    {
        yield return new object?[] { "\\Ga+" };
        yield return new object?[] { "\\Ga*" };
        yield return new object?[] { "\\Gx*|y" };
        yield return new object?[] { "\\G(?:a|b)+" };
    }

    [Theory]
    [MemberData(nameof(SliceContiguousShapes))]
    public void ContiguousShapesRouteOwned(string pattern)
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

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "\\Ga+", "aabaa", true };
        yield return new object?[] { "\\Ga+", "baa", false };
        yield return new object?[] { "a\\Gb", "aab", false };
        yield return new object?[] { "\\Ga*", "aaa", true };
        yield return new object?[] { "\\Gx*", "abc", true };
        yield return new object?[] { "\\G(?:a|b)+", "abxab", true };
        yield return new object?[] { "\\Gb", "ab", false };
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
            Assert.Equal(expected, regex.IsMatch(input));
            using var source = new CancellationTokenSource();
            Assert.Equal(expected, regex.IsMatch(input, source.Token));
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
            }
        }
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "\\Ga+", "aabaa", 1 };
        yield return new object?[] { "\\Gx*", "abc", 1 };
        yield return new object?[] { "\\Ga*", "aaa", 2 };
        yield return new object?[] { "\\Ga", "a", 1 };
        yield return new object?[] { "\\G(?:a|b)+", "abxab", 1 };
        yield return new object?[] { "\\Ga+", "baa", 0 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void OwnedCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.Count(input));
        if (regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    public static IEnumerable<object?[]> EnumerateCases()
    {
        yield return new object?[] { "\\Ga+", "aabaa" };
        yield return new object?[] { "\\Ga*", "aaa" };
        yield return new object?[] { "\\Gx*", "abc" };
        yield return new object?[] { "\\G(?:a|b)+", "abxab" };
    }

    [Theory]
    [MemberData(nameof(EnumerateCases))]
    public void OwnedEnumerateMatchesOracle(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        var expected = new List<(int Index, int Length)>();
        foreach (Match m in oracle.Matches(inputText))
        {
            expected.Add((m.Index, m.Length));
        }

        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        var actual = new List<(int Index, int Length)>();
        foreach (var m in regex.EnumerateMatches(input))
        {
            actual.Add((m.IndexInUtf16, m.LengthInUtf16));
        }

        Assert.Equal(expected, actual);
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Fact]
    public void OwnedLookbehindReadsOuterOrigin()
    {
        var regex = new Utf8Regex("(?<=\\Ga)b", RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var oracle = new Regex("(?<=\\Ga)b", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.IsMatch("ab"), regex.IsMatch(Encoding.UTF8.GetBytes("ab")));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("\\Ga+", "aab", 1, 1, 2)]
    [InlineData("\\Ga+", "aab", 0, 0, 2)]
    public void OwnedFromOffsetMatchesOracle(
        string pattern, string inputText, int offset, int expectedIndex, int expectedEnd)
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
            Assert.Equal(expectedIndex, actual.IndexInUtf16);
            Assert.Equal(expectedEnd, actual.IndexInUtf16 + actual.LengthInUtf16);
        }

        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("\\Ga+", "aabaa", "Xbaa")]
    public void OwnedReplaceMatchesOracle(
        string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Replace(inputText, "X");
        Assert.Equal(expected, oracle);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, Encoding.UTF8.GetString(regex.Replace(input, "X")));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}