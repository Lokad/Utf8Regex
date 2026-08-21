using Lokad.Utf8Regex.Internal.Caching;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal sealed class Utf8ReplacementPlanCache
{
    internal const int Capacity = 16;

    private readonly Utf8BoundedPreparationCache<string, Utf8AnalyzedReplacement> _plans = new(Capacity, StringComparer.Ordinal);

    internal int Count => _plans.Count;

    internal Utf8AnalyzedReplacement GetOrAdd<TState>(
        string replacement,
        TState state,
        Func<string, TState, Utf8AnalyzedReplacement> factory)
        => _plans.GetOrAdd(replacement, state, factory);
}
