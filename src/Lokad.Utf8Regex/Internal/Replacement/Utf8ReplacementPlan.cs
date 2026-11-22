using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal sealed class Utf8ReplacementPlan
{
    private readonly Utf8ReplacementInstruction[] _instructions;

    private Utf8ReplacementPlan(Utf8ReplacementInstruction[] instructions)
    {
        _instructions = instructions;
    }

    internal static Utf8ReplacementPlan Create(Utf8ConstructionSession storage, ReadOnlySpan<Utf8ReplacementInstruction> instructions)
    {
        var copied = storage.Allocate<Utf8ReplacementInstruction>(instructions.Length, true);
        storage.Budget.ChargeWork((ulong)instructions.Length);
        instructions.CopyTo(copied);
        return storage.Budget.AllocateObject(copied, static value => new Utf8ReplacementPlan(value));
    }

    internal ReadOnlySpan<Utf8ReplacementInstruction> Instructions => _instructions;
}
