using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8CandidatePortfolioBenchmarks
{
    private byte[] _input = [];
    private Utf8StructuralSearchPlan[] _plans = [];

    [Params(256, 1024, 4096)]
    public int DenseCandidateCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _input = Encoding.UTF8.GetBytes(new string('a', DenseCandidateCount) + "z");
        var dense = new PreparedSearcher(
            new PreparedSubstringSearch([(byte)'a'], ignoreCase: false),
            ignoreCase: false);
        var far = new PreparedSearcher(
            new PreparedSubstringSearch([(byte)'z'], ignoreCase: false),
            ignoreCase: false);
        _plans =
        [
            Utf8StructuralSearchPlan.CreateStartPlan(dense),
            Utf8StructuralSearchPlan.CreateStartPlan(far),
        ];
    }

    [Benchmark(Baseline = true)]
    public int PersistentMerge()
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout, default);
        using var cursor = new Utf8CandidatePortfolioCursor(_plans, _input, 0, ref invocation);
        var count = 0;
        while (cursor.TryGetNext(out _))
        {
            count++;
        }

        return count;
    }

    [Benchmark]
    public int RestartSourcesAfterEveryCandidate()
    {
        var count = 0;
        var startIndex = 0;
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, System.Text.RegularExpressions.Regex.InfiniteMatchTimeout, default);
        while (startIndex <= _input.Length)
        {
            using var cursor = new Utf8CandidatePortfolioCursor(_plans, _input, startIndex, ref invocation);
            if (!cursor.TryGetNext(out var candidate))
            {
                break;
            }

            count++;
            startIndex = candidate.StartIndex + 1;
        }

        return count;
    }
}
