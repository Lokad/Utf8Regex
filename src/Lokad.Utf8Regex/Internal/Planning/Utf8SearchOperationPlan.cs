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
/// Candidate discovery, prefiltering and confirmation shared by operations
/// using the prepared search sources. A runtime can retain one copy.
/// </summary>
internal readonly struct Utf8SearchOperationPlan
{
    private Utf8SearchOperationPlan(
        Utf8SearchOperationKind kind, Utf8CandidateSearchPlan candidateSource, Utf8CandidateSearchPlan prefilterSource, Utf8ConfirmationPlan confirmation, Utf8SearchObservabilityKind observabilityKind)
    {
        Kind = kind;
        CandidateSource = candidateSource;
        PrefilterSource = prefilterSource;
        Confirmation = confirmation;
        ObservabilityKind = observabilityKind;
    }

    public Utf8SearchOperationKind Kind { get; }

    public Utf8CandidateSearchPlan CandidateSource { get; }

    public Utf8CandidateSearchPlan PrefilterSource { get; }

    public Utf8ConfirmationPlan Confirmation { get; }

    public Utf8SearchObservabilityKind ObservabilityKind { get; }

    public bool HasValue => Kind != Utf8SearchOperationKind.None;

    public static Utf8SearchOperationPlan Create(
        Utf8SearchOperationKind kind, Utf8CandidateSearchPlan candidateSource, Utf8CandidateSearchPlan prefilterSource, Utf8ConfirmationPlan confirmation, Utf8SearchObservabilityKind observabilityKind)
    {
        if (kind == Utf8SearchOperationKind.None)
        {
            return default;
        }

        return new Utf8SearchOperationPlan(
            kind,
            candidateSource,
            prefilterSource,
            confirmation,
            observabilityKind);
    }
}
