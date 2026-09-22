using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Detailed single-shots follow the same per-kind limit contract as value
// Match: stepwise backtracking routes breach under tiny budgets (including
// from an explicit start offset), while direct literal routes return the
// first match within budget.
public sealed class Pcre2DetailedLimitTests
{
    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits)
    {
        return new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    [Fact]
    public void BacktrackingDetailedBreachesUnderTinyLimit()
    {
        var input = Encoding.UTF8.GetBytes("aaab aaab");
        var regex = Create("(a+)+b", new Utf8Pcre2ExecutionLimits { MatchLimit = 1 });
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed(input)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.MatchDetailed(input, 0)).ErrorKind);
    }

    [Fact]
    public void LiteralDetailedSucceedsUnderTinyLimit()
    {
        var input = Encoding.UTF8.GetBytes("foo foo foo");
        var regex = Create("foo", new Utf8Pcre2ExecutionLimits { MatchLimit = 1 });
        var actual = regex.MatchDetailed(input);
        Assert.True(actual.Success);
        var expected = Create("foo", default).MatchDetailed(input);
        Assert.Equal(expected.GetValueString(), actual.GetValueString());
    }
}
