using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8ReplacementRetainedStorage : IUtf8RetainedStorage<byte[], Utf8AnalyzedReplacement>
{
    public ulong GetKeyCharge(byte[] key) => Utf8ConstructionBudget.ArrayBytes<byte>(key.Length);

    public bool TryGetValueCharge<TControl>(Utf8AnalyzedReplacement value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        var instructions = value.Plan.Instructions;
        ulong total = 0;
        charge = 0;
        // This closed program has no recursive storage. Each slot contributes
        // independently; shared literals intentionally receive no discount.
        if (!Add(Utf8ConstructionBudget.ArrayBytes<byte>(value.LiteralUtf8.Length)
            + Utf8ConstructionBudget.ObjectBytes<Utf8ReplacementInstruction[]>()
            + Utf8ConstructionBudget.ArrayBytes<Utf8ReplacementInstruction>(instructions.Length))) return false;
        foreach (var instruction in instructions)
        {
            if (!Add(Utf8ConstructionBudget.ArrayBytes<byte>(instruction.LiteralUtf8?.Length ?? 0))) return false;
        }
        charge = total;
        return true;

        bool Add(ulong bytes)
        {
            control.Check(); budget.Checkpoint();
            if (bytes > maximum - total) return false;
            total += bytes; return true;
        }
    }
}
