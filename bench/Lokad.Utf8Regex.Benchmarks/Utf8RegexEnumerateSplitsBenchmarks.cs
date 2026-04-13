using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8RegexEnumerateSplitsBenchmarks
{
    private Utf8RegexBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8RegexBenchmarkCatalog.GetIds(Utf8RegexBenchmarkOperation.EnumerateSplits);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8RegexBenchmarkContext(Utf8RegexBenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Regex()
    {
        var sum = 0;
        foreach (var split in _context.Utf8Regex.EnumerateSplits(_context.InputBytes))
        {
            sum += split.IndexInUtf16 + split.LengthInUtf16;
        }

        return sum;
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        var sum = 0;
        foreach (var split in _context.Regex.EnumerateSplits(System.Text.Encoding.UTF8.GetString(_context.InputBytes)))
        {
            var (offset, length) = ResolveRange(split, _context.InputString.Length);
            sum += offset + length;
        }

        return sum;
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        var sum = 0;
        foreach (var split in _context.Regex.EnumerateSplits(_context.InputString))
        {
            var (offset, length) = ResolveRange(split, _context.InputString.Length);
            sum += offset + length;
        }

        return sum;
    }

    private static (int Offset, int Length) ResolveRange(Range range, int inputLength)
    {
        var start = range.Start.GetOffset(inputLength);
        var end = range.End.GetOffset(inputLength);
        return (start, end - start);
    }
}
