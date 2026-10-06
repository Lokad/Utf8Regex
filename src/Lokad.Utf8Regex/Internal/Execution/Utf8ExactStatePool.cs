using System.Numerics;
using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.Execution;

// One thread owns each cache, with a byte cap including its five-slot table.
// Loans always have the requested extent, known before acquisition. Finite
// storage invocations may borrow existing loans but cannot grow this cache.
internal static class Utf8ExactStatePool<T> where T : unmanaged
{
    internal const int MaximumRetainedBytes = 16 * 1024;
    [ThreadStatic] private static T[]?[]? t_items;
    [ThreadStatic] private static int t_retainedBytes;

    internal static int RetainedBytes => t_retainedBytes;

    internal static T[] Rent(int capacity, bool retain)
    {
        var bucket = Bucket(capacity);
        if (retain && bucket >= 0 && t_items is null &&
            (ulong)(uint)capacity * (ulong)(uint)Unsafe.SizeOf<T>() <= (ulong)(MaximumRetainedBytes - 5 * IntPtr.Size))
        {
            t_items = new T[]?[5];
            t_retainedBytes = 5 * IntPtr.Size;
        }
        if (bucket >= 0 && t_items is { } cache && cache[bucket] is { } items)
        {
            cache[bucket] = null;
            t_retainedBytes -= items.Length * Unsafe.SizeOf<T>();
            return items;
        }
        return new T[capacity];
    }

    internal static void Return(T[] items, bool retain)
    {
        var bucket = Bucket(items.Length);
        if (!retain || bucket < 0 || t_items is null) return;
        var extent = (ulong)(uint)items.Length * (ulong)(uint)Unsafe.SizeOf<T>();
        var tableBytes = 5 * IntPtr.Size;
        if (extent > (ulong)(MaximumRetainedBytes - tableBytes)) return;
        var bytes = (int)extent;
        if (t_items[bucket] is { } previous)
            t_retainedBytes -= previous.Length * Unsafe.SizeOf<T>();
        t_items[bucket] = null;
        if (bytes > MaximumRetainedBytes - t_retainedBytes) return;
        t_items[bucket] = items;
        t_retainedBytes += bytes;
    }

    private static int Bucket(int capacity)
        => capacity is >= 16 and <= 256 && BitOperations.IsPow2((uint)capacity)
            ? BitOperations.Log2((uint)capacity) - 4 : -1;
}
