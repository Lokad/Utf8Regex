using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

// Existing no-match catalog workload with the public finite-policy lanes.
// The literal unlimited route and both .NET lanes provide unchanged controls.
[MemoryDiagnoser]
public class Utf8PythonReNoMatchOutputBenchmarks
{
    private byte[] _input = null!;
    private string _text = null!;
    private string _replacement = null!;
    private byte[] _replacementUtf8 = null!;
    private Utf8PythonRegex _unlimited = null!;
    private Utf8PythonRegex _finite = null!;
    private Regex _regex = null!;
    private CancellationTokenSource _cancellation = null!;

    [GlobalSetup]
    public void Setup()
    {
        var item = PythonReBenchmarkCatalog.Cases.Single(
            item => item.Id == "replacement/no-match-string");
        _text = item.Input;
        _input = Encoding.UTF8.GetBytes(_text);
        _replacement = item.Replacement;
        _replacementUtf8 = Encoding.UTF8.GetBytes(_replacement);
        _unlimited = new Utf8PythonRegex(item.Pattern, options: item.Options);
        _finite = new Utf8PythonRegex(item.Pattern, options: item.Options, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxDepth = 1024,
                MaxWorkingBytes = 64 << 20,
                MaxResultBytes = 128 << 20
            }
        });
        _regex = new Regex(item.Pattern, RegexOptions.CultureInvariant);
        _cancellation = new CancellationTokenSource();
        if (Unlimited() != _text || Finite() != _text || FiniteWithToken() != _text ||
            PredecodedRegex() != _text || DecodeThenRegex() != _text)
            throw new InvalidOperationException("No-match replacement outputs disagree.");
    }

    [GlobalCleanup]
    public void Cleanup() => _cancellation.Dispose();

    [Benchmark(Baseline = true)]
    public string Unlimited() => System.Text.Encoding.UTF8.GetString(_unlimited.Replace(_input, _replacementUtf8));
    [Benchmark]
    public string Finite() => System.Text.Encoding.UTF8.GetString(_finite.Replace(_input, _replacementUtf8));
    [Benchmark]
    public string FiniteWithToken() => System.Text.Encoding.UTF8.GetString(_finite.Replace(_input, _replacementUtf8, 0, 0, _cancellation.Token));
    [Benchmark]
    public string PredecodedRegex() => _regex.Replace(_text, _replacement);
    [Benchmark]
    public string DecodeThenRegex() => _regex.Replace(Encoding.UTF8.GetString(_input), _replacement);
}
