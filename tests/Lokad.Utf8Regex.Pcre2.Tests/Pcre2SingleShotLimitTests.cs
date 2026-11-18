using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// R06 single-shot limit contract: stepwise kinds meter every candidate probe,
// so a tiny MatchLimit breaches single-shot IsMatch/Match on multi-candidate
// inputs. Native literal value results also fund their required endpoint
// projection, including literal bodies with ignored comments.
// Routes verified via
// DebugDescribeExecutionPlan during authoring.
public sealed class Pcre2SingleShotLimitTests
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
    [InlineData("(?#comment)ab", "xxab", 3u)]
    public void DirectSingleShotsShareAdmissionAndDiscoveryWork(string pattern, string inputText, uint valueSteps)
    {
        // Entry, discovery and final result checkpoints share one allowance. Byte
        // results need no coordinate projection. Comments do not change the work.
        var input = Encoding.UTF8.GetBytes(inputText);
        var limited = Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 });
        var unlimited = Create(pattern, default);

        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => limited.IsMatch(input)).ErrorKind);
        var expected = unlimited.Match(input);
        if (valueSteps > 1)
            Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => limited.Match(input)).ErrorKind);
        var actual = Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = valueSteps }).Match(input);
        Assert.True(actual.Success);
        Assert.Equal((expected.StartOffsetInBytes, expected.EndOffsetInBytes),
            (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
    }
    [Theory]
    [InlineData("abc", "xxABCxx")]
    [InlineData("^ERROR", "ERROR a")]
    public void CharacterSingleShotsKeepUsefulWorkLimits(string pattern, string inputText)
    {
        var options = pattern.StartsWith("^") ? Pcre2CompileOptions.Multiline : Pcre2CompileOptions.Caseless;
        var input = Encoding.UTF8.GetBytes(inputText);
        var limited = new Utf8Pcre2Regex(pattern, options: options, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => limited.IsMatch(input)).ErrorKind);
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => limited.Match(input)).ErrorKind);
        var admitted = new Utf8Pcre2Regex(pattern, options: options, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1000
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        Assert.True(admitted.IsMatch(input));
        Assert.True(admitted.Match(input).Success);
    }

    [Theory]
    [InlineData("cat|horse", "cat horse cat")]
    [InlineData("cat|horse", "cat!")]
    public void FamilySingleShotBreachesEvenTrivialHits(string pattern, string inputText)
    {
        // The literal-family IsMatch charges per branch exploration, so even a first-position hit exceeds one unit.
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = 1
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        Assert.Equal(Pcre2ErrorKind.MatchLimit, Assert.Throws<Pcre2MatchException>(() => regex.IsMatch(input)).ErrorKind);
    }
}
