using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("DotNetPerformanceReplica", "LiteralAlternate")]
public class DotNetPerformanceReplicaLiteralAlternateBenchmarks
{
    private DotNetPerformanceReplicaBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => DotNetPerformanceReplicaBenchmarkCatalog.GetIds("literal-alternate", DotNetPerformanceReplicaBenchmarkModel.Count);

    [GlobalSetup]
    public void Setup()
    {
        _context = new DotNetPerformanceReplicaBenchmarkContext(DotNetPerformanceReplicaBenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Regex()
    {
        return _context.Utf8Regex.Count(_context.InputBytes);
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        return _context.CountDecodeThenRegex();
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        return _context.CountPredecodedRegex();
    }
}
