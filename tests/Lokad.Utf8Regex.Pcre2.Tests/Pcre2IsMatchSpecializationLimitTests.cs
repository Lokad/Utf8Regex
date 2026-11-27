using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Exercise real growing workspace and backtracking depth. Fixed-scratch line
// runs are covered separately; numerical needs follow the selected program.
public sealed class Pcre2IsMatchSpecializationLimitTests
{
    private static Utf8Pcre2Regex Create(string pattern, Pcre2CompileOptions options, Utf8Pcre2ExecutionLimits limits)
    {
        return new Utf8Pcre2Regex(pattern, options: options, compileSettings: default, policy: new Utf8RegexPolicy
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
    [InlineData("^((.)(?1)\\2|.?)$", Pcre2CompileOptions.None, "abccba")]
    [InlineData("^[+-]?[+-]?[+-]?\\d*$", Pcre2CompileOptions.None, "++12")]
    public void GrowingSearchBreachesDepth(string pattern, Pcre2CompileOptions options, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(Create(pattern, options, default).IsMatch(input));
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(pattern, options, new Utf8Pcre2ExecutionLimits { DepthLimit = 1 }).IsMatch(input));
        Assert.Equal(Pcre2ErrorKind.DepthLimit, exception.ErrorKind);
    }

    [Theory]
    [InlineData("(\\d{4}[- ]){3}\\d{3,4}", "4111-1111-1111-1111")]
    [InlineData(".*(ss)", "xxss")]
    [InlineData("^[-+]?\\d*\\.?\\d*$", "12.5")]
    [InlineData("^[-+]?\\d*\\.?\\d*$", "-12.5")]
    [InlineData("^([0-9]+)(\\-| |$)(.*)$", "200 ok")]
    public void BoundedSearchSucceedsUnderDepthLimit(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(Create(pattern, Pcre2CompileOptions.None, default).IsMatch(input));
        Assert.True(Create(pattern, Pcre2CompileOptions.None, new Utf8Pcre2ExecutionLimits { DepthLimit = 1 }).IsMatch(input));
    }

    [Fact]
    public void DisjointOptionalTokensNeedNoHeapBeforeTerminalDigitRuns()
    {
        using var regex = Create(@"^[-+]?\d*\.?\d*$", Pcre2CompileOptions.Ucp,
            new Utf8Pcre2ExecutionLimits { DepthLimit = 1, HeapLimitInBytes = 1 });
        Assert.True(regex.IsMatch("-12.5"u8));
        Assert.True(regex.IsMatch("+١.𝟚\n"u8));
        Assert.False(regex.IsMatch("3.14x9"u8));
    }

    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$", Pcre2CompileOptions.None, "abccba")]
    [InlineData("(\\d{4}[- ]){3}\\d{3,4}[0-9]\\z", Pcre2CompileOptions.None, "4111-1111-1111-1111")]
    [InlineData("^[+-]?[+-]?[+-]?\\d*$", Pcre2CompileOptions.None, "++12")]
    public void AllocatingSearchBreachesHeap(string pattern, Pcre2CompileOptions options, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(Create(pattern, options, default).IsMatch(input));
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(pattern, options, new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 }).IsMatch(input));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, exception.ErrorKind);
    }

    [Theory]
    [InlineData("(a)b+", "ab abb abbb x ab")]
    [InlineData(".*(ss)", "xxss")]
    [InlineData(@"^([0-9]+)(\-| |$)(.*)$", "200 ok")]
    public void WholeValueSearchOmitsUnobservedCaptureSlots(string pattern, string inputText)
    {
        using var regex = Create(pattern, Pcre2CompileOptions.None, new() { HeapLimitInBytes = 1 });
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(regex.IsMatch(input));
        Assert.True(regex.Match(input).Success);
        var captures = new Utf8Pcre2CaptureRange[regex.CaptureGroupCount + 1];
        Assert.Equal(Pcre2ErrorKind.HeapLimit,
            Assert.Throws<Pcre2MatchException>(() => regex.Match(input, captures: captures)).ErrorKind);
    }
}
