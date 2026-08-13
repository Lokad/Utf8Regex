using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledSearchAnalysisPolicy
{
    public static bool IsExactLiteralPipeline(Utf8SearchOperationPlan pipeline)
    {
        return pipeline.CandidateSource.Kind == Utf8CandidateSearchKind.PreparedSearcher &&
            pipeline.CandidateSource.PortfolioKind is Utf8SearchPortfolioKind.ExactLiteral or Utf8SearchPortfolioKind.IgnoreCaseLiteral;
    }

    public static bool IsLiteralFamilyPipeline(Utf8SearchOperationPlan pipeline)
    {
        return pipeline.CandidateSource.Kind == Utf8CandidateSearchKind.PreparedSearcher &&
            pipeline.CandidateSource.PortfolioKind is
                Utf8SearchPortfolioKind.ExactDirectFamily or
                Utf8SearchPortfolioKind.ExactTrieFamily or
                Utf8SearchPortfolioKind.ExactAutomatonFamily or
                Utf8SearchPortfolioKind.ExactPackedFamily or
                Utf8SearchPortfolioKind.ExactEarliestFamily or
                Utf8SearchPortfolioKind.AsciiIgnoreCaseFamily;
    }

    public static bool IsSearchGuidedFallbackPipeline(
        Utf8SearchOperationPlan countPipeline,
        Utf8SearchOperationPlan firstMatchPipeline)
    {
        if (countPipeline.Confirmation.Kind != Utf8ConfirmationKind.FallbackVerifier ||
            firstMatchPipeline.Confirmation.Kind != Utf8ConfirmationKind.FallbackVerifier)
        {
            return false;
        }

        return countPipeline.CandidateSource.Kind == Utf8CandidateSearchKind.StructuralSearchSet &&
            firstMatchPipeline.CandidateSource.Kind == Utf8CandidateSearchKind.StructuralSearchSet &&
            countPipeline.Kind is
                Utf8SearchOperationKind.SearchThenConfirm or
                Utf8SearchOperationKind.PrefilterThenConfirm;
    }

    public static bool CanPromoteFallbackExecution(Utf8PreparedRegex regexPlan)
    {
        return !string.Equals(regexPlan.FallbackReason, "unsupported_options", StringComparison.Ordinal);
    }

    public static bool CanUseStructuralLinearSimplePattern(Utf8PreparedRegex regexPlan)
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

    public static bool CanUseCompiledFallback(Utf8PreparedRegex regexPlan)
    {
        return regexPlan.ExecutionProgram is not null &&
            regexPlan.SearchPlan.FallbackSearch.CandidatePlans is { Length: > 0 } &&
            !regexPlan.FallbackVerifier.RequiresCandidateEndCoverage &&
            !regexPlan.FallbackVerifier.RequiresTrailingAnchorCoverage &&
            string.Equals(regexPlan.FallbackReason, "unsupported_loop", StringComparison.Ordinal) &&
            !HasBackreferenceInstructions(regexPlan);
    }

    private static bool HasBackreferenceInstructions(Utf8PreparedRegex regexPlan)
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
