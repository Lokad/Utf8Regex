using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Caching;

internal static class Utf8RegexCache
{
    // Retained keys, facade metadata, scalar/prepared graphs and bounded replacement descendants.
    // This internal migration backstop is not a complete program/process cap
    // or a qualified public safe default.
    internal const ulong MaximumRootAndLazyBytes = 64 * 1024 * 1024;
    private static Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex>? s_cache;

    // Failed admission must not poison a static type initializer. Construction
    // joins the caller's owner; only the successfully acquired winner is shared.
    private static Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex> Cache
        => Volatile.Read(ref s_cache) ?? Initialize();

    private static Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex> Initialize()
    {
        var created = Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex>.CreateRootAndDescendantRetained(
            15, EqualityComparer<Utf8RegexCacheKey>.Default,
            Utf8BoundedPreparationCache<Utf8RegexCacheKey, Utf8Regex>.DefaultMaximumMetadataBytes, MaximumRootAndLazyBytes);
        return Interlocked.CompareExchange(ref s_cache, created, null) ?? created;
    }

    public static Utf8Regex GetOrAdd(string pattern, RegexOptions options)
    {
        return GetOrAdd(pattern, options, Utf8Regex.DefaultMatchTimeout);
    }

    public static Utf8Regex GetOrAdd(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        if (pattern is null) throw new ArgumentNullException(nameof(pattern));
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
            key, 0,
            static (cacheKey, _) => Utf8Regex.CreateAdmittedForCache(cacheKey.Pattern, cacheKey.Options, cacheKey.MatchTimeout),
            default(Utf8ConstructionTextNoControl), default(Utf8RegexRootAndLazyStorage));
    }

    public static int EntryCount => Cache.Count;

    internal static ulong RootAndLazyBytes => Cache.PayloadBytes;
    internal static ulong MetadataBytes => Cache.MetadataBytes;
    internal static Utf8RetainedStorageScope StorageScope => Cache.StorageScope;

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
