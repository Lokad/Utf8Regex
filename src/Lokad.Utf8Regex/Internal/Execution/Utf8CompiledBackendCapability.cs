using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8CompiledBackendCapability
{
    public static bool CanUseEmittedLiteralFamily(Utf8SearchPlan searchPlan)
    {
        return IsEmittableLiteralFamilyProgram(searchPlan.CountOperation) &&
            IsEmittableLiteralFamilyProgram(searchPlan.FirstMatchOperation) &&
            Utf8EmittedLiteralFamilyCounter.CanCreate(searchPlan, searchPlan.CountOperation, searchPlan.FirstMatchOperation);
    }

    public static bool CanUseEmittedStructuralLinear(Utf8PreparedRegex regexPlan)
    {
        return Utf8EmittedDeterministicMatcher.CanCreate(regexPlan.StructuralLinearProgram);
    }

    public static bool CanUseEmittedSearchGuidedFallback(Utf8PreparedRegex regexPlan)
    {
        return IsEmittableSearchGuidedFallbackProgram(regexPlan.SearchPlan.CountOperation) &&
            IsEmittableSearchGuidedFallbackProgram(regexPlan.SearchPlan.FirstMatchOperation) &&
            (CanUseVerifierDrivenSearchGuidedFallback(regexPlan) ||
             CanUseBoundaryLiteralFamilySearchGuidedFallback(regexPlan));
    }

    private static bool IsEmittableSearchGuidedFallbackProgram(Utf8SearchOperationPlan program)
    {
        return program.HasValue &&
            program.Confirmation.HasValue &&
            program.CandidateSource.Kind is Utf8CandidateSearchKind.StructuralSearchSet or Utf8CandidateSearchKind.PreparedSearcher &&
            program.Projection.Kind == Utf8ProjectionKind.None;
    }

    private static bool CanUseVerifierDrivenSearchGuidedFallback(Utf8PreparedRegex regexPlan)
    {
        return regexPlan.SearchPlan.CountOperation.Confirmation.Kind == Utf8ConfirmationKind.FallbackVerifier &&
            regexPlan.SearchPlan.FirstMatchOperation.Confirmation.Kind == Utf8ConfirmationKind.FallbackVerifier;
    }

    private static bool CanUseBoundaryLiteralFamilySearchGuidedFallback(Utf8PreparedRegex regexPlan)
    {
        var searchPlan = regexPlan.SearchPlan;
        return regexPlan.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            searchPlan.Kind == Utf8SearchKind.ExactAsciiLiterals &&
            searchPlan.HasPreparedSearcher &&
            searchPlan.PreparedSearcher.Kind == PreparedSearcherKind.MultiLiteral &&
            searchPlan.HasBoundaryRequirements &&
            !searchPlan.HasTrailingLiteralRequirement &&
            searchPlan.CountOperation.Confirmation.Kind == Utf8ConfirmationKind.BoundaryRequirements &&
            searchPlan.FirstMatchOperation.Confirmation.Kind == Utf8ConfirmationKind.BoundaryRequirements;
    }

    private static bool IsEmittableLiteralFamilyProgram(Utf8SearchOperationPlan program)
    {
        return program.HasValue &&
            program.Kind is not Utf8SearchOperationKind.HybridSearch;
    }
}
