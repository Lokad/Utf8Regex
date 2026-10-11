using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8ReplacementRetainedStorage : IUtf8RetainedStorage<string, Utf8AnalyzedReplacement>
{
    public bool IsCertified => true;
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
    public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);

    public void CollectValue<TControl>(Utf8AnalyzedReplacement value, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Text(value.OriginalText);
        storage.Array(value.LiteralUtf8);
        storage.Object<(Utf8ReplacementToken[], bool, bool, bool, bool)>(value.Pattern);
        storage.Array(value.Pattern.Tokens);
        foreach (var token in value.Pattern.Tokens)
        { storage.Text(token.Literal); storage.Text(token.ReferenceText); }
        value.Plan.CollectStorage(storage);
    }
}
