using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 ExplicitCapture on the slice: unnamed groups lower transparently (no
// slot, no number), so numbering, group APIs, and backreference digits follow
// the named groups only. Unbound digits decline to legacy-exact construction
// failures. Bare shapes keep native precedence.
public sealed class Utf8SliceExplicitCaptureTests
{
    public static IEnumerable<object?[]> SliceExplicitShapes()
    {
        yield return new object?[] { "(a)+(?<x>b)+" };
        yield return new object?[] { "(?:a)+(?<x>b)+" };
        yield return new object?[] { "(a)(?<x>b)\\1" };
        yield return new object?[] { "(\\p{L}+)(?<x>y)" };
        yield return new object?[] { "([\\p{L}a]+)(?<x>y)" };
    }

    [Theory]
    [MemberData(nameof(SliceExplicitShapes))]
    public void ExplicitShapesRouteOwned(string pattern)
    {
        foreach (var options in new[]
        {
            RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
            RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, options.ToString());
        }
    }

    public static IEnumerable<object?[]> IsMatchCases()
    {
        yield return new object?[] { "(a)(b)", "ab", true };
        yield return new object?[] { "(a)(b)", "ac", false };
        yield return new object?[] { "(a)(?<x>b)", "ab", true };
        yield return new object?[] { "(a)+(?<x>b)+", "aabb", true };
        yield return new object?[] { "(a)(?<x>b)\\1", "abb", true };
        yield return new object?[] { "(a)(?<x>b)\\1", "aba", false };
        yield return new object?[] { "(\\p{L}+)(?<x>y)", "aby", true };
        yield return new object?[] { "(\\p{L}+)(?<x>y)", "ab", false };
        yield return new object?[] { "([\\p{L}a]+)(?<x>y)", "aBy", true };
        yield return new object?[] { "(\\p{L}+)(x)", "abx", true };
    }

    [Theory]
    [MemberData(nameof(IsMatchCases))]
    public void OwnedIsMatchMatchesOracle(string pattern, string inputText, bool expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.IsMatch(inputText));
        foreach (var options in new[]
        {
            RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant,
            RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant | RegexOptions.Compiled,
        })
        {
            var regex = new Utf8Regex(pattern, options);
            Assert.Equal(expected, regex.IsMatch(input));
            if (regex.Inspection.DebugUsesOwnedSliceProgram)
            {
                Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
            }
        }
    }

    [Fact]
    public void OwnedGroupApisSkipUnnamedGroups()
    {
        var regex = new Utf8Regex("(a)+(?<x>b)+", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var oracle = new Regex("(a)+(?<x>b)+", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.GetGroupNames(), regex.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), regex.GetGroupNumbers());
        Assert.Equal(new[] { "0", "x" }, regex.GetGroupNames());
        Assert.Equal(new[] { 0, 1 }, regex.GetGroupNumbers());
        var detailed = regex.MatchDetailed(Encoding.UTF8.GetBytes("ab"));
        Assert.True(detailed.Success);
        Assert.Equal("b", detailed.GetGroup(1).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Fact]
    public void OwnedCategoryGroupApisSkipUnnamedGroups()
    {
        var regex = new Utf8Regex("(\\p{L}+)(?<x>y)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var oracle = new Regex("(\\p{L}+)(?<x>y)", RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(oracle.GetGroupNames(), regex.GetGroupNames());
        Assert.Equal(oracle.GetGroupNumbers(), regex.GetGroupNumbers());
        Assert.Equal(new[] { "0", "x" }, regex.GetGroupNames());
        Assert.Equal(new[] { 0, 1 }, regex.GetGroupNumbers());
        var detailed = regex.MatchDetailed(Encoding.UTF8.GetBytes("aby"));
        Assert.True(detailed.Success);
        Assert.Equal("y", detailed.GetGroup(1).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    [Theory]
    [InlineData("(a)\\1")]
    [InlineData("(a)(b)\\2")]
    [InlineData("(a)(?<x>b)\\2")]
    public void UnboundDigitsFailConstruction(string pattern)
    {
        Utf8PatternFailureAssert.Throws(
            () => new Utf8Regex(pattern, RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant));
    }
}