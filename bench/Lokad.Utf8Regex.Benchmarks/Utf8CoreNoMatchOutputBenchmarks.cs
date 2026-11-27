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
    private CoreRegex _nativeGrouped = null!;
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
            MaxDepth = 1024,
            MaxWorkingBytes = 64 << 20,
            MaxResultBytes = 128 << 20
        };
        _finite = new CoreRegex("(cat)", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        _native = new CoreRegex("cat", options: RegexOptions.CultureInvariant);
        _matched = new CoreRegex("(dog)", options: RegexOptions.CultureInvariant, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Timeout.InfiniteTimeSpan,
            ResourcePolicy = policy
        });
        _nativeGrouped = new CoreRegex("^((cat))$", options: RegexOptions.Compiled | RegexOptions.CultureInvariant);
        _regex = new Regex("(cat)", RegexOptions.CultureInvariant);
        _matchedRegex = new Regex("(dog)", RegexOptions.CultureInvariant);
        _cancellation = new CancellationTokenSource();
        if (!FiniteBytes().AsSpan().SequenceEqual(_input) ||
            !FiniteBytesWithToken().AsSpan().SequenceEqual(_input) || FiniteDestination() != _input.Length ||
            !_destination.AsSpan().SequenceEqual(_input) || !NativeBytes().AsSpan().SequenceEqual(_input) ||
            !PredecodedRegex().AsSpan().SequenceEqual(_input) || !DecodeThenRegex().AsSpan().SequenceEqual(_input) ||
            !MatchedFiniteBytes().AsSpan().SequenceEqual(MatchedPredecodedRegex()) ||
            !NativeGroupedBytes().AsSpan().SequenceEqual(_input) ||
            NativeGroupedDestination() != _input.Length || !_destination.AsSpan().SequenceEqual(_input) ||
            !MatchedNativeGroupedBytes().AsSpan().SequenceEqual("cat-cat"u8))
            throw new InvalidOperationException("Core replacement outputs disagree.");
    }

    [GlobalCleanup]
    public void Cleanup() => _cancellation.Dispose();

    [Benchmark(Baseline = true)]
    public byte[] FiniteBytes() => _finite.Replace(_input, "tiger"u8);
    [Benchmark]
    public byte[] FiniteBytesWithToken() => _finite.Replace(_input, "tiger"u8, _cancellation.Token);
    [Benchmark]
    public int FiniteDestination() => _finite.TryReplace(_input, "tiger"u8, _destination, out var written)
        == OperationStatus.Done ? written : throw new InvalidOperationException("Destination too small.");
    [Benchmark]
    public byte[] NativeGroupedBytes() => _nativeGrouped.Replace(_input, "$1-$2"u8);
    [Benchmark]
    public int NativeGroupedDestination() => _nativeGrouped.TryReplace(_input, "$1-$2"u8, _destination, out var written)
        == OperationStatus.Done ? written : throw new InvalidOperationException("Destination too small.");
    [Benchmark]
    public byte[] MatchedNativeGroupedBytes() => _nativeGrouped.Replace("cat"u8, "$1-$2"u8);
    [Benchmark]
    public byte[] NativeBytes() => _native.Replace(_input, "tiger"u8);
    [Benchmark]
    public byte[] PredecodedRegex() => Encoding.UTF8.GetBytes(_regex.Replace(_text, "tiger"));
    [Benchmark]
    public byte[] DecodeThenRegex() => Encoding.UTF8.GetBytes(_regex.Replace(Encoding.UTF8.GetString(_input), "tiger"));
    [Benchmark]
    public byte[] MatchedFiniteBytes() => _matched.Replace(_input, "<$1>"u8);
    [Benchmark]
    public byte[] MatchedPredecodedRegex() => Encoding.UTF8.GetBytes(_matchedRegex.Replace(_text, "<$1>"));
}
