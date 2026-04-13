namespace Lokad.Utf8Regex;

public readonly ref struct Utf8GroupContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly string _decoded;
    private readonly Group? _group;
    private readonly Utf8BoundaryMap? _boundaryMap;

    internal Utf8GroupContext(ReadOnlySpan<byte> input, string decoded, Group? group, Utf8BoundaryMap? boundaryMap = null)
    {
        _input = input;
        _decoded = decoded;
        _group = group;
        _boundaryMap = boundaryMap;
    }

    public bool Success => _group?.Success ?? false;

    public int IndexInUtf16 => Success ? _group!.Index : 0;

    public int LengthInUtf16 => Success ? _group!.Length : 0;

    public int CaptureCount => _group?.Captures.Count ?? 0;

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

    public int IndexInBytes => IsByteAligned
        ? GetIndexInBytes()
        : throw new InvalidOperationException("The group is not aligned to valid UTF-8 byte boundaries.");

    public int LengthInBytes => IsByteAligned
        ? GetLengthInBytes()
        : throw new InvalidOperationException("The group is not aligned to valid UTF-8 byte boundaries.");

    public ReadOnlySpan<byte> GetValueBytes()
    {
        return IsByteAligned
            ? _input.Slice(IndexInBytes, LengthInBytes)
            : throw new InvalidOperationException("The group is not aligned to valid UTF-8 byte boundaries.");
    }

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
        return _group?.Value ?? string.Empty;
    }

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
        return _boundaryMap?.Resolve(utf16Offset) ?? Utf8Utf16BoundaryResolver.ResolveBoundary(_input, utf16Offset);
    }
}
