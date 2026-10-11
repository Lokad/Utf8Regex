using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Search;

// Typed collection follows every stored reference-bearing field, including
// inactive portfolio payloads. Inline values are charged by their owner.

internal readonly partial struct AsciiExactLiteralBucket
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Literals, storage);
        Utf8PreparedRetainedStorage.CollectArray(CommonPrefix, storage);
        Utf8PreparedRetainedStorage.CollectArray(SecondByteCandidates, storage);
        PrefixDiscriminator.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct AsciiExactLiteralPrefixDiscriminator
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_literalsByByte, storage);
    }
}

internal readonly partial struct AsciiExactLiteralSearchData
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        FirstByteSearch.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(Buckets, storage);
        Utf8PreparedRetainedStorage.CollectArray(BucketIndexMap, storage);
    }
}

internal readonly partial struct AsciiIgnoreCaseLiteralBucket
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Literals, storage);
    }
}

internal readonly partial struct PreparedAsciiIgnoreCaseLiteralSetSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        if (_data is { } value0) value0.CollectRetainedStorage(storage);
    }
}

internal sealed partial class PreparedAsciiIgnoreCaseLiteralSetSearchData
{
    internal static ulong RetainedObjectBytes => Utf8ConstructionBudget.ObjectBytes<PreparedAsciiIgnoreCaseLiteralSetSearchDataState>();

    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, RetainedObjectBytes)) return;
        FirstByteSearch.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(Buckets, storage);
        Utf8PreparedRetainedStorage.CollectArray(BucketIndexMap, storage);
        CorrelatedPrefilter.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedByteSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_values, storage);
        if (ByteSet is { } value1) value1.CollectRetainedStorage(storage);
    }
}

internal sealed partial class PreparedByteSet
{
    internal static ulong RetainedObjectBytes => Utf8ConstructionBudget.ObjectBytes<PreparedByteSetState>();

    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, RetainedObjectBytes)) return;
    }
}

internal readonly partial struct PreparedLiteralSetSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        SearchData.CollectRetainedStorage(storage);
        if (UniqueAnchor is { } value1) value1.CollectRetainedStorage(storage);
        MultiLiteral.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedLiteralSetUniqueAnchor
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        AnchorSearch.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(LiteralByAnchorByte, storage);
    }
}

internal readonly partial struct PreparedMultiLiteralAutomatonSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Transitions, storage);
        Utf8PreparedRetainedStorage.CollectArray(BestOutputOrders, storage);
        Utf8PreparedRetainedStorage.CollectArray(BestOutputLengths, storage);
        Utf8PreparedRetainedStorage.CollectArray(ShortestOutputLengths, storage);
    }
}

internal readonly partial struct PreparedMultiLiteralEarliestExactSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Literals, storage);
    }
}

internal readonly partial struct PreparedMultiLiteralPackedNibbleSimdPrefilter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(MaskOffsets, storage);
        Utf8PreparedRetainedStorage.CollectArray(LowMaskVectors, storage);
        Utf8PreparedRetainedStorage.CollectArray(HighMaskVectors, storage);
        Search.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedMultiLiteralPackedSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        DiscriminatorSearch.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(Literals, storage);
        Utf8PreparedRetainedStorage.CollectArray(LiteralByDiscriminator, storage);
        Utf8PreparedRetainedStorage.CollectArray(LiteralIdByDiscriminator, storage);
        Utf8PreparedRetainedStorage.CollectArray(SecondByteByDiscriminator, storage);
        Utf8PreparedRetainedStorage.CollectArray(ThirdByteByDiscriminator, storage);
    }
}

internal readonly partial struct PreparedMultiLiteralSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Literals, storage);
        ExactSearch.CollectRetainedStorage(storage);
        TrieSearch.CollectRetainedStorage(storage);
        AutomatonSearch.CollectRetainedStorage(storage);
        EarliestSearch.CollectRetainedStorage(storage);
        PackedSearch.CollectRetainedStorage(storage);
        IgnoreCaseSearch.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedMultiLiteralTrieNode
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Keys, storage);
        Utf8PreparedRetainedStorage.CollectArray(Children, storage);
    }
}

internal readonly partial struct PreparedMultiLiteralTrieSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_nodes, storage);
        RootSearch.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(RootMap, storage);
    }
}

internal readonly partial struct PreparedSearcher
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        LiteralSearch.CollectRetainedStorage(storage);
        ByteSearch.CollectRetainedStorage(storage);
        MultiLiteralSearch.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedSubstringSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_foldedNeedle, storage);
        Utf8PreparedRetainedStorage.CollectArray(_ignoreCaseShiftTable, storage);
        Utf8PreparedRetainedStorage.CollectArray(Needle, storage);
    }
}

internal readonly partial struct PreparedWindowSearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        LeadingSearcher.CollectRetainedStorage(storage);
        TrailingSearcher.CollectRetainedStorage(storage);
    }
}
