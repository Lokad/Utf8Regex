namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8SearchMetaStrategyKind : byte
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

internal readonly struct Utf8SearchMetaStrategyPlan
{
    public Utf8SearchMetaStrategyPlan(
        Utf8SearchMetaStrategyKind kind,
        Utf8SearchSemantics semantics,
        Utf8SearchEnginePlan candidateEngine = default,
        Utf8SearchEnginePlan prefilterEngine = default,
        Utf8SearchObservabilityKind observabilityKind = Utf8SearchObservabilityKind.None)
    {
        Kind = kind;
        Semantics = semantics;
        CandidateEngine = candidateEngine;
        PrefilterEngine = prefilterEngine;
        ObservabilityKind = observabilityKind;
    }

    public Utf8SearchMetaStrategyKind Kind { get; }

    public Utf8SearchSemantics Semantics { get; }

    public Utf8SearchEnginePlan CandidateEngine { get; }

    public Utf8SearchEnginePlan PrefilterEngine { get; }

    public Utf8SearchObservabilityKind ObservabilityKind { get; }

    public bool HasValue => Kind != Utf8SearchMetaStrategyKind.None;
}
