using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Utf8ClassBoundaryMechanicsBenchmarks
{
    private readonly byte[] _ascii = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("alpha_42-", 1024)));
    private readonly byte[] _unicode = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aéЖ😀_ ", 512)));
    private readonly AsciiCharClass _word = AsciiCharClass.FromPredicate(Utf8AsciiBytePredicates.IsWord);
    private int[] _unicodeBoundaries = [];

    [GlobalSetup]
    public void Setup()
    {
        var offsets = new List<int> { 0 };
        for (var offset = 0; offset < _unicode.Length;)
        {
            var width = _unicode[offset] switch
            {
                < 0x80 => 1,
                < 0xE0 => 2,
                < 0xF0 => 3,
                _ => 4,
            };
            offset += width;
            offsets.Add(offset);
        }

        _unicodeBoundaries = [.. offsets];
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Set")]
    public int PredicateSetLoop()
    {
        var count = 0;
        foreach (var value in _ascii)
        {
            count += Utf8AsciiBytePredicates.IsWord(value) ? 1 : 0;
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Set")]
    public int MaskSetLoop()
    {
        var count = 0;
        foreach (var value in _ascii)
        {
            count += _word.Contains(value) ? 1 : 0;
        }

        return count;
    }

    [Benchmark(Baseline = true)]
    [BenchmarkCategory("Boundary")]
    public int DirectAsciiBoundaries()
    {
        var count = 0;
        for (var offset = 0; offset <= _ascii.Length; offset++)
        {
            var previousIsWord = offset > 0 && Utf8AsciiBytePredicates.IsWord(_ascii[offset - 1]);
            var nextIsWord = offset < _ascii.Length && Utf8AsciiBytePredicates.IsWord(_ascii[offset]);
            count += previousIsWord != nextIsWord ? 1 : 0;
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Boundary")]
    public int DenseAsciiBoundaries()
    {
        var count = 0;
        for (var offset = 0; offset <= _ascii.Length; offset++)
        {
            count += DotNetUtf8WordBoundary.IsBoundary(_ascii, offset) ? 1 : 0;
        }

        return count;
    }

    [Benchmark]
    [BenchmarkCategory("Boundary")]
    public int DenseUnicodeBoundaries()
    {
        var count = 0;
        foreach (var offset in _unicodeBoundaries)
        {
            count += DotNetUtf8WordBoundary.IsBoundary(_unicode, offset) ? 1 : 0;
        }

        return count;
    }
}
