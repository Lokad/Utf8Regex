using System.Collections.Concurrent;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

internal static class Utf8RegexCache
{
    private static readonly ConcurrentDictionary<Utf8RegexCacheKey, Utf8Regex> s_cache = new();
    private static readonly ConcurrentQueue<Utf8RegexCacheKey> s_insertionOrder = new();
    private static int s_maxEntries = 15;

    public static Utf8Regex GetOrAdd(string pattern, RegexOptions options)
    {
        return GetOrAdd(pattern, options, Utf8Regex.DefaultMatchTimeout);
    }

    public static Utf8Regex GetOrAdd(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        if (s_maxEntries == 0)
        {
            return new Utf8Regex(pattern, options, matchTimeout);
        }

        var normalizedOptions = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        var key = new Utf8RegexCacheKey(pattern, normalizedOptions, matchTimeout);
        if (s_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var created = new Utf8Regex(key.Pattern, key.Options, key.MatchTimeout);
        if (s_cache.TryAdd(key, created))
        {
            s_insertionOrder.Enqueue(key);
            TrimToCapacity();
            return created;
        }

        return s_cache[key];
    }

    public static int EntryCount => s_cache.Count;

    public static int MaxEntries
    {
        get => s_maxEntries;
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            s_maxEntries = value;
            TrimToCapacity();
        }
    }

    internal static void ResetForTests()
    {
        s_cache.Clear();
        while (s_insertionOrder.TryDequeue(out _))
        {
        }

        s_maxEntries = 15;
    }

    private static void TrimToCapacity()
    {
        while (s_cache.Count > s_maxEntries && s_insertionOrder.TryDequeue(out var key))
        {
            s_cache.TryRemove(key, out _);
        }
    }
}
