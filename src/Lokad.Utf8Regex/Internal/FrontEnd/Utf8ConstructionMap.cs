using System.Diagnostics.CodeAnalysis;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Construction-only open-addressed map with exact power-of-two array
/// capacity. Growth admits old/new overlap before acquiring the replacement;
/// cached hashes reinsert entries without invoking user equality during copying.</summary>
internal sealed class Utf8ConstructionMap<TKey, TValue>(Utf8ConstructionBudget budget, IEqualityComparer<TKey> comparer) : IDisposable
    where TKey : notnull
{
    private readonly record struct Entry(TKey Key, TValue Value, int Hash, bool Used);
    private Entry[] _entries = [];
    internal int Count { get; private set; }

    internal TValue this[TKey key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
        set
        {
            var hash = comparer.GetHashCode(key) & int.MaxValue;
            var index = Find(key, hash);
            if (index >= 0 && _entries[index].Used)
            {
                _entries[index] = new(key, value, hash, true);
                return;
            }
            if (_entries.Length == 0 || Count >= _entries.Length / 2)
            {
                var capacity = _entries.Length == 0 ? 4 : checked(_entries.Length * 2);
                var grown = budget.AllocateArray<Entry>(capacity, false);
                try
                {
                    foreach (var entry in _entries)
                    {
                        budget.ChargeWork(1);
                        if (!entry.Used) continue;
                        var target = entry.Hash & (capacity - 1);
                        while (grown[target].Used)
                        {
                            budget.ChargeWork(1);
                            target = (target + 1) & (capacity - 1);
                        }
                        grown[target] = entry;
                    }
                }
                catch { budget.ReleaseArray(grown, false); throw; }
                budget.ReleaseArray(_entries, false);
                _entries = grown;
                index = Find(key, hash);
            }
            _entries[index] = new(key, value, hash, true);
            Count++;
        }
    }

    internal bool ContainsKey(TKey key) => TryGetValue(key, out _);
    internal bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        var index = Find(key, comparer.GetHashCode(key) & int.MaxValue);
        if (index >= 0 && _entries[index].Used) { value = _entries[index].Value; return true; }
        value = default;
        return false;
    }

    internal void Add(TKey key, TValue value)
    {
        if (ContainsKey(key)) throw new ArgumentException("The construction key is already present.", nameof(key));
        this[key] = value;
    }

    internal int Find(TKey key, int hash)
    {
        if (_entries.Length == 0) return -1;
        var index = hash & (_entries.Length - 1);
        while (_entries[index].Used)
        {
            budget.ChargeWork(1);
            if (_entries[index].Hash == hash && comparer.Equals(_entries[index].Key, key)) return index;
            index = (index + 1) & (_entries.Length - 1);
        }
        return index;
    }

    /// <summary>Releases the temporary table while leaving borrowed keys/values to their owners.</summary>
    public void Dispose()
    {
        budget.ReleaseArray(_entries, false);
        _entries = [];
        Count = 0;
    }
}
