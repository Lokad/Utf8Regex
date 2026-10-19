using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using CoreRegex = Lokad.Utf8Regex.Utf8Regex;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8CoreNoMatchOutputBenchmarks
{
    private string _text = null!;
    private byte[] _input = null!;
    private byte[] _destination = null!;
    private CoreRegex _finite = null!;
    private CoreRegex _native = null!;
    private CoreRegex _matched = null!;
    private Regex _regex = null!;
    private Regex _matchedRegex = null!;
    private CancellationTokenSource _cancellation = null!;

    [GlobalSetup]
    public void Setup()
    {
        // Same corpus as the existing PythonRe no-match replacement workload.
        _text = PythonReBenchmarkCatalog.Cases.Single(
            item => item.Id == "replacement/no-match-string").Input;
        _input = Encoding.UTF8.GetBytes(_text);
        _destination = new byte[_input.Length];
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 50_000_000, MaxDepth = 1024,
            MaxWorkingBytes = 64 << 20, MaxResultBytes = 128 << 20,
        };
        _finite = new CoreRegex("(cat)", RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan, policy);
        _native = new CoreRegex("cat", RegexOptions.CultureInvariant);
        _matched = new CoreRegex("(dog)", RegexOptions.CultureInvariant, Timeout.InfiniteTimeSpan, policy);
        _regex = new Regex("(cat)", RegexOptions.CultureInvariant);
        _matchedRegex = new Regex("(dog)", RegexOptions.CultureInvariant);
        _cancellation = new CancellationTokenSource();
        if (FiniteText() != _text || FiniteTextWithToken() != _text ||
            !FiniteBytes().AsSpan().SequenceEqual(_input) || FiniteDestination() != _input.Length ||
            !_destination.AsSpan().SequenceEqual(_input) || NativeText() != _text ||
            PredecodedRegex() != _text || DecodeThenRegex() != _text ||
            MatchedFiniteText() != MatchedPredecodedRegex())
            throw new InvalidOperationException("Core replacement outputs disagree.");
    }

    [GlobalCleanup]
    public void Cleanup() => _cancellation.Dispose();

    [Benchmark(Baseline = true)]
    public string FiniteText() => _finite.ReplaceToString(_input, "tiger");
    [Benchmark]
    public string FiniteTextWithToken() => _finite.ReplaceToString(_input, "tiger", _cancellation.Token);
    [Benchmark]
    public byte[] FiniteBytes() => _finite.Replace(_input, "tiger"u8);
    [Benchmark]
    public int FiniteDestination() => _finite.TryReplace(_input, "tiger", _destination, out var written)
        == OperationStatus.Done ? written : throw new InvalidOperationException("Destination too small.");
    [Benchmark]
    public string NativeText() => _native.ReplaceToString(_input, "tiger");
    [Benchmark]
    public string PredecodedRegex() => _regex.Replace(_text, "tiger");
    [Benchmark]
    public string DecodeThenRegex() => _regex.Replace(Encoding.UTF8.GetString(_input), "tiger");
    [Benchmark]
    public string MatchedFiniteText() => _matched.ReplaceToString(_input, "<$1>");
    [Benchmark]
    public string MatchedPredecodedRegex() => _matchedRegex.Replace(_text, "<$1>");
}
