using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.Internal.Input;
using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8InputProjectionBenchmarks
{
    private byte[] _input = [];

    [Params(256, 512, 1024, 2048)]
    public int Repetitions { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _input = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("aé€😀", Repetitions)));
    }

    [Benchmark(Baseline = true)]
    public int ValidateOnly()
    {
        return Utf8InputAnalyzer.ValidateOnly(_input).Utf16Length;
    }

    [Benchmark]
    public int ValidateAndProjectDensely()
    {
        var subject = Utf8ValidatedInput.Create(_input);
        var cursor = subject.CreateProjectionCursor();
        var offset = 0;
        var checksum = 0;
        while (offset < _input.Length)
        {
            checksum += cursor.Project(subject.GetBytePosition(offset, "offset")).Value;
            offset += _input[offset] switch
            {
                < 0x80 => 1,
                < 0xE0 => 2,
                < 0xF0 => 3,
                _ => 4,
            };
        }

        return checksum + cursor.Project(subject.GetBytePosition(_input.Length, "offset")).Value;
    }

    [Benchmark]
    public int ValidateAndBuildRandomAccessMap()
    {
        var subject = Utf8ValidatedInput.Create(_input);
        return subject.GetRandomAccessMap().GetUtf16OffsetForByteOffset(_input.Length);
    }
}
