using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8AsciiInstructionLinearExecutor
{
    // Preserve the existing throughput kernel only when there is no bounded
    // policy, deadline, token or diagnostic checkpoint. All other scans borrow
    // the invocation used by entry validation and subsequent projection.
    internal static Utf8ExecutionLimitKind TryFindNextDeterministicMatch(
        in Utf8StructuralLinearProgram program, ReadOnlySpan<byte> input,
        ref Utf8AsciiDeterministicScanState state, ref Utf8SliceInvocation invocation,
        out Utf8AsciiDeterministicMatch match, out bool found)
    {
        match = default;
        found = false;
        var pending = state;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (!program.DeterministicProgram.HasValue) return Utf8ExecutionLimitKind.None;
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            found = TryFindNextNonOverlappingDeterministicRawMatch(program, input, ref pending,
                invocation.Budget.Deadline, out match);
            state = pending;
            return Utf8ExecutionLimitKind.None;
        }

        var deterministic = program.DeterministicProgram;
        if (deterministic.IsAbsoluteStartAnchored && pending.NextStartIndex != 0)
            return Utf8ExecutionLimitKind.None;
        var candidate = pending.NextStartIndex;
        var searchFrom = Math.Max((long)pending.SearchFrom, (long)candidate + deterministic.SearchLiteralOffset);
        var diagnostics = Utf8SearchDiagnosticsSession.Current;
        while ((uint)candidate <= (uint)input.Length)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (deterministic.SearchLiterals.Length > 0)
            {
                if (searchFrom > input.Length) break;
                breach = Utf8SearchExecutor.TryFindLiteralFamilyCandidate(deterministic.SearchLiterals,
                    deterministic.IgnoreCase, input, (int)searchFrom, input.Length, false,
                    ref invocation, out var anchor, out var exists);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (!exists) break;
                candidate = anchor.Index - deterministic.SearchLiteralOffset;
                searchFrom = (long)anchor.Index + 1;
            }
            diagnostics?.CountSearchCandidate();
            if (candidate >= pending.NextStartIndex)
            {
                diagnostics?.CountVerifierInvocation();
                var accepted = TryMatchDeterministicAt(input, deterministic, candidate,
                    ref invocation, out var length, out breach);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (accepted)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    invocation.Budget.ThrowIfCancellationRequested();
                    pending.NextStartIndex = candidate + Math.Max(length, 1);
                    pending.SearchFrom = pending.NextStartIndex + deterministic.SearchLiteralOffset;
                    state = pending;
                    match = new(candidate, length);
                    found = true;
                    diagnostics?.CountVerifierMatch();
                    return Utf8ExecutionLimitKind.None;
                }
            }
            if (deterministic.SearchLiterals.Length == 0) candidate++;
        }
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        invocation.Budget.ThrowIfCancellationRequested();
        return Utf8ExecutionLimitKind.None;
    }

    internal static bool TryMatchDeterministicAt(ReadOnlySpan<byte> input,
        Utf8AsciiDeterministicProgram program, int start, ref Utf8SliceInvocation invocation,
        out int length, out Utf8ExecutionLimitKind breach)
    {
        length = 0;
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return false;
        breach = invocation.Budget.TryNoteDepth(1);
        if (breach != Utf8ExecutionLimitKind.None) return false;
        if ((uint)start > (uint)input.Length || program.IsAbsoluteStartAnchored && start != 0 ||
            program.IsStartAnchored && !program.IsAbsoluteStartAnchored && start != 0 && input[start - 1] != (byte)'\n')
            return false;
        var index = start;
        foreach (var step in program.Steps)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return false;
            switch (step.Kind)
            {
                case Utf8AsciiDeterministicStepKind.Literal:
                    if ((uint)index >= (uint)input.Length ||
                        (program.IgnoreCase ? AsciiSearch.FoldCase(input[index]) : input[index]) != step.Literal)
                        return false;
                    index++;
                    break;
                case Utf8AsciiDeterministicStepKind.AnyByte:
                    if ((uint)index >= (uint)input.Length) return false;
                    index++;
                    break;
                case Utf8AsciiDeterministicStepKind.CharClass when !step.CharClass.IsEmpty:
                    if ((uint)index >= (uint)input.Length || !step.CharClass.Contains(
                        program.IgnoreCase ? AsciiSearch.FoldCase(input[index]) : input[index])) return false;
                    index++;
                    break;
                case Utf8AsciiDeterministicStepKind.RunCharClass when !step.CharClass.IsEmpty:
                    var count = 0;
                    while ((uint)index < (uint)input.Length && count < step.MaxCount && step.CharClass.Contains(input[index]))
                    {
                        if ((count & 2047) == 0)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return false;
                        }
                        index++;
                        count++;
                    }
                    if (count < step.MinCount) return false;
                    break;
                case Utf8AsciiDeterministicStepKind.Accept:
                    if (program.IsEndAnchored && index != input.Length &&
                        !(program.AllowsTrailingNewlineBeforeEnd && index + 1 == input.Length && input[index] == (byte)'\n') &&
                        !(!program.IsAbsoluteEndAnchored && (uint)index < (uint)input.Length && input[index] == (byte)'\n'))
                        return false;
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return false;
                    invocation.Budget.ThrowIfCancellationRequested();
                    length = index - start;
                    return true;
                default:
                    return false;
            }
        }
        return false;
    }
}
