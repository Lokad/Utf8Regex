using Lokad.Utf8Regex.Internal.Input;
using System.Text;

namespace Lokad.Utf8Regex;

/// <summary>Provides a stack-only view of one split segment over the original UTF-8 input.</summary>
/// <remarks>The view and any spans obtained from it must not outlive the input supplied to the split operation.</remarks>
public readonly ref struct Utf8ValueSplit
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly string? _decoded;
    private readonly Utf8BoundaryMap? _boundaryMap;
    private readonly int _indexInBytes;
    private readonly int _lengthInBytes;
    private readonly bool _hasDirectByteRange;

    internal Utf8ValueSplit(ReadOnlySpan<byte> input, string? decoded, int indexInUtf16, int lengthInUtf16, Utf8BoundaryMap boundaryMap)
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

    /// <summary>Gets whether both UTF-16 segment boundaries map to UTF-8 scalar boundaries.</summary>
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

    /// <summary>Gets the segment start as a zero-based byte offset in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The segment splits a UTF-8 scalar.</exception>
    public int IndexInBytes => IsByteAligned
        ? (_hasDirectByteRange ? _indexInBytes : ResolveBoundary(IndexInUtf16).ByteOffset)
        : throw new InvalidOperationException("The split is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Gets the segment length in bytes in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The segment splits a UTF-8 scalar.</exception>
    public int LengthInBytes => IsByteAligned
        ? (_hasDirectByteRange ? _lengthInBytes : ResolveBoundary(IndexInUtf16 + LengthInUtf16).ByteOffset - IndexInBytes)
        : throw new InvalidOperationException("The split is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Gets the segment start as a zero-based UTF-16 code-unit offset.</summary>
    public int IndexInUtf16 { get; }

    /// <summary>Gets the segment length in UTF-16 code units.</summary>
    public int LengthInUtf16 { get; }

    /// <summary>Gets the segment bytes from the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The segment splits a UTF-8 scalar.</exception>
    public ReadOnlySpan<byte> GetValueBytes()
    {
        return IsByteAligned
            ? _input.Slice(IndexInBytes, LengthInBytes)
            : throw new InvalidOperationException("The split is not aligned to valid UTF-8 byte boundaries.");
    }

    /// <summary>Gets the segment value decoded as a UTF-16 string.</summary>
    public string GetValueString()
    {
        return _decoded is null
            ? Encoding.UTF8.GetString(GetValueBytes())
            : _decoded.Substring(IndexInUtf16, LengthInUtf16);
    }

    private Utf16Boundary ResolveBoundary(int utf16Offset)
    {
        return _boundaryMap is { } map
            ? map.Resolve(utf16Offset)
            : throw new InvalidOperationException("UTF-16 projection was not prepared for this split.");
    }
}
