using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

/// <summary>Executes the resource-limit example published in SPEC-PCRE2.md.</summary>
public sealed class Pcre2SafetyContractExampleTests
{
    [Fact]
    public void TinyMatchLimitBreachesLiteralCount()
    {
        var limits = new Utf8Pcre2ExecutionLimits { MatchLimit = 1 };
        var regex = new Utf8Pcre2Regex("foo", Pcre2CompileOptions.None, default, limits, Utf8Pcre2Regex.DefaultMatchTimeout);
        var exception = Assert.Throws<Pcre2MatchException>(() => regex.Count("foo foo foo"u8));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, exception.ErrorKind);
    }
}
