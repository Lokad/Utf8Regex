namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8SearchEngineKind : byte
{
    None = 0,
    PreparedSearcher = 1,
    StructuralSearch = 2,
    StructuralSearchSet = 3,
    StructuralIdentifierFamily = 4,
    OrderedLiteralWindow = 5,
    StructuralDeterministicAutomaton = 6,
}

internal readonly struct Utf8SearchEnginePlan
{
    public Utf8SearchEnginePlan(
        Utf8SearchEngineKind kind,
        Utf8SearchSemantics semantics,
        PreparedSearcher preparedSearcher = default,
        Utf8StructuralSearchPlan structuralSearchPlan = default,
        Utf8StructuralSearchPlan[]? structuralSearchPlans = null,
        Utf8SearchPortfolioKind portfolioKind = Utf8SearchPortfolioKind.None)
    {
        Kind = kind;
        Semantics = semantics;
        PreparedSearcher = preparedSearcher;
        StructuralSearchPlan = structuralSearchPlan;
        StructuralSearchPlans = structuralSearchPlans;
        PortfolioKind = portfolioKind;
    }

    public Utf8SearchEngineKind Kind { get; }

    public Utf8SearchSemantics Semantics { get; }

    public PreparedSearcher PreparedSearcher { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8StructuralSearchPlan[]? StructuralSearchPlans { get; }

    public Utf8SearchPortfolioKind PortfolioKind { get; }

    public bool HasValue => Kind != Utf8SearchEngineKind.None;
}
