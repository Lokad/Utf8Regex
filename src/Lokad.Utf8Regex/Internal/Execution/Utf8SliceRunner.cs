using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Resume actions for explicit slice backtrack frames.</summary>
internal enum Utf8SliceResumeKind : byte
{
    Alternation = 0,
    RepeatUnwind = 1,
    RepeatContinue = 2,
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
    internal bool IsStart;
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

internal readonly struct Utf8SliceUnitOutcome
{
    internal Utf8SliceUnitOutcome(bool found, int startUnit, int endUnit, int startByte, int endByte, Utf8ExecutionLimitKind breach)
    {
        Found = found;
        StartUnit = startUnit;
        EndUnit = endUnit;
        StartByte = startByte;
        EndByte = endByte;
        Breach = breach;
    }

    internal bool Found { get; }
    internal int StartUnit { get; }
    internal int EndUnit { get; }
    internal int StartByte { get; }
    internal int EndByte { get; }
    internal Utf8ExecutionLimitKind Breach { get; }
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

internal readonly struct Utf8SliceDetailedOutcome
{
    internal Utf8SliceDetailedOutcome(
        bool found, int startUnit, int endUnit, int startByte, int endByte,
        int[][]? captureStarts, int[][]? captureLengths, Utf8ExecutionLimitKind breach)
    {
        Found = found;
        StartUnit = startUnit;
        EndUnit = endUnit;
        StartByte = startByte;
        EndByte = endByte;
        CaptureStarts = captureStarts;
        CaptureLengths = captureLengths;
        Breach = breach;
    }

    internal bool Found { get; }
    internal int StartUnit { get; }
    internal int EndUnit { get; }
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
internal static class Utf8SliceRunner
{
    internal const int FrameChunkCapacity = 64;
    internal const int TrailChunkCapacity = 128;
    internal const int JournalChunkCapacity = 128;
    internal const int ChunkTableCapacity = 4;

    internal static int FrameReserveBytes => Unsafe.SizeOf<Utf8SliceFrame>() * FrameChunkCapacity;
    internal static int TrailReserveBytes => Unsafe.SizeOf<Utf8SliceTrailEntry>() * TrailChunkCapacity;
    internal static int JournalReserveBytes => Unsafe.SizeOf<Utf8SliceCountMutation>() * JournalChunkCapacity;

    internal static Utf8SliceSlots RentSlots(int groups, ref Utf8SliceInvocation invocation)
    {
        if (groups == 0)
        {
            return new Utf8SliceSlots(Array.Empty<int>(), Array.Empty<int>());
        }

        if (!invocation.Ledger.TryReserve((ulong)groups * 8))
        {
            throw new InvalidOperationException("Owned slice capture slots breached an unlimited invocation budget.");
        }

        var starts = new int[groups];
        var ends = new int[groups];
        Array.Fill(starts, -1);
        Array.Fill(ends, -1);
        return new Utf8SliceSlots(starts, ends);
    }

    internal static void ReleaseSlots(Utf8SliceSlots slots, ref Utf8SliceInvocation invocation)
    {
        if (slots.Starts.Length != 0)
        {
            invocation.Ledger.Release((ulong)slots.Starts.Length * 8);
        }
    }

    internal static void ReleaseHistory(int[][]? starts, int[][]? lengths, ref Utf8SliceInvocation invocation)
    {
        if (starts is null || lengths is null)
        {
            return;
        }

        invocation.Ledger.Release((ulong)starts.Length * 8);
        invocation.Ledger.Release((ulong)lengths.Length * 8);
        for (var group = 0; group < starts.Length && group < lengths.Length; group++)
        {
            if (starts[group].Length != 0)
            {
                invocation.Ledger.Release((ulong)starts[group].Length * 4);
            }

            if (lengths[group].Length != 0)
            {
                invocation.Ledger.Release((ulong)lengths[group].Length * 4);
            }
        }
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        return TryMatchAt(program, input, start, captureStarts, captureEnds, false, ref invocation);
    }

    internal static Utf8SliceUnitOutcome TryMatchFromUnitNonEmpty(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int unit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        if (unit < 0 || unit > map.Utf16Length)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
        }

        var startByte = map.Resolve(unit).ByteOffset;
        var outcome = TryMatchAt(program, input, startByte, captureStarts, captureEnds, false, true, ref invocation);
        if (outcome.Breach != Utf8ExecutionLimitKind.None || !outcome.IsMatch)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, outcome.Breach);
        }

        var endUnit = map.GetUtf16OffsetForByteOffset(outcome.EndIndex);
        return new Utf8SliceUnitOutcome(true, unit, endUnit, startByte, outcome.EndIndex, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        bool collectHistory,
        ref Utf8SliceInvocation invocation)
    {
        return TryMatchAt(program, input, start, captureStarts, captureEnds, collectHistory, false, ref invocation);
    }

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        bool collectHistory,
        bool requireNonEmpty,
        ref Utf8SliceInvocation invocation)
    {
        var counts = RentIntArray(program.RepeatCount, ref invocation, out var breach);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            return new Utf8SliceMatchOutcome(false, -1, breach, null, null);
        }

