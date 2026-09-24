using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8InlineDiffTests
{
    public static TheoryData<string, string> InlineCases => new()
    {
        { "(?i:ab)+", "ABab" },
        { "(?i)a+b+", "AABB" },
        { "(?m:^a)+", "x\na\na" },
        { "(?s:a.c)+", "xa\ncy" },
        { "(a)(?i:b)+", "xaBBxb" },
        { "(?i:(a)(b))+", "ABab" },
        { "a(?i:b|c)+d", "aBdaCdabd" },
        { "(?-i:ab)+", "abABab" },
        { "((?i)a)+", "AaA" },
        { "(?im:ab)+", "ABab" },
        { "(?m:a$)+", "a\nba" },
        { "(a)?(?(1)(?i)b|c)", "abac" },
        { "(?n:(a)(b))+", "ababab" },
    };

    [Theory]
    [MemberData(nameof(InlineCases))]
    public void InlineCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(InlineCases))]
    public void InlineMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [MemberData(nameof(InlineCases))]
    public void InlineReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(InlineCases))]
    public void InlineEnumerateGroupsMatchRuntime(string pattern, string input)
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

