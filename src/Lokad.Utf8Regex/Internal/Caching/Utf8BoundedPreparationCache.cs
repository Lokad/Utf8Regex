using System.Runtime.ExceptionServices;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

/// <summary>Owns bounded preparation-entry metadata. Pending entries share one
/// preparation without retaining its factory, state or invocation controls.
/// Immutable payload charges or declared root/descendant reservations have
/// a separate conservative per-entry cap. Shared storage across entries is
/// charged repeatedly; no retained manifests or global identity registry.
/// These modeled cache charges do not bound total program/process memory;
/// public invocation and construction policies have their own documented scope.
/// PCRE2-INTEGRATION-POINT</summary>
internal sealed class Utf8BoundedPreparationCache<TKey, TValue> where TKey : notnull
{
    internal const ulong DefaultMaximumMetadataBytes = 64 * 1024;
    internal const ulong DefaultMaximumPayloadBytes = 4 * 1024 * 1024;
    private CacheEntry? _first;
    private CacheEntry? _last;
    private readonly IEqualityComparer<TKey> _comparer;
    private int _capacity;
    private int _count;
    private readonly ulong _maximumMetadataBytes;
    private long _generation;
    private readonly ulong _maximumPayloadBytes;
    private ulong _payloadBytes;
    private long _revision;
    private readonly Utf8RetainedStorageScope _storageScope;

    private Utf8BoundedPreparationCache(IEqualityComparer<TKey> comparer, int capacity, ulong maximumMetadataBytes,
        ulong maximumPayloadBytes, Utf8RetainedStorageScope storageScope)
    { _comparer = comparer; _capacity = capacity; _maximumMetadataBytes = maximumMetadataBytes; _maximumPayloadBytes = maximumPayloadBytes; _storageScope = storageScope; }

    internal static ulong ObjectBytes => Utf8ConstructionBudget.ObjectBytes<
        (CacheEntry?, CacheEntry?, IEqualityComparer<TKey>, int, int, ulong, long, ulong, ulong, long, Utf8RetainedStorageScope)>();
    internal static ulong EntryBytes => Utf8ConstructionBudget.ObjectBytes<
        (TKey, ValueTuple<TValue>, Exception?, CacheEntry?, int, int, int, ulong)>();
    private ulong BaseMetadataBytes => ObjectBytes;

    internal static Utf8BoundedPreparationCache<TKey, TValue> CreateRetained(int capacity, IEqualityComparer<TKey> comparer)
        => CreateRetained(capacity, comparer, DefaultMaximumMetadataBytes, DefaultMaximumPayloadBytes);

