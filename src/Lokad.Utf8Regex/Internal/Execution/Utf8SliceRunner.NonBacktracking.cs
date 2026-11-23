using System.Numerics;
using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    // The ancestor masks test unconditional closure membership without
    // replaying the thread order. Conditions are evaluated at their scalar edge.
    internal static Utf8ExecutionLimitKind IsMatchPackedNonBacktracking(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int fromByte, ref Utf8SliceInvocation invocation, out bool found)
    {
        found = false;
        invocation.Budget.ThrowIfCancellationRequested();
        if ((uint)fromByte > (uint)input.Length ||
            fromByte < input.Length && (input[fromByte] & 0xC0) == 0x80)
            return Utf8ExecutionLimitKind.None;

        const int capacity = 32;
        var tableBytes = (ulong)(capacity * Unsafe.SizeOf<(ulong States, byte Value, ulong Next)>());
        var workingLimit = invocation.Budget.Limits.MaxWorkingBytes;
        // A tight numeric cap uses the same state calculation without caching.
        // For ordinary Boolean search, skip this optional scratch when the
        // host owns admission. A refused loan would be terminal.
        var cache = input.Length - fromByte >= 64 && (program.NonBacktrackingAcceptClosure & 1) == 0 &&
            (program.IsNonBacktracking || !invocation.Ledger.HasMemoryAllowance) &&
            (workingLimit == 0 || tableBytes <= workingLimit && invocation.Ledger.ReservedBytes <= workingLimit - tableBytes);
        var reserved = cache ? tableBytes : 0;
        var breach = invocation.TryReserveWorking(reserved);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        try
        {
            Span<(ulong States, byte Value, ulong Next)> transitions = stackalloc (ulong, byte, ulong)[cache ? capacity : 0];
            transitions.Clear();
            var hasConditions = program.NonBacktrackingEpsilonOrder.Length != 0;
            var starts = input.Length - fromByte >= 64 ? program.NonBacktrackingOrderedStartBytes : default;
            ulong states = 1;
            var position = fromByte;
            while (true)
            {
                // Bit zero seeds the next start. Other live states can consume
                // bytes that the root proof excludes, so they must advance first.
                if (states == 1 && starts.Count != 0 && position < input.Length &&
                    !starts.Contains(input[position]))
                {
                    breach = TrySeekRegularStart(input, ref position, starts, ref invocation);
                    if (breach != Utf8ExecutionLimitKind.None || position == input.Length) break;
                }
                // Cached transitions still spend a step at each scalar edge.
                breach = invocation.TryChargeStep();
                if (breach != Utf8ExecutionLimitKind.None) break;
                if (hasConditions)
                {
                    breach = ExpandPackedNonBacktrackingConditions(program, input, position, ref states, ref invocation);
                    if (breach != Utf8ExecutionLimitKind.None) break;
                }
                found = (states & program.NonBacktrackingAcceptClosure) != 0;
                if (found || position == input.Length) break;

                var value = input[position];
                var width = 1;
                var ascii = value < 0x80;
                var canCache = ascii && cache;
                var slot = 0;
                if (canCache)
                {
                    slot = (int)((states ^ (states >> 32) ^ (uint)(value * 13)) & (capacity - 1));
                    ref readonly var transition = ref transitions[slot];
                    if (transition.States != 0 && (transition.States != states || transition.Value != value))
                    {
                        // Keep a resident primary key across collisions. The
                        // paired slot is inside the same admitted capacity.
                        slot ^= capacity / 2;
                        transition = ref transitions[slot];
                    }
                    if (transition.States == states && transition.Value == value)
                    {
                        states = transition.Next;
                        position++;
                        continue;
                    }
                }

                Rune scalar;
                if (ascii) scalar = new Rune(value);
                else if (!TryReadScalar(input, position, false, out scalar, out width)) break;
                breach = AdvancePackedNonBacktracking(program, states, scalar, ref invocation, out var next);
                if (breach != Utf8ExecutionLimitKind.None) break;
                if (canCache) transitions[slot] = (states, value, next);
                states = next;
                position += width;
            }
            invocation.Budget.ThrowIfCancellationRequested();
            invocation.Budget.CheckDeadline();
            return breach;
        }
        finally { invocation.Ledger.Release(reserved); }
    }

    private static Utf8ExecutionLimitKind ExpandPackedNonBacktrackingConditions(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, ref ulong states, ref Utf8SliceInvocation invocation)
    {
        // Packed viability walks this order backwards through the graph.
        // Walk it in reverse so each condition sees the roots admitted by its
        // predecessors. Cache only the following consuming transition.
        var order = program.NonBacktrackingEpsilonOrder;
        for (var index = order.Length - 1; index >= 0; index--)
        {
            var pc = order[index];
            if ((program.NonBacktrackingEpsilonAncestors[pc] & states) == 0) continue;
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var instruction = program.Instructions[pc];
            var allows = instruction.Kind == Utf8SliceInstructionKind.Anchor
                ? HoldsNonBacktrackingAnchor(instruction, input, position)
                : MatchesBoundary(program, instruction, input, position);
            if (allows) states |= 1UL << (pc + 1);
        }
        return Utf8ExecutionLimitKind.None;
    }

    private static Utf8ExecutionLimitKind AdvancePackedNonBacktracking(Utf8SliceProgram program,
        ulong states, Rune scalar, ref Utf8SliceInvocation invocation, out ulong next)
    {
        // Unanchored search admits a new start at every scalar edge. All
        // consuming instructions have a following instruction in this graph.
        next = 1;
        var consumers = program.NonBacktrackingConsumerMask;
        while (consumers != 0)
        {
            var pc = BitOperations.TrailingZeroCount(consumers);
            consumers &= consumers - 1;
            if ((program.NonBacktrackingEpsilonAncestors[pc] & states) == 0) continue;
            var hit = MatchesNonBacktrackingAtom(program, program.Instructions[pc], scalar, ref invocation, out var breach);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (hit) next |= 1UL << (pc + 1);
        }
        return Utf8ExecutionLimitKind.None;
    }

    // Ordered Thompson search: earlier starts and preferred branches enter a
    // boundary's closure first. The first visit to a state dominates all later
    // visits because this grammar never reads captures or counter state. Each
    // state is processed at most once per scalar boundary, including unanchored
    // misses. A saved acceptance waits only for its higher-priority threads.
    private static Utf8SliceSearchOutcome RunNonBacktracking(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        int[] captureStarts, int[] captureEnds,
        bool anchored, bool requireNonEmpty, ref Utf8SliceInvocation invocation)
    {
        var workspace = new NonBacktrackingWorkspace();
        var completed = false;
        Array.Fill(captureStarts, -1);
        Array.Fill(captureEnds, -1);
        try
        {
            if (!anchored && program.Instructions.Length > 64)
            {
                var seek = TrySeekInitialRegularStart(program, input, ref fromByte, ref invocation);
                if (seek != Utf8ExecutionLimitKind.None || fromByte < 0)
                    return new(false, -1, -1, seek);
            }
            var breach = workspace.Initialize(program, captureStarts.Length != 0, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
            var bestStart = -1;
            var bestEnd = -1;
            var position = fromByte;
            while (true)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
                invocation.Budget.ThrowIfCancellationRequested();
                if (bestStart < 0 && (!anchored || position == fromByte))
                {
                    breach = workspace.Seed(program, input, position, ref invocation);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
                }
                breach = workspace.ClearVisited(ref invocation);
                if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
                var hasScalar = TryReadScalar(input, position, false, out var scalar, out var width);
                for (var thread = 0; thread < workspace.Current.Count; thread++)
                {
                    var pc = workspace.Current.Pcs[thread];
                    var instruction = program.Instructions[pc];
                    var start = workspace.Current.Starts[thread];
                    if (instruction.Kind == Utf8SliceInstructionKind.Accept)
                    {
                        if (requireNonEmpty && start == position) continue;
                        bestStart = start;
                        bestEnd = position;
                        breach = NonBacktrackingWorkspace.Copy(workspace.Current.Registers.AsSpan(thread * workspace.RegisterCount, workspace.RegisterCount),
                            workspace.Best, ref invocation);
                        if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
                        // Lower-priority threads can no longer determine a match.
                        break;
                    }
                    if (!hasScalar) continue;
                    var hit = MatchesNonBacktrackingAtom(program, instruction, scalar, ref invocation, out breach);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
                    if (!hit) continue;
                    breach = workspace.AddClosure(program, input, pc + 1, position + width, start,
                        workspace.Current.Registers.AsSpan(thread * workspace.RegisterCount, workspace.RegisterCount),
                        workspace.Next, ref invocation);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
                }
                if (bestStart >= 0 && workspace.Next.Count == 0)
                {
                    breach = workspace.Project(program, captureStarts, captureEnds, ref invocation);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, breach);
                    invocation.Budget.ThrowIfCancellationRequested();
                    completed = true;
                    return new(true, bestStart, bestEnd, Utf8ExecutionLimitKind.None);
                }
                if (!hasScalar || anchored && workspace.Next.Count == 0)
                    return new(false, -1, -1, Utf8ExecutionLimitKind.None);
                (workspace.Current, workspace.Next) = (workspace.Next, workspace.Current);
                workspace.Next.Count = 0;
                position += width;
            }
        }
        finally
        {
            workspace.Release(ref invocation);
            if (!completed)
            {
                Array.Fill(captureStarts, -1);
                Array.Fill(captureEnds, -1);
            }
        }
    }

    private sealed class NonBacktrackingThreads
    {
        internal NonBacktrackingThreads(int states, int registers)
        {
            Pcs = states == 0 ? [] : new int[states];
            Starts = states == 0 ? [] : new int[states];
            Registers = checked(states * registers) == 0 ? [] : new int[checked(states * registers)];
        }
        internal readonly int[] Pcs;
        internal readonly int[] Starts;
        internal readonly int[] Registers;
        internal int Count;
    }

    private sealed class NonBacktrackingWorkspace
    {
        internal NonBacktrackingThreads Current = new(0, 0);
        internal NonBacktrackingThreads Next = new(0, 0);
        internal int RegisterCount;
        internal int[] Best = [];
        internal int[] Scratch = [];
        internal int[] PendingPcs = [];
        internal int[] PendingRegisters = [];
        internal bool[] Visited = [];
        internal ulong Reserved;

        internal Utf8ExecutionLimitKind Initialize(Utf8SliceProgram program, bool recordCaptures, ref Utf8SliceInvocation invocation)
        {
            var states = program.Instructions.Length;
            var registers = recordCaptures ? (long)program.GroupCount * 2 + program.NonBacktrackingCaptureOpeningCount : 0;
            if (registers > Array.MaxLength || registers * states > Array.MaxLength)
            {
                invocation.Budget.ThrowIfCancellationRequested();
                invocation.Budget.CheckDeadline();
                throw new InvalidOperationException("The NonBacktracking capture registers exceed the maximum managed array length.");
            }
            RegisterCount = (int)registers;
            var cells = checked(states * RegisterCount);
            var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int>(states) * 5
                + Utf8ConstructionBudget.ArrayBytes<int>(cells) * 3
                + Utf8ConstructionBudget.ArrayBytes<int>(RegisterCount) * 2
                + Utf8ConstructionBudget.ArrayBytes<bool>(states));
            var breach = invocation.TryReserveWorking(bytes);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            Reserved = bytes;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            Current = new(states, RegisterCount);
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            Next = new(states, RegisterCount);
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            Best = RegisterCount == 0 ? [] : new int[RegisterCount];
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            Scratch = RegisterCount == 0 ? [] : new int[RegisterCount];
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            PendingPcs = new int[states];
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            PendingRegisters = cells == 0 ? [] : new int[cells];
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            Visited = new bool[states];
            return Utf8ExecutionLimitKind.None;
        }

        internal void Release(ref Utf8SliceInvocation invocation)
        {
            invocation.Ledger.Release(Reserved);
            Reserved = 0;
        }

        internal Utf8ExecutionLimitKind ClearVisited(ref Utf8SliceInvocation invocation)
        {
            for (var offset = 0; offset < Visited.Length; offset += 4096)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                Visited.AsSpan(offset, Math.Min(4096, Visited.Length - offset)).Clear();
            }
            return Utf8ExecutionLimitKind.None;
        }

        internal static Utf8ExecutionLimitKind Copy(ReadOnlySpan<int> source, Span<int> destination, ref Utf8SliceInvocation invocation)
        {
            for (var offset = 0; offset < source.Length; offset += 512)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                var count = Math.Min(512, source.Length - offset);
                source.Slice(offset, count).CopyTo(destination.Slice(offset, count));
            }
            return Utf8ExecutionLimitKind.None;
        }

        internal Utf8ExecutionLimitKind Seed(Utf8SliceProgram program, ReadOnlySpan<byte> input, int position, ref Utf8SliceInvocation invocation)
        {
            for (var offset = 0; offset < Scratch.Length; offset += 512)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                Scratch.AsSpan(offset, Math.Min(512, Scratch.Length - offset)).Fill(-1);
            }
            return AddClosure(program, input, 0, position, position, Scratch, Current, ref invocation);
        }

        internal Utf8ExecutionLimitKind AddClosure(Utf8SliceProgram program, ReadOnlySpan<byte> input,
            int pc, int position, int start, ReadOnlySpan<int> registers,
            NonBacktrackingThreads output, ref Utf8SliceInvocation invocation)
        {
            var breach = Copy(registers, Scratch, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var pending = 0;
            while (true)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                invocation.Budget.ThrowIfCancellationRequested();
                if (!Visited[pc])
                {
                    Visited[pc] = true;
                    var instruction = program.Instructions[pc];
                    switch (instruction.Kind)
                    {
                        case Utf8SliceInstructionKind.Fork:
                            breach = invocation.Budget.TryNoteDepth((uint)pending + 1);
                            if (breach != Utf8ExecutionLimitKind.None) return breach;
                            PendingPcs[pending] = instruction.B == 0 ? instruction.A : pc + 1;
                            breach = Copy(Scratch, PendingRegisters.AsSpan(pending * RegisterCount, RegisterCount), ref invocation);
                            if (breach != Utf8ExecutionLimitKind.None) return breach;
                            pending++;
                            pc = instruction.B == 0 ? pc + 1 : instruction.A;
                            continue;
                        case Utf8SliceInstructionKind.Jump:
                            pc = instruction.A;
                            continue;
                        case Utf8SliceInstructionKind.CaptureStart:
                        case Utf8SliceInstructionKind.CaptureEnd:
                            if (RegisterCount == 0)
                            {
                                // These markers have one continuation and write
                                // no state when discovering a range without captures.
                                pc = instruction.C != 0 ? instruction.C : pc + 1;
                                continue;
                            }
                            if (instruction.Kind == Utf8SliceInstructionKind.CaptureStart)
                                Scratch[program.GroupCount * 2 + instruction.B] = position;
                            else
                            {
                                Scratch[instruction.A * 2] = Scratch[program.GroupCount * 2 + instruction.B];
                                Scratch[instruction.A * 2 + 1] = position;
                            }
                            pc++;
                            continue;
                        case Utf8SliceInstructionKind.Anchor:
                            if (HoldsNonBacktrackingAnchor(instruction, input, position)) { pc++; continue; }
                            break;
                        case Utf8SliceInstructionKind.Boundary:
                            var beforeWord = TryReadScalar(input, position, true, out var before, out _) &&
                                Utf8SliceScalarClasses.IsBoundaryWord(before, program, instruction);
                            var afterWord = TryReadScalar(input, position, false, out var after, out _) &&
                                Utf8SliceScalarClasses.IsBoundaryWord(after, program, instruction);
                            if ((beforeWord != afterWord) == (instruction.A != 0)) { pc++; continue; }
                            break;
                        case Utf8SliceInstructionKind.Fail:
                            break;
                        default:
                            output.Pcs[output.Count] = pc;
                            output.Starts[output.Count] = start;
                            breach = Copy(Scratch, output.Registers.AsSpan(output.Count * RegisterCount, RegisterCount), ref invocation);
                            if (breach != Utf8ExecutionLimitKind.None) return breach;
                            output.Count++;
                            break;
                    }
                }
                if (pending == 0) return Utf8ExecutionLimitKind.None;
                pending--;
                pc = PendingPcs[pending];
                breach = Copy(PendingRegisters.AsSpan(pending * RegisterCount, RegisterCount), Scratch, ref invocation);
                if (breach != Utf8ExecutionLimitKind.None) return breach;
            }
        }

        internal Utf8ExecutionLimitKind Project(Utf8SliceProgram program, int[] captureStarts, int[] captureEnds,
            ref Utf8SliceInvocation invocation)
        {
            var groupCount = RegisterCount == 0 ? 0 : program.GroupCount;
            for (var group = 0; group < groupCount; group++)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                captureStarts[group] = Best[group * 2];
                captureEnds[group] = Best[group * 2 + 1];
            }
            return Utf8ExecutionLimitKind.None;
        }
    }

    internal static bool HoldsNonBacktrackingAnchor(in Utf8SliceInstruction instruction, ReadOnlySpan<byte> input, int position)
        => instruction.A switch
        {
            (int)Utf8SliceAnchorKind.Beginning => position == 0 || instruction.B != 0 && position > 0 && input[position - 1] == (byte)'\n',
            (int)Utf8SliceAnchorKind.VeryEnd => position == input.Length,
            (int)Utf8SliceAnchorKind.End => position == input.Length ||
                position < input.Length && input[position] == (byte)'\n' && (instruction.B != 0 || position + 1 == input.Length),
            _ => false,
        };

    internal static bool MatchesNonBacktrackingAtom(Utf8SliceProgram program, Utf8SliceInstruction instruction, Rune scalar,
        ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        // Admit bulk classes before their first block. Constant-size predicates
        // still charge work and check cancellation on the VM clock cadence.
        breach = instruction.Kind is Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.CategorySet
            ? invocation.TryChargeInputStep()
            : invocation.TryChargeStep();
        if (breach != Utf8ExecutionLimitKind.None) return false;
        return MatchesRegularAtom(program, instruction, scalar, ref invocation, out breach);
    }

    private static bool MatchesRegularAtom(Utf8SliceProgram program, Utf8SliceInstruction instruction, Rune scalar,
        ref Utf8SliceInvocation invocation, out Utf8ExecutionLimitKind breach)
    {
        breach = Utf8ExecutionLimitKind.None;
        switch (instruction.Kind)
        {
            case Utf8SliceInstructionKind.Literal:
                return scalar.Value == instruction.A || instruction.B != 0 && Rune.IsValid(instruction.A) &&
                    Utf8SliceCaseEquivalences.ScalarsEquivalent(scalar, new Rune(instruction.A), program.CaseBehavior);
            case Utf8SliceInstructionKind.Dot:
                return instruction.A != 0 || scalar.Value != '\n';
            case Utf8SliceInstructionKind.Set when instruction.D != 0:
                return instruction.MatchesAsciiInterval(scalar.Value);
            case Utf8SliceInstructionKind.Set:
                return Utf8SliceScalarClasses.MatchesMask(scalar, program.SetMasks[instruction.A], instruction.C != 0, program.CaseBehavior) != (instruction.B != 0);
            case Utf8SliceInstructionKind.Shorthand:
                return Utf8SliceScalarClasses.MatchesShorthand(scalar, (Utf8SliceShorthand)instruction.A);
            case Utf8SliceInstructionKind.Hybrid:
            case Utf8SliceInstructionKind.CategorySet:
                breach = TryMatchScalarClass(program, instruction, scalar, ref invocation, out var hit);
                return hit;
            default:
                throw new InvalidOperationException("A nonregular instruction entered the NonBacktracking atom runner.");
        }
    }

}
