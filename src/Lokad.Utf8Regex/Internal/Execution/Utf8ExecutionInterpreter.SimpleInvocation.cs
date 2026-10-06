using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8ExecutionInterpreter
{
    internal static Utf8ExecutionLimitKind TryFindNextSimplePattern(ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program, in Utf8SearchPlan search, in AsciiSimplePatternPlan plan,
        int floor, ref Utf8SliceInvocation invocation, out int index, out int length)
    {
        index = -1;
        length = 0;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (program is null || (uint)floor > (uint)input.Length) return Utf8ExecutionLimitKind.None;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            index = FindNextSimplePattern(input, program, search, plan, floor, null,
                invocation.Budget.Deadline, out length);
            return Utf8ExecutionLimitKind.None;
        }
        if (plan.IsAbsoluteStartAnchored && floor != 0) return Utf8ExecutionLimitKind.None;
        var candidate = floor;
        var searchFrom = (long)floor + plan.SearchLiteralOffset;
        var diagnostics = Utf8SearchDiagnosticsSession.Current;
        while ((uint)candidate <= (uint)input.Length)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (plan.SearchLiterals.Length > 0)
            {
                if (searchFrom > input.Length) break;
                breach = Utf8SearchExecutor.TryFindLiteralFamilyCandidate(plan.SearchLiterals, plan.IgnoreCase,
                    input, (int)searchFrom, input.Length, false, ref invocation, out var anchor, out var exists);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!exists) break;
                candidate = anchor.Index - plan.SearchLiteralOffset;
                searchFrom = (long)anchor.Index + 1;
            }
            diagnostics?.CountSearchCandidate();
            if (candidate >= floor)
            {
                var accepted = true;
                foreach (var check in plan.FixedLiteralChecks)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var offset = (long)candidate + check.Offset;
                    if (offset < 0 || offset > input.Length - check.Literal.Length) { accepted = false; break; }
                    breach = Utf8SearchExecutor.TryCompareLiteral(input.Slice((int)offset, check.Literal.Length),
                        check.Literal, plan.IgnoreCase, ref invocation, out accepted);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    if (!accepted) break;
                }
                if (!accepted) diagnostics?.CountFixedCheckReject();
                else
                {
                    diagnostics?.CountVerifierInvocation();
                    accepted = TryMatchSimpleBranchesAt(input, plan, candidate, ref invocation, out var matchedLength, out breach);
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    if (accepted)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        invocation.Budget.ThrowIfCancellationRequested();
                        index = candidate;
                        length = matchedLength;
                        diagnostics?.CountVerifierMatch();
                        return Utf8ExecutionLimitKind.None;
                    }
                }
            }
            if (plan.SearchLiterals.Length == 0) candidate++;
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        return Utf8ExecutionLimitKind.None;
    }

    // Branch ordinals encode greedy/lazy expansion and alternation priority.
    // Keep them unchanged; candidate discovery only supplies possible starts.
    internal static bool TryMatchSimpleBranchesAt(ReadOnlySpan<byte> input, in AsciiSimplePatternPlan plan,
        int start, ref Utf8SliceInvocation invocation, out int length, out Utf8ExecutionLimitKind breach)
    {
        length = 0;
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return false;
        breach = invocation.Budget.TryNoteDepth(1);
        if (breach != Utf8ExecutionLimitKind.None) return false;
        if ((uint)start > (uint)input.Length || plan.IsAbsoluteStartAnchored && start != 0 ||
            plan.IsStartAnchored && !plan.IsAbsoluteStartAnchored && start != 0 && input[start - 1] != (byte)'\n')
            return false;
        foreach (var branch in plan.Branches)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            if (branch.Length > input.Length - start) continue;
            var end = start + branch.Length;
            if (plan.IsEndAnchored && end != input.Length &&
                !(plan.AllowsTrailingNewlineBeforeEnd && end + 1 == input.Length && input[end] == (byte)'\n') &&
                !(!plan.IsAbsoluteEndAnchored && end < input.Length && input[end] == (byte)'\n')) continue;
            var accepted = true;
            for (var i = 0; i < branch.Length; i++)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                var token = branch[i];
                var value = plan.IgnoreCase ? AsciiSearch.FoldCase(input[start + i]) : input[start + i];
                accepted = token.Kind switch
                {
                    AsciiSimplePatternTokenKind.Literal => value == token.Literal,
                    AsciiSimplePatternTokenKind.Dot => true,
                    AsciiSimplePatternTokenKind.CharClass => !token.CharClass.IsEmpty && token.CharClass.Contains(value),
                    _ => false,
                };
                if (!accepted) break;
            }
            if (!accepted) continue;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            invocation.Budget.ThrowIfCancellationRequested();
            length = branch.Length;
            return true;
        }
        return false;
    }
}
