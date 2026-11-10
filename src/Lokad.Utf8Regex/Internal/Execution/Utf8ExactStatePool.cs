using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

// Generic entry points carry no retained roots. One shared owner bounds exact
// array capacity across every thread and state type. Finite storage invocations
// may borrow existing leaves but cannot initialize or grow retained storage.
internal static class Utf8ExactStatePool<T>
{
    internal const int MaximumRetainedBytes = 16 * 1024;
    internal static int RetainedBytes => Utf8ExactStatePool.RetainedBytesFor<T>();
    internal static T[] Rent(int capacity, bool retain) => Utf8ExactStatePool.Rent<T>(capacity, retain);
    internal static void Return(T[] items, bool retain) => Utf8ExactStatePool.Return(items, retain);
}

internal static class Utf8ExactStatePool
{
    internal const int MaximumRetainedBytes = 256 * 1024;
    internal const int MaximumEntries = 64;
    private const int BucketCount = 5;
    internal const int MaximumLeafBytes = Utf8ExactStatePool<byte>.MaximumRetainedBytes / BucketCount;
    private readonly record struct Entry(Array? Value, int Bytes);
    private static SpinLock s_gate = new(enableThreadOwnerTracking: false);
    private static Entry[]? s_items;
    private static int s_retainedBytes;

    internal static int TableBytes => checked((int)Utf8ConstructionBudget.ArrayBytes<Entry>(MaximumEntries));

    internal static int RetainedBytes
    {
        get
        {
            var taken = false;
            try { s_gate.Enter(ref taken); return s_retainedBytes; }
            finally { if (taken) s_gate.Exit(); }
        }
    }

    internal static int RetainedBytesFor<T>()
    {
        var taken = false;
        try
        {
            s_gate.Enter(ref taken);
            var bytes = 0;
            if (s_items is { } cache)
                foreach (var entry in cache)
                    if (entry.Value?.GetType() == typeof(T[])) bytes += entry.Bytes;
            return bytes;
        }
        finally { if (taken) s_gate.Exit(); }
    }

    internal static T[] Rent<T>(int capacity, bool retain)
    {
        var bucket = Bucket(capacity);
        if (bucket >= 0)
        {
            var taken = false;
            try
            {
                // Optional retention never waits behind another invocation.
                s_gate.TryEnter(0, ref taken);
                if (taken && s_items is null && retain &&
                    Utf8ConstructionBudget.ArrayBytes<T>(capacity) <= MaximumLeafBytes)
                {
                    // Reserve the complete table extent before acquisition. A
                    // failed allocation restores the shared owner's loan.
                    s_retainedBytes = TableBytes;
                    try { s_items = new Entry[MaximumEntries]; }
                    catch { s_retainedBytes = 0; throw; }
                }
                if (taken && s_items is { } cache)
                {
                    var index = Slot<T>(bucket);
                    if (cache[index].Value is T[] items && items.GetType() == typeof(T[]) && items.Length == capacity)
                    {
                        s_retainedBytes -= cache[index].Bytes;
                        cache[index] = default;
                        return items;
                    }
                }
            }
            finally { if (taken) s_gate.Exit(); }
        }
        return new T[capacity];
    }

    internal static void Return<T>(T[] items, bool retain)
    {
        // Reference arrays are covariant. A pooled leaf must have the exact
        // requested element type so later borrowers can write any T safely.
        if (items.GetType() != typeof(T[])) return;
        var bucket = Bucket(items.Length);
        if (!retain || bucket < 0) return;
        var extent = Utf8ConstructionBudget.ArrayBytes<T>(items.Length);
        if (extent > MaximumLeafBytes) return;
        var bytes = (int)extent;
        // Return relinquishes the working leaf. Clear its bounded graph slots
        // before optional publication, including a contended retention bypass.
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>()) Array.Clear(items);
        var taken = false;
        try
        {
            s_gate.TryEnter(0, ref taken);
            if (!taken) return;
            if (s_items is not { } cache) return;
            // One slot per exact type/capacity permits replacement without
            // identity tracking or per-type totals. Five bounded leaves fit the
            // type ceiling; the fixed table and all slots fit the global one.
            var index = Slot<T>(bucket);
            s_retainedBytes = s_retainedBytes - cache[index].Bytes + bytes;
            cache[index] = new(items, bytes);
        }
        finally { if (taken) s_gate.Exit(); }
    }

    // Explicit release of the shared owner. Active working loans are independent
    // and may return later; finite calls still cannot initialize a new table.
    internal static void Clear()
    {
        var taken = false;
        try
        {
            s_gate.Enter(ref taken);
            s_items = null;
            s_retainedBytes = 0;
        }
        finally { if (taken) s_gate.Exit(); }
    }

    private static int Bucket(int capacity)
        => capacity is >= 16 and <= 256 && BitOperations.IsPow2((uint)capacity)
            ? BitOperations.Log2((uint)capacity) - 4 : -1;

    private static int Slot<T>(int bucket)
        => (RuntimeHelpers.GetHashCode(typeof(T[])) ^ bucket) & (MaximumEntries - 1);
}
