using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexNonCapturingDiffTests
{
    public static TheoryData<string, string> NonCapturingCases => new()
    {
        { @"(?:a)+", "aa aa ab" },
        { @"(?:a|b)+c", "abc ac bc" },
        { @"(a)(?:b)(c)", "abc axc abc" },
        { @"(?:(?:a))", "a b a" },
        { @"a(?#middle)b", "ab acb ab" },
        { @"(?#lead)a", "a b a" },
    };

    [Theory]
    [MemberData(nameof(NonCapturingCases))]
    public void NonCapturingCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(NonCapturingCases))]
    public void NonCapturingMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(NonCapturingCases))]
    public void NonCapturingReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(NonCapturingCases))]
    public void NonCapturingEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertEnumeratedCaptureParity();
    }

}