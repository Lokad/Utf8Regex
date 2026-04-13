namespace Lokad.Utf8Regex.Internal.Input;

internal readonly ref struct Utf8Cursor
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8BoundaryMap _boundaryMap;

    public Utf8Cursor(ReadOnlySpan<byte> input, Utf8BoundaryMap boundaryMap, int utf16Offset = 0)
    {
        _input = input;
        _boundaryMap = boundaryMap;
        Boundary = boundaryMap.Resolve(utf16Offset);
    }

    public Utf16Boundary Boundary { get; }

    public int Utf16Offset => Boundary.Utf16Offset;

    public int ByteOffset => Boundary.ByteOffset;

    public bool IsScalarBoundary => Boundary.IsScalarBoundary;

    public bool IsAtStart => Utf16Offset == 0;

    public bool IsAtEnd => Utf16Offset == _boundaryMap.Utf16Length;

    public ReadOnlySpan<byte> Remaining => _input[ByteOffset..];

    public Utf8Cursor MoveTo(int utf16Offset) => new(_input, _boundaryMap, utf16Offset);

    public bool TryAdvanceCodeUnit(out Utf8Cursor next)
    {
        if (!_boundaryMap.TryAdvanceCodeUnit(Utf16Offset, out var nextBoundary))
        {
            next = default;
            return false;
        }

        next = new Utf8Cursor(_input, _boundaryMap, nextBoundary.Utf16Offset);
        return true;
    }

    public bool TryAdvanceScalar(out Utf8Cursor next)
    {
        if (!_boundaryMap.TryAdvanceScalar(Utf16Offset, out var nextBoundary))
        {
            next = default;
            return false;
        }

        next = new Utf8Cursor(_input, _boundaryMap, nextBoundary.Utf16Offset);
        return true;
    }
}
