using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

/// <summary>Executes the resource-limit example published in SPEC-PCRE2.md.</summary>
public sealed class Pcre2SafetyContractExampleTests
{
    [Fact]
    public void TinyMatchLimitBreachesLiteralCount()
    {
        var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 1 };
        var regex = new Utf8Pcre2Regex("foo", options: Pcre2CompileOptions.None, compileSettings: default, policy: new Utf8RegexPolicy
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
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count("foo foo foo"u8));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }
}
