using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.PythonRe;

internal readonly struct PythonReReplacementRetainedStorage : IUtf8RetainedStorage<string, PythonReReplacementPlan>
{
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
    public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);
    public bool TryGetValueCharge<TControl>(PythonReReplacementPlan value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        ulong total = 0;
        charge = 0;
        if (!Add(Utf8ConstructionBudget.ArrayBytes<PythonReReplacementToken>(value.Tokens.Length)
            + Utf8ConstructionBudget.ArrayBytes<byte>(value.LiteralUtf8?.Length ?? 0))) return false;
        foreach (var token in value.Tokens)
            if (!Add(Utf8ConstructionBudget.ArrayBytes<byte>(token.Utf8?.Length ?? 0))) return false;
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
