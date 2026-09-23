using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexBalanceDiffTests
{
    public static TheoryData<string, string> BalanceCases => new()
    {
        { @"(?<open>a)+(?<-open>b)+", "aab aab bbb" },
        { @"(?<open>a)(?<-open>b)", "ab ac ab" },
        { @"(?<a>x)(?<b-a>y)", "xy xx xy" },
        { @"(?<a>x)(?<b-a>y)(?<c-b>z)", "xyz xy xyz" },
        { @"((?<o>a)+(?<-o>b)+)", "aab b aab" },
        { @"(?<o>a)(?<-o>b)?b", "ab b ab" },
    };

    [Theory]
    [MemberData(nameof(BalanceCases))]
    public void BalanceCountMatchesRuntime(string pattern, string input)
    {
        // Balancing pops drain the stack and interval captures record
        // [popped end, pop-time position); the oracle defines per-shape truth.
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(BalanceCases))]
    public void BalanceMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [MemberData(nameof(BalanceCases))]
    public void BalanceReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("<$1>");
    }

    [Theory]
    [MemberData(nameof(BalanceCases))]
    public void BalanceEnumerateGroupsMatchRuntime(string pattern, string input)
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