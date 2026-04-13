using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexStartAtTests
{
    [Fact]
    public void IsMatchStartAtUsesUtf16PositionInsteadOfSlicingSemantics()
    {
        var regex = new Utf8Regex(@"\Aabc", RegexOptions.CultureInvariant);

        Assert.False(regex.IsMatch("xxabc"u8, 2));
    }

    [Fact]
    public void CountStartAtMatchesRuntimeSemantics()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        Assert.Equal(1, regex.Count("abcabc"u8, 3));
    }

    [Fact]
    public void MatchStartAtReturnsFirstMatchFromRequestedUtf16Position()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        var match = regex.Match("xxabcxxabc"u8, 5);

        Assert.True(match.Success);
        Assert.Equal(7, match.IndexInUtf16);
        Assert.Equal(3, match.LengthInUtf16);
        Assert.Equal(7, match.IndexInBytes);
        Assert.Equal(3, match.LengthInBytes);
    }

    [Fact]
    public void MatchDetailedStartAtExposesDetailedContextFromRequestedUtf16Position()
    {
        var regex = new Utf8Regex("(abc)", RegexOptions.CultureInvariant);

        var match = regex.MatchDetailed("xxabcxxabc"u8, 5);

        Assert.True(match.Success);
        Assert.Equal("abc", match.GetValueString());
        Assert.Equal("abc", match.GetGroup(1).GetValueString());
        Assert.Equal(7, match.IndexInUtf16);
    }

    [Fact]
    public void EnumerateMatchesStartAtHonorsRightToLeftStartPosition()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant | RegexOptions.RightToLeft);

        var enumerator = regex.EnumerateMatches("xxabcxxabc"u8, 6);
        Assert.True(enumerator.MoveNext());

        var match = enumerator.Current;
        Assert.True(match.Success);
        Assert.Equal(2, match.IndexInUtf16);
        Assert.Equal(3, match.LengthInUtf16);
    }

    [Fact]
    public void StartAtThrowsWhenBeyondUtf16Length()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatch("abc"u8, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Count("abc"u8, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.Match("abc"u8, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchDetailed("abc"u8, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.EnumerateMatches("abc"u8, 4));
    }
}
