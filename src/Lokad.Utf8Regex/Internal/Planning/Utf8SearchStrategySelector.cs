using Lokad.Utf8Regex.Internal.Search;
namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8SearchStrategySelector
{
    public static Utf8SearchOperationPlan Create(
        in Utf8SearchPlan plan,
        Utf8ConfirmationPlan confirmation,
        Utf8ProjectionPlan projection)
    {
        var prefilterEngine = plan.PrefilterPlan.PrimarySource;
        if (!prefilterEngine.HasValue) prefilterEngine = plan.PrefilterPlan.SecondarySource;
        if (!prefilterEngine.HasValue) prefilterEngine = plan.PrefilterPlan.WindowSource;
        var nativeSource = plan.NativeCandidateSource;
        var requiresConfirmation = plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement;

        if (nativeSource.HasValue)
        {
            var kind = plan.HasRequiredPrefilter
                ? requiresConfirmation
                    ? Utf8SearchOperationKind.PrefilterThenConfirm
                    : Utf8SearchOperationKind.PrefilterThenSearch
                : requiresConfirmation
                    ? Utf8SearchOperationKind.SearchThenConfirm
                    : IsLargeAutomatonHybridCandidate(plan)
                        ? Utf8SearchOperationKind.HybridSearch
                        : Utf8SearchOperationKind.DirectSearch;
            return Utf8SearchOperationPlan.Create(
                kind,
                nativeSource,
                prefilterEngine,
                confirmation,
                projection,
                kind == Utf8SearchOperationKind.HybridSearch
                    ? Utf8SearchObservabilityKind.Effectiveness
                    : Utf8SearchObservabilityKind.CandidateCounts);
        }

        var fallbackSource = plan.FallbackCandidateSource;
        if (fallbackSource.HasValue)
        {
            var kind = plan.HasRequiredPrefilter
                ? Utf8SearchOperationKind.PrefilterThenConfirm
                : Utf8SearchOperationKind.SearchThenConfirm;
            return Utf8SearchOperationPlan.Create(
                kind,
                fallbackSource,
                prefilterEngine,
                confirmation,
                projection,
                Utf8SearchObservabilityKind.CandidateCounts);
        }

        return default;
    }

    private static bool IsLargeAutomatonHybridCandidate(in Utf8SearchPlan plan)
    {
        return plan.MultiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactAutomaton &&
            !plan.HasBoundaryRequirements &&
            !plan.HasTrailingLiteralRequirement;
    }
}
