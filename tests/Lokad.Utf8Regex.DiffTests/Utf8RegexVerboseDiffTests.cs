using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexVerboseDiffTests
{
    private const RegexOptions Options = RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant;

    public static TheoryData<string, string> VerboseCases => new()
    {
        { "a +b", "aab ab" },
        { "( a | b ) +c", "abac ac" },
        { "a #c", "a a" },
        { "[a b]+", "a b ab" },
        { "a\\ b", "a b ab" },
    };

    [Theory]
    [MemberData(nameof(VerboseCases))]
    public void VerboseCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(VerboseCases))]
    public void VerboseMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(VerboseCases))]
    public void VerboseReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(VerboseCases))]
    public void VerboseEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertEnumeratedCaptureParity();
    }

}