namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    // An assertion borrows its parent's capture trail and owns only its local
    // repeat arrays. Suspending a cursor preserves the exact backtrack boundary.
    internal struct AssertionCursor
    {
        internal AssertionCursor(Utf8SliceProgram program, int start, int[] counts, int[] positions,
            bool requireNonEmpty, bool aligned, int frameBase)
        {
            Program = program;
            Start = start;
            Counts = counts;
            Positions = positions;
            RequireNonEmpty = requireNonEmpty;
            Aligned = aligned;
            FrameBase = frameBase;
            ProgramIndex = 0;
            InputIndex = start;
            RepeatReservation = 0;
            RequiredEnd = null;
        }

        internal Utf8SliceProgram Program;
        internal int Start;
        internal int[] Counts;
        internal int[] Positions;
        internal bool RequireNonEmpty;
        internal bool Aligned;
        internal int FrameBase;
        internal int ProgramIndex;
        internal int InputIndex;
        internal ulong RepeatReservation;
        internal int? RequiredEnd;
    }

    internal readonly record struct AssertionContinuation(
        AssertionCursor Parent, int FrameMark, int JournalMark, int TrailMark, bool Positive);

    internal static Utf8SliceMatchOutcome RunFrom(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int start,
        scoped Span<int> captureStarts, scoped Span<int> captureEnds,
        bool collectHistory, bool requireNonEmpty, bool startIsScalarBoundary,
        ref AttemptWorkspace workspace, ref Utf8SliceInvocation invocation, int? requiredEnd = null)
    {
        ref var stacks = ref workspace.Stacks;
        ref var continuations = ref workspace.AssertionContinuations;
        var balanceHeads = workspace.Heads;
        // Only the complete root program may discard terminal rewind choices.
        // Assertion segments can still need them to satisfy RequiredEnd.
        var terminalClassRepeat = program.TerminalRoot == Utf8SliceTerminalRootKind.ClassRepeat;
        var current = new AssertionCursor(program, start, workspace.Counts, workspace.Positions,
            requireNonEmpty, startIsScalarBoundary, stacks.FrameCount) { RequiredEnd = requiredEnd };
        var resumeFailure = false;
        try
        {
            while (true)
            {
                var suspended = false;
                var outcome = terminalClassRepeat
                    ? RunTerminalClassRepeat(program, input, start, requireNonEmpty, ref invocation)
                    : RunSegment(input, captureStarts, captureEnds, balanceHeads,
                        ref current, resumeFailure, preserveCaptureTrail: true, ref stacks, ref invocation, out suspended);
                resumeFailure = false;
                if (suspended)
                {
                    var instruction = current.Program.Instructions[current.ProgramIndex];
                    var body = current.Program.Assertions[instruction.A].Body;
                    var assertion = current.Program.Assertions[instruction.A];
                    var assertionStart = current.InputIndex;
                    var startBreach = TryRewindAssertionStart(input, assertion.ForwardScalarCount,
                        ref assertionStart, ref invocation, out var available);
                    if (startBreach != Utf8ExecutionLimitKind.None)
                        return new(false, -1, startBreach);
                    if (!available)
                    {
                        resumeFailure = instruction.B != 0;
                        if (!resumeFailure) current.ProgramIndex++;
                        continue;
                    }
                    var nextCounts = Array.Empty<int>();
                    var nextPositions = Array.Empty<int>();
                    ulong nextReservation = 0;
                    var adopted = false;
                    try
                    {
                        var breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach);
                        // The child needs a marker even when its body has no
                        // forks. Reject depth before acquiring its local arrays
                        // or growing the continuation buffer.
                        var depthBreach = invocation.Budget.TryNoteDepth((uint)stacks.FrameCount + 1);
                        if (depthBreach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, depthBreach);
                        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(body.RepeatCount) * 2);
                        breach = invocation.TryReserveWorking(bytes);
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach);
                        nextReservation = bytes;
                        if (body.RepeatCount != 0)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach);
                            nextCounts = new int[body.RepeatCount];
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach);
                            nextPositions = new int[body.RepeatCount];
                        }
                        for (var offset = 0; offset < nextCounts.Length;)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None)
                                return new(false, -1, breach);
                            var length = Math.Min(2048, nextCounts.Length - offset);
                            nextCounts.AsSpan(offset, length).Fill(-1);
                            nextPositions.AsSpan(offset, length).Fill(-1);
                            offset += length;
                        }

                        var frameMark = stacks.FrameCount;
                        breach = continuations.TryPush(new(current, frameMark,
                            stacks.JournalCount, stacks.TrailCount, instruction.B != 0), ref invocation);
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach);
                        var expectedEnd = current.InputIndex;
                        var aligned = (uint)assertionStart >= (uint)input.Length ||
                            (input[assertionStart] & 0xC0) != 0x80;
                        current = new(body, assertionStart, nextCounts, nextPositions,
                            false, aligned, frameMark);
                        current.RequiredEnd = assertion.ForwardScalarCount.HasValue ? expectedEnd : null;
                        current.RepeatReservation = nextReservation;
                        adopted = true;
                        // The marker remains outside the child's backtrack boundary,
                        // including when its body has no forks of its own.
                        if (!stacks.PushFrame(new Utf8SliceFrame { ResumeKind = Utf8SliceResumeKind.Assertion }, ref invocation))
                            return new(false, -1, stacks.Breach);
                        current.FrameBase = stacks.FrameCount;
                    }
                    finally
                    {
                        if (!adopted)
                        {
                            invocation.Ledger.Release(nextReservation);
                        }
                    }
                    continue;
                }

                if (continuations.Count == 0)
                {
                    if (!outcome.IsMatch) return outcome;
                    (int Start, int End)[][]? history = null;
                    if (collectHistory)
                    {
                        var historyBreach = stacks.MaterializeHistory(program.GroupCount, ref invocation, out history);
                        if (historyBreach != Utf8ExecutionLimitKind.None) return new(false, -1, historyBreach);
                    }
                    return new(true, outcome.EndIndex, outcome.Breach, history);
                }
                var exitBreach = invocation.TryChargeInputStep();
                if (exitBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, exitBreach);
                var continuation = continuations[continuations.Count - 1];
                continuations.Pop();
                RestoreAssertion(continuation, ref current, captureStarts, captureEnds, balanceHeads,
                    continuation.Positive && outcome.IsMatch && outcome.Breach == Utf8ExecutionLimitKind.None,
                    ref stacks, ref invocation);
                if (outcome.Breach != Utf8ExecutionLimitKind.None) return outcome;
                resumeFailure = outcome.IsMatch != continuation.Positive;
                if (!resumeFailure) current.ProgramIndex++;
            }
        }
        finally
        {
            // Cleanup cannot be interrupted by a token or a spent work budget.
            // Every suspended child releases only its own repeat-array leases.
            while (continuations.Count != 0)
            {
                var continuation = continuations[continuations.Count - 1];
                continuations.Pop();
                RestoreAssertion(continuation, ref current, captureStarts, captureEnds, balanceHeads,
                    false, ref stacks, ref invocation);
            }
            // Only the root can acquire deferred tables; children retain their
            // existing independently released arrays. Keep root storage reusable
            // after success, failure and interruption of an assertion child.
            workspace.Counts = current.Counts;
            workspace.Positions = current.Positions;
        }
    }

    private static Utf8ExecutionLimitKind TryRewindAssertionStart(ReadOnlySpan<byte> input,
        uint? scalarCount, ref int start, ref Utf8SliceInvocation invocation, out bool available)
    {
        available = true;
        if (scalarCount is not uint length) return Utf8ExecutionLimitKind.None;
        available = length <= (uint)start;
        for (uint count = 0; available && count < length; count++)
        {
            if ((count & 63) == 0)
            {
                var step = invocation.TryChargeInputStep();
                if (step != Utf8ExecutionLimitKind.None) return step;
            }
            start = AdvancePastScalar(input, start, true);
            available = start >= 0;
        }
        return Utf8ExecutionLimitKind.None;
    }

    private static bool TryRunInlineAssertion(Utf8SliceAssertion assertion, ReadOnlySpan<byte> input,
        int start, uint depth, ref Utf8SliceInvocation invocation,
        out bool matched, out Utf8ExecutionLimitKind breach)
    {
        matched = false;
        breach = Utf8ExecutionLimitKind.None;
        var body = assertion.Body;
        var code = body.Instructions;
        var prefixLength = assertion.InlineLiteralPrefixLength;
        var singleClass = prefixLength < 0 && code is
            [{ Kind: Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Shorthand or
                Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.CategorySet },
             { Kind: Utf8SliceInstructionKind.Accept }];
        if (prefixLength < 0 && !singleClass) return false;
        // Fixed-forward lookbehind keeps its original scalar rewind and end
        // constraint, without suspending the parent for this bounded body.
        int? requiredEnd = assertion.ForwardScalarCount.HasValue ? start : null;
        breach = TryRewindAssertionStart(input, assertion.ForwardScalarCount,
            ref start, ref invocation, out var available);
        if (breach != Utf8ExecutionLimitKind.None || !available) return true;
        if (!singleClass)
        {
            breach = TryMatchInlineLiteralAssertion(code, 0, code.Length - 1, prefixLength,
                input, start, body.RightToLeft, requiredEnd, depth, ref invocation, out matched);
            return true;
        }

        breach = invocation.TryChargeInputStep();
        if (breach == Utf8ExecutionLimitKind.None) breach = invocation.Budget.TryNoteDepth(depth);
        if (breach != Utf8ExecutionLimitKind.None) return true;
        var reverse = body.RightToLeft;
        if (TryReadScalar(input, start, reverse, out var scalar, out var width) &&
            (requiredEnd is null || requiredEnd == start + (reverse ? -width : width)))
        {
            breach = TryMatchScalarClass(body, code[0], scalar, ref invocation, out matched);
            if (breach != Utf8ExecutionLimitKind.None) return true;
        }
        breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return true;
    }

    private static void PrepareInlineRepeatAssertion(Span<Utf8SliceInstruction> code, int repeatIndex,
        Utf8ConstructionBudget budget)
    {
        var fork = repeatIndex - 1;
        if (fork < 0 || code[fork] is not { Kind: Utf8SliceInstructionKind.Fork, B: 1, C: 0 } guard ||
            (uint)guard.A >= (uint)code.Length) return;
        var tail = code[repeatIndex].A + 1;
        var end = guard.A - 2;
        if (tail >= end || code[end].Kind != Utf8SliceInstructionKind.Commit) return;
        var exit = code[guard.A - 1];
        var positive = exit.Kind == Utf8SliceInstructionKind.Jump && exit.A == guard.A + 1 &&
            code[guard.A].Kind == Utf8SliceInstructionKind.Fail;
        if (!positive && exit.Kind != Utf8SliceInstructionKind.Fail) return;
        if (Utf8SliceAssertion.FindInlineLiteralPrefixLength(code, tail, end, budget) != end - tail) return;

        // The repeat excludes the first tail literal. Its last endpoint is
        // the only one that can match, and the body has no capture effects.
        code[fork] = new(guard.Kind, guard.A, guard.B, -1, positive ? 1 : 0);
    }

    private static Utf8ExecutionLimitKind TryMatchInlineRepeatAssertion(Utf8SliceProgram program,
        int repeatIndex, int end, ReadOnlySpan<byte> input, int start, uint depth,
        ref Utf8SliceInvocation invocation, out bool matched)
    {
        matched = false;
        var breach = invocation.TryChargeInputStep();
        if (breach == Utf8ExecutionLimitKind.None) breach = invocation.Budget.TryNoteDepth(depth);
        if (breach != Utf8ExecutionLimitKind.None) return breach;

        var run = RunClassRepeat(program, repeatIndex, input, start, ref invocation, checkExitDeadline: false);
        if (run.Breach != Utf8ExecutionLimitKind.None) return run.Breach;
        var code = program.Instructions;
        var tail = code[repeatIndex].A + 1;
        matched = run.EndIndex >= 0 && MatchAsciiLiterals(code.AsSpan(tail, end - tail),
            input, run.EndIndex, reverse: false, requiredEnd: null);
        breach = ChargeRepeatBoundary(ref invocation, checkDeadline: false);
        invocation.Budget.ThrowIfCancellationRequested();
        return breach;
    }

    private static Utf8ExecutionLimitKind TryMatchInlineLiteralAssertion(ReadOnlySpan<Utf8SliceInstruction> code,
        int first, int end, int prefixLength, ReadOnlySpan<byte> input, int start, bool reverse,
        int? requiredEnd, uint depth, ref Utf8SliceInvocation invocation, out bool matched,
        bool checkExitDeadline = true)
    {
        matched = false;
        var breach = invocation.TryChargeStep();
        if (breach == Utf8ExecutionLimitKind.None) breach = invocation.Budget.TryNoteDepth(depth);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            invocation.Budget.CheckDeadline();
            return breach;
        }

        // At most 32 instructions are inspected. Guarded repeats use the
        // instruction cadence inside the loop and check the deadline at exit.
        matched = MatchInlineLiteralAssertion(code, first, end, prefixLength, input, start, reverse, requiredEnd);
        breach = ChargeRepeatBoundary(ref invocation, checkExitDeadline);
        invocation.Budget.ThrowIfCancellationRequested();
        return breach;
    }

    private static bool MatchInlineLiteralAssertion(ReadOnlySpan<Utf8SliceInstruction> code,
        int first, int end, int prefixLength,
        ReadOnlySpan<byte> input, int start, bool reverse, int? requiredEnd)
    {
        var position = first + prefixLength;
        if (position == end) return MatchAsciiLiterals(code[first..end], input, start, reverse, requiredEnd);
        if (prefixLength != 0 && !MatchAsciiLiterals(code[first..position], input, start, reverse, null)) return false;
        start += reverse ? -prefixLength : prefixLength;

        // Every fork has a straight literal arm and a jump to the assertion
        // end. Only the input edge is local to an arm.
        while (position < end && code[position].Kind == Utf8SliceInstructionKind.Fork)
        {
            var next = code[position].A;
            if (MatchAsciiLiterals(code[(position + 1)..(next - 1)], input, start, reverse, requiredEnd)) return true;
            position = next;
        }
        return MatchAsciiLiterals(code[position..end], input, start, reverse, requiredEnd);
    }

    private static bool MatchAsciiLiterals(ReadOnlySpan<Utf8SliceInstruction> literals, ReadOnlySpan<byte> input,
        int start, bool reverse, int? requiredEnd)
    {
        var length = literals.Length;
        if ((uint)start > (uint)input.Length || (reverse ? start < length : length > input.Length - start)) return false;
        if (requiredEnd is int end && end != (reverse ? start - length : start + length)) return false;
        var bytes = input.Slice(reverse ? start - length : start, length);
        for (var index = 0; index < length; index++)
            if (bytes[reverse ? length - 1 - index : index] != literals[index].A) return false;
        return true;
    }

    private static void RestoreAssertion(AssertionContinuation continuation, ref AssertionCursor current,
        scoped Span<int> captureStarts, scoped Span<int> captureEnds, int[] balanceHeads, bool keepCaptures,
        ref ChunkedStacks stacks, ref Utf8SliceInvocation invocation)
    {
        try
        {
            while (stacks.FrameCount > continuation.FrameMark) stacks.PopFrame(out _);
            stacks.TruncateJournal(continuation.JournalMark, current.Counts, current.Positions);
            if (!keepCaptures)
                stacks.TruncateTrail(continuation.TrailMark, captureStarts, captureEnds, balanceHeads);
        }
        finally
        {
            invocation.Ledger.Release(current.RepeatReservation);
            current = continuation.Parent;
        }
    }
}
