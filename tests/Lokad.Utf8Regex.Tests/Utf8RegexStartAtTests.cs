using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8RegexStartAtTests
{
    [Fact]
    public void IsMatchStartAtUsesUtf16PositionInsteadOfSlicingSemantics()
    {
        var regex = new Utf8Regex(@"\Aabc", RegexOptions.CultureInvariant);

        Assert.False(regex.IsMatchFromUtf16Offset("xxabc"u8, 2));
    }

    [Fact]
    public void CountStartAtMatchesRuntimeSemantics()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        Assert.Equal(1, regex.CountFromUtf16Offset("abcabc"u8, 3));
    }

    [Fact]
    public void MatchStartAtReturnsFirstMatchFromRequestedUtf16Position()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        var match = regex.MatchFromUtf16Offset("xxabcxxabc"u8, 5);

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

        var match = regex.MatchDetailedFromUtf16Offset("xxabcxxabc"u8, 5);

        Assert.True(match.Success);
        Assert.Equal("abc", match.GetValueString());
        Assert.Equal("abc", match.GetGroup(1).GetValueString());
        Assert.Equal(7, match.IndexInUtf16);
    }

    [Fact]
    public void EnumerateMatchesStartAtHonorsRightToLeftStartPosition()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant | RegexOptions.RightToLeft);

        var enumerator = regex.EnumerateMatchesFromUtf16Offset("xxabcxxabc"u8, 6);
        Assert.True(enumerator.MoveNext());

        var match = enumerator.Current;
        Assert.True(match.Success);
        Assert.Equal(2, match.IndexInUtf16);
        Assert.Equal(3, match.LengthInUtf16);
    }

    [Fact]
    public void EnumerateMatchesStartAtReturnsByteAlignedMatchesFromScalarBoundary()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        var enumerator = regex.EnumerateMatchesFromUtf16Offset("éabcéabc"u8, 1);
        Assert.True(enumerator.MoveNext());

        var match = enumerator.Current;
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(1, match.IndexInUtf16);
        Assert.Equal(3, match.LengthInUtf16);
        Assert.Equal(2, match.IndexInBytes);
        Assert.Equal(3, match.LengthInBytes);
    }

    [Fact]
    public void EnumerateMatchesStartAtFallsBackWhenUtf16StartSplitsSurrogatePair()
    {
        var regex = new Utf8Regex("a", RegexOptions.CultureInvariant);

        var enumerator = regex.EnumerateMatchesFromUtf16Offset("😀a"u8, 1);
        Assert.True(enumerator.MoveNext());

        var match = enumerator.Current;
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(2, match.IndexInUtf16);
        Assert.Equal(1, match.LengthInUtf16);
        Assert.Equal(4, match.IndexInBytes);
        Assert.Equal(1, match.LengthInBytes);
    }

    [Fact]
    public void StartAtThrowsWhenBeyondUtf16Length()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant);

        Assert.Throws<ArgumentOutOfRangeException>(() => regex.IsMatchFromUtf16Offset("abc"u8, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.CountFromUtf16Offset("abc"u8, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchFromUtf16Offset("abc"u8, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.MatchDetailedFromUtf16Offset("abc"u8, 4));
        Assert.Throws<ArgumentOutOfRangeException>(() => regex.EnumerateMatchesFromUtf16Offset("abc"u8, 4));
    }
}
