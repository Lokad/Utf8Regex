using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.Planning;

internal readonly struct Utf8PrefilterPlan
{
    public Utf8PrefilterPlan(
        PreparedSearcher primarySearcher,
        PreparedSearcher secondarySearcher,
        Utf8StructuralSearchPlan[]? windowPlans)
    {
        PrimarySearcher = primarySearcher;
        SecondarySearcher = secondarySearcher;
        WindowPlans = windowPlans;
    }

    public PreparedSearcher PrimarySearcher { get; }

    public PreparedSearcher SecondarySearcher { get; }

    public Utf8StructuralSearchPlan[]? WindowPlans { get; }

    public Utf8CandidateSearchPlan PrimarySource =>
        Utf8CandidateSearchPlan.FromPreparedSearcher(
            PrimarySearcher,
            Utf8SearchSemantics.FirstMatch,
            Utf8SearchPortfolioKind.None);

    public Utf8CandidateSearchPlan SecondarySource =>
        Utf8CandidateSearchPlan.FromPreparedSearcher(
            SecondarySearcher,
            Utf8SearchSemantics.FirstMatch,
            Utf8SearchPortfolioKind.None);

    public Utf8CandidateSearchPlan WindowSource =>
        Utf8CandidateSearchPlan.FromStructuralSearchSet(
            WindowPlans,
            Utf8SearchSemantics.FirstMatch);

    public bool HasValue =>
        PrimarySearcher.HasValue ||
        SecondarySearcher.HasValue ||
        WindowPlans is { Length: > 0 };

}
