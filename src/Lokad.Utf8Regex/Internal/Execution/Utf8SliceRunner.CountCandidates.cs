using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8SliceRunner
{
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
