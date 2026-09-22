using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Depth/heap enforcement on IsMatch-only specializations: every stepwise
// specialization meters both budgets, except the frameless bounded scan,
// which succeeds under DepthLimit=1 while still breaching HeapLimit=1.
// Unlimited sanity asserts prove each row is a hit, so breaches reflect
// metering rather than misses.
public sealed class Pcre2IsMatchSpecializationLimitTests
{
    private static Utf8Pcre2Regex Create(string pattern, Pcre2CompileOptions options, Utf8Pcre2ExecutionLimits limits)
    {
        return new Utf8Pcre2Regex(pattern, options, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$", Pcre2CompileOptions.None, "abccba")]
    [InlineData(".*(ss)", Pcre2CompileOptions.None, "xxss")]
    [InlineData("^[-+]?\\d*\\.?\\d*$", Pcre2CompileOptions.None, "12.5")]
    [InlineData("^([0-9]+)(\\-| |$)(.*)$", Pcre2CompileOptions.None, "200 ok")]
    [InlineData("^ERROR: .+$", Pcre2CompileOptions.Multiline, "ERROR: bad")]
    public void StepwiseSpecializationsBreachDepth(string pattern, Pcre2CompileOptions options, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(Create(pattern, options, default).IsMatch(input));
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(pattern, options, new Utf8Pcre2ExecutionLimits { DepthLimit = 1 }).IsMatch(input));
        Assert.Equal(Pcre2ErrorKind.DepthLimit, exception.ErrorKind);
    }

    [Fact]
    public void FramelessBoundedScanSucceedsUnderDepthLimit()
    {
        const string pattern = "(\\d{4}[- ]){3}\\d{3,4}";
        var input = Encoding.UTF8.GetBytes("4111-1111-1111-1111");
        Assert.True(Create(pattern, Pcre2CompileOptions.None, default).IsMatch(input));
        Assert.True(Create(pattern, Pcre2CompileOptions.None, new Utf8Pcre2ExecutionLimits { DepthLimit = 1 }).IsMatch(input));
    }

    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$", Pcre2CompileOptions.None, "abccba")]
    [InlineData(".*(ss)", Pcre2CompileOptions.None, "xxss")]
    [InlineData("(\\d{4}[- ]){3}\\d{3,4}", Pcre2CompileOptions.None, "4111-1111-1111-1111")]
    [InlineData("^[-+]?\\d*\\.?\\d*$", Pcre2CompileOptions.None, "12.5")]
    [InlineData("^([0-9]+)(\\-| |$)(.*)$", Pcre2CompileOptions.None, "200 ok")]
    [InlineData("^ERROR: .+$", Pcre2CompileOptions.Multiline, "ERROR: bad")]
    public void EverySpecializationBreachesHeap(string pattern, Pcre2CompileOptions options, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(Create(pattern, options, default).IsMatch(input));
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(pattern, options, new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 }).IsMatch(input));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, exception.ErrorKind);
    }
}
