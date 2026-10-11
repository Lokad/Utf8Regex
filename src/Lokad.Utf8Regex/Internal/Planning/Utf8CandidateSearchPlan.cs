using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8CandidateSearchKind : byte
{
    None = 0,
    PreparedSearcher = 1,
    StructuralSearch = 2,
    StructuralSearchSet = 3,
}

internal readonly partial struct Utf8CandidateSearchPlan
{
    private Utf8CandidateSearchPlan(
        Utf8CandidateSearchKind kind,
        Utf8SearchSemantics semantics,
        PreparedSearcher preparedSearcher,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8StructuralSearchPlan[]? structuralSearchPlans,
        Utf8SearchPortfolioKind portfolioKind)
    {
        Kind = kind;
        Semantics = semantics;
        PreparedSearcher = preparedSearcher;
        StructuralSearchPlan = structuralSearchPlan;
        StructuralSearchPlans = structuralSearchPlans;
        PortfolioKind = portfolioKind;
    }

    public Utf8CandidateSearchKind Kind { get; }

    public Utf8SearchSemantics Semantics { get; }

    public PreparedSearcher PreparedSearcher { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8StructuralSearchPlan[]? StructuralSearchPlans { get; }

    public Utf8SearchPortfolioKind PortfolioKind { get; }

    public bool HasValue => Kind != Utf8CandidateSearchKind.None;

    public static Utf8CandidateSearchPlan FromPreparedSearcher(
        PreparedSearcher searcher,
        Utf8SearchSemantics semantics,
        Utf8SearchPortfolioKind portfolioKind)
    {
        if (!searcher.HasValue)
        {
            return default;
        }

        return new Utf8CandidateSearchPlan(
            Utf8CandidateSearchKind.PreparedSearcher,
            semantics,
            searcher,
            default,
            null,
            portfolioKind);
    }

    public static Utf8CandidateSearchPlan FromStructuralSearch(
        Utf8StructuralSearchPlan plan,
        Utf8SearchSemantics semantics,
        Utf8SearchPortfolioKind portfolioKind)
    {
        if (!plan.HasValue)
        {
            return default;
        }

        return new Utf8CandidateSearchPlan(
            Utf8CandidateSearchKind.StructuralSearch,
            semantics,
            default,
            plan,
            null,
            portfolioKind);
    }

    public static Utf8CandidateSearchPlan FromStructuralSearchSet(
        Utf8StructuralSearchPlan[]? plans,
        Utf8SearchSemantics semantics)
    {
        if (plans is not { Length: > 0 })
        {
            return default;
        }

        return new Utf8CandidateSearchPlan(
            Utf8CandidateSearchKind.StructuralSearchSet,
            semantics,
            default,
            default,
            plans,
            Utf8SearchPortfolioKind.None);
    }

}
