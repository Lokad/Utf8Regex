using System.Text;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Pcre2.Tests;

// Empty-match progression: after an empty match the search advances one
// UTF-8 scalar (never into the middle of one), so global operations visit
// every scalar boundary exactly once and Count, EnumerateMatches, and
// MatchMany agree.
public sealed class Pcre2EmptyMatchIterationTests
{
    private static Utf8Pcre2Regex Create(string pattern)
    {
        return new Utf8Pcre2Regex(pattern, Pcre2CompileOptions.None, default, default(Utf8Pcre2ExecutionLimits), Utf8Pcre2Regex.DefaultMatchTimeout);
    }

    private static int[] EnumerateStarts(Utf8Pcre2Regex regex, byte[] input)
    {
        var starts = new List<int>();
        var enumerator = regex.EnumerateMatches(input);
        while (enumerator.MoveNext())
        {
            starts.Add(enumerator.Current.StartOffsetInBytes);
        }

        return starts.ToArray();
    }

    [Theory]
    [InlineData("a*", "bbb", 4, "0,1,2,3")]
    [InlineData("a*", "", 1, "0")]
    [InlineData("x*", "axb", 4, "0,1,2,3")]
    [InlineData("", "ab", 3, "0,1,2")]
    public void EmptyMatchesVisitEveryBoundaryOnce(string pattern, string inputText, int expected, string expectedStarts)
    {
        var input = Encoding.UTF8.GetBytes(inputText);
        var regex = Create(pattern);
        Assert.Equal(expected, regex.Count(input));
        Assert.Equal(expectedStarts, string.Join(",", EnumerateStarts(regex, input)));

        var destination = new Utf8Pcre2MatchData[8];
        Assert.Equal(expected, regex.MatchMany(input, destination, out var isMore));
        Assert.False(isMore);
    }

    [Fact]
    public void EmptyMatchesAdvanceWholeScalars()
    {
        var input = Encoding.UTF8.GetBytes("é");
        var regex = Create("x*");
        Assert.Equal(2, regex.Count(input));
        Assert.Equal("0,2", string.Join(",", EnumerateStarts(regex, input)));
    }
}
