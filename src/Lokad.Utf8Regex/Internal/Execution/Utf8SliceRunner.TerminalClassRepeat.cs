using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    private static bool CanRunTerminalClassRepeat(Utf8SliceProgram program)
    {
        if (program.IsNonBacktracking || program.RightToLeft || program.GroupCount != 0 ||
            program.RepeatCount != 1 || program.Instructions.Length != 5)
            return false;
        var instructions = program.Instructions;
        var repeat = instructions[0];
        var atom = instructions[1];
        if (repeat.Kind != Utf8SliceInstructionKind.RepeatGreedy ||
            repeat.A != 3 || repeat.B != 0 || repeat.C < 1 || repeat.D < repeat.C ||
            instructions[2].Kind != Utf8SliceInstructionKind.RepeatEnd || instructions[2].A != 0 ||
            instructions[3].Kind != Utf8SliceInstructionKind.RepeatExit || instructions[3].A != 0 ||
            instructions[4].Kind != Utf8SliceInstructionKind.Accept ||
            atom.Kind is not (Utf8SliceInstructionKind.Shorthand or Utf8SliceInstructionKind.Category or
                Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid))
            return false;
        // Keep each fused base step bounded. A nested class program retains its
        // own metering; a long list of independent members stays on the VM path.
        return atom.Kind != Utf8SliceInstructionKind.CategorySet ||
            program.CategorySetMembers[atom.D].Length <= 1;
    }

    private static Utf8SliceCountOutcome CountTerminalClassRepeat(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int start, ref Utf8SliceInvocation invocation)
    {
        // This complete root has no tail to reconsider a greedy boundary.
        // Publish full maxima and discard each final run below the minimum.
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
        // Observe an enclosing loan without acquiring any local scratch.
        breach = invocation.TryReserveWorking(0);
        if (breach != Utf8ExecutionLimitKind.None) return new(0, breach);
        var repeat = program.Instructions[0];
        var atom = program.Instructions[1];
        var position = start;
        var run = 0;
        var count = 0;
        while (position < input.Length)
        {
            breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            if (!TryReadScalar(input, position, false, out var scalar, out var width)) break;
            breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out var hit);
            if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            if (hit)
            {
                position += width;
                if (++run == repeat.D)
                {
                    count++;
                    run = 0;
                    invocation.AnchorOriginByte = position;
                }
            }
            else
            {
                if (run >= repeat.C)
                {
                    count++;
                    invocation.AnchorOriginByte = position;
                }
                run = 0;
                position += width;
            }
        }
        if (run >= repeat.C)
        {
            count++;
            invocation.AnchorOriginByte = position;
        }
        breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return new(count, breach);
    }

    private static Utf8SliceMatchOutcome RunTerminalClassRepeat(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int start, ref Utf8SliceInvocation invocation)
        => RunClassRepeat(program, 0, input, start, ref invocation);

    private static bool TryRunClassRepeatWithoutRewind(Utf8SliceProgram program, int repeatIndex,
        ReadOnlySpan<byte> input, int start, int? requiredEnd, ref Utf8SliceInvocation invocation,
        out Utf8SliceMatchOutcome outcome)
    {
        outcome = default;
        var instructions = program.Instructions;
        if (program.RightToLeft || instructions.Length - repeatIndex < 5) return false;
        var repeat = instructions[repeatIndex];
        var atom = instructions[repeatIndex + 1];
        if (repeat.Kind != Utf8SliceInstructionKind.RepeatGreedy || repeat.A != repeatIndex + 3 ||
            instructions[repeatIndex + 2].Kind != Utf8SliceInstructionKind.RepeatEnd ||
            instructions[repeatIndex + 2].A != repeatIndex ||
            instructions[repeatIndex + 3].Kind != Utf8SliceInstructionKind.RepeatExit ||
            instructions[repeatIndex + 3].A != repeat.B ||
            atom.Kind is not (Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Shorthand or
                Utf8SliceInstructionKind.Category or Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid))
            return false;
        var tail = repeatIndex + 4;
        if (instructions[tail].Kind == Utf8SliceInstructionKind.CaptureEnd) tail++;
        if (tail == instructions.Length) return false;
        var literal = instructions[tail];
        // RunFrom can retry a child after Accept to satisfy its required end.
        // Without that constraint, no shorter successful endpoint is requested.
        if (literal.Kind == Utf8SliceInstructionKind.Accept && requiredEnd is null)
        {
            outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
            return true;
        }
        if (literal.Kind != Utf8SliceInstructionKind.Literal || literal.B != 0 || !Rune.IsValid(literal.A))
            return false;

        // Every shorter greedy boundary points at a scalar accepted by this
        // side-effect-free atom. If the following exact literal is excluded,
        // none of those alternatives can succeed. CaptureEnd still executes
        // normally, with its original trail and publication authority.
        var breach = invocation.TryChargeStep();
        var includesLiteral = false;
        if (breach == Utf8ExecutionLimitKind.None)
            breach = TryMatchScalarClass(program, atom, new Rune(literal.A), ref invocation, out includesLiteral);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            outcome = new(false, -1, breach, null, null);
            return true;
        }
        if (includesLiteral) return false;
        outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
        return true;
    }

    private static Utf8SliceMatchOutcome RunClassRepeat(Utf8SliceProgram program, int repeatIndex,
        ReadOnlySpan<byte> input, int start, ref Utf8SliceInvocation invocation)
    {
        var repeat = program.Instructions[repeatIndex];
        var atom = program.Instructions[repeatIndex + 1];
        var position = start;
        var count = 0;
        while (count < repeat.D)
        {
            var breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach, null, null);
            if (!TryReadScalar(input, position, false, out var scalar, out var width)) break;
            breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out var hit);
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach, null, null);
            if (!hit) break;
            position += width;
            count++;
        }
        var terminal = invocation.TryChargeInputStep();
        if (terminal != Utf8ExecutionLimitKind.None) return new(false, -1, terminal, null, null);
        return count >= repeat.C ? new(true, position, Utf8ExecutionLimitKind.None, null, null)
            : new(false, -1, Utf8ExecutionLimitKind.None, null, null);
    }

    private static Utf8ExecutionLimitKind TryMatchScalarClass(Utf8SliceProgram program,
        Utf8SliceInstruction instruction, Rune scalar, ref Utf8SliceInvocation invocation, out bool hit)
    {
        if (instruction.Kind == Utf8SliceInstructionKind.Set)
        {
            hit = Utf8SliceScalarClasses.MatchesMask(scalar, program.SetMasks[instruction.A],
                instruction.C != 0, program.CaseBehavior) != (instruction.B != 0);
            return Utf8ExecutionLimitKind.None;
        }
        if (instruction.Kind == Utf8SliceInstructionKind.Shorthand)
        {
            hit = Utf8SliceScalarClasses.MatchesShorthand(scalar, (Utf8SliceShorthand)instruction.A);
            return Utf8ExecutionLimitKind.None;
        }
        if (instruction.Kind == Utf8SliceInstructionKind.Category)
        {
            hit = Utf8SliceScalarClasses.MatchesTable(scalar, program.CategoryTables[instruction.A],
                (instruction.B & 2) != 0, (instruction.B & 1) != 0, program.CaseBehavior, ref invocation, out var breach);
            return breach;
        }
        var categorySet = instruction.Kind == Utf8SliceInstructionKind.CategorySet;
        var members = categorySet ? program.CategorySetMembers[instruction.D] : null;
        if (members is { Length: 1 } && members[0].Table.ClassProgram is { } classProgram)
        {
            hit = classProgram.TryMatch(scalar, program.CaseBehavior, ref invocation, out var breach);
            return breach;
        }
        var fold = categorySet ? (instruction.B & 2) != 0 : instruction.D != 0;
        var memberHit = Utf8SliceScalarClasses.MatchesMask(scalar, program.SetMasks[instruction.A], fold, program.CaseBehavior) ||
            Utf8SliceScalarClasses.MatchesMembers(scalar, instruction.C);
        if (!memberHit && members is not null)
        {
            memberHit = Utf8SliceScalarClasses.MatchesTables(scalar, members, fold, program.CaseBehavior,
                ref invocation, out var breach);
            if (breach != Utf8ExecutionLimitKind.None) { hit = false; return breach; }
        }
        hit = memberHit != ((instruction.B & 1) != 0);
        return Utf8ExecutionLimitKind.None;
    }
}
