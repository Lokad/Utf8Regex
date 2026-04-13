using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchStrategyExecutor
{
    public static bool TryFindNextFallbackVerifiedMatch(
        Utf8SearchPlan plan,
        Utf8ExecutablePipelinePlan pipeline,
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
            Utf8BackendInstructionProgramBuilder.Create(pipeline),
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
        Utf8ExecutablePipelinePlan pipeline,
        Utf8VerifierRuntime verifierRuntime,
        Utf8ExecutionProgram program,
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        int startIndex,
        ref Utf8BoundaryMap? boundaryMap,
        ref string? decoded,
        Utf8ExecutionBudget? budget,
        out Utf8ValueMatch match)
    {
        return Utf8BackendInstructionExecutor.TryFindNextCompiledFallbackMatch(
            plan,
            Utf8BackendInstructionProgramBuilder.Create(pipeline),
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
            plan.FirstMatchPipeline,
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
        Utf8ExecutionBudget? budget,
        out Utf8ValueMatch match)
    {
        return TryFindNextCompiledFallbackMatch(
            plan,
            plan.FirstMatchPipeline,
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
        Utf8ExecutionBudget? budget,
        out PreparedSearchMatch match)
    {
        return Utf8BackendInstructionExecutor.TryFindNextLiteralFamilyMatch(
            plan,
            plan.EnumerationProgram,
            input,
            ref state,
            budget,
            out match);
    }

    public static int CountLiteralFamily(Utf8SearchPlan plan, ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return Utf8BackendInstructionExecutor.CountLiteralFamily(
            plan,
            plan.CountProgram,
            input,
            budget);
    }

    public static bool IsMatchLiteralFamily(Utf8SearchPlan plan, ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, bool rightToLeft)
    {
        return Utf8BackendInstructionExecutor.IsMatchLiteralFamily(
            plan,
            plan.FirstMatchProgram,
            input,
            budget,
            rightToLeft);
    }
}
