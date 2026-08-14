using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchStrategyExecutor
{
    public static bool TryFindNextFallbackVerifiedMatch(
        Utf8SearchPlan plan,
        Utf8SearchOperationPlan operation,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out Utf8FallbackVerificationResult verification)
    {
        return Utf8BackendInstructionExecutor.TryFindNextFallbackVerifiedMatch(
            plan,
            operation,
            verifierRuntime,
            input,
            validation,
            startIndex,
            ref boundaryMap,
            ref decoded,
            out verification);
    }

    public static bool TryFindNextCompiledFallbackMatch(
        Utf8SearchPlan plan,
        Utf8SearchOperationPlan operation,
        Utf8VerifierRuntime verifierRuntime,
        Utf8ExecutionProgram program,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        Utf8ExecutionDeadline budget,
        out Utf8ValueMatch match)
    {
        return Utf8BackendInstructionExecutor.TryFindNextCompiledFallbackMatch(
            plan,
            operation,
            verifierRuntime,
            program,
            input,
            validation,
            startIndex,
            ref boundaryMap,
            ref decoded,
            budget,
            out match);
    }

    public static int CountFallbackCandidates(Utf8SearchPlan plan, ReadOnlySpan<byte> input, bool requireScalarBoundary)
    {
        return Utf8FallbackSearchExecutor.CountCandidates(plan, input, requireScalarBoundary);
    }

    public static bool TryFindNextFallbackVerifiedMatch(
        Utf8SearchPlan plan,
        Utf8VerifierRuntime verifierRuntime,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        out Utf8FallbackVerificationResult verification)
    {
        return TryFindNextFallbackVerifiedMatch(
            plan,
            plan.FirstMatchOperation,
            verifierRuntime,
            input,
            validation,
            startIndex,
            ref boundaryMap,
            ref decoded,
            out verification);
    }

    public static bool TryFindNextCompiledFallbackMatch(
        Utf8SearchPlan plan,
        Utf8VerifierRuntime verifierRuntime,
        Utf8ExecutionProgram program,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        Utf8ExecutionDeadline budget,
        out Utf8ValueMatch match)
    {
        return TryFindNextCompiledFallbackMatch(
            plan,
            plan.FirstMatchOperation,
            verifierRuntime,
            program,
            input,
            validation,
            startIndex,
            ref boundaryMap,
            ref decoded,
            budget,
            out match);
    }

    public static bool TryFindNextLiteralFamilyMatch(
        Utf8SearchPlan plan,
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        Utf8ExecutionDeadline budget,
        out PreparedSearchMatch match)
    {
        return Utf8BackendInstructionExecutor.TryFindNextLiteralFamilyMatch(
            plan,
            plan.EnumerationOperation,
            input,
            ref state,
            budget,
            out match);
    }

    public static int CountLiteralFamily(Utf8SearchPlan plan, ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return Utf8BackendInstructionExecutor.CountLiteralFamily(
            plan,
            plan.CountOperation,
            input,
            budget);
    }

    public static bool IsMatchLiteralFamily(Utf8SearchPlan plan, ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        return Utf8BackendInstructionExecutor.IsMatchLiteralFamily(
            plan,
            plan.FirstMatchOperation,
            input,
            budget,
            rightToLeft);
    }
}
