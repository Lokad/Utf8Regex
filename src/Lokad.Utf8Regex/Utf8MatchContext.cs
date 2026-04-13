namespace Lokad.Utf8Regex;

public readonly ref struct Utf8MatchContext
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly string _decoded;
    private readonly Match? _match;
    private readonly Utf8BoundaryMap? _boundaryMap;
    private readonly string[]? _groupNames;

    internal Utf8MatchContext(ReadOnlySpan<byte> input, string decoded, Match? match, Utf8BoundaryMap? boundaryMap = null, string[]? groupNames = null)
    {
        _input = input;
        _decoded = decoded;
        _match = match;
        _boundaryMap = boundaryMap;
        _groupNames = groupNames;
    }

    public bool Success => _match?.Success ?? false;

    public int IndexInUtf16 => Success ? _match!.Index : 0;

    public int LengthInUtf16 => Success ? _match!.Length : 0;

    public bool IsByteAligned
    {
        get
        {
            if (!Success)
            {
                return true;
            }

            var start = ResolveBoundary(_match!.Index);
            var end = ResolveBoundary(_match.Index + _match.Length);
            return start.IsScalarBoundary && end.IsScalarBoundary;
        }
    }

    public int IndexInBytes => IsByteAligned
        ? ResolveBoundary(IndexInUtf16).ByteOffset
        : throw new InvalidOperationException("The match is not aligned to valid UTF-8 byte boundaries.");

    public int LengthInBytes => IsByteAligned
        ? ResolveBoundary(IndexInUtf16 + LengthInUtf16).ByteOffset - IndexInBytes
        : throw new InvalidOperationException("The match is not aligned to valid UTF-8 byte boundaries.");

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

    public int GroupCount => _match?.Groups.Count ?? 0;

    public Utf8GroupContext GetGroup(int number)
    {
        if (_match is null)
        {
            throw new InvalidOperationException("No match is available.");
        }

        return new Utf8GroupContext(_input, _decoded, _match.Groups[number], _boundaryMap);
    }

    public Utf8GroupContext GetGroup(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_match is null)
        {
            throw new InvalidOperationException("No match is available.");
        }

        return new Utf8GroupContext(_input, _decoded, _match.Groups[name], _boundaryMap);
    }

    public bool TryGetGroup(int number, out Utf8GroupContext group)
    {
        if (_match is null || number < 0 || number >= _match.Groups.Count)
        {
            group = default;
            return false;
        }

        group = new Utf8GroupContext(_input, _decoded, _match.Groups[number], _boundaryMap);
        return true;
    }

    public bool TryGetGroup(string name, out Utf8GroupContext group)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (_match is null || _groupNames is null || Array.IndexOf(_groupNames, name) < 0)
        {
            group = default;
            return false;
        }

        group = new Utf8GroupContext(_input, _decoded, _match.Groups[name], _boundaryMap);
        return true;
    }

    public string GetValueString()
    {
        return Success ? _match!.Value : string.Empty;
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

    private Utf16Boundary ResolveBoundary(int utf16Offset)
    {
        return _boundaryMap?.Resolve(utf16Offset) ?? Utf8Utf16BoundaryResolver.ResolveBoundary(_input, utf16Offset);
    }
}
