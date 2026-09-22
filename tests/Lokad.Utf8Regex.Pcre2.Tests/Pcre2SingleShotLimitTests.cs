using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R06 single-shot limit contract: stepwise kinds meter every candidate probe,
// so a tiny MatchLimit breaches single-shot IsMatch/Match on multi-candidate
// inputs. Direct single-call kinds (literal scan, Utf8-kind core delegation)
// perform one bounded call governed by MatchTimeout instead: a first match
// succeeds under any finite limit. Routes verified via
// DebugDescribeExecutionPlan during authoring.
public sealed class Pcre2SingleShotLimitTests
{
    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits)
    {
        return new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    [Theory]
    [InlineData("(a+)+b", "aaab aaab")]
    [InlineData("[ab]+", "ab ab ab")]
    public void StepwiseSingleShotsBreachUnderTinyLimit(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 });

        var isMatchBreach = Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, isMatchBreach.ErrorKind);
        var matchBreach = Assert.Throws<Pcre2MatchException>(() => regex.Match(input));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, matchBreach.ErrorKind);
    }

    [Theory]
    [InlineData("foo", "xxfoo")]
    [InlineData("(?#comment)ab", "xxab")]
    public void DirectSingleShotsReturnFirstMatchUnderTinyLimit(string pattern, string inputText)
    {
        // Documents the R06 residual precisely: these routes are timeout-
        // governed single calls, not stepwise-metered scans. A first match at
        // a nonzero offset succeeds where a stepwise route would breach.
        var input = Encoding.UTF8.GetBytes(inputText);
        var limited = Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 });
        var unlimited = Create(pattern, default);

        Assert.True(limited.IsMatch(input));
        var expected = unlimited.Match(input);
        var actual = limited.Match(input);
        Assert.True(actual.Success);
        Assert.Equal(expected.StartOffsetInUtf16, actual.StartOffsetInUtf16);
    }
}
