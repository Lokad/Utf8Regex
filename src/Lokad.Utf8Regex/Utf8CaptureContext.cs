using Lokad.Utf8Regex.Internal.Input;
namespace Lokad.Utf8Regex;

/// <summary>Provides a stack-only view of one capture over the original UTF-8 input.</summary>
/// <remarks>The view and any spans obtained from it must not outlive the input supplied to the matching operation.
/// The default view has no capture, with zero coordinates and an empty value.</remarks>
public readonly ref struct Utf8CaptureContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8BoundaryMap _boundaryMap;
    private readonly bool _ownedHasValue;
    private readonly int _ownedIndex;
    private readonly int _ownedLength;

    internal Utf8CaptureContext(ReadOnlySpan<byte> input, Utf8BoundaryMap boundaryMap, int indexInUtf16, int lengthInUtf16)
    {
        _input = input;
        _boundaryMap = boundaryMap;
        _ownedHasValue = true;
        _ownedIndex = indexInUtf16;
        _ownedLength = lengthInUtf16;
    }

    /// <summary>Gets whether this context represents an existing capture.</summary>
    public bool Success => _ownedHasValue;

    /// <summary>Gets the capture start as a zero-based UTF-16 code-unit offset, or zero when no capture exists.</summary>
    public int IndexInUtf16 => _ownedHasValue ? _ownedIndex : 0;

    /// <summary>Gets the capture length in UTF-16 code units, or zero when no capture exists.</summary>
    public int LengthInUtf16 => _ownedHasValue ? _ownedLength : 0;

    /// <summary>Gets whether both UTF-16 capture boundaries map to UTF-8 scalar boundaries.</summary>
    public bool IsByteAligned
    {
        get
        {
            if (!_ownedHasValue) return true;
            var start = ResolveBoundary(_ownedIndex);
            var end = ResolveBoundary(_ownedIndex + _ownedLength);
            return start.IsScalarBoundary && end.IsScalarBoundary;
        }
    }

    /// <summary>Gets the capture start as a zero-based byte offset in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The capture splits a UTF-8 scalar.</exception>
    public int IndexInBytes => !_ownedHasValue ? 0 : IsByteAligned
        ? ResolveBoundary(IndexInUtf16).ByteOffset
        : throw new InvalidOperationException("The capture is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Gets the capture length in bytes in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The capture splits a UTF-8 scalar.</exception>
    public int LengthInBytes => !_ownedHasValue ? 0 : IsByteAligned
        ? ResolveBoundary(IndexInUtf16 + LengthInUtf16).ByteOffset - IndexInBytes
        : throw new InvalidOperationException("The capture is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Gets the captured bytes from the original UTF-8 input.</summary>
    /// <returns>An empty span when no capture exists.</returns>
    /// <exception cref="InvalidOperationException">The capture splits a UTF-8 scalar.</exception>
    public ReadOnlySpan<byte> GetValueBytes()
    {
        return IsByteAligned
            ? _input.Slice(IndexInBytes, LengthInBytes)
            : throw new InvalidOperationException("The capture is not aligned to valid UTF-8 byte boundaries.");
    }

    /// <summary>Attempts to map the capture range to the original UTF-8 input.</summary>
    /// <param name="indexInBytes">The zero-based byte offset, or zero when the range is not byte-aligned.</param>
    /// <param name="lengthInBytes">The byte length, or zero when the range is not byte-aligned.</param>
    /// <returns><see langword="true"/> when both capture boundaries are UTF-8 scalar boundaries.</returns>
    public bool TryGetByteRange(out int indexInBytes, out int lengthInBytes)
    {
        if (!_ownedHasValue)
        {
            indexInBytes = lengthInBytes = 0;
            return true;
        }

        if (!IsByteAligned)
        {
            indexInBytes = 0;
            lengthInBytes = 0;
            return false;
        }

        indexInBytes = ResolveBoundary(IndexInUtf16).ByteOffset;
        lengthInBytes = ResolveBoundary(IndexInUtf16 + LengthInUtf16).ByteOffset - indexInBytes;
        return true;
    }

    /// <summary>Attempts to get the captured bytes from the original UTF-8 input.</summary>
    /// <param name="valueBytes">The captured bytes, or a default span when the range is not byte-aligned.</param>
    /// <returns><see langword="true"/> when the capture can be projected to bytes.</returns>
    public bool TryGetValueBytes(out ReadOnlySpan<byte> valueBytes)
    {
        if (!TryGetByteRange(out var indexInBytes, out var lengthInBytes))
        {
            valueBytes = default;
            return false;
        }

        valueBytes = _input.Slice(indexInBytes, lengthInBytes);
        return true;
    }

    /// <summary>Gets the capture value as a UTF-16 string, or an empty string when no capture exists.</summary>
    public string GetValueString()
    {
        return _ownedHasValue ? Encoding.UTF8.GetString(_input.Slice(IndexInBytes, LengthInBytes)) : string.Empty;
    }

    private Utf16Boundary ResolveBoundary(int utf16Offset)
    {
        return _boundaryMap.Resolve(utf16Offset);
    }
}
