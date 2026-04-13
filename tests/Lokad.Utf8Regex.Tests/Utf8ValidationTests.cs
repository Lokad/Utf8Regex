using System.Text;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Tests;

public sealed class Utf8ValidationTests
{
    [Fact]
    public void ValidateAsciiInputReportsExpectedLengths()
    {
        ReadOnlySpan<byte> input = "hello"u8;

        var result = Utf8Validation.Validate(input);

        Assert.Equal(5, result.ByteLength);
        Assert.Equal(5, result.Utf16Length);
        Assert.True(result.IsAscii);
        Assert.False(result.ContainsSupplementaryScalars);
        Assert.Equal(5, result.EndBoundary.ByteOffset);
        Assert.Equal(5, result.EndBoundary.Utf16Offset);
        Assert.True(result.EndBoundary.IsScalarBoundary);
    }

    [Fact]
    public void ValidateBmpInputReportsUtf16Length()
    {
        var input = "café"u8;

        var result = Utf8Validation.Validate(input);

        Assert.Equal(input.Length, result.ByteLength);
        Assert.Equal("café".Length, result.Utf16Length);
        Assert.False(result.IsAscii);
        Assert.False(result.ContainsSupplementaryScalars);
    }

    [Fact]
    public void ValidateSupplementaryInputReportsTwoUtf16CodeUnits()
    {
        var input = "a😀b"u8;

        var result = Utf8Validation.Validate(input);

        Assert.Equal(input.Length, result.ByteLength);
        Assert.Equal("a😀b".Length, result.Utf16Length);
        Assert.False(result.IsAscii);
        Assert.True(result.ContainsSupplementaryScalars);
    }

    [Fact]
    public void ThrowIfInvalidRejectsTruncatedSequence()
    {
        var invalid = new byte[] { 0xE2, 0x82 };

        var error = Assert.Throws<ArgumentException>(() => Utf8Validation.ThrowIfInvalid(invalid));

        Assert.Contains("byte offset 0", error.Message);
    }

    [Fact]
    public void ThrowIfInvalidRejectsInvalidContinuation()
    {
        var invalid = new byte[] { 0x61, 0xE2, 0x28, 0xA1 };

        var error = Assert.Throws<ArgumentException>(() => Utf8Validation.ThrowIfInvalid(invalid));

        Assert.Contains("byte offset 1", error.Message);
    }

    [Fact]
    public void SurrogateSplitBoundaryIsNotScalarBoundary()
    {
        var boundary = Utf16Boundary.SurrogateSplitBoundary(4, 7);

        Assert.Equal(4, boundary.ByteOffset);
        Assert.Equal(7, boundary.Utf16Offset);
        Assert.Equal((byte)1, boundary.IntraScalarCodeUnitOffset);
        Assert.False(boundary.IsScalarBoundary);
    }

    [Fact]
    public void BoundaryMapResolvesScalarAndSurrogateSplitOffsets()
    {
        var input = "a😀b"u8;

        var map = Utf8BoundaryMap.Create(input);

        Assert.Equal(input.Length, map.ByteLength);
        Assert.Equal("a😀b".Length, map.Utf16Length);
        Assert.Equal(0, map.Resolve(0).ByteOffset);
        Assert.Equal(1, map.Resolve(1).ByteOffset);
        Assert.False(map.Resolve(2).IsScalarBoundary);
        Assert.Equal(1, map.Resolve(2).ByteOffset);
        Assert.Equal(5, map.Resolve(3).ByteOffset);
        Assert.Equal(6, map.Resolve(4).ByteOffset);
    }

    [Fact]
    public void BoundaryMapUsesIdentityOffsetsForAsciiInput()
    {
        var map = Utf8BoundaryMap.Create("hello"u8);

        Assert.Equal(3, map.Resolve(3).ByteOffset);
        Assert.True(map.Resolve(3).IsScalarBoundary);
        Assert.True(map.TryGetByteRange(1, 3, out var indexInBytes, out var lengthInBytes));
        Assert.Equal(1, indexInBytes);
        Assert.Equal(3, lengthInBytes);
    }

