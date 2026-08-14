using System.Collections.Concurrent;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal sealed class Utf8ReplacementPlanCache
{
    internal const int Capacity = 16;

    private readonly ConcurrentDictionary<string, Lazy<Utf8AnalyzedReplacement>> _plans = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> _insertionOrder = new();

    internal int Count => _plans.Count;

    internal Utf8AnalyzedReplacement GetOrAdd<TState>(
        string replacement,
        TState state,
        Func<string, TState, Utf8AnalyzedReplacement> factory)
    {
        while (true)
        {
            if (_plans.TryGetValue(replacement, out var existing))
            {
                return GetPreparedValue(replacement, existing);
            }

            var created = new Lazy<Utf8AnalyzedReplacement>(
                () => factory(replacement, state),
                LazyThreadSafetyMode.ExecutionAndPublication);
            if (_plans.TryAdd(replacement, created))
            {
                _insertionOrder.Enqueue(replacement);
                TrimToCapacity();
                return GetPreparedValue(replacement, created);
            }
        }
    }

    private Utf8AnalyzedReplacement GetPreparedValue(
        string replacement,
        Lazy<Utf8AnalyzedReplacement> preparation)
    {
        try
        {
            return preparation.Value;
        }
        catch
        {
            _plans.TryRemove(
                new KeyValuePair<string, Lazy<Utf8AnalyzedReplacement>>(replacement, preparation));
            throw;
        }
    }

    private void TrimToCapacity()
    {
        while (_plans.Count > Capacity && _insertionOrder.TryDequeue(out var replacement))
        {
            _plans.TryRemove(replacement, out _);
        }
    }
}
