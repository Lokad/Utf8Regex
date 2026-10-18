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
    private Utf8PythonRegex _detailedUnlimited = null!;
    private Utf8PythonRegex _detailedFinite = null!;
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
        // A wrapping capture commonly repeats group zero in owned snapshots.
        // Keep the existing corpus and exercise the actual public materializer.
        var detailedPattern = "(" + item.Pattern + ")";
        _detailedUnlimited = new Utf8PythonRegex(detailedPattern, item.Options, Timeout.InfiniteTimeSpan);
        _detailedFinite = new Utf8PythonRegex(detailedPattern, item.Options, Timeout.InfiniteTimeSpan,
            _finite.ResourcePolicy);

        var expected = PredecodedRegex();
        if (expected.Length != 1024)
            throw new InvalidOperationException("Unexpected catalog capture count.");
        Verify(Unlimited(), expected);
        Verify(Finite(), expected);
        Verify(FiniteWithToken(), expected);
        Verify(DecodeThenRegex(), expected);
        VerifyDetailed(DetailedUnlimited(), expected);
        VerifyDetailed(DetailedFinite(), expected);
        VerifyDetailed(DetailedFiniteWithToken(), expected);
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

    [Benchmark]
    public Utf8PythonDetailedMatchData[] DetailedUnlimited() => _detailedUnlimited.FindIterDetailed(_input);

    [Benchmark]
    public Utf8PythonDetailedMatchData[] DetailedFinite() => _detailedFinite.FindIterDetailed(_input);

    [Benchmark]
    public Utf8PythonDetailedMatchData[] DetailedFiniteWithToken() =>
        _detailedFinite.FindIterDetailed(_input, 0, _cancellation.Token);

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

    private static void VerifyDetailed(Utf8PythonDetailedMatchData[] actual, byte[][][] expected)
    {
        if (actual.Length != expected.Length)
            throw new InvalidOperationException("Detailed capture counts disagree.");
        for (var row = 0; row < actual.Length; row++)
        {
            var groups = actual[row].Groups;
            if (groups.Length != 4 || !groups.All(group => group.Success) ||
                groups[0].ValueText != groups[1].ValueText ||
                groups[0].StartOffsetInBytes != groups[1].StartOffsetInBytes ||
                groups[0].EndOffsetInBytes != groups[1].EndOffsetInBytes ||
                !Encoding.UTF8.GetBytes(groups[2].ValueText).AsSpan().SequenceEqual(expected[row][0]) ||
                !Encoding.UTF8.GetBytes(groups[3].ValueText).AsSpan().SequenceEqual(expected[row][1]))
                throw new InvalidOperationException($"Detailed capture {row} disagrees.");
        }
    }
}