    [Fact]
    public void BoundaryMapCanComputeAlignedAndUnalignedRanges()
    {
        var map = Utf8BoundaryMap.Create("a😀b"u8);

        Assert.True(map.TryGetByteRange(1, 2, out var alignedIndex, out var alignedLength));
        Assert.Equal(1, alignedIndex);
        Assert.Equal(4, alignedLength);

        Assert.False(map.TryGetByteRange(2, 1, out _, out _));
    }

    [Fact]
    public void BoundaryMapCanAdvanceAndRetreatByCodeUnitAndScalar()
    {
        var map = Utf8BoundaryMap.Create("a😀b"u8);

        Assert.True(map.TryAdvanceCodeUnit(1, out var surrogateSplit));
        Assert.Equal(2, surrogateSplit.Utf16Offset);
        Assert.False(surrogateSplit.IsScalarBoundary);

        Assert.True(map.TryAdvanceScalar(1, out var afterEmoji));
        Assert.Equal(3, afterEmoji.Utf16Offset);
        Assert.True(afterEmoji.IsScalarBoundary);

        Assert.True(map.TryRetreatCodeUnit(3, out var beforeEmojiSplit));
        Assert.Equal(2, beforeEmojiSplit.Utf16Offset);
        Assert.False(beforeEmojiSplit.IsScalarBoundary);

        Assert.True(map.TryRetreatScalar(3, out var beforeEmoji));
        Assert.Equal(1, beforeEmoji.Utf16Offset);
        Assert.True(beforeEmoji.IsScalarBoundary);
    }

    [Fact]
    public void Utf8CursorsExposeMappedOffsets()
    {
        var input = "a😀b"u8;
        var map = Utf8BoundaryMap.Create(input);

        var cursor = new Utf8Cursor(input, map, 3);
        var reverse = new Utf8ReverseCursor(input, map, 1);

        Assert.Equal(3, cursor.Utf16Offset);
        Assert.Equal(5, cursor.ByteOffset);
        Assert.True(cursor.IsScalarBoundary);
        Assert.Equal("b"u8.ToArray(), cursor.Remaining.ToArray());

        Assert.Equal(1, reverse.Utf16Offset);
        Assert.Equal(1, reverse.ByteOffset);
        Assert.True(reverse.IsScalarBoundary);
        Assert.Equal("a"u8.ToArray(), reverse.Consumed.ToArray());
    }

    [Fact]
    public void Utf8CursorsCanAdvanceAndRetreatByCodeUnitAndScalar()
    {
        var input = "a😀b"u8;
        var map = Utf8BoundaryMap.Create(input);

        var cursor = new Utf8Cursor(input, map);
        Assert.True(cursor.TryAdvanceCodeUnit(out var afterA));
        Assert.Equal(1, afterA.Utf16Offset);
        Assert.True(afterA.TryAdvanceCodeUnit(out var surrogateSplit));
        Assert.Equal(2, surrogateSplit.Utf16Offset);
        Assert.False(surrogateSplit.IsScalarBoundary);
        Assert.True(afterA.TryAdvanceScalar(out var afterEmoji));
        Assert.Equal(3, afterEmoji.Utf16Offset);

        var reverse = new Utf8ReverseCursor(input, map, map.Utf16Length);
        Assert.True(reverse.TryRetreatCodeUnit(out var beforeB));
        Assert.Equal(3, beforeB.Utf16Offset);
        Assert.True(beforeB.TryRetreatScalar(out var beforeEmoji));
        Assert.Equal(1, beforeEmoji.Utf16Offset);
        Assert.True(beforeEmoji.TryRetreatScalar(out var beforeA));
        Assert.Equal(0, beforeA.Utf16Offset);
    }

    [Fact]
    public void InputAnalyzerCanValidateAndBuildBoundaryMapInOnePass()
    {
        var input = "a😀b"u8;

        var analysis = Utf8InputAnalyzer.Analyze(input);

        Assert.Equal(input.Length, analysis.Validation.ByteLength);
        Assert.Equal("a😀b".Length, analysis.Validation.Utf16Length);
        Assert.False(analysis.Validation.IsAscii);
        Assert.True(analysis.Validation.ContainsSupplementaryScalars);
        Assert.Equal(1, analysis.BoundaryMap.Resolve(1).ByteOffset);
        Assert.False(analysis.BoundaryMap.Resolve(2).IsScalarBoundary);
        Assert.Equal(5, analysis.BoundaryMap.Resolve(3).ByteOffset);
    }
}
