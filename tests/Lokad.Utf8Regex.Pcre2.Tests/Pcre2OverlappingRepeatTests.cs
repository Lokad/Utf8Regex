using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2OverlappingRepeatTests
{
    [Theory]
    [InlineData(@"^\d*1$", "11", true)]
    [InlineData(@"^\d*1$", "2111", true)]
    [InlineData(@"^\d*1$", "11\n", true)]
    [InlineData(@"^\d*1$", "112", false)]
    [InlineData(@"^\d*[12]$", "1212", true)]
    [InlineData(@"^\d*[12]$", "1213", false)]
    [InlineData(@"^\d*\.?\d*1$", "11", true)]
    [InlineData(@"^\d*\.?\d*1$", "1.21", true)]
    [InlineData(@"^\d*\.?\d*1$", "1.211", true)]
    [InlineData(@"^\d*\.?\d*1$", "1.21\n", true)]
    [InlineData(@"^\d*\.?\d*1$", "1.21x", false)]
    [InlineData(@"^\d+\d*[12]$", "1112", true)]
    public void PublicBooleanExecutionRetainsEveryOverlappingPath(string pattern, string text, bool expected)
    {
        var direct = new Utf8Pcre2Regex(pattern);
        var finite = new Utf8Pcre2Regex(pattern, options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 4096
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
        var bytes = Encoding.UTF8.GetBytes(text);
        Assert.Equal(expected, Regex.IsMatch(text, pattern)); // Deliberately overlapping ASCII semantics.
        Assert.Equal(expected, direct.IsMatch(bytes));
        Assert.Equal(expected, finite.IsMatch(bytes));
        Assert.Equal(expected ? 1 : 0, direct.Count(bytes));
        Assert.Equal(expected, direct.Match(bytes).Success);

    }

    [Fact]
    public void CorrectedBooleanRouteKeepsOriginalInterruption()
    {
        var expression = new Utf8Pcre2Regex(@"^\d*1$");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var failure = Assert.ThrowsAny<OperationCanceledException>(() => expression.IsMatch("11"u8, 0, default, cancellation.Token));
        Assert.Equal(cancellation.Token, failure.CancellationToken);
        var limited = new Utf8Pcre2Regex(@"^\d*1$", options: default, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 4096
            },
            MatchTimeout = Timeout.InfiniteTimeSpan
        });
    }
}
