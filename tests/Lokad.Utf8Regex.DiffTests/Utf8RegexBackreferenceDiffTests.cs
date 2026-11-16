using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexBackreferenceDiffTests
{
    public static TheoryData<string, string> BackreferenceCases => new()
    {
        { @"(a)\1", "aa aaa a" },
        { @"(\w+)\s\1", "foo foo bar foo" },
        { @"(?<w>\w+)-\k<w>", "ab-ab ab-cd" },
        { @"(a|b)\1{2}", "aaabbb ab" },
        { @"(é|ü)\1", "éé üx üü" },
        { @"(a)(b)\2\1", "abba abab" },
        { @"((a)(b))\1", "abab aca" },
        { @"(a+)\1", "aaaa aa" },
    };

    [Theory]
    [MemberData(nameof(BackreferenceCases))]
    public void BackreferenceCountMatchesRuntime(string pattern, string input)
    {
        // backreferences reuse capture slots across global retries;
        // the oracle defines the per-retry contract.
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(BackreferenceCases))]
    public void BackreferenceMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(BackreferenceCases))]
    public void BackreferenceReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$1$2>");
    }

    [Theory]
    [MemberData(nameof(BackreferenceCases))]
    public void BackreferenceEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertEnumeratedCaptureParity();
    }

}
