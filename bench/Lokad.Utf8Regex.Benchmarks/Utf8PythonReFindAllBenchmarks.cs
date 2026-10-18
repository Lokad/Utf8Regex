using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

// Existing catalog workload; BDN supplies warmup/calibration rather than the
// custom diagnostic loops. These .NET lanes compare composed UTF-8 tuples,
// not CPython performance. The pattern has overlapping scalar/code-unit results.
[MemoryDiagnoser]
public class Utf8PythonReFindAllBenchmarks
{
    private string _text = null!;
    private byte[] _input = null!;
    private Utf8PythonRegex _unlimited = null!;
    private Utf8PythonRegex _finite = null!;
    private Regex _regex = null!;
    private CancellationTokenSource _cancellation = null!;

    [GlobalSetup]
    public void Setup()
    {
        var item = PythonReBenchmarkCatalog.Cases.Single(
            item => item.Id == "findall/unicode-capture-utf8");
        _text = item.Input;
        _input = Encoding.UTF8.GetBytes(_text);
        _unlimited = new Utf8PythonRegex(item.Pattern, item.Options, Timeout.InfiniteTimeSpan);
        _finite = new Utf8PythonRegex(item.Pattern, item.Options, Timeout.InfiniteTimeSpan,
            new Utf8ResourcePolicy
            {
                MaxWorkSteps = 50_000_000,
                MaxDepth = 1024,
                MaxWorkingBytes = 64 << 20,
                MaxResultBytes = 128 << 20,
            });
        _regex = new Regex(item.Pattern, RegexOptions.CultureInvariant);
        _cancellation = new CancellationTokenSource();

        var expected = PredecodedRegex();
        if (expected.Length != 1024)
            throw new InvalidOperationException("Unexpected catalog capture count.");
        Verify(Unlimited(), expected);
        Verify(Finite(), expected);
        Verify(FiniteWithToken(), expected);
        Verify(DecodeThenRegex(), expected);
    }

    [GlobalCleanup]
    public void Cleanup() => _cancellation.Dispose();

    [Benchmark(Baseline = true)]
    public byte[][][] Unlimited() => _unlimited.FindAllToUtf8(_input).TupleValues;

    [Benchmark]
    public byte[][][] Finite() => _finite.FindAllToUtf8(_input).TupleValues;

    [Benchmark]
    public byte[][][] FiniteWithToken() =>
        _finite.FindAllToUtf8(_input, 0, _cancellation.Token).TupleValues;

    [Benchmark]
    public byte[][][] PredecodedRegex() => Project(_text);

    [Benchmark]
    public byte[][][] DecodeThenRegex() => Project(Encoding.UTF8.GetString(_input));

    private byte[][][] Project(string text) => _regex.Matches(text)
        .Select(match => new[]
        {
            Encoding.UTF8.GetBytes(match.Groups[1].Value),
            Encoding.UTF8.GetBytes(match.Groups[2].Value),
        }).ToArray();

    private static void Verify(byte[][][] actual, byte[][][] expected)
    {
        if (actual.Length != expected.Length)
            throw new InvalidOperationException("Capture tuple counts disagree.");
        for (var row = 0; row < expected.Length; row++)
        {
            if (actual[row].Length != 2 ||
                !actual[row][0].AsSpan().SequenceEqual(expected[row][0]) ||
                !actual[row][1].AsSpan().SequenceEqual(expected[row][1]))
                throw new InvalidOperationException($"Capture tuple {row} disagrees.");
        }
    }
}
