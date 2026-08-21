using Lokad.Utf8Regex.Internal.Input;
namespace Lokad.Utf8Regex;

/// <summary>Provides a stack-only view of one capture over the original UTF-8 input.</summary>
/// <remarks>The view and any spans obtained from it must not outlive the input supplied to the matching operation.</remarks>
public readonly ref struct Utf8CaptureContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly string _decoded;
    private readonly Capture? _capture;
    private readonly Utf8BoundaryMap _boundaryMap;

    internal Utf8CaptureContext(ReadOnlySpan<byte> input, string decoded, Capture? capture, Utf8BoundaryMap boundaryMap)
    {
        _input = input;
        _decoded = decoded;
        _capture = capture;
        _boundaryMap = boundaryMap;
    }

    /// <summary>Gets whether this context represents an existing capture.</summary>
    public bool Success => _capture is not null;

    /// <summary>Gets the capture start as a zero-based UTF-16 code-unit offset, or zero when no capture exists.</summary>
    public int IndexInUtf16 => _capture?.Index ?? 0;

    /// <summary>Gets the capture length in UTF-16 code units, or zero when no capture exists.</summary>
    public int LengthInUtf16 => _capture?.Length ?? 0;

    /// <summary>Gets whether both UTF-16 capture boundaries map to UTF-8 scalar boundaries.</summary>
    public bool IsByteAligned
    {
        get
        {
            if (_capture is not { } capture)
            {
                return true;
            }

            var start = ResolveBoundary(capture.Index);
            var end = ResolveBoundary(capture.Index + capture.Length);
            return start.IsScalarBoundary && end.IsScalarBoundary;
        }
    }

    /// <summary>Gets the capture start as a zero-based byte offset in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The capture splits a UTF-8 scalar.</exception>
    public int IndexInBytes => IsByteAligned
        ? ResolveBoundary(IndexInUtf16).ByteOffset
        : throw new InvalidOperationException("The capture is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Gets the capture length in bytes in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The capture splits a UTF-8 scalar.</exception>
    public int LengthInBytes => IsByteAligned
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
        return _capture?.Value ?? string.Empty;
    }

    private Utf16Boundary ResolveBoundary(int utf16Offset)
    {
        return _boundaryMap.Resolve(utf16Offset);
    }
}
