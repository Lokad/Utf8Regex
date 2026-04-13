using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8RegexCountBenchmarks
{
    private Utf8RegexBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8RegexBenchmarkCatalog.GetIds(Utf8RegexBenchmarkOperation.Count);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8RegexBenchmarkContext(Utf8RegexBenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Regex()
    {
        return _context.Utf8Regex.Count(_context.InputBytes);
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        return _context.Regex.Count(System.Text.Encoding.UTF8.GetString(_context.InputBytes));
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        return _context.Regex.Count(_context.InputString);
    }
}
