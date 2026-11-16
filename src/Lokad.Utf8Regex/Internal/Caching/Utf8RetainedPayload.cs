using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

// PCRE2-INTEGRATION-POINT: common ownership of immutable prepared payloads.
// Providers conservatively charge complete immutable key/value storage before
// publication. Shared storage can be charged more than once. String/array loans
// include modeled headers and physical capacity; they are not heap/RSS bounds.

// Cache probes may throw for interruption/admission, but cannot mutate the
// owner cache. They are closed library implementations, not host callbacks.
internal interface IUtf8RetainedStorage<TKey, TValue>
{
    // Keys have closed, length-based storage. Charge them independently of the
    // value graph; shared text may be counted again without an identity map.
    ulong GetKeyCharge(TKey key);
    bool TryGetValueCharge<TControl>(TValue value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl;
}
