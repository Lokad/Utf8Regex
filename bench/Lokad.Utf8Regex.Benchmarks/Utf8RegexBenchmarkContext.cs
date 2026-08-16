using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class Utf8RegexBenchmarkContext
{
    public Utf8RegexBenchmarkContext(Utf8RegexBenchmarkCase benchmarkCase)
    {
        BenchmarkCase = benchmarkCase;
        InputString = benchmarkCase.Input;
        InputBytes = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        Utf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options);
        CompiledUtf8Regex = new Utf8Regex(benchmarkCase.Pattern, benchmarkCase.Options | RegexOptions.Compiled);
        Regex = new Regex(benchmarkCase.Pattern, benchmarkCase.Options, Regex.InfiniteMatchTimeout);
        CompiledRegex = new Regex(
            benchmarkCase.Pattern,
            benchmarkCase.Options | RegexOptions.Compiled,
            Regex.InfiniteMatchTimeout);
        Replacement = benchmarkCase.Replacement ?? string.Empty;
        ReplacementUtf8 = Encoding.UTF8.GetBytes(Replacement);
    }

    public Utf8RegexBenchmarkCase BenchmarkCase { get; }

    public string InputString { get; }

    public byte[] InputBytes { get; }

    public Utf8Regex Utf8Regex { get; }

    public Utf8Regex CompiledUtf8Regex { get; }

    public Regex Regex { get; }

    public Regex CompiledRegex { get; }

    public string Replacement { get; }

    public byte[] ReplacementUtf8 { get; }
}
