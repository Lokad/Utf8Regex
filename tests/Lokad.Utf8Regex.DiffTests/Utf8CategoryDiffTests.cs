using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8CategoryDiffTests
{
    public static TheoryData<string, string> CategoryCases => new()
    {
        { "\\p{L}+", "a\u03B1b12" },
        { "\\p{Nd}+", "a12b345" },
        { "\\P{L}+", "a1 b2" },
        { "\\p{Ll}+", "aAbB" },
        { "\\p{IsGreek}+", "x\u03B1\u03B2y" },
        { "\\p{IsBasicLatin}+", "AB\u00C9" },
        { "(\\p{L}+)", "a1b" },
        { "[\\p{L}]+", "a\u03B1" },
        { "\\p{L}{2,4}", "abcdefg" },
        { "[\\p{L}a]+", "1a\u03B1b" },
        { "[\\p{L}\\p{Nd}_]+", "a1_" },
        { "[^\\p{L}a]+", "a1 b" },
        { "[\\P{L}-x]+", "a-x1" },
        { "[\\p{L}-x]+", "a-x1" },
        { "[\\p{L}\\D]+", "a15 " },
        { "[^\\p{L}\\d]+", "a1 " },
        { "[\\p{IsGreek}a]+", "a\u03B1b1" },
        { "[^\\p{IsGreek}a]+", "a\u03B1b1" },
        { "[\\p{L}a-z]+", "aM1" },
        { "([\\p{L}\\p{Nd}_]+)", "xa1" },
        { "\\p{Nd}{2}", "a12b345" },
        { "[^\\p{L}]+", "a1 b2" },
    };

    [Theory]
    [MemberData(nameof(CategoryCases))]
    public void CategoryCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(CategoryCases))]
    public void CategoryMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(CategoryCases))]
    public void CategoryReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(CategoryCases))]
    public void CategoryEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertEnumeratedCaptureParity();
    }

    public static TheoryData<string, string> CategoryIgnoreCaseCases => new()
    {
        { "\\p{IsBasicLatin}+", "a\u212A1" },
        { "\\P{IsBasicLatin}+", "a\u212AK B" },
        { "\\p{IsCherokee}+", "x\u13A0\uAB70y" },
        { "\\P{IsCherokee}+", "x\u13A0\uAB70y" },
        { "[\\p{IsBasicLatin}a]+", "1aKB\u00E9" },
        { "[^\\p{IsBasicLatin}a]+", "aKB\u00E9" },
        { "[\\P{IsBasicLatin}a]+", "1\u00E9" },
        { "[^\\P{IsBasicLatin}]+", "1ak\u00E9" },
        { "[\\p{IsCherokee}ab]+", "xa\u13A0\uAB701" },
        { "(\\p{IsLatinExtended-A}+)", "x\u0131I\u0160" },
        { "\\p{_xmlC}+", "a-K" },
        { "[\\p{_xmlC}a]+", "1aK\u00E9" },
    };

    private const RegexOptions CategoryIgnoreCaseOptions =
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    [Theory]
    [MemberData(nameof(CategoryIgnoreCaseCases))]
    public void CategoryIgnoreCaseCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, CategoryIgnoreCaseOptions).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(CategoryIgnoreCaseCases))]
    public void CategoryIgnoreCaseMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, CategoryIgnoreCaseOptions).AssertCaptureMatchParity();
    }

    [Theory]
    [MemberData(nameof(CategoryIgnoreCaseCases))]
    public void CategoryIgnoreCaseReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, CategoryIgnoreCaseOptions).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(CategoryIgnoreCaseCases))]
    public void CategoryIgnoreCaseEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, CategoryIgnoreCaseOptions).AssertEnumeratedCaptureParity();
    }

}
