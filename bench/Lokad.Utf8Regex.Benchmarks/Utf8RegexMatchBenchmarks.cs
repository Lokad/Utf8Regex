using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8RegexMatchBenchmarks
{
    private Utf8RegexBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8RegexBenchmarkCatalog.GetIds(Utf8RegexBenchmarkOperation.Match);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8RegexBenchmarkContext(Utf8RegexBenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Regex()
    {
        var match = _context.Utf8Regex.Match(_context.InputBytes);
        return match.Success ? match.IndexInUtf16 + match.LengthInUtf16 : -1;
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        var match = _context.Regex.Match(System.Text.Encoding.UTF8.GetString(_context.InputBytes));
        return match.Success ? match.Index + match.Length : -1;
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        var match = _context.Regex.Match(_context.InputString);
        return match.Success ? match.Index + match.Length : -1;
    }
}
