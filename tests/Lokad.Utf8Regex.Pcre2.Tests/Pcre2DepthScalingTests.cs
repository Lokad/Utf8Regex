using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Depth scales with the longest greedy run, not with input size or match
// count: any finite limit routes global operations through the backtracking
// fallback (single-purpose cursors require unmetered execution), and the VM
// pushes one frame per greedy-dot iteration. The peak therefore equals the
// longest line body, and multi-line inputs with short bodies stay shallow.
public sealed class Pcre2DepthScalingTests
{
    private static Utf8Pcre2Regex Create(uint depthLimit)
    {
        return new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline, default, new Utf8Pcre2ExecutionLimits { DepthLimit = depthLimit }, Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    [Theory]
    [InlineData("ERROR: x", 1u)]
    [InlineData("ERROR: xy", 2u)]
    [InlineData("ERROR: xyz", 3u)]
    [InlineData("ERROR: xyzw", 4u)]
    public void CountSucceedsWhenDepthLimitEqualsLongestRun(string inputText, uint peakDepth)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = Create(peakDepth);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(1, regex.Count(input));
    }

    [Theory]
    [InlineData("ERROR: xy", 1u)]
    [InlineData("ERROR: xyz", 2u)]
    [InlineData("ERROR: xyzw", 3u)]
    public void CountBreachesOneBelowLongestRun(string inputText, uint depthLimit)
    {
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(depthLimit).Count(Encoding.UTF8.GetBytes(inputText)));
        Assert.Equal(Pcre2ErrorKind.DepthLimit, exception.ErrorKind);
    }

    [Fact]
    public void ShortBodiesStayShallowAcrossLines()
    {
        var input = Encoding.UTF8.GetBytes("ERROR: a\nERROR: b");
        Assert.Equal(2, Create(1).Count(input));
    }
}
