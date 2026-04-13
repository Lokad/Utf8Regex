using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum Utf8RegexBenchmarkOperation : byte
{
    IsMatch = 0,
    Match = 1,
    Count = 2,
    EnumerateMatches = 3,
    Replace = 4,
    EnumerateSplits = 5,
}

internal sealed class Utf8RegexBenchmarkCase
{
    public Utf8RegexBenchmarkCase(
        string id,
        string family,
        Utf8RegexBenchmarkOperation operation,
        string pattern,
        string input,
        RegexOptions options,
        string? replacement,
        string expectedSupport,
        string notes)
    {
        Id = id;
        Family = family;
        Operation = operation;
        Pattern = pattern;
        Input = input;
        Options = options;
        Replacement = replacement;
        ExpectedSupport = expectedSupport;
        Notes = notes;
    }

    public string Id { get; }

    public string Family { get; }

    public Utf8RegexBenchmarkOperation Operation { get; }

    public string Pattern { get; }

    public string Input { get; }

    public RegexOptions Options { get; }

    public string? Replacement { get; }

    public string ExpectedSupport { get; }

    public string Notes { get; }
}
