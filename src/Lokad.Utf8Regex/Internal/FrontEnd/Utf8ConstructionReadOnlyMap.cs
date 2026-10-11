using System.Collections;
using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Immutable metadata transfers its admitted table from the mutable map. Reads
// spend the current construction owner's work, never the closed original owner.
internal sealed class Utf8ConstructionReadOnlyMap<TKey, TValue>(
    Utf8ConstructionMap<TKey, TValue>.Entry[] entries, IEqualityComparer<TKey> comparer, int count)
    : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
{
    internal static Utf8ConstructionReadOnlyMap<TKey, TValue> Empty { get; } = new([], EqualityComparer<TKey>.Default, 0);
    public int Count { get; } = count;

    // The comparer is a borrowed shared framework singleton. Collect the actual
    // table capacity; the caller separately collects reference-bearing entries.
    internal ReadOnlySpan<Utf8ConstructionMap<TKey, TValue>.Entry> CollectStorage<TControl>(Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (!ReferenceEquals(comparer, EqualityComparer<TKey>.Default) && !ReferenceEquals(comparer, StringComparer.Ordinal))
            throw new InvalidOperationException("An owned comparer requires its own retained collector.");
        if (!storage.TryAdd(this, Utf8ConstructionBudget.ObjectBytes<
            (Utf8ConstructionMap<TKey, TValue>.Entry[], IEqualityComparer<TKey>, int)>())) return [];
        storage.Array(entries);
        return entries;
    }
    public TValue this[TKey key] => TryGetValue(key, out var value) ? value : throw new KeyNotFoundException();
    public IEnumerable<TKey> Keys => Create(new KeyView(this), static state => (IEnumerable<TKey>)state);
    public IEnumerable<TValue> Values => Create(new ValueView(this), static state => (IEnumerable<TValue>)state);

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

    public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() =>
        Create(new PairEnumerator(entries), static state => (IEnumerator<KeyValuePair<TKey, TValue>>)state);
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private static T Create<TState, T>(TState state, Func<TState, T> factory) where T : class =>
        Utf8ConstructionScope.IsActive ? Utf8ConstructionScope.CurrentBudget.AllocateObject(state, factory) : factory(state);

    private readonly struct KeyView(Utf8ConstructionReadOnlyMap<TKey, TValue> map) : IEnumerable<TKey>
    {
        public IEnumerator<TKey> GetEnumerator() => Create(new KeyEnumerator(map), static state => (IEnumerator<TKey>)state);
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
    private readonly struct ValueView(Utf8ConstructionReadOnlyMap<TKey, TValue> map) : IEnumerable<TValue>
    {
        public IEnumerator<TValue> GetEnumerator() => Create(new ValueEnumerator(map), static state => (IEnumerator<TValue>)state);
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
