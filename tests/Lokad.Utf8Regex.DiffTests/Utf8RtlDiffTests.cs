using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RtlDiffTests
{
    private static RegexOptions Rtl => RegexOptions.RightToLeft | RegexOptions.CultureInvariant;

    public static TheoryData<string, string> RtlCases => new()
    {
        { "(a+)(b+)", "xaaby" },
        { "a|ab", "xaabx" },
        { "a(?=b)", "xabx" },
        { "(?<=a)b", "xaab" },
        { "(a)(b)", "xab" },
        { "(?:ab)+", "xababx" },
        { "[ab]+", "cxaby" },
    };

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void RtlCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Rtl).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void RtlMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Rtl).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void RtlReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, Rtl).AssertReplaceParity("<$0>");
    }

    [Theory]
    [MemberData(nameof(RtlCases))]
    public void RtlEnumerateGroupsMatchRuntime(string pattern, string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = new List<string>();
        foreach (Match match in new Regex(pattern, Rtl, Regex.InfiniteMatchTimeout).Matches(input))
        {
            expected.Add(DescribeGroups(match));
        }

        foreach (var compiled in new[] { false, true })
        {
            var options = Rtl | (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options);
            var actual = new List<string>();
            foreach (var match in regex.EnumerateMatches(bytes))
            {
                // Right-to-left from-offset scans bound the search on the right,
                // so the detailed re-query runs from the match end (the discovery
                // edge), reproducing the same hit deterministically.
                actual.Add(DescribeGroups(regex.MatchDetailedFromUtf16Offset(bytes, match.IndexInUtf16 + match.LengthInUtf16)));
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