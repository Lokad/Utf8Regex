using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Literal endpoint projection respects the configured capacity controls.
public sealed class Pcre2SingleShotLimitTests
{
    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits)
    {
        return new Utf8Pcre2Regex(pattern, options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
    }

    [Theory]
    [InlineData("foo", "xxfoo")]
    [InlineData("(?#comment)ab", "xxab")]
    public void DepthLimitPreservesSingleShotLiteralRanges(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var unlimited = Create(pattern, default);
        var expected = unlimited.Match(input);
        var actual = Create(pattern, new Utf8Pcre2ExecutionLimits { DepthLimit = 4096 }).Match(input);
        Assert.True(actual.Success);
        Assert.Equal((expected.StartOffsetInBytes, expected.EndOffsetInBytes),
            (actual.StartOffsetInBytes, actual.EndOffsetInBytes));
    }
}
