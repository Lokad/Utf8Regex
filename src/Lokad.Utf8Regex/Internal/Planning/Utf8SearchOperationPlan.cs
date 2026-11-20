namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8SearchOperationKind : byte
{
    None = 0,
    DirectSearch = 1,
    SearchThenConfirm = 2,
    PrefilterThenSearch = 3,
    PrefilterThenConfirm = 4,
    HybridSearch = 5,
}

internal enum Utf8SearchObservabilityKind : byte
{
    None = 0,
    CandidateCounts = 1,
    Effectiveness = 2,
}

/// <summary>Search strategy and confirmation for the prepared plan.</summary>
internal readonly struct Utf8SearchOperationPlan
{
    public Utf8SearchOperationPlan(
        Utf8SearchOperationKind kind,
        Utf8ConfirmationPlan confirmation,
        Utf8SearchObservabilityKind observabilityKind)
    {
        Kind = kind;
        Confirmation = confirmation;
        ObservabilityKind = observabilityKind;
    }

    public Utf8SearchOperationKind Kind { get; }

    public Utf8ConfirmationPlan Confirmation { get; }

    public Utf8SearchObservabilityKind ObservabilityKind { get; }

    public bool HasValue => Kind != Utf8SearchOperationKind.None;
}
