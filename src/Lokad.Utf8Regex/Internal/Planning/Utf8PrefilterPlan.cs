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

    public Utf8SearchEnginePlan PrimaryEngine =>
        PrimarySearcher.HasValue
            ? new Utf8SearchEnginePlan(
                Utf8SearchEngineKind.PreparedSearcher,
                Utf8SearchSemantics.FirstMatch,
                preparedSearcher: PrimarySearcher)
            : default;

    public Utf8SearchEnginePlan SecondaryEngine =>
        SecondarySearcher.HasValue
            ? new Utf8SearchEnginePlan(
                Utf8SearchEngineKind.PreparedSearcher,
                Utf8SearchSemantics.FirstMatch,
                preparedSearcher: SecondarySearcher)
            : default;

    public Utf8SearchEnginePlan WindowEngine =>
        WindowPlans is { Length: > 0 } windowPlans
            ? new Utf8SearchEnginePlan(
                Utf8SearchEngineKind.StructuralSearchSet,
                Utf8SearchSemantics.FirstMatch,
                structuralSearchPlans: windowPlans)
            : default;

    public bool HasValue =>
        PrimarySearcher.HasValue ||
        SecondarySearcher.HasValue ||
        WindowPlans is { Length: > 0 };

}