    internal static Utf8BoundedPreparationCache<TKey, TValue> CreateRetained(int capacity,
        IEqualityComparer<TKey> comparer, ulong maximumMetadataBytes, ulong maximumPayloadBytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maximumPayloadBytes);
        return Create(capacity, comparer, maximumMetadataBytes, maximumPayloadBytes, Utf8RetainedStorageScope.CompletePayload);
    }

    internal static Utf8BoundedPreparationCache<TKey, TValue> CreateRootAndDescendantRetained(int capacity,
        IEqualityComparer<TKey> comparer, ulong maximumMetadataBytes, ulong maximumStorageBytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maximumStorageBytes);
        return Create(capacity, comparer, maximumMetadataBytes, maximumStorageBytes, Utf8RetainedStorageScope.RootsAndBoundedDescendants);
    }

    private static Utf8BoundedPreparationCache<TKey, TValue> Create(int capacity,
        IEqualityComparer<TKey> comparer, ulong maximumMetadataBytes, ulong maximumPayloadBytes, Utf8RetainedStorageScope storageScope)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        if (maximumMetadataBytes < ObjectBytes) throw new ArgumentOutOfRangeException(nameof(maximumMetadataBytes));
        using var owner = Utf8ConstructionScope.Enter();
        var result = owner.Budget.AllocateObject(
            (First: (CacheEntry?)null, Last: (CacheEntry?)null, Comparer: comparer, Capacity: capacity, Count: 0, Maximum: maximumMetadataBytes, Generation: 0L, MaximumPayload: maximumPayloadBytes, PayloadBytes: 0UL, Revision: 0L, Scope: storageScope),
            static state => new Utf8BoundedPreparationCache<TKey, TValue>(state.Comparer, state.Capacity, state.Maximum, state.MaximumPayload, state.Scope));
        owner.Complete();
        return result;
    }

    internal int Count { get { lock (this) return _count; } }
    internal int QueueLengthForTests => Count;
    internal ulong MetadataBytes { get { lock (this) return BaseMetadataBytes + (ulong)_count * EntryBytes; } }
    internal ulong MaximumMetadataBytes => _maximumMetadataBytes;
    internal Utf8RetainedStorageScope StorageScope => _storageScope;
    internal ulong PayloadBytes { get { lock (this) return _payloadBytes; } }
    internal ulong MaximumPayloadBytes => _maximumPayloadBytes;
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
                _revision++;
                while (_count > value && _first is { } first) Remove(first);
            }
        }
    }

    internal TValue GetOrAdd<TState, TControl, TStorage>(TKey key, TState state,
        Func<TKey, TState, TValue> factory, TControl control, TStorage storage)
        where TControl : struct, IUtf8ConstructionTextControl
        where TStorage : struct, IUtf8RetainedStorage<TKey, TValue>
    {
        if (storage.Scope != _storageScope)
            throw new InvalidOperationException("Retention requires the owner's declared storage scope.");
        Utf8ConstructionScope? cleanup = null;
        try
        {
            control.Check();
            if (Capacity == 0) return PrepareUncached(key, state, factory, control);
            var keyCharge = storage.GetKeyCharge(key);
            if (keyCharge > _maximumPayloadBytes)
            {
                // This immutable owner can never retain the key. Do not hash or
                // compare its text before the factory's construction admission.
                using var oversized = Utf8ConstructionScope.Enter();
                oversized.Budget.ChargeWork(1);
                return Complete(oversized, PrepareUncached(key, state, factory, control));
            }
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
                control.Check();
                preparation.Budget.ChargeWork(1);
                if (CanAdd())
                {
                    entry = Acquire(key, default, null, 0, Environment.CurrentManagedThreadId, hash, keyCharge);
                    control.Check();
                    if (_generation != generation || !CanAdd() || Find(key, hash, control) is not null ||
                        !PublishCharge(entry, null, keyCharge, false, generation, control))
                        entry = null;
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
                    if (_capacity == 0 || EntryBytes > _maximumMetadataBytes - BaseMetadataBytes) return Complete(preparation, value);
                    CacheEntry? victim = null;
                    if (!CanAdd())
                    {
                        victim = _first;
                        while (victim is not null && victim.Status == 0) { control.Check(); victim = victim.Next; }
                        if (victim is null) return Complete(preparation, value);
                    }
                    var charge = CollectCharge(keyCharge, value, storage, control, out var fits);
                    if (!fits) return Complete(preparation, value);
                    var completed = Acquire(key, new(value), null, 1, 0, hash, charge);
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
                    _ = PublishCharge(completed, null, charge, true, generation, control);
                }
                return Complete(preparation, value);
            }
            try
            {
                var value = PrepareUncached(key, state, factory, control);
                lock (this)
                {
                    if (Contains(entry))
                    {
                        var charge = CollectCharge(keyCharge, value, storage, control, out var fits);
                        if (!fits || !PublishCharge(entry, entry, charge, true, generation, control)) Remove(entry);
                    }
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

    private bool CanAdd() => _count < _capacity && EntryBytes <= _maximumMetadataBytes - BaseMetadataBytes - (ulong)_count * EntryBytes;

    private ulong CollectCharge<TStorage, TControl>(ulong keyCharge, TValue value, TStorage storage,
        TControl control, out bool fits)
        where TControl : struct, IUtf8ConstructionTextControl
        where TStorage : struct, IUtf8RetainedStorage<TKey, TValue>
    {
        if (keyCharge > _maximumPayloadBytes) { fits = false; return 0; }
        var remaining = _maximumPayloadBytes - keyCharge;
        fits = storage.TryGetValueCharge(value, remaining, control, out var valueCharge) && valueCharge <= remaining;
        // The provider admits value capacity against the remainder, so this sum
        // cannot overflow. Key/value sharing intentionally earns no discount.
        return fits ? keyCharge + valueCharge : 0;
    }

    private bool Contains(CacheEntry target)
    {
        for (var entry = _first; entry is not null; entry = entry.Next)
            if (ReferenceEquals(entry, target)) return true;
        return false;
    }

    // Plan eviction with scalar totals. No graph union, manifest acquisition or
    // reference-count rollback. Keep the revision check because a diagnostic
    // control may reenter Clear/shrink while the owner lock is held.
    private bool PublishCharge<TControl>(CacheEntry candidate, CacheEntry? replacing,
        ulong charge, bool mayEvict, long generation, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (charge > _maximumPayloadBytes) return false;
        while (true)
        {
            control.Check();
            Utf8ConstructionScope.ChargeWork(1);
            if (_generation != generation || replacing is not null && !Contains(replacing)) return false;
            var revision = _revision;
            var bytes = _payloadBytes - (replacing?.PayloadCharge ?? 0);
            var retained = _count - (replacing is null ? 0 : 1);
            var victims = 0;
            for (var entry = _first; entry is not null; entry = entry.Next)
            {
                if (HasRoomForCharge(bytes, retained, charge)) break;
                control.Check();
                Utf8ConstructionScope.ChargeWork(1);
                if (_revision != revision) break;
                if (!mayEvict || ReferenceEquals(entry, replacing) || entry.Status != 1) continue;
                bytes -= entry.PayloadCharge;
                retained--;
                victims++;
            }
            control.Check();
            if (_generation != generation) return false;
            if (_revision != revision) continue;
            if (!HasRoomForCharge(bytes, retained, charge)) return false;
            // No allocation, cancellation callback or fallible arithmetic remains.
            for (var entry = _first; entry is not null && victims > 0;)
            {
                var next = entry.Next;
                if (!ReferenceEquals(entry, replacing) && entry.Status == 1) { Remove(entry); victims--; }
                entry = next;
            }
            _payloadBytes -= replacing?.PayloadCharge ?? 0;
            candidate.PayloadCharge = charge;
            _payloadBytes += charge;
            if (replacing is null) Append(candidate); else _revision++;
            return true;
        }

    }

    private bool HasRoomForCharge(ulong bytes, int retained, ulong charge) =>
        bytes <= _maximumPayloadBytes - charge && retained < _capacity &&
        (ulong)(retained + 1) * EntryBytes <= _maximumMetadataBytes - BaseMetadataBytes;

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

    private static CacheEntry Acquire(TKey key, ValueTuple<TValue> value, Exception? error, int status, int ownerThread, int hash,
        ulong charge)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = owner.Budget.AllocateObject((key, value, error, Next: (CacheEntry?)null, status, ownerThread, hash, charge),
            static state => new CacheEntry(state.key, state.value, state.error, state.status, state.ownerThread, state.hash, state.charge));
        owner.Complete();
        return result;
    }

    private void Append(CacheEntry entry)
    {
        if (_last is null) _first = entry; else _last.Next = entry;
        _last = entry; _count++; _revision++;
    }

    private void Remove(CacheEntry target)
    {
        CacheEntry? previous = null;
        for (var entry = _first; entry is not null; previous = entry, entry = entry.Next)
        {
            if (!ReferenceEquals(entry, target)) continue;
            if (previous is null) _first = entry.Next; else previous.Next = entry.Next;
            if (ReferenceEquals(_last, entry)) _last = previous;
            _payloadBytes -= entry.PayloadCharge;
            entry.Next = null; entry.PayloadCharge = 0; _count--; _revision++;
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

    private sealed class CacheEntry(TKey key, ValueTuple<TValue> value, Exception? error, int status, int ownerThread, int hash,
        ulong charge)
    {
        internal readonly TKey Key = key;
        internal ValueTuple<TValue> Value = value;
        internal Exception? Error = error;
        internal CacheEntry? Next;
        internal int Status = status;
        internal int OwnerThread = ownerThread;
        internal readonly int Hash = hash;
        internal ulong PayloadCharge = charge;
    }
}
