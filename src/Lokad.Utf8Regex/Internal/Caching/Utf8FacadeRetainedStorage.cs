using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Execution;

// Complete immutable facade helpers. Compiled matchers/delegates have separate
// roots and are not certified by these closed graphs. Models match the full
// field states admitted by each factory; inline data belongs to its container.
internal sealed partial class Utf8LiteralFamilyCaptureMap
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<(int[], int[][])>())) return;
        storage.Array(WholeMatchGroups);
        Utf8PreparedRetainedStorage.CollectArray(BranchGroups, storage);
    }
}

internal sealed partial class Utf8GroupedLiteralSpanTable
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<(int, int[], int[], int[], bool, int, int[], int[])>())) return;
        storage.Array(Groups); storage.Array(StartsInLiteral); storage.Array(LengthsInLiteral);
        storage.Array(AbsentStartsInLiteral); storage.Array(AbsentLengthsInLiteral);
    }
}

internal sealed partial class Utf8AlternationBranchSpanMap
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<(int[], string[], int[][], int[][], int[][])>())) return;
        storage.Array(WholeMatchGroups);
        if (storage.TryAdd(BranchExpansions, Utf8ConstructionBudget.ArrayBytes<string>(BranchExpansions.Length)))
            foreach (var text in BranchExpansions) storage.Text(text);
        Utf8PreparedRetainedStorage.CollectArray(BranchGroups, storage);
        Utf8PreparedRetainedStorage.CollectArray(BranchStarts, storage);
        Utf8PreparedRetainedStorage.CollectArray(BranchLengths, storage);
    }
}

internal sealed partial class Utf8PackedNibbleLiteralFamilyCountStrategy
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<PreparedMultiLiteralCandidatePrefilter>())) return;
        _prefilter.CollectRetainedStorage(storage);
    }
}

internal sealed partial class Utf8InvariantCyrillicLiteralCountStrategy
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<ConstructionState>())) return;
        if (storage.TryAdd(_literals, Utf8ConstructionBudget.ArrayBytes<Literal>(_literals.Length)))
            foreach (ref readonly var literal in _literals.AsSpan())
            { storage.Array(literal.Units); storage.Array(literal.Bytes); }
        _sharedAnchorSearch?.CollectRetainedStorage(storage);
        storage.Array(_sharedAnchorLiteralMasks);
        _correlatedPrefilter.CollectRetainedStorage(storage);
    }
}
