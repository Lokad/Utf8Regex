namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8ExecutionProgram
{
    public Utf8ExecutionProgram(IReadOnlyList<Utf8ExecutionInstruction> instructions)
    {
        Instructions = instructions;
        DeterministicCaptureLayout = Utf8ExecutionInterpreter.CreateDeterministicCaptureLayout(this);
    }

    public IReadOnlyList<Utf8ExecutionInstruction> Instructions { get; }

    internal Utf8DeterministicCaptureLayout? DeterministicCaptureLayout { get; }
}
