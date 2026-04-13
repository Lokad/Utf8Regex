using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
public class Utf8Pcre2MatchManyBenchmarks
{
    private Utf8Pcre2BenchmarkContext _context = null!;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    public IEnumerable<string> CaseIds => Utf8Pcre2BenchmarkCatalog.GetIds(Utf8Pcre2BenchmarkOperation.MatchMany, Utf8Pcre2BenchmarkBackend.AllManagedComparisons);

    [GlobalSetup]
    public void Setup()
    {
        _context = new Utf8Pcre2BenchmarkContext(Utf8Pcre2BenchmarkCatalog.Get(CaseId));
    }

    [Benchmark(Baseline = true)]
    public int Utf8Pcre2Regex()
    {
        Span<global::Lokad.Utf8Regex.Pcre2.Utf8Pcre2MatchData> destination = stackalloc global::Lokad.Utf8Regex.Pcre2.Utf8Pcre2MatchData[8];
        var written = _context.Utf8Pcre2Regex.MatchMany(_context.InputBytes, destination, out var isMore);
        var sum = isMore ? 1 : 0;
        for (var i = 0; i < written; i++)
        {
            sum += destination[i].StartOffsetInBytes;
        }

        return sum;
    }

    [Benchmark]
    public int Utf8Regex()
    {
        Span<int> starts = stackalloc int[8];
        var written = 0;
        var enumerator = _context.Utf8Regex!.EnumerateMatches(_context.InputBytes);
        while (written < starts.Length && enumerator.MoveNext())
        {
            starts[written] = enumerator.Current.IndexInBytes;
            written++;
        }

        var isMore = written == starts.Length && enumerator.MoveNext();
        var sum = isMore ? 1 : 0;
        for (var i = 0; i < written; i++)
        {
            sum += starts[i];
        }

        return sum;
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        Span<int> starts = stackalloc int[8];
        var written = 0;
        var matches = _context.Regex!.Matches(System.Text.Encoding.UTF8.GetString(_context.InputBytes));
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (written == starts.Length)
            {
                break;
            }

            starts[written] = match.Index;
            written++;
        }

        var isMore = matches.Count > written;
        var sum = isMore ? 1 : 0;
        for (var i = 0; i < written; i++)
        {
            sum += starts[i];
        }

        return sum;
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        Span<int> starts = stackalloc int[8];
        var written = 0;
        var matches = _context.Regex!.Matches(_context.InputString);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (written == starts.Length)
            {
                break;
            }

            starts[written] = match.Index;
            written++;
        }

        var isMore = matches.Count > written;
        var sum = isMore ? 1 : 0;
        for (var i = 0; i < written; i++)
        {
            sum += starts[i];
        }

        return sum;
    }
}
