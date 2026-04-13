namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8ProjectionKind : byte
{
    None = 0,
    ByteOnly = 1,
    Utf16BoundaryMap = 2,
    Utf16Incremental = 3,
}

internal readonly struct Utf8ProjectionPlan
{
    public Utf8ProjectionPlan(Utf8ProjectionKind kind)
    {
        Kind = kind;
    }

    public Utf8ProjectionKind Kind { get; }

    public bool HasValue => Kind != Utf8ProjectionKind.None;
}
