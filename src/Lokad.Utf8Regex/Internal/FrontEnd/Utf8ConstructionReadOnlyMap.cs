using System.Collections;
using System.Diagnostics.CodeAnalysis;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Immutable metadata transfers its admitted table from the mutable map. Reads
// spend the current construction owner's work, never the closed original owner.
internal sealed class Utf8ConstructionReadOnlyMap<TKey, TValue>(
    Utf8ConstructionMap<TKey, TValue>.Entry[] entries, IEqualityComparer<TKey> comparer, int count)
    : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
    internal static Utf8ConstructionReadOnlyMap<TKey, TValue> Empty { get; } = new([], EqualityComparer<TKey>.Default, 0);
    public int Count { get; } = count;

    public TValue this[TKey key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
    // The backing table is admitted at construction/publication. Temporary views
    // and enumerator boxes have no individual byte loans or acquisition charges;
    // keep original interruption checks and charge the traversal itself below.
    public IEnumerable<TKey> Keys
    {
        get { Utf8ConstructionScope.ChargeWork(0); return new KeyView(this); }
    }
    public IEnumerable<TValue> Values
    {
        get { Utf8ConstructionScope.ChargeWork(0); return new ValueView(this); }
    }

    public bool ContainsKey(TKey key) => TryGetValue(key, out _);
    public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        if (entries.Length != 0)
        {
            if (key is string text) Utf8ConstructionScope.ChargeWork((ulong)text.Length);
            var hash = comparer.GetHashCode(key) & int.MaxValue;
            Utf8ConstructionScope.ChargeWork(1);
            var index = hash & (entries.Length - 1);
            var probes = 0;
            while (entries[index].Used)
            {
                if ((++probes & 63) == 0) Utf8ConstructionScope.ChargeWork(1);
                if (entries[index].Hash == hash)
                {
                    if (key is string candidate) Utf8ConstructionScope.ChargeWork((ulong)candidate.Length);
                    if (comparer.Equals(entries[index].Key, key)) { value = entries[index].Value; return true; }
                }
                index = (index + 1) & (entries.Length - 1);
            }
        }
        value = default;
        return false;
    }

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
    {
        Utf8ConstructionScope.ChargeWork(0);
        return EnumeratePairs().GetEnumerator();
    }

    private IEnumerable<KeyValuePair<TKey, TValue>> EnumeratePairs()
    {
        // Admit the complete backing-table scan once. Early termination may
        // overcharge; individual slots and iterator helper objects are not metered.
        Utf8ConstructionScope.ChargeWork((ulong)entries.Length);
        for (var index = 0; index < entries.Length; index++)
        {
            if ((index & 63) == 0) Utf8ConstructionScope.ChargeWork(0);
            if (entries[index].Used) yield return new(entries[index].Key, entries[index].Value);
        }
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<TKey> EnumerateKeys()
    {
        foreach (var entry in this) yield return entry.Key;
    }
    private IEnumerable<TValue> EnumerateValues()
    {
        foreach (var entry in this) yield return entry.Value;
    }
    private readonly struct KeyView(Utf8ConstructionReadOnlyMap<TKey, TValue> map) : IEnumerable<TKey>
    {
        public IEnumerator<TKey> GetEnumerator()
        {
            Utf8ConstructionScope.ChargeWork(0);
            return map.EnumerateKeys().GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private readonly struct ValueView(Utf8ConstructionReadOnlyMap<TKey, TValue> map) : IEnumerable<TValue>
    {
        public IEnumerator<TValue> GetEnumerator()
        {
            Utf8ConstructionScope.ChargeWork(0);
            return map.EnumerateValues().GetEnumerator();
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
