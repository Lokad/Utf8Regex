using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8RequiredLiteralBenchmarks
{
    private const string Pattern = @"[a-z]+@[a-z]+\.[a-z]+";
    private byte[] _input = null!;
    private string _text = null!;
    private Utf8Regex _finite = null!;
    private Regex _baseline = null!;
    private CancellationTokenSource _cancellation = null!;

    [Params(false, true)]
    public bool Hit { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _text = new string('a', 512) + (Hit ? "@example.com" : string.Empty);
        _input = Encoding.UTF8.GetBytes(_text);
        _finite = new Utf8Regex(Pattern, RegexOptions.CultureInvariant, Regex.InfiniteMatchTimeout,
            new Utf8ResourcePolicy
            {
                MaxWorkSteps = 50000000, MaxDepth = 8192,
                MaxWorkingBytes = 64UL << 20, MaxResultBytes = 128UL << 20,
            });
        _baseline = new Regex(Pattern, RegexOptions.CultureInvariant);
        _cancellation = new CancellationTokenSource();
        if (FiniteIsMatch() != Hit || LiveTokenIsMatch() != Hit ||
            FiniteCount() != (Hit ? 1 : 0) || PredecodedRegex() != Hit)
            throw new InvalidOperationException("Required-literal public results disagree.");
    }

    [GlobalCleanup]
    public void Cleanup() => _cancellation.Dispose();

    [Benchmark]
    public bool FiniteIsMatch() => _finite.IsMatch(_input);

    [Benchmark]
    public int FiniteCount() => _finite.Count(_input);

    [Benchmark]
    public bool LiveTokenIsMatch() => _finite.IsMatch(_input, _cancellation.Token);

    [Benchmark]
    public bool PredecodedRegex() => _baseline.IsMatch(_text);
}
