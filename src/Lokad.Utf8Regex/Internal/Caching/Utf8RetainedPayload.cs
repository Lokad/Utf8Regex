using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

// PCRE2-INTEGRATION-POINT: common ownership of immutable prepared payloads.
// Collectors declare their scope explicitly. Complete-payload collectors cover
// immutable values. Root/descendant collectors reserve each bounded child's
// enforced maximum before publication, including future lazy growth. Neither
// scope may substitute transient construction reservations or reflection
// estimates. String/array loans include modeled headers and physical capacity.
internal enum Utf8RetainedStorageScope : byte
{
    Uncertified = 0,
    CompletePayload = 1,
    RootsAndBoundedDescendants = 2,
}

internal readonly record struct Utf8RetainedPayloadExtent(object Value, ulong Bytes);

internal readonly struct Utf8RetainedPayloadIdentity(object value) : IEquatable<Utf8RetainedPayloadIdentity>
{
    internal readonly object Value = value;
    public bool Equals(Utf8RetainedPayloadIdentity other) => ReferenceEquals(Value, other.Value);
    public override bool Equals(object? other) => other is Utf8RetainedPayloadIdentity identity && Equals(identity);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Value);
}

internal interface IUtf8RetainedStorage<TKey, TValue>
{
    bool IsCertified { get; }
    Utf8RetainedStorageScope Scope { get; }
    void CollectKey<TControl>(TKey key, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl;
    void CollectValue<TControl>(TValue value, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl;
}

internal readonly struct Utf8UncertifiedStorage<TKey, TValue> : IUtf8RetainedStorage<TKey, TValue>
{
    public bool IsCertified => false;
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.Uncertified;
    public void CollectKey<TControl>(TKey key, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl { }
    public void CollectValue<TControl>(TValue value, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl { }
}

internal sealed class Utf8RetainedPayloadBuilder<TControl> : IDisposable
    where TControl : struct, IUtf8ConstructionTextControl
{
    private readonly Utf8ConstructionBudget _budget;
    private readonly Utf8ConstructionMap<Utf8RetainedPayloadIdentity, ulong> _seen;
    private readonly Utf8ConstructionBuffer<Utf8RetainedPayloadExtent> _values;
    private readonly TControl _control;
    private readonly ulong _maximum;
    private ulong _bytes;
    internal bool Exceeded { get; private set; }

    private Utf8RetainedPayloadBuilder(Utf8ConstructionBudget budget,
        Utf8ConstructionMap<Utf8RetainedPayloadIdentity, ulong> seen,
        Utf8ConstructionBuffer<Utf8RetainedPayloadExtent> values, TControl control, ulong maximum)
    { _budget = budget; _seen = seen; _values = values; _control = control; _maximum = maximum; }

    internal static Utf8RetainedPayloadBuilder<TControl> Create(ulong maximum, TControl control)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        var seen = Utf8ConstructionMap<Utf8RetainedPayloadIdentity, ulong>.Create(budget, EqualityComparer<Utf8RetainedPayloadIdentity>.Default);
        var values = Utf8ConstructionBuffer<Utf8RetainedPayloadExtent>.Create(budget);
        return budget.AllocateObject((budget, seen, values, control, maximum, Bytes: 0ul, Exceeded: false),
            static state => new Utf8RetainedPayloadBuilder<TControl>(state.budget, state.seen, state.values, state.control, state.maximum));
    }

    internal void Check() { _control.Check(); _budget.ChargeWork(1); }
    internal void Text(string? value) { if (value is not null) Add(value, Utf8ConstructionBudget.StringBytes(value.Length)); }
    internal void Array<T>(T[]? value) { if (value is not null) Add(value, Utf8ConstructionBudget.ArrayBytes<T>(value.Length)); }
    internal void Object<TState>(object value) => Add(value, Utf8ConstructionBudget.ObjectBytes<TState>());

    internal void Add(object value, ulong bytes) => TryAdd(value, bytes);

    // Graph walkers expand each identity once, including shared assertion bodies.
    // An inconsistent extent still fails even when that identity was seen before.
    internal bool TryAdd(object value, ulong bytes)
    {
        Check();
        if (Exceeded || bytes == 0) return false;
        var identity = new Utf8RetainedPayloadIdentity(value);
        if (_seen.TryGetValue(identity, out var previous))
        {
            if (previous != bytes) throw new InvalidOperationException("One retained object has inconsistent physical extents.");
            return false;
        }
        if (bytes > _maximum - _bytes) { Exceeded = true; return false; }
        _seen.Add(identity, bytes);
        _values.Add(new(value, bytes));
        _bytes += bytes;
        return true;
    }

    internal Utf8RetainedPayloadExtent[] Finish()
    {
        Check();
        if (Exceeded) return [];
        var descriptor = Utf8ConstructionBudget.ArrayBytes<Utf8RetainedPayloadExtent>(_values.Count);
        if (descriptor > _maximum - _bytes) { Exceeded = true; return []; }
        var result = _budget.AllocateArray<Utf8RetainedPayloadExtent>(_values.Count, false);
        _budget.ChargeWork((ulong)_values.Count);
        _values.Span.CopyTo(result);
        return result;
    }

    public void Dispose() { _seen.Dispose(); _values.Dispose(); }
}

internal sealed class Utf8RetainedPayloadLedger
{
    internal readonly record struct Reference(ulong Bytes, int Count);
    private Utf8ConstructionMap<Utf8RetainedPayloadIdentity, Reference>.Entry[] _entries = [];
    private int _count;
    private ulong _leafBytes;
    internal static ulong ObjectBytes => Utf8ConstructionBudget.ObjectBytes<
        (Utf8ConstructionMap<Utf8RetainedPayloadIdentity, Reference>.Entry[], int, ulong)>();
    internal ulong Bytes => _leafBytes + Utf8ConstructionBudget.ArrayBytes<Utf8ConstructionMap<Utf8RetainedPayloadIdentity, Reference>.Entry>(_entries.Length);

    internal static Utf8RetainedPayloadLedger Create() => Utf8ConstructionScope.CurrentBudget.AllocateObject(
        (Array.Empty<Utf8ConstructionMap<Utf8RetainedPayloadIdentity, Reference>.Entry>(), 0, 0ul),
        static _ => new Utf8RetainedPayloadLedger());

    internal void Publish(Utf8ConstructionMap<Utf8RetainedPayloadIdentity, Reference>.Entry[] entries, int count, ulong leafBytes)
    { _entries = entries; _count = count; _leafBytes = leafBytes; }

    // Cleanup cannot acquire storage or fail a borrowed cancellation check.
    // Tombstones keep probe chains intact without retaining the released object.
    internal void Release(Utf8RetainedPayloadExtent[] manifest)
    {
        foreach (var extent in manifest) Release(extent.Value);
        if (manifest.Length > 0) Release((object)manifest);
        if (_count == 0) { _entries = []; _leafBytes = 0; }
    }

    private void Release(object value)
    {
        var identity = new Utf8RetainedPayloadIdentity(value);
        var hash = identity.GetHashCode() & int.MaxValue;
        var index = hash & (_entries.Length - 1);
        while (_entries[index].Used)
        {
            var entry = _entries[index];
            if (entry.Value.Count > 0 && entry.Hash == hash && entry.Key.Equals(identity))
            {
                if (entry.Value.Count == 1)
                {
                    _leafBytes -= entry.Value.Bytes; _count--;
                    _entries[index] = new(default, default, hash, true);
                }
                else _entries[index] = entry with { Value = entry.Value with { Count = entry.Value.Count - 1 } };
                return;
            }
            index = (index + 1) & (_entries.Length - 1);
        }
        throw new InvalidOperationException("A retained payload was released without its ownership reference.");
    }

    internal static bool AddManifest<TControl>(Utf8ConstructionMap<Utf8RetainedPayloadIdentity, Reference> entries,
        Utf8RetainedPayloadExtent[] manifest, ulong maximum, ref ulong bytes, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (manifest.Length > 0 && !Add(entries, manifest, Utf8ConstructionBudget.ArrayBytes<Utf8RetainedPayloadExtent>(manifest.Length), maximum, ref bytes, control)) return false;
        foreach (var extent in manifest)
            if (!Add(entries, extent.Value, extent.Bytes, maximum, ref bytes, control)) return false;
        return true;
    }

    private static bool Add<TControl>(Utf8ConstructionMap<Utf8RetainedPayloadIdentity, Reference> entries,
        object value, ulong extent, ulong maximum, ref ulong bytes, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        control.Check();
        Utf8ConstructionScope.ChargeWork(1);
        var key = new Utf8RetainedPayloadIdentity(value);
        if (entries.TryGetValue(key, out var existing))
        {
            if (existing.Bytes != extent) throw new InvalidOperationException("One retained object has inconsistent physical extents.");
            entries[key] = existing with { Count = checked(existing.Count + 1) };
            return true;
        }
        if (extent > maximum - bytes) return false;
        entries.Add(key, new(extent, 1)); bytes += extent;
        return true;
    }
}
