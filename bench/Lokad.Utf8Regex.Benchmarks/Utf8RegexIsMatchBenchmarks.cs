using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8RegexIsMatchBenchmarks
{
    private const int RepeatCount = 256;
    private Utf8RegexBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8RegexBenchmarkCatalog.GetIds(Utf8RegexBenchmarkOperation.IsMatch);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8RegexBenchmarkContext(Utf8RegexBenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true, OperationsPerInvoke = RepeatCount)]
    public int Utf8Regex()
    {
        var count = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            if (_context.Utf8Regex.IsMatch(_context.InputBytes))
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int DecodeThenRegex()
    {
        var count = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            if (_context.Regex.IsMatch(System.Text.Encoding.UTF8.GetString(_context.InputBytes)))
            {
                count++;
            }
        }

        return count;
    }

    [Benchmark(OperationsPerInvoke = RepeatCount)]
    public int PredecodedRegex()
    {
        var count = 0;
        for (var i = 0; i < RepeatCount; i++)
        {
            if (_context.Regex.IsMatch(_context.InputString))
            {
                count++;
            }
        }

        return count;
    }
}
