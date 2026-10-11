using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8ReplacementRetainedStorage : IUtf8RetainedStorage<string, Utf8AnalyzedReplacement>
{
    public bool IsCertified => true;
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
    public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);

    public bool TryGetValueCharge<TControl>(Utf8AnalyzedReplacement value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        ulong total = 0;
        charge = 0;
        // This closed program has no recursive storage. Each slot contributes
        // independently; shared literals intentionally receive no discount.
        if (!Add(Utf8ConstructionBudget.StringBytes(value.OriginalText.Length)
            + Utf8ConstructionBudget.ArrayBytes<byte>(value.LiteralUtf8.Length)
            + Utf8ConstructionBudget.ObjectBytes<(Utf8ReplacementToken[], bool, bool, bool, bool)>()
            + Utf8ConstructionBudget.ArrayBytes<Utf8ReplacementToken>(value.Pattern.Tokens.Length)
            + Utf8ConstructionBudget.ObjectBytes<(IReadOnlyList<Utf8ReplacementInstruction>, int[])>()
            + Utf8ConstructionBudget.ArrayBytes<Utf8ReplacementInstruction>(value.Plan.Instructions.Count)
            + Utf8ConstructionBudget.ArrayBytes<int>(value.Plan.ReferencedCaptureGroups.Length))) return false;
        foreach (var token in value.Pattern.Tokens)
            if (!Add(Utf8ConstructionBudget.StringBytes(token.Literal?.Length ?? 0)
                + Utf8ConstructionBudget.StringBytes(token.ReferenceText?.Length ?? 0))) return false;
        for (var index = 0; index < value.Plan.Instructions.Count; index++)
        {
            var instruction = value.Plan.Instructions[index];
            if (!Add(Utf8ConstructionBudget.ArrayBytes<byte>(instruction.LiteralUtf8?.Length ?? 0)
                + Utf8ConstructionBudget.StringBytes(instruction.LiteralText?.Length ?? 0))) return false;
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
