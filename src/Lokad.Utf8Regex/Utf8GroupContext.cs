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

    internal Utf8GroupContext(ReadOnlySpan<byte> input, string decoded, Group? group, Utf8BoundaryMap boundaryMap)
    {
        _input = input;
        _decoded = decoded;
        _group = group;
        _boundaryMap = boundaryMap;
    }

    /// <summary>Gets whether the group participated in the match.</summary>
    public bool Success => _group?.Success ?? false;

    /// <summary>Gets the successful group's start as a zero-based UTF-16 code-unit offset, or zero otherwise.</summary>
    public int IndexInUtf16 => _group is { Success: true } group ? group.Index : 0;

    /// <summary>Gets the successful group's length in UTF-16 code units, or zero otherwise.</summary>
    public int LengthInUtf16 => _group is { Success: true } group ? group.Length : 0;

    /// <summary>Gets the number of captures retained for the group.</summary>
    public int CaptureCount => _group?.Captures.Count ?? 0;

    /// <summary>Gets whether both group boundaries map to UTF-8 scalar boundaries.</summary>
    public bool IsByteAligned
    {
        get
        {
            if (_group is null)
            {
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
        return _group?.Value ?? string.Empty;
    }

    /// <summary>Gets a capture by its zero-based index within this group.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The index is negative, no group exists, or the index is outside the capture collection.</exception>
    public Utf8CaptureContext GetCapture(int index)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(index);
        if (_group is null || index >= _group.Captures.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return new Utf8CaptureContext(_input, _decoded, _group.Captures[index], _boundaryMap);
    }

    private int GetIndexInBytes()
    {
        return ResolveBoundary(_group?.Index ?? 0).ByteOffset;
    }

    private int GetLengthInBytes()
    {
        var group = _group;
        if (group is null)
        {
            return 0;
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
