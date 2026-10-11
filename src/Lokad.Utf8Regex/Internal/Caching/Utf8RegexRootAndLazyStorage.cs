using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

// This collector certifies its declared root/descendant subgraph, including
// the complete owned scalar program graph. Other immutable
// prepared/runtime payload and opaque CLR storage remain outside the scope.
// Reserve bounded replacement descendants before parent publication;
// their already-enforced maxima cover lazy growth without mutable manifests.
internal readonly struct Utf8RegexRootAndLazyStorage : IUtf8RetainedStorage<Utf8RegexCacheKey, Utf8Regex>
{
    public bool IsCertified => true;
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.RootsAndBoundedDescendants;
    public void CollectKey<TControl>(Utf8RegexCacheKey key, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Text(key.Pattern);
        storage.Text(key.Culture);
    }
    public void CollectValue<TControl>(Utf8Regex value, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl => value.CollectCacheRoots(storage);
}
