using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8ReversibleMatchStateBenchmarks
{
    private readonly byte[] _input = Enumerable.Repeat((byte)'a', 4096).Append((byte)'z').ToArray();
    private readonly Utf8Regex _regex = new("((?:a|b){0,2147483647})z", RegexOptions.CultureInvariant);

    // Measures the production scalar capture route, including result publication.
    // Measurements of the retired private journal are historical only.
    [Benchmark]
    public int QuantifiedCaptureWithHugeSyntacticBound()
        => _regex.MatchDetailed(_input).LengthInBytes;
}
