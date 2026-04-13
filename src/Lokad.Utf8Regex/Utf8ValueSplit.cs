using System.Text;

namespace Lokad.Utf8Regex;

public readonly ref struct Utf8ValueSplit
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly string? _decoded;
    private readonly Utf8BoundaryMap? _boundaryMap;
    private readonly int _indexInBytes;
    private readonly int _lengthInBytes;
    private readonly bool _hasDirectByteRange;

    internal Utf8ValueSplit(ReadOnlySpan<byte> input, string? decoded, int indexInUtf16, int lengthInUtf16, Utf8BoundaryMap? boundaryMap = null)
    {
        _input = input;
        _decoded = decoded;
        IndexInUtf16 = indexInUtf16;
        LengthInUtf16 = lengthInUtf16;
        _boundaryMap = boundaryMap;
        _indexInBytes = 0;
        _lengthInBytes = 0;
        _hasDirectByteRange = false;
    }

    internal Utf8ValueSplit(ReadOnlySpan<byte> input, string? decoded, int indexInUtf16, int lengthInUtf16, int indexInBytes, int lengthInBytes)
    {
        _input = input;
        _decoded = decoded;
        IndexInUtf16 = indexInUtf16;
        LengthInUtf16 = lengthInUtf16;
        _boundaryMap = null;
        _indexInBytes = indexInBytes;
        _lengthInBytes = lengthInBytes;
        _hasDirectByteRange = true;
    }

    public bool IsByteAligned
    {
        get
        {
            if (_hasDirectByteRange)
            {
                return true;
            }

            var start = ResolveBoundary(IndexInUtf16);
            var end = ResolveBoundary(IndexInUtf16 + LengthInUtf16);
            return start.IsScalarBoundary && end.IsScalarBoundary;
        }
    }

    public int IndexInBytes => IsByteAligned
        ? (_hasDirectByteRange ? _indexInBytes : ResolveBoundary(IndexInUtf16).ByteOffset)
        : throw new InvalidOperationException("The split is not aligned to valid UTF-8 byte boundaries.");

    public int LengthInBytes => IsByteAligned
        ? (_hasDirectByteRange ? _lengthInBytes : ResolveBoundary(IndexInUtf16 + LengthInUtf16).ByteOffset - IndexInBytes)
        : throw new InvalidOperationException("The split is not aligned to valid UTF-8 byte boundaries.");

    public int IndexInUtf16 { get; }

    public int LengthInUtf16 { get; }

    public ReadOnlySpan<byte> GetValueBytes()
    {
        return IsByteAligned
            ? _input.Slice(IndexInBytes, LengthInBytes)
            : throw new InvalidOperationException("The split is not aligned to valid UTF-8 byte boundaries.");
    }

    public string GetValueString()
    {
        return _decoded is null
            ? Encoding.UTF8.GetString(GetValueBytes())
            : _decoded.Substring(IndexInUtf16, LengthInUtf16);
    }

    private Utf16Boundary ResolveBoundary(int utf16Offset)
    {
        return _boundaryMap?.Resolve(utf16Offset) ?? Utf8Utf16BoundaryResolver.ResolveBoundary(_input, utf16Offset);
    }
}
