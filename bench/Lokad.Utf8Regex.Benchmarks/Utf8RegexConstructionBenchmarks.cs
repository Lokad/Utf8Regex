using BenchmarkDotNet.Attributes;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public sealed class Utf8RegexConstructionBenchmarks
{
    [Params(
        "needle",
        "ab[0-9]d",
        "cat|horse",
        @"\d{2,4}",
        "(ab)+")]
    public string Pattern { get; set; } = null!;

    [Params(
        RegexOptions.CultureInvariant,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    public RegexOptions Options { get; set; }

    [Benchmark(Baseline = true)]
    public Utf8Regex ConstructUtf8Regex()
    {
        return new Utf8Regex(Pattern, Options);
    }

    [Benchmark]
    public Regex ConstructRegex()
    {
        return new Regex(Pattern, Options, Regex.InfiniteMatchTimeout);
    }
}
