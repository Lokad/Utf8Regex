using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("LokadReplica", "PublicIndustry")]
public class DotNetPerformanceRegexIndustryBenchmarks
{
    private Utf8Regex _utf8Regex = null!;
    private Regex _regex = null!;
    private string _input = string.Empty;
    private byte[] _inputBytes = [];

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    [Params(RegexOptions.None, RegexOptions.Compiled)]
    public RegexOptions Options { get; set; }

    public IEnumerable<string> CaseIds =>
    [
        "mariomka/email-count",
        "mariomka/uri-count",
        "mariomka/ip-count",
        "rust-sherlock/letter-count",
        "rust-sherlock/holmes-window-count",
        "rust-sherlock/ing-count",
        "rust-sherlock/word-holmes-count",
        "rust-sherlock/nonnewline-count",
        "leipzig/twain-count",
        "leipzig/name-family-count",
        "leipzig/river-window-count",
        "leipzig/symbol-count",
        "boostdocs/ftp-line-match",
        "boostdocs/credit-card-match",
        "boostdocs/postcode-match",
        "boostdocs/date-match",
        "boostdocs/float-match",
    ];

    [GlobalSetup]
    public void Setup()
    {
        (_input, var pattern) = CaseId switch
        {
            "mariomka/email-count" => (DotNetPerformanceRegexData.MailNetworkCorpus, @"[\w\.+-]+@[\w\.-]+\.[\w\.-]+"),
            "mariomka/uri-count" => (DotNetPerformanceRegexData.MailNetworkCorpus, @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?"),
            "mariomka/ip-count" => (DotNetPerformanceRegexData.MailNetworkCorpus, @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])"),
            "rust-sherlock/letter-count" => (DotNetPerformanceRegexData.DetectiveCorpus, @"\p{L}"),
            "rust-sherlock/holmes-window-count" => (DotNetPerformanceRegexData.DetectiveCorpus, @"Holmes.{0,25}Watson|Watson.{0,25}Holmes"),
            "rust-sherlock/ing-count" => (DotNetPerformanceRegexData.DetectiveCorpus, @"\s[a-zA-Z]{0,12}ing\s"),
            "rust-sherlock/word-holmes-count" => (DotNetPerformanceRegexData.DetectiveCorpus, @"\w+\s+Holmes"),
            "rust-sherlock/nonnewline-count" => (DotNetPerformanceRegexData.DetectiveCorpus, @"[^\n]*"),
            "leipzig/twain-count" => (DotNetPerformanceRegexData.RiverCorpus, "Twain"),
            "leipzig/name-family-count" => (DotNetPerformanceRegexData.RiverCorpus, "Tom|Sawyer|Huckleberry|Finn"),
            "leipzig/river-window-count" => (DotNetPerformanceRegexData.RiverCorpus, "Tom.{10,25}river|river.{10,25}Tom"),
            "leipzig/symbol-count" => (DotNetPerformanceRegexData.RiverCorpus, @"\p{Sm}"),
            "boostdocs/ftp-line-match" => ("100- this is a line of ftp response which contains a message string", @"^([0-9]+)(\-| |$)(.*)$"),
            "boostdocs/credit-card-match" => ("1234-5678-1234-456", @"(\d{4}[- ]){3}\d{3,4}"),
            "boostdocs/postcode-match" => ("SW1 1ZZ", @"^[a-zA-Z]{1,2}[0-9][0-9A-Za-z]{0,1} {0,1}[0-9][A-Za-z]{2}$"),
            "boostdocs/date-match" => ("12/12/2001", @"^\d{1,2}/\d{1,2}/\d{4}$"),
            "boostdocs/float-match" => ("-3.14159", @"^[-+]?\d*\.?\d*$"),
            _ => throw new ArgumentOutOfRangeException(nameof(CaseId)),
        };

        _inputBytes = Encoding.UTF8.GetBytes(_input);
        _regex = new Regex(pattern, Options, Regex.InfiniteMatchTimeout);
        _utf8Regex = new Utf8Regex(pattern, Options);
    }

    [Benchmark(Baseline = true)]
    public int Utf8Regex()
    {
        return CaseId.Contains("match", StringComparison.Ordinal)
            ? (_utf8Regex.IsMatch(_inputBytes) ? 1 : 0)
            : _utf8Regex.Count(_inputBytes);
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        var decoded = Encoding.UTF8.GetString(_inputBytes);
        return CaseId.Contains("match", StringComparison.Ordinal)
            ? (_regex.IsMatch(decoded) ? 1 : 0)
            : _regex.Count(decoded);
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        return CaseId.Contains("match", StringComparison.Ordinal)
            ? (_regex.IsMatch(_input) ? 1 : 0)
            : _regex.Count(_input);
    }
}

[MemoryDiagnoser]
[BenchmarkCategory("LokadReplica", "PublicIndustry", "SliceSlice")]
public class DotNetPerformanceRegexSliceSliceBenchmarks
{
    private Regex[] _regexes = [];
    private Utf8Regex[] _utf8Regexes = [];
    private byte[] _inputBytes = [];
    private string _input = string.Empty;

    [Params(RegexOptions.None, RegexOptions.IgnoreCase)]
    public RegexOptions Options { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _input = DotNetPerformanceRegexData.DetectiveCorpus;
        _inputBytes = Encoding.UTF8.GetBytes(_input);
        var words = Regex.Matches(_input, @"\b(\w+)\b", RegexOptions.CultureInvariant)
            .Cast<Match>()
            .Select(static m => m.Groups[1].Value)
            .Distinct(StringComparer.Ordinal)
            .Take(512)
            .ToArray();

        _regexes = words.Select(w => new Regex(w, Options, Regex.InfiniteMatchTimeout)).ToArray();
        _utf8Regexes = words.Select(w => new Utf8Regex(w, Options)).ToArray();
    }

    [Benchmark(Baseline = true)]
    [MinIterationCount(3)]
    public int Utf8Regex()
    {
        var total = 0;
        foreach (var regex in _utf8Regexes)
        {
            total += regex.Count(_inputBytes);
        }

        return total;
    }

    [Benchmark]
    [MinIterationCount(3)]
    public int PredecodedRegex()
    {
        var total = 0;
        foreach (var regex in _regexes)
        {
            total += regex.Count(_input);
        }

        return total;
    }
}
