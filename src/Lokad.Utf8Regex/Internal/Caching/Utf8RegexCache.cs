using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

internal static class Utf8RegexCache
{
    private static readonly Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex> s_cache = new(15);

    public static Utf8Regex GetOrAdd(string pattern, RegexOptions options)
    {
        return GetOrAdd(pattern, options, Utf8Regex.DefaultMatchTimeout);
    }

    public static Utf8Regex GetOrAdd(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        var normalizedOptions = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        var key = new Utf8RegexCacheKey(pattern, normalizedOptions, matchTimeout);
        return s_cache.GetOrAdd(
            key,
            static cacheKey => new Utf8Regex(cacheKey.Pattern, cacheKey.Options, cacheKey.MatchTimeout));
    }

    public static int EntryCount => s_cache.Count;

    public static int MaxEntries
    {
        get => s_cache.Capacity;
        set => s_cache.Capacity = value;
    }

    internal static void ResetForTests()
    {
        s_cache.Clear();
        s_cache.Capacity = 15;
    }
}
