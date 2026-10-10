using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal sealed class Utf8ReplacementPlan
{
    private readonly int[] _referencedCaptureGroups;

    private Utf8ReplacementPlan(Utf8ReplacementInstruction[] instructions, int[] referencedGroups)
    {
        Instructions = instructions;
        _referencedCaptureGroups = referencedGroups;
    }

    internal static Utf8ReplacementPlan Create(Utf8ConstructionSession storage, ReadOnlySpan<Utf8ReplacementInstruction> instructions)
    {
        var copied = storage.Allocate<Utf8ReplacementInstruction>(instructions.Length, true);
        storage.Budget.ChargeWork((ulong)instructions.Length);
        instructions.CopyTo(copied);
        var referenced = storage.Buffer<int>();
        using var seen = Utf8ConstructionMap<int, byte>.Create(storage.Budget, EqualityComparer<int>.Default);
        foreach (var instruction in instructions)
        {
            storage.Budget.ChargeWork(1);
            if (instruction.Kind == Utf8ReplacementInstructionKind.Group && instruction.GroupNumber > 0 &&
                !seen.ContainsKey(instruction.GroupNumber))
            {
                seen.Add(instruction.GroupNumber, 0);
                referenced.Add(instruction.GroupNumber);
            }
        }
        var groups = storage.Copy(referenced, true);
        return storage.Budget.AllocateObject((copied, groups), static state => new Utf8ReplacementPlan(state.copied, state.groups));
    }

    public IReadOnlyList<Utf8ReplacementInstruction> Instructions { get; }
    internal ReadOnlySpan<int> ReferencedCaptureGroups => _referencedCaptureGroups;
}
