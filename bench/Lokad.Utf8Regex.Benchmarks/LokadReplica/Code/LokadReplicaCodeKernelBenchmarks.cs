using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("LokadReplica\\Code", "Kernel")]
public class LokadCodeKernelBenchmarks
{
    private LokadReplicaCodeBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => LokadReplicaCodeBenchmarkCatalog.GetAllIds(LokadReplicaCodeBenchmarkModel.Count);

    [GlobalSetup]
    public void Setup() => _context = new LokadReplicaCodeBenchmarkContext(LokadReplicaCodeBenchmarkCatalog.Get(CaseId));

    [Benchmark(Baseline = true)]
    public int Utf8Regex() => _context.Utf8Regex.Count(_context.InputBytes);

    [Benchmark]
    public int DecodeThenRegex() => _context.CountDecodeThenRegex();

    [Benchmark]
    public int PredecodedRegex() => _context.CountPredecodedRegex();
}
