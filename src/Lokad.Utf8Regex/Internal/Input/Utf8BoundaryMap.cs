using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Input;

internal sealed class Utf8BoundaryMap
{
    private readonly int[]? _byteOffsets;
    private readonly bool[]? _isScalarBoundary;
    private readonly bool _isAscii;

    private Utf8BoundaryMap(int byteLength, int utf16Length, bool isAscii)
    {
        ByteLength = byteLength;
        Utf16Length = utf16Length;
        _isAscii = isAscii;
        if (!isAscii)
        {
            _byteOffsets = new int[utf16Length + 1];
            _isScalarBoundary = new bool[utf16Length + 1];
        }
    }

    public int ByteLength { get; }

    public int Utf16Length { get; }

    public static Utf8BoundaryMap Create(ReadOnlySpan<byte> input)
    {
        return Create(input, Utf8Validation.Validate(input));
    }

    public static Utf8BoundaryMap Create(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        var map = new Utf8BoundaryMap(validation.ByteLength, validation.Utf16Length, validation.IsAscii);
        if (!validation.IsAscii)
        {
            map.Build(input);
        }

        return map;
    }

    public Utf16Boundary Resolve(int utf16Offset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(utf16Offset);
        if (utf16Offset > Utf16Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(utf16Offset),
                utf16Offset,
                "The requested UTF-16 offset exceeds the length of the input.");
        }

        if (_isAscii)
        {
            return Utf16Boundary.ScalarBoundary(utf16Offset, utf16Offset);
        }

        return _isScalarBoundary![utf16Offset]
            ? Utf16Boundary.ScalarBoundary(_byteOffsets![utf16Offset], utf16Offset)
            : Utf16Boundary.SurrogateSplitBoundary(_byteOffsets![utf16Offset], utf16Offset);
    }

    public int GetUtf16OffsetForByteOffset(int byteOffset)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(byteOffset);
        if (byteOffset > ByteLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteOffset),
                byteOffset,
                "The requested byte offset exceeds the length of the input.");
        }

        if (_isAscii)
        {
            return byteOffset;
        }

        var offsets = _byteOffsets!;
        var index = Array.BinarySearch(offsets, byteOffset);
        if (index < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteOffset),
                byteOffset,
                "The requested byte offset is not aligned to a UTF-16 boundary.");
        }

        while (index > 0 && offsets[index - 1] == byteOffset)
        {
            index--;
        }

        for (var candidate = index; candidate < offsets.Length && offsets[candidate] == byteOffset; candidate++)
        {
            if (_isScalarBoundary![candidate])
            {
                return candidate;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(byteOffset),
            byteOffset,
            "The requested byte offset is not aligned to a scalar boundary.");
    }

    public bool TryGetByteRange(int indexInUtf16, int lengthInUtf16, out int indexInBytes, out int lengthInBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(indexInUtf16);
        ArgumentOutOfRangeException.ThrowIfNegative(lengthInUtf16);

        var endInUtf16 = checked(indexInUtf16 + lengthInUtf16);
        if (endInUtf16 > Utf16Length)
        {
            throw new ArgumentOutOfRangeException(nameof(lengthInUtf16));
        }

        var start = Resolve(indexInUtf16);
        var end = Resolve(endInUtf16);
        if (!start.IsScalarBoundary || !end.IsScalarBoundary)
        {
            indexInBytes = 0;
            lengthInBytes = 0;
            return false;
        }

        indexInBytes = start.ByteOffset;
        lengthInBytes = end.ByteOffset - start.ByteOffset;
        return true;
    }

    public bool TryAdvanceCodeUnit(int utf16Offset, out Utf16Boundary nextBoundary)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(utf16Offset);
        if (utf16Offset >= Utf16Length)
        {
            nextBoundary = default;
            return false;
        }

        nextBoundary = Resolve(utf16Offset + 1);
        return true;
    }

    public bool TryAdvanceScalar(int utf16Offset, out Utf16Boundary nextBoundary)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(utf16Offset);
        if (utf16Offset >= Utf16Length)
        {
            nextBoundary = default;
            return false;
        }

        var current = Resolve(utf16Offset);
        var nextOffset = utf16Offset + 1;
        if (nextOffset < Utf16Length &&
            current.IsScalarBoundary &&
            !Resolve(nextOffset).IsScalarBoundary)
        {
            nextOffset++;
        }

        nextBoundary = Resolve(nextOffset);
        return true;
    }

    public bool TryRetreatCodeUnit(int utf16Offset, out Utf16Boundary previousBoundary)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(utf16Offset);
        if (utf16Offset == 0)
        {
            previousBoundary = default;
            return false;
        }

        previousBoundary = Resolve(utf16Offset - 1);
        return true;
    }

    public bool TryRetreatScalar(int utf16Offset, out Utf16Boundary previousBoundary)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(utf16Offset);
        if (utf16Offset == 0)
        {
            previousBoundary = default;
            return false;
        }

        var current = Resolve(utf16Offset);
        var previousOffset = utf16Offset - 1;
        if (previousOffset > 0 &&
            current.IsScalarBoundary &&
            !Resolve(previousOffset).IsScalarBoundary)
        {
            previousOffset--;
        }

        previousBoundary = Resolve(previousOffset);
        return true;
    }

    private void Build(ReadOnlySpan<byte> input)
    {
        var currentByte = 0;
        var currentUtf16 = 0;

        _byteOffsets![0] = 0;
        _isScalarBoundary![0] = true;

        while (currentByte < input.Length)
        {
            var status = Rune.DecodeFromUtf8(input[currentByte..], out var rune, out var bytesConsumed);
            if (status != OperationStatus.Done)
            {
                throw Utf8Validation.CreateInvalidUtf8Exception(currentByte);
            }

            if (rune.IsBmp)
            {
                currentByte += bytesConsumed;
                currentUtf16++;
                _byteOffsets[currentUtf16] = currentByte;
                _isScalarBoundary[currentUtf16] = true;
                continue;
            }

            _byteOffsets[currentUtf16 + 1] = currentByte;
            _isScalarBoundary[currentUtf16 + 1] = false;

            currentByte += bytesConsumed;
            currentUtf16 += 2;
            _byteOffsets[currentUtf16] = currentByte;
            _isScalarBoundary[currentUtf16] = true;
        }
    }
}
