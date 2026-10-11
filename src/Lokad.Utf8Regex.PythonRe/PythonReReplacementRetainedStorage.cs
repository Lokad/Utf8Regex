using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.PythonRe;

internal readonly struct PythonReReplacementRetainedStorage : IUtf8RetainedStorage<string, PythonReReplacementPlan>
{
    public bool IsCertified => true;
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
    public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);
    public void CollectValue<TControl>(PythonReReplacementPlan value, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Array(value.Tokens); storage.Array(value.LiteralUtf8);
        foreach (var token in value.Tokens) storage.Array(token.Utf8);
    }
}
