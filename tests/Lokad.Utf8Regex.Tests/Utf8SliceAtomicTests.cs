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
    [Fact]
    public void OwnedDetailedProjectsAtomicCaptures()
    {
        var regex = new Utf8Regex("(?>(a+))b", options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram);
        var detailed = Utf8MatchSequence.First(regex.CreateMatchCursor(Encoding.UTF8.GetBytes("aaab")));
        Assert.True(detailed.Success);
        Assert.Equal("aaa", System.Text.Encoding.UTF8.GetString(detailed.GetGroupMemory(1).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null);
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
        var regex = new Utf8Regex(pattern, options: RegexOptions.CultureInvariant);
        Assert.True(regex.Inspection.DebugUsesOwnedSliceProgram, "pattern=" + pattern);
        Assert.Equal(expected, System.Text.Encoding.UTF8.GetString(Utf8MatchSequence.First(regex.CreateMatchCursor(input)).GetGroupMemory(0).Span));
        Assert.True(regex.Inspection.PreparedRegex.ScalarProgram is not null, "pattern=" + pattern);
    }
}
