using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexBalanceDiffTests
{
    public static TheoryData<string, string> BalanceCases => new()
    {
        { @"(?<open>a)+(?<-open>b)+", "aab aab bbb" },
        { @"(?<open>a)(?<-open>b)", "ab ac ab" },
        { @"(?<a>x)(?<b-a>y)", "xy xx xy" },
        { @"(?<a>x)(?<b-a>y)(?<c-b>z)", "xyz xy xyz" },
        { @"((?<o>a)+(?<-o>b)+)", "aab b aab" },
        { @"(?<o>a)(?<-o>b)?b", "ab b ab" },
    };

    [Theory]
    [MemberData(nameof(BalanceCases))]
    public void BalanceCountMatchesRuntime(string pattern, string input)
    {
        // Balancing pops drain the stack and interval captures record
        // [popped end, pop-time position); the oracle defines per-shape truth.
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(BalanceCases))]
    public void BalanceMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(BalanceCases))]
    public void BalanceReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$1>");
    }

    [Theory]
    [MemberData(nameof(BalanceCases))]
    public void BalanceEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertEnumeratedCaptureParity();
    }

}