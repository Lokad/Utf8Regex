using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.DiffTests;

public sealed class Utf8RegexContiguousDiffTests
{
    public static TheoryData<string, string> ContiguousCases => new()
    {
        { @"\Ga+", "aabaa" },
        { @"\Ga*", "aaa" },
        { @"\Gx*|y", "axbyc" },
        { @"\G(?:a|b)+", "abxab" },
        { @"a\Gb", "aab ab" },
        { @"(?<=\Ga)b", "ab ab" },
    };

    [Theory]
    [MemberData(nameof(ContiguousCases))]
    public void ContiguousCountMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertCountParity();
    }

    [Theory]
    [MemberData(nameof(ContiguousCases))]
    public void ContiguousMatchGroupsMatchRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertDetailedMatchGroupParity();
    }

    [Theory]
    [MemberData(nameof(ContiguousCases))]
    public void ContiguousReplaceMatchesRuntime(string pattern, string input)
    {
        RegexParityContext.Create(pattern, input, RegexOptions.CultureInvariant).AssertReplaceParity("X");
    }

    // Match positions compare directly: re-querying detailed results at a match
    // index starts a fresh search with a fresh pin, which even the BCL oracle
    // answers differently (verified: BCL Match(input, 1) misses where Matches
    // from 0 finds [1,2)).
    [Theory]
    [MemberData(nameof(ContiguousCases))]
    public void ContiguousEnumerateMatchesRuntime(string pattern, string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var expected = new List<(int Index, int Length)>();
        foreach (Match match in new Regex(pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout).Matches(input))
        {
            expected.Add((match.Index, match.Length));
        }

        foreach (var compiled in new[] { false, true })
        {
            var options = RegexOptions.CultureInvariant |
                (compiled ? RegexOptions.Compiled : RegexOptions.None);
            var regex = new Utf8Regex(pattern, options);
            var actual = new List<(int Index, int Length)>();
            foreach (var match in regex.EnumerateMatches(bytes))
            {
                actual.Add((match.IndexInUtf16, match.LengthInUtf16));
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