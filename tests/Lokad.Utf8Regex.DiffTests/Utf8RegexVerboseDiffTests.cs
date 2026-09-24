using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexVerboseDiffTests
{
    private const RegexOptions Options = RegexOptions.IgnorePatternWhitespace | RegexOptions.CultureInvariant;

    public static TheoryData<string, string> VerboseCases => new()
    {
        { "a +b", "aab ab" },
        { "( a | b ) +c", "abac ac" },
        { "a #c", "a a" },
        { "[a b]+", "a b ab" },
        { "a\\ b", "a b ab" },
    };

    [Theory]
    [MemberData(nameof(VerboseCases))]
    public void VerboseCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(VerboseCases))]
    public void VerboseMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [MemberData(nameof(VerboseCases))]
    public void VerboseReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Options).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(VerboseCases))]
    public void VerboseEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = new List<string>();
        foreach (Match match in new Regex(pattern, Options, Regex.InfiniteMatchTimeout).Matches(input))
        {
            expected.Add(DescribeGroups(match));
        }

        foreach (var compiled in new[] { false, true })
        {
            var options = Options |
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