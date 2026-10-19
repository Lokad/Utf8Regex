using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchStrategyExecutor
{
    internal static Utf8ExecutionLimitKind TryFindNextLiteralFamilyMatch(
        in Utf8SearchPlan plan, in Utf8SearchOperationPlan operation,
        ReadOnlySpan<byte> input, ref PreparedMultiLiteralScanState state,
        ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found)
    {
        return Utf8BackendInstructionExecutor.TryFindNextLiteralFamilyMatch(
            plan, operation, input, ref state, ref invocation, out match, out found);
    }
}
