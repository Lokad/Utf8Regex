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
                MaxWorkSteps = ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).MatchLimit,
                MaxDepth = ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).DepthLimit,
                MaxWorkingBytes = ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).HeapLimitInBytes,
                MaxResultBytes = ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes == 0 ? null : ((Utf8Pcre2ExecutionLimits)(limits)).ResultLimitInBytes
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
    }

    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$", Pcre2CompileOptions.None, "abccba")]
    [InlineData("^[-+]?\\d*\\.?\\d*$", Pcre2CompileOptions.None, "-12.5")]
    [InlineData("^([0-9]+)(\\-| |$)(.*)$", Pcre2CompileOptions.None, "200 ok")]
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
    public void BoundedSearchSucceedsUnderDepthLimit(string pattern, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(Create(pattern, Pcre2CompileOptions.None, default).IsMatch(input));
        Assert.True(Create(pattern, Pcre2CompileOptions.None, new Utf8Pcre2ExecutionLimits { DepthLimit = 1 }).IsMatch(input));
    }

    [Theory]
    [InlineData("^((.)(?1)\\2|.?)$", Pcre2CompileOptions.None, "abccba")]
    [InlineData(".*(ss)", Pcre2CompileOptions.None, "xxss")]
    [InlineData("(\\d{4}[- ]){3}\\d{3,4}", Pcre2CompileOptions.None, "4111-1111-1111-1111")]
    [InlineData("^[-+]?\\d*\\.?\\d*$", Pcre2CompileOptions.None, "12.5")]
    [InlineData("^([0-9]+)(\\-| |$)(.*)$", Pcre2CompileOptions.None, "200 ok")]
    public void AllocatingSearchBreachesHeap(string pattern, Pcre2CompileOptions options, string inputText)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        Assert.True(Create(pattern, options, default).IsMatch(input));
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(pattern, options, new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 }).IsMatch(input));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, exception.ErrorKind);
    }
}
