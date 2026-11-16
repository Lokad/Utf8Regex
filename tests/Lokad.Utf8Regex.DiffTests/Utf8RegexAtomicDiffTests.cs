using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexAtomicDiffTests
{
    public static TheoryData<string, string> AtomicCases => new()
    {
        { @"(?>a+)b", "aaab aaab" },
        { @"(?>a+)ab", "aaab aab" },
        { @"(?>a|ab)c", "abc ac" },
        { @"(?>(a+))b", "aaab ab" },
        { @"(?>a+)+b", "aaab ab" },
        { @"x(?>y|y)z", "xyz xyyz" },
    };

    [Theory]
    [MemberData(nameof(AtomicCases))]
    public void AtomicCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(AtomicCases))]
    public void AtomicMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(AtomicCases))]
    public void AtomicReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(AtomicCases))]
    public void AtomicEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertEnumeratedCaptureParity();
    }

}