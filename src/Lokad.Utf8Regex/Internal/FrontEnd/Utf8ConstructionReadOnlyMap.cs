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
            var index = hash & (entries.Length - 1);
            while (entries[index].Used)
            {
                Utf8ConstructionScope.ChargeWork(1);
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
        return new PairEnumerator(entries);
    }
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private readonly struct KeyView(Utf8ConstructionReadOnlyMap<TKey, TValue> map) : IEnumerable<TKey>
    {
        public IEnumerator<TKey> GetEnumerator()
        {
            Utf8ConstructionScope.ChargeWork(0);
            return new KeyEnumerator(map);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private readonly struct ValueView(Utf8ConstructionReadOnlyMap<TKey, TValue> map) : IEnumerable<TValue>
    {
        public IEnumerator<TValue> GetEnumerator()
        {
            Utf8ConstructionScope.ChargeWork(0);
            return new ValueEnumerator(map);
        }
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private struct PairEnumerator(Utf8ConstructionMap<TKey, TValue>.Entry[] source) : IEnumerator<KeyValuePair<TKey, TValue>>
    {
        private int _index = -1;
        public readonly KeyValuePair<TKey, TValue> Current => new(source[_index].Key, source[_index].Value);
        readonly object IEnumerator.Current => Current;
        public bool MoveNext()
        {
            while (++_index < source.Length)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (source[_index].Used) return true;
            }
            return false;
        }
        public void Reset() => _index = -1;
        public readonly void Dispose() { }
    }
    private struct KeyEnumerator : IEnumerator<TKey>
    {
        private PairEnumerator _pairs;
        internal KeyEnumerator(Utf8ConstructionReadOnlyMap<TKey, TValue> map) => _pairs = new(map.GetEntries());
        public readonly TKey Current => _pairs.Current.Key;
        readonly object IEnumerator.Current => Current;
        public bool MoveNext() => _pairs.MoveNext();
        public void Reset() => _pairs.Reset();
        public readonly void Dispose() { }
    }
    private struct ValueEnumerator : IEnumerator<TValue>
    {
        private PairEnumerator _pairs;
        internal ValueEnumerator(Utf8ConstructionReadOnlyMap<TKey, TValue> map) => _pairs = new(map.GetEntries());
        public readonly TValue Current => _pairs.Current.Value;
        readonly object? IEnumerator.Current => Current;
        public bool MoveNext() => _pairs.MoveNext();
        public void Reset() => _pairs.Reset();
        public readonly void Dispose() { }
    }
    private Utf8ConstructionMap<TKey, TValue>.Entry[] GetEntries() => entries;
}
