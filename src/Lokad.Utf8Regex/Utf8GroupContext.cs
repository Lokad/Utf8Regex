using Lokad.Utf8Regex.Internal.Input;
namespace Lokad.Utf8Regex;

/// <summary>Provides a stack-only view of one regex group over the original UTF-8 input.</summary>
/// <remarks>The view and any spans obtained from it must not outlive the input supplied to the matching operation.</remarks>
public readonly ref struct Utf8GroupContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly string _decoded;
    private readonly Group? _group;
    private readonly Utf8BoundaryMap _boundaryMap;
    private readonly bool _ownedHasValue;
    private readonly int _ownedIndex;
    private readonly int _ownedLength;
    private readonly int[]? _ownedCaptureStarts;
    private readonly int[]? _ownedCaptureLengths;

    internal Utf8GroupContext(ReadOnlySpan<byte> input, string decoded, Group? group, Utf8BoundaryMap boundaryMap)
    {
        _input = input;
        _decoded = decoded;
        _group = group;
        _boundaryMap = boundaryMap;
    }

    internal Utf8GroupContext(ReadOnlySpan<byte> input, string decoded, Utf8BoundaryMap boundaryMap, int indexInUtf16, int lengthInUtf16)
    {
        _input = input;
        _decoded = decoded;
        _boundaryMap = boundaryMap;
        _ownedHasValue = true;
        _ownedIndex = indexInUtf16;
        _ownedLength = lengthInUtf16;
    }

    internal Utf8GroupContext(
        ReadOnlySpan<byte> input,
        string decoded,
        Utf8BoundaryMap boundaryMap,
        int indexInUtf16,
        int lengthInUtf16,
        int[] captureStarts,
        int[] captureLengths)
        : this(input, decoded, boundaryMap, indexInUtf16, lengthInUtf16)
    {
        _ownedCaptureStarts = captureStarts;
        _ownedCaptureLengths = captureLengths;
    }

    /// <summary>Gets whether the group participated in the match.</summary>
    public bool Success => _group?.Success ?? _ownedHasValue;

    /// <summary>Gets the successful group's start as a zero-based UTF-16 code-unit offset, or zero otherwise.</summary>
    public int IndexInUtf16 => _group is { Success: true } group ? group.Index : (_ownedHasValue ? _ownedIndex : 0);

    /// <summary>Gets the successful group's length in UTF-16 code units, or zero otherwise.</summary>
    public int LengthInUtf16 => _group is { Success: true } group ? group.Length : (_ownedHasValue ? _ownedLength : 0);

    /// <summary>Gets the number of captures retained for the group.</summary>
    public int CaptureCount => _group?.Captures.Count ?? _ownedCaptureStarts?.Length ?? (_ownedHasValue ? 1 : 0);

    /// <summary>Gets whether both group boundaries map to UTF-8 scalar boundaries.</summary>
    public bool IsByteAligned
    {
        get
        {
            if (_group is null)
            {
                if (_ownedHasValue)
                {
                    var ownedStart = ResolveBoundary(_ownedIndex);
                    var ownedEnd = ResolveBoundary(_ownedIndex + _ownedLength);
                    return ownedStart.IsScalarBoundary && ownedEnd.IsScalarBoundary;
                }

                return true;
            }

            var start = ResolveBoundary(_group.Index);
            var end = ResolveBoundary(_group.Index + _group.Length);
            return start.IsScalarBoundary && end.IsScalarBoundary;
        }
    }

    /// <summary>Gets the group start as a zero-based byte offset in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The group splits a UTF-8 scalar.</exception>
    public int IndexInBytes => IsByteAligned
        ? GetIndexInBytes()
        : throw new InvalidOperationException("The group is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Gets the group length in bytes in the original UTF-8 input.</summary>
    /// <exception cref="InvalidOperationException">The group splits a UTF-8 scalar.</exception>
    public int LengthInBytes => IsByteAligned
        ? GetLengthInBytes()
        : throw new InvalidOperationException("The group is not aligned to valid UTF-8 byte boundaries.");

    /// <summary>Gets the group value from the original UTF-8 input.</summary>
    /// <returns>An empty span when the group is absent.</returns>
    /// <exception cref="InvalidOperationException">The group splits a UTF-8 scalar.</exception>
    public ReadOnlySpan<byte> GetValueBytes()
    {
        return IsByteAligned
            ? _input.Slice(IndexInBytes, LengthInBytes)
            : throw new InvalidOperationException("The group is not aligned to valid UTF-8 byte boundaries.");
    }

    /// <summary>Attempts to map the group range to the original UTF-8 input.</summary>
    /// <param name="indexInBytes">The zero-based byte offset, or zero when the range is not byte-aligned.</param>
    /// <param name="lengthInBytes">The byte length, or zero when the range is not byte-aligned.</param>
    /// <returns><see langword="true"/> when both group boundaries are UTF-8 scalar boundaries.</returns>
    public bool TryGetByteRange(out int indexInBytes, out int lengthInBytes)
    {
        if (!IsByteAligned)
        {
            indexInBytes = 0;
            lengthInBytes = 0;
            return false;
        }

        indexInBytes = GetIndexInBytes();
        lengthInBytes = GetLengthInBytes();
        return true;
    }

    /// <summary>Attempts to get the group value from the original UTF-8 input.</summary>
    /// <param name="valueBytes">The group bytes, or a default span when the range is not byte-aligned.</param>
    /// <returns><see langword="true"/> when the group can be projected to bytes.</returns>
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

    /// <summary>Gets the group value as a UTF-16 string, or an empty string when the group is absent.</summary>
    public string GetValueString()
    {
        return _group?.Value ?? (_ownedHasValue ? _decoded.Substring(_ownedIndex, _ownedLength) : string.Empty);
    }

    /// <summary>Gets a capture by its zero-based index within this group.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative, no group exists, or the index is outside the capture collection.</exception>
    public Utf8CaptureContext GetCapture(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (_group is null)
        {
            if (_ownedCaptureStarts is { } starts && _ownedCaptureLengths is { } lengths)
            {
                if ((uint)index >= (uint)starts.Length)
                {
                    throw new ArgumentOutOfRangeException(nameof(index));
                }

                return new Utf8CaptureContext(_input, _decoded, _boundaryMap, starts[index], lengths[index]);
            }

            if (!_ownedHasValue || index > 0)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new Utf8CaptureContext(_input, _decoded, _boundaryMap, _ownedIndex, _ownedLength);
        }

        if (index >= _group.Captures.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return new Utf8CaptureContext(_input, _decoded, _group.Captures[index], _boundaryMap);
    }

    private int GetIndexInBytes()
    {
        return ResolveBoundary(_group?.Index ?? (_ownedHasValue ? _ownedIndex : 0)).ByteOffset;
    }

    private int GetLengthInBytes()
    {
        var group = _group;
        if (group is null)
        {
            if (!_ownedHasValue)
            {
                return 0;
            }

            var ownedStart = ResolveBoundary(_ownedIndex).ByteOffset;
            var ownedEnd = ResolveBoundary(_ownedIndex + _ownedLength).ByteOffset;
            return ownedEnd - ownedStart;
        }

        var start = ResolveBoundary(group.Index).ByteOffset;
        var end = ResolveBoundary(group.Index + group.Length).ByteOffset;
        return end - start;
    }

    private Utf16Boundary ResolveBoundary(int utf16Offset)
    {
        return _boundaryMap.Resolve(utf16Offset);
    }
}
