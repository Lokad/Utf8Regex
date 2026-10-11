using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;

namespace Lokad.Utf8Regex.Internal.Caching;

// Closed prepared graph. Existing scalar/execution/capture collectors own
// those subgraphs. Compiled runtimes and facade helpers have separate roots.
internal static class Utf8PreparedRetainedStorage
{
    internal static void Collect<TControl>(Utf8PreparedRegex prepared, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl => prepared.CollectRetainedStorage(storage);

    internal static void CollectArray<T, TControl>(T[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl => storage.Array(values);

    internal static void CollectArray<T, TControl>(T[][]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<T[]>(values.Length))) return;
        foreach (var value in values) { storage.Check(); storage.Array(value); }
    }

    internal static void CollectArray<TControl>(AsciiFixedLiteralCheck[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<AsciiFixedLiteralCheck>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(AsciiSimplePatternAnchoredValidatorSegment[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<AsciiSimplePatternAnchoredValidatorSegment>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(AsciiStructuralLinearVerifierInstruction[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<AsciiStructuralLinearVerifierInstruction>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(AsciiStructuralSuffixPart[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<AsciiStructuralSuffixPart>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(AsciiStructuralVerifierStep[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<AsciiStructuralVerifierStep>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(Utf8ByteSafeLinearVerifierStep[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<Utf8ByteSafeLinearVerifierStep>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(Utf8FixedDistanceSet[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<Utf8FixedDistanceSet>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(Utf8StructuralSearchPlan[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchPlan>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(Utf8StructuralSearchStage[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<Utf8StructuralSearchStage>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(AsciiExactLiteralBucket[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<AsciiExactLiteralBucket>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(AsciiIgnoreCaseLiteralBucket[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<AsciiIgnoreCaseLiteralBucket>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(PreparedMultiLiteralTrieNode[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<PreparedMultiLiteralTrieNode>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }

    internal static void CollectArray<TControl>(PreparedSearcher[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<PreparedSearcher>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan())
        {
            storage.Check();
            value.CollectRetainedStorage(storage);
        }
    }
}
