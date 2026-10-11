using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

internal static class Utf8RegexCache
{
    private static Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex>? s_cache;

    // Failed admission must not poison a static type initializer. Construction
    // joins the caller's owner; only the successfully acquired winner is shared.
    private static Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex> Cache
        => Volatile.Read(ref s_cache) ?? Initialize();

    private static Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex> Initialize()
    {
        var created = Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex>.Create(15);
        return Interlocked.CompareExchange(ref s_cache, created, null) ?? created;
    }

    public static Utf8Regex GetOrAdd(string pattern, RegexOptions options)
    {
        return GetOrAdd(pattern, options, Utf8Regex.DefaultMatchTimeout);
    }

    public static Utf8Regex GetOrAdd(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        var normalizedOptions = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);
        // Mirror System.Text.RegularExpressions.RegexCache (PLAN.md C02): the
        // effective construction culture is part of the cache identity, so a
        // culture-sensitive instance is never reused under another culture.
        // Invariant requests keep sharing one entry: their culture is constant.
        var culture = Utf8OwnedOptionPrelude.GetTargetCulture(normalizedOptions).ToString();
        // PLAN.md D07: Compiled is execution-mode identity, not a shareable semantic option. The prepared program is identical for both modes, but engine selection differs, so each mode keeps its own cache entry.
        var keyOptions = normalizedOptions | (options & RegexOptions.Compiled);
        var key = new Utf8RegexCacheKey(pattern, culture, keyOptions, matchTimeout);
        return Cache.GetOrAdd(
            key,
            static cacheKey => Utf8Regex.CreateAdmittedForCache(cacheKey.Pattern, cacheKey.Options, cacheKey.MatchTimeout));
    }

    public static int EntryCount => Cache.Count;

    public static int MaxEntries
    {
        get => Cache.Capacity;
        set => Cache.Capacity = value;
    }

    internal static void ResetForTests()
    {
        Cache.Clear();
        Cache.Capacity = 15;
    }
}
