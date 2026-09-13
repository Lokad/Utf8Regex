using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

// PLAN.md C13: literal search plans dropped leading and middle lookarounds of
// either direction and miscompiled trailing lookbehind as a forward guard,
// while MatchDetailed on the same inputs was already correct. Every row below
// failed on at least one value API before the direction-aware admission fix
// and now agrees with the .NET oracle on all of them.
public sealed class Utf8LookaroundRegressionTests
{
    [Theory]
    [InlineData("(?=x)foo", "xfoo", false, 0, 0)]
    [InlineData("(?<=x)foo", "foo", false, 0, 0)]
    [InlineData("(?<=x)foo", "xfoo", true, 1, 4)]
    [InlineData("a(?=y)x", "ax", false, 0, 0)]
    [InlineData("a(?<=x)foo", "afoo", false, 0, 0)]
    [InlineData("a(?<=a)foo", "afoo", true, 0, 4)]
    [InlineData("foo(?=o)", "fooo", true, 0, 3)]
    [InlineData("foo(?=x)", "foo", false, 0, 0)]
    [InlineData("foo(?<=o)", "foo", true, 0, 3)]
    [InlineData("foo(?<=x)", "foo", false, 0, 0)]
    [InlineData("(?<!x)foo", "foo", true, 0, 3)]
    [InlineData("ab(?<=x)cd", "abcd", false, 0, 0)]
    [InlineData("ab(?=x)cd", "abcd", false, 0, 0)]
    [InlineData("ab(?<=b)cd", "abcd", true, 0, 4)]
    [InlineData("ab(?=c)cd", "abcd", true, 0, 4)]
    public void LookaroundPositionsAgreeWithOracle(string pattern, string input, bool expectedSuccess, int expectedStart, int expectedEnd)
    {
        var regex = new Utf8Regex(pattern, RegexOptions.None);
        var oracle = new Regex(pattern, RegexOptions.None, Regex.InfiniteMatchTimeout);
        var inputBytes = System.Text.Encoding.UTF8.GetBytes(input);
        var oracleMatch = oracle.Match(input);

        Assert.Equal(expectedSuccess, oracleMatch.Success);
        Assert.Equal(expectedSuccess, regex.IsMatch(inputBytes));

        var match = regex.Match(inputBytes);
        Assert.Equal(oracleMatch.Success, match.Success);
        Assert.Equal(oracle.Matches(input).Count, regex.Count(inputBytes));

        var detailed = regex.MatchDetailed(inputBytes);
        Assert.Equal(oracleMatch.Success, detailed.Success);

        var enumerated = 0;
        foreach (var _ in regex.EnumerateMatches(inputBytes))
        {
            enumerated++;
        }

        Assert.Equal(oracle.Matches(input).Count, enumerated);

        if (expectedSuccess)
        {
            Assert.Equal(expectedStart, match.IndexInBytes);
            Assert.Equal(expectedEnd, match.IndexInBytes + match.LengthInBytes);
            Assert.Equal(input.Substring(expectedStart, expectedEnd - expectedStart), System.Text.Encoding.UTF8.GetString(inputBytes.AsSpan(match.IndexInBytes, match.LengthInBytes)));
            Assert.Equal(oracleMatch.Value, System.Text.Encoding.UTF8.GetString(inputBytes.AsSpan(match.IndexInBytes, match.LengthInBytes)));
        }
    }
}
