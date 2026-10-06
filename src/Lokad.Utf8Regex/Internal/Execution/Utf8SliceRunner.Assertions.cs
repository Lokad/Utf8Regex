namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    // An assertion borrows its parent's capture trail and owns only its local
    // repeat arrays. Suspending a cursor preserves the exact backtrack boundary.
    private struct AssertionCursor
    {
        internal AssertionCursor(Utf8SliceProgram program, int start, int[] counts, int[] positions,
            bool collectHistory, bool requireNonEmpty, bool aligned, int frameBase)
        {
            Program = program;
            Start = start;
            Counts = counts;
            Positions = positions;
            CollectHistory = collectHistory;
            RequireNonEmpty = requireNonEmpty;
            Aligned = aligned;
            FrameBase = frameBase;
            ProgramIndex = 0;
            InputIndex = start;
        }

        internal Utf8SliceProgram Program;
        internal int Start;
        internal int[] Counts;
        internal int[] Positions;
        internal bool CollectHistory;
        internal bool RequireNonEmpty;
        internal bool Aligned;
        internal int FrameBase;
        internal int ProgramIndex;
        internal int InputIndex;
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
            collectHistory, requireNonEmpty, startIsScalarBoundary, stacks.FrameCount);
        var continuations = new Utf8InvocationStateBuffer<AssertionContinuation>();
        var resumeFailure = false;
        try
        {
            while (true)
            {
                var outcome = RunSegment(input, captureStarts, captureEnds, balanceHeads,
                    ref current, resumeFailure, ref stacks, ref invocation, out var suspended);
                resumeFailure = false;
                if (suspended)
                {
                    var instruction = current.Program.Instructions[current.ProgramIndex];
                    var body = current.Program.Assertions[instruction.A].Body;
                    var nextCounts = Array.Empty<int>();
                    var nextPositions = Array.Empty<int>();
                    var adopted = false;
                    try
                    {
                        var breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach, null, null);
                        nextCounts = RentIntArray(body.RepeatCount, ref invocation, out breach);
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach, null, null);
                        nextPositions = RentIntArray(body.RepeatCount, ref invocation, out breach);
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach, null, null);
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
                        var aligned = (uint)current.InputIndex >= (uint)input.Length ||
                            (input[current.InputIndex] & 0xC0) != 0x80;
                        current = new(body, current.InputIndex, nextCounts, nextPositions,
                            false, false, aligned, frameMark);
                        adopted = true;
                        // The marker remains outside the child's backtrack boundary,
                        // including when its body has no forks of its own.
                        if (!stacks.PushFrame(new Utf8SliceFrame { ResumeKind = Utf8SliceResumeKind.Assertion }, ref invocation))
                            return new(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        current.FrameBase = stacks.FrameCount;
                        breach = invocation.Budget.TryNoteDepth((uint)stacks.FrameCount);
                        if (breach != Utf8ExecutionLimitKind.None)
                            return new(false, -1, breach, null, null);
                    }
                    finally
                    {
                        if (!adopted)
                        {
                            ReleaseIntArray(nextPositions, ref invocation);
                            ReleaseIntArray(nextCounts, ref invocation);
                        }
                    }
                    continue;
                }

                if (continuations.Count == 0) return outcome;
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
            ReleaseIntArray(current.Positions, ref invocation);
            ReleaseIntArray(current.Counts, ref invocation);
            current = continuation.Parent;
        }
    }
}
