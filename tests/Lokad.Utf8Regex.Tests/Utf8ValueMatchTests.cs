using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValueMatchTests
{
    [Fact]
    public void RightToLeftLiteralMatchReturnsRightmostOccurrence()
    {
        var regex = new Utf8Regex("abc", RegexOptions.CultureInvariant | RegexOptions.RightToLeft);

        var match = regex.Match("xxabcxxabc"u8);

        Assert.True(match.Success);
        Assert.Equal(7, match.IndexInUtf16);
        Assert.Equal(3, match.LengthInUtf16);
        Assert.Equal(7, match.IndexInBytes);
        Assert.Equal(3, match.LengthInBytes);
    }

    [Fact]
    public void Utf8LiteralMatchProjectsUtf16AndByteCoordinates()
    {
        var regex = new Utf8Regex("😀", RegexOptions.CultureInvariant);

        var match = regex.Match("x😀y😀"u8);

        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(1, match.IndexInUtf16);
        Assert.Equal(2, match.LengthInUtf16);
        Assert.Equal(1, match.IndexInBytes);
        Assert.Equal(4, match.LengthInBytes);
    }

    [Fact]
    public void BytePropertiesThrowForNonAlignedMatches()
    {
        var match = new Utf8ValueMatch(
            success: true,
            isByteAligned: false,
            indexInUtf16: 3,
            lengthInUtf16: 1);

        Assert.Throws<InvalidOperationException>(() => _ = match.IndexInBytes);
        Assert.Throws<InvalidOperationException>(() => _ = match.LengthInBytes);
    }

    [Fact]
    public void TryGetByteRangeReturnsCoordinatesForAlignedMatch()
    {
        var match = new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: 1,
            lengthInUtf16: 2,
            indexInBytes: 3,
            lengthInBytes: 4);

        Assert.True(match.TryGetByteRange(out var indexInBytes, out var lengthInBytes));
        Assert.Equal(3, indexInBytes);
        Assert.Equal(4, lengthInBytes);
    }

    [Fact]
    public void TryGetByteRangeReturnsFalseForNonAlignedMatch()
    {
        var match = new Utf8ValueMatch(
            success: true,
            isByteAligned: false,
            indexInUtf16: 3,
            lengthInUtf16: 1);

        Assert.False(match.TryGetByteRange(out var indexInBytes, out var lengthInBytes));
        Assert.Equal(0, indexInBytes);
        Assert.Equal(0, lengthInBytes);
    }
}
