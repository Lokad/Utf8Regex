using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8AsciiFindExecutor
{
    internal static Utf8ExecutionLimitKind TryFindNextFixedDistanceCandidate(ReadOnlySpan<byte> input,
        PreparedAsciiFindPlan plan, int start, ref Utf8SliceInvocation invocation,
        out int index, out int length, out bool found)
    {
        index = -1;
        length = 0;
        found = false;
        var pendingIndex = -1;
        var pendingLength = 0;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if ((uint)start <= (uint)input.Length && plan.Mode == PreparedAsciiFindMode.FixedDistanceLiteral)
        {
            var offset = Math.Max(0L, (long)start + plan.Distance);
            if (offset <= input.Length)
            {
                breach = Utf8SearchExecutor.TryFindLiteralCandidate(plan.LiteralSearch, input, (int)offset,
                    input.Length - plan.LiteralSearch.Length, false, ref invocation, out var anchor);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var candidate = (long)anchor - plan.Distance;
                if (anchor >= 0 && candidate >= start && candidate <= input.Length)
                { pendingIndex = (int)candidate; pendingLength = plan.LiteralSearch.Length; }
            }
        }
        else if ((uint)start <= (uint)input.Length && plan.Mode == PreparedAsciiFindMode.FixedDistanceSet && plan.FixedDistanceSets.Length > 0)
        {
            var sets = plan.FixedDistanceSets;
            var primary = sets[0];
            var offset = Math.Max(0L, (long)start + primary.Distance);
            var position = offset <= input.Length ? (int)offset : input.Length;
            while (position < input.Length)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var count = Math.Min(2048, input.Length - position);
                var relative = input.Slice(position, count).IndexOfAny(primary.SearchValues);
                if (relative < 0) { position += count; continue; }
                var anchor = position + relative;
                var candidate = (long)anchor - primary.Distance;
                var equal = candidate >= start && candidate <= input.Length;
                foreach (var set in sets)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var at = candidate + set.Distance;
                    if (at < 0 || at >= input.Length || !set.Contains(input[(int)at])) { equal = false; break; }
                }
                if (equal) { pendingIndex = (int)candidate; pendingLength = 1; break; }
                position = anchor + 1;
            }
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        index = pendingIndex;
        length = pendingLength;
        found = index >= 0;
        return Utf8ExecutionLimitKind.None;
    }
}
