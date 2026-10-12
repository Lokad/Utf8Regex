using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R06 single-shot limit contract: stepwise kinds meter every candidate probe,
// so a tiny MatchLimit breaches single-shot IsMatch/Match on multi-candidate
// inputs. Native literal value results also fund their required endpoint
// projection. Utf8-kind core delegation retains its existing admission route.
// Routes verified via
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
    [InlineData("foo", "xxfoo", 3u)]
    [InlineData("(?#comment)ab", "xxab", 1u)]
    public void DirectSingleShotsUseTheirDiscoveryAndProjectionAllowance(string pattern, string inputText, uint valueSteps)
    {
        // A native literal first-hit uses one candidate plus two projected
        // endpoints. Delegated core routes keep their separate admission.
        var input = Encoding.UTF8.GetBytes(inputText);
        var limited = Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 });
        var unlimited = Create(pattern, default);

        Assert.True(limited.IsMatch(input));
        var expected = unlimited.Match(input);
        if (valueSteps > 1)
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => limited.Match(input)).ErrorKind);
        var actual = Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = valueSteps }).Match(input);
        Assert.True(actual.Success);
        Assert.Equal(expected.StartOffsetInUtf16, actual.StartOffsetInUtf16);
    }
    [Theory]
    [InlineData("abc", "xxABCxx", true)]
    [InlineData("^ERROR", "ERROR a", false)]
    public void CharacterSingleShotsMeterEveryProbe(string pattern, string inputText, bool breaches)
    {
        // Pcre2Character single-shots charge per candidate probe: a first-position hit fits one unit, a later hit does not.
        var options = pattern.StartsWith("^") ? Pcre2CompileOptions.Multiline : Pcre2CompileOptions.Caseless;
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = new Utf8Pcre2Regex(pattern, options, default, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 }, Utf8Pcre2Regex.DefaultMatchTimeout);
        if (breaches)
        {
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input)).ErrorKind);
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.Match(input)).ErrorKind);
        }
        else
        {
            Assert.True(regex.IsMatch(input));
            Assert.True(regex.Match(input).Success);
        }
    }

    [Theory]
    [InlineData("cat|horse", "cat horse cat")]
    [InlineData("cat|horse", "cat!")]
    public void FamilySingleShotBreachesEvenTrivialHits(string pattern, string inputText)
    {
        // The literal-family IsMatch charges per branch exploration, so even a first-position hit exceeds one unit.
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 }, Utf8Pcre2Regex.DefaultMatchTimeout);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input)).ErrorKind);
    }
}
