using System.Diagnostics.CodeAnalysis;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Construction-only open-addressed map with exact power-of-two array
/// capacity. Growth admits old/new overlap before acquiring the replacement;
/// cached hashes reinsert entries without invoking user equality during copying.</summary>
internal sealed class Utf8ConstructionMap<TKey, TValue>(Utf8ConstructionBudget budget, IEqualityComparer<TKey> comparer) : IDisposable
    where TKey : notnull
{
    internal readonly record struct Entry(TKey Key, TValue Value, int Hash, bool Used);
    private Entry[] _entries = [];
    internal int Count { get; private set; }
    internal ulong StorageBytes => Utf8ConstructionBudget.ArrayBytes<Entry>(_entries.Length);

    internal static Utf8ConstructionMap<TKey, TValue> Create(Utf8ConstructionBudget owner, IEqualityComparer<TKey> equality) =>
        owner.AllocateObject((owner, equality), static state => new Utf8ConstructionMap<TKey, TValue>(state.owner, state.equality));

    internal Utf8ConstructionReadOnlyMap<TKey, TValue> Freeze(Utf8ConstructionArena arena)
    {
        if (Count == 0) return Utf8ConstructionReadOnlyMap<TKey, TValue>.Empty;
        var result = budget.AllocateObject((_entries, comparer, Count), static state =>
            new Utf8ConstructionReadOnlyMap<TKey, TValue>(state._entries, state.comparer, state.Count));
        var entries = _entries;
        _entries = [];
        Count = 0;
        arena.Adopt(entries, true);
        return result;
    }

    // Transfers the funded physical table to an owner that supplies its own
    // immutable publication and reference-release protocol.
    internal Entry[] TakeEntries(out int count)
    {
        var result = _entries;
        count = Count;
        _entries = [];
        Count = 0;
        return result;
    }

    internal TValue this[TKey key]
    {
        get => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
        set => Insert(key, value, false);
    }

    private void Insert(TKey key, TValue value, bool addOnly)
    {
        if (key is string text) budget.ChargeWork((ulong)text.Length);
        var hash = comparer.GetHashCode(key) & int.MaxValue;
        var index = Find(_entries, key, hash);
        if (index >= 0 && _entries[index].Used)
        {
            if (addOnly) throw new ArgumentException("The construction key is already present.", nameof(key));
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
                // The last metered probe must succeed before the new table
                // replaces the caller's existing capacity and entries.
                index = Find(grown, key, hash);
            }
            catch { budget.ReleaseArray(grown, false); throw; }
            budget.ReleaseArray(_entries, false);
            _entries = grown;
        }
        _entries[index] = new(key, value, hash, true);
        Count++;
    }

    internal bool ContainsKey(TKey key) => TryGetValue(key, out _);
    internal bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (key is string text) budget.ChargeWork((ulong)text.Length);
        var index = Find(_entries, key, comparer.GetHashCode(key) & int.MaxValue);
        if (index >= 0 && _entries[index].Used) { value = _entries[index].Value; return true; }
        value = default;
        return false;
    }

    internal void Add(TKey key, TValue value) => Insert(key, value, true);

    private int Find(Entry[] entries, TKey key, int hash)
    {
        if (entries.Length == 0) return -1;
        var index = hash & (entries.Length - 1);
        while (entries[index].Used)
        {
            budget.ChargeWork(1);
            if (entries[index].Hash == hash)
            {
                if (key is string text) budget.ChargeWork((ulong)text.Length);
                if (comparer.Equals(entries[index].Key, key)) return index;
            }
            index = (index + 1) & (entries.Length - 1);
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
