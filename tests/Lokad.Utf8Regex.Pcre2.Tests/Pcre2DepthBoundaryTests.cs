using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R06/R07 depth-boundary pins: depth tracks the peak backtracking-frame depth
// over the whole call with a strict-greater breach comparison. Greedy loop
// iterations each push a frame, so the peak (and the sufficient limit) grows
// with input length: callers cannot pick a depth budget from the pattern
// alone. Boundaries below were discovered behaviorally and are deterministic
// per fixed pattern and input.
public sealed class Pcre2DepthBoundaryTests
{
    private static Utf8Pcre2Regex Create(string pattern, uint depthLimit)
    {
        return new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, new Utf8Pcre2ExecutionLimits { DepthLimit = depthLimit }, Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    [Theory]
    [InlineData("(a+)(b+)", "aaabbb aaabbb", 5, 2)]
    [InlineData("((a+))b", "aaab aaab", 3, 2)]
    [InlineData("(a+)+b", "aaab", 3, 1)]
    public void CountSucceedsWhenDepthLimitEqualsPeak(string pattern, string inputText, uint peakDepth, int expected)
    {
        Assert.Equal(expected, Create(pattern, peakDepth).Count(Encoding.UTF8.GetBytes(inputText)));
    }

    [Theory]
    [InlineData("(a+)(b+)", "aaabbb aaabbb", 4)]
    [InlineData("((a+))b", "aaab aaab", 2)]
    [InlineData("(a+)+b", "aaab", 2)]
    public void CountBreachesWhenDepthLimitIsOneBelowPeak(string pattern, string inputText, uint depthLimit)
    {
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(pattern, depthLimit).Count(Encoding.UTF8.GetBytes(inputText)));
        Assert.Equal(Pcre2ErrorKind.DepthLimit, exception.ErrorKind);
    }

    [Theory]
    [InlineData("(a+)(b+)", "ab", 1)]
    [InlineData("((a+))b", "ab ab", 2)]
    public void ShortInputsNeedLessDepth(string pattern, string inputText, int expected)
    {
        // Same pattern families as above on minimal inputs at depth limit 1:
        // the sufficient limit is an input-coupled peak, not a pattern constant.
        Assert.Equal(expected, Create(pattern, 1).Count(Encoding.UTF8.GetBytes(inputText)));
    }
}
