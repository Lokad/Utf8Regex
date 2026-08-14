using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8ExecutionDeadlineBenchmarks
{
    private Utf8ExecutionDeadline _finite = Utf8ExecutionDeadline.Infinite;

    [GlobalSetup]
    public void Setup() => _finite = Utf8ExecutionDeadline.Start(TimeSpan.FromHours(1));

    [Benchmark(Baseline = true, OperationsPerInvoke = 1024)]
    public bool PollInfinite()
    {
        var expired = false;
        for (var i = 0; i < 1024; i++)
        {
            expired |= Utf8ExecutionDeadline.Infinite.Poll();
        }

        return expired;
    }

    [Benchmark(OperationsPerInvoke = 1024)]
    public bool PollFiniteNonExpired()
    {
        var expired = false;
        for (var i = 0; i < 1024; i++)
        {
            expired |= _finite.Poll();
        }

        return expired;
    }
}
