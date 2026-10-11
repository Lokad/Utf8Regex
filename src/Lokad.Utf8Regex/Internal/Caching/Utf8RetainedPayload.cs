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
    // Keys have closed, length-based storage. Charge them independently of the
    // value graph; shared text may be counted again without an identity map.
    ulong GetKeyCharge(TKey key);
    void CollectValue<TControl>(TValue value, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl;
}

internal readonly struct Utf8UncertifiedStorage<TKey, TValue> : IUtf8RetainedStorage<TKey, TValue>
{
    public bool IsCertified => false;
    public Utf8RetainedStorageScope Scope => Utf8RetainedStorageScope.Uncertified;
    public ulong GetKeyCharge(TKey key) => 0;
    public void CollectValue<TControl>(TValue value, Utf8RetainedPayloadBuilder<TControl> storage)
        where TControl : struct, IUtf8ConstructionTextControl { }
}

internal sealed class Utf8RetainedPayloadBuilder<TControl> : IDisposable
    where TControl : struct, IUtf8ConstructionTextControl
{
    private readonly Utf8ConstructionBudget _budget;
    private readonly Utf8ConstructionMap<Utf8RetainedPayloadIdentity, ulong> _seen;
    private readonly TControl _control;
    private readonly ulong _maximum;
    private ulong _bytes;
    internal bool Exceeded { get; private set; }

    private Utf8RetainedPayloadBuilder(Utf8ConstructionBudget budget,
        Utf8ConstructionMap<Utf8RetainedPayloadIdentity, ulong> seen,
        TControl control, ulong maximum)
    { _budget = budget; _seen = seen; _control = control; _maximum = maximum; }

    internal static Utf8RetainedPayloadBuilder<TControl> Create(ulong maximum, TControl control)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        var seen = Utf8ConstructionMap<Utf8RetainedPayloadIdentity, ulong>.Create(budget, EqualityComparer<Utf8RetainedPayloadIdentity>.Default);
        return budget.AllocateObject((budget, seen, control, maximum, Bytes: 0ul, Exceeded: false),
            static state => new Utf8RetainedPayloadBuilder<TControl>(state.budget, state.seen, state.control, state.maximum));
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
        _bytes += bytes;
        return true;
    }

    // Cache entries retain this conservative scalar charge, never the manifest.
    internal ulong FinishCharge() { Check(); return _bytes; }

    public void Dispose() => _seen.Dispose();
}
