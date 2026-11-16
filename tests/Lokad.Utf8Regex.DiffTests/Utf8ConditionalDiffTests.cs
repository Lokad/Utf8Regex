using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8ConditionalDiffTests
{
    public static TheoryData<string, string> ConditionalCases => new()
    {
        { "(a)?(?(1)b|c)", "abcab" },
        { "(a)(?(1)b|c)(d)", "abdabd" },
        { "(?(?=a)b|c)", "abcab" },
        { "(a)?(?(1)(b)|(c))", "abc" },
        { "(?<n>a)?(?(n)b|c)", "abnab" },
    };

    [Theory]
    [MemberData(nameof(ConditionalCases))]
    public void ConditionalCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(ConditionalCases))]
    public void ConditionalMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(ConditionalCases))]
    public void ConditionalReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(ConditionalCases))]
    public void ConditionalEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertEnumeratedCaptureParity();
    }

}