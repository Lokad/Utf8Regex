namespace Lokad.Utf8Regex.Internal.Input;

internal readonly ref struct Utf8ReverseCursor
{
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8BoundaryMap _boundaryMap;

    public Utf8ReverseCursor(ReadOnlySpan<byte> input, Utf8BoundaryMap boundaryMap, int utf16Offset)
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

    public ReadOnlySpan<byte> Consumed => _input[..ByteOffset];

    public Utf8ReverseCursor MoveTo(int utf16Offset) => new(_input, _boundaryMap, utf16Offset);

    public bool TryRetreatCodeUnit(out Utf8ReverseCursor previous)
    {
        if (!_boundaryMap.TryRetreatCodeUnit(Utf16Offset, out var previousBoundary))
        {
            previous = default;
            return false;
        }

        previous = new Utf8ReverseCursor(_input, _boundaryMap, previousBoundary.Utf16Offset);
        return true;
    }

    public bool TryRetreatScalar(out Utf8ReverseCursor previous)
    {
        if (!_boundaryMap.TryRetreatScalar(Utf16Offset, out var previousBoundary))
        {
            previous = default;
            return false;
        }

        previous = new Utf8ReverseCursor(_input, _boundaryMap, previousBoundary.Utf16Offset);
        return true;
    }
}
