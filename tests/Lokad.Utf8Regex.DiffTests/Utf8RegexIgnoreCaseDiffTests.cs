using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexIgnoreCaseDiffTests
{
    private const RegexOptions Options = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    public static TheoryData<string, string> IgnoreCaseCases => new()
    {
        { "a+", "AAaBbbA" },
        { "abc", "xABCxabx" },
        { "[a-z]+", "ABC def GHI" },
        { "[^a]+", "AbB" },
        { "(a)\\1", "aAaA" },
        { "(ab)\\1", "ABab ab" },
        { "(a)(b)", "Ab aB AB" },
        { "k", "Kk" },
    };

    [Theory]
    [MemberData(nameof(IgnoreCaseCases))]
    public void IgnoreCaseCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseCases))]
    public void IgnoreCaseMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseCases))]
    public void IgnoreCaseReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(IgnoreCaseCases))]
    public void IgnoreCaseEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertEnumeratedCaptureParity();
    }

}