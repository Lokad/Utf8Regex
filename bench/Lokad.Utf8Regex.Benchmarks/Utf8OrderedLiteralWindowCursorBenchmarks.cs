using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

// Focused throughput controls for the R05 global window cursor. Run each
// category separately in Release/Short on the quiet host. Predecoded Regex
// retains its natural string I/O; the decode lane pays input conversion.
[MemoryDiagnoser]
[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
public class Utf8OrderedLiteralWindowCursorBenchmarks
{
    private byte[] _input = [];
    private string _text = string.Empty;
    private Utf8Regex _utf8 = new(@"a\s{2,}b", RegexOptions.CultureInvariant);
    private Regex _oracle = new(@"a\s{2,}b", RegexOptions.CultureInvariant);

    [Params(false, true)]
    public bool Compiled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _text = string.Concat(Enumerable.Repeat("xa  byy ", 1024));
        _input = Encoding.UTF8.GetBytes(_text);
        var options = RegexOptions.CultureInvariant | (Compiled ? RegexOptions.Compiled : RegexOptions.None);
        _utf8 = new Utf8Regex(@"a\s{2,}b", options);
        _oracle = new Regex(@"a\s{2,}b", options);
        if (EnumerateUtf8() != EnumeratePredecoded() || EnumerateDecode() != EnumeratePredecoded() ||
            SplitUtf8() != SplitPredecoded() || SplitDecode() != SplitPredecoded() ||
            Encoding.UTF8.GetString(ReplaceUtf8()) != ReplacePredecoded() || ReplaceDecode() != ReplacePredecoded())
        {
            throw new InvalidOperationException("Ordered-window cursor benchmark lanes disagree.");
        }
    }

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int EnumerateUtf8()
    {
        var checksum = 0;
        foreach (var match in _utf8.EnumerateMatches(_input))
        {
            checksum += match.IndexInUtf16 + match.LengthInUtf16;
        }
        return checksum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Enumerate")]
    public int EnumeratePredecoded() => SumMatches(_text);

    [Benchmark]
    [BenchmarkCategory("Enumerate")]
    public int EnumerateDecode() => SumMatches(Encoding.UTF8.GetString(_input));

    private int SumMatches(string text)
    {
        var checksum = 0;
        foreach (var match in _oracle.EnumerateMatches(text))
        {
            checksum += match.Index + match.Length;
        }
        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("Split")]
    public int SplitUtf8()
    {
        var checksum = 0;
        foreach (var split in _utf8.EnumerateSplits(_input))
        {
            checksum += split.IndexInUtf16 + split.LengthInUtf16;
        }
        return checksum;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Split")]
    public int SplitPredecoded() => SumSplits(_text);

    [Benchmark]
    [BenchmarkCategory("Split")]
    public int SplitDecode() => SumSplits(Encoding.UTF8.GetString(_input));

    private int SumSplits(string text)
    {
        var checksum = 0;
        foreach (var split in _oracle.EnumerateSplits(text))
        {
            checksum += split.End.Value;
        }
        return checksum;
    }

    [Benchmark]
    [BenchmarkCategory("Replace")]
    public byte[] ReplaceUtf8() => _utf8.Replace(_input, "Q");

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Replace")]
    public string ReplacePredecoded() => _oracle.Replace(_text, "Q");

    [Benchmark]
    [BenchmarkCategory("Replace")]
    public string ReplaceDecode() => _oracle.Replace(Encoding.UTF8.GetString(_input), "Q");
}
