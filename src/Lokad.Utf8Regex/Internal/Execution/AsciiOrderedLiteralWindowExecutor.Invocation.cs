using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class AsciiOrderedLiteralWindowExecutor
{
    internal static Utf8ExecutionLimitKind TryFindNext(ReadOnlySpan<byte> input, in AsciiOrderedLiteralWindowPlan plan,
        in Utf8SearchPlan search, int start, ref Utf8SliceInvocation invocation, out PreparedSearchMatch match, out bool found)
    {
        match = default;
        found = false;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (!plan.HasValue || (uint)start > (uint)input.Length) return Utf8ExecutionLimitKind.None;
        var next = start;
        while (next <= input.Length)
        {
            breach = search.PreparedSearcher.TryFindMatch(input, next, input.Length, false,
                ref invocation, out var candidate, out var exists);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!exists) break;
            next = candidate.Index + 1;
            Utf8SearchDiagnosticsSession.Current?.CountSearchCandidate();
            // Retry complete branches at the same start in pattern order. A raw
            // candidate's first matching prefix does not decide its suffix.
            var branches = plan.IsLiteralFamily ? plan.LeadingLiteralsUtf8.Length : 1;
            for (var id = 0; id < branches; id++)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var leading = plan.IsLiteralFamily ? plan.LeadingLiteralsUtf8[id] : plan.LeadingLiteralUtf8;
                if (candidate.Index > input.Length - leading.Length) continue;
                breach = Utf8SearchExecutor.TryCompareLiteral(input[candidate.Index..], leading, false,
                    ref invocation, out var equal);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!equal || !DotNetUtf8WordBoundary.MatchesRequirement(plan.LeadingLiteralLeadingBoundary, input, candidate.Index) ||
                    !DotNetUtf8WordBoundary.MatchesRequirement(plan.LeadingLiteralTrailingBoundary, input, candidate.Index + leading.Length)) continue;
                Utf8SearchDiagnosticsSession.Current?.CountVerifierInvocation();
                breach = TryMatchAt(input, plan, candidate.Index, leading.Length, id,
                    ref invocation, out var length, out var accepted);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!accepted) continue;
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                invocation.Budget.ThrowIfCancellationRequested();
                match = new PreparedSearchMatch(candidate.Index, length, id);
                found = true;
                Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
                return Utf8ExecutionLimitKind.None;
            }
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        return Utf8ExecutionLimitKind.None;
    }

    internal static Utf8ExecutionLimitKind TryCount(ReadOnlySpan<byte> input, in AsciiOrderedLiteralWindowPlan plan,
        in Utf8SearchPlan search, ref Utf8SliceInvocation invocation, out int count)
    {
        count = 0;
        var total = 0;
        var floor = 0;
        while (floor <= input.Length)
        {
            var breach = TryFindNext(input, plan, search, floor, ref invocation, out var match, out var found);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!found) break;
            total++;
            floor = match.Index + Math.Max(1, match.Length);
        }
        var kind = invocation.TryChargeInputStep();
        if (kind != Utf8ExecutionLimitKind.None) return kind;
        invocation.Budget.ThrowIfCancellationRequested();
        count = total;
        return Utf8ExecutionLimitKind.None;
    }

    // Every scan borrows prepared literals and the same deadline, token, work
    // budget and ledger. Gap lengths are byte counts on admitted ASCII subjects.
    internal static Utf8ExecutionLimitKind TryMatchAt(ReadOnlySpan<byte> input, in AsciiOrderedLiteralWindowPlan plan,
        int start, int leadingLength, int branch, ref Utf8SliceInvocation invocation, out int length, out bool accepted)
    {
        length = 0;
        accepted = false;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        var separator = plan.BranchSeparators.Length > 0 ? plan.BranchSeparators[branch] : plan.Separator;
        var trailing = plan.TrailingSearchers[plan.HasPairedTrailingLiterals ? branch : 0];
        var trailingLiteral = plan.HasPairedTrailingLiterals ? plan.TrailingLiteralsUtf8[branch] : plan.TrailingLiteralUtf8;
        var trailingLength = trailingLiteral.Length;
        var prefixEnd = start + leadingLength;
        var highSeparator = prefixEnd;
        if (separator.HasValue)
        {
            while (highSeparator < input.Length && separator.CharClass.Contains(input[highSeparator]))
            {
                if (((highSeparator - prefixEnd) & 2047) == 0)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                }
                highSeparator++;
            }
        }
        var minimumSeparator = (long)prefixEnd + separator.Minimum;
        if (minimumSeparator > highSeparator) return Utf8ExecutionLimitKind.None;
        var lowSeparator = (int)minimumSeparator;
        var lazy = separator.IsLazy;
        var current = lazy ? lowSeparator : highSeparator;
        var separatorOnlyNonseparatorHead = plan.MaxGap == 0 && separator.HasValue &&
            !separator.CharClass.Contains(trailingLiteral[0]);
        if (separatorOnlyNonseparatorHead) current = highSeparator;
        var lineEnd = input.Length;
        var lineScan = current;
        var rejectedLow = input.Length;
        var rejectedHigh = -1;
        while (current >= lowSeparator && current <= highSeparator)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var lower = (long)current + plan.MinGap;
            var upper = (int)Math.Min((long)input.Length - trailingLength, (long)current + plan.MaxGap);
            if (plan.GapSameLine)
            {
                if (lazy && current > lineEnd) { lineEnd = input.Length; lineScan = current; }
                // Extend a monotonic line census. Greedy separator backtracking
                // checks each newly exposed byte below this initial census.
                while (lineEnd == input.Length && lineScan < upper)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var size = Math.Min(2048, upper - lineScan);
                    var relative = input.Slice(lineScan, size).IndexOf((byte)'\n');
                    if (relative >= 0) lineEnd = lineScan + relative;
                    lineScan += size;
                }
                // A newline at the trailing start belongs to the literal, not
                // to dot. Never truncate the trailing literal's own bytes.
                upper = Math.Min(upper, lineEnd);
            }
            var low = (int)Math.Min(input.Length, lower);
            var high = upper;
            if (lazy) low = Math.Max(low, rejectedHigh + 1);
            else high = Math.Min(high, rejectedLow - 1);
            while (lower <= upper && low <= high)
            {
                breach = trailing.TryFindMatch(input, low, high + trailingLength, true,
                    ref invocation, out var candidate, out var found);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!found) break;
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (DotNetUtf8WordBoundary.MatchesRequirement(plan.TrailingLiteralLeadingBoundary, input, candidate.Index) &&
                    DotNetUtf8WordBoundary.MatchesRequirement(plan.TrailingLiteralTrailingBoundary, input, candidate.Index + trailingLength))
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    invocation.Budget.ThrowIfCancellationRequested();
                    length = plan.YieldLeadingLiteralOnly ? leadingLength : candidate.Index + trailingLength - start;
                    accepted = true;
                    return Utf8ExecutionLimitKind.None;
                }
                high = candidate.Index - 1;
            }
            // Failed separator attempts expose only new candidate positions.
            // Replaying their whole overlapping suffix would be quadratic.
            if (lazy) rejectedHigh = Math.Max(rejectedHigh, upper);
            else rejectedLow = Math.Min(rejectedLow, (int)Math.Min(input.Length, lower));
            if (current == (lazy ? highSeparator : lowSeparator)) break;
            if (separatorOnlyNonseparatorHead) break;
            current += lazy ? 1 : -1;
            if (!lazy && plan.GapSameLine && input[current] == (byte)'\n') lineEnd = current;
            // With no dot gap and a nonseparator trailing head, only the end
            // of the separator run can match, in either separator order.
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        return Utf8ExecutionLimitKind.None;
    }
}
