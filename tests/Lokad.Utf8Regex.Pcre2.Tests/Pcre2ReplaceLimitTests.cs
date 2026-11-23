using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Replacement honors the same work budget as every other global operation:
// a breached replacement aborts with a kind-identified exception instead of
// returning a partial prefix from a byte-template operation.
public sealed class Pcre2ReplaceLimitTests
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
    [InlineData("foo", "foo foo foo", "Z")]
    [InlineData("cat|horse", "cat horse cat", "Z")]
    [InlineData("(a+)+b", "aaab aaab", "Z")]
    [InlineData("[ab]+", "ab ab ab", "Z")]
    public void TinyLimitBreachesTemplateReplace(string pattern, string inputText, string replacement)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var exception = Assert.Throws<Pcre2MatchException>(() => Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = 1 }).Replace(input, Encoding.UTF8.GetBytes(replacement)));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);

        var generous = Create(pattern, new Utf8Pcre2ExecutionLimits { MatchLimit = 1000 }).Replace(input, Encoding.UTF8.GetBytes(replacement));
        var unlimited = Create(pattern, default).Replace(input, Encoding.UTF8.GetBytes(replacement));
        Assert.Equal(unlimited, generous);
    }
    [Fact]
    public void TinyDepthLimitBreachesTemplateReplace()
    {
        var input = Encoding.UTF8.GetBytes("aaabbb aaabbb");
        var exception = Assert.Throws<Pcre2MatchException>(() => Create("(a)+(b)+", new Utf8Pcre2ExecutionLimits { DepthLimit = 1 }).Replace(input, "Z"u8));
        Assert.Equal(Pcre2ErrorKind.DepthLimit, exception.ErrorKind);
    }

    [Fact]
    public void TinyHeapLimitBreachesTemplateReplace()
    {
        var input = Encoding.UTF8.GetBytes("aaabbb aaabbb");
        var exception = Assert.Throws<Pcre2MatchException>(() => Create("(a+)(b+)", new Utf8Pcre2ExecutionLimits { HeapLimitInBytes = 1 }).Replace(input, "Z"u8));
        Assert.Equal(Pcre2ErrorKind.HeapLimit, exception.ErrorKind);
    }

    [Fact]
    public void SliceTemplateReuseClearsUnsetGroupsAndChargesEachProjection()
    {
        const string pattern = "[a-z]+@(?<word>[a-z]+)?(?<digits>[0-9]+)?";
        const Pcre2SubstitutionOptions options = Pcre2SubstitutionOptions.UnsetEmpty;
        using var regex = Create(pattern, default);
        Assert.IsType<Pcre2SliceDirectProgram>(regex.DebugCompiledProgram.Operations.Replace);
        Assert.Equal("<tail:>!<:12>!<fin:>"u8.ToArray(),
            regex.Replace("ab@tail!cd@12!xy@fin"u8, "<${word}:${digits}>"u8, substitutionOptions: options));

        using var limited = Create(pattern, new()
        {
            ResultLimitInBytes = 2 * Utf8ConstructionBudget.ArrayBytes<Pcre2GroupData>(3)
        });
        Assert.IsType<Pcre2SliceDirectProgram>(limited.DebugCompiledProgram.Operations.Replace);
        var error = Assert.Throws<Pcre2MatchException>(() =>
            limited.Replace("ab@tail!cd@12!xy@fin"u8, "<${word}:${digits}>"u8, substitutionOptions: options));
        Assert.Equal(Pcre2ErrorKind.ResultLimit, error.ErrorKind);
    }
}
