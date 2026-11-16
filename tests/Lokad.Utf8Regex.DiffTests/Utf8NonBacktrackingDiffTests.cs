using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8NonBacktrackingDiffTests
{
    private static RegexOptions NonBacktracking => RegexOptions.NonBacktracking | RegexOptions.CultureInvariant;

    public static TheoryData<string, string> NonBacktrackingCases => new()
    {
        { "(a+)(b+)", "xaaby" },
        { "a|ab", "xaabx" },
        { "(a)(b)", "xab" },
        { "(?:ab)+", "xababx" },
        { "[ab]+", "cxaby" },
        { "(a|ab)*", "xaab" },
        { "\\d+-\\d+", "call 555-1234 now" },
    };

    [Theory]
    [MemberData(nameof(NonBacktrackingCases))]
    public void NonBacktrackingCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, NonBacktracking).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(NonBacktrackingCases))]
    public void NonBacktrackingMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, NonBacktracking).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(NonBacktrackingCases))]
    public void NonBacktrackingReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, NonBacktracking).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(NonBacktrackingCases))]
    public void NonBacktrackingEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, NonBacktracking).AssertEnumeratedCaptureParity();
    }

}