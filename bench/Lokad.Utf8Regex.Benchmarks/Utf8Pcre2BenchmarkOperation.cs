namespace Lokad.Utf8Regex.Benchmarks;

[Flags]
internal enum Utf8Pcre2BenchmarkOperation
{
    None = 0,
    IsMatch = 1 << 0,
    Count = 1 << 1,
    EnumerateMatches = 1 << 2,
    MatchMany = 1 << 3,
    Replace = 1 << 4,
    All = IsMatch | Count | EnumerateMatches | MatchMany | Replace,
}
