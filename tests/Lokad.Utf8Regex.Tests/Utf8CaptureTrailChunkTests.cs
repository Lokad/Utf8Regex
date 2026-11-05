using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8CaptureTrailChunkTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)]
    [InlineData(7)] [InlineData(8)] [InlineData(9)]
    [InlineData(15)] [InlineData(16)] [InlineData(17)]
    [InlineData(31)] [InlineData(32)] [InlineData(33)]
    [InlineData(63)] [InlineData(64)] [InlineData(65)]
    [InlineData(127)] [InlineData(128)] [InlineData(129)] [InlineData(257)]
    public void HistoriesAndRollbackSurviveChunkAndTableBoundaries(int count)
    {
        var text = new string('a', count) + "b";
        var input = Encoding.UTF8.GetBytes(text);
        foreach (var pattern in new[] { "^(?<v>a)*b$", "^(?:(?<v>a)+c|(?<v>a)*b)$" })
        foreach (var mode in new[] { RegexOptions.None, RegexOptions.Compiled, RegexOptions.RightToLeft })
        {
            var oracle = new Regex(pattern, mode);
            var regex = new Utf8Regex(pattern, mode);
            var expected = oracle.Match(text);
            var retained = regex.MatchDetailed(input);
            Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, retained);
            Assert.True(regex.MatchDetailed("b"u8).Success);
            Assert.Equal(1, regex.Count(input));
            Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, retained);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ScalarHistoriesRemainValidAfterLargeGrowthAndReuse(bool reverse)
    {
        var options = reverse ? RegexOptions.RightToLeft : RegexOptions.None;
        var text = string.Concat(Enumerable.Repeat("é😀", 129)) + "X";
        var input = Encoding.UTF8.GetBytes(text);
        const string pattern = "(?<v>é|😀)+X";
        var oracle = new Regex(pattern, options);
        var expected = oracle.Match(text);
        var regex = new Utf8Regex(pattern, options);
        var retained = regex.MatchDetailed(input);
        Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, retained);
        Assert.True(regex.IsMatch("éX"u8));
        Utf8SliceSparseGroupTests.AssertDetailed(oracle, expected, retained);
    }
}
