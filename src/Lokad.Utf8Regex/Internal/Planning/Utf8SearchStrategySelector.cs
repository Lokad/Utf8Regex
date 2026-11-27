using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8SearchStrategySelector
{
    public static Utf8SearchOperationPlan Create(
        in Utf8SearchPlan plan, Utf8ConfirmationPlan confirmation)
    {
        if (!plan.HasPreparedSearcher)
        {
            return default;
        }

        var requiresConfirmation = plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement;
        var kind = plan.HasRequiredPrefilter
            ? requiresConfirmation
                ? Utf8SearchOperationKind.PrefilterThenConfirm
                : Utf8SearchOperationKind.PrefilterThenSearch
            : requiresConfirmation
                ? Utf8SearchOperationKind.SearchThenConfirm
                : IsLargeAutomatonHybridCandidate(plan)
                    ? Utf8SearchOperationKind.HybridSearch
                    : Utf8SearchOperationKind.DirectSearch;

        return new Utf8SearchOperationPlan(kind, confirmation,
            kind == Utf8SearchOperationKind.HybridSearch
                ? Utf8SearchObservabilityKind.Effectiveness
                : Utf8SearchObservabilityKind.CandidateCounts);
    }

    private static bool IsLargeAutomatonHybridCandidate(in Utf8SearchPlan plan)
    {
        return plan.MultiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactAutomaton &&
            !plan.HasBoundaryRequirements &&
            !plan.HasTrailingLiteralRequirement;
    }
}
