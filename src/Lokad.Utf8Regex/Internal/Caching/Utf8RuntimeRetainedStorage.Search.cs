using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Search;

// Typed owned state; runtime method/resolver/JIT descendants of delegates
// remain outside this declared retained subgraph. Inline metadata belongs to its owner.

internal readonly partial struct PreparedSmallAsciiLiteralFamilySearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        _searchData.CollectRetainedStorage(storage);
        _anchorSearch.CollectRetainedStorage(storage);
        CollectArray(_filters, storage);
        _tripleDispatch.CollectRetainedStorage(storage);
        _pairDispatch.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedSmallAsciiLiteralFamilySearch
{
private readonly partial record struct Filter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Values, storage);
    }
}
}

internal readonly partial struct PreparedSmallAsciiLiteralFamilySearch
{
private readonly partial record struct PairDispatch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        CollectArray(Entries, storage);
    }
}
}

internal readonly partial struct PreparedSmallAsciiLiteralFamilySearch
{
private readonly partial record struct PairDispatchEntry
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Literal, storage);
    }
}
}

internal readonly partial struct PreparedSmallAsciiLiteralFamilySearch
{
private readonly partial record struct TripleDispatch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Literal0, storage);
        Utf8PreparedRetainedStorage.CollectArray(Literal1, storage);
        Utf8PreparedRetainedStorage.CollectArray(Literal2, storage);
    }
}
}

internal readonly partial struct PreparedSmallAsciiLiteralFamilySearch
{
    private static void CollectArray<TControl>(Filter[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<Filter>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan()) value.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedSmallAsciiLiteralFamilySearch
{
    private static void CollectArray<TControl>(PairDispatchEntry[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<PairDispatchEntry>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan()) value.CollectRetainedStorage(storage);
    }
}
