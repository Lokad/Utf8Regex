using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("DotNetPerformanceReplica", "AwsKeys")]
public class DotNetPerformanceReplicaAwsKeysBenchmarks
{
    private DotNetPerformanceReplicaBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => DotNetPerformanceReplicaBenchmarkCatalog.GetIds("aws-keys", DotNetPerformanceReplicaBenchmarkModel.Count);

    [GlobalSetup]
    public void Setup() => _context = new DotNetPerformanceReplicaBenchmarkContext(DotNetPerformanceReplicaBenchmarkCatalog.Get(CaseId));

    [Benchmark(Baseline = true)]
    public int Utf8Regex() => _context.Utf8Regex.Count(_context.InputBytes);

    [Benchmark]
    public int DecodeThenRegex() => _context.CountDecodeThenRegex();

    [Benchmark]
    public int PredecodedRegex() => _context.CountPredecodedRegex();
}

[MemoryDiagnoser]
[BenchmarkCategory("DotNetPerformanceReplicaDev", "AwsKeys")]
public class DotNetPerformanceReplicaAwsKeysDevBenchmarks
{
    private DotNetPerformanceReplicaBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => DotNetPerformanceReplicaBenchmarkCatalog.GetIds("aws-keys", DotNetPerformanceReplicaBenchmarkModel.Count, developmentOnly: true);

    [GlobalSetup]
    public void Setup() => _context = new DotNetPerformanceReplicaBenchmarkContext(DotNetPerformanceReplicaBenchmarkCatalog.Get(CaseId));

    [Benchmark(Baseline = true)]
    public int Utf8Regex() => _context.Utf8Regex.Count(_context.InputBytes);

    [Benchmark]
    public int DecodeThenRegex() => _context.CountDecodeThenRegex();

    [Benchmark]
    public int PredecodedRegex() => _context.CountPredecodedRegex();
}
