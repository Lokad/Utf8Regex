using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Detailed single-shots include bounded validation, discovery and captured
// results in one allowance; sufficient literal budgets still return the first hit.
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
    public void LiteralDetailedFundsValidationDiscoveryAndBothProjectedEndpoints()
    {
        var input = Encoding.UTF8.GetBytes("foo foo foo");
        Assert.Equal(Pcre2ErrorKind.MatchLimit,
            Assert.Throws<Pcre2MatchException>(() => Create("foo", new Utf8Pcre2ExecutionLimits { MatchLimit = 5 }).MatchDetailed(input)).ErrorKind);
        var regex = Create("foo", new Utf8Pcre2ExecutionLimits { MatchLimit = 6 });
        var actual = regex.MatchDetailed(input);
        Assert.True(actual.Success);
        var expected = Create("foo", default).MatchDetailed(input);
        Assert.Equal(expected.GetValueString(), actual.GetValueString());
    }
}
