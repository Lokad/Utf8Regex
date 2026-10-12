using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

// The completed immutable program charge covers facade/scalar/prepared/runtime
// and helper storage, with one coarse allowance for fixed/borrowed metadata.
// Delegate managed copies are covered; opaque CLR
// method/resolver/JIT descendants remain outside this declared cache scope.
// Reserve bounded replacement descendants before parent publication;
// their already-enforced maxima cover lazy growth without mutable manifests.
internal readonly struct Utf8RegexRootAndLazyStorage : IUtf8RetainedStorage<Utf8RegexCacheKey, Utf8Regex>
{
    public bool IsCertified => true;
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.RootsAndBoundedDescendants;
    public ulong GetKeyCharge(Utf8RegexCacheKey key)
        => checked(Utf8ConstructionBudget.StringBytes(key.Pattern.Length) + Utf8ConstructionBudget.StringBytes(key.Culture.Length));
    public bool TryGetValueCharge<TControl>(Utf8Regex value, ulong maximum, TControl control, out ulong charge)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        control.Check();
        Utf8ConstructionScope.CurrentBudget.ChargeWork(1);
        charge = value.CacheProgramBytes;
        if (charge == 0) throw new InvalidOperationException("A cache program requires a completed construction charge.");
        return charge <= maximum;
    }
}
