using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Search;

// The facade's candidate portfolio stores these inline payloads even when Kind
// selects another lane. Expand the full state; metadata belongs to its owner.
internal readonly partial struct PreparedMultiLiteralCandidatePrefilter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(Literals, storage);
        RootByte.CollectRetainedStorage(storage); Earliest.CollectRetainedStorage(storage);
        RareByte.CollectRetainedStorage(storage); OffsetMask.CollectRetainedStorage(storage);
        NibbleMask.CollectRetainedStorage(storage); PackedNibbleSimd.CollectRetainedStorage(storage);
        LeadingUtf8Segment.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedMultiLiteralRootByteFamilySearch
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    { storage.Check(); ExactSearch.CollectRetainedStorage(storage); }
}

internal readonly partial struct PreparedMultiLiteralRareBytePrefilter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    { storage.Check(); Search.CollectRetainedStorage(storage); storage.Array(OffsetByValue); }
}

internal readonly partial struct PreparedMultiLiteralOffsetMaskPrefilter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check(); storage.Array(Offsets);
        Utf8PreparedRetainedStorage.CollectArray(Masks, storage); Search.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedMultiLiteralNibbleMaskPrefilter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(LowMasks, storage);
        Utf8PreparedRetainedStorage.CollectArray(HighMasks, storage); Search.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct PreparedMultiLiteralLeadingUtf8SegmentPrefilter
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    { storage.Check(); Utf8PreparedRetainedStorage.CollectArray(Segments, storage); }
}
