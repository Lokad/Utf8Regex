using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Replacement honors the same work budget as every other global operation:
// a breached replacement aborts with a kind-identified exception instead of
// returning a partial prefix, on template and evaluator paths alike.
public sealed class Pcre2ReplaceLimitTests
{
    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits)
    {
        return new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    [Theory]
    [InlineData("foo", "foo foo foo", "Z")]
    [InlineData("(a+)+b", "aaab aaab", "Z")]
    [InlineData("[ab]+", "ab ab ab", "Z")]
    public void TinyLimitBreachesTemplateReplace(string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 }).Replace(input, replacement));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);

        var generous = Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = 1000 }).Replace(input, replacement);
        var unlimited = Create(pattern, default).Replace(input, replacement);
        Assert.Equal(unlimited, generous);
    }

    [Theory]
    [InlineData("foo", "foo foo foo")]
    [InlineData("cat|horse", "cat horse cat")]
    public void TinyLimitBreachesEvaluatorReplace(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 });
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Replace(
            input,
            0,
            (in Utf8Pcre2MatchContext match, ref Utf8ReplacementWriter writer, ref int state) => writer.Append("Z"u8),
            0,
            Pcre2MatchOptions.None));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }
}
