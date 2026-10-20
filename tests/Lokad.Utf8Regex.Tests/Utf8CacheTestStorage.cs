using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Coordination fixtures use strings or empty marker objects; exercise the same
// bounded cache implementation as real producers with an explicit numeric policy.
internal readonly struct Utf8CacheTestStorage<TValue> : IUtf8RetainedStorage<string, TValue>
{
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.CompletePayload;
    public ulong GetKeyCharge(string key) => Utf8ConstructionBudget.StringBytes(key.Length);
    public bool TryGetValueCharge<TControl>(TValue value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        control.Check();
        charge = value is string text ? Utf8ConstructionBudget.StringBytes(text.Length) : value is null ? 0UL : 128UL;
        return charge <= maximum;
    }
}
