using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    // Capture markers and mandatory repeat entries precede the first scalar.
    // An unfolded ASCII literal/positive range proves possible starts. Other
    // instructions (including optional repeats/forks/assertions) decline.
    private static int GetLeadingAsciiCandidate(Utf8SliceProgram program, out byte last,
        out int second, out byte secondLast)
    {
        last = 0;
        second = -1;
        secondLast = 0;
        var code = program.Instructions.AsSpan();
        var index = 0;
        // This optional per-invocation proof stays bounded even for deeply
        // nested captures; larger prefixes retain the metered VM scan.
        while (index < code.Length && index < 64)
        {
            var entry = code[index];
            if (entry.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd ||
                entry.Kind == Utf8SliceInstructionKind.RepeatGreedy && entry.RepeatMinimum > 0)
                index++;
            else break;
        }
        if (index >= code.Length || index >= 64) return -1;
        var first = GetUnfoldedAsciiRange(program, code[index], out last);
        if (first < 0) return first;
        // Only straight-line mandatory entries can precede the next scalar.
        // Repeat-end/exit, optional entries and forks stay opaque: the next
        // instruction in storage need not be the next consumed scalar.
        index++;
        while (index < code.Length && index < 64)
        {
            var entry = code[index];
            if (entry.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd ||
                entry.Kind == Utf8SliceInstructionKind.RepeatGreedy && entry.RepeatMinimum > 0)
                index++;
            else break;
        }
        if (index < code.Length && index < 64)
            second = GetUnfoldedAsciiRange(program, code[index], out secondLast);
        return first;
    }

    private static int GetUnfoldedAsciiRange(Utf8SliceProgram program, Utf8SliceInstruction instruction, out byte last)
    {
        last = 0;
        if (instruction is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } literal && (uint)literal.A < 128)
        {
            last = (byte)literal.A;
            return literal.A;
        }
        if (instruction is not { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } set) return -1;
        var mask = program.SetMasks[set.A].AsSpan();
        if (mask.Length != 16) return -1;
        var first = -1;
        var ended = false;
        for (var value = 0; value < 128; value++)
        {
            if ((mask[value >> 3] & (1 << (value & 7))) != 0)
            {
                if (ended) return -1; // A union is not its enclosing range.
                if (first < 0) first = value;
                last = (byte)value;
            }
            else if (first >= 0) ended = true;
        }
        return first;
    }

    private static Utf8ExecutionLimitKind TrySeekAsciiCandidate(ReadOnlySpan<byte> input, ref int position,
        byte first, byte last, ref Utf8SliceInvocation invocation)
    {
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            var relative = first == last ? input[position..].IndexOf(first) : input[position..].IndexOfAnyInRange(first, last);
            position = relative < 0 ? -1 : position + relative;
            return Utf8ExecutionLimitKind.None;
        }
        while (position < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var length = Math.Min(2048, input.Length - position);
            var block = input.Slice(position, length);
            var relative = first == last ? block.IndexOf(first) : block.IndexOfAnyInRange(first, last);
            if (relative >= 0) { position += relative; return Utf8ExecutionLimitKind.None; }
            position += length;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    // A whitespace repeat followed by an exact non-whitespace ASCII scalar
    // admits a window before that scalar; one scalar-class run can precede it,
    // with a finite scalar gap between whitespace and the literal.
    // Verify every start in that window with the unchanged VM: captures,
    // references and assertions may make a later start win after an earlier
    // one fails. No candidate is itself accepted by this necessary condition.
    private static int GetLeadingWhitespaceLiteral(Utf8SliceProgram program,
        out int leadingClass, out int literalIndex, out long gapMaximum)
    {
        leadingClass = -1;
        literalIndex = -1;
        gapMaximum = 0;
        var code = program.Instructions.AsSpan();
        var index = 0;
        while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            index++;
        // One leading scalar-class run may precede the whitespace run. Reuse
        // the actual VM predicate when rewinding: ASCII intersections are not
        // a proof of Unicode word/category membership.
        if (IsSingleClassRepeat(code, index) && code[index].RepeatMinimum > 0)
        {
            leadingClass = index + 1;
            index += 4;
            while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                index++;
        }
        if (index >= 64 || !IsSingleClassRepeat(code, index)) return -1;
        var repeat = code[index];
        if (repeat.RepeatMaximum != long.MaxValue ||
            repeat.RepeatMinimum > (leadingClass >= 0 ? 1 : 0)) return -1;
        var body = code[index + 1];
        var whitespace = body.Kind == Utf8SliceInstructionKind.Shorthand && body.A == (int)Utf8SliceShorthand.Space ||
            body.Kind == Utf8SliceInstructionKind.Hybrid && body.B == 0 && body.D == 0 &&
            body.C == 1 << (int)Utf8SliceShorthand.Space &&
            ContainsOnlyAsciiWhitespace(program.SetMasks[body.A]);
        if (!whitespace) return -1;
        index += 4;
        while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            index++;
        if (leadingClass >= 0 && index < 64 && IsSingleClassRepeat(code, index) &&
            code[index].RepeatMaximum != long.MaxValue &&
            code[index + 1] is { Kind: Utf8SliceInstructionKind.Dot, A: 1 })
        {
            gapMaximum = code[index].RepeatMaximum;
            index += 4;
            while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                index++;
        }
        if (index >= code.Length || index >= 64 || code[index].Kind != Utf8SliceInstructionKind.Literal || code[index].B != 0 ||
            (uint)code[index].A >= 128 || Rune.IsWhiteSpace(new Rune(code[index].A)))
            return -1;
        literalIndex = index;
        return code[index].A;
    }

    private static bool IsSingleClassRepeat(ReadOnlySpan<Utf8SliceInstruction> code, int index)
        => code.Length - index >= 4 && code[index].Kind == Utf8SliceInstructionKind.RepeatGreedy &&
            code[index].A == index + 3 &&
            code[index + 1].Kind is (Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.Set or
                Utf8SliceInstructionKind.Shorthand or Utf8SliceInstructionKind.Hybrid or Utf8SliceInstructionKind.CategorySet) &&
            code[index + 2].Kind == Utf8SliceInstructionKind.RepeatEnd && code[index + 2].A == index &&
            code[index + 3].Kind == Utf8SliceInstructionKind.RepeatExit && code[index + 3].A == code[index].B;

    private static bool LiteralStartsAtInstruction(Utf8SliceProgram program, int index, ReadOnlySpan<byte> needle)
    {
        // A required fragment elsewhere in the tail is not the literal at the
        // end of these runs. Check alignment before borrowing the prepared search.
        if (needle.IsEmpty || needle.Length > 64 || program.Instructions.Length - index < needle.Length) return false;
        for (var i = 0; i < needle.Length; i++)
        {
            var instruction = program.Instructions[index + i];
            if (needle[i] >= 128 || instruction.Kind != Utf8SliceInstructionKind.Literal ||
                instruction.B != 0 || instruction.A != needle[i]) return false;
        }
        return true;
    }

    private static Utf8ExecutionLimitKind TryRewindClassWhitespace(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int floor, int leadingClass, long gapMaximum, ref int position, ref Utf8SliceInvocation invocation)
    {
        // Move by scalar count, not bytes. If this lands inside a valid prefix,
        // it lies in its class/whitespace runs; their conservative rewind then
        // covers the earliest start. Landing before the prefix only adds starts.
        for (long remaining = gapMaximum; remaining > 0 && position > floor; remaining--)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!TryReadScalar(input, position, true, out _, out var width)) break;
            position -= width;
        }
        // The classes can overlap. Cover the complete whitespace/class window,
        // then let the VM test each start, rather than choosing a split here.
        while (position > floor)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!TryReadScalar(input, position, true, out var scalar, out var width) || !Rune.IsWhiteSpace(scalar)) break;
            position -= width;
        }
        var classMaximum = leadingClass >= 0 ? program.Instructions[leadingClass - 1].RepeatMaximum : 0;
        for (long remaining = classMaximum; remaining > 0 && position > floor; remaining--)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!TryReadScalar(input, position, true, out var scalar, out var width)) break;
            breach = TryMatchScalarClass(program, program.Instructions[leadingClass], scalar, ref invocation, out var hit);
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            if (!hit) break;
            position -= width;
        }
        return Utf8ExecutionLimitKind.None;
    }

    private static bool ContainsOnlyAsciiWhitespace(ReadOnlySpan<byte> mask)
    {
        for (var index = 0; index < mask.Length; index++)
        {
            // ASCII whitespace is 09..0D and 20. A subset is sufficient:
            // rewinding a wider whitespace run merely adds VM candidates.
            var allowed = index == 1 ? 0x3E : index == 4 ? 1 : 0;
            if ((mask[index] & ~allowed) != 0) return false;
        }
        return true;
    }

    private static Utf8ExecutionLimitKind TrySeekLeadingWhitespaceLiteral(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, ref int position, int literalIndex, ref Utf8SliceInvocation invocation, out int windowEnd)
    {
        var floor = position;
        var literal = (byte)program.Instructions[literalIndex].A;
        windowEnd = -1;
        while (position < input.Length)
        {
            var breach = TrySeekAsciiCandidate(input, ref position, literal, literal, ref invocation);
            if (breach != Utf8ExecutionLimitKind.None || position < 0) return breach;
            if (MatchesStraightAsciiPrefix(program, input, position, literalIndex))
            {
                windowEnd = position;
                return TryRewindClassWhitespace(program, input, floor, -1, 0, ref position, ref invocation);
            }
            // The admitted first byte is ASCII, so the next byte is a scalar boundary.
            position++;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    private static bool MatchesStraightAsciiPrefix(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int position, int instructionIndex)
    {
        // Reject only a proven mandatory straight-line prefix. This bounded
        // filter accepts no match; branches, wider classes and effects stop it.
        var code = program.Instructions.AsSpan();
        var end = Math.Min(code.Length, instructionIndex + 64);
        for (; instructionIndex < end; instructionIndex++)
        {
            var entry = code[instructionIndex];
            if (entry.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) continue;
            if (entry is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } && (uint)entry.A < 128)
            {
                if (position >= input.Length || input[position++] != entry.A) return false;
            }
            else if (entry is { Kind: Utf8SliceInstructionKind.Set, B: 0, C: 0 } && program.SetMasks[entry.A].Length == 16)
            {
                if (position >= input.Length || input[position] >= 128) return false;
                var value = input[position++];
                if ((program.SetMasks[entry.A][value >> 3] & (1 << (value & 7))) == 0) return false;
            }
            else break;
        }
        return true;
    }
}
