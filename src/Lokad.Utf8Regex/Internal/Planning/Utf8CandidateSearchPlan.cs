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

internal readonly struct Utf8CandidateSearchPlan
{
    private Utf8CandidateSearchPlan(
        Utf8CandidateSearchKind kind,
        PreparedSearcher preparedSearcher,
        Utf8StructuralSearchPlan structuralSearchPlan,
        Utf8StructuralSearchPlan[]? structuralSearchPlans,
        Utf8SearchPortfolioKind portfolioKind)
    {
        Kind = kind;
        PreparedSearcher = preparedSearcher;
        StructuralSearchPlan = structuralSearchPlan;
        StructuralSearchPlans = structuralSearchPlans;
        PortfolioKind = portfolioKind;
    }

    public Utf8CandidateSearchKind Kind { get; }

    public PreparedSearcher PreparedSearcher { get; }

    public Utf8StructuralSearchPlan StructuralSearchPlan { get; }

    public Utf8StructuralSearchPlan[]? StructuralSearchPlans { get; }

    public Utf8SearchPortfolioKind PortfolioKind { get; }

    public bool HasValue => Kind != Utf8CandidateSearchKind.None;

    public static Utf8CandidateSearchPlan FromPreparedSearcher(
        PreparedSearcher searcher,
        Utf8SearchPortfolioKind portfolioKind)
    {
        if (!searcher.HasValue)
        {
            return default;
        }

        return new Utf8CandidateSearchPlan(
            Utf8CandidateSearchKind.PreparedSearcher,
            searcher,
            default,
            null,
            portfolioKind);
    }

    public static Utf8CandidateSearchPlan FromStructuralSearch(
        Utf8StructuralSearchPlan plan,
        Utf8SearchPortfolioKind portfolioKind)
    {
        if (!plan.HasValue)
        {
            return default;
        }

        return new Utf8CandidateSearchPlan(
            Utf8CandidateSearchKind.StructuralSearch,
            default,
            plan,
            null,
            portfolioKind);
    }

    public static Utf8CandidateSearchPlan FromStructuralSearchSet(
        Utf8StructuralSearchPlan[]? plans)
    {
        if (plans is not { Length: > 0 })
        {
            return default;
        }

        return new Utf8CandidateSearchPlan(
            Utf8CandidateSearchKind.StructuralSearchSet,
            default,
            default,
            plans,
            Utf8SearchPortfolioKind.None);
    }

}
