using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8Pcre2ReplaceBenchmarks
{
    private Utf8Pcre2BenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8Pcre2BenchmarkCatalog.GetIds(Utf8Pcre2BenchmarkOperation.Replace, Utf8Pcre2BenchmarkBackend.AllManagedComparisons);

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

    [Benchmark]
    public byte[] Utf8Regex()
    {
        return _context.Utf8Regex!.Replace(_context.InputBytes, _context.Replacement);
    }

    [Benchmark]
    public string DecodeThenRegex()
    {
        return _context.Regex!.Replace(System.Text.Encoding.UTF8.GetString(_context.InputBytes), _context.Replacement);
    }

    [Benchmark]
    public string PredecodedRegex()
    {
        return _context.Regex!.Replace(_context.InputString, _context.Replacement);
    }
}
