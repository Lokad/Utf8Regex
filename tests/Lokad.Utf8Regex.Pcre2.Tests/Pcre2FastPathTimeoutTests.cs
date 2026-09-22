using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Finite timeouts abort slow work but never break fast paths: every
// operation below completes normally on small inputs under a 1ms budget.
public sealed class Pcre2FastPathTimeoutTests
{
    [Fact]
    public void FiniteTimeoutNeverBreaksFastPaths()
    {
        var regex = new Utf8Pcre2Regex("abc", Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), TimeSpan.FromMilliseconds(1));
        var input = Encoding.UTF8.GetBytes("xxabcxx");

        Assert.True(regex.IsMatch(input));
        Assert.True(regex.Match(input).Success);
        Assert.Equal(1, regex.Count(input));
        var destination = new Utf8Pcre2MatchData[8];
        Assert.Equal(1, regex.MatchMany(input, destination, out var isMore));
        Assert.False(isMore);
        var matches = 0;
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            matches++;
        }

        Assert.Equal(1, matches);
        Assert.True(regex.MatchDetailed(input).Success);
    }
}
