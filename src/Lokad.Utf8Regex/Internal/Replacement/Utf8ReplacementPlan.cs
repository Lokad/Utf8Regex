namespace Lokad.Utf8Regex.Internal.Replacement;

internal sealed class Utf8ReplacementPlan
{
    private readonly int[] _referencedCaptureGroups;

    public Utf8ReplacementPlan(IReadOnlyList<Utf8ReplacementInstruction> instructions)
    {
        ArgumentNullException.ThrowIfNull(instructions);
        var copied = new Utf8ReplacementInstruction[instructions.Count];
        var referencedGroups = new List<int>();
        for (var i = 0; i < copied.Length; i++)
        {
            var instruction = instructions[i];
            copied[i] = instruction;
            if (instruction.Kind == Utf8ReplacementInstructionKind.Group &&
                instruction.GroupNumber > 0 &&
                !referencedGroups.Contains(instruction.GroupNumber))
            {
                referencedGroups.Add(instruction.GroupNumber);
            }
        }

        Instructions = copied;
        _referencedCaptureGroups = [.. referencedGroups];
    }

    public IReadOnlyList<Utf8ReplacementInstruction> Instructions { get; }

    internal ReadOnlySpan<int> ReferencedCaptureGroups => _referencedCaptureGroups;
}
