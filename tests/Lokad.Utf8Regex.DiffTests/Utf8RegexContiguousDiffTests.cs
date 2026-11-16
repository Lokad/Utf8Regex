using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexContiguousDiffTests
{
    public static TheoryData<string, string> ContiguousCases => new()
    {
        { @"\Ga+", "aabaa" },
        { @"\Ga*", "aaa" },
        { @"\Gx*|y", "axbyc" },
        { @"\G(?:a|b)+", "abxab" },
        { @"a\Gb", "aab ab" },
        { @"(?<=\Ga)b", "ab ab" },
    };

    [Theory]
    [MemberData(nameof(ContiguousCases))]
    public void ContiguousCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(ContiguousCases))]
    public void ContiguousMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(ContiguousCases))]
    public void ContiguousReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("X");
    }

    [Theory]
    [MemberData(nameof(ContiguousCases))]
    public void ContiguousEnumerateMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertEnumeratedCaptureParity();
    }

}