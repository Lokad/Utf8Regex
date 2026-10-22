using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValueMatchTests
{
    [Fact]
    public void ZeroInitializedValuesRepresentNoMatch()
    {
        foreach (var match in new[] { default(Utf8ValueMatch), new Utf8ValueMatch(),
            (new Utf8ValueMatch[1])[0], Utf8ValueMatch.NoMatch,
            new Utf8ValueMatch(false, true, 7, 3, 8, 4) })
        {
            Assert.False(match.Success);
            Assert.False(match.IsByteAligned);
            Assert.Equal(0, match.IndexInUtf16);
            Assert.Equal(0, match.LengthInUtf16);
            Assert.False(match.TryGetByteRange(out var start, out var length));
            Assert.Equal(0, start);
            Assert.Equal(0, length);
            Assert.Throws<InvalidOperationException>(() => _ = match.IndexInBytes);
            Assert.Throws<InvalidOperationException>(() => _ = match.LengthInBytes);
        }
        Assert.Equal(16, System.Runtime.CompilerServices.Unsafe.SizeOf<Utf8ValueMatch>());
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, int.MaxValue)]
    [InlineData(int.MaxValue, 0)]
    [InlineData(123, int.MaxValue - 123)]
    public void PackedRangesPreserveLegalCoordinates(int start, int length)
    {
        var match = new Utf8ValueMatch(true, true, start, length, start, length);
        Assert.True(match.Success);
        Assert.True(match.IsByteAligned);
        Assert.Equal(start, match.IndexInUtf16);
        Assert.Equal(length, match.LengthInUtf16);
        Assert.Equal(start, match.IndexInBytes);
        Assert.Equal(length, match.LengthInBytes);
        Assert.True(match.TryGetByteRange(out var byteStart, out var byteLength));
        Assert.Equal(start, byteStart);
        Assert.Equal(length, byteLength);
    }

    [Theory]
    [InlineData(RegexOptions.None)]
    [InlineData(RegexOptions.Compiled)]
    [InlineData(RegexOptions.RightToLeft)]
    [InlineData(RegexOptions.NonBacktracking)]
    public void EngineMissAndSuccessfulEmptyMatchRemainDistinct(RegexOptions options)
    {
        Assert.False(new Utf8Regex("a", options).Match("😀"u8).Success);
        var empty = new Utf8Regex("^", options).Match("😀"u8);
        Assert.True(empty.Success);
        Assert.True(empty.IsByteAligned);
        Assert.Equal(0, empty.IndexInUtf16);
        Assert.Equal(0, empty.LengthInUtf16);
        Assert.True(empty.TryGetByteRange(out var start, out var length));
        Assert.Equal(0, start);
        Assert.Equal(0, length);
    }

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
