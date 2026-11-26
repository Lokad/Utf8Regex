using System.Runtime.CompilerServices;
using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    private static Utf8SliceSearchOutcome FindMultilineSeparatedClasses(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, ref Utf8SliceInvocation invocation)
    {
        var outcome = ScanMultilineSeparatedClasses(program, input, position, firstOnly: true, ref invocation, out var range);
        return outcome.Breach == Utf8ExecutionLimitKind.None && range.Success
            ? new(true, range.StartOffsetInBytes, range.EndOffsetInBytes, outcome.Breach)
            : new(false, -1, -1, outcome.Breach);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static Utf8SliceCountOutcome ScanMultilineSeparatedClasses(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, bool firstOnly, ref Utf8SliceInvocation invocation,
        out Utf8CaptureRange range)
    {
        range = default;
        var count = 0;
        var literal = (byte)program.Instructions[9].A;
        var firstDelimiter = position;
        var breach = invocation.Ledger.ReservedBytes != 0 ? invocation.TryReserveWorking(0) : Utf8ExecutionLimitKind.None;
        if (breach == Utf8ExecutionLimitKind.None)
            breach = TrySeekAsciiCandidate(input, ref firstDelimiter, literal, literal, ref invocation);
        while (breach == Utf8ExecutionLimitKind.None && firstDelimiter >= 0 && position >= 0 && position < input.Length)
        {
            breach = TrySeekBeginning(program, input, ref position, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None || position < 0) break;
            breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) break;

            var remaining = BranchedPrefixInstructionLimit - 4;
            var whitespace = MatchLineRepeat(program, input, position, 1, ref remaining, ref invocation);
            breach = whitespace.Breach;
            if (breach != Utf8ExecutionLimitKind.None || whitespace.EndIndex >= input.Length) break;
            var label = MatchLineRepeat(program, input, whitespace.EndIndex, 5, ref remaining, ref invocation);
            breach = label.Breach;
            if (breach != Utf8ExecutionLimitKind.None) break;
            if ((uint)label.EndIndex < (uint)input.Length && input[label.EndIndex] == literal)
            {
                count++;
                if (firstOnly)
                {
                    range = new(position, label.EndIndex + 1);
                    break;
                }
                position = label.EndIndex + 1;
            }
            else
            {
                // Every anchor in the consumed whitespace reaches the same
                // suffix. A shorter whitespace run exposes an excluded scalar.
                position = AdvancePastScalar(input, whitespace.EndIndex, reverse: false);
            }
        }
        if (breach == Utf8ExecutionLimitKind.None) breach = invocation.TryChargeInputStep();
        invocation.Budget.ThrowIfCancellationRequested();
        return new(count, breach);
    }

    private static (int EndIndex, Utf8ExecutionLimitKind Breach) MatchLineRepeat(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, int index, ref int remaining, ref Utf8SliceInvocation invocation)
    {
        var end = remaining == 0 ? BranchedPrefixUndecided :
            TryMatchLongPrefixRepeat(program, input, position, index + 1, in program.Instructions[index], ref remaining);
        return end >= 0 ? (end, Utf8ExecutionLimitKind.None)
            : end == BranchedPrefixRejected ? (-1, Utf8ExecutionLimitKind.None)
            : RunClassRepeat(program, index, input, position, ref invocation);
    }

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
        if (prefixLength == 0 || Rune.IsWhiteSpace(new Rune(code[prefixStart].A)) || code.Length - pc < 3 ||
            code[pc + 1] is not { Kind: Utf8SliceInstructionKind.Dot, A: 0 }) return false;
        var requiresDot = false;
        if (IsLineCountLoop(code, pc)) pc += 3;
        else
        {
            // With LF excluded, only the end of this run can satisfy the end anchor.
            // Greedy and lazy single-atom repeats therefore have the same endpoint.
            var repeat = code[pc];
            if (repeat.Kind is not (Utf8SliceInstructionKind.RepeatGreedy or Utf8SliceInstructionKind.RepeatLazy) ||
                !repeat.HasSingleAtomBody || repeat.RepeatMinimum > 1 || repeat.RepeatMaximum != long.MaxValue)
                return false;
            requiresDot = repeat.RepeatMinimum != 0;
            pc = repeat.A + 1;
        }
        pc = SkipLineCountCaptures(code, pc);
        if (pc >= code.Length || code[pc].Kind != Utf8SliceInstructionKind.Anchor ||
            code[pc].A is not ((int)Utf8SliceAnchorKind.End or (int)Utf8SliceAnchorKind.VeryEnd)) return false;
        var end = code[pc++];
        pc = SkipLineCountCaptures(code, pc);
        if (pc != code.Length - 1 || code[pc].Kind != Utf8SliceInstructionKind.Accept) return false;

        scoped ReadOnlySpan<byte> linePrefix = default;
        if (!program.IsNonBacktracking)
        {
            // Reuse Count's anchored search. Dense occurrences inside a line
            // must not turn into individual candidate checks.
            Span<byte> needle = stackalloc byte[code.Length];
            needle[0] = (byte)'\n';
            var offset = 1;
            for (var index = prefixStart; offset <= prefixLength; index++)
                if (code[index].Kind == Utf8SliceInstructionKind.Literal)
                    needle[offset++] = (byte)code[index].A;
            linePrefix = needle[..offset];
        }
        outcome = CountAnchoredLines(program, input, fromByte, prefixStart, prefixLength,
            whitespace, beginning.B != 0, requiresDot, end, linePrefix, ref invocation);
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
        int fromByte, int prefixStart, int prefixLength, bool whitespace, bool multilineBeginning, bool requiresDot,
        in Utf8SliceInstruction endAnchor, scoped ReadOnlySpan<byte> linePrefix, ref Utf8SliceInvocation invocation)
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
            if (!linePrefix.IsEmpty)
            {
                breach = TrySeekBeginning(program, input, ref position, ref invocation, linePrefix);
                if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                if (position < 0) break;
            }
            else
            {
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
            }
            // A non-white first literal prevents overlapping whitespace rewinds.
            // Count needs only an admitted start; all such starts have this end.
            var nextCandidate = position + 1;
            position += prefixLength;
            var dotStart = position;
            while (position < input.Length)
            {
                breach = invocation.TryChargeInputStep();
                if (breach != Utf8ExecutionLimitKind.None) return new(count, breach);
                var block = input.Slice(position, Math.Min(2048, input.Length - position));
                relative = block.IndexOf((byte)'\n');
                if (relative >= 0) { position += relative; break; }
                position += block.Length;
            }
            if ((!requiresDot || position != dotStart) && HoldsNonBacktrackingAnchor(endAnchor, input, position))
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
