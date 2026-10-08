using System.Buffers;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Input;

internal readonly partial struct Utf8BoundaryMap
{
    private readonly int[] _byteOffsets;
    private readonly uint[] _scalarBoundaryBits;
    private readonly bool _isAscii;

    private Utf8BoundaryMap(int byteLength, int utf16Length, bool isAscii)
    {
        ByteLength = byteLength;
        Utf16Length = utf16Length;
        _isAscii = isAscii;
        _byteOffsets = isAscii ? [] : new int[utf16Length + 1];
        _scalarBoundaryBits = isAscii ? [] : new uint[((utf16Length + 1) + 31) / 32];
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

    private Utf16Boundary ResolveCore(int utf16Offset)
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

        return IsScalarBoundary(utf16Offset)
            ? Utf16Boundary.ScalarBoundary(_byteOffsets[utf16Offset], utf16Offset)
            : Utf16Boundary.SurrogateSplitBoundary(_byteOffsets[utf16Offset], utf16Offset);
    }

    private int GetUtf16OffsetForByteOffsetCore(int byteOffset)
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

        var offsets = _byteOffsets;
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
            if (IsScalarBoundary(candidate))
            {
                return candidate;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(byteOffset),
            byteOffset,
            "The requested byte offset is not aligned to a scalar boundary.");
    }

    private bool TryGetUtf16OffsetForByteOffsetCore(int byteOffset, out int utf16Offset)
    {
        utf16Offset = 0;
        if (byteOffset < 0 || byteOffset > ByteLength)
        {
            return false;
        }

        if (_isAscii)
        {
            utf16Offset = byteOffset;
            return true;
        }

        var offsets = _byteOffsets;
        var index = Array.BinarySearch(offsets, byteOffset);
        if (index < 0)
        {
            return false;
        }

        while (index > 0 && offsets[index - 1] == byteOffset)
        {
            index--;
        }

        for (var candidate = index; candidate < offsets.Length && offsets[candidate] == byteOffset; candidate++)
        {
            if (IsScalarBoundary(candidate))
            {
                utf16Offset = candidate;
                return true;
            }
        }

        return false;
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

    private void Build(ReadOnlySpan<byte> input)
    {
        var currentByte = 0;
        var currentUtf16 = 0;

        _byteOffsets[0] = 0;
        SetScalarBoundary(0);

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
                SetScalarBoundary(currentUtf16);
                continue;
            }

            _byteOffsets[currentUtf16 + 1] = currentByte;
            currentByte += bytesConsumed;
            currentUtf16 += 2;
            _byteOffsets[currentUtf16] = currentByte;
            SetScalarBoundary(currentUtf16);
        }
    }

    private bool IsScalarBoundary(int utf16Offset)
        => (_scalarBoundaryBits[utf16Offset >> 5] & (1u << (utf16Offset & 31))) != 0;

    private void SetScalarBoundary(int utf16Offset)
        => _scalarBoundaryBits[utf16Offset >> 5] |= 1u << (utf16Offset & 31);
}
