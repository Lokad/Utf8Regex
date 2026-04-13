using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8Pcre2IsMatchBenchmarks
{
    private Utf8Pcre2BenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8Pcre2BenchmarkCatalog.GetIds(Utf8Pcre2BenchmarkOperation.IsMatch, Utf8Pcre2BenchmarkBackend.AllManagedComparisons);

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

    [Benchmark]
    public int Utf8Regex()
    {
        return _context.Utf8Regex!.IsMatch(_context.InputBytes) ? 1 : 0;
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        return _context.Regex!.IsMatch(System.Text.Encoding.UTF8.GetString(_context.InputBytes)) ? 1 : 0;
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        return _context.Regex!.IsMatch(_context.InputString) ? 1 : 0;
    }
}
