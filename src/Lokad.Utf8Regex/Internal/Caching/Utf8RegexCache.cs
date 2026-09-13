using Lokad.Utf8Regex.Internal.FrontEnd;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

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
        // Mirror System.Text.RegularExpressions.RegexCache (PLAN.md C02): the
        // effective construction culture is part of the cache identity, so a
        // culture-sensitive instance is never reused under another culture.
        // Invariant requests keep sharing one entry: their culture is constant.
        var culture = RuntimeFrontEnd.RegexParser.GetTargetCulture(normalizedOptions).ToString();
        // PLAN.md D07: Compiled is execution-mode identity, not a shareable semantic option. The prepared program is identical for both modes, but engine selection differs, so each mode keeps its own cache entry.
        var keyOptions = normalizedOptions | (options & RegexOptions.Compiled);
        var key = new Utf8RegexCacheKey(pattern, culture, keyOptions, matchTimeout);
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
