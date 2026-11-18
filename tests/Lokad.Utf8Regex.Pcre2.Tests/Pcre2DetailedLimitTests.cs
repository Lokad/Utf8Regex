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
        return new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit,
                MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
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
            Assert.Throws<Pcre2MatchException>(() => Create("foo", new Utf8Pcre2ExecutionLimits { MatchLimit = 2 }).MatchDetailed(input)).ErrorKind);
        var regex = Create("foo", new Utf8Pcre2ExecutionLimits { MatchLimit = 64 });
        var actual = regex.MatchDetailed(input);
        Assert.True(actual.Success);
        var expected = Create("foo", default).MatchDetailed(input);
        Assert.Equal(System.Text.Encoding.UTF8.GetString(expected.Value.GetValueBytes()), System.Text.Encoding.UTF8.GetString(actual.Value.GetValueBytes()));
    }
}
