using System.Text;
using System.Text.RegularExpressions;
using BenchmarkDotNet.Attributes;

namespace Lokad.Utf8Regex.Benchmarks;

[MemoryDiagnoser]
[BenchmarkCategory("LokadReplica", "PublicCommon")]
public class DotNetPerformanceRegexCommonBenchmarks
{
    private Utf8Regex _utf8Regex = null!;
    private Regex _regex = null!;
    private string _input = string.Empty;
    private byte[] _inputBytes = [];
    private string _replacement = string.Empty;

    [ParamsSource(nameof(CaseIds))]
    public string CaseId { get; set; } = string.Empty;

    [Params(RegexOptions.None, RegexOptions.Compiled)]
    public RegexOptions Options { get; set; }

    public IEnumerable<string> CaseIds =>
    [
        "email-match",
        "email-miss",
        "date-match",
        "date-miss",
        "ip-match",
        "ip-miss",
        "uri-match",
        "uri-miss",
        "matches-set",
        "matches-boundary",
        "matches-word",
        "matches-words",
        "match-word",
        "replace-words",
        "split-words",
        "backtracking",
        "one-node-backtracking",
    ];

    [GlobalSetup]
    public void Setup()
    {
        (_input, _replacement, var pattern) = CaseId switch
        {
            "email-match" => ("ops.dispatch@northwind-control.net", string.Empty, @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$"),
            "email-miss" => ("ops.dispatch@northwind-control.net#", string.Empty, @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$"),
            "date-match" => ("Today is 11/18/2019 and tomorrow is 11/19/2019.", string.Empty, @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b"),
            "date-miss" => ("Today is 11/18/201A and tomorrow is 11/19/201A.", string.Empty, @"\b\d{1,2}\/\d{1,2}\/\d{2,4}\b"),
            "ip-match" => ("012.200.033.199", string.Empty, @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])"),
            "ip-miss" => ("012.200.033.19A", string.Empty, @"(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9])"),
            "uri-match" => ("https://atlas.example.org/reports/export?id=42", string.Empty, @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?"),
            "uri-miss" => ("https://a https://b", string.Empty, @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?"),
            "matches-set" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"\w{10,}"),
            "matches-boundary" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"\b\w{10,}\b"),
            "matches-word" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus"),
            "matches-words" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus|magna|semper"),
            "match-word" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus|magna|semper"),
            "replace-words" => (DotNetPerformanceRegexData.CommonSearchText, "amoveatur", @"tempus|magna|semper"),
            "split-words" => (DotNetPerformanceRegexData.CommonSearchText, string.Empty, @"tempus|magna|semper"),
            "backtracking" => ("Essential services are provided by regular exprs.", string.Empty, ".*(ss)"),
            "one-node-backtracking" => ("This regex has the potential to be optimized further", string.Empty, @"[^a]+\.[^z]+"),
            _ => throw new ArgumentOutOfRangeException(nameof(CaseId)),
        };

        _inputBytes = Encoding.UTF8.GetBytes(_input);
        _regex = new Regex(pattern, Options, Regex.InfiniteMatchTimeout);
        _utf8Regex = new Utf8Regex(pattern, Options);
    }

    [Benchmark(Baseline = true)]
    public int Utf8Regex()
    {
        return CaseId switch
        {
            "replace-words" => _utf8Regex.Replace(_inputBytes, _replacement).Length,
            "split-words" => CountUtf8Splits(_utf8Regex, _inputBytes),
            "match-word" => MeasureMatch(_utf8Regex.Match(_inputBytes)),
            "backtracking" or "one-node-backtracking" or "email-match" or "email-miss" or "date-match" or "date-miss" or "ip-match" or "ip-miss" or "uri-match" or "uri-miss"
                => _utf8Regex.IsMatch(_inputBytes) ? 1 : 0,
            _ => _utf8Regex.Count(_inputBytes),
        };
    }

    [Benchmark]
    public int DecodeThenRegex()
    {
        var decoded = Encoding.UTF8.GetString(_inputBytes);
        return ExecuteRegex(_regex, decoded);
    }

    [Benchmark]
    public int PredecodedRegex()
    {
        return ExecuteRegex(_regex, _input);
    }

    private int ExecuteRegex(Regex regex, string text)
    {
        return CaseId switch
        {
            "replace-words" => regex.Replace(text, _replacement).Length,
            "split-words" => regex.Split(text).Length,
            "match-word" => MeasureMatch(regex.Match(text)),
            "backtracking" or "one-node-backtracking" or "email-match" or "email-miss" or "date-match" or "date-miss" or "ip-match" or "ip-miss" or "uri-match" or "uri-miss"
                => regex.IsMatch(text) ? 1 : 0,
            _ => regex.Count(text),
        };
    }

    private static int MeasureMatch(Utf8ValueMatch match) => match.Success ? match.IndexInUtf16 + match.LengthInUtf16 : -1;

    private static int MeasureMatch(Match match) => match.Success ? match.Index + match.Length : -1;

    private static int CountUtf8Splits(Utf8Regex regex, byte[] input)
    {
        var count = 0;
        foreach (var _ in regex.EnumerateSplits(input))
        {
            count++;
        }

        return count;
    }
}

[MemoryDiagnoser]
[BenchmarkCategory("LokadReplica", "PublicCommon", "Construction")]
public class DotNetPerformanceRegexConstructionBenchmarks
{
    private string _pattern = string.Empty;
    private string _input = string.Empty;
    private byte[] _inputBytes = [];

    [Params("warning", "email", "uri")]
    public string CaseId { get; set; } = string.Empty;

    [Params(RegexOptions.None, RegexOptions.Compiled)]
    public RegexOptions Options { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        (_pattern, _input) = CaseId switch
        {
            "warning" => (@"(^(.*)(\(([0-9]+),([0-9]+)\)): )(error|warning) ([A-Z]+[0-9]+) ?: (.*)", "(17,42): warning RX0001 : synthetic parser warning"),
            "email" => (@"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$", "ops.dispatch@northwind-control.net"),
            "uri" => (@"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?", "https://atlas.example.org/reports/export?id=42"),
            _ => throw new ArgumentOutOfRangeException(nameof(CaseId)),
        };

        _inputBytes = Encoding.UTF8.GetBytes(_input);
    }

    [Benchmark(Baseline = true)]
    public int Utf8RegexCtor()
    {
        var regex = new Utf8Regex(_pattern, Options);
        return regex.GetGroupNumbers().Length;
    }

    [Benchmark]
    public int DotNetCtor()
    {
        var regex = new Regex(_pattern, Options, Regex.InfiniteMatchTimeout);
        return regex.GetGroupNumbers().Length;
    }

    [Benchmark]
    public int Utf8RegexCtorInvoke()
    {
        var regex = new Utf8Regex(_pattern, Options);
        return regex.IsMatch(_inputBytes) ? 1 : 0;
    }

    [Benchmark]
    public int DotNetCtorInvoke()
    {
        var regex = new Regex(_pattern, Options, Regex.InfiniteMatchTimeout);
        return regex.IsMatch(_input) ? 1 : 0;
    }
}
