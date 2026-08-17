using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8RegexReplaceBenchmarks
{
    private Utf8RegexBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8RegexBenchmarkCatalog.GetIds(Utf8RegexBenchmarkOperation.Replace);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8RegexBenchmarkContext(Utf8RegexBenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Regex()
    {
        return _context.Utf8Regex.Replace(_context.InputBytes, _context.ReplacementUtf8).Length;
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        var decoded = System.Text.Encoding.UTF8.GetString(_context.InputBytes);
        return RegexBenchmarkResult.ReplaceUtf8Length(_context.Regex, decoded, _context.Replacement);
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        return RegexBenchmarkResult.ReplaceUtf8Length(_context.Regex, _context.InputString, _context.Replacement);
    }
}
