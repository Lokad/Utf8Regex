using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Tests;

// Test-only identity inspection for independent physical/cycle/alias witnesses.
internal readonly struct Utf8GraphIdentity(object value) : IEquatable<Utf8GraphIdentity>
{
    internal readonly object Value = value;
    public bool Equals(Utf8GraphIdentity other) => ReferenceEquals(Value, other.Value);
    public override bool Equals(object? other) => other is Utf8GraphIdentity identity && Equals(identity);
    public override int GetHashCode() => RuntimeHelpers.GetHashCode(Value);
}

internal sealed class Utf8GraphInspection<TControl> : IDisposable
    where TControl : struct, IUtf8ConstructionTextControl
{
    private readonly Utf8ConstructionBudget _budget;
    private readonly Utf8ConstructionMap<Utf8GraphIdentity, ulong> _seen;
    private readonly TControl _control;
    private readonly ulong _maximum;
    private ulong _bytes;
    internal bool Exceeded { get; private set; }

    private Utf8GraphInspection(Utf8ConstructionBudget budget,
        Utf8ConstructionMap<Utf8GraphIdentity, ulong> seen,
        TControl control, ulong maximum)
    { _budget = budget; _seen = seen; _control = control; _maximum = maximum; }

    internal static Utf8GraphInspection<TControl> Create(ulong maximum, TControl control)
    {
        var budget = Utf8ConstructionScope.CurrentBudget;
        var seen = Utf8ConstructionMap<Utf8GraphIdentity, ulong>.Create(budget, EqualityComparer<Utf8GraphIdentity>.Default);
        return budget.AllocateObject((budget, seen, control, maximum, Bytes: 0ul, Exceeded: false),
            static state => new Utf8GraphInspection<TControl>(state.budget, state.seen, state.control, state.maximum));
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
        var identity = new Utf8GraphIdentity(value);
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
