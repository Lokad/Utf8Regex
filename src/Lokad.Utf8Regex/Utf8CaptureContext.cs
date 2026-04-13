namespace Lokad.Utf8Regex;

public readonly ref struct Utf8CaptureContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly string _decoded;
    private readonly Capture? _capture;
    private readonly Utf8BoundaryMap? _boundaryMap;

    internal Utf8CaptureContext(ReadOnlySpan<byte> input, string decoded, Capture? capture, Utf8BoundaryMap? boundaryMap = null)
    {
        _input = input;
        _decoded = decoded;
        _capture = capture;
        _boundaryMap = boundaryMap;
    }

    public bool Success => _capture is not null;

    public int IndexInUtf16 => Success ? _capture!.Index : 0;

    public int LengthInUtf16 => Success ? _capture!.Length : 0;

    public bool IsByteAligned
    {
        get
        {
            if (!Success)
            {
                return true;
            }

            var start = ResolveBoundary(_capture!.Index);
            var end = ResolveBoundary(_capture.Index + _capture.Length);
            return start.IsScalarBoundary && end.IsScalarBoundary;
        }
    }

    public int IndexInBytes => IsByteAligned
        ? ResolveBoundary(IndexInUtf16).ByteOffset
        : throw new InvalidOperationException("The capture is not aligned to valid UTF-8 byte boundaries.");

    public int LengthInBytes => IsByteAligned
        ? ResolveBoundary(IndexInUtf16 + LengthInUtf16).ByteOffset - IndexInBytes
        : throw new InvalidOperationException("The capture is not aligned to valid UTF-8 byte boundaries.");

    public ReadOnlySpan<byte> GetValueBytes()
    {
        return IsByteAligned
            ? _input.Slice(IndexInBytes, LengthInBytes)
            : throw new InvalidOperationException("The capture is not aligned to valid UTF-8 byte boundaries.");
    }

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

    public string GetValueString()
    {
        return Success ? _capture!.Value : string.Empty;
    }

    private Utf16Boundary ResolveBoundary(int utf16Offset)
    {
        return _boundaryMap?.Resolve(utf16Offset) ?? Utf8Utf16BoundaryResolver.ResolveBoundary(_input, utf16Offset);
    }
}
