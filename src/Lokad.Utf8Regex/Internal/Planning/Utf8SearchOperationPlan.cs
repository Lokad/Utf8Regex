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

/// <summary>
/// Immutable execution choices for one regex operation. Search payloads remain
/// in the canonical search plan; only confirmation, projection and observability
/// needed by runtime consumers are carried here.
/// </summary>
internal readonly struct Utf8SearchOperationPlan
{
    private Utf8SearchOperationPlan(
        Utf8SearchOperationKind kind,
        Utf8ConfirmationPlan confirmation,
        Utf8ProjectionPlan projection,
        Utf8SearchObservabilityKind observabilityKind)
    {
        Kind = kind;
        Confirmation = confirmation;
        Projection = projection;
        ObservabilityKind = observabilityKind;
    }

    public Utf8SearchOperationKind Kind { get; }

    public Utf8ConfirmationPlan Confirmation { get; }

    public Utf8ProjectionPlan Projection { get; }

    public Utf8SearchObservabilityKind ObservabilityKind { get; }

    public bool HasValue => Kind != Utf8SearchOperationKind.None;

    public static Utf8SearchOperationPlan Create(
        Utf8SearchOperationKind kind,
        Utf8ConfirmationPlan confirmation,
        Utf8ProjectionPlan projection,
        Utf8SearchObservabilityKind observabilityKind)
    {
        if (kind == Utf8SearchOperationKind.None)
        {
            return default;
        }

        return new Utf8SearchOperationPlan(
            kind,
            confirmation,
            projection,
            observabilityKind);
    }

    public Utf8SearchOperationPlan WithProjection(Utf8ProjectionPlan projection)
        => HasValue
            ? new Utf8SearchOperationPlan(
                Kind,
                Confirmation,
                projection,
                ObservabilityKind)
            : default;
}
