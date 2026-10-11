using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

internal static class Utf8RuntimeRetainedStorage
{
    // Same complete six-field state admitted by Utf8DynamicIlBuilder. The CLR
    // method base, resolver, token/signature/code storage and native JIT payload
    // are explicitly unqualified descendants, not zero-sized certified objects.
    internal static void CollectDelegate<TControl>(Delegate? value, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (value is null) return;
        storage.Check();
        if (!value.HasSingleTarget || value.Target is not null)
            throw new InvalidOperationException("Owned emitted delegates must be single-target and use explicit receiver parameters.");
        storage.Add(value, Utf8ConstructionBudget.ObjectBytes<(object, object, nint, nint, object, nint)>());
    }

    internal static void CollectArray<TControl>(Lokad.Utf8Regex.Internal.Planning.Utf8StructuralSearchPlan[]? values, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (values is null || !storage.TryAdd(values, Utf8ConstructionBudget.ArrayBytes<Lokad.Utf8Regex.Internal.Planning.Utf8StructuralSearchPlan>(values.Length))) return;
        foreach (ref readonly var value in values.AsSpan()) value.CollectRetainedStorage(storage);
    }
}
