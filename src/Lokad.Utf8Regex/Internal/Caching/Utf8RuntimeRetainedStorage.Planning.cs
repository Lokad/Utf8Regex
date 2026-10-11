using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Planning;

// Typed owned state; runtime method/resolver/JIT descendants of delegates
// remain outside this declared retained subgraph. Inline metadata belongs to its owner.

internal readonly partial struct Utf8CandidateSearchPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        PreparedSearcher.CollectRetainedStorage(storage);
        StructuralSearchPlan.CollectRetainedStorage(storage);
        Utf8RuntimeRetainedStorage.CollectArray(StructuralSearchPlans, storage);
    }
}

internal readonly partial struct Utf8SearchOperationPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        CandidateSource.CollectRetainedStorage(storage);
        PrefilterSource.CollectRetainedStorage(storage);
    }
}
