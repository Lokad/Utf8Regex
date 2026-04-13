using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8Pcre2EnumerateBenchmarks
{
    private Utf8Pcre2BenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8Pcre2BenchmarkCatalog.GetIds(Utf8Pcre2BenchmarkOperation.EnumerateMatches, Utf8Pcre2BenchmarkBackend.AllManagedComparisons);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8Pcre2BenchmarkContext(Utf8Pcre2BenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Pcre2Regex()
    {
        var sum = 0;
        var enumerator = _context.Utf8Pcre2Regex.EnumerateMatches(_context.InputBytes);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.StartOffsetInBytes;
        }

        return sum;
    }

    [Benchmark]
    public int Utf8Regex()
    {
        var sum = 0;
        var enumerator = _context.Utf8Regex!.EnumerateMatches(_context.InputBytes);
        while (enumerator.MoveNext())
        {
            sum += enumerator.Current.IndexInBytes;
        }

        return sum;
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        var sum = 0;
        foreach (System.Text.RegularExpressions.Match match in _context.Regex!.Matches(System.Text.Encoding.UTF8.GetString(_context.InputBytes)))
        {
            sum += match.Index;
        }

        return sum;
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        var sum = 0;
        foreach (System.Text.RegularExpressions.Match match in _context.Regex!.Matches(_context.InputString))
        {
            sum += match.Index;
        }

        return sum;
    }
}
