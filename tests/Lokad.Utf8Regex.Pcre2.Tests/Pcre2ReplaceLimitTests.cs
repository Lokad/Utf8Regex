using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Replacement honors the same work budget as every other global operation:
// a breached replacement aborts with a kind-identified exception instead of
// returning a partial prefix from a byte-template operation.
public sealed class Pcre2ReplaceLimitTests
{
    private static Utf8Pcre2Regex Create(string pattern, Utf8Pcre2ExecutionLimits limits)
    {
        return new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    [Theory]
    [InlineData("foo", "foo foo foo", "Z")]
    [InlineData("cat|horse", "cat horse cat", "Z")]
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
    [Fact]
    public void TinyDepthLimitBreachesTemplateReplace()
    {
        var input = Encoding.UTF8.GetBytes("aaabbb aaabbb");
        var exception = Assert.Throws<Pcre2MatchException>(() => Create("(a+)(b+)", new Utf8Pcre2ExecutionLimits { DepthLimit = 1 }).Replace(input, "Z"));
        Assert.Equal(Pcre2ErrorKind.DepthLimit, exception.ErrorKind);
    }

    [Fact]
    public void TinyHeapLimitBreachesTemplateReplace()
    {
        var input = Encoding.UTF8.GetBytes("aaabbb aaabbb");
        var exception = Assert.Throws<Pcre2MatchException>(() => Create("(a+)(b+)", new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 }).Replace(input, "Z"));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, exception.ErrorKind);
    }
}
