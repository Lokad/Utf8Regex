using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    private static bool TryCountAnchoredLine(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int fromByte, ref Utf8SliceInvocation invocation, out Utf8SliceCountOutcome outcome)
    {
        outcome = default;
        var code = program.Instructions.AsSpan();
        // This bounded proof accepts a complete root. Capture markers have no
        // effect on Count; any other branch, condition or scalar grammar declines.
        if (code.Length > 64 || program.RightToLeft) return false;
        var pc = SkipLineCountCaptures(code, 0);
        if (pc >= code.Length || code[pc].Kind != Utf8SliceInstructionKind.Anchor ||
            code[pc].A != (int)Utf8SliceAnchorKind.Beginning) return false;
        var beginning = code[pc++];
        pc = SkipLineCountCaptures(code, pc);
        var whitespace = false;
        if (IsLineCountLoop(code, pc))
        {
            var atom = code[pc + 1];
            whitespace = atom.Kind == Utf8SliceInstructionKind.Shorthand && atom.A == (int)Utf8SliceShorthand.Space ||
                atom.Kind == Utf8SliceInstructionKind.Hybrid && atom.B == 0 && atom.D == 0 &&
                atom.C == 1 << (int)Utf8SliceShorthand.Space && ContainsOnlyAsciiWhitespace(program.SetMasks[atom.A]);
            if (!whitespace) return false;
            pc = SkipLineCountCaptures(code, pc + 3);
        }
        var prefixStart = pc;
        var prefixLength = 0;
        while (pc < code.Length)
        {
            var instruction = code[pc];
            if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) { pc++; continue; }
            if (instruction.Kind != Utf8SliceInstructionKind.Literal || instruction.B != 0 || (uint)instruction.A >= 128) break;
            prefixLength++;
            pc++;
        }
        if (prefixLength == 0 || Rune.IsWhiteSpace(new Rune(code[prefixStart].A)) || !IsLineCountLoop(code, pc) ||
            code[pc + 1].Kind != Utf8SliceInstructionKind.Dot || code[pc + 1].A != 0) return false;
        pc = SkipLineCountCaptures(code, pc + 3);
        if (pc >= code.Length || code[pc].Kind != Utf8SliceInstructionKind.Anchor ||
            code[pc].A is not ((int)Utf8SliceAnchorKind.End or (int)Utf8SliceAnchorKind.VeryEnd)) return false;
        var end = code[pc++];
        pc = SkipLineCountCaptures(code, pc);
        if (pc != code.Length - 1 || code[pc].Kind != Utf8SliceInstructionKind.Accept) return false;
        outcome = CountAnchoredLines(program, input, fromByte, prefixStart, prefixLength,
            whitespace, beginning.B != 0, end, ref invocation);
        return true;
    }

    private static int SkipLineCountCaptures(ReadOnlySpan<Utf8SliceInstruction> code, int pc)
    {
        while (pc < code.Length && code[pc].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) pc++;
        return pc;
    }

    private static bool IsLineCountLoop(ReadOnlySpan<Utf8SliceInstruction> code, int pc)
        => code.Length - pc >= 3 && code[pc].Kind == Utf8SliceInstructionKind.Fork &&
            code[pc].B == 0 && code[pc].A == pc + 3 &&
            code[pc + 2].Kind == Utf8SliceInstructionKind.Jump && code[pc + 2].A == pc;

    private static Utf8SliceCountOutcome CountAnchoredLines(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        int fromByte, int prefixStart, int prefixLength, bool whitespace, bool multilineBeginning,
        in Utf8SliceInstruction endAnchor, ref Utf8SliceInvocation invocation)
    {
        var count = 0;
        var breach = invocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
        // No table or local scratch, but an enclosing working loan still applies.
        breach = invocation.TryReserveWorking(0);
        if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
        var position = fromByte;
        var floor = fromByte;
        var first = (byte)program.Instructions[prefixStart].A;
        while (position < input.Length)
        {
            var relative = -1;
            while (position < input.Length)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                var block = input.Slice(position, Math.Min(2048, input.Length - position));
                relative = block.IndexOf(first);
                if (relative >= 0) { position += relative; break; }
                position += block.Length;
            }
            if (relative < 0) break;
            // At most 64 instructions, including transparent capture markers.
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
            if (input.Length - position < prefixLength || !MatchesStraightAsciiPrefix(program, input, position, prefixStart))
            { position++; continue; }
            var start = position;
            var anchored = start == 0 || multilineBeginning && input[start - 1] == (byte)'\n';
            if (whitespace)
            {
                var visited = 0;
                while (!anchored && start > floor)
                {
                    if ((visited++ & 63) == 0)
                    {
                        breach = invocation.TryChargeInputStep();
                        if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                    }
                    _ = Rune.DecodeLastFromUtf8(input[..start], out var scalar, out var width);
                    if (!Rune.IsWhiteSpace(scalar)) break;
                    start -= width;
                    anchored = start == 0 || multilineBeginning && input[start - 1] == (byte)'\n';
                }
            }
            if (!anchored) { position++; continue; }
            // A non-white first literal prevents overlapping whitespace rewinds.
            // Count needs only an admitted start; all such starts have this end.
            var nextCandidate = position + 1;
            position += prefixLength;
            while (position < input.Length)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                var block = input.Slice(position, Math.Min(2048, input.Length - position));
                relative = block.IndexOf((byte)'\n');
                if (relative >= 0) { position += relative; break; }
                position += block.Length;
            }
            if (HoldsNonBacktrackingAnchor(endAnchor, input, position))
            {
                count++;
                floor = position;
            }
            else
            {
                // A newline inside a prefix can hide a later anchored start.
                // On failure retain it; the <=64-instruction prefix bounds
                // how many anchored attempts can share a tail line.
                position = nextCandidate;
            }
        }
        invocation.Budget.ThrowIfCancellationRequested();
        invocation.Budget.CheckDeadline();
        return new(count, Utf8ExecutionLimitKind.None);
    }
}
