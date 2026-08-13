using Lokad.Utf8Regex.Internal.Utilities;
namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8SearchStrategySelector
{
    public static Utf8SearchOperationPlan Create(
        Utf8SearchPlan plan,
        Utf8SearchSemantics semantics,
        Utf8ConfirmationPlan confirmation,
        Utf8ProjectionPlan projection)
    {
        var prefilterEngine = plan.PrefilterPlan.PrimarySource.HasValue
            ? plan.PrefilterPlan.PrimarySource
            : plan.PrefilterPlan.SecondarySource.HasValue
                ? plan.PrefilterPlan.SecondarySource
                : plan.PrefilterPlan.WindowSource;

        if (plan.NativeCandidateSource.HasValue)
        {
            var kind = plan.HasRequiredPrefilter
                ? plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement
                    ? Utf8SearchOperationKind.PrefilterThenConfirm
                    : Utf8SearchOperationKind.PrefilterThenSearch
                : plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement
                    ? Utf8SearchOperationKind.SearchThenConfirm
                    : IsLargeAutomatonHybridCandidate(plan)
                        ? Utf8SearchOperationKind.HybridSearch
                        : Utf8SearchOperationKind.DirectSearch;
            return Utf8SearchOperationPlan.Create(
                kind,
                semantics with
                {
                    RequiresConfirmation = plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement,
                    RequiresProjection = semantics.RequiresProjection,
                },
                plan.NativeCandidateSource,
                prefilterEngine,
                confirmation,
                projection,
                kind == Utf8SearchOperationKind.HybridSearch
                    ? Utf8SearchObservabilityKind.Effectiveness
                    : Utf8SearchObservabilityKind.CandidateCounts);
        }

        if (plan.FallbackCandidateSource.HasValue)
        {
            var kind = plan.HasRequiredPrefilter
                ? Utf8SearchOperationKind.PrefilterThenConfirm
                : Utf8SearchOperationKind.SearchThenConfirm;
            return Utf8SearchOperationPlan.Create(
                kind,
                semantics with { RequiresConfirmation = true, RequiresProjection = semantics.RequiresProjection },
                plan.FallbackCandidateSource,
                prefilterEngine,
                confirmation,
                projection,
                Utf8SearchObservabilityKind.CandidateCounts);
        }

        return default;
    }

    private static bool IsLargeAutomatonHybridCandidate(Utf8SearchPlan plan)
    {
        return plan.MultiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactAutomaton &&
            !plan.HasBoundaryRequirements &&
            !plan.HasTrailingLiteralRequirement;
    }
}
