using System.Text;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2DepthScalingTests
{
    [Theory]
    [InlineData("ERROR: x", 1)]
    [InlineData("ERROR: xyzw", 1)]
    [InlineData("ERROR: é😀", 1)]
    [InlineData("ERROR: a\nERROR: b", 2)]
    public void FixedScratchLineRunsStayShallow(string inputText, int expected)
    {
        var regex = new Utf8Pcre2Regex("^ERROR: .+$", Pcre2CompileOptions.Multiline, default,
            new() { DepthLimit = 1, HeapLimitInBytes = 1 }, Timeout.InfiniteTimeSpan);
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(regex.IsMatch(input));
        Assert.Equal(expected, regex.Count(input));
    }

    [Fact]
    public void RecursiveGrowthStillRefusesAtTheDepthBoundary()
    {
        const string pattern = @"^(a(?1)?b)$";
        var input = "aaaaabbbbb"u8.ToArray();
        var admitted = new Utf8Pcre2Regex(pattern, default, default,
            new() { DepthLimit = 8192 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(1, admitted.Count(input));
        var refused = new Utf8Pcre2Regex(pattern, default, default,
            new() { DepthLimit = 1 }, Timeout.InfiniteTimeSpan);
        Assert.Equal(Pcre2ErrorKind.DepthLimit,
            Assert.Throws<Pcre2MatchException>(() => refused.Count(input)).ErrorKind);
    }
}
