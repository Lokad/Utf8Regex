using System.Buffers.Binary;
using System.Numerics;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    internal static bool TryCountPreparedRepeatedSegment(Utf8SliceProgram program,
        in AsciiStructuralRepeatedSegmentPlan plan, ReadOnlySpan<byte> input, int fromByte,
        ref Utf8SliceInvocation invocation, out Utf8SliceCountOutcome outcome)
    {
        outcome = default;
        var code = program.Instructions.AsSpan();
        // The existing plan proves disjoint ASCII classes. Relate it to the
        // complete scalar root before borrowing its masks; the separator keeps
        // its original Unicode predicate. Other operations/grammars keep the VM.
        if (!plan.HasValue || program.GroupCount != 0 || program.RightToLeft || program.IgnoreCase ||
            program.IsNonBacktracking || code.Length != 13 || (uint)fromByte > (uint)input.Length ||
            fromByte < input.Length && (input[fromByte] & 0xc0) == 0x80 ||
            code[0] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, A: 11, B: 0 } ||
            code[0].RepeatMinimum != plan.RepetitionMinCount || code[0].RepeatMaximum != plan.RepetitionMaxCount ||
            !MatchesPreparedAsciiSet(program, code[1], plan.LeadingCharClass) ||
            code[2] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, A: 5, B: 1 } ||
            code[2].RepeatMinimum != plan.TrailingMinCount || code[2].RepeatMaximum != long.MaxValue ||
            !MatchesPreparedAsciiSet(program, code[3], plan.TrailingCharClass) ||
            code[4] is not { Kind: Utf8SliceInstructionKind.RepeatEnd, A: 2 } ||
            code[5] is not { Kind: Utf8SliceInstructionKind.RepeatExit, A: 1 } ||
            code[6] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, A: 9, B: 2 } ||
            code[6].RepeatMinimum != plan.SeparatorMinCount || code[6].RepeatMaximum != long.MaxValue ||
            code[7].Kind is not (Utf8SliceInstructionKind.Set or Utf8SliceInstructionKind.Shorthand or
                Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.CategorySet) ||
            code[8] is not { Kind: Utf8SliceInstructionKind.RepeatEnd, A: 6 } ||
            code[9] is not { Kind: Utf8SliceInstructionKind.RepeatExit, A: 2 } ||
            code[10] is not { Kind: Utf8SliceInstructionKind.RepeatEnd, A: 0 } ||
            code[11] is not { Kind: Utf8SliceInstructionKind.RepeatExit, A: 0 } ||
            code[12].Kind != Utf8SliceInstructionKind.Accept)
            return false;

        outcome = CountPreparedRepeatedSegments(program, plan, input, fromByte, ref invocation);
        return true;
    }

    private static bool MatchesPreparedAsciiSet(Utf8SliceProgram program,
        Utf8SliceInstruction instruction, AsciiCharClass expected)
    {
        if (instruction is not { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } ||
            (uint)instruction.A >= (uint)program.SetMasks.Length) return false;
        var mask = program.SetMasks[instruction.A];
        return mask.Length == 16 && BinaryPrimitives.ReadUInt64LittleEndian(mask) == expected.LowMask &&
            BinaryPrimitives.ReadUInt64LittleEndian(mask.AsSpan(8)) == expected.HighMask;
    }

    private static Utf8SliceCountOutcome CountPreparedRepeatedSegments(Utf8SliceProgram program,
        in AsciiStructuralRepeatedSegmentPlan plan, ReadOnlySpan<byte> input, int fromByte,
        ref Utf8SliceInvocation invocation)
    {
        var count = 0;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
        // This path needs no local arrays/frames, but enclosing working loans
        // and the invocation's first denial still govern admission.
        breach = invocation.TryReserveWorking(0);
        if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
        var lower = plan.LeadingCharClass.LowMask;
        var upper = plan.LeadingCharClass.HighMask;
        var first = (byte)(lower != 0 ? BitOperations.TrailingZeroCount(lower) : 64 + BitOperations.TrailingZeroCount(upper));
        var last = (byte)(upper != 0 ? 127 - BitOperations.LeadingZeroCount(upper) : 63 - BitOperations.LeadingZeroCount(lower));
        var position = fromByte;
        while (position < input.Length)
        {
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            if (!plan.LeadingCharClass.Contains(input[position]))
            {
                var block = input.Slice(position, Math.Min(2048, input.Length - position));
                var relative = block.IndexOfAnyInRange(first, last);
                if (relative < 0) { position += block.Length; continue; }
                position += relative;
            }
            // Retain the existing scalar counter's mandatory second-character
            // rejection; a failed pair must not enter the repeated verifier.
            if (!plan.LeadingCharClass.Contains(input[position]) || position + 1 == input.Length ||
                !plan.TrailingCharClass.Contains(input[position + 1])) { position++; continue; }
            var candidate = position;
            var repetitions = 0;
            while (repetitions < plan.RepetitionMaxCount && position < input.Length &&
                plan.LeadingCharClass.Contains(input[position]))
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                var end = position + 1;
                var tail = 0;
                while (end < input.Length && plan.TrailingCharClass.Contains(input[end]))
                {
                    if ((tail & 63) == 0)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                    }
                    end++;
                    tail++;
                }
                if (tail < plan.TrailingMinCount) break;
                var separators = 0;
                while (end < input.Length)
                {
                    if ((separators & 63) == 0)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                    }
                    var width = 1;
                    if (input[end] < 128)
                    {
                        if (!plan.SeparatorCharClass.Contains(input[end])) break;
                    }
                    else
                    {
                        _ = Rune.DecodeFromUtf8(input[end..], out var scalar, out width);
                        var hit = MatchesNonBacktrackingAtom(program, program.Instructions[7], scalar, ref invocation, out breach);
                        if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                        if (!hit) break;
                    }
                    end += width;
                    separators++;
                }
                if (separators < plan.SeparatorMinCount) break;
                position = end;
                repetitions++;
            }
            // Disjoint classes make the greedy segments unambiguous. A failed
            // attempt retains the existing native search's next-byte retry.
            if (repetitions >= plan.RepetitionMinCount) count++;
            else position = candidate + 1;
        }
        breach = invocation.TryChargeInputStep();
        // TryChargeInputStep already polls the deadline. Its diagnostic clock
        // can cancel the token, so the token check must follow the last poll.
        invocation.Budget.ThrowIfCancellationRequested();
        return new(count, breach);
    }
}
