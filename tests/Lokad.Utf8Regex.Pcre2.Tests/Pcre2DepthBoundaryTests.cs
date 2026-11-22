using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Count depth tracks the peak pending backtracking frames over the whole call.
// A shorter input can leave fewer pending retries for the same program.
public sealed class Pcre2DepthBoundaryTests
{
    private static Utf8Pcre2Regex Create(string pattern, uint depthLimit)
    {
        return new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = (depthLimit) == 0 ? null : (depthLimit)
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
    }

    [Theory]
    [InlineData("(a)+(b)+", "aaabbb aaabbb", 2, 2)]
    [InlineData("(a+)+b", "aaab", 2, 1)]
    public void CountSucceedsWhenDepthLimitEqualsPeak(string pattern, string inputText, uint peakDepth, int expected)
    {
        Assert.Equal(expected, Create(pattern, peakDepth).Count(Encoding.UTF8.GetBytes(inputText)));
    }

    [Theory]
    [InlineData("(a)+(b)+", "aaabbb aaabbb", 1)]
    [InlineData("(a+)+b", "aaab", 1)]
    public void CountBreachesWhenDepthLimitIsOneBelowPeak(string pattern, string inputText, uint depthLimit)
    {
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(pattern, depthLimit).Count(Encoding.UTF8.GetBytes(inputText)));
        Assert.Equal(Pcre2ErrorKind.DepthLimit, exception.ErrorKind);
    }

    [Fact]
    public void ShortInputsNeedLessDepth()
    {
        Assert.Equal(1, Create("(a)+(b)+", 1).Count("ab"u8));
    }
}
