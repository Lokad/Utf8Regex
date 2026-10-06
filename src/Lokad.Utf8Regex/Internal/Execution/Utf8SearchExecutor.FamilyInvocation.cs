using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SearchExecutor
{
    // Each branch retains its original ordinal. LTR chooses the earliest start;
    // RTL chooses the latest end, then the first branch at that end, as .NET does.
    // All rejected candidates and long predicates charge the same invocation.
    internal static Utf8ExecutionLimitKind TryFindLiteralFamily(
        in Utf8SearchPlan plan, ReadOnlySpan<byte> input, int start, int end,
        bool reverse, ref Utf8SliceInvocation invocation, out PreparedSearchMatch match,
        out bool found)
    {
        match = default;
        found = false;
        ArgumentOutOfRangeException.ThrowIfNegative(start);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(end, input.Length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(start, end);
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var low = start;
        var high = end - 1;
        if (plan.AlternateLiteralsUtf8 is { } literals)
        {
            var folded = plan.Kind == Utf8SearchKind.AsciiFoldedByteLiterals;
            while (low <= high)
            {
                // Search only one candidate window for every branch. Searching
                // an absent branch to the subject end on every MoveNext would
                // make a dense later branch quadratic over the enumeration.
                var count = Math.Min(2048, high - low + 1);
                var blockStart = reverse ? high - count + 1 : low;
                var block = input.Slice(blockStart, count);
                var candidateByte = -1;
                for (var id = 0; id < literals.Length; id++)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var literal = literals[id];
                    if (literal.Length == 0)
                        throw new InvalidOperationException("Native literal families require nonempty branches.");
                    var value = reverse ? literal[^1] : literal[0];
                    var other = folded && (uint)((value | 0x20) - 'a') <= 'z' - 'a' ? (byte)(value ^ 0x20) : value;
                    var relative = reverse ? block.LastIndexOfAny(value, other) : block.IndexOfAny(value, other);
                    if (relative >= 0 && (candidateByte < 0 || (reverse
                            ? blockStart + relative > candidateByte : blockStart + relative < candidateByte)))
                        candidateByte = blockStart + relative;
                }
                if (candidateByte < 0)
                {
                    if (reverse) high -= count;
                    else low += count;
                    continue;
                }

                for (var id = 0; id < literals.Length; id++)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var literal = literals[id];
                    var candidate = reverse ? candidateByte + 1 - literal.Length : candidateByte;
                    if (candidate < start || candidate > end - literal.Length) continue;
                    breach = TryCompareLiteral(input.Slice(candidate, literal.Length), literal, folded,
                        ref invocation, out var equal);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    if (!equal) continue;
                    breach = TryConfirmLiteral(plan, input, candidate, literal.Length,
                        ref invocation, out var accepted);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    if (accepted)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        invocation.Budget.ThrowIfCancellationRequested();
                        match = new PreparedSearchMatch(candidate, literal.Length, id);
                        found = true;
                        Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
                        return Utf8ExecutionLimitKind.None;
                    }
                }
                if (reverse) high = candidateByte - 1;
                else low = candidateByte + 1;
            }
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        return Utf8ExecutionLimitKind.None;
    }
}
