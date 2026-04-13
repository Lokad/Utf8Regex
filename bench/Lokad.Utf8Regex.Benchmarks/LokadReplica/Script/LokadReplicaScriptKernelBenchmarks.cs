using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("LokadReplica\\Script", "Kernel")]
public class LokadScriptKernelBenchmarks
{
    private LokadReplicaScriptBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => LokadReplicaScriptBenchmarkCatalog.GetAllIds();

    [GlobalSetup]
    public void Setup() => _context = new LokadReplicaScriptBenchmarkContext(LokadReplicaScriptBenchmarkCatalog.Get(CaseId));

    [Benchmark(Baseline = true)]
    public int Utf8Regex() => _context.ExecuteUtf8Regex();

    [Benchmark]
    public int DecodeThenRegex() => _context.ExecuteDecodeThenRegex();

    [Benchmark]
    public int PredecodedRegex() => _context.ExecutePredecodedRegex();
}
