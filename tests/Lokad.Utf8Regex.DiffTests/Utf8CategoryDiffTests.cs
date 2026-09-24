using System.Collections.Generic;
using System.Text;
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
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertDetailedMatchGroupParity();
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
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = new List<string>();
        foreach (Match match in new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Matches(input))
        {
            expected.Add(DescribeGroups(match));
        }

        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options);
            var actual = new List<string>();
            foreach (var match in regex.EnumerateMatches(bytes))
            {
                actual.Add(DescribeGroups(regex.MatchDetailedFromUtf16Offset(bytes, match.IndexInUtf16)));
            }

            Assert.Equal(expected, actual);
        }
    }

    private static string DescribeGroups(Match match)
    {
        var sb = new StringBuilder();
        sb.Append(match.Index).Append(":").Append(match.Length);
        foreach (Group group in match.Groups)
        {
            sb.Append("|").Append(group.Success ? group.Value : "<>");
        }

        return sb.ToString();
    }

    private static string DescribeGroups(Utf8MatchContext match)
    {
        var sb = new StringBuilder();
        sb.Append(match.IndexInUtf16).Append(":").Append(match.LengthInUtf16);
        for (var i = 0; i < match.GroupCount; i++)
        {
            var group = match.GetGroup(i);
            sb.Append("|").Append(group.Success ? group.GetValueString() : "<>");
        }

        return sb.ToString();
    }
}
