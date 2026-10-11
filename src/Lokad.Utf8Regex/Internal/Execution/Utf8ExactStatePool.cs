using System.Numerics;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

// Generic entry points carry no retained roots. One shared owner bounds exact
// array capacity across every thread and state type. Finite storage invocations
// may borrow existing leaves but cannot initialize or grow retained storage.
internal static class Utf8ExactStatePool<T> where T : struct
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
    private readonly record struct Entry(Array? Value, int Bytes);
    private static SpinLock s_gate = new(enableThreadOwnerTracking: false);
    private static Entry[]? s_items;
    private static int s_retainedBytes;
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

    internal static int RetainedBytesFor<T>() where T : struct
    {
        var taken = false;
        try
        {
            s_gate.Enter(ref taken);
            var bytes = 0;
            if (s_items is { } cache)
                foreach (var entry in cache)
                    if (entry.Value is T[]) bytes += entry.Bytes;
            return bytes;
        }
        finally { if (taken) s_gate.Exit(); }
    }

    internal static T[] Rent<T>(int capacity, bool retain) where T : struct
    {
        if (Bucket(capacity) >= 0)
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
                    for (var index = 0; index < cache.Length; index++)
                        if (cache[index].Value is T[] items && items.Length == capacity)
                        {
                            s_retainedBytes -= cache[index].Bytes;
                            cache[index] = default;
                            return items;
                        }
            }
            finally { if (taken) s_gate.Exit(); }
        }
        return new T[capacity];
    }

    internal static void Return<T>(T[] items, bool retain) where T : struct
    {
        if (!retain || Bucket(items.Length) < 0) return;
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
            var empty = -1;
            var typeBytes = 0;
            // Identical capacities can belong to distinct simultaneous loans.
            // Every returned leaf contributes to both retention bounds.
            for (var index = 0; index < cache.Length; index++)
            {
                var entry = cache[index];
                if (entry.Value is null) { if (empty < 0) empty = index; }
                else if (ReferenceEquals(entry.Value, items)) return;
                else if (entry.Value is T[]) typeBytes += entry.Bytes;
            }
            if (bytes > Utf8ExactStatePool<T>.MaximumRetainedBytes - typeBytes) return;
            while (empty < 0 || bytes > MaximumRetainedBytes - s_retainedBytes)
            {
                var victim = s_nextVictim;
                s_nextVictim = (s_nextVictim + 1) % cache.Length;
                if (cache[victim].Value is null) continue;
                s_retainedBytes -= cache[victim].Bytes;
                cache[victim] = default;
                if (empty < 0) empty = victim;
            }
            s_retainedBytes += bytes; // Admit the physical leaf before adoption.
            cache[empty] = new(items, bytes);
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
            s_nextVictim = 0;
        }
        finally { if (taken) s_gate.Exit(); }
    }

    private static int Bucket(int capacity)
        => capacity is >= 16 and <= 256 && BitOperations.IsPow2((uint)capacity)
            ? BitOperations.Log2((uint)capacity) - 4 : -1;
}
