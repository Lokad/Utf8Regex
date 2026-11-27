using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal sealed class Utf8ReplacementPlanCache
{
    internal const int Capacity = 16;

    private readonly Utf8BoundedPreparationCache<byte[], Utf8AnalyzedReplacement> _plans;

    private Utf8ReplacementPlanCache(Utf8BoundedPreparationCache<byte[], Utf8AnalyzedReplacement> plans) => _plans = plans;

    internal static Utf8ReplacementPlanCache Create(Utf8ConstructionBudget budget)
    {
        var plans = Utf8BoundedPreparationCache<byte[], Utf8AnalyzedReplacement>.CreateRetained(Capacity, Utf8ReplacementKeyComparer.Instance);
        return budget.AllocateObject(plans, static value => new Utf8ReplacementPlanCache(value));
    }

    internal int Count => _plans.Count;
    internal ulong PayloadBytes => _plans.PayloadBytes;
    internal ulong MaximumRetainedBytes => _plans.MaximumMetadataBytes + _plans.MaximumPayloadBytes;

    internal Utf8AnalyzedReplacement GetOrAdd<TState, TControl>(byte[] replacement, TState state,
        Func<byte[], TState, Utf8AnalyzedReplacement> factory, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
        => _plans.GetOrAdd(replacement, state, factory, control, default(Utf8ReplacementRetainedStorage));

    internal bool TryGet<TControl>(ReadOnlySpan<byte> template, TControl control, out Utf8AnalyzedReplacement value)
        where TControl : struct, IUtf8ConstructionTextControl
        => _plans.TryGet(template, control,
            static (key, query, current) => EqualsNative(key, query, current),
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

internal sealed class Utf8ReplacementKeyComparer : IEqualityComparer<byte[]>
{
    internal static readonly Utf8ReplacementKeyComparer Instance = new();
    public bool Equals(byte[]? left, byte[]? right)
    {
        if (ReferenceEquals(left, right)) return true;
        if (left is null || right is null || left.Length != right.Length) return false;
        for (var offset = 0; offset < left.Length;)
        {
            var count = Math.Min(2048, left.Length - offset);
            Utf8ConstructionScope.ChargeWork((ulong)count);
            if (!left.AsSpan(offset, count).SequenceEqual(right.AsSpan(offset, count))) return false;
            offset += count;
        }
        return true;
    }
    public int GetHashCode(byte[] bytes)
    {
        var hash = new HashCode();
        for (var offset = 0; offset < bytes.Length;)
        {
            var count = Math.Min(2048, bytes.Length - offset);
            Utf8ConstructionScope.ChargeWork((ulong)count);
            hash.AddBytes(bytes.AsSpan(offset, count)); offset += count;
        }
        return hash.ToHashCode();
    }
}
