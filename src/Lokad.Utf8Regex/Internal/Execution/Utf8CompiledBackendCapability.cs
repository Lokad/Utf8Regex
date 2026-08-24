using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

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
            CanUseVerifierDrivenSearchGuidedFallback(regexPlan);
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

    private static bool IsEmittableLiteralFamilyProgram(Utf8SearchOperationPlan program)
    {
        return program.HasValue &&
            program.Kind is not Utf8SearchOperationKind.HybridSearch;
    }
}
