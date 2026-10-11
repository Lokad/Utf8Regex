using System.Runtime.ExceptionServices;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

/// <summary>Owns bounded preparation-entry metadata. Pending entries share one
/// preparation without retaining its factory, state or invocation controls.
/// Qualified immutable payloads have a separate identity-based byte cap. The
/// global program cache and complete public policy remain separate gates.
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
    private readonly Utf8RetainedPayloadLedger? _payloadLedger;
    private long _revision;

    private Utf8BoundedPreparationCache(IEqualityComparer<TKey> comparer, int capacity, ulong maximumMetadataBytes,
        ulong maximumPayloadBytes, Utf8RetainedPayloadLedger? payloadLedger)
    { _comparer = comparer; _capacity = capacity; _maximumMetadataBytes = maximumMetadataBytes; _maximumPayloadBytes = maximumPayloadBytes; _payloadLedger = payloadLedger; }

    internal static ulong ObjectBytes => Utf8ConstructionBudget.ObjectBytes<
        (CacheEntry?, CacheEntry?, IEqualityComparer<TKey>, int, int, ulong, long, ulong, Utf8RetainedPayloadLedger?, long)>();
    internal static ulong EntryBytes => Utf8ConstructionBudget.ObjectBytes<
        (TKey, ValueTuple<TValue>, Exception?, CacheEntry?, int, int, int, Utf8RetainedPayloadExtent[])>();
    private ulong BaseMetadataBytes => ObjectBytes + (_payloadLedger is null ? 0 : Utf8RetainedPayloadLedger.ObjectBytes);

    internal static Utf8BoundedPreparationCache<TKey, TValue> Create(int capacity)
        => Create(capacity, EqualityComparer<TKey>.Default, DefaultMaximumMetadataBytes);

    internal static Utf8BoundedPreparationCache<TKey, TValue> Create(int capacity, IEqualityComparer<TKey> comparer)
        => Create(capacity, comparer, DefaultMaximumMetadataBytes);

    internal static Utf8BoundedPreparationCache<TKey, TValue> Create(int capacity,
        IEqualityComparer<TKey> comparer, ulong maximumMetadataBytes)
        => Create(capacity, comparer, maximumMetadataBytes, 0);

    internal static Utf8BoundedPreparationCache<TKey, TValue> CreateRetained(int capacity, IEqualityComparer<TKey> comparer)
        => CreateRetained(capacity, comparer, DefaultMaximumMetadataBytes, DefaultMaximumPayloadBytes);

    internal static Utf8BoundedPreparationCache<TKey, TValue> CreateRetained(int capacity,
        IEqualityComparer<TKey> comparer, ulong maximumMetadataBytes, ulong maximumPayloadBytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(maximumPayloadBytes);
        return Create(capacity, comparer, maximumMetadataBytes, maximumPayloadBytes);
    }

    private static Utf8BoundedPreparationCache<TKey, TValue> Create(int capacity,
        IEqualityComparer<TKey> comparer, ulong maximumMetadataBytes, ulong maximumPayloadBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        if (maximumMetadataBytes < ObjectBytes + (maximumPayloadBytes == 0 ? 0 : Utf8RetainedPayloadLedger.ObjectBytes)) throw new ArgumentOutOfRangeException(nameof(maximumMetadataBytes));
        using var owner = Utf8ConstructionScope.Enter();
        var ledger = maximumPayloadBytes == 0 ? null : Utf8RetainedPayloadLedger.Create();
        var result = owner.Budget.AllocateObject(
            (First: (CacheEntry?)null, Last: (CacheEntry?)null, Comparer: comparer, Capacity: capacity, Count: 0, Maximum: maximumMetadataBytes, Generation: 0L, MaximumPayload: maximumPayloadBytes, Ledger: ledger, Revision: 0L),
            static state => new Utf8BoundedPreparationCache<TKey, TValue>(state.Comparer, state.Capacity, state.Maximum, state.MaximumPayload, state.Ledger));
        owner.Complete();
        return result;
    }

    internal int Count { get { lock (this) return _count; } }
    internal int QueueLengthForTests => Count;
    internal ulong MetadataBytes { get { lock (this) return BaseMetadataBytes + (ulong)_count * EntryBytes; } }
    internal ulong MaximumMetadataBytes => _maximumMetadataBytes;
    internal bool IsPayloadBounded => _payloadLedger is not null;
    internal ulong PayloadBytes { get { lock (this) return _payloadLedger?.Bytes ?? 0; } }
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

    internal TValue GetOrAdd(TKey key, Func<TKey, TValue> factory)
        => GetOrAdd(key, factory, static (itemKey, itemFactory) => itemFactory(itemKey));

    internal TValue GetOrAdd<TState>(TKey key, TState state, Func<TKey, TState, TValue> factory)
        => GetOrAdd(key, state, factory, default(Utf8ConstructionTextNoControl));

    internal TValue GetOrAdd<TState, TControl>(TKey key, TState state, Func<TKey, TState, TValue> factory, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
        => GetOrAdd(key, state, factory, control, default(Utf8UncertifiedStorage<TKey, TValue>));

    internal TValue GetOrAdd<TState, TControl, TStorage>(TKey key, TState state,
        Func<TKey, TState, TValue> factory, TControl control, TStorage storage)
        where TControl : struct, IUtf8ConstructionTextControl
        where TStorage : struct, IUtf8RetainedStorage<TKey, TValue>
    {
        if (IsPayloadBounded && !storage.IsCertified) throw new InvalidOperationException("Payload retention requires a complete storage collector.");
        Utf8ConstructionScope? cleanup = null;
        try
        {
            control.Check();
            if (Capacity == 0) return PrepareUncached(key, state, factory, control);
            var hash = _comparer.GetHashCode(key);
            CacheEntry? entry = null;
            var keyFits = true;
            long generation;
            Utf8ConstructionScope preparation;
            lock (this)
            {
                generation = _generation;
                var existing = Find(key, hash, control);
                if (existing is not null) return Read(existing, control);
                preparation = Utf8ConstructionScope.Enter();
                cleanup = preparation;
                Utf8RetainedPayloadExtent[] keyPayloads = [];
                if (IsPayloadBounded)
                {
                    using var builder = Utf8RetainedPayloadBuilder<TControl>.Create(_maximumPayloadBytes, control);
                    storage.CollectKey(key, builder);
                    keyPayloads = builder.Finish();
                    keyFits = !builder.Exceeded;
                }
                if (keyFits && CanAdd())
                {
                    entry = Acquire(key, default, null, 0, Environment.CurrentManagedThreadId, hash, keyPayloads);
                    control.Check();
                    if (_generation == generation && CanAdd() && Find(key, hash, control) is null &&
                        (!IsPayloadBounded || PublishPayloads(entry, null, keyPayloads, false, generation, control)))
                    { if (!IsPayloadBounded) Append(entry); }
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
                    if (_generation != generation || !keyFits) return Complete(preparation, value);
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
                    var payloads = CollectPayloads(key, value, storage, control, out var fits);
                    if (!fits) return Complete(preparation, value);
                    var completed = Acquire(key, new(value), null, 1, 0, hash, payloads);
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
                    if (IsPayloadBounded) _ = PublishPayloads(completed, null, payloads, true, generation, control);
                    else
                    {
                        if (victim is not null) Remove(victim);
                        Append(completed);
                    }
                }
                return Complete(preparation, value);
            }
            try
            {
                var value = PrepareUncached(key, state, factory, control);
                lock (this)
                {
                    if (IsPayloadBounded && Contains(entry))
                    {
                        var payloads = CollectPayloads(key, value, storage, control, out var fits);
                        if (!fits || !PublishPayloads(entry, entry, payloads, true, generation, control)) Remove(entry);
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

    private Utf8RetainedPayloadExtent[] CollectPayloads<TStorage, TControl>(TKey key, TValue value, TStorage storage,
        TControl control, out bool fits)
        where TControl : struct, IUtf8ConstructionTextControl
        where TStorage : struct, IUtf8RetainedStorage<TKey, TValue>
    {
        if (!IsPayloadBounded) { fits = true; return []; }
        using var builder = Utf8RetainedPayloadBuilder<TControl>.Create(_maximumPayloadBytes, control);
        storage.CollectKey(key, builder); storage.CollectValue(value, builder);
        var payloads = builder.Finish(); fits = !builder.Exceeded;
        return payloads;
    }

    private bool Contains(CacheEntry target)
    {
        for (var entry = _first; entry is not null; entry = entry.Next)
            if (ReferenceEquals(entry, target)) return true;
        return false;
    }

    // Stage a fresh funded union before any publication or eviction. A physical
    // table, every per-entry manifest and every unique leaf share the byte cap.
    // Reentrant controls can invalidate a stage; no staged state is committed
    // across Clear, shrink, or any other list revision.
    private bool PublishPayloads<TControl>(CacheEntry candidate, CacheEntry? replacing,
        Utf8RetainedPayloadExtent[] manifest, bool mayEvict, long generation, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        var ledger = _payloadLedger;
        if (ledger is null) throw new InvalidOperationException("Payload ownership is unavailable.");
        var victims = 0;
        while (true)
        {
            control.Check();
            if (_generation != generation) return false;
            var revision = _revision;
            if (replacing is not null && !Contains(replacing)) return false;
            using var union = Utf8ConstructionMap<Utf8RetainedPayloadIdentity, Utf8RetainedPayloadLedger.Reference>.Create(
                Utf8ConstructionScope.CurrentBudget, EqualityComparer<Utf8RetainedPayloadIdentity>.Default);
            var bytes = 0ul;
            var skipped = 0;
            var retained = 0;
            var ready = 0;
            var fits = true;
            for (var entry = _first; entry is not null; entry = entry.Next)
            {
                control.Check();
                if (ReferenceEquals(entry, replacing)) continue;
                if (entry.Status == 1)
                {
                    ready++;
                    if (skipped < victims) { skipped++; continue; }
                }
                retained++;
                if (fits) fits = Utf8RetainedPayloadLedger.AddManifest(union, entry.Payloads, _maximumPayloadBytes, ref bytes, control);
            }
            if (fits) fits = Utf8RetainedPayloadLedger.AddManifest(union, manifest, _maximumPayloadBytes, ref bytes, control);
            fits &= retained < _capacity && (ulong)(retained + 1) * EntryBytes <= _maximumMetadataBytes - BaseMetadataBytes;
            if (fits)
            {
                fits = union.StorageBytes <= _maximumPayloadBytes - bytes;
                control.Check();
                if (_generation != generation) return false;
                if (_revision != revision) { victims = 0; continue; }
                if (fits)
                {
                    // Nothing that can allocate, check cancellation or invoke
                    // caller code remains after the atomic publication point.
                    var entries = union.TakeEntries(out var count);
                    ledger.Publish(entries, count, bytes);
                    for (var entry = _first; entry is not null && victims > 0;)
                    {
                        var next = entry.Next;
                        if (!ReferenceEquals(entry, replacing) && entry.Status == 1) { Detach(entry); victims--; }
                        entry = next;
                    }
                    candidate.Payloads = manifest;
                    if (replacing is null) Append(candidate); else _revision++;
                    return true;
                }
            }
            control.Check();
            if (_generation != generation) return false;
            if (_revision != revision) { victims = 0; continue; }
            if (!mayEvict || victims >= ready) return false;
            victims++;
        }
    }

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
        Utf8RetainedPayloadExtent[] payloads)
    {
        using var owner = Utf8ConstructionScope.Enter();
        var result = owner.Budget.AllocateObject((key, value, error, Next: (CacheEntry?)null, status, ownerThread, hash, payloads),
            static state => new CacheEntry(state.key, state.value, state.error, state.status, state.ownerThread, state.hash, state.payloads));
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
        if (!Contains(target)) return;
        _payloadLedger?.Release(target.Payloads);
        Detach(target);
    }

    private void Detach(CacheEntry target)
    {
        CacheEntry? previous = null;
        for (var entry = _first; entry is not null; previous = entry, entry = entry.Next)
        {
            if (!ReferenceEquals(entry, target)) continue;
            if (previous is null) _first = entry.Next; else previous.Next = entry.Next;
            if (ReferenceEquals(_last, entry)) _last = previous;
            entry.Next = null; entry.Payloads = []; _count--; _revision++;
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
        Utf8RetainedPayloadExtent[] payloads)
    {
        internal readonly TKey Key = key;
        internal ValueTuple<TValue> Value = value;
        internal Exception? Error = error;
        internal CacheEntry? Next;
        internal int Status = status;
        internal int OwnerThread = ownerThread;
        internal readonly int Hash = hash;
        internal Utf8RetainedPayloadExtent[] Payloads = payloads;
    }
}
