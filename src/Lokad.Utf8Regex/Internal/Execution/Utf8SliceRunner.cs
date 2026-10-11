using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Resume actions for explicit slice backtrack frames.</summary>
internal enum Utf8SliceResumeKind : byte
{
    Alternation = 0,
    RepeatUnwind = 1,
    RepeatContinue = 2,
    Assertion = 3,
    Atomic = 4,
}

internal struct Utf8SliceFrame
{
    internal Utf8SliceResumeKind ResumeKind;
    internal int Target;
    internal int InputIndex;
    internal int TrailCount;
    internal int Aux;
    internal int Count;
    internal int JournalCount;
}

internal struct Utf8SliceTrailEntry
{
    internal int Slot;
    internal int OldValue;
    internal int NewValue;
    // Keep the journal entry at sixteen bytes as capture markers are added.
    private byte _flags;
    internal bool IsStart { readonly get => (_flags & 1) != 0; set => _flags = (byte)(value ? _flags | 1 : _flags & ~1); }
    internal bool IsHead { readonly get => (_flags & 2) != 0; set => _flags = (byte)(value ? _flags | 2 : _flags & ~2); }
    internal bool IsRestore { readonly get => (_flags & 4) != 0; set => _flags = (byte)(value ? _flags | 4 : _flags & ~4); }
    internal bool IsOpening { readonly get => (_flags & 8) != 0; set => _flags = (byte)(value ? _flags | 8 : _flags & ~8); }
    internal bool IsClosing { readonly get => (_flags & 16) != 0; set => _flags = (byte)(value ? _flags | 16 : _flags & ~16); }
}

internal struct Utf8SliceBalanceEntry
{
    internal int Slot;
    internal int Start;
    internal int End;
    internal int Prev;
}

internal struct Utf8SliceCountMutation
{
    internal int RepeatId;
    internal int OldCount;
    internal int OldPosition;
}

