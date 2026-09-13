namespace Lokad.Utf8Regex.Internal.Caching;

internal readonly record struct Utf8RegexCacheKey(
    string Pattern,
    string Culture,
    RegexOptions Options,
    TimeSpan MatchTimeout);
