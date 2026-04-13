namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8SearchStrategySelector
{
    public static Utf8SearchMetaStrategyPlan CreateCountStrategy(Utf8SearchPlan plan)
        => Create(plan, Utf8SearchSemantics.CountMatches);

    public static Utf8SearchMetaStrategyPlan CreateFirstMatchStrategy(Utf8SearchPlan plan)
        => Create(plan, Utf8SearchSemantics.FirstMatch);

    public static Utf8SearchMetaStrategyPlan CreateEnumerationStrategy(Utf8SearchPlan plan)
        => Create(plan, Utf8SearchSemantics.EnumerateMatches);

    private static Utf8SearchMetaStrategyPlan Create(Utf8SearchPlan plan, Utf8SearchSemantics semantics)
    {
        var prefilterEngine = plan.PrefilterPlan.PrimaryEngine.HasValue
            ? plan.PrefilterPlan.PrimaryEngine
            : plan.PrefilterPlan.SecondaryEngine.HasValue
                ? plan.PrefilterPlan.SecondaryEngine
                : plan.PrefilterPlan.WindowEngine;

        if (plan.NativeCandidateEngine.HasValue)
        {
            var kind = plan.HasRequiredPrefilter
                ? plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement
                    ? Utf8SearchMetaStrategyKind.PrefilterThenConfirm
                    : Utf8SearchMetaStrategyKind.PrefilterThenSearch
                : plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement
                    ? Utf8SearchMetaStrategyKind.SearchThenConfirm
                    : IsLargeAutomatonHybridCandidate(plan)
                        ? Utf8SearchMetaStrategyKind.HybridSearch
                        : Utf8SearchMetaStrategyKind.DirectSearch;
            return new Utf8SearchMetaStrategyPlan(
                kind,
                semantics with
                {
                    RequiresConfirmation = plan.HasBoundaryRequirements || plan.HasTrailingLiteralRequirement,
                    RequiresProjection = semantics.RequiresProjection,
                },
                plan.NativeCandidateEngine,
                prefilterEngine,
                kind == Utf8SearchMetaStrategyKind.HybridSearch
                    ? Utf8SearchObservabilityKind.Effectiveness
                    : Utf8SearchObservabilityKind.CandidateCounts);
        }

        if (plan.FallbackCandidateEngine.HasValue)
        {
            var kind = plan.HasRequiredPrefilter
                ? Utf8SearchMetaStrategyKind.PrefilterThenConfirm
                : Utf8SearchMetaStrategyKind.SearchThenConfirm;
            return new Utf8SearchMetaStrategyPlan(
                kind,
                semantics with { RequiresConfirmation = true, RequiresProjection = semantics.RequiresProjection },
                plan.FallbackCandidateEngine,
                prefilterEngine,
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
