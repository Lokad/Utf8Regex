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
    public void InputAnalyzerValidatesBeforeLazilyBuildingBoundaryMap()
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

    [Fact]
    public void ValidatedInputProjectsEveryMixedWidthScalarBoundary()
    {
        ReadOnlySpan<byte> input = "aé€😀z"u8;
        var subject = Utf8ValidatedInput.Create(input);
        var cursor = subject.CreateProjectionCursor();
        var expected = new (int ByteOffset, int Utf16Offset)[]
        {
            (0, 0),
            (1, 1),
            (3, 2),
            (6, 3),
            (10, 5),
            (11, 6),
        };

        foreach (var (byteOffset, utf16Offset) in expected)
        {
            var position = subject.GetBytePosition(byteOffset, nameof(byteOffset));
            Assert.Equal(utf16Offset, cursor.Project(position).Value);
        }

        Assert.Equal(input.Length, cursor.BytesConsumed);
    }

    [Fact]
    public void ValidatedInputRejectsEveryContinuationByteAsAStart()
    {
        var input = "aé€😀z"u8.ToArray();

        for (var offset = 0; offset < input.Length; offset++)
        {
            if ((input[offset] & 0xC0) == 0x80)
            {
                Assert.Throws<ArgumentOutOfRangeException>(
                    () => ValidateBytePosition(input, offset));
            }
        }
    }

    [Fact]
    public void ValidatedInputAdvancesAndRetreatsByScalar()
    {
        var subject = Utf8ValidatedInput.Create("a😀b"u8);
        var atEmoji = subject.GetBytePosition(1, "position");

        Assert.True(subject.TryAdvanceScalar(atEmoji, out var afterEmoji));
        Assert.Equal(5, afterEmoji.Value);
        Assert.True(subject.TryRetreatScalar(afterEmoji, out var beforeEmoji));
        Assert.Equal(atEmoji, beforeEmoji);
        Assert.True(subject.TryRetreatScalar(beforeEmoji, out var start));
        Assert.Equal(0, start.Value);
        Assert.False(subject.TryRetreatScalar(start, out _));
    }

    [Fact]
    public void ValidatedRangeProjectionDoesNotRevalidateDisjointGaps()
    {
        var subject = Utf8ValidatedInput.Create("aé€😀z"u8);
        var range = subject.GetByteRange(3, 7, "start", "length");

        var projected = subject.ProjectRange(range);

        Assert.Equal(2, projected.Start.Value);
        Assert.Equal(5, projected.End.Value);
        Assert.Equal(3, projected.Length);
    }

    [Fact]
    public void DenseMonotoneProjectionConsumesEachByteExactlyOnceAtEveryScale()
    {
        foreach (var repetitions in new[] { 1, 2, 4, 8, 256 })
        {
            var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aé€😀", repetitions)));
            var subject = Utf8ValidatedInput.Create(input);
            var cursor = subject.CreateProjectionCursor();
            var offset = 0;
            while (offset < input.Length)
            {
                var position = subject.GetBytePosition(offset, "offset");
                _ = cursor.Project(position);
                offset += input[offset] switch
                {
                    < 0x80 => 1,
                    < 0xE0 => 2,
                    < 0xF0 => 3,
                    _ => 4,
                };
            }

            _ = cursor.Project(subject.GetBytePosition(input.Length, "offset"));
            Assert.Equal(input.Length, cursor.BytesConsumed);
        }
    }

    [Fact]
    public void AsciiValidationAndProjectionAreAllocationFree()
    {
        ReadOnlySpan<byte> input = "the quick brown fox jumps over the lazy dog"u8;
        _ = MeasureAsciiProjectionAllocations(input);

        var allocated = MeasureAsciiProjectionAllocations(input);

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void RandomAccessProjectionIsMaterializedOnlyWhenRequested()
    {
        var input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aé€😀", 128)));
        var subject = Utf8ValidatedInput.Create(input);
        var before = GC.GetAllocatedBytesForCurrentThread();

        var map = subject.GetRandomAccessMap();

        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.True(allocated > 0);
        Assert.Equal(subject.Utf16Length, map.GetUtf16OffsetForByteOffset(input.Length));
        for (var i = 0; i < 32; i++)
        {
            Assert.Equal(1, map.GetUtf16OffsetForByteOffset(1));
            Assert.Equal(3, map.GetUtf16OffsetForByteOffset(6));
        }
    }

    private static long MeasureAsciiProjectionAllocations(ReadOnlySpan<byte> input)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var subject = Utf8ValidatedInput.Create(input);
        var cursor = subject.CreateProjectionCursor();
        for (var offset = 0; offset <= input.Length; offset++)
        {
            _ = cursor.Project(subject.GetBytePosition(offset, "offset"));
        }

        _ = subject.GetRandomAccessMap().Resolve(input.Length);
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    private static void ValidateBytePosition(byte[] input, int offset)
    {
        var subject = Utf8ValidatedInput.Create(input);
        _ = subject.GetBytePosition(offset, "startOffsetInBytes");
    }
}
