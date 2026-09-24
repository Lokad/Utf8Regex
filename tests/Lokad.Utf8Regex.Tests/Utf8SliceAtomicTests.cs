using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// R03 atomic groups: (?>sub) runs its body with normal in-body backtracking,
// then commits (discarding the body choice points while keeping captures,
// input, and repeat state); failing bodies unwind to the fork and fail
// outward. Atomic groups consume no group numbers. Bare shapes keep native
// precedence; quantified shapes route owned.
public sealed class Utf8SliceAtomicTests
{
    public static IEnumerable<object?[]> SliceAtomicShapes()
    {
        yield return new object?[] { "(?>a+)+" };
        yield return new object?[] { "(?>a|ab)+c" };
        yield return new object?[] { "x(?>y|y)z" };
        yield return new object?[] { "(?>(a+))b" };
    }

    [Theory]
    [MemberData(nameof(SliceAtomicShapes))]
    public void AtomicShapesRouteOwned(string pattern)
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
        yield return new object?[] { "(?>a+)ab", "aaab", false };
        yield return new object?[] { "(?>a|ab)c", "abc", false };
        yield return new object?[] { "(?>a+)b", "aaab", true };
        yield return new object?[] { "(?>(a+))b", "aaab", true };
        yield return new object?[] { "(?>a+)+b", "aaab", true };
        yield return new object?[] { "(?>)a", "a", true };
        yield return new object?[] { "x(?>y|y)z", "xyz", true };
        yield return new object?[] { "(?>a+)ab", "aab", false };
        yield return new object?[] { "(?>ab|a)+z", "abababz", true };
        yield return new object?[] { "(?>a)?b", "ab", true };
        yield return new object?[] { "(?>a)?b", "b", true };
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

    [Fact]
    public void OwnedDetailedProjectsAtomicCaptures()
    {
        var regex = new Utf8Regex("(?>(a+))b", RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var detailed = regex.MatchDetailed(Encoding.UTF8.GetBytes("aaab"));
        Assert.True(detailed.Success);
        Assert.Equal("aaa", detailed.GetGroup(1).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated);
    }

    public static IEnumerable<object?[]> CountCases()
    {
        yield return new object?[] { "(?>a+)b", "aaab aaab", 2 };
        yield return new object?[] { "(?>a+)ab", "aaab aaab", 0 };
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
    [InlineData("((?>a)b|(c))", "c", "c")]
    [InlineData("(?>(a)|(b))", "b", "b")]
    [InlineData("(?>x(?>y)z)", "xyz", "xyz")]
    public void OwnedNestedAtomicMatchesOracle(string pattern, string inputText, string expected)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var oracle = new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout);
        Assert.Equal(expected, oracle.Match(inputText).Value);
        var regex = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, regex.MatchDetailed(input).GetGroup(0).GetValueString());
        Assert.False(regex.Inspection.DebugFallbackRegexIsCreated, "pattern=" + pattern);
    }
}
