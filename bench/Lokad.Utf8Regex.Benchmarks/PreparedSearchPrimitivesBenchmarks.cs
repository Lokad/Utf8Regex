using System.Text;
using BenchmarkDotNet.Attributes;
using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class PreparedSearchPrimitivesBenchmarks
{
    private ReadOnlyMemory<byte> _shortInput;
    private ReadOnlyMemory<byte> _mediumInput;
    private ReadOnlyMemory<byte> _largeInput;
    private PreparedByteSearch _singleByteSearch;
    private PreparedByteSearch _tripleByteSearch;
    private PreparedByteSearch _setByteSearch;
    private PreparedSubstringSearch _substringSearch;

    [GlobalSetup]
    public void Setup()
    {
        _shortInput = Encoding.UTF8.GetBytes(BuildInput(256));
        _mediumInput = Encoding.UTF8.GetBytes(BuildInput(4096));
        _largeInput = Encoding.UTF8.GetBytes(BuildInput(65536));
        _singleByteSearch = PreparedByteSearch.Create((byte)'n');
        _tripleByteSearch = PreparedByteSearch.Create((byte)'n', (byte)'o', (byte)'x');
        _setByteSearch = PreparedByteSearch.Create((byte)'n', (byte)'o', (byte)'x', (byte)'y', (byte)'z', (byte)'q');
        _substringSearch = new PreparedSubstringSearch(Encoding.UTF8.GetBytes("needle"), ignoreCase: false);
    }

    [Benchmark(Baseline = true)]
    public int ShortSingleByte()
    {
        return _singleByteSearch.IndexOf(_shortInput.Span);
    }

    [Benchmark]
    public int ShortTripleByte()
    {
        return _tripleByteSearch.IndexOf(_shortInput.Span);
    }

    [Benchmark]
    public int ShortByteSet()
    {
        return _setByteSearch.IndexOf(_shortInput.Span);
    }

    [Benchmark]
    public int ShortSubstring()
    {
        return _substringSearch.IndexOf(_shortInput.Span);
    }

    [Benchmark]
    public int MediumSingleByte()
    {
        return _singleByteSearch.IndexOf(_mediumInput.Span);
    }

    [Benchmark]
    public int MediumTripleByte()
    {
        return _tripleByteSearch.IndexOf(_mediumInput.Span);
    }

    [Benchmark]
    public int MediumByteSet()
    {
        return _setByteSearch.IndexOf(_mediumInput.Span);
    }

    [Benchmark]
    public int MediumSubstring()
    {
        return _substringSearch.IndexOf(_mediumInput.Span);
    }

    [Benchmark]
    public int LargeSingleByte()
    {
        return _singleByteSearch.IndexOf(_largeInput.Span);
    }

    [Benchmark]
    public int LargeTripleByte()
    {
        return _tripleByteSearch.IndexOf(_largeInput.Span);
    }

    [Benchmark]
    public int LargeByteSet()
    {
        return _setByteSearch.IndexOf(_largeInput.Span);
    }

    [Benchmark]
    public int LargeSubstring()
    {
        return _substringSearch.IndexOf(_largeInput.Span);
    }

    private static string BuildInput(int tokenCount)
    {
        var sb = new StringBuilder(tokenCount * 12);
        for (var i = 0; i < tokenCount; i++)
        {
            sb.Append("seg:");
            sb.Append(i % 29 == 0 ? "needle" : "haystack");
            sb.Append(';');
        }

        return sb.ToString();
    }
}
