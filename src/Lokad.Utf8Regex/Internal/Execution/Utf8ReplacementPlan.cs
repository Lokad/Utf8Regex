namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8ReplacementPlan
{
    public Utf8ReplacementPlan(IReadOnlyList<Utf8ReplacementInstruction> instructions)
    {
        Instructions = instructions;
    }

    public IReadOnlyList<Utf8ReplacementInstruction> Instructions { get; }
}