internal readonly struct Utf8SliceMatchOutcome
{
    internal Utf8SliceMatchOutcome(bool isMatch, int endIndex, Utf8ExecutionLimitKind breach, int[][]? captureStarts, int[][]? captureLengths)
    {
        IsMatch = isMatch;
        EndIndex = endIndex;
        Breach = breach;
        CaptureStarts = captureStarts;
        CaptureLengths = captureLengths;
    }

    internal bool IsMatch { get; }
    internal int EndIndex { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
    internal int[][]? CaptureStarts { get; }
    internal int[][]? CaptureLengths { get; }
}

internal readonly struct Utf8SliceSlots
{
    internal Utf8SliceSlots(int[] starts, int[] ends)
    {
        Starts = starts;
        Ends = ends;
    }

    internal int[] Starts { get; }
    internal int[] Ends { get; }
}

internal readonly struct Utf8SliceSearchOutcome
{
    internal Utf8SliceSearchOutcome(
        bool found, int startByte, int endByte,
        int[][]? captureStarts, int[][]? captureLengths, Utf8ExecutionLimitKind breach)
    {
        Found = found;
        StartByte = startByte;
        EndByte = endByte;
        CaptureStarts = captureStarts;
        CaptureLengths = captureLengths;
        Breach = breach;
    }

    internal bool Found { get; }
    internal int StartByte { get; }
    internal int EndByte { get; }
    internal int[][]? CaptureStarts { get; }
    internal int[][]? CaptureLengths { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
}

internal readonly struct Utf8SliceCountOutcome
{
    internal Utf8SliceCountOutcome(int count, Utf8ExecutionLimitKind breach)
    {
        Count = count;
        Breach = breach;
    }

    internal int Count { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
}

/// <summary>Explicit-frame backtracking runner over immutable slice programs.
/// One budgeted step per visited instruction; depth is the live frame count.
/// Repeat count/position writes go through a mutation journal so backtracking
/// past one repeat restores sibling repeats to their push-time state; frames,
/// trail chunks, journal chunks, tables, counts, and capture slots are
/// ledger-admitted before acquisition and released on every return path,
/// including breach, cancellation, and disposal. Capture slot pairs hold
/// start/end input indexes, -1 when unset; the trail journal restores them.</summary>
internal static partial class Utf8SliceRunner
{
    internal const int FrameChunkCapacity = 64;
    internal const int TrailChunkCapacity = 128;
    internal const int JournalChunkCapacity = 128;
    internal const int BalanceChunkCapacity = 128;
    internal const int ChunkTableCapacity = 4;

    internal static int FrameReserveBytes => checked((int)Utf8ConstructionBudget.ArrayBytes<Utf8SliceFrame>(FrameChunkCapacity));
    internal static int TrailReserveBytes => checked((int)Utf8ConstructionBudget.ArrayBytes<Utf8SliceTrailEntry>(TrailChunkCapacity));
    internal static int JournalReserveBytes => checked((int)Utf8ConstructionBudget.ArrayBytes<Utf8SliceCountMutation>(JournalChunkCapacity));
    internal static int BalanceReserveBytes => checked((int)Utf8ConstructionBudget.ArrayBytes<Utf8SliceBalanceEntry>(BalanceChunkCapacity));

    internal static Utf8SliceSlots RentSlots(int groups, ref Utf8SliceInvocation invocation)
    {
        if (TryRentSlots(groups, ref invocation, out var slots) != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned slice capture slots exceeded the invocation storage budget.");
        return slots;
    }

    internal static Utf8ExecutionLimitKind TryRentSlots(int groups, ref Utf8SliceInvocation invocation, out Utf8SliceSlots slots)
    {
        slots = default;
        if (groups == 0)
        {
            slots = new Utf8SliceSlots(Array.Empty<int>(), Array.Empty<int>());
            return Utf8ExecutionLimitKind.None;
        }

        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(groups) * 2);
        if (!invocation.Ledger.TryReserve(bytes))
        {
            return Utf8ExecutionLimitKind.WorkingBytes;
        }

        try
        {
            var starts = new int[groups];
            var ends = new int[groups];
            Array.Fill(starts, -1);
            Array.Fill(ends, -1);
            slots = new Utf8SliceSlots(starts, ends);
            return Utf8ExecutionLimitKind.None;
        }
        catch
        {
            invocation.Ledger.Release(bytes);
            throw;
        }
    }

    internal static void ReleaseSlots(Utf8SliceSlots slots, ref Utf8SliceInvocation invocation)
    {
        if (slots.Starts.Length != 0)
        {
            invocation.Ledger.Release(checked(Utf8ConstructionBudget.ArrayBytes<int>(slots.Starts.Length)
                + Utf8ConstructionBudget.ArrayBytes<int>(slots.Ends.Length)));
        }
    }

    internal static void ReleaseHistory(int[][]? starts, int[][]? lengths, ref Utf8SliceInvocation invocation)
    {
        if (starts is null || lengths is null)
        {
            return;
        }

        invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<int[]>(starts.Length));
        invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<int[]>(lengths.Length));
        for (var group = 0; group < starts.Length && group < lengths.Length; group++)
        {
            if (starts[group].Length != 0)
            {
                invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<int>(starts[group].Length));
            }

            if (lengths[group].Length != 0)
            {
                invocation.Ledger.Release(Utf8ConstructionBudget.ArrayBytes<int>(lengths[group].Length));
            }
        }
    }

    // Width of the scalar ending at rightEdge (validated input): ASCII is
    // one byte, longer scalars measure back from their lead. Mid-scalar
    // edges and out-of-range positions fail closed so mirrored right-to-left
    // attempts cannot report unaligned starts. Shared by every mirrored
    // consuming arm (six callers, so the single-caller debt ceiling does
    // not apply).
    private static bool TryGetScalarWidthEndingAt(ReadOnlySpan<byte> input, int rightEdge, out int width)
    {
        width = 0;
        if (rightEdge <= 0 || rightEdge > input.Length)
        {
            return false;
        }

        var leadIndex = rightEdge - 1;
        if (input[leadIndex] < 0x80)
        {
            width = 1;
            return true;
        }

        var steps = 0;
        while ((input[leadIndex] & 0xC0) == 0x80)
        {
            leadIndex--;
            steps++;
            if (leadIndex < 0 || steps > 3)
            {
                return false;
            }
        }

        var lead = input[leadIndex];
        if (lead < 0xC2 || lead >= 0xF5)
        {
            return false;
        }

        width = lead < 0xE0 ? 2 : lead < 0xF0 ? 3 : 4;
        return leadIndex + width == rightEdge;
    }

    private static bool TryReadScalar(ReadOnlySpan<byte> input, int edge, bool rightToLeft, out Rune scalar, out int width)
    {
        scalar = default;
        width = 0;
        if (rightToLeft)
        {
            if (!TryGetScalarWidthEndingAt(input, edge, out width)) return false;
            edge -= width;
        }
        if ((uint)edge >= (uint)input.Length) return false;
        if (Rune.DecodeFromUtf8(input.Slice(edge), out scalar, out var decodedWidth) != OperationStatus.Done) return false;
        if (rightToLeft && decodedWidth != width) return false;
        width = decodedWidth;
        return true;
    }

    private static bool IsWordScalarStartingAt(ReadOnlySpan<byte> input, int index, bool includeJoiners, bool ecma)
        => TryReadScalar(input, index, false, out var scalar, out _) &&
            Utf8SliceScalarClasses.IsWord(scalar, includeJoiners, ecma);

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        bool startIsScalarBoundary,
        ref Utf8SliceInvocation invocation)
    {
        return TryMatchAt(program, input, start, captureStarts, captureEnds, false, startIsScalarBoundary, ref invocation);
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        bool collectHistory,
        bool startIsScalarBoundary,
        ref Utf8SliceInvocation invocation)
    {
        return TryMatchAt(program, input, start, captureStarts, captureEnds, collectHistory, false, startIsScalarBoundary, ref invocation);
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        bool collectHistory,
        bool requireNonEmpty,
        bool startIsScalarBoundary,
        ref Utf8SliceInvocation invocation)
    {
        if (program.IsNonBacktracking)
        {
            if (!startIsScalarBoundary || (uint)start > (uint)input.Length ||
                start < input.Length && (input[start] & 0xC0) == 0x80)
                return new(false, -1, Utf8ExecutionLimitKind.None, null, null);
            var outcome = RunNonBacktracking(program, input, start, captureStarts, captureEnds,
                collectHistory, true, requireNonEmpty, ref invocation);
            return new(outcome.Found, outcome.EndByte, outcome.Breach, outcome.CaptureStarts, outcome.CaptureLengths);
        }
        var workspace = new AttemptWorkspace();
        try
        {
            return TryMatchAt(program, input, start, captureStarts, captureEnds,
                collectHistory, requireNonEmpty, startIsScalarBoundary, ref workspace, ref invocation);
        }
        finally { workspace.Release(ref invocation); }
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        bool collectHistory,
        bool requireNonEmpty,
        bool startIsScalarBoundary,
        ref AttemptWorkspace workspace,
        ref Utf8SliceInvocation invocation)
    {
        // Profile cursors can reuse this admitted workspace across candidate
        // attempts while preserving their own empty-match and anchor progress.
        if (program.IsNonBacktracking)
            return TryMatchAt(program, input, start, captureStarts, captureEnds,
                collectHistory, requireNonEmpty, startIsScalarBoundary, ref invocation);
        if (!startIsScalarBoundary || (uint)start > (uint)input.Length ||
            start < input.Length && (input[start] & 0xC0) == 0x80)
            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None, null, null);

        if (!workspace.Initialized)
        {
            var breach = workspace.Initialize(program, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None)
                return new(false, -1, breach, null, null);
        }
        var counts = workspace.Counts;
        var positions = workspace.Positions;
        var heads = workspace.Heads;
        Array.Fill(counts, -1);
        Array.Clear(positions);
        Array.Fill(captureStarts, -1);
        Array.Fill(captureEnds, -1);
        Array.Fill(heads, -1);
        workspace.Stacks.ResetAttempt();
        var completed = false;
        try
        {
            var attempt = RunFrom(program, input, start, captureStarts, captureEnds, counts, positions, heads, collectHistory, requireNonEmpty, startIsScalarBoundary, ref workspace.Stacks, ref invocation);
            if (attempt.IsMatch && program.RightToLeft)
            {
                // Mirrored runs record right edges at capture starts and left
                // edges at capture ends; projection and backreferences read
                // normalized spans while the trail stays raw and consistent.
                for (var slot = 0; slot < captureStarts.Length; slot++)
                {
                    if (captureStarts[slot] >= 0 && captureEnds[slot] >= 0 && captureEnds[slot] < captureStarts[slot])
                    {
                        var normalizedStart = captureEnds[slot];
                        captureEnds[slot] = captureStarts[slot];
                        captureStarts[slot] = normalizedStart;
                    }
                }
            }

            completed = attempt.IsMatch;
            return attempt;
        }
        finally
        {
            if (!completed)
            {
                // A committed positive assertion still belongs to this attempt;
                // failure or cancellation must not publish its capture slots.
                Array.Fill(captureStarts, -1);
                Array.Fill(captureEnds, -1);
            }
        }
    }

    // The complete subject is validated by the caller. Candidate positions and
    // results are bytes; derived interoperability coordinates belong to projection.
    internal static Utf8SliceSearchOutcome TryFindFirst(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        int[] captureStarts, int[] captureEnds, bool collectHistory,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        if ((uint)fromByte > (uint)input.Length ||
            fromByte < input.Length && (input[fromByte] & 0xC0) == 0x80)
            return new(false, -1, -1, null, null, Utf8ExecutionLimitKind.None);
        if (program.IsNonBacktracking)
            return RunNonBacktracking(program, input, fromByte, captureStarts, captureEnds,
                collectHistory, false, false, ref invocation);
        var workspace = new AttemptWorkspace();
        try
        {
            for (var position = fromByte; position >= 0; position = AdvancePastScalar(input, position, program.RightToLeft))
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, null, null, breach);
                invocation.Budget.ThrowIfCancellationRequested();
                var outcome = TryMatchAt(program, input, position, captureStarts, captureEnds, collectHistory, false, true, ref workspace, ref invocation);
                if (outcome.Breach != Utf8ExecutionLimitKind.None)
                    return new(false, -1, -1, null, null, outcome.Breach);
                if (outcome.IsMatch)
                    return new(true, Math.Min(position, outcome.EndIndex), Math.Max(position, outcome.EndIndex),
                        outcome.CaptureStarts, outcome.CaptureLengths, Utf8ExecutionLimitKind.None);
            }
            return new(false, -1, -1, null, null, Utf8ExecutionLimitKind.None);
        }
        finally { workspace.Release(ref invocation); }
    }

    // -1 is the exhausted sentinel in either direction; avoid Length + 1 overflow.
    internal static int AdvancePastScalar(ReadOnlySpan<byte> input, int position, bool reverse)
        => TryReadScalar(input, position, reverse, out _, out var width)
            ? position + (reverse ? -width : width) : -1;

    internal static Utf8SliceMatchOutcome IsMatch(
        Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int[] captureStarts, int[] captureEnds, ref Utf8SliceInvocation invocation)
    {
        var start = program.RightToLeft ? input.Length : 0;
        invocation.AnchorOriginByte = start;
        var outcome = TryFindFirst(program, input, start, captureStarts, captureEnds, false, ref invocation);
        return new(outcome.Found, outcome.EndByte, outcome.Breach, null, null);
    }

    // The caller validates the complete subject. Count needs no coordinate map:
    // attempts, successful endpoints and empty progress are all byte boundaries.
    internal static Utf8SliceCountOutcome CountFromBytes(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int fromByte,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        if ((uint)fromByte > (uint)input.Length ||
            fromByte < input.Length && (input[fromByte] & 0xC0) == 0x80)
            return new Utf8SliceCountOutcome(0, Utf8ExecutionLimitKind.None);
        var count = 0;
        var position = fromByte;
        invocation.AnchorOriginByte = fromByte;
        if (program.IsNonBacktracking)
        {
            Array.Fill(captureStarts, -1);
            Array.Fill(captureEnds, -1);
            var breach = Utf8NonBacktrackingSearchState.TryCreate(program, input, ref invocation, out var batch);
            if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
            if (batch is null) throw new InvalidOperationException("NonBacktracking preparation returned no state.");
            try
            {
                var boundary = batch.ResolveByteOffset(fromByte);
                while (true)
                {
                    var found = batch.FindFirst(program, ref boundary, captureStarts, captureEnds, false, ref invocation);
                    if (found.Breach != Utf8ExecutionLimitKind.None) return new(count, found.Breach);
                    if (!found.Found) break;
                    count++;
                }
                return new(count, Utf8ExecutionLimitKind.None);
            }
            finally { batch.Release(ref invocation); }
        }
        var workspace = new AttemptWorkspace();
        try
        {
            while (true)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None)
                    return new Utf8SliceCountOutcome(count, breach);
                invocation.Budget.ThrowIfCancellationRequested();
                var outcome = TryMatchAt(program, input, position, captureStarts, captureEnds, false, false, true, ref workspace, ref invocation);
                if (outcome.Breach != Utf8ExecutionLimitKind.None)
                    return new Utf8SliceCountOutcome(count, outcome.Breach);
                invocation.Budget.ThrowIfCancellationRequested();
                if (outcome.IsMatch)
                {
                    count++;
                    invocation.AnchorOriginByte = outcome.EndIndex;
                    if (outcome.EndIndex != position)
                    {
                        position = outcome.EndIndex;
                        continue;
                    }
                }
                else if (program.RightToLeft && program.HasContiguousAnchor)
                    break;
                position = AdvancePastScalar(input, position, program.RightToLeft);
                if (position < 0) break;
            }
            return new Utf8SliceCountOutcome(count, Utf8ExecutionLimitKind.None);
        }

        finally { workspace.Release(ref invocation); }
    }

    internal static Utf8SliceCountOutcome Count(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        return CountFromBytes(program, input, program.RightToLeft ? input.Length : 0, captureStarts, captureEnds, ref invocation);
    }

    private static Utf8SliceMatchOutcome RunSegment(
        ReadOnlySpan<byte> input, int[] captureStarts, int[] captureEnds, int[] balanceHeads,
        ref AssertionCursor cursor, bool resumeFailure,
        ref ChunkedStacks stacks, ref Utf8SliceInvocation invocation, out bool suspended)
    {
        suspended = false;
        var program = cursor.Program;
        var start = cursor.Start;
        var counts = cursor.Counts;
        var positions = cursor.Positions;
        var collectHistory = cursor.CollectHistory;
        var requireNonEmpty = cursor.RequireNonEmpty;
        var startIsScalarBoundary = cursor.Aligned;
        var frameBase = cursor.FrameBase;
        var instructions = program.Instructions;
        var programIndex = cursor.ProgramIndex;
        var inputIndex = cursor.InputIndex;
        while (true)
        {
            var failed = resumeFailure;
            if (resumeFailure)
            {
                resumeFailure = false;
                goto Backtrack;
            }
            var stepBreach = invocation.TryChargeStep();
            if (stepBreach != Utf8ExecutionLimitKind.None)
            {
                return new Utf8SliceMatchOutcome(false, -1, stepBreach, null, null);
            }

            var instruction = instructions[programIndex];
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Literal:
                    // Literal atoms consume one complete scalar in either direction.
                    var literalIndex = inputIndex;
                    var literalWidth = 0;
                    if (program.RightToLeft)
                    {
                        if (TryGetScalarWidthEndingAt(input, inputIndex, out literalWidth))
                            literalIndex -= literalWidth;
                    }
                    else if (inputIndex < input.Length && (input[inputIndex] & 0xC0) != 0x80)
                    {
                        var lead = input[inputIndex];
                        literalWidth = lead < 0x80 ? 1 : lead < 0xE0 ? 2 : lead < 0xF0 ? 3 : 4;
                    }

                    if (literalWidth == 0)
                    {
                        failed = true;
                        break;
                    }

                    var literalLead = input[literalIndex];
                    bool literalMatches;
                    if (literalLead < 0x80 && instruction.A < 0x80)
                    {
                        // Retain the byte-only ASCII fast path.
                        literalMatches = instruction.B == 0
                            ? literalLead == instruction.A
                            : program.CaseBehavior == Utf8ScalarCaseBehavior.Turkic &&
                                ((literalLead | 0x20) == 'i' || (instruction.A | 0x20) == 'i')
                                ? literalLead == instruction.A
                            : Internal.Search.AsciiSearch.FoldCase(literalLead) ==
                                Internal.Search.AsciiSearch.FoldCase((byte)instruction.A);
                    }
                    else
                    {
                        if (Rune.DecodeFromUtf8(input.Slice(literalIndex), out var literalScalar, out _) != OperationStatus.Done)
                        {
                            failed = true;
                            break;
                        }

                        literalMatches = literalScalar.Value == instruction.A;
                        if (!literalMatches && instruction.B != 0)
                        {
                            literalMatches = Rune.IsValid(instruction.A) &&
                                Utf8SliceCaseEquivalences.ScalarsEquivalent(literalScalar, new Rune(instruction.A), program.CaseBehavior);
                        }
                    }

                    if (literalMatches)
                    {
                        inputIndex += program.RightToLeft ? -literalWidth : literalWidth;
                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }
                    break;
                case Utf8SliceInstructionKind.Set:
                    if (TryReadScalar(input, inputIndex, program.RightToLeft, out var setScalar, out var setWidth) &&
                        Utf8SliceScalarClasses.MatchesMask(setScalar, program.SetMasks[instruction.A], instruction.C != 0, program.CaseBehavior) != (instruction.B != 0))
                    {
                        inputIndex += program.RightToLeft ? -setWidth : setWidth;
                        programIndex++;
                    }
                    else failed = true;
                    break;

                case Utf8SliceInstructionKind.Fork:
                    if (!stacks.PushFrame(
                        new Utf8SliceFrame
                        {
                            ResumeKind = instruction.B == 0 ? Utf8SliceResumeKind.Alternation : instruction.B == 1 ? Utf8SliceResumeKind.Assertion : Utf8SliceResumeKind.Atomic,
                            Target = instruction.A,
                            InputIndex = inputIndex,
                            TrailCount = stacks.TrailCount,
                            JournalCount = stacks.JournalCount,
                        },
                        ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                    }

                    if (invocation.Budget.TryNoteDepth((uint)stacks.FrameCount) != Utf8ExecutionLimitKind.None)
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.Depth, null, null);
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.Jump:
                    programIndex = instruction.A;
                    break;
                case Utf8SliceInstructionKind.RepeatGreedy:
                case Utf8SliceInstructionKind.RepeatLazy:
                    {
                        var repeatId = instruction.B;
                        var count = counts[repeatId];
                        if (count < 0)
                        {
                            if (!SetRepeat(counts, positions, repeatId, 0, -1, ref stacks, ref invocation))
                            {
                                return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                            }

                            count = 0;
                        }

                        var madeNoProgress = count > 0 && positions[repeatId] == inputIndex;
                        var minimum = instruction.C;
                        var maximum = instruction.D;
                        var greedy = instruction.Kind != Utf8SliceInstructionKind.RepeatLazy;
                        // Repetition counts atom iterations, independently of UTF-8 or UTF-16 width.
                        if (count < minimum)
                        {
                            if (!SetRepeat(counts, positions, repeatId, count + 1, inputIndex, ref stacks, ref invocation))
                            {
                                return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                            }

                            programIndex++;
                            break;
                        }

                        if (count >= maximum || madeNoProgress)
                        {
                            programIndex = instruction.A;
                            break;
                        }

                        var frame = new Utf8SliceFrame
                        {
                            InputIndex = inputIndex,
                            TrailCount = stacks.TrailCount,
                            JournalCount = stacks.JournalCount,
                            Aux = repeatId,
                        };
                        if (greedy)
                        {
                            frame.ResumeKind = Utf8SliceResumeKind.RepeatUnwind;
                            frame.Target = instruction.A;
                            frame.Count = count;
                        }
                        else
                        {
                            frame.ResumeKind = Utf8SliceResumeKind.RepeatContinue;
                            frame.Target = programIndex + 1;
                            frame.Count = count + 1;
                        }

                        if (!stacks.PushFrame(frame, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                        }

                        if (invocation.Budget.TryNoteDepth((uint)stacks.FrameCount) != Utf8ExecutionLimitKind.None)
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.Depth, null, null);
                        }

                        if (greedy)
                        {
                            if (!SetRepeat(counts, positions, repeatId, count + 1, inputIndex, ref stacks, ref invocation))
                            {
                                return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                            }

                            programIndex++;
                        }
                        else
                        {
                            programIndex = instruction.A;
                        }

                        break;
                    }

                case Utf8SliceInstructionKind.RepeatEnd:
                    programIndex = instruction.A;
                    break;
                case Utf8SliceInstructionKind.RepeatExit:
                    if (!SetRepeat(counts, positions, instruction.A, -1, -1, ref stacks, ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                    if (program.UsesCaptureOpeningJournal)
                    {
                        if (!stacks.PushTrailEntry(new Utf8SliceTrailEntry { Slot = instruction.A, NewValue = inputIndex, IsOpening = true }, ref invocation))
                            return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                        programIndex++;
                        break;
                    }
                    if (!stacks.PushTrail(instruction.A, captureStarts[instruction.A], inputIndex, true, ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                    }

                    captureStarts[instruction.A] = inputIndex;
                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    if (program.UsesCaptureOpeningJournal)
                    {
                        // An opening capture does not overwrite the last completed one.
                        // This preserves nested aliases and references inside their bodies.
                        if (!stacks.TryFindCaptureOpening(instruction.A, ref invocation, out var opening, out var openingBreach))
                            return new Utf8SliceMatchOutcome(false, -1, openingBreach, null, null);
                        if (!stacks.PushRestoreTrail(instruction.A, captureStarts[instruction.A], opening, captureEnds[instruction.A], inputIndex, ref invocation) ||
                            !stacks.PushTrailEntry(new Utf8SliceTrailEntry { Slot = instruction.A, OldValue = opening, NewValue = inputIndex, IsClosing = true }, ref invocation))
                            return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                        captureStarts[instruction.A] = opening;
                        captureEnds[instruction.A] = inputIndex;
                    }
                    else
                    {
                        if (!stacks.PushTrail(instruction.A, captureEnds[instruction.A], inputIndex, false, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                        }
                        captureEnds[instruction.A] = inputIndex;
                    }
                    if (program.HasBalancing)
                    {
                        // Balancing stacks mirror every capture so pops can
                        // restore previous tops; the head journal makes the
                        // push vanish on backtrack like any trail write.
                        if (!stacks.PushBalance(instruction.A, captureStarts[instruction.A], inputIndex, balanceHeads[instruction.A], ref invocation) ||
                            !stacks.PushHeadTrail(instruction.A, balanceHeads[instruction.A], stacks.BalanceCount - 1, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                        }

                        balanceHeads[instruction.A] = stacks.BalanceCount - 1;
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.Dot:
                    // Mirrored dots consume the scalar ending at the attempt edge.
                    if (program.RightToLeft)
                    {
                        if (TryGetScalarWidthEndingAt(input, inputIndex, out var rtlDotWidth) &&
                            (instruction.A != 0 || input[inputIndex - 1] != (byte)'\n'))
                        {
                            inputIndex -= rtlDotWidth;
                            programIndex++;
                        }
                        else
                        {
                            failed = true;
                        }

                        break;
                    }

                    // Dots consume one scalar: ASCII and BMP leads take
                    // their scalar, astral leads take all four bytes.
                    // Continuation bytes fail the attempt, so byte-stepping
                    // drivers cannot report unaligned starts. Inputs are
                    // validated before execution, hence lead widths stay
                    // in bounds without revalidation here.
                    if (inputIndex < input.Length &&
                        (instruction.A != 0 || input[inputIndex] != (byte)'\n') &&
                        (input[inputIndex] & 0xC0) != 0x80)
                    {
                        var lead = input[inputIndex];
                        if (lead < 0x80)
                        {
                            inputIndex++;
                        }
                        else if (lead < 0xE0)
                        {
                            inputIndex += 2;
                        }
                        else if (lead < 0xF0)
                        {
                            inputIndex += 3;
                        }
                        else
                        {
                            inputIndex += 4;
                        }

                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Anchor:
                    // Zero-width edges: beginning holds at the input origin
                    // or (multiline `^`) just after a line feed; lax end
                    // (`$`, `\Z`) holds at the input end, before a trailing
                    // line feed, or (multiline `$`) just before any line
                    // feed; strict end (`\z`) holds only at the input end.
                    // Line feeds are single bytes, so these checks are
                    // encoding-indifferent and need no astral routing.
                    // Guards order short-circuit before any indexing.
                    var anchorMultiline = instruction.B != 0;
                    bool anchorHolds;
                    if (instruction.A == 0)
                    {
                        // Origin assertions require a scalar-aligned attempt.
                        anchorHolds = (inputIndex == 0 && startIsScalarBoundary) ||
                            (anchorMultiline && inputIndex > 0 && input[inputIndex - 1] == (byte)'\n');
                    }
                    else if (instruction.A == 2)
                    {
                        anchorHolds = inputIndex == input.Length;
                    }
                    else if (instruction.A == 3)
                    {
                        // Contiguous matches pin to the scan start (BCL startat),
                        // defaulting to the attempt start when no driver set an origin.
                        var origin = invocation.AnchorOriginByte;
                        anchorHolds = inputIndex == (origin < 0 ? start : origin);
                    }
                    else if (inputIndex == input.Length)
                    {
                        anchorHolds = true;
                    }
                    else if (anchorMultiline)
                    {
                        anchorHolds = input[inputIndex] == (byte)'\n';
                    }
                    else
                    {
                        anchorHolds = inputIndex + 1 == input.Length && input[inputIndex] == (byte)'\n';
                    }

                    if (anchorHolds)
                    {
                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Boundary:
                    // Zero-width word edges: `\b` holds on a word/non-word
                    // transition, `\B` where both sides agree. Both require
                    // scalar-aligned positions and classify complete neighbors.
                    if (!startIsScalarBoundary)
                    {
                        failed = true;
                    }
                    else if (inputIndex <= input.Length &&
                        (inputIndex == input.Length || (input[inputIndex] & 0xC0) != 0x80))
                    {
                        var beforeWord = false;
                        if (inputIndex > 0)
                        {
                            var previous = inputIndex - 1;
                            while (previous > 0 && (input[previous] & 0xC0) == 0x80)
                            {
                                previous--;
                            }

                            beforeWord = IsWordScalarStartingAt(input, previous, true, instruction.B != 0);
                        }

                        var afterWord = inputIndex < input.Length && IsWordScalarStartingAt(input, inputIndex, true, instruction.B != 0);
                        if ((beforeWord != afterWord) == (instruction.A != 0))
                        {
                            programIndex++;
                        }
                        else
                        {
                            failed = true;
                        }
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Shorthand:
                case Utf8SliceInstructionKind.Category:
                case Utf8SliceInstructionKind.CategorySet:
                case Utf8SliceInstructionKind.Hybrid:
                    {
                        if (!TryReadScalar(input, inputIndex, program.RightToLeft, out var classScalar, out var classWidth))
                        {
                            failed = true;
                            break;
                        }

                        bool classHit;
                        if (instruction.Kind == Utf8SliceInstructionKind.Shorthand)
                        {
                            classHit = Utf8SliceScalarClasses.MatchesShorthand(classScalar, (Utf8SliceShorthand)instruction.A);
                        }
                        else if (instruction.Kind == Utf8SliceInstructionKind.Category)
                        {
                            classHit = Utf8SliceScalarClasses.MatchesTable(classScalar, program.CategoryTables[instruction.A],
                                (instruction.B & 2) != 0, (instruction.B & 1) != 0, program.CaseBehavior);
                        }
                        else
                        {
                            var categorySet = instruction.Kind == Utf8SliceInstructionKind.CategorySet;
                            var classMembers = categorySet ? program.CategorySetMembers[instruction.D] : null;
                            if (classMembers is { Length: 1 } && classMembers[0].Table.ClassProgram is { } classProgram)
                            {
                                classHit = classProgram.TryMatch(classScalar, program.CaseBehavior, ref invocation, out var classBreach);
                                if (classBreach != Utf8ExecutionLimitKind.None)
                                    return new Utf8SliceMatchOutcome(false, -1, classBreach, null, null);
                            }
                            else
                            {
                                var fold = categorySet ? (instruction.B & 2) != 0 : instruction.D != 0;
                                var memberHit = Utf8SliceScalarClasses.MatchesMask(classScalar, program.SetMasks[instruction.A], fold, program.CaseBehavior) ||
                                    Utf8SliceScalarClasses.MatchesMembers(classScalar, instruction.C);
                                if (!memberHit && classMembers is not null)
                                {
                                    foreach (var member in classMembers)
                                    {
                                        if (Utf8SliceScalarClasses.MatchesTable(classScalar, member.Table, fold, member.Negated, program.CaseBehavior))
                                        {
                                            memberHit = true;
                                            break;
                                        }
                                    }
                                }
                                classHit = memberHit != ((instruction.B & 1) != 0);
                            }
                        }

                        if (classHit)
                        {
                            inputIndex += program.RightToLeft ? -classWidth : classWidth;
                            programIndex++;
                        }
                        else failed = true;
                        break;
                    }

                case Utf8SliceInstructionKind.Backref:
                    // ECMAScript permits a defined group that has not completed
                    // a capture yet to match an empty reference.
                    if (instruction.C != 0 && (captureStarts[instruction.A] < 0 || captureEnds[instruction.A] < 0))
                    {
                        programIndex++;
                        break;
                    }
                    // Mirrored backreferences compare the captured span backward: the
                    // run records right edges at capture starts and left edges at
                    // capture ends, so an unmatched (or in-progress) group fails on
                    // the mirrored guard and surviving spans compare from their ends.
                    // Byte comparison stays encoding-indifferent; the
                    // case-insensitive walk folds both sides with backward scalar
                    // widths like the forward arm.
                    if (program.RightToLeft)
                    {
                        if (captureEnds[instruction.A] < 0 ||
                            captureStarts[instruction.A] < 0)
                        {
                            failed = true;
                            break;
                        }

                        var rtlReferStart = Math.Min(captureStarts[instruction.A], captureEnds[instruction.A]);
                        var rtlReferEnd = Math.Max(captureStarts[instruction.A], captureEnds[instruction.A]);
                        var rtlReferLength = rtlReferEnd - rtlReferStart;
                        if (instruction.B != 0)
                        {
                            var rtlCapIndex = rtlReferEnd;
                            var rtlBackIndex = inputIndex;
                            var rtlBackFailed = false;
                            while (rtlCapIndex > rtlReferStart)
                            {
                                var rtlBackCharge = invocation.TryChargeStep();
                                if (rtlBackCharge != Utf8ExecutionLimitKind.None)
                                {
                                    return new Utf8SliceMatchOutcome(false, -1, rtlBackCharge, null, null);
                                }

                                if (rtlBackIndex <= 0)
                                {
                                    rtlBackFailed = true;
                                    break;
                                }

                                if (!TryReadScalar(input, rtlCapIndex, true, out var rtlCaptureScalar, out var rtlCapWidth) ||
                                    rtlCapIndex - rtlCapWidth < rtlReferStart ||
                                    !TryReadScalar(input, rtlBackIndex, true, out var rtlInputScalar, out var rtlInWidth) ||
                                    !BackreferenceScalarsEqual(program, instruction, rtlCaptureScalar, rtlInputScalar))
                                {
                                    rtlBackFailed = true;
                                    break;
                                }
                                rtlCapIndex -= rtlCapWidth;
                                rtlBackIndex -= rtlInWidth;
                            }

                            if (rtlBackFailed)
                            {
                                failed = true;
                            }
                            else
                            {
                                inputIndex = rtlBackIndex;
                                programIndex++;
                            }

                            break;
                        }

                        if (rtlReferLength > inputIndex)
                        {
                            failed = true;
                            break;
                        }

                        var rtlCompared = 0;
                        while (rtlCompared < rtlReferLength)
                        {
                            var rtlCharge = invocation.TryChargeStep();
                            if (rtlCharge != Utf8ExecutionLimitKind.None)
                            {
                                return new Utf8SliceMatchOutcome(false, -1, rtlCharge, null, null);
                            }

                            if (input[rtlReferEnd - 1 - rtlCompared] != input[inputIndex - 1 - rtlCompared])
                            {
                                break;
                            }

                            rtlCompared++;
                        }

                        if (rtlCompared < rtlReferLength)
                        {
                            failed = true;
                        }
                        else
                        {
                            inputIndex -= rtlReferLength;
                            programIndex++;
                        }

                        break;
                    }

                    // Ordinal backreference: an unmatched (or in-progress)
                    // group fails, otherwise the captured bytes must repeat
                    // verbatim at the current position. Scalar-aligned captures
                    // have a unique strict UTF-8 encoding. Each compared byte
                    // charges work so long captures stay bounded and
                    // cancellable like the rest of the engine.
                    if (captureStarts[instruction.A] < 0 ||
                        captureEnds[instruction.A] < 0)
                    {
                        failed = true;
                        break;
                    }

                    {
                        var referStart = Math.Min(captureStarts[instruction.A], captureEnds[instruction.A]);
                        var referEnd = Math.Max(captureStarts[instruction.A], captureEnds[instruction.A]);
                        var referLength = referEnd - referStart;
                        if (instruction.B != 0)
                        {
                            // Compare complete captured/input scalars using the same
                            // invariant equivalence as literal atoms. Each side advances
                            // by its own UTF-8 width (for example k versus Kelvin).
                            var capIndex = referStart;
                            var backIndex = inputIndex;
                            var backFailed = false;
                            while (capIndex < referEnd)
                            {
                                var backCharge = invocation.TryChargeStep();
                                if (backCharge != Utf8ExecutionLimitKind.None)
                                {
                                    return new Utf8SliceMatchOutcome(false, -1, backCharge, null, null);
                                }

                                if (backIndex >= input.Length)
                                {
                                    backFailed = true;
                                    break;
                                }

                                if (!TryReadScalar(input, capIndex, false, out var captureScalar, out var capWidth) ||
                                    capIndex + capWidth > referEnd ||
                                    !TryReadScalar(input, backIndex, false, out var inputScalar, out var inWidth) ||
                                    !BackreferenceScalarsEqual(program, instruction, captureScalar, inputScalar))
                                {
                                    backFailed = true;
                                    break;
                                }
                                capIndex += capWidth;
                                backIndex += inWidth;
                            }

                            if (backFailed)
                            {
                                failed = true;
                            }
                            else
                            {
                                inputIndex = backIndex;
                                programIndex++;
                            }

                            break;
                        }

                        if (referLength > input.Length - inputIndex)
                        {
                            failed = true;
                            break;
                        }

                        var compared = 0;
                        while (compared < referLength)
                        {
                            var charge = invocation.TryChargeStep();
                            if (charge != Utf8ExecutionLimitKind.None)
                            {
                                return new Utf8SliceMatchOutcome(false, -1, charge, null, null);
                            }

                            if (input[referStart + compared] != input[inputIndex + compared])
                            {
                                break;
                            }

                            compared++;
                        }

                        if (compared < referLength)
                        {
                            failed = true;
                        }
                        else
                        {
                            inputIndex += referLength;
                            programIndex++;
                        }
                    }

                    break;
                case Utf8SliceInstructionKind.TestGroup:
                    // One-shot group test (BCL IsMatched: participated, even empty;
                    // in-progress and unset groups test false, group zero never matches):
                    // jump to the no-branch on false, fall into yes on true. Downstream
                    // failure never retries the other branch (BCL commits to the test).
                    // Direction-free: mirrored runs record inverted spans, but both ends
                    // stay non-negative exactly when the group participated.
                    if (instruction.A < 0 || captureStarts[instruction.A] < 0 || captureEnds[instruction.A] < 0)
                    {
                        programIndex = instruction.B;
                    }
                    else
                    {
                        programIndex++;
                    }

                    break;
                case Utf8SliceInstructionKind.Fail:
                    failed = true;
                    break;
                case Utf8SliceInstructionKind.AtomicCommit:
                    {
                        // Possessive commit: discard every frame up to and
                        // including the atomic fork (a surviving body must not
                        // resume its alternatives), keeping input, captures,
                        // and repeat state.
                        var foundAtomic = false;
                        while (stacks.FrameCount > frameBase && stacks.PopFrame(out var committed))
                        {
                            if (committed.ResumeKind == Utf8SliceResumeKind.Atomic)
                            {
                                foundAtomic = true;
                                break;
                            }
                        }

                        if (!foundAtomic)
                        {
                            failed = true;
                            break;
                        }

                        programIndex++;
                        break;
                    }
                case Utf8SliceInstructionKind.Commit:
                    {
                        // Assertion commit: discard every frame up to and
                        // including the assertion fork (a surviving body must
                        // not resume its alternatives), rewind to the
                        // assertion position (zero width), restore repeat
                        // state to the fork mark, and keep capture writes.
                        var journalMark = -1;
                        var rewindTo = -1;
                        while (stacks.FrameCount > frameBase && stacks.PopFrame(out var committed))
                        {
                            if (committed.ResumeKind == Utf8SliceResumeKind.Assertion)
                            {
                                journalMark = committed.JournalCount;
                                rewindTo = committed.InputIndex;
                                break;
                            }
                        }

                        if (journalMark < 0)
                        {
                            failed = true;
                            break;
                        }

                        inputIndex = rewindTo;
                        stacks.TruncateJournal(journalMark, counts, positions);
                        programIndex++;
                        break;
                    }
                case Utf8SliceInstructionKind.Lookbehind:
                case Utf8SliceInstructionKind.Lookahead:
                    // The driver owns child arrays and saves this exact edge;
                    // returning here suspends, rather than recursively entering,
                    // the assertion's independent instruction stream.
                    cursor.ProgramIndex = programIndex;
                    cursor.InputIndex = inputIndex;
                    suspended = true;
                    return default;
                case Utf8SliceInstructionKind.BalanceStart:
                    // Retain the body's opening without testing its source yet:
                    // the body can establish the capture popped at successful close.
                    if (!stacks.PushTrailEntry(new Utf8SliceTrailEntry {
                            Slot = instruction.B, NewValue = inputIndex, IsOpening = true }, ref invocation))
                        return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                    programIndex++;
                    break;

                case Utf8SliceInstructionKind.BalancePop:
                    {
                        // Check at successful close: the body may have consumed
                        // the source through a nested balancing group. Pop its
                        // current completed capture, then project the interval
                        // against the completed body independently of direction.
                        var popSlot = instruction.A;
                        var captureSlot = instruction.B;
                        if (balanceHeads[popSlot] < 0)
                        {
                            failed = true;
                            break;
                        }
                        var top = stacks.BalanceEntryAt(balanceHeads[popSlot]);
                        var previousStart = -1;
                        var previousEnd = -1;
                        if (top.Prev >= 0)
                        {
                            var below = stacks.BalanceEntryAt(top.Prev);
                            previousStart = below.Start;
                            previousEnd = below.End;
                        }
                        if (!stacks.PushRestoreTrail(popSlot, captureStarts[popSlot], previousStart,
                                captureEnds[popSlot], previousEnd, ref invocation) ||
                            !stacks.PushHeadTrail(popSlot, balanceHeads[popSlot], top.Prev, ref invocation))
                            return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                        captureStarts[popSlot] = previousStart;
                        captureEnds[popSlot] = previousEnd;
                        balanceHeads[popSlot] = top.Prev;
                        if (captureSlot >= 0)
                        {
                            if (!stacks.TryFindCaptureOpening(captureSlot, ref invocation,
                                    out var opening, out var openingBreach))
                                return new Utf8SliceMatchOutcome(false, -1, openingBreach, null, null);
                            var sourceStart = Math.Min(top.Start, top.End);
                            var sourceEnd = Math.Max(top.Start, top.End);
                            var gapStart = Math.Min(opening, inputIndex);
                            var gapEnd = Math.Max(opening, inputIndex);
                            // Capture the gap for disjoint spans or their
                            // intersection when the body overlaps its source.
                            if (gapStart >= sourceEnd)
                            {
                                gapEnd = gapStart;
                                gapStart = sourceEnd;
                            }
                            else if (gapEnd <= sourceStart)
                            {
                                gapStart = gapEnd;
                                gapEnd = sourceStart;
                            }
                            else
                            {
                                gapStart = Math.Max(gapStart, sourceStart);
                                gapEnd = Math.Min(gapEnd, sourceEnd);
                            }
                            if (!stacks.PushRestoreTrail(captureSlot, captureStarts[captureSlot], gapStart,
                                    captureEnds[captureSlot], gapEnd, ref invocation) ||
                                !stacks.PushTrailEntry(new Utf8SliceTrailEntry {
                                    Slot = captureSlot, OldValue = gapStart, NewValue = gapEnd, IsClosing = true }, ref invocation) ||
                                !stacks.PushBalance(captureSlot, gapStart, gapEnd, balanceHeads[captureSlot], ref invocation) ||
                                !stacks.PushHeadTrail(captureSlot, balanceHeads[captureSlot], stacks.BalanceCount - 1, ref invocation))
                                return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                            captureStarts[captureSlot] = gapStart;
                            captureEnds[captureSlot] = gapEnd;
                            balanceHeads[captureSlot] = stacks.BalanceCount - 1;
                        }
                        programIndex++;
                        break;
                    }

                case Utf8SliceInstructionKind.Accept:
                    if (requireNonEmpty && inputIndex == start)
                    {
                        failed = true;
                        break;
                    }

                    if (!collectHistory)
                    {
                        return new Utf8SliceMatchOutcome(true, inputIndex, Utf8ExecutionLimitKind.None, null, null);
                    }

                    var historyBreach = stacks.MaterializeHistory(program.GroupCount, ref invocation, out var historyStarts, out var historyLengths);
                    if (historyBreach != Utf8ExecutionLimitKind.None)
                        return new Utf8SliceMatchOutcome(false, -1, historyBreach, null, null);

                    return new Utf8SliceMatchOutcome(true, inputIndex, Utf8ExecutionLimitKind.None, historyStarts, historyLengths);
                default:
                    throw new InvalidOperationException($"Unsupported slice instruction {instruction.Kind}.");
            }

        Backtrack:
            if (failed)
            {
                if (stacks.FrameCount == frameBase || !stacks.PopFrame(out var popped))
                {
                    return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None, null, null);
                }

                stacks.TruncateJournal(popped.JournalCount, counts, positions);
                stacks.TruncateTrail(popped.TrailCount, captureStarts, captureEnds, balanceHeads);
                switch (popped.ResumeKind)
                {
                    case Utf8SliceResumeKind.Alternation:
                    case Utf8SliceResumeKind.Assertion:
                    case Utf8SliceResumeKind.Atomic:
                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                    case Utf8SliceResumeKind.RepeatUnwind:
                        if (!SetRepeat(counts, positions, popped.Aux, popped.Count, popped.InputIndex, ref stacks, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                        }

                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                    default:
                        if (!SetRepeat(counts, positions, popped.Aux, popped.Count, popped.InputIndex, ref stacks, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, stacks.Breach, null, null);
                        }

                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                }
            }
        }
    }

    private static bool SetRepeat(
        int[] counts,
        int[] positions,
        int repeatId,
        int count,
        int position,
        ref ChunkedStacks stacks,
        ref Utf8SliceInvocation invocation)
    {
        if (!stacks.PushJournal(repeatId, counts[repeatId], positions[repeatId], ref invocation))
        {
            return false;
        }

        counts[repeatId] = count;
        positions[repeatId] = position;
        return true;
    }

    // One search/Count call retains its admitted scratch between candidates.
    // State is reset for each attempt; no workspace escapes or crosses invocations.
    internal struct AttemptWorkspace
    {
        internal int[] Counts;
        internal int[] Positions;
        internal int[] Heads;
        internal ChunkedStacks Stacks;
        internal bool Initialized;
        private ulong _reservation;

        public AttemptWorkspace()
        {
            Counts = [];
            Positions = [];
            Heads = [];
            Stacks = default;
            Initialized = false;
            _reservation = 0;
        }

        internal Utf8ExecutionLimitKind Initialize(Utf8SliceProgram program, ref Utf8SliceInvocation invocation)
        {
            var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(program.RepeatCount) * 2
                + Utf8ConstructionBudget.ArrayBytes<int>(program.GroupCount));
            if (!invocation.Ledger.TryReserve(bytes)) return Utf8ExecutionLimitKind.WorkingBytes;
            _reservation = bytes;
            var breach = invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (program.RepeatCount != 0)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                Counts = new int[program.RepeatCount];
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                Positions = new int[program.RepeatCount];
            }
            if (program.GroupCount != 0)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                Heads = new int[program.GroupCount];
            }
            Initialized = true;
            return Utf8ExecutionLimitKind.None;
        }

        internal void Release(ref Utf8SliceInvocation invocation)
        {
            Stacks.ReleaseAll(ref invocation);
            invocation.Ledger.Release(_reservation);
            _reservation = 0;
            Heads = [];
            Positions = [];
            Counts = [];
            Initialized = false;
        }
    }

    internal struct ChunkedStacks
    {
        private Utf8SliceFrame[][] _frameTables;
        private Utf8SliceTrailEntry[][] _trailTables;
        private Utf8SliceCountMutation[][] _journalTables;
        private Utf8SliceBalanceEntry[][] _balanceTables;
        private int _frameCount;
        private int _trailCount;
        private int _journalCount;
        private int _balanceCount;
        private ulong _reservedBytes;
        internal Utf8ExecutionLimitKind Breach;

        internal int FrameCount => _frameCount;
        internal int TrailCount => _trailCount;
        internal int JournalCount => _journalCount;
        internal int BalanceCount => _balanceCount;

        internal bool PushFrame(Utf8SliceFrame frame, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _frameTables, ref _frameCount, FrameChunkCapacity, frame, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool PushTrail(int slot, int oldValue, int newValue, bool isStart, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldValue, NewValue = newValue, IsStart = isStart }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool PushTrailEntry(Utf8SliceTrailEntry entry, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                entry, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool TryFindCaptureOpening(int slot, ref Utf8SliceInvocation invocation, out int opening, out Utf8ExecutionLimitKind breach)
        {
            var closed = 0;
            opening = -1;
            breach = Utf8ExecutionLimitKind.None;
            for (var index = _trailCount - 1; index >= 0; index--)
            {
                breach = invocation.TryChargeStep();
                if (breach != Utf8ExecutionLimitKind.None) return false;
                var entry = TrailEntryAt(index);
                if (entry.Slot != slot) continue;
                if (entry.IsClosing) closed++;
                else if (entry.IsOpening)
                {
                    if (closed != 0) closed--;
                    else
                    {
                        opening = entry.NewValue;
                        return true;
                    }
                }
            }
            throw new InvalidOperationException("Capture close has no surviving opening marker.");
        }

        internal bool PushRestoreTrail(int slot, int oldStart, int newStart, int oldEnd, int newEnd, ref Utf8SliceInvocation invocation)
        {
            // Balance-pop slot restores: live slot state for backtrack
            // truncation, but no capture record (the popped capture
            // drains through its head entry; the restored top stays
            // recorded from its own capture time).
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldStart, NewValue = newStart, IsStart = true, IsRestore = true }, ref invocation, ref _reservedBytes, out Breach) &&
                Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldEnd, NewValue = newEnd, IsStart = false, IsRestore = true }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool PushJournal(int repeatId, int oldCount, int oldPosition, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _journalTables, ref _journalCount, JournalChunkCapacity,
                new Utf8SliceCountMutation { RepeatId = repeatId, OldCount = oldCount, OldPosition = oldPosition }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool PushBalance(int slot, int start, int end, int prev, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _balanceTables, ref _balanceCount, BalanceChunkCapacity,
                new Utf8SliceBalanceEntry { Slot = slot, Start = start, End = end, Prev = prev }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal bool PushHeadTrail(int slot, int oldHead, int newHead, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldHead, NewValue = newHead, IsStart = false, IsHead = true }, ref invocation, ref _reservedBytes, out Breach);
        }

        internal Utf8SliceBalanceEntry BalanceEntryAt(int index)
        {
            return _balanceTables[index / BalanceChunkCapacity][index % BalanceChunkCapacity];
        }

        internal bool PopFrame(out Utf8SliceFrame frame)
        {
            if (_frameCount == 0)
            {
                frame = default;
                return false;
            }

            _frameCount--;
            frame = _frameTables[_frameCount / FrameChunkCapacity][_frameCount % FrameChunkCapacity];
            return true;
        }

        internal Utf8SliceTrailEntry TrailEntryAt(int index)
        {
            return _trailTables[index / TrailChunkCapacity][index % TrailChunkCapacity];
        }

        internal void TruncateTrail(int count, int[] captureStarts, int[] captureEnds, int[] balanceHeads)
        {
            while (_trailCount > count)
            {
                _trailCount--;
                var entry = _trailTables[_trailCount / TrailChunkCapacity][_trailCount % TrailChunkCapacity];
                if (entry.IsOpening || entry.IsClosing) continue;
                if (entry.IsHead)
                {
                    balanceHeads[entry.Slot] = entry.OldValue;
                }
                else if (entry.IsStart)
                {
                    captureStarts[entry.Slot] = entry.OldValue;
                }
                else
                {
                    captureEnds[entry.Slot] = entry.OldValue;
                }
            }
        }

        internal void TruncateJournal(int count, int[] counts, int[] positions)
        {
            while (_journalCount > count)
            {
                _journalCount--;
                var entry = _journalTables[_journalCount / JournalChunkCapacity][_journalCount % JournalChunkCapacity];
                counts[entry.RepeatId] = entry.OldCount;
                positions[entry.RepeatId] = entry.OldPosition;
            }
        }

        internal Utf8ExecutionLimitKind MaterializeHistory(int groups,
            ref Utf8SliceInvocation invocation, out int[][] captureStarts, out int[][] captureLengths)
        {
            captureStarts = [];
            captureLengths = [];
            if (groups == 0) return Utf8ExecutionLimitKind.None;
            var ownedBytes = checked(Utf8ConstructionBudget.ArrayBytes<int[]>(groups) * 2);
            var temporaryBytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(groups) * 2);
            if (!invocation.Ledger.TryReserve(checked(ownedBytes + temporaryBytes))) return Utf8ExecutionLimitKind.WorkingBytes;
            int[]? rentedCounts = null;
            int[]? rentedOpen = null;
            var retain = invocation.Ledger.IsUnlimited;
            var published = false;
            try
            {
                var breach = invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                captureStarts = new int[groups][];
                captureLengths = new int[groups][];
                var counts = Utf8ExactStatePool<int>.Rent(groups, retain);
                rentedCounts = counts;
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var open = Utf8ExactStatePool<int>.Rent(groups, retain);
                rentedOpen = open;
                for (var offset = 0; offset < groups;)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var count = Math.Min(2048, groups - offset);
                    counts.AsSpan(offset, count).Clear();
                    offset += count;
                }
                var sawBalanceDrain = false;
                for (var entry = 0; entry < _trailCount; entry++)
                {
                    if ((entry & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (item.IsRestore || item.IsOpening) continue;
                    if (item.IsHead)
                    {
                        if (item.NewValue < item.OldValue) sawBalanceDrain = true;
                    }
                    else if (!item.IsStart) counts[item.Slot]++;
                }
                ulong captureBytes = 0;
                for (var group = 0; group < groups; group++)
                {
                    if ((group & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                    captureBytes = checked(captureBytes + Utf8ConstructionBudget.ArrayBytes<int>(counts[group]) * 2);
                }
                if (!invocation.Ledger.TryReserve(captureBytes)) return Utf8ExecutionLimitKind.WorkingBytes;
                ownedBytes = checked(ownedBytes + captureBytes);
                breach = invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                for (var group = 0; group < groups; group++)
                {
                    if ((group & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                    open[group] = -1;
                    captureStarts[group] = counts[group] == 0 ? [] : new int[counts[group]];
                    captureLengths[group] = counts[group] == 0 ? [] : new int[counts[group]];
                    counts[group] = 0;
                }
                for (var entry = 0; entry < _trailCount; entry++)
                {
                    if ((entry & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (item.IsRestore || item.IsOpening) continue;
                    if (item.IsHead)
                    {
                        if (item.NewValue < item.OldValue) counts[item.Slot]--;
                    }
                    else if (item.IsStart) open[item.Slot] = item.NewValue;
                    else
                    {
                        // Histories normalize mirrored capture edges; the trail
                        // itself retains its original rollback representation.
                        var left = item.IsClosing ? item.OldValue : open[item.Slot];
                        var right = item.NewValue;
                        captureStarts[item.Slot][counts[item.Slot]] = Math.Min(left, right);
                        captureLengths[item.Slot][counts[item.Slot]] = Math.Abs(right - left);
                        counts[item.Slot]++;
                    }
                }
                if (sawBalanceDrain)
                {
                    for (var group = 0; group < groups; group++)
                    {
                        if ((group & 255) == 0 && (breach = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return breach;
                        if (counts[group] == captureStarts[group].Length) continue;
                        var oldBytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(captureStarts[group].Length) * 2);
                        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(counts[group]) * 2);
                        // Admit both replacement arrays while old capacity is
                        // held. Copy in bounded blocks, then transfer ownership.
                        if (!invocation.Ledger.TryReserve(bytes)) return Utf8ExecutionLimitKind.WorkingBytes;
                        ownedBytes += bytes;
                        breach = invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
                        if (breach != Utf8ExecutionLimitKind.None) return breach;
                        var starts = counts[group] == 0 ? [] : new int[counts[group]];
                        var lengths = counts[group] == 0 ? [] : new int[counts[group]];
                        for (var offset = 0; offset < counts[group];)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return breach;
                            var count = Math.Min(512, counts[group] - offset);
                            captureStarts[group].AsSpan(offset, count).CopyTo(starts.AsSpan(offset, count));
                            captureLengths[group].AsSpan(offset, count).CopyTo(lengths.AsSpan(offset, count));
                            offset += count;
                        }
                        captureStarts[group] = starts;
                        captureLengths[group] = lengths;
                        invocation.Ledger.Release(oldBytes);
                        ownedBytes -= oldBytes;
                    }
                }
                published = true;
                return Utf8ExecutionLimitKind.None;
            }
            finally
            {
                if (rentedOpen is not null) Utf8ExactStatePool<int>.Return(rentedOpen, retain);
                if (rentedCounts is not null) Utf8ExactStatePool<int>.Return(rentedCounts, retain);
                invocation.Ledger.Release(temporaryBytes);
                if (!published)
                {
                    invocation.Ledger.Release(ownedBytes);
                    captureStarts = [];
                    captureLengths = [];
                }
            }
        }

        internal void ResetAttempt()
        {
            _frameCount = 0;
            _trailCount = 0;
            _journalCount = 0;
            _balanceCount = 0;
        }

        internal void ReleaseAll(ref Utf8SliceInvocation invocation)
        {
            if (_reservedBytes != 0)
            {
                invocation.Ledger.Release(_reservedBytes);
                _reservedBytes = 0;
            }

            _frameTables = Array.Empty<Utf8SliceFrame[]>();
            _trailTables = Array.Empty<Utf8SliceTrailEntry[]>();
            _journalTables = Array.Empty<Utf8SliceCountMutation[]>();
            _balanceTables = Array.Empty<Utf8SliceBalanceEntry[]>();
            _frameCount = 0;
            _trailCount = 0;
            _journalCount = 0;
            _balanceCount = 0;
        }

        private static bool Push<T>(ref T[][] tables, ref int count, int chunkCapacity,
            T value, ref Utf8SliceInvocation invocation, ref ulong reservedBytes, out Utf8ExecutionLimitKind breach)
            where T : struct
        {
            breach = Utf8ExecutionLimitKind.None;
            if (count == int.MaxValue) { breach = Utf8ExecutionLimitKind.WorkingBytes; return false; }
            var chunk = count / chunkCapacity;
            var slot = count % chunkCapacity;
            if (slot == 0 && (tables is null || chunk >= tables.Length || tables[chunk] is null))
            {
                var replaceTable = tables is null || chunk >= tables.Length;
                var oldCapacity = tables?.Length ?? 0;
                var capacity = oldCapacity == 0 ? ChunkTableCapacity :
                    oldCapacity <= Array.MaxLength / 2 ? oldCapacity * 2 : Array.MaxLength;
                if (replaceTable && capacity <= chunk) { breach = Utf8ExecutionLimitKind.WorkingBytes; return false; }
                var tableBytes = replaceTable ? Utf8ConstructionBudget.ArrayBytes<T[]>(capacity) : 0;
                var bytes = checked(tableBytes + Utf8ConstructionBudget.ArrayBytes<T>(chunkCapacity));
                if (!invocation.Ledger.TryReserve(bytes)) { breach = Utf8ExecutionLimitKind.WorkingBytes; return false; }
                var adopted = false;
                try
                {
                    breach = invocation.Budget.TryNoteWorkingBytes(invocation.Ledger.ReservedBytes);
                    if (breach != Utf8ExecutionLimitKind.None) return false;
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return false;
                    var candidate = replaceTable ? new T[capacity][] : tables ?? throw new InvalidOperationException("Live slice stack table is missing.");
                    if (replaceTable && tables is not null)
                    {
                        for (var offset = 0; offset < oldCapacity;)
                        {
                            breach = invocation.TryChargeInputStep();
                            if (breach != Utf8ExecutionLimitKind.None) return false;
                            var length = Math.Min(256, oldCapacity - offset);
                            tables.AsSpan(offset, length).CopyTo(candidate.AsSpan(offset, length));
                            offset += length;
                        }
                    }
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return false;
                    var leaf = new T[chunkCapacity];
                    // Publish only after acquisition and copy complete. Failed growth
                    // leaves the original table, count and parent reservation intact.
                    var oldBytes = replaceTable ? Utf8ConstructionBudget.ArrayBytes<T[]>(oldCapacity) : 0;
                    candidate[chunk] = leaf;
                    tables = candidate;
                    invocation.Ledger.Release(oldBytes);
                    reservedBytes = checked(reservedBytes + bytes - oldBytes);
                    adopted = true;
                }
                finally { if (!adopted) invocation.Ledger.Release(bytes); }
            }
            tables[chunk][slot] = value;
            count++;
            return true;
        }
    }

    private static bool BackreferenceScalarsEqual(Utf8SliceProgram program,
        Utf8SliceInstruction instruction, Rune captured, Rune input)
    {
        if (instruction.D == 0) return Utf8SliceCaseEquivalences.ScalarsEquivalent(captured, input, program.CaseBehavior);
        var lower = program.BackreferenceLowerings[instruction.D - 1];
        return lower(captured.Value) == lower(input.Value);
    }

}
