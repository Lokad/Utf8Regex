using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

public class Utf8Pcre2SpecialReplaceBenchmarks
{
    private Utf8Pcre2BenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8Pcre2BenchmarkCatalog.GetIds(Utf8Pcre2BenchmarkOperation.Replace, Utf8Pcre2BenchmarkBackend.Pcre2Only)
        .Where(static id => Utf8Pcre2BenchmarkCatalog.Get(id).SupportedBackends == Utf8Pcre2BenchmarkBackend.Pcre2Only);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8Pcre2BenchmarkContext(Utf8Pcre2BenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public byte[] Utf8Pcre2Regex()
    {
        return _context.Utf8Pcre2Regex.Replace(_context.InputBytes, _context.Replacement);
    }
}
