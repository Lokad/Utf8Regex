using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledBackendCapability
{
    public static bool CanUseEmittedLiteralFamily(Utf8SearchPlan searchPlan)
    {
        return IsEmittableLiteralFamilyProgram(searchPlan.CountProgram) &&
            IsEmittableLiteralFamilyProgram(searchPlan.FirstMatchProgram) &&
            Utf8EmittedLiteralFamilyCounter.CanCreate(searchPlan, searchPlan.CountProgram, searchPlan.FirstMatchProgram);
    }

    public static bool CanUseEmittedStructuralLinear(Utf8RegexPlan regexPlan)
    {
        return Utf8EmittedDeterministicMatcher.CanCreate(regexPlan.StructuralLinearProgram);
    }

    public static bool CanUseEmittedSearchGuidedFallback(Utf8RegexPlan regexPlan)
    {
        return IsEmittableSearchGuidedFallbackProgram(regexPlan.SearchPlan.CountProgram) &&
            IsEmittableSearchGuidedFallbackProgram(regexPlan.SearchPlan.FirstMatchProgram) &&
            (CanUseVerifierDrivenSearchGuidedFallback(regexPlan) ||
             CanUseBoundaryLiteralFamilySearchGuidedFallback(regexPlan));
    }

    private static bool IsEmittableSearchGuidedFallbackProgram(Utf8BackendInstructionProgram program)
    {
        return program.HasValue &&
            program.InstructionCount == 2 &&
            program.First.Kind == Utf8BackendInstructionKind.Search &&
            program.Second.Kind == Utf8BackendInstructionKind.Confirm &&
            program.Strategy.CandidateEngine.Kind is Utf8SearchEngineKind.StructuralSearchSet or Utf8SearchEngineKind.PreparedSearcher &&
            program.Projection.Kind == Utf8ProjectionKind.None;
    }

    private static bool CanUseVerifierDrivenSearchGuidedFallback(Utf8RegexPlan regexPlan)
    {
        return regexPlan.SearchPlan.CountProgram.Confirmation.Kind == Utf8ConfirmationKind.FallbackVerifier &&
            regexPlan.SearchPlan.FirstMatchProgram.Confirmation.Kind == Utf8ConfirmationKind.FallbackVerifier;
    }

    private static bool CanUseBoundaryLiteralFamilySearchGuidedFallback(Utf8RegexPlan regexPlan)
    {
        var searchPlan = regexPlan.SearchPlan;
        return regexPlan.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            searchPlan.Kind == Utf8SearchKind.ExactAsciiLiterals &&
            searchPlan.NativeSearch.HasPreparedSearcher &&
            searchPlan.PreparedSearcher.Kind == PreparedSearcherKind.MultiLiteral &&
            searchPlan.HasBoundaryRequirements &&
            !searchPlan.HasTrailingLiteralRequirement &&
            searchPlan.CountProgram.Confirmation.Kind == Utf8ConfirmationKind.BoundaryRequirements &&
            searchPlan.FirstMatchProgram.Confirmation.Kind == Utf8ConfirmationKind.BoundaryRequirements;
    }

    private static bool IsEmittableLiteralFamilyProgram(Utf8BackendInstructionProgram program)
    {
        return program.HasValue &&
            program.Strategy.Kind is not Utf8SearchMetaStrategyKind.HybridSearch;
    }
}
