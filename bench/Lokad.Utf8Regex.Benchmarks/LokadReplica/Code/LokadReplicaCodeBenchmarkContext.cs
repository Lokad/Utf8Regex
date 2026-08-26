using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class LokadReplicaCodeBenchmarkContext
{
    public LokadReplicaCodeBenchmarkContext(LokadReplicaCodeBenchmarkCase benchmarkCase)
    {
        BenchmarkCase = benchmarkCase;
        Pattern = benchmarkCase.Pattern;
        CompiledPattern = benchmarkCase.PatternMode == LokadCodePatternMode.Literal
            ? Regex.Escape(benchmarkCase.Pattern)
            : benchmarkCase.Pattern;
        InputString = LoadCorpus();
        InputBytes = Encoding.UTF8.GetBytes(InputString);
        Utf8Regex = new Utf8Regex(CompiledPattern, benchmarkCase.Options);
        Regex = new Regex(CompiledPattern, benchmarkCase.Options, Regex.InfiniteMatchTimeout);
        CompiledRegex = new Regex(CompiledPattern, benchmarkCase.Options | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
    }

    public LokadReplicaCodeBenchmarkCase BenchmarkCase { get; }

    public string Pattern { get; }

    public string CompiledPattern { get; }

    public string InputString { get; }

    public byte[] InputBytes { get; }

    public Utf8Regex Utf8Regex { get; }

    public Regex Regex { get; }

    public Regex CompiledRegex { get; }

    public int CountDecodeThenRegex() => Regex.Count(Encoding.UTF8.GetString(InputBytes));

    public int CountDecodeThenCompiledRegex() => CompiledRegex.Count(Encoding.UTF8.GetString(InputBytes));

    public int CountPredecodedRegex() => Regex.Count(InputString);

    public int CountPredecodedCompiledRegex() => CompiledRegex.Count(InputString);

    private static string LoadCorpus()
    {
        return BenchmarkSubjectDefinitions.CodeCorpus;
    }
}