        var positions = RentIntArray(program.RepeatCount, ref invocation, out breach);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            ReleaseIntArray(counts, ref invocation);
            return new Utf8SliceMatchOutcome(false, -1, breach, null, null);
        }

        Array.Fill(counts, -1);
        var stacks = new ChunkedStacks();
        try
        {
            return RunFrom(program, input, start, captureStarts, captureEnds, counts, positions, collectHistory, requireNonEmpty, ref stacks, ref invocation);
        }
        finally
        {
            stacks.ReleaseAll(ref invocation);
            ReleaseIntArray(positions, ref invocation);
            ReleaseIntArray(counts, ref invocation);
        }
    }

    internal static Utf8SliceUnitOutcome TryMatchFromUnit(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int unit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        if (unit < 0 || unit > map.Utf16Length)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
        }

        var startByte = map.Resolve(unit).ByteOffset;
        var outcome = TryMatchAt(program, input, startByte, captureStarts, captureEnds, ref invocation);
        if (outcome.Breach != Utf8ExecutionLimitKind.None || !outcome.IsMatch)
        {
            return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, outcome.Breach);
        }

        if (outcome.EndIndex == startByte)
        {
            return new Utf8SliceUnitOutcome(true, unit, unit, startByte, startByte, Utf8ExecutionLimitKind.None);
        }

        var endUnit = map.GetUtf16OffsetForByteOffset(outcome.EndIndex);
        return new Utf8SliceUnitOutcome(true, unit, endUnit, startByte, outcome.EndIndex, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8ValueMatch ProjectMatch(Utf8BoundaryMap map, Utf8SliceUnitOutcome found)
    {
        var startBoundary = map.Resolve(found.StartUnit);
        var endBoundary = map.Resolve(found.EndUnit);
        var aligned = startBoundary.IsScalarBoundary && endBoundary.IsScalarBoundary;
        return new Utf8ValueMatch(
            true,
            aligned,
            found.StartUnit,
            found.EndUnit - found.StartUnit,
            found.StartByte,
            found.EndByte - found.StartByte);
    }

    internal static Utf8SliceUnitOutcome TryFindFirst(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int fromUnit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        for (var unit = fromUnit; unit <= map.Utf16Length; unit++)
        {
            var outcome = TryMatchFromUnit(program, input, map, unit, captureStarts, captureEnds, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None || outcome.Found)
            {
                return outcome;
            }
        }

        return new Utf8SliceUnitOutcome(false, -1, -1, -1, -1, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8SliceMatchOutcome IsMatch(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        var outcome = TryFindFirst(program, input, map, 0, captureStarts, captureEnds, ref invocation);
        if (outcome.Breach != Utf8ExecutionLimitKind.None)
        {
            return new Utf8SliceMatchOutcome(false, -1, outcome.Breach, null, null);
        }

        return outcome.Found
            ? new Utf8SliceMatchOutcome(true, outcome.EndByte, Utf8ExecutionLimitKind.None, null, null)
            : new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None, null, null);
    }

    internal static Utf8SliceCountOutcome CountFrom(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int fromUnit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        var count = 0;
        var unit = fromUnit;
        while (unit <= map.Utf16Length)
        {
            var outcome = TryMatchFromUnit(program, input, map, unit, captureStarts, captureEnds, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                return new Utf8SliceCountOutcome(count, outcome.Breach);
            }

            if (!outcome.Found)
            {
                unit++;
                continue;
            }

            count++;
            unit = outcome.EndUnit > outcome.StartUnit ? outcome.EndUnit : outcome.StartUnit + 1;
        }

        return new Utf8SliceCountOutcome(count, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8SliceCountOutcome Count(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        return CountFrom(program, input, map, 0, captureStarts, captureEnds, ref invocation);
    }

    internal static Utf8SliceDetailedOutcome TryFindFirstDetailed(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        int fromUnit,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        for (var unit = fromUnit; unit <= map.Utf16Length; unit++)
        {
            var startByte = map.Resolve(unit).ByteOffset;
            var outcome = TryMatchAt(program, input, startByte, captureStarts, captureEnds, true, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                return new Utf8SliceDetailedOutcome(false, -1, -1, -1, -1, null, null, outcome.Breach);
            }

            if (!outcome.IsMatch)
            {
                continue;
            }

            if (outcome.EndIndex == startByte)
            {
                return new Utf8SliceDetailedOutcome(
                    true, unit, unit, startByte, startByte,
                    outcome.CaptureStarts, outcome.CaptureLengths, Utf8ExecutionLimitKind.None);
            }

            var endUnit = map.GetUtf16OffsetForByteOffset(outcome.EndIndex);
            return new Utf8SliceDetailedOutcome(
                true, unit, endUnit, startByte, outcome.EndIndex,
                outcome.CaptureStarts, outcome.CaptureLengths, Utf8ExecutionLimitKind.None);
        }

        return new Utf8SliceDetailedOutcome(false, -1, -1, -1, -1, null, null, Utf8ExecutionLimitKind.None);
    }

    private static Utf8SliceMatchOutcome RunFrom(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        int[] counts,
        int[] positions,
        bool collectHistory,
        bool requireNonEmpty,
        ref ChunkedStacks stacks,
        ref Utf8SliceInvocation invocation)
    {
        var instructions = program.Instructions;
        var programIndex = 0;
        var inputIndex = start;
        while (true)
        {
            var stepBreach = invocation.Budget.TryChargeStep();
            if (stepBreach != Utf8ExecutionLimitKind.None)
            {
                return new Utf8SliceMatchOutcome(false, -1, stepBreach, null, null);
            }

            if (invocation.CheckpointHook is not null && (invocation.Budget.Steps & 63) == 0)
            {
                invocation.CheckpointHook(invocation.Budget.Steps);
            }

            var instruction = instructions[programIndex];
            var failed = false;
            switch (instruction.Kind)
            {
                case Utf8SliceInstructionKind.Literal:
                    if (inputIndex < input.Length && input[inputIndex] == (byte)instruction.A)
                    {
                        inputIndex++;
                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Set:
                    if (inputIndex < input.Length && input[inputIndex] < 128 &&
                        (program.SetMasks[instruction.A][input[inputIndex] >> 3] & (byte)(1 << (input[inputIndex] & 7))) != 0)
                    {
                        inputIndex++;
                        programIndex++;
                    }
                    else
                    {
                        failed = true;
                    }

                    break;
                case Utf8SliceInstructionKind.Fork:
                    if (!stacks.PushFrame(
                        new Utf8SliceFrame
                        {
                            ResumeKind = Utf8SliceResumeKind.Alternation,
                            Target = instruction.A,
                            InputIndex = inputIndex,
                            TrailCount = stacks.TrailCount,
                            JournalCount = stacks.JournalCount,
                        },
                        ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
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
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                            }

                            count = 0;
                        }

                        var madeNoProgress = count > 0 && positions[repeatId] == inputIndex;
                        var minimum = instruction.C;
                        var maximum = instruction.D;
                        var greedy = instruction.Kind == Utf8SliceInstructionKind.RepeatGreedy;
                        if (count < minimum)
                        {
                            if (!SetRepeat(counts, positions, repeatId, count + 1, inputIndex, ref stacks, ref invocation))
                            {
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
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
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        }

                        if (invocation.Budget.TryNoteDepth((uint)stacks.FrameCount) != Utf8ExecutionLimitKind.None)
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.Depth, null, null);
                        }

                        if (greedy)
                        {
                            if (!SetRepeat(counts, positions, repeatId, count + 1, inputIndex, ref stacks, ref invocation))
                            {
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
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
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                    if (!stacks.PushTrail(instruction.A, captureStarts[instruction.A], inputIndex, true, ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                    }

                    captureStarts[instruction.A] = inputIndex;
                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    if (!stacks.PushTrail(instruction.A, captureEnds[instruction.A], inputIndex, false, ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                    }

                    captureEnds[instruction.A] = inputIndex;
                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.Dot:
                    // Dots consume one scalar: ASCII and BMP leads take
                    // their scalar, astral leads take all four bytes.
                    // Continuation bytes fail the attempt, so byte-stepping
                    // drivers cannot report unaligned starts. Inputs are
                    // validated before execution, hence lead widths stay
                    // in bounds without revalidation here. Unit-exact
                    // consumers decline astral inputs at routing, where
                    // scalar and unit semantics diverge.
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
                    // Zero-width line edges: `^` holds at the input origin
                    // or (multiline) just after a line feed; `$` holds at
                    // the input end, before a trailing line feed, or
                    // (multiline) just before any line feed. Line feeds
                    // are single bytes, so these checks are
                    // encoding-indifferent and need no astral routing.
                    // Guards order short-circuit before any indexing.
                    var anchorAtBeginning = instruction.A == 0;
                    var anchorMultiline = instruction.B != 0;
                    bool anchorHolds;
                    if (anchorAtBeginning)
                    {
                        anchorHolds = inputIndex == 0 ||
                            (anchorMultiline && inputIndex > 0 && input[inputIndex - 1] == (byte)'\n');
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

                    if (!stacks.MaterializeHistory(program.GroupCount, ref invocation, out var historyStarts, out var historyLengths))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                    }

                    return new Utf8SliceMatchOutcome(true, inputIndex, Utf8ExecutionLimitKind.None, historyStarts, historyLengths);
                default:
                    throw new InvalidOperationException($"Unsupported slice instruction {instruction.Kind}.");
            }

            if (failed)
            {
                if (!stacks.PopFrame(out var popped))
                {
                    return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None, null, null);
                }

                stacks.TruncateJournal(popped.JournalCount, counts, positions);
                stacks.TruncateTrail(popped.TrailCount, captureStarts, captureEnds);
                switch (popped.ResumeKind)
                {
                    case Utf8SliceResumeKind.Alternation:
                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                    case Utf8SliceResumeKind.RepeatUnwind:
                        if (!SetRepeat(counts, positions, popped.Aux, popped.Count, popped.InputIndex, ref stacks, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
                        }

                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                    default:
                        if (!SetRepeat(counts, positions, popped.Aux, popped.Count, popped.InputIndex, ref stacks, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes, null, null);
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

    private static int[] RentIntArray(int length, ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        if (length == 0)
        {
            return Array.Empty<int>();
        }

        if (!invocation.Ledger.TryReserve((ulong)length * 4))
        {
            breach = Utf8ExecutionLimitKind.WorkingBytes;
            return Array.Empty<int>();
        }

        return new int[length];
    }

    private static void ReleaseIntArray(int[] array, ref Utf8SliceInvocation invocation)
    {
        if (array.Length != 0)
        {
            invocation.Ledger.Release((ulong)array.Length * 4);
        }
    }

    private struct ChunkedStacks
    {
        private Utf8SliceFrame[][] _frameTables;
        private Utf8SliceTrailEntry[][] _trailTables;
        private Utf8SliceCountMutation[][] _journalTables;
        private int _frameCount;
        private int _trailCount;
        private int _journalCount;
        private ulong _reservedBytes;

        internal int FrameCount => _frameCount;
        internal int TrailCount => _trailCount;
        internal int JournalCount => _journalCount;

        internal bool PushFrame(Utf8SliceFrame frame, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _frameTables, ref _frameCount, FrameChunkCapacity, FrameReserveBytes, frame, ref invocation, ref _reservedBytes);
        }

        internal bool PushTrail(int slot, int oldValue, int newValue, bool isStart, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity, TrailReserveBytes,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldValue, NewValue = newValue, IsStart = isStart }, ref invocation, ref _reservedBytes);
        }

        internal bool PushJournal(int repeatId, int oldCount, int oldPosition, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _journalTables, ref _journalCount, JournalChunkCapacity, JournalReserveBytes,
                new Utf8SliceCountMutation { RepeatId = repeatId, OldCount = oldCount, OldPosition = oldPosition }, ref invocation, ref _reservedBytes);
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

        internal void TruncateTrail(int count, int[] captureStarts, int[] captureEnds)
        {
            while (_trailCount > count)
            {
                _trailCount--;
                var entry = _trailTables[_trailCount / TrailChunkCapacity][_trailCount % TrailChunkCapacity];
                if (entry.IsStart)
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

        internal bool MaterializeHistory(
            int groups,
            ref Utf8SliceInvocation invocation,
            out int[][] captureStarts,
            out int[][] captureLengths)
        {
            if (!invocation.Ledger.TryReserve((ulong)groups * 16))
            {
                captureStarts = Array.Empty<int[]>();
                captureLengths = Array.Empty<int[]>();
                return false;
            }

            captureStarts = new int[groups][];
            captureLengths = new int[groups][];
            if (groups == 0)
            {
                return true;
            }

            if (!invocation.Ledger.TryReserve((ulong)groups * 4))
            {
                return false;
            }

            var reservedBytes = (ulong)groups * 4;
            var counts = new int[groups];
            try
            {
                for (var entry = 0; entry < _trailCount; entry++)
                {
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (!item.IsStart)
                    {
                        counts[item.Slot]++;
                    }
                }

                for (var group = 0; group < groups; group++)
                {
                    if (counts[group] == 0)
                    {
                        captureStarts[group] = Array.Empty<int>();
                        captureLengths[group] = Array.Empty<int>();
                        continue;
                    }

                    if (!invocation.Ledger.TryReserve((ulong)counts[group] * 8))
                    {
                        for (var created = 0; created < group; created++)
                        {
                            invocation.Ledger.Release((ulong)captureStarts[created].Length * 8);
                        }

                        invocation.Ledger.Release(reservedBytes);
                        return false;
                    }

                    reservedBytes += (ulong)counts[group] * 8;
                    captureStarts[group] = new int[counts[group]];
                    captureLengths[group] = new int[counts[group]];
                    counts[group] = 0;
                }

                var open = new int[groups];
                for (var group = 0; group < groups; group++)
                {
                    open[group] = -1;
                }

                for (var entry = 0; entry < _trailCount; entry++)
                {
                    var item = _trailTables[entry / TrailChunkCapacity][entry % TrailChunkCapacity];
                    if (item.IsStart)
                    {
                        open[item.Slot] = item.NewValue;
                    }
                    else
                    {
                        captureStarts[item.Slot][counts[item.Slot]] = open[item.Slot];
                        captureLengths[item.Slot][counts[item.Slot]] = item.NewValue - open[item.Slot];
                        counts[item.Slot]++;
                    }
                }

                return true;
            }
            finally
            {
                invocation.Ledger.Release((ulong)groups * 4);
            }
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
            _frameCount = 0;
            _trailCount = 0;
            _journalCount = 0;
        }

        private static bool Push<T>(
            ref T[][] tables,
            ref int count,
            int chunkCapacity,
            int chunkBytes,
            T value,
            ref Utf8SliceInvocation invocation,
            ref ulong reservedBytes)
            where T : struct
        {
            var chunk = count / chunkCapacity;
            var slot = count % chunkCapacity;
            if (slot == 0)
            {
                if (tables is null || tables.Length == 0)
                {
                    if (!invocation.Ledger.TryReserve((ulong)ChunkTableCapacity * 8))
                    {
                        return false;
                    }

                    reservedBytes += (ulong)ChunkTableCapacity * 8;
                    tables = new T[ChunkTableCapacity][];
                }
                else if (chunk >= tables.Length)
                {
                    var grown = new T[tables.Length * 2][];
                    if (!invocation.Ledger.TryReserve((ulong)grown.Length * 8))
                    {
                        return false;
                    }

                    Array.Copy(tables, grown, tables.Length);
                    invocation.Ledger.Release((ulong)tables.Length * 8);
                    reservedBytes += (ulong)tables.Length * 8;
                    tables = grown;
                }

                if (tables[chunk] is null)
                {
                    if (!invocation.Ledger.TryReserve((ulong)chunkBytes))
                    {
                        return false;
                    }

                    reservedBytes += (ulong)chunkBytes;
                    tables[chunk] = new T[chunkCapacity];
                }
            }

            tables[chunk][slot] = value;
            count++;
            return true;
        }
    }
}
