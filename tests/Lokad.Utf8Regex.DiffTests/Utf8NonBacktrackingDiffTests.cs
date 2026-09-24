using System.Collections.Generic;
using System.Text;
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
        RegexParityContext.Create(pattern, input, NonBacktracking).AssertDetailedMatchGroupParity();
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
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = new List<string>();
        foreach (Match match in new Regex(pattern, NonBacktracking, Regex.InfiniteMatchTimeout).Matches(input))
        {
            expected.Add(DescribeGroups(match));
        }

        foreach (var compiled in new[] { false, true })
        {
            var options = NonBacktracking | (compiled ? RegexOptions.Compiled : RegexOptions.None);
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