using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8InlineDiffTests
{
    public static TheoryData<string, string> InlineCases => new()
    {
        { "(?i:ab)+", "ABab" },
        { "(?i)a+b+", "AABB" },
        { "(?m:^a)+", "x\na\na" },
        { "(?s:a.c)+", "xa\ncy" },
        { "(a)(?i:b)+", "xaBBxb" },
        { "(?i:(a)(b))+", "ABab" },
        { "a(?i:b|c)+d", "aBdaCdabd" },
        { "(?-i:ab)+", "abABab" },
        { "((?i)a)+", "AaA" },
        { "(?im:ab)+", "ABab" },
        { "(?m:a$)+", "a\nba" },
        { "(a)?(?(1)(?i)b|c)", "abac" },
        { "(?n:(a)(b))+", "ababab" },
    };

    [Theory]
    [MemberData(nameof(InlineCases))]
    public void InlineCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(InlineCases))]
    public void InlineMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(InlineCases))]
    public void InlineReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(InlineCases))]
    public void InlineEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertEnumeratedCaptureParity();
    }

}

