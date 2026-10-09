using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 transparent groups: (?:sub) lowers its body with no slot and no group
// number, so numbering, backreferences, and histories behave as if the
// parentheses were absent; (?#comment) runs to the next closing paren and
// contributes nothing. Bare shapes keep native precedence (like named
// captures); quantified and backreference shapes route the owned slice.
// Unterminated forms decline to legacy-exact routing, where construction
// fails exactly like the BCL oracle.
public sealed class Utf8SliceNonCapturingTests
{
    public static IEnumerable<object?[]> SliceTransparentShapes()
    {
        yield return new object?[] { "(?:a)+" };
        yield return new object?[] { "(?:a|b)+c" };
        yield return new object?[] { "(a)(?:b)\\1" };
        yield return new object?[] { "(?:a)+(?:b)+" };
        yield return new object?[] { "((?:ab)+c)" };
    }

    [Theory]
    [MemberData(nameof(SliceTransparentShapes))]
    public void TransparentShapesRouteOwned(string pattern)
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
        yield return new object?[] { "(?:a)+", "aaa", true };
        yield return new object?[] { "(?:a)+", "aab", true };
        yield return new object?[] { "(?:a|b)+c", "abac", true };
        yield return new object?[] { "(?:a|b)+c", "abad", false };
        yield return new object?[] { "(a)(?:b)(c)", "abc", true };
        yield return new object?[] { "(a)(?:b)(c)", "axc", false };
        yield return new object?[] { "(?:(?:a))", "a", true };
        yield return new object?[] { "a(?#middle)b", "ab", true };
        yield return new object?[] { "a(?#middle)b", "acb", false };
        yield return new object?[] { "(?#lead)a", "a", true };
        yield return new object?[] { "a(?#trail)", "a", true };
        yield return new object?[] { "(?:a)(?#c)(?:b)", "ab", true };
        yield return new object?[] { "(a)(?:b)\\1", "aba", true };
        yield return new object?[] { "(a)(?:b)\\1", "abb", false };
        yield return new object?[] { "(?:a)+(?:b)+", "aabb", true };
        yield return new object?[] { "((?:ab)+c)", "ababc", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void TransparentIsMatchMatchesOracle(string pattern, string inputText, bool expected)
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
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    [Fact]
    public void TransparentDetailedSkipsNonCapturingGroups()
    {
        var regex = new Utf8Regex("(a)(?:b)(c)", RegexOptions.CultureInvariant);
        var oracle = new Regex("(a)(?:b)(c)", RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Match("abc");
        var detailed = regex.MatchDetailed(Encoding.UTF8.GetBytes("abc"));
        Assert.True(detailed.Success);
        Assert.Equal(oracle.Groups[1].Value, detailed.GetGroup(1).GetValueString());
        Assert.Equal(oracle.Groups[2].Value, detailed.GetGroup(2).GetValueString());
        Assert.Equal("a", detailed.GetGroup(1).GetValueString());
        Assert.Equal("c", detailed.GetGroup(2).GetValueString());
        // Native-route detailed shaping still rides the BCL fallback by standing
        // architecture (owned shaping covers groupless and whole-literal shapes);
        // group-skip numbering itself is BCL-exact on every route.
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "(?:a)+", "aa aa", 2 };
        yield return new object?[] { "(a)(?:b)(c)", "abc abc", 2 };
        yield return new object?[] { "(?:a|b)+c", "abc ac", 2 };
    }

    [Theory]
    [MemberData(nameof(CountCases))]
    public void TransparentCountMatchesOracle(string pattern, string inputText, int expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Matches(inputText).Count);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(expected, regex.Count(input));
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }

    [Theory]
    [InlineData("(a)(?:b)(c)", "abc", "<$1$2>")]
    [InlineData("(?:a)+", "aa", "<$0>")]
    public void TransparentReplaceMatchesOracle(
        string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Replace(inputText, replacement);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.Equal(oracle, Encoding.UTF8.GetString(regex.Replace(input, replacement)));
        if (regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }

    [Theory]
    [InlineData("(?:a")]
    [InlineData("a(?#unterminated")]
    [InlineData("(?:a|b")]
    public void UnterminatedShapesFailConstruction(string pattern)
    {
        Utf8PatternFailureAssert.Throws(
            () => new Utf8Regex(pattern, RegexOptions.CultureInvariant));
    }

    [Theory]
    [InlineData("(?:a)+", "xaa", 1, 3)]
    [InlineData("(a)(?:b)(c)", "xabc", 1, 4)]
    public void TransparentFromOffsetMatchesOracle(
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

        if (regex.Inspection.DebugUsesOwnedSliceProgram)
        {
            Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
        }
    }
}
