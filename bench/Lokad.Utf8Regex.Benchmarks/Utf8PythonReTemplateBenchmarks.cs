using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

// Public text replacement on overlapping ASCII-capture/Unicode-subject semantics.
// Setup warms immutable template plans; cold admission is qualified by tests.
[MemoryDiagnoser]
public class Utf8PythonReTemplateBenchmarks
{
    private string _text = null!;
    private byte[] _input = null!;
    private Utf8PythonRegex _unlimited = null!;
    private Utf8PythonRegex _finite = null!;
    private Regex _regex = null!;
    private CancellationTokenSource _cancellation = null!;
    private const string Template = @"<\g<word>>";

    [Params(1, 128)]
    public int Repetitions { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _text = string.Concat(Enumerable.Repeat("alpha 😀 café beta é ", Repetitions));
        _input = Encoding.UTF8.GetBytes(_text);
        _unlimited = new Utf8PythonRegex("(?P<word>[a-z]+)");
        _finite = new Utf8PythonRegex("(?P<word>[a-z]+)", PythonReCompileOptions.None,
            Timeout.InfiniteTimeSpan, new Utf8ResourcePolicy
            {
                MaxWorkSteps = 50_000_000, MaxDepth = 8192,
                MaxWorkingBytes = 64 << 20, MaxResultBytes = 128 << 20,
            });
        _regex = new Regex("(?<word>[a-z]+)", RegexOptions.CultureInvariant);
        _cancellation = new CancellationTokenSource();
        var expected = PredecodedRegex();
        if (Unlimited() != expected || Finite() != expected || FiniteWithToken() != expected ||
            DecodeThenRegex() != expected)
            throw new InvalidOperationException("Public replacement outputs disagree.");
    }

    [GlobalCleanup]
    public void Cleanup() => _cancellation.Dispose();

    [Benchmark(Baseline = true)]
    public string Unlimited() => _unlimited.ReplaceToString(_input, Template);
    [Benchmark]
    public string Finite() => _finite.ReplaceToString(_input, Template);
    [Benchmark]
    public string FiniteWithToken() => _finite.ReplaceToString(_input, Template, 0, 0, _cancellation.Token);
    [Benchmark]
    public string PredecodedRegex() => _regex.Replace(_text, "<${word}>");
    [Benchmark]
    public string DecodeThenRegex() => _regex.Replace(Encoding.UTF8.GetString(_input), "<${word}>");
}
