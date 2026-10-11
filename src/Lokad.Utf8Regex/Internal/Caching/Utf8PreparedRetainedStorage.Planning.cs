using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Planning;

// Typed collection follows every stored reference-bearing field, including
// inactive portfolio payloads. Inline values are charged by their owner.

internal readonly partial struct Utf8FixedDistanceSet
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        if (Search is { } value2) value2.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8PrefilterPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        PrimarySearcher.CollectRetainedStorage(storage);
        SecondarySearcher.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(WindowPlans, storage);
    }
}

internal sealed partial class Utf8PreparedRegex
{
    internal static ulong RetainedObjectBytes => Utf8ConstructionBudget.ObjectBytes<ConstructionState>();

    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, RetainedObjectBytes)) return;
        _fallbackDirectFamily.CollectRetainedStorage(storage);
        if (_operationMatchCursorPlans is { } value1) value1.CollectRetainedStorage(storage);
        SemanticRegex.CollectRetainedStorage(storage);
        storage.Text(ExecutionPattern);
        if (ExecutionTree is { } value8) Utf8ExecutionRetainedStorage.Collect(value8, null, storage);
        if (ExecutionProgram is { } value9) Utf8ExecutionRetainedStorage.Collect(null, value9, storage);
        if (ScalarProgram is { } value10) Utf8ScalarProgramRetainedStorage.Collect(value10, storage);
        StructuralSearchPlan.CollectRetainedStorage(storage);
        DeterministicAnchor.CollectRetainedStorage(storage);
        DeterministicGuards.CollectRetainedStorage(storage);
        StructuralVerifier.CollectRetainedStorage(storage);
        StructuralIdentifierFamilyPlan.CollectRetainedStorage(storage);
        StructuralTokenWindowPlan.CollectRetainedStorage(storage);
        StructuralQuotedRelationPlan.CollectRetainedStorage(storage);
        OrderedLiteralWindowPlan.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(LiteralUtf8, storage);
        storage.Text(FallbackReason);
    }
}

internal readonly partial struct Utf8PreparedSemanticRegex
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        storage.Text(Pattern);
        storage.Text(ExecutionPattern);
        if (CaptureCatalog is { } value4) value4.CollectStorage(storage);
    }
}

internal readonly partial struct Utf8SearchPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_fallbackCandidatePlans, storage);
        Utf8PreparedRetainedStorage.CollectArray(LiteralUtf8, storage);
        if (LiteralSearch is { } value5) value5.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(AlternateLiteralsUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(AlternateLiteralUtf16Lengths, storage);
        if (AlternateLiteralSearch is { } value8) value8.CollectRetainedStorage(storage);
        if (AlternateIgnoreCaseLiteralSearch is { } value9) value9.CollectRetainedStorage(storage);
        MultiLiteralSearch.CollectRetainedStorage(storage);
        PreparedSearcher.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(RequiredPrefilterLiteralUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(RequiredPrefilterAlternateLiteralsUtf8, storage);
        PrefilterPlan.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(FixedDistanceSets, storage);
        Utf8PreparedRetainedStorage.CollectArray(TrailingLiteralUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(OrderedWindowLeadingLiteralsUtf8, storage);
        Utf8PreparedRetainedStorage.CollectArray(OrderedWindowTrailingLiteralUtf8, storage);
        WindowSearch.CollectRetainedStorage(storage);
        StructuralSearchPlan.CollectRetainedStorage(storage);
    }
}

internal readonly partial struct Utf8StructuralSearchPlan
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Utf8PreparedRetainedStorage.CollectArray(_stages, storage);
    }
}

internal readonly partial struct Utf8StructuralSearchStage
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        storage.Check();
        Searcher.CollectRetainedStorage(storage);
        WindowSearch.CollectRetainedStorage(storage);
        AsciiFindPlan.CollectRetainedStorage(storage);
        Utf8PreparedRetainedStorage.CollectArray(LiteralUtf8, storage);
    }
}
