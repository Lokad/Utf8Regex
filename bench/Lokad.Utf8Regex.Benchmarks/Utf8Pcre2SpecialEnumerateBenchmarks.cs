using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8Pcre2SpecialEnumerateBenchmarks
{
    private Utf8Pcre2BenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8Pcre2BenchmarkCatalog.GetIds(Utf8Pcre2BenchmarkOperation.EnumerateMatches, Utf8Pcre2BenchmarkBackend.Pcre2Only)
        .Where(static id => Utf8Pcre2BenchmarkCatalog.Get(id).SupportedBackends == Utf8Pcre2BenchmarkBackend.Pcre2Only);

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
}
