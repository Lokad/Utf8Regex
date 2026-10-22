using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Planning;

internal enum Utf8CandidateSearchKind : byte
{
    None = 0,
    PreparedSearcher = 1,
    StructuralSearchSet = 3,
}

internal readonly struct Utf8CandidateSearchPlan
{
    private Utf8CandidateSearchPlan(Utf8CandidateSearchKind kind,
        PreparedSearcher preparedSearcher, Utf8StructuralSearchPlan[]? structuralSearchPlans)
    {
        Kind = kind;
        PreparedSearcher = preparedSearcher;
        StructuralSearchPlans = structuralSearchPlans;
    }

    public Utf8CandidateSearchKind Kind { get; }

    public PreparedSearcher PreparedSearcher { get; }

    public Utf8StructuralSearchPlan[]? StructuralSearchPlans { get; }

    public bool HasValue => Kind != Utf8CandidateSearchKind.None;

    public static Utf8CandidateSearchPlan FromPreparedSearcher(PreparedSearcher searcher)
        => searcher.HasValue
            ? new(Utf8CandidateSearchKind.PreparedSearcher, searcher, null)
            : default;

    public static Utf8CandidateSearchPlan FromStructuralSearchSet(Utf8StructuralSearchPlan[]? plans)
        => plans is { Length: > 0 }
            ? new(Utf8CandidateSearchKind.StructuralSearchSet, default, plans)
            : default;
}
