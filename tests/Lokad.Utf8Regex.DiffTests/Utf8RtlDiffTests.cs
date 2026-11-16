using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RtlDiffTests
{
    private static RegexOptions Rtl => RegexOptions.RightToLeft | RegexOptions.CultureInvariant;

    public static TheoryData<string, string> RtlCases => new()
    {
        { "(a+)(b+)", "xaaby" },
        { "a|ab", "xaabx" },
        { "a(?=b)", "xabx" },
        { "(?<=a)b", "xaab" },
        { "(a)(b)", "xab" },
        { "(?:ab)+", "xababx" },
        { "[ab]+", "cxaby" },
    };

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void RtlCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Rtl).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void RtlMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Rtl).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void RtlReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Rtl).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void RtlEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Rtl).AssertEnumeratedCaptureParity();
    }

}