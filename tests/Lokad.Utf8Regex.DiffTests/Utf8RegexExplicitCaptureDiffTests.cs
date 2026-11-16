using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexExplicitCaptureDiffTests
{
    private const RegexOptions Options = RegexOptions.ExplicitCapture | RegexOptions.CultureInvariant;

    public static TheoryData<string, string> ExplicitCases => new()
    {
        { "(a)(b)", "ab ab" },
        { "(a)(?<x>b)", "ab ab" },
        { "(a)+(?<x>b)+", "aabb ab" },
        { "(a)(?<x>b)\\1", "abb ab" },
    };

    [Theory]
    [MemberData(nameof(ExplicitCases))]
    public void ExplicitCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(ExplicitCases))]
    public void ExplicitMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(ExplicitCases))]
    public void ExplicitReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(ExplicitCases))]
    public void ExplicitEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertEnumeratedCaptureParity();
    }

}