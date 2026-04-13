namespace Lokad.Utf8Regex.Benchmarks;

[Flags]
internal enum Utf8Pcre2BenchmarkBackend
{
    None = 0,
    Utf8Pcre2Regex = 1 << 0,
    Utf8Regex = 1 << 1,
    DecodeThenRegex = 1 << 2,
    PredecodedRegex = 1 << 3,
    AllManagedComparisons = Utf8Pcre2Regex | Utf8Regex | DecodeThenRegex | PredecodedRegex,
    Pcre2Only = Utf8Pcre2Regex,
    All = AllManagedComparisons,
}
