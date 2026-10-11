using System.Runtime.ExceptionServices;

namespace Lokad.Utf8Regex.Internal.Caching;

/// <summary>Owns bounded preparation-entry metadata. Pending entries share one
/// preparation without retaining its factory, state or invocation controls.
/// Key/value payload retention and complete public operation policy are separate
/// gates. PCRE2-INTEGRATION-POINT</summary>
internal sealed class Utf8BoundedPreparationCache<TKey, TValue> where TKey : notnull
{
    internal const ulong DefaultMaximumMetadataBytes = 64 * 1024;
    private CacheEntry? _first;
    private CacheEntry? _last;
    private readonly IEqualityComparer<TKey> _comparer;
    private int _capacity;
    private int _count;
    private readonly ulong _maximumMetadataBytes;
    private long _generation;

    private Utf8BoundedPreparationCache(IEqualityComparer<TKey> comparer, int capacity, ulong maximumMetadataBytes)
    { _comparer = comparer; _capacity = capacity; _maximumMetadataBytes = maximumMetadataBytes; }

    internal static ulong ObjectBytes => Utf8ConstructionBudget.ObjectBytes<
        (CacheEntry?, CacheEntry?, IEqualityComparer<TKey>, int, int, ulong, long)>();
    internal static ulong EntryBytes => Utf8ConstructionBudget.ObjectBytes<
        (TKey, ValueTuple<TValue>, Exception?, CacheEntry?, int, int, int)>();

    internal static Utf8BoundedPreparationCache<TKey, TValue> Create(int capacity)
        => Create(capacity, EqualityComparer<TKey>.Default, DefaultMaximumMetadataBytes);

    internal static Utf8BoundedPreparationCache<TKey, TValue> Create(int capacity, IEqualityComparer<TKey> comparer)
        => Create(capacity, comparer, DefaultMaximumMetadataBytes);

    internal static Utf8BoundedPreparationCache<TKey, TValue> Create(int capacity,
        IEqualityComparer<TKey> comparer, ulong maximumMetadataBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        if (maximumMetadataBytes < ObjectBytes) throw new ArgumentOutOfRangeException(nameof(maximumMetadataBytes));
        using var owner = Utf8ConstructionScope.Enter();
        var result = owner.Budget.AllocateObject(
            (First: (CacheEntry?)null, Last: (CacheEntry?)null, Comparer: comparer, Capacity: capacity, Count: 0, Maximum: maximumMetadataBytes, Generation: 0L),
            static state => new Utf8BoundedPreparationCache<TKey, TValue>(state.Comparer, state.Capacity, state.Maximum));
        owner.Complete();
        return result;
    }

