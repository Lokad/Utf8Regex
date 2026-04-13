using BenchmarkDotNet.Attributes;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("DotNetPerformanceReplica", "Compile")]
public class DotNetPerformanceReplicaCompileBenchmarks
{
    private DotNetPerformanceReplicaBenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds
    {
        get
        {
            foreach (var group in new[] { "bounded-repeat", "dictionary", "aws-keys", "ruff-noqa" })
            {
                foreach (var id in DotNetPerformanceReplicaBenchmarkCatalog.GetIds(group, DotNetPerformanceReplicaBenchmarkModel.Compile))
                {
                    yield return id;
                }
            }
        }
    }

    [GlobalSetup]
    public void Setup() => _context = new DotNetPerformanceReplicaBenchmarkContext(DotNetPerformanceReplicaBenchmarkCatalog.Get(CaseId));

    [Benchmark(Baseline = true)]
    public Utf8Regex ConstructUtf8Regex() => new(_context.Pattern, _context.BenchmarkCase.Options);

    [Benchmark]
    public Regex ConstructDotNetRegex() => new(_context.Pattern, _context.BenchmarkCase.Options, Regex.InfiniteMatchTimeout);
}
