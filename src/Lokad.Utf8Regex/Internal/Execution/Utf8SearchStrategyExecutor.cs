using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchStrategyExecutor
{
    public static int CountFallbackCandidates(Utf8SearchPlan plan, ReadOnlySpan<byte> input, bool requireScalarBoundary)
    {
        return Utf8FallbackSearchExecutor.CountCandidates(plan, input, requireScalarBoundary);
    }

    public static bool TryFindNextLiteralFamilyMatch(
        in Utf8SearchPlan plan,
        in Utf8SearchOperationPlan operation,
        ReadOnlySpan<byte> input,
        ref PreparedMultiLiteralScanState state,
        Utf8ExecutionDeadline budget,
        out PreparedSearchMatch match)
    {
        return Utf8BackendInstructionExecutor.TryFindNextLiteralFamilyMatch(
            in plan,
            in operation,
            input,
            ref state,
            budget,
            out match);
    }

    internal static Utf8ExecutionLimitKind TryFindNextLiteralFamilyMatch(
        in Utf8SearchPlan plan, in Utf8SearchOperationPlan operation,
        ReadOnlySpan<byte> input, ref PreparedMultiLiteralScanState state,
        ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found)
    {
        return Utf8BackendInstructionExecutor.TryFindNextLiteralFamilyMatch(
            plan, operation, input, ref state, ref invocation, out match, out found);
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
