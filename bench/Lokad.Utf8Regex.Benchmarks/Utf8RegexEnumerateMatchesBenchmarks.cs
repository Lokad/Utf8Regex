using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8RegexEnumerateMatchesBenchmarks
{
    private Utf8RegexBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8RegexBenchmarkCatalog.GetIds(Utf8RegexBenchmarkOperation.EnumerateMatches);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8RegexBenchmarkContext(Utf8RegexBenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Regex()
    {
        var sum = 0;
        foreach (var match in _context.Utf8Regex.EnumerateMatches(_context.InputBytes))
        {
            sum += match.IndexInUtf16;
        }

        return sum;
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        var sum = 0;
        foreach (var match in _context.Regex.EnumerateMatches(System.Text.Encoding.UTF8.GetString(_context.InputBytes)))
        {
            sum += match.Index;
        }

        return sum;
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        var sum = 0;
        foreach (var match in _context.Regex.EnumerateMatches(_context.InputString))
        {
            sum += match.Index;
        }

        return sum;
    }
}
