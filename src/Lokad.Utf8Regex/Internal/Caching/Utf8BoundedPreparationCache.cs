using System.Collections.Concurrent;

namespace Lokad.Utf8Regex.Internal.Caching;

/// <summary>
/// Bounded, concurrency-safe cache for values whose preparation must run at
/// most once per retained entry. Queue records carry the exact dictionary
/// entry so a failed generation can never evict a later value for the same key.
/// PCRE2-INTEGRATION-POINT
/// </summary>
internal sealed class Utf8BoundedPreparationCache<TKey, TValue>
    where TKey : notnull
{
    private readonly ConcurrentDictionary<TKey, CacheEntry> _entries;
    private readonly ConcurrentQueue<CacheEntry> _insertionOrder = new();
    private int _capacity;

    internal Utf8BoundedPreparationCache(int capacity)
        : this(capacity, EqualityComparer<TKey>.Default)
    {
    }

    internal Utf8BoundedPreparationCache(int capacity, IEqualityComparer<TKey> comparer)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        _entries = new ConcurrentDictionary<TKey, CacheEntry>(comparer);
        _capacity = capacity;
    }

    internal int Count => _entries.Count;

    internal int Capacity
    {
        get => Volatile.Read(ref _capacity);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            Volatile.Write(ref _capacity, value);
            TrimToCapacity();
        }
    }

    internal TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        => GetOrAdd(key, factory, static (itemKey, itemFactory) => itemFactory(itemKey));

    internal TValue GetOrAdd<TState>(TKey key, TState state, Func<TKey, TState, TValue> factory)
    {
        if (Capacity == 0)
        {
            return factory(key, state);
        }

        while (true)
        {
            if (_entries.TryGetValue(key, out var existing))
            {
                return GetPreparedValue(existing);
            }

            var created = new CacheEntry(
                key,
                new Lazy<TValue>(
                    () => factory(key, state),
                    LazyThreadSafetyMode.ExecutionAndPublication));
            if (_entries.TryAdd(key, created))
            {
                _insertionOrder.Enqueue(created);
                TrimToCapacity();
                return GetPreparedValue(created);
            }
        }
    }

    internal void Clear()
    {
        _entries.Clear();
        while (_insertionOrder.TryDequeue(out _))
        {
        }
    }

    private TValue GetPreparedValue(CacheEntry entry)
    {
        try
        {
            return entry.Preparation.Value;
        }
        catch
        {
            _entries.TryRemove(new KeyValuePair<TKey, CacheEntry>(entry.Key, entry));
            throw;
        }
    }

    private void TrimToCapacity()
    {
        var capacity = Capacity;
        while (_entries.Count > capacity && _insertionOrder.TryDequeue(out var entry))
        {
            _entries.TryRemove(new KeyValuePair<TKey, CacheEntry>(entry.Key, entry));
        }
    }

    private sealed record CacheEntry(TKey Key, Lazy<TValue> Preparation);
}
