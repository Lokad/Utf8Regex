using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Execution;

// Each builder publishes one delegate, retaining three final CLR-managed copies.
// The receipt is final retained capacity, not a construction high-water mark.
// Its delegate identity owns the allowance; aliases share it without double charge.
internal readonly record struct Utf8EmittedDelegate<TDelegate>(TDelegate Callback, ulong ManagedArrayBytes)
    where TDelegate : Delegate
{
    internal void CollectRetainedStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (Callback is null) return;
        if (ManagedArrayBytes == 0)
            throw new InvalidOperationException("An emitted delegate must retain its final managed-copy receipt.");
        Utf8RuntimeRetainedStorage.CollectDelegate(Callback, storage, ManagedArrayBytes);
    }
}
