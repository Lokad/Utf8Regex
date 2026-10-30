using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal sealed class Utf8ReplacementPlanCache
{
    internal const int Capacity = 16;

    private readonly Utf8BoundedPreparationCache<object, Utf8AnalyzedReplacement> _plans;

    private Utf8ReplacementPlanCache(Utf8BoundedPreparationCache<object, Utf8AnalyzedReplacement> plans) => _plans = plans;

    internal static Utf8ReplacementPlanCache Create()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var plans = Utf8BoundedPreparationCache<object, Utf8AnalyzedReplacement>.CreateRetained(Capacity, Utf8ReplacementKeyComparer.Instance);
        var result = owner.Budget.AllocateObject(plans, static value => new Utf8ReplacementPlanCache(value));
        owner.Complete();
        return result;
    }

    internal int Count => _plans.Count;
    internal ulong PayloadBytes => _plans.PayloadBytes;

    // Reserve the wrapper and the child's enforced metadata/payload maxima.
    // This immutable allowance covers every later lazy publication. It is a
    // conservative owner loan, not a snapshot of the child's current leaves.
    internal ulong MaximumRetainedBytes => Utf8ConstructionBudget.ObjectBytes<
        Utf8BoundedPreparationCache<object, Utf8AnalyzedReplacement>>()
        + _plans.MaximumMetadataBytes + _plans.MaximumPayloadBytes;

    internal Utf8AnalyzedReplacement GetOrAdd<TState>(
        object replacement,
        TState state,
        Func<object, TState, Utf8AnalyzedReplacement> factory)
        => _plans.GetOrAdd(replacement, state, factory, default(Utf8ConstructionTextNoControl), default(Utf8ReplacementRetainedStorage));

    internal Utf8AnalyzedReplacement GetOrAdd<TState, TControl>(object replacement, TState state,
        Func<object, TState, Utf8AnalyzedReplacement> factory, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
        => _plans.GetOrAdd(replacement, state, factory, control, default(Utf8ReplacementRetainedStorage));

    internal bool TryGet<TControl>(ReadOnlySpan<byte> template, TControl control, out Utf8AnalyzedReplacement value)
        where TControl : struct, IUtf8ConstructionTextControl
        => _plans.TryGet(template, control,
            static (key, query, current) => key is byte[] bytes && EqualsNative(bytes, query, current),
            control, out value, out _);

    private static bool EqualsNative<TControl>(ReadOnlySpan<byte> owned, ReadOnlySpan<byte> query, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
    {
        if (owned.Length != query.Length) return false;
        for (var offset = 0; offset < query.Length;)
        {
            control.Check();
            var count = Math.Min(2048, query.Length - offset);
            if (!owned.Slice(offset, count).SequenceEqual(query.Slice(offset, count))) return false;
            offset += count;
        }
        control.Check(); return true;
    }
}

// A native source owns byte storage; legacy text keeps its original key. One
// bounded cache intentionally charges independent representations separately.
internal sealed class Utf8ReplacementKeyComparer : IEqualityComparer<object>
{
    internal static readonly Utf8ReplacementKeyComparer Instance = new();
    public new bool Equals(object? left, object? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is string text && right is string otherText) return StringComparer.Ordinal.Equals(text, otherText);
        if (left is not byte[] bytes || right is not byte[] other || bytes.Length != other.Length) return false;
        for (var offset = 0; offset < bytes.Length;)
        {
            var count = Math.Min(2048, bytes.Length - offset);
            Utf8ConstructionScope.ChargeWork((ulong)count);
            if (!bytes.AsSpan(offset, count).SequenceEqual(other.AsSpan(offset, count))) return false;
            offset += count;
        }
        return true;
    }
    public int GetHashCode(object key)
    {
        if (key is string text) return StringComparer.Ordinal.GetHashCode(text);
        var hash = new HashCode(); var bytes = (byte[])key;
        for (var offset = 0; offset < bytes.Length;)
        {
            var count = Math.Min(2048, bytes.Length - offset);
            Utf8ConstructionScope.ChargeWork((ulong)count);
            hash.AddBytes(bytes.AsSpan(offset, count)); offset += count;
        }
        return hash.ToHashCode();
    }
}
