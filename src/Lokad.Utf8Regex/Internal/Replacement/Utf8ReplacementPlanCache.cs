using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal sealed class Utf8ReplacementPlanCache
{
    internal const int Capacity = 16;

    private readonly Utf8BoundedPreparationCache<string, Utf8AnalyzedReplacement> _plans;

    private Utf8ReplacementPlanCache(Utf8BoundedPreparationCache<string, Utf8AnalyzedReplacement> plans) => _plans = plans;

    internal static Utf8ReplacementPlanCache Create()
    {
        using var owner = Utf8ConstructionScope.Enter();
        var plans = Utf8BoundedPreparationCache<string, Utf8AnalyzedReplacement>.CreateRetained(Capacity, StringComparer.Ordinal);
        var result = owner.Budget.AllocateObject(plans, static value => new Utf8ReplacementPlanCache(value));
        owner.Complete();
        return result;
    }

    internal int Count => _plans.Count;
    internal ulong PayloadBytes => _plans.PayloadBytes;
    internal ulong MaximumPayloadBytes => _plans.MaximumPayloadBytes;

    // Reserve the wrapper and the child's enforced metadata/payload maxima.
    // This immutable allowance covers every later lazy publication. It is a
    // conservative owner loan, not a snapshot of the child's current leaves.
    internal ulong MaximumRetainedBytes => Utf8ConstructionBudget.ObjectBytes<
        Utf8BoundedPreparationCache<string, Utf8AnalyzedReplacement>>()
        + _plans.MaximumMetadataBytes + _plans.MaximumPayloadBytes;

    internal Utf8AnalyzedReplacement GetOrAdd<TState>(
        string replacement,
        TState state,
        Func<string, TState, Utf8AnalyzedReplacement> factory)
        => _plans.GetOrAdd(replacement, state, factory, default(Utf8ConstructionTextNoControl), default(Utf8ReplacementRetainedStorage));

    internal Utf8AnalyzedReplacement GetOrAdd<TState, TControl>(string replacement, TState state,
        Func<string, TState, Utf8AnalyzedReplacement> factory, TControl control)
        where TControl : struct, IUtf8ConstructionTextControl
        => _plans.GetOrAdd(replacement, state, factory, control, default(Utf8ReplacementRetainedStorage));
}
