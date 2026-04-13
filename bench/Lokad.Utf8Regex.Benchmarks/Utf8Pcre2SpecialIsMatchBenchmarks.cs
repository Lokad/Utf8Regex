using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8Pcre2SpecialIsMatchBenchmarks
{
    private Utf8Pcre2BenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8Pcre2BenchmarkCatalog.GetIds(Utf8Pcre2BenchmarkOperation.IsMatch, Utf8Pcre2BenchmarkBackend.Pcre2Only)
        .Where(static id => Utf8Pcre2BenchmarkCatalog.Get(id).SupportedBackends == Utf8Pcre2BenchmarkBackend.Pcre2Only);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8Pcre2BenchmarkContext(Utf8Pcre2BenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Pcre2Regex()
    {
        return _context.Utf8Pcre2Regex.IsMatch(_context.InputBytes) ? 1 : 0;
    }
}
