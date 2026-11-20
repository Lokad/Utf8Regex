using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

/// <summary>Executes the resource-limit example published in SPEC-PCRE2.md.</summary>
public sealed class Pcre2SafetyContractExampleTests
{
    [Fact]
    public void TinyMatchLimitBreachesLiteralCount()
    {
        using var regex = new Utf8Pcre2Regex("foo", policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new() { MaxWorkSteps = 1 }
        });
        var error = Assert.Throws<Pcre2MatchException>(() => regex.Count("foo foo foo"u8));
        Assert.Equal(Pcre2ErrorKind.MatchLimit, error.ErrorKind);
    }
}
