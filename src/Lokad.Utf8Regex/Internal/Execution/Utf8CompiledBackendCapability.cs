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

    private static bool IsEmittableLiteralFamilyProgram(Utf8SearchOperationPlan program)
    {
        return program.HasValue &&
            program.Kind is not Utf8SearchOperationKind.HybridSearch;
    }
}
