namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    // Ordered Thompson search: earlier starts and preferred branches enter a
    // boundary's closure first. The first visit to a state dominates all later
    // visits because this grammar never reads captures or counter state. Each
    // state is processed at most once per scalar boundary, including unanchored
    // misses. A saved acceptance waits only for its higher-priority threads.
    private static Utf8SliceSearchOutcome RunNonBacktracking(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, int fromByte,
        int[] captureStarts, int[] captureEnds, bool collectHistory,
        bool anchored, bool requireNonEmpty, ref Utf8SliceInvocation invocation)
    {
        var workspace = new NonBacktrackingWorkspace();
        var completed = false;
        Array.Fill(captureStarts, -1);
        Array.Fill(captureEnds, -1);
        try
        {
            var breach = workspace.Initialize(program, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
            var bestStart = -1;
            var bestEnd = -1;
            var position = fromByte;
            while (true)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                invocation.Budget.ThrowIfCancellationRequested();
                if (bestStart < 0 && (!anchored || position == fromByte))
                {
                    breach = workspace.Seed(program, input, position, ref invocation);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                }
                breach = workspace.ClearVisited(ref invocation);
                if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
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
                        if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                        // Lower-priority threads can no longer determine a match.
                        break;
                    }
                    if (!hasScalar) continue;
                    var hit = MatchesNonBacktrackingAtom(program, instruction, scalar, ref invocation, out breach);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                    if (!hit) continue;
                    breach = workspace.AddClosure(program, input, pc + 1, position + width, start,
                        workspace.Current.Registers.AsSpan(thread * workspace.RegisterCount, workspace.RegisterCount),
                        workspace.Next, ref invocation);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                }
                if (bestStart >= 0 && workspace.Next.Count == 0)
                {
                    breach = workspace.Project(program, captureStarts, captureEnds, collectHistory,
                        ref invocation, out var starts, out var lengths);
                    if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, -1, null, null, breach);
                    try
                    {
                        invocation.Budget.ThrowIfCancellationRequested();
                        completed = true;
                        return new(true, bestStart, bestEnd, starts, lengths, Utf8ExecutionLimitKind.None);
                    }
                    catch
                    {
                        ReleaseHistory(starts, lengths, ref invocation);
                        throw;
                    }
                }
                if (!hasScalar || anchored && workspace.Next.Count == 0)
                    return new(false, -1, -1, null, null, Utf8ExecutionLimitKind.None);
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

        internal Utf8ExecutionLimitKind Initialize(Utf8SliceProgram program, ref Utf8SliceInvocation invocation)
        {
            var states = program.Instructions.Length;
            var registers = (long)program.GroupCount * 2 + program.NonBacktrackingCaptureOpeningCount;
            if (registers > Array.MaxLength || registers * states > Array.MaxLength)
                return Utf8ExecutionLimitKind.WorkingBytes;
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
                            Scratch[program.GroupCount * 2 + instruction.B] = position;
                            pc++;
                            continue;
                        case Utf8SliceInstructionKind.CaptureEnd:
                            Scratch[instruction.A * 2] = Scratch[program.GroupCount * 2 + instruction.B];
                            Scratch[instruction.A * 2 + 1] = position;
                            pc++;
                            continue;
                        case Utf8SliceInstructionKind.Anchor:
                            if (HoldsNonBacktrackingAnchor(instruction, input, position)) { pc++; continue; }
                            break;
                        case Utf8SliceInstructionKind.Boundary:
                            var beforeWord = TryReadScalar(input, position, true, out var before, out _) &&
                                Utf8SliceScalarClasses.IsWord(before, true, instruction.B != 0);
                            var afterWord = TryReadScalar(input, position, false, out var after, out _) &&
                                Utf8SliceScalarClasses.IsWord(after, true, instruction.B != 0);
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
            bool collectHistory, ref Utf8SliceInvocation invocation, out int[][]? starts, out int[][]? lengths)
        {
            starts = null;
            lengths = null;
            for (var group = 0; group < program.GroupCount; group++)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                captureStarts[group] = Best[group * 2];
                captureEnds[group] = Best[group * 2 + 1];
            }
            return BuildNonBacktrackingHistory(captureStarts, captureEnds, collectHistory, ref invocation, out starts, out lengths);
        }
    }

    internal static Utf8ExecutionLimitKind BuildNonBacktrackingHistory(int[] captureStarts, int[] captureEnds,
        bool collectHistory, ref Utf8SliceInvocation invocation, out int[][]? starts, out int[][]? lengths)
    {
        starts = null;
        lengths = null;
        var participants = 0;
        if (collectHistory)
            for (var group = 0; group < captureStarts.Length; group++)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                if (captureStarts[group] >= 0) participants++;
            }
        if (!collectHistory) return Utf8ExecutionLimitKind.None;
        var bytes = checked(Utf8ConstructionBudget.ArrayBytes<int[]>(captureStarts.Length) * 2
            + (ulong)participants * Utf8ConstructionBudget.ArrayBytes<int>(1) * 2);
        var resultAdmission = invocation.Budget.TryChargeResultBytes(bytes);
        if (resultAdmission != Utf8ExecutionLimitKind.None) return resultAdmission;
        var admission = invocation.TryReserveWorking(bytes);
        if (admission != Utf8ExecutionLimitKind.None) return admission;
        var published = false;
        try
        {
            if (bytes != 0 && (admission = invocation.TryChargeInputStep()) != Utf8ExecutionLimitKind.None) return admission;
            var resultStarts = captureStarts.Length == 0 ? [] : new int[captureStarts.Length][];
            var resultLengths = captureStarts.Length == 0 ? [] : new int[captureStarts.Length][];
            for (var group = 0; group < captureStarts.Length; group++)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return breach;
                resultStarts[group] = captureStarts[group] >= 0 ? [captureStarts[group]] : [];
                resultLengths[group] = captureStarts[group] >= 0 ? [captureEnds[group] - captureStarts[group]] : [];
            }
            starts = resultStarts;
            lengths = resultLengths;
            published = true;
            return Utf8ExecutionLimitKind.None;
        }
        finally { if (!published) invocation.Ledger.Release(bytes); }
    }

    internal static bool HoldsNonBacktrackingAnchor(Utf8SliceInstruction instruction, ReadOnlySpan<byte> input, int position)
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
        breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return false;
        switch (instruction.Kind)
        {
            case Utf8SliceInstructionKind.Literal:
                return scalar.Value == instruction.A || instruction.B != 0 && Rune.IsValid(instruction.A) &&
                    Utf8SliceCaseEquivalences.ScalarsEquivalent(scalar, new Rune(instruction.A), program.CaseBehavior);
            case Utf8SliceInstructionKind.Dot:
                return instruction.A != 0 || scalar.Value != '\n';
            case Utf8SliceInstructionKind.Set:
                return Utf8SliceScalarClasses.MatchesMask(scalar, program.SetMasks[instruction.A], instruction.C != 0, program.CaseBehavior) != (instruction.B != 0);
            case Utf8SliceInstructionKind.Shorthand:
                return Utf8SliceScalarClasses.MatchesShorthand(scalar, (Utf8SliceShorthand)instruction.A);
            case Utf8SliceInstructionKind.Category:
            case Utf8SliceInstructionKind.Hybrid:
            case Utf8SliceInstructionKind.CategorySet:
                breach = TryMatchScalarClass(program, instruction, scalar, ref invocation, out var hit);
                return hit;
            default:
                throw new InvalidOperationException("A nonregular instruction entered the NonBacktracking atom runner.");
        }
    }

}
