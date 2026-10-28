using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
    // Capture markers consume no input. A first exact ASCII scalar therefore
    // proves a possible start even when the front-end chose a different search
    // portfolio. Other instructions (including forks/assertions) decline.
    private static int GetLeadingAsciiLiteral(Utf8SliceProgram program)
    {
        var code = program.Instructions.AsSpan();
        var index = 0;
        // This optional per-invocation proof stays bounded even for deeply
        // nested captures; larger prefixes retain the metered VM scan.
        while (index < code.Length && index < 64 && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            index++;
        return index < code.Length && index < 64 && code[index] is { Kind: Utf8SliceInstructionKind.Literal, B: 0 } literal &&
            (uint)literal.A < 128 ? literal.A : -1;
    }

    private static Utf8ExecutionLimitKind TrySeekAsciiLiteral(ReadOnlySpan<byte> input, ref int position,
        byte literal, ref Utf8SliceInvocation invocation)
    {
        if (invocation.IsUnmetered && invocation.CheckpointHook is null)
        {
            var relative = input[position..].IndexOf(literal);
            position = relative < 0 ? -1 : position + relative;
            return Utf8ExecutionLimitKind.None;
        }
        while (position < input.Length)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            var length = Math.Min(2048, input.Length - position);
            var relative = input.Slice(position, length).IndexOf(literal);
            if (relative >= 0) { position += relative; return Utf8ExecutionLimitKind.None; }
            position += length;
        }
        position = -1;
        return Utf8ExecutionLimitKind.None;
    }

    // A leading \s* followed by an exact non-whitespace ASCII scalar can only
    // start in the whitespace run immediately before that scalar, or at it.
    // Verify every start in that window with the unchanged VM: captures,
    // references and assertions may make a later start win after an earlier
    // one fails. No candidate is itself accepted by this necessary condition.
    private static int GetLeadingWhitespaceLiteral(Utf8SliceProgram program)
    {
        var code = program.Instructions.AsSpan();
        var index = 0;
        while (index < code.Length && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            index++;
        if (code.Length - index < 5) return -1;
        var repeat = code[index];
        if (repeat.Kind != Utf8SliceInstructionKind.RepeatGreedy || repeat.RepeatMinimum != 0 ||
            repeat.RepeatMaximum != long.MaxValue || repeat.A != index + 3 ||
            code[index + 2].Kind != Utf8SliceInstructionKind.RepeatEnd || code[index + 2].A != index ||
            code[index + 3].Kind != Utf8SliceInstructionKind.RepeatExit)
            return -1;
        var body = code[index + 1];
        var whitespace = body.Kind == Utf8SliceInstructionKind.Shorthand && body.A == (int)Utf8SliceShorthand.Space ||
            body.Kind == Utf8SliceInstructionKind.Hybrid && body.B == 0 && body.D == 0 &&
            body.C == 1 << (int)Utf8SliceShorthand.Space &&
            ContainsOnlyAsciiWhitespace(program.SetMasks[body.A]);
        if (!whitespace) return -1;
        index += 4;
        while (index < code.Length && code[index].Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            index++;
        if (index == code.Length || code[index].Kind != Utf8SliceInstructionKind.Literal || code[index].B != 0 ||
            (uint)code[index].A >= 128 || Rune.IsWhiteSpace(new Rune(code[index].A)))
            return -1;
        return code[index].A;
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

    private static int SeekLeadingWhitespaceLiteral(ReadOnlySpan<byte> input, int fromByte, byte literal, out int windowEnd)
    {
        var relative = input[fromByte..].IndexOf(literal);
        if (relative < 0) { windowEnd = -1; return -1; }
        windowEnd = fromByte + relative;
        var start = windowEnd;
        while (start > fromByte && TryReadScalar(input, start, true, out var scalar, out var width) && Rune.IsWhiteSpace(scalar))
            start -= width;
        return start;
    }
}
