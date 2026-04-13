namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8ConfirmationKind : byte
{
    None = 0,
    BoundaryRequirements = 1,
    BoundaryAndTrailingLiteral = 2,
    FallbackVerifier = 3,
}

internal readonly struct Utf8ConfirmationPlan
{
    public Utf8ConfirmationPlan(Utf8ConfirmationKind kind)
    {
        Kind = kind;
    }

    public Utf8ConfirmationKind Kind { get; }

    public bool HasValue => Kind != Utf8ConfirmationKind.None;
}
