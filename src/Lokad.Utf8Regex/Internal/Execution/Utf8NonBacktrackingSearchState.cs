using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Immutable scalar-boundary viability for one NonBacktracking batch.
/// Each row records which instruction states can reach acceptance from that
/// boundary. Ordered replay then skips dead choices without rescanning suffixes.
/// Capture values never affect viability in this regular grammar. Preparation
/// takes O(scalars * states) work; stored offsets and packed rows cost
/// (scalars + 1) * (4 + 8 * ceiling(states / 64)) payload bytes, plus complete modeled array headers/alignment.</summary>
internal sealed class Utf8NonBacktrackingSearchState
{
    private readonly int[] _boundaries;
    private readonly ulong[] _viability;
    private readonly int _stride;
    private ulong _reservation;

    private Utf8NonBacktrackingSearchState(int[] boundaries, ulong[] viability, int stride, ulong reservation)
    {
        _boundaries = boundaries;
        _viability = viability;
        _stride = stride;
        _reservation = reservation;
    }

    internal static int[] BuildEpsilonOrder(Utf8SliceInstruction[] instructions)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        var colors = budget.AllocateArray<byte>(instructions.Length, false);
        try
        {
            var order = budget.AllocateArray<int>(instructions.Length, false);
            var written = 0;
            using var frames = new Utf8ConstructionBuffer<(int Pc, bool Complete)>(budget);
            for (var pc = 0; pc < instructions.Length; pc++)
            {
                if (colors[pc] == 2) continue;
                frames.Push((pc, false));
                while (frames.TryPop(out var frame))
                {
                    if (frame.Complete)
                    {
                        colors[frame.Pc] = 2;
                        order[written++] = frame.Pc;
                        continue;
                    }
                    if (colors[frame.Pc] == 2) continue;
                    if (colors[frame.Pc] == 1)
                        throw new InvalidOperationException("A nullable NonBacktracking cycle escaped scalar lowering.");
                    colors[frame.Pc] = 1;
                    frames.Push((frame.Pc, true));
                    var instruction = instructions[frame.Pc];
                    switch (instruction.Kind)
                    {
                        case Utf8SliceInstructionKind.Fork:
                            frames.Push((instruction.A, false));
                            frames.Push((frame.Pc + 1, false));
                            break;
                        case Utf8SliceInstructionKind.Jump:
                            frames.Push((instruction.A, false));
                            break;
                        case Utf8SliceInstructionKind.CaptureStart:
                        case Utf8SliceInstructionKind.CaptureEnd:
                        case Utf8SliceInstructionKind.Anchor:
                        case Utf8SliceInstructionKind.Boundary:
                            frames.Push((frame.Pc + 1, false));
                            break;
                    }
                }
            }
            construction.Complete();
            return order;
        }
        finally
        {
            budget.ReleaseArray(colors, false);
        }
    }

    // The caller has validated the complete subject. Two bounded scalar passes
    // admit exact offsets and bit rows; no UTF-16 subject or unit map is built.
    internal static Utf8ExecutionLimitKind TryCreate(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        ref Utf8SliceInvocation invocation, out Utf8NonBacktrackingSearchState? state)
    {
        state = null;
        invocation.Budget.ThrowIfCancellationRequested();
        var scalars = 0;
        for (var edge = 0; edge < input.Length; scalars++)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            edge = Utf8SliceRunner.AdvancePastScalar(input, edge, false);
            if (edge < 0) throw new InvalidOperationException("The NonBacktracking batch requires validated scalar input.");
        }
        var count = (long)scalars + 1;
        var stride = (int)(((long)program.Instructions.Length + 63) / 64);
        var cells = count * stride;
        if (count > Array.MaxLength || cells > Array.MaxLength) return Utf8ExecutionLimitKind.WorkingBytes;
        var reservation = checked(Utf8ConstructionBudget.ArrayBytes<int>((int)count)
            + Utf8ConstructionBudget.ArrayBytes<ulong>((int)cells));
        var admission = invocation.TryReserveWorking(reservation);
        if (admission != Utf8ExecutionLimitKind.None) return admission;
        var published = false;
        try
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var boundaries = new int[(int)count];
            var bits = new ulong[(int)cells];
            for (var index = 1; index < boundaries.Length; index++)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                boundaries[index] = Utf8SliceRunner.AdvancePastScalar(input, boundaries[index - 1], false);
            }
            var result = new Utf8NonBacktrackingSearchState(boundaries, bits, stride, reservation);
            var singleWord = stride == 1;
            for (var boundary = scalars; boundary >= 0; boundary--)
            {
                var edge = boundaries[boundary];
                // Epsilon order resolves this row's dependencies before use.
                // Small programs need no repeated array loads/stores for them;
                // the next row is complete already. Wider rows keep the table.
                ulong current = 0;
                var following = singleWord && boundary < scalars ? bits[boundary + 1] : 0;
                var scalar = default(Rune);
                var before = default(Rune);
                if (boundary < scalars) _ = Rune.DecodeFromUtf8(input[edge..], out scalar, out _);
                if (boundary > 0) _ = Rune.DecodeFromUtf8(input[boundaries[boundary - 1]..], out before, out _);
                foreach (var pc in program.NonBacktrackingEpsilonOrder)
                {
                    breach = invocation.TryChargeInputStep();
                    if (breach != Utf8ExecutionLimitKind.None) return breach;
                    var instruction = program.Instructions[pc];
                    bool viable;
                    switch (instruction.Kind)
                    {
                        case Utf8SliceInstructionKind.Accept:
                            viable = true;
                            break;
                        case Utf8SliceInstructionKind.Fail:
                            viable = false;
                            break;
                        case Utf8SliceInstructionKind.Fork:
                            viable = singleWord
                                ? (current & ((1UL << (pc + 1)) | (1UL << instruction.A))) != 0
                                : result.CanMatch(boundary, pc + 1) || result.CanMatch(boundary, instruction.A);
                            break;
                        case Utf8SliceInstructionKind.Jump:
                            viable = singleWord ? (current & (1UL << instruction.A)) != 0
                                : result.CanMatch(boundary, instruction.A);
                            break;
                        case Utf8SliceInstructionKind.CaptureStart:
                        case Utf8SliceInstructionKind.CaptureEnd:
                            viable = singleWord ? (current & (1UL << (pc + 1))) != 0
                                : result.CanMatch(boundary, pc + 1);
                            break;
                        case Utf8SliceInstructionKind.Anchor:
                            viable = (singleWord ? (current & (1UL << (pc + 1))) != 0
                                : result.CanMatch(boundary, pc + 1)) &&
                                Utf8SliceRunner.HoldsNonBacktrackingAnchor(instruction, input, edge);
                            break;
                        case Utf8SliceInstructionKind.Boundary:
                            var beforeWord = boundary > 0 && Utf8SliceScalarClasses.IsBoundaryWord(before, program, instruction);
                            var afterWord = boundary < scalars && Utf8SliceScalarClasses.IsBoundaryWord(scalar, program, instruction);
                            viable = (singleWord ? (current & (1UL << (pc + 1))) != 0
                                : result.CanMatch(boundary, pc + 1)) && (beforeWord != afterWord) == (instruction.A != 0);
                            break;
                        default:
                            viable = boundary < scalars && (singleWord ? (following & (1UL << (pc + 1))) != 0
                                : result.CanMatch(boundary + 1, pc + 1)) &&
                                Utf8SliceRunner.MatchesNonBacktrackingAtom(program, instruction, scalar, ref invocation, out breach);
                            if (breach != Utf8ExecutionLimitKind.None) return breach;
                            break;
                    }
                    if (viable)
                    {
                        if (singleWord) current |= 1UL << pc;
                        else bits[boundary * stride + (pc >> 6)] |= 1UL << (pc & 63);
                    }
                }
                if (singleWord) bits[boundary] = current;
            }
            invocation.Budget.ThrowIfCancellationRequested();
            state = result;
            published = true;
            return Utf8ExecutionLimitKind.None;
        }
        finally { if (!published) invocation.Ledger.Release(reservation); }
    }

    internal bool CanMatch(int boundary, int pc)
        => (_viability[boundary * _stride + (pc >> 6)] & (1UL << (pc & 63))) != 0;

    internal int ResolveByteOffset(int byteOffset)
    {
        var index = Array.BinarySearch(_boundaries, byteOffset);
        if (index < 0) throw new ArgumentOutOfRangeException(nameof(byteOffset), "The start must be a complete scalar boundary.");
        return index;
    }

    // Exclusive batch owners and failed setup release synchronously. Published
    // value cursors leave this reservation charged for the shared invocation;
    // dropping one view cannot invalidate another copy's table or accounting.
    internal void Release(ref Utf8SliceInvocation invocation)
    {
        invocation.Ledger.Release(_reservation);
        _reservation = 0;
    }

    internal Utf8SliceSearchOutcome FindFirst(Utf8SliceProgram program, ref int boundary,
        int[] captureStarts, int[] captureEnds, bool collectHistory, ref Utf8SliceInvocation invocation)
    {
        var completed = false;
        ulong reservation = 0;
        Array.Fill(captureStarts, -1);
        Array.Fill(captureEnds, -1);
        try
        {
            for (; boundary < _boundaries.Length; boundary++)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                if (CanMatch(boundary, 0)) break;
            }
            if (boundary == _boundaries.Length) return new(false, -1, -1, null, null, Utf8ExecutionLimitKind.None);
            var bytes = Utf8ConstructionBudget.ArrayBytes<int>(program.NonBacktrackingCaptureOpeningCount);
            var admission = invocation.TryReserveWorking(bytes);
            if (admission != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, admission);
            reservation = bytes;
            admission = invocation.TryChargeInputStep();
            if (admission != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, admission);
            var openings = program.NonBacktrackingCaptureOpeningCount == 0 ? [] : new int[program.NonBacktrackingCaptureOpeningCount];
            var start = boundary;
            var pc = 0;
            while (true)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                var instruction = program.Instructions[pc];
                switch (instruction.Kind)
                {
                    case Utf8SliceInstructionKind.Fork:
                        var first = instruction.B == 0 ? pc + 1 : instruction.A;
                        var second = instruction.B == 0 ? instruction.A : pc + 1;
                        pc = CanMatch(boundary, first) ? first : second;
                        break;
                    case Utf8SliceInstructionKind.Jump:
                        pc = instruction.A;
                        break;
                    case Utf8SliceInstructionKind.CaptureStart:
                        openings[instruction.B] = _boundaries[boundary];
                        pc++;
                        break;
                    case Utf8SliceInstructionKind.CaptureEnd:
                        captureStarts[instruction.A] = openings[instruction.B];
                        captureEnds[instruction.A] = _boundaries[boundary];
                        pc++;
                        break;
                    case Utf8SliceInstructionKind.Anchor:
                    case Utf8SliceInstructionKind.Boundary:
                        pc++;
                        break;
                    case Utf8SliceInstructionKind.Accept:
                        breach = Utf8SliceRunner.BuildNonBacktrackingHistory(captureStarts, captureEnds, collectHistory,
                            ref invocation, out var starts, out var lengths);
                        if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                        try
                        {
                            invocation.Budget.ThrowIfCancellationRequested();
                            var startByte = _boundaries[start];
                            var endByte = _boundaries[boundary];
                            boundary += start == boundary ? 1 : 0;
                            completed = true;
                            return new(true, startByte, endByte, starts, lengths, Utf8ExecutionLimitKind.None);
                        }
                        catch { Utf8SliceRunner.ReleaseHistory(starts, lengths, ref invocation); throw; }
                    default:
                        boundary++;
                        pc++;
                        break;
                }
            }
        }
        finally
        {
            invocation.Ledger.Release(reservation);
            if (!completed)
            {
                Array.Fill(captureStarts, -1);
                Array.Fill(captureEnds, -1);
            }
        }
    }
}
