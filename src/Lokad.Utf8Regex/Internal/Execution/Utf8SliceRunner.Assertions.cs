namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    // An assertion borrows its parent's capture trail and owns only its local
    // repeat arrays. Suspending a cursor preserves the exact backtrack boundary.
    private struct AssertionCursor
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

    private readonly record struct AssertionContinuation(
        AssertionCursor Parent, int FrameMark, int JournalMark, int TrailMark, bool Positive);

    private static Utf8SliceMatchOutcome RunFrom(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int start,
        int[] captureStarts, int[] captureEnds, int[] counts, int[] positions, int[] balanceHeads,
        bool collectHistory, bool requireNonEmpty, bool startIsScalarBoundary,
        ref ChunkedStacks stacks, ref Utf8SliceInvocation invocation)
    {
        var current = new AssertionCursor(program, start, counts, positions,
            requireNonEmpty, startIsScalarBoundary, stacks.FrameCount);
        var continuations = new Utf8InvocationStateBuffer<AssertionContinuation>();
        var resumeFailure = false;
        try
        {
            while (true)
            {
                var outcome = RunSegment(input, captureStarts, captureEnds, balanceHeads,
                    ref current, resumeFailure, ref stacks, ref invocation, out var suspended);
                resumeFailure = false;
                if (!suspended && outcome.IsMatch && current.RequiredEnd is int requiredEnd && outcome.EndIndex != requiredEnd)
                {
                    // A forward fixed-length assertion must end at its original
                    // edge. Keep alternatives within the same owned VM.
                    resumeFailure = true;
                    continue;
                }
                if (suspended)
                {
                    var instruction = current.Program.Instructions[current.ProgramIndex];
                    var body = current.Program.Assertions[instruction.A].Body;
                    var assertion = current.Program.Assertions[instruction.A];
                    var assertionStart = current.InputIndex;
                    if (assertion.ForwardScalarCount is uint scalarCount)
                    {
                        var available = scalarCount <= (uint)assertionStart;
                        for (uint count = 0; available && count < scalarCount; count++)
                        {
                            if ((count & 63) == 0)
                            {
                                var step = invocation.TryChargeInputStep();
                                if (step != Utf8ExecutionLimitKind.None) return new(false, -1, step, null, null);
                            }
                            assertionStart = AdvancePastScalar(input, assertionStart, true);
                            available = assertionStart >= 0;
                        }
                        if (!available)
                        {
                            resumeFailure = instruction.B != 0;
                            if (!resumeFailure) current.ProgramIndex++;
                            continue;
                        }
                    }
                    var nextCounts = Array.Empty<int>();
                    var nextPositions = Array.Empty<int>();
                    ulong nextReservation = 0;
                    var adopted = false;
                    try
                    {
                        var breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach, null, null);
                        // The child needs a marker even when its body has no
                        // forks. Reject depth before acquiring its local arrays
                        // or growing the continuation buffer.
                        var depthBreach = invocation.Budget.TryNoteDepth((uint)stacks.FrameCount + 1);
                        if (depthBreach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, depthBreach, null, null);
                        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(body.RepeatCount) * 2);
                        breach = invocation.TryReserveWorking(bytes);
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach, null, null);
                        nextReservation = bytes;
                        if (body.RepeatCount != 0)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach, null, null);
                            nextCounts = new int[body.RepeatCount];
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach, null, null);
                            nextPositions = new int[body.RepeatCount];
                        }
                        for (var offset = 0; offset < nextCounts.Length;)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None)
                                return new(false, -1, breach, null, null);
                            var length = Math.Min(2048, nextCounts.Length - offset);
                            nextCounts.AsSpan(offset, length).Fill(-1);
                            nextPositions.AsSpan(offset, length).Fill(-1);
                            offset += length;
                        }

                        var frameMark = stacks.FrameCount;
                        breach = continuations.TryPush(new(current, frameMark,
                            stacks.JournalCount, stacks.TrailCount, instruction.B != 0), ref invocation);
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach, null, null);
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
                            return new(false, -1, stacks.Breach, null, null);
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
                    var lastClosedSlot = -1;
                    if (program.GroupCount != 0)
                    {
                        var completionBreach = stacks.FindLastClosedSlot(ref invocation, out lastClosedSlot);
                        if (completionBreach != Utf8ExecutionLimitKind.None) return new(false, -1, completionBreach, null, null);
                    }
                    int[][]? historyStarts = null, historyLengths = null;
                    if (collectHistory)
                    {
                        var historyBreach = stacks.MaterializeHistory(program.GroupCount, ref invocation, out historyStarts, out historyLengths);
                        if (historyBreach != Utf8ExecutionLimitKind.None) return new(false, -1, historyBreach, null, null);
                    }
                    return new(true, outcome.EndIndex, outcome.Breach, historyStarts, historyLengths, lastClosedSlot);
                }
                var exitBreach = invocation.TryChargeInputStep();
                if (exitBreach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, exitBreach, null, null);
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
            continuations.Release(ref invocation);
        }
    }

    private static void RestoreAssertion(AssertionContinuation continuation, ref AssertionCursor current,
        int[] captureStarts, int[] captureEnds, int[] balanceHeads, bool keepCaptures,
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