    internal int Count { get { lock (this) return _count; } }
    internal int QueueLengthForTests => Count;
    internal ulong MetadataBytes { get { lock (this) return ObjectBytes + (ulong)_count * EntryBytes; } }
    internal ulong MaximumMetadataBytes => _maximumMetadataBytes;
    internal int Capacity
    {
        get => Volatile.Read(ref _capacity);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            lock (this)
            {
                if (value < _capacity) _generation++;
                _capacity = value;
                while (_count > value && _first is { } first) Remove(first);
            }
        }
    }

    internal TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        => GetOrAdd(key, factory, static (itemKey, itemFactory) => itemFactory(itemKey));

    internal TValue GetOrAdd<TState>(TKey key, TState state, Func<TKey, TState, TValue> factory)
        => GetOrAdd(key, state, factory, default(Utf8ConstructionTextNoControl));

    internal TValue GetOrAdd<TState, TControl>(TKey key, TState state, Func<TKey, TState, TValue> factory, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        Utf8ConstructionScope? cleanup = null;
        try
        {
            control.Check();
            if (Capacity == 0) return PrepareUncached(key, state, factory, control);
            var hash = _comparer.GetHashCode(key);
            CacheEntry? entry = null;
            long generation;
            Utf8ConstructionScope preparation;
            lock (this)
            {
                generation = _generation;
                var existing = Find(key, hash, control);
                if (existing is not null) return Read(existing, control);
                preparation = Utf8ConstructionScope.Enter();
                cleanup = preparation;
                if (CanAdd())
                {
                    entry = Acquire(key, default, null, 0, Environment.CurrentManagedThreadId, hash);
                    control.Check();
                    if (_generation == generation && CanAdd() && Find(key, hash, control) is null) Append(entry);
                    else entry = null;
                }
            }
            // A full cache does not evict a useful value before knowing that a new
            // preparation succeeds. No pending metadata is acquired in this lane.
            if (entry is null)
            {
                var value = PrepareUncached(key, state, factory, control);
                lock (this)
                {
                    control.Check();
                    if (_generation != generation) return Complete(preparation, value);
                    var existing = Find(key, hash, control);
                    if (existing is not null) return Read(existing, control);
                    if (_capacity == 0 || EntryBytes > _maximumMetadataBytes - ObjectBytes) return Complete(preparation, value);
                    CacheEntry? victim = null;
                    if (!CanAdd())
                    {
                        victim = _first;
                        while (victim is not null && victim.Status == 0) { control.Check(); victim = victim.Next; }
                        if (victim is null) return Complete(preparation, value);
                    }
                    var completed = Acquire(key, new(value), null, 1, 0, hash);
                    control.Check();
                    if (_generation != generation) return Complete(preparation, value);
                    existing = Find(key, hash, control);
                    if (existing is not null) return Read(existing, control);
                    victim = null;
                    if (!CanAdd())
                    {
                        victim = _first;
                        while (victim is not null && victim.Status == 0) { control.Check(); victim = victim.Next; }
                        if (victim is null) return Complete(preparation, value);
                    }
                    if (victim is not null) Remove(victim);
                    Append(completed);
                }
                return Complete(preparation, value);
            }
            try
            {
                var value = PrepareUncached(key, state, factory, control);
                lock (this)
                {
                    entry.Value = new(value); entry.OwnerThread = 0; entry.Status = 1;
                    // A detached generation can satisfy its existing waiters, but
                    // never repopulates the list after Clear or a capacity shrink.
                    Monitor.PulseAll(this);
                }
                return Complete(preparation, value);
            }
            catch (Exception failure)
            {
                lock (this)
                {
                    entry.Error = failure; entry.OwnerThread = 0; entry.Status = 2;
                    Remove(entry);
                    Monitor.PulseAll(this);
                }
                throw;
            }
        }
        finally { cleanup?.Dispose(); }
    }

    private static TValue Complete(Utf8ConstructionScope owner, TValue value)
    { owner.Complete(); return value; }

    private static TValue PrepareUncached<TState, TControl>(TKey key, TState state, Func<TKey, TState, TValue> factory, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        control.Check();
        var value = factory(key, state);
        control.Check();
        return value;
    }

    private bool CanAdd() => _count < _capacity && EntryBytes <= _maximumMetadataBytes - ObjectBytes - (ulong)_count * EntryBytes;

    private CacheEntry? Find<TControl>(TKey key, int hash, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        for (var entry = _first; entry is not null; entry = entry.Next)
        { control.Check(); if (entry.Hash == hash && _comparer.Equals(entry.Key, key)) return entry; }
        return null;
    }

    private TValue Read<TControl>(CacheEntry entry, TControl control) where TControl : struct, IUtf8ConstructionTextControl
    {
        while (entry.Status == 0)
        {
            control.Check();
            if (entry.OwnerThread == Environment.CurrentManagedThreadId)
                throw new InvalidOperationException("A preparation cannot recursively acquire its own cache entry.");
            // Waiting releases the cache lock. Invocation controls stay on the
            // caller's stack and can abort a follower without removing the owner.
            Monitor.Wait(this, 20);
        }
        control.Check();
        if (entry.Error is not null) ExceptionDispatchInfo.Throw(entry.Error);
        return entry.Value.Item1;
    }

    private static CacheEntry Acquire(TKey key, ValueTuple<TValue> value, Exception? error, int status, int ownerThread, int hash)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = owner.Budget.AllocateObject((key, value, error, Next: (CacheEntry?)null, status, ownerThread, hash),
            static state => new CacheEntry(state.key, state.value, state.error, state.status, state.ownerThread, state.hash));
        owner.Complete();
        return result;
    }

    private void Append(CacheEntry entry)
    {
        if (_last is null) _first = entry; else _last.Next = entry;
        _last = entry; _count++;
    }

    private void Remove(CacheEntry target)
    {
        CacheEntry? previous = null;
        for (var entry = _first; entry is not null; previous = entry, entry = entry.Next)
        {
            if (!ReferenceEquals(entry, target)) continue;
            if (previous is null) _first = entry.Next; else previous.Next = entry.Next;
            if (ReferenceEquals(_last, entry)) _last = previous;
            entry.Next = null; _count--;
            return;
        }
    }

    internal void Clear()
    {
        lock (this)
        {
            _generation++;
            while (_first is not null) Remove(_first);
        }
    }

    private sealed class CacheEntry(TKey key, ValueTuple<TValue> value, Exception? error, int status, int ownerThread, int hash)
    {
        internal readonly TKey Key = key;
        internal ValueTuple<TValue> Value = value;
        internal Exception? Error = error;
        internal CacheEntry? Next;
        internal int Status = status;
        internal int OwnerThread = ownerThread;
        internal readonly int Hash = hash;
    }
}
