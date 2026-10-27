using System.Collections.Generic;
using System.Text;
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
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertDetailedMatchGroupParity();
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
