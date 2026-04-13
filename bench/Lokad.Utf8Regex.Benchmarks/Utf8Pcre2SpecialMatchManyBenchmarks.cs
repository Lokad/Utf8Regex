using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8Pcre2SpecialMatchManyBenchmarks
{
    private Utf8Pcre2BenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8Pcre2BenchmarkCatalog.GetIds(Utf8Pcre2BenchmarkOperation.MatchMany, Utf8Pcre2BenchmarkBackend.Pcre2Only)
        .Where(static id => Utf8Pcre2BenchmarkCatalog.Get(id).SupportedBackends == Utf8Pcre2BenchmarkBackend.Pcre2Only);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8Pcre2BenchmarkContext(Utf8Pcre2BenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Pcre2Regex()
    {
        Span<global::Lokad.Utf8Regex.Pcre2.Utf8Pcre2MatchData> destination = stackalloc global::Lokad.Utf8Regex.Pcre2.Utf8Pcre2MatchData[8];
        var written = _context.Utf8Pcre2Regex.MatchMany(_context.InputBytes, destination, out var isMore);
        var sum = isMore ? 1 : 0;
        for (var i = 0; i < written; i++)
        {
            sum += destination[i].StartOffsetInBytes;
        }

        return sum;
    }
}
