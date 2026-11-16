using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexEscapeDiffTests
{
    public static TheoryData<string, string> EscapeCases => new()
    {
        { "\\x41+", "AAaBB" },
        { "\\u0041+", "AAa" },
        { "\\01+", "\0\0a" },
        { "[\\x41-\\x5A]+", "ABcAB" },
        { "[\\101-\\132]+", "STs" },
        { "(\\x41)(\\x42)", "AB ab" },
        { "a\\0b", "a\0b" },
        { "\\n+", "a\n\nb" },
        { "\\t+", "a\tb\t" },
        { "\\cA+", "a\u0001\u0001b" },
        { "\\e+", "a\u001Bb" },
    };

    [Theory]
    [MemberData(nameof(EscapeCases))]
    public void EscapeCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(EscapeCases))]
    public void EscapeMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(EscapeCases))]
    public void EscapeReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(EscapeCases))]
    public void EscapeEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertEnumeratedCaptureParity();
    }

}
