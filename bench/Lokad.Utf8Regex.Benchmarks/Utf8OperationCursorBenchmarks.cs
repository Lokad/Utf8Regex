using BenchmarkDotNet.Attributes;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8OperationCursorBenchmarks
{
    private byte[] _input = [];
    private Utf8Regex _regex = new("ab", RegexOptions.CultureInvariant);

    [Params(false, true)]
    public bool Compiled { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("xxabyy", 1024)));
        var options = RegexOptions.CultureInvariant |
            (Compiled ? RegexOptions.Compiled : RegexOptions.None);
        _regex = new Utf8Regex("ab", options);
    }

    [Benchmark]
    public bool IsMatch() => _regex.IsMatch(_input);

    [Benchmark]
    public Utf8ValueMatch Match() => _regex.Match(_input);

    [Benchmark]
    public int Count() => _regex.Count(_input);

    [Benchmark]
    public int EnumerateMatches()
    {
        var count = 0;
        foreach (var match in _regex.EnumerateMatches(_input))
        {
            count += match.LengthInBytes;
        }

        return count;
    }

    [Benchmark]
    public int EnumerateSplits()
    {
        var count = 0;
        foreach (var split in _regex.EnumerateSplits(_input))
        {
            count += split.LengthInBytes;
        }

        return count;
    }
}
