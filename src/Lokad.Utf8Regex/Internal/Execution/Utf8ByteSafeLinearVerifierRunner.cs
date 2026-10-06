namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ByteSafeLinearVerifierRunner
{
    public static bool TryMatchPrefix(
        ReadOnlySpan<byte> input,
        Utf8ExecutionProgram? program,
        int startIndex,
        Utf8CaptureSlots? captures,
        Utf8ExecutionDeadline budget,
        out int matchedLength)
    {
        matchedLength = 0;
        if (program is null || program.Instructions.Count == 0 || (uint)startIndex > (uint)input.Length)
        {
            captures?.Clear();
            budget.Step();
            return false;
        }
        if (!Utf8ExecutionInterpreter.TryMatchProgramAt(input, program, 0, startIndex, captures, budget, out var endIndex))
            return false;
        matchedLength = endIndex - startIndex;
        return true;
    }
}
