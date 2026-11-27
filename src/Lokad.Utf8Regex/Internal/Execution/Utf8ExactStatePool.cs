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
    // Keep lookup keys together so scans need not access every retained array.
    private readonly record struct Entry(Array Value, Type ArrayType, int Capacity, int Bytes);
    private static SpinLock s_gate = new(enableThreadOwnerTracking: false);
    private static Entry[]? s_items;
    private static int s_retainedBytes;
    private static int s_count;
    private static int s_nextVictim;

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
            {
                var arrayType = typeof(T[]);
                for (var index = 0; index < s_count; index++)
                {
                    ref readonly var entry = ref cache[index];
                    if (entry.ArrayType == arrayType) bytes += entry.Bytes;
                }
            }
            return bytes;
        }
        finally { if (taken) s_gate.Exit(); }
    }

    internal static T[] Rent<T>(int capacity, bool retain)
    {
        if (CanPool<T>(capacity))
        {
            var taken = false;
            try
            {
                // Optional retention never waits behind another invocation.
                s_gate.TryEnter(0, ref taken);
                if (taken && s_items is null && retain &&
                    Utf8ConstructionBudget.ArrayBytes<T>(capacity) <= (ulong)Utf8ExactStatePool<T>.MaximumRetainedBytes)
                {
                    // Reserve the complete table extent before acquisition. A
                    // failed allocation restores the shared owner's loan.
                    s_retainedBytes = TableBytes;
                    try { s_items = new Entry[MaximumEntries]; }
                    catch { s_retainedBytes = 0; throw; }
                }
                if (taken && s_items is { } cache)
                {
                    var arrayType = typeof(T[]);
                    for (var index = 0; index < s_count; index++)
                    {
                        ref readonly var entry = ref cache[index];
                        if (entry.ArrayType == arrayType && entry.Capacity == capacity)
                        {
                            var value = (T[])entry.Value;
                            RemoveAt(cache, index);
                            return value;
                        }
                    }
                }
            }
            finally { if (taken) s_gate.Exit(); }
        }
        return new T[capacity];
    }

    internal static void Return<T>(T[] items, bool retain)
    {
        if (!retain || !CanPool<T>(items.Length)) return;
        // Reference arrays are covariant. A pooled leaf must have the exact
        // requested element type so later borrowers can write any T safely.
        var arrayType = typeof(T[]);
        if (items.GetType() != arrayType) return;
        var extent = Utf8ConstructionBudget.ArrayBytes<T>(items.Length);
        if (extent > (ulong)Utf8ExactStatePool<T>.MaximumRetainedBytes) return;
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
            var typeBytes = 0;
            // Identical capacities can belong to distinct simultaneous loans.
            // Every returned leaf contributes to both retention bounds.
            for (var index = 0; index < s_count; index++)
            {
                ref readonly var entry = ref cache[index];
                if (entry.ArrayType != arrayType) continue;
                if (ReferenceEquals(entry.Value, items)) return;
                typeBytes += entry.Bytes;
            }
            if (bytes > Utf8ExactStatePool<T>.MaximumRetainedBytes - typeBytes) return;
            while (s_count == cache.Length || bytes > MaximumRetainedBytes - s_retainedBytes)
            {
                var victim = s_nextVictim % s_count;
                s_nextVictim = (s_nextVictim + 1) % cache.Length;
                RemoveAt(cache, victim);
            }
            s_retainedBytes += bytes; // Admit the physical leaf before adoption.
            cache[s_count++] = new(items, arrayType, items.Length, bytes);
        }
        finally { if (taken) s_gate.Exit(); }
    }

    // Under the gate, keep all retained leaves in one dense prefix. Vacated
    // suffix slots release their roots; several same-type/capacity loans remain
    // independent entries. Optional eviction order is not an ownership contract.
    private static void RemoveAt(Entry[] cache, int index)
    {
        s_retainedBytes -= cache[index].Bytes;
        var last = --s_count;
        if (index != last) cache[index] = cache[last];
        cache[last] = default;
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
            s_count = 0;
            s_nextVictim = 0;
        }
        finally { if (taken) s_gate.Exit(); }
    }

    // Larger value-only staging buffers need no graph clearing on return.
    private static bool CanPool<T>(int capacity)
        => capacity is >= 8 and <= 512 && BitOperations.IsPow2((uint)capacity) &&
            (capacity <= 256 || !RuntimeHelpers.IsReferenceOrContainsReferences<T>());
}
