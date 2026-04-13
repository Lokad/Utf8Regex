using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledSearchAnalysisPolicy
{
    public static bool IsExactLiteralPipeline(Utf8ExecutablePipelinePlan pipeline)
    {
        return pipeline.Strategy.CandidateEngine.Kind == Utf8SearchEngineKind.PreparedSearcher &&
            pipeline.Strategy.CandidateEngine.PortfolioKind is Utf8SearchPortfolioKind.ExactLiteral or Utf8SearchPortfolioKind.IgnoreCaseLiteral;
    }

    public static bool IsLiteralFamilyPipeline(Utf8ExecutablePipelinePlan pipeline)
    {
        return pipeline.Strategy.CandidateEngine.Kind == Utf8SearchEngineKind.PreparedSearcher &&
            pipeline.Strategy.CandidateEngine.PortfolioKind is
                Utf8SearchPortfolioKind.ExactDirectFamily or
                Utf8SearchPortfolioKind.ExactTrieFamily or
                Utf8SearchPortfolioKind.ExactAutomatonFamily or
                Utf8SearchPortfolioKind.ExactPackedFamily or
                Utf8SearchPortfolioKind.ExactEarliestFamily or
                Utf8SearchPortfolioKind.AsciiIgnoreCaseFamily;
    }

    public static bool IsSearchGuidedFallbackPipeline(
        Utf8ExecutablePipelinePlan countPipeline,
        Utf8ExecutablePipelinePlan firstMatchPipeline)
    {
        if (countPipeline.Confirmation.Kind != Utf8ConfirmationKind.FallbackVerifier ||
            firstMatchPipeline.Confirmation.Kind != Utf8ConfirmationKind.FallbackVerifier)
        {
            return false;
        }

        return countPipeline.Strategy.CandidateEngine.Kind == Utf8SearchEngineKind.StructuralSearchSet &&
            firstMatchPipeline.Strategy.CandidateEngine.Kind == Utf8SearchEngineKind.StructuralSearchSet &&
            countPipeline.Strategy.Kind is
                Utf8SearchMetaStrategyKind.SearchThenConfirm or
                Utf8SearchMetaStrategyKind.PrefilterThenConfirm;
    }

    public static bool CanPromoteFallbackExecution(Utf8RegexPlan regexPlan)
    {
        return !string.Equals(regexPlan.FallbackReason, "unsupported_options", StringComparison.Ordinal);
    }

    public static bool CanUseStructuralLinearSimplePattern(Utf8RegexPlan regexPlan)
    {
        if (regexPlan.SimplePatternPlan.IsStartAnchored ||
            regexPlan.SimplePatternPlan.IsEndAnchored ||
            HasBackreferenceInstructions(regexPlan))
        {
            return false;
        }

        return regexPlan.SimplePatternPlan.RunPlan.HasValue ||
            regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern ||
            regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiLiteralFamilyRun;
    }

    public static bool CanUseCompiledFallback(Utf8RegexPlan regexPlan)
    {
        return regexPlan.ExecutionProgram is not null &&
            regexPlan.SearchPlan.FallbackSearch.CandidatePlans is { Length: > 0 } &&
            !regexPlan.FallbackVerifier.RequiresCandidateEndCoverage &&
            !regexPlan.FallbackVerifier.RequiresTrailingAnchorCoverage &&
            string.Equals(regexPlan.FallbackReason, "unsupported_loop", StringComparison.Ordinal) &&
            !HasBackreferenceInstructions(regexPlan);
    }

    private static bool HasBackreferenceInstructions(Utf8RegexPlan regexPlan)
    {
        var executionProgram = regexPlan.ExecutionProgram;
        if (executionProgram is null)
        {
            return false;
        }

        foreach (var instruction in executionProgram.Instructions)
        {
            if (instruction.NodeKind == Utf8ExecutionNodeKind.Backreference)
            {
                return true;
            }
        }

        return false;
    }
}
