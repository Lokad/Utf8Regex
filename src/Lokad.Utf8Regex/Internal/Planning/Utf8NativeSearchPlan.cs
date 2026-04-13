namespace Lokad.Utf8Regex.Internal.Planning;

internal readonly struct Utf8NativeSearchPlan
{
    public Utf8NativeSearchPlan(
        PreparedSearcher preparedSearcher,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8SearchPortfolioKind portfolioKind)
    {
        PreparedSearcher = preparedSearcher;
        StructuralSearchPlan = structuralSearchPlan;
        PortfolioKind = portfolioKind;
    }

    public PreparedSearcher PreparedSearcher { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8SearchPortfolioKind PortfolioKind { get; }

    public Utf8SearchEnginePlan CandidateEngine =>
        HasPreparedSearcher
            ? new Utf8SearchEnginePlan(
                Utf8SearchEngineKind.PreparedSearcher,
                Utf8SearchSemantics.CandidateScan,
                preparedSearcher: PreparedSearcher,
                portfolioKind: PortfolioKind)
            : HasStructuralCandidates
                ? new Utf8SearchEnginePlan(
                    Utf8SearchEngineKind.StructuralSearch,
                    Utf8SearchSemantics.CandidateScan,
                    structuralSearchPlan: StructuralSearchPlan,
                    portfolioKind: PortfolioKind)
                : default;

    public bool HasPreparedSearcher => PreparedSearcher.HasValue;

    public bool HasStructuralCandidates => StructuralSearchPlan.HasValue;
}
