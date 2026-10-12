using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

// PCRE2-INTEGRATION-POINT: common ownership of immutable prepared payloads.
// Storage providers declare their scope explicitly. Complete-payload charges cover
// immutable values. Root/descendant certificates reserve each bounded child's
// enforced maximum before publication, including future lazy growth.
// Program certificates use completed covered construction capacity,
// explicit borrowed/fixed metadata allowance and enforced child maxima; transient
// construction peaks and reflection estimates are not production certificates.
// String/array loans include modeled headers and physical capacity.
internal enum Utf8RetainedStorageScope : byte
{
    Uncertified = 0,
    CompletePayload = 1,
    RootsAndBoundedDescendants = 2,
}

internal interface IUtf8RetainedStorage<TKey, TValue>
{
    bool IsCertified { get; }
    Utf8RetainedStorageScope Scope { get; }
    // Keys have closed, length-based storage. Charge them independently of the
    // value graph; shared text may be counted again without an identity map.
    ulong GetKeyCharge(TKey key);
    bool TryGetValueCharge<TControl>(TValue value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl;
}

internal readonly struct Utf8UncertifiedStorage<TKey, TValue> : IUtf8RetainedStorage<TKey, TValue>
{
    public bool IsCertified => false;
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.Uncertified;
    public ulong GetKeyCharge(TKey key) => 0;
    public bool TryGetValueCharge<TControl>(TValue value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl { charge = 0; return true; }
}
