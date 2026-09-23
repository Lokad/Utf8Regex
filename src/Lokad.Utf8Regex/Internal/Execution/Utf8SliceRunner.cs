using System.Runtime.CompilerServices;

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
    internal Utf8SliceMatchOutcome(bool isMatch, int endIndex, Utf8ExecutionLimitKind breach)
    {
        IsMatch = isMatch;
        EndIndex = endIndex;
        Breach = breach;
    }

    internal bool IsMatch { get; }
    internal int EndIndex { get; }
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

    internal static Utf8SliceMatchOutcome TryMatchAt(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        var counts = RentIntArray(program.RepeatCount, ref invocation, out var breach);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            return new Utf8SliceMatchOutcome(false, -1, breach);
        }

        var positions = RentIntArray(program.RepeatCount, ref invocation, out breach);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            ReleaseIntArray(counts, ref invocation);
            return new Utf8SliceMatchOutcome(false, -1, breach);
        }

        Array.Fill(counts, -1);
        var stacks = new ChunkedStacks();
        try
        {
            return RunFrom(program, input, start, captureStarts, captureEnds, counts, positions, ref stacks, ref invocation);
        }
        finally
        {
            stacks.ReleaseAll(ref invocation);
            ReleaseIntArray(positions, ref invocation);
            ReleaseIntArray(counts, ref invocation);
        }
    }

    internal static Utf8SliceMatchOutcome IsMatch(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        for (var start = 0; start <= input.Length; start++)
        {
            var outcome = TryMatchAt(program, input, start, captureStarts, captureEnds, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None || outcome.IsMatch)
            {
                return outcome;
            }
        }

        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None);
    }

    internal static Utf8SliceCountOutcome Count(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int[] captureStarts,
        int[] captureEnds,
        ref Utf8SliceInvocation invocation)
    {
        invocation.Budget.ThrowIfCancellationRequested();
        var count = 0;
        var position = 0;
        while (position <= input.Length)
        {
            var outcome = TryMatchAt(program, input, position, captureStarts, captureEnds, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                return new Utf8SliceCountOutcome(count, outcome.Breach);
            }

            if (!outcome.IsMatch)
            {
                position++;
                continue;
            }

            count++;
            position = outcome.EndIndex > position ? outcome.EndIndex : position + 1;
        }

        return new Utf8SliceCountOutcome(count, Utf8ExecutionLimitKind.None);
    }

    private static Utf8SliceMatchOutcome RunFrom(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        int start,
        int[] captureStarts,
        int[] captureEnds,
        int[] counts,
        int[] positions,
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
                return new Utf8SliceMatchOutcome(false, -1, stepBreach);
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
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
                    }

                    if (invocation.Budget.TryNoteDepth((uint)stacks.FrameCount) != Utf8ExecutionLimitKind.None)
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.Depth);
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
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
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
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
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
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
                        }

                        if (invocation.Budget.TryNoteDepth((uint)stacks.FrameCount) != Utf8ExecutionLimitKind.None)
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.Depth);
                        }

                        if (greedy)
                        {
                            if (!SetRepeat(counts, positions, repeatId, count + 1, inputIndex, ref stacks, ref invocation))
                            {
                                return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
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
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
                    }

                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureStart:
                    if (!stacks.PushTrail(instruction.A, captureStarts[instruction.A], true, ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
                    }

                    captureStarts[instruction.A] = inputIndex;
                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.CaptureEnd:
                    if (!stacks.PushTrail(instruction.A, captureEnds[instruction.A], false, ref invocation))
                    {
                        return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
                    }

                    captureEnds[instruction.A] = inputIndex;
                    programIndex++;
                    break;
                case Utf8SliceInstructionKind.Accept:
                    return new Utf8SliceMatchOutcome(true, inputIndex, Utf8ExecutionLimitKind.None);
                default:
                    throw new InvalidOperationException($"Unsupported slice instruction {instruction.Kind}.");
            }

            if (failed)
            {
                if (!stacks.PopFrame(out var popped))
                {
                    return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.None);
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
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
                        }

                        inputIndex = popped.InputIndex;
                        programIndex = popped.Target;
                        break;
                    default:
                        if (!SetRepeat(counts, positions, popped.Aux, popped.Count, popped.InputIndex, ref stacks, ref invocation))
                        {
                            return new Utf8SliceMatchOutcome(false, -1, Utf8ExecutionLimitKind.WorkingBytes);
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

        internal bool PushTrail(int slot, int oldValue, bool isStart, ref Utf8SliceInvocation invocation)
        {
            return Push(ref _trailTables, ref _trailCount, TrailChunkCapacity, TrailReserveBytes,
                new Utf8SliceTrailEntry { Slot = slot, OldValue = oldValue, IsStart = isStart }, ref invocation, ref _reservedBytes);
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
