using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8ReplacementRetainedStorage : IUtf8RetainedStorage<object, Utf8AnalyzedReplacement>
{
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
    public ulong GetKeyCharge(object key) => key is byte[] bytes
        ? Utf8ConstructionBudget.ArrayBytes<byte>(bytes.Length) : Utf8ConstructionBudget.StringBytes(((string)key).Length);

    public bool TryGetValueCharge<TControl>(Utf8AnalyzedReplacement value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        ulong total = 0;
        charge = 0;
        // This closed program has no recursive storage. Each slot contributes
        // independently; shared literals intentionally receive no discount.
        if (!Add(Utf8ConstructionBudget.ArrayBytes<byte>(value.LiteralUtf8.Length)
            + Utf8ConstructionBudget.ObjectBytes<IReadOnlyList<Utf8ReplacementInstruction>>()
            + Utf8ConstructionBudget.ArrayBytes<Utf8ReplacementInstruction>(value.Plan.Instructions.Count))) return false;
        for (var index = 0; index < value.Plan.Instructions.Count; index++)
        {
            var instruction = value.Plan.Instructions[index];
            if (!Add(Utf8ConstructionBudget.ArrayBytes<byte>(instruction.LiteralUtf8?.Length ?? 0))) return false;
        }
        charge = total;
        return true;

        bool Add(ulong bytes)
        {
            control.Check(); budget.ChargeWork(1);
            if (bytes > maximum - total) return false;
            total += bytes; return true;
        }
    }
}
