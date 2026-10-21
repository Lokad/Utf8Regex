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

    public static bool CanUseStructuralLinearSimplePattern(Utf8PreparedRegex regexPlan)
    {
        if (regexPlan.SimplePatternPlan.IsStartAnchored ||
            regexPlan.SimplePatternPlan.IsEndAnchored ||
            regexPlan.Features.HasBackreferences)
        {
            return false;
        }

        return regexPlan.SimplePatternPlan.RunPlan.HasValue ||
            regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern ||
            regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiLiteralFamilyRun;
    }
}
