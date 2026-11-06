using System.Buffers.Binary;
using System.Numerics;
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
            repeat.A != 3 || repeat.B != 0 || repeat.RepeatMinimum < 0 || repeat.RepeatMaximum < repeat.RepeatMinimum ||
            instructions[2].Kind != Utf8SliceInstructionKind.RepeatEnd || instructions[2].A != 0 ||
            instructions[3].Kind != Utf8SliceInstructionKind.RepeatExit || instructions[3].A != 0 ||
            instructions[4].Kind != Utf8SliceInstructionKind.Accept ||
            atom.Kind is not (Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Shorthand or
                Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid))
            return false;
        if (atom.Kind == Utf8SliceInstructionKind.Literal && atom.B != 0) return false;
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
        ReadOnlySpan<byte> asciiMask = atom.Kind == Utf8SliceInstructionKind.Set && atom.B == 0 && atom.C == 0
            ? program.SetMasks[atom.A] : [];
        var minimum = repeat.RepeatMinimum;
        var maximum = repeat.RepeatMaximum;
        var position = start;
        var run = 0;
        var count = 0;
        while (position < input.Length)
        {
            breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            bool hit;
            int width;
            if (!asciiMask.IsEmpty && input[position] < 128)
            {
                // Same positive ASCII predicate as RunClassRepeat, with the
                // original per-scalar charge. Wider/folded/complemented masks
                // retain full scalar decoding and membership semantics.
                var value = input[position];
                hit = (asciiMask[value >> 3] & (1 << (value & 7))) != 0;
                width = 1;
            }
            else
            {
                if (!TryReadScalar(input, position, false, out var scalar, out width)) break;
                breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out hit);
                if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            }
            if (hit)
            {
                position += width;
                if (++run == maximum)
                {
                    count++;
                    run = 0;
                    invocation.AnchorOriginByte = position;
                }
            }
            else
            {
                if (run >= minimum)
                {
                    count++;
                    invocation.AnchorOriginByte = position;
                }
                run = 0;
                position += width;
            }
        }
        if (run >= minimum)
        {
            count++;
            invocation.AnchorOriginByte = position;
        }
        breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return new(count, breach);
    }

    private static Utf8SliceMatchOutcome RunTerminalClassRepeat(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int start, bool requireNonEmpty, ref Utf8SliceInvocation invocation)
    {
        var outcome = RunClassRepeat(program, 0, input, start, ref invocation);
        // A complete greedy atom has no other positive endpoint when its
        // longest match is empty. The profile caller owns scalar advancement.
        return requireNonEmpty && outcome.IsMatch && outcome.EndIndex == start
            ? new(false, -1, outcome.Breach, null, null) : outcome;
    }

    private static bool TryRunClassRepeatWithoutRewind(Utf8SliceProgram program, int repeatIndex,
        ReadOnlySpan<byte> input, int start, int? requiredEnd, ref Utf8SliceInvocation invocation,
        out Utf8SliceMatchOutcome outcome)
    {
        outcome = default;
        var instructions = program.Instructions;
        if (program.RightToLeft || instructions.Length - repeatIndex < 5) return false;
        var repeat = instructions[repeatIndex];
        var atom = instructions[repeatIndex + 1];
        if (repeat.Kind is not (Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy) || repeat.A != repeatIndex + 3 ||
            instructions[repeatIndex + 2].Kind != Utf8SliceInstructionKind.RepeatEnd ||
            instructions[repeatIndex + 2].A != repeatIndex ||
            instructions[repeatIndex + 3].Kind != Utf8SliceInstructionKind.RepeatExit ||
            instructions[repeatIndex + 3].A != repeat.B ||
            atom.Kind is not (Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Shorthand or
                Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Hybrid))
            return false;
        if (atom.Kind == Utf8SliceInstructionKind.Literal && atom.B != 0) return false;
        // A pure atom repeated an exact number of times has one endpoint,
        // independently of its tail or an enclosing assertion's required end.
        if (repeat.RepeatMinimum == repeat.RepeatMaximum)
        {
            outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
            return true;
        }
        var tail = repeatIndex + 4;
        // Nested capture endings do not consume input. They still execute with
        // their original trails after the selected boundary; only this proof
        // looks past them. Keep pathological chains bounded and decline others.
        for (var skipped = 0; skipped < 32 && tail < instructions.Length &&
            instructions[tail].Kind == Utf8SliceInstructionKind.CaptureEnd; skipped++) tail++;
        if (tail == instructions.Length) return false;
        var suffix = instructions[tail];
        // A lazy repeat can skip shorter endpoints only when the following
        // required literal excludes every scalar consumed by its atom.
        if (repeat.Kind == Utf8SliceInstructionKind.RepeatLazy && suffix.Kind != Utf8SliceInstructionKind.Literal)
            return false;
        // RunFrom can retry a child after Accept to satisfy its required end.
        // Without that constraint, no shorter successful endpoint is requested.
        // An atomic commit discards shorter greedy endpoints even if its tail
        // later fails. Capture endings and the commit still execute normally.
        if (suffix.Kind == Utf8SliceInstructionKind.Accept && requiredEnd is null ||
            suffix.Kind == Utf8SliceInstructionKind.AtomicCommit)
        {
            outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
            return true;
        }
        if (suffix.Kind == Utf8SliceInstructionKind.Boundary && suffix.A == 1 &&
            repeat.RepeatMinimum >= 1 && repeat.RepeatMaximum <= 8)
        {
            // Every shorter nonempty endpoint lies between consumed word scalars,
            // where a positive boundary cannot succeed. Prove only this bounded
            // ASCII input window using the actual profile predicate. Zero minima
            // can rewind to a boundary before the first scalar and stay on the VM.
            var proofBreach = invocation.TryChargeStep();
            if (proofBreach != Utf8ExecutionLimitKind.None)
            {
                outcome = new(false, -1, proofBreach, null, null);
                return true;
            }
            var window = input.Slice(start, Math.Min(input.Length - start, (int)repeat.RepeatMaximum));
            var onlyWordCanBeConsumed = true;
            foreach (var value in window)
            {
                if (value >= 128) { onlyWordCanBeConsumed = false; break; }
                if (Utf8SliceScalarClasses.IsBoundaryWord(new Rune(value), program, suffix)) continue;
                // An excluded nonword stops the run before this scalar. Later
                // bytes cannot introduce a consumed nonword or useful rewind.
                proofBreach = TryMatchScalarClass(program, atom, new Rune(value), ref invocation, out var consumesNonword);
                if (proofBreach != Utf8ExecutionLimitKind.None)
                {
                    outcome = new(false, -1, proofBreach, null, null);
                    return true;
                }
                onlyWordCanBeConsumed = !consumesNonword;
                break;
            }
            if (onlyWordCanBeConsumed)
            {
                outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
                return true;
            }
        }
        // A mandatory positive ASCII set excludes every shorter endpoint when
        // none of its members can be consumed by this repeat. A nonempty simple
        // following repeat has the same required first atom. Nullable/folded or
        // complemented followers keep their normal backtracking paths.
        var following = suffix;
        if (following.Kind is Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy)
        {
            if (following.RepeatMinimum < 1 || instructions.Length - tail < 5 || following.A != tail + 3 ||
                instructions[tail + 2].Kind != Utf8SliceInstructionKind.RepeatEnd || instructions[tail + 2].A != tail ||
                instructions[tail + 3].Kind != Utf8SliceInstructionKind.RepeatExit || instructions[tail + 3].A != following.B)
                return false;
            following = instructions[tail + 1];
        }
        if (following is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 })
        {
            ReadOnlySpan<byte> mask = program.SetMasks[following.A].AsSpan(0, 16);
            for (var octet = 0; octet < mask.Length; octet++)
            {
                var bits = (uint)mask[octet];
                while (bits != 0)
                {
                    var value = octet * 8 + BitOperations.TrailingZeroCount(bits);
                    // At most128 members; retain each profile's actual predicate
                    // and charge proof work before inspecting a member.
                    var proofBreach = invocation.TryChargeStep();
                    var overlaps = false;
                    if (proofBreach == Utf8ExecutionLimitKind.None)
                        proofBreach = TryMatchScalarClass(program, atom, new Rune(value), ref invocation, out overlaps);
                    if (proofBreach != Utf8ExecutionLimitKind.None)
                    {
                        outcome = new(false, -1, proofBreach, null, null);
                        return true;
                    }
                    if (overlaps) return false;
                    bits &= bits - 1;
                }
            }
            outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
            return true;
        }
        // End anchors can succeed only at the subject end or before LF. If the
        // atom excludes LF, no shorter endpoint can satisfy either kind of end.
        // Keep this conservative proof for absolute ends as well as multiline $.
        var alternateScalar = -1;
        int excludedScalar;
        if (suffix is { Kind: Utf8SliceInstructionKind.Fork, B: 0 } && suffix.A > tail)
        {
            // Each alternative's first consuming atom must be an excluded literal.
            // Only inspect through bounded capture markers;
            // the VM still executes their effects and chooses the branch normally.
            if (!TryGetLiteralAfterCaptureMarkers(instructions, tail + 1, out excludedScalar) ||
                !TryGetLiteralAfterCaptureMarkers(instructions, suffix.A, out alternateScalar))
                return false;
        }
        else
        {
            excludedScalar = suffix.Kind == Utf8SliceInstructionKind.Literal && suffix.B == 0 && Rune.IsValid(suffix.A)
                ? suffix.A
                : suffix.Kind == Utf8SliceInstructionKind.Anchor &&
                    suffix.A is (int)Utf8SliceAnchorKind.End or (int)Utf8SliceAnchorKind.VeryEnd ? '\n' : -1;
        }
        if (excludedScalar < 0) return false;

        // Every shorter boundary points at a scalar accepted by this
        // side-effect-free atom. If the following literal/end stop is excluded,
        // none of those alternatives can succeed. CaptureEnd still executes
        // normally, with its original trail and publication authority.
        var breach = invocation.TryChargeStep();
        var includesLiteral = false;
        if (breach == Utf8ExecutionLimitKind.None)
            breach = TryMatchScalarClass(program, atom, new Rune(excludedScalar), ref invocation, out includesLiteral);
        if (breach != Utf8ExecutionLimitKind.None)
        {
            outcome = new(false, -1, breach, null, null);
            return true;
        }
        if (includesLiteral)
        {
            if (repeat.Kind == Utf8SliceInstructionKind.RepeatLazy ||
                atom.Kind != Utf8SliceInstructionKind.Dot || suffix.Kind != Utf8SliceInstructionKind.Literal ||
                (uint)suffix.A >= 128 || repeat.RepeatMaximum is < 8 or > 511)
                return false;
            // A scalar consumes at most four bytes. Absence in this conservative
            // window rules out every endpoint; presence still needs normal VM
            // backtracking, including newline, capture and greedy-order rules.
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None)
            {
                outcome = new(false, -1, breach, null, null);
                return true;
            }
            // Bound instruction inspection and the search even for enormous tails.
            // Stop at captures, folds or other instructions; this prefix is only
            // a necessary condition, never a substitute for executing the tail.
            Span<byte> prefix = stackalloc byte[8];
            prefix[0] = (byte)suffix.A;
            var prefixLength = 1;
            while (prefixLength < prefix.Length && prefixLength < instructions.Length - tail)
            {
                var next = instructions[tail + prefixLength];
                if (next.Kind != Utf8SliceInstructionKind.Literal || next.B != 0 || (uint)next.A >= 128)
                    break;
                prefix[prefixLength++] = (byte)next.A;
            }
            // Include the whole prefix at the final possible scalar endpoint.
            var length = Math.Min(input.Length - start, (int)repeat.RepeatMaximum * 4 + prefixLength);
            var window = input.Slice(start, length);
            var possible = prefixLength == 1 ? window.IndexOf(prefix[0]) >= 0
                : window.IndexOf(prefix[..prefixLength]) >= 0;
            breach = invocation.TryChargeInputStep();
            invocation.Budget.ThrowIfCancellationRequested();
            if (breach != Utf8ExecutionLimitKind.None || !possible)
            {
                outcome = new(false, -1, breach, null, null);
                return true;
            }
            return false;
        }
        if (alternateScalar >= 0)
        {
            breach = invocation.TryChargeStep();
            if (breach == Utf8ExecutionLimitKind.None)
                breach = TryMatchScalarClass(program, atom, new Rune(alternateScalar), ref invocation, out includesLiteral);
            if (breach != Utf8ExecutionLimitKind.None)
            {
                outcome = new(false, -1, breach, null, null);
                return true;
            }
            if (includesLiteral) return false;
        }
        outcome = RunClassRepeat(program, repeatIndex, input, start, ref invocation);
        return true;
    }

    private static bool TryGetLiteralAfterCaptureMarkers(ReadOnlySpan<Utf8SliceInstruction> instructions,
        int index, out int scalar)
    {
        for (var inspected = 0; inspected < 32 && (uint)index < (uint)instructions.Length; inspected++, index++)
        {
            var instruction = instructions[index];
            if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                continue;
            if (instruction is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } && Rune.IsValid(instruction.A))
            {
                scalar = instruction.A;
                return true;
            }
            break;
        }
        scalar = -1;
        return false;
    }

    private static Utf8SliceMatchOutcome RunClassRepeat(Utf8SliceProgram program, int repeatIndex,
        ReadOnlySpan<byte> input, int start, ref Utf8SliceInvocation invocation)
    {
        var repeat = program.Instructions[repeatIndex];
        var atom = program.Instructions[repeatIndex + 1];
        var minimum = repeat.RepeatMinimum;
        var maximum = repeat.RepeatMaximum;
        var position = start;
        var count = 0;
        if (minimum <= 1 && maximum == long.MaxValue &&
            atom is { Kind: Utf8SliceInstructionKind.Set, B: 1, C: 0 } &&
            TryGetAsciiMaskPair(program.SetMasks[atom.A], out var first, out var second))
        {
            // Whole-subject validity is established by the caller. ASCII
            // stops cannot occur inside wider scalars; intermediate blocks
            // may split a scalar, but the selected endpoint never does.
            while (position < input.Length)
            {
                var breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach, null, null);
                var block = input.Slice(position, Math.Min(2048, input.Length - position));
                var stop = block.IndexOfAny(first, second);
                if (stop >= 0) { position += stop; break; }
                position += block.Length;
            }
            var terminalBreach = invocation.TryChargeInputStep();
            if (terminalBreach != Utf8ExecutionLimitKind.None) return new(false, -1, terminalBreach, null, null);
            // With no maximum and minimum at most one, nonempty byte extent
            // suffices; bounded/scalar-counted repeats keep the normal loop.
            return position != start || minimum == 0
                ? new(true, position, Utf8ExecutionLimitKind.None, null, null)
                : new(false, -1, Utf8ExecutionLimitKind.None, null, null);
        }
        ReadOnlySpan<byte> asciiMask = atom.Kind == Utf8SliceInstructionKind.Set && atom.B == 0 && atom.C == 0
            ? program.SetMasks[atom.A] : [];
        while (count < maximum)
        {
            var breach = invocation.TryChargeStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach, null, null);
            // This positive, unfolded mask has the same ASCII predicate; keep
            // its existing per-scalar charge without Rune decoding/dispatch.
            if (!asciiMask.IsEmpty && (uint)position < (uint)input.Length && input[position] < 128)
            {
                var value = input[position];
                if ((asciiMask[value >> 3] & (1 << (value & 7))) == 0) break;
                position++;
                count++;
                continue;
            }
            if (!TryReadScalar(input, position, false, out var scalar, out var width)) break;
            breach = TryMatchScalarClass(program, atom, scalar, ref invocation, out var hit);
            if (breach != Utf8ExecutionLimitKind.None) return new(false, -1, breach, null, null);
            if (!hit) break;
            position += width;
            count++;
        }
        var terminal = invocation.TryChargeInputStep();
        if (terminal != Utf8ExecutionLimitKind.None) return new(false, -1, terminal, null, null);
        return count >= minimum ? new(true, position, Utf8ExecutionLimitKind.None, null, null)
            : new(false, -1, Utf8ExecutionLimitKind.None, null, null);
    }

    private static bool TryGetAsciiMaskPair(ReadOnlySpan<byte> mask, out byte first, out byte second)
    {
        first = second = 0;
        var low = BinaryPrimitives.ReadUInt64LittleEndian(mask);
        var high = BinaryPrimitives.ReadUInt64LittleEndian(mask[8..]);
        if (BitOperations.PopCount(low) + BitOperations.PopCount(high) != 2) return false;
        first = (byte)(low != 0 ? BitOperations.TrailingZeroCount(low) : 64 + BitOperations.TrailingZeroCount(high));
        if (low != 0) low &= low - 1;
        else high &= high - 1;
        second = (byte)(low != 0 ? BitOperations.TrailingZeroCount(low) : 64 + BitOperations.TrailingZeroCount(high));
        return true;
    }

    private static Utf8ExecutionLimitKind TryMatchScalarClass(Utf8SliceProgram program,
        Utf8SliceInstruction instruction, Rune scalar, ref Utf8SliceInvocation invocation, out bool hit)
    {
        if (instruction.Kind == Utf8SliceInstructionKind.Literal)
        {
            hit = scalar.Value == instruction.A;
            return Utf8ExecutionLimitKind.None;
        }
        if (instruction.Kind == Utf8SliceInstructionKind.Dot)
        {
            hit = instruction.A != 0 || scalar.Value != '\n';
            return Utf8ExecutionLimitKind.None;
        }
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
