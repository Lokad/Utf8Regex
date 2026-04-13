using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum LokadReplicaCodeBenchmarkModel : byte
{
    Count = 0,
}

internal enum LokadCodePatternMode : byte
{
    Literal = 0,
    Regex = 1,
}

internal sealed class LokadReplicaCodeBenchmarkCase
{
    public LokadReplicaCodeBenchmarkCase(
        string id,
        string group,
        LokadReplicaCodeBenchmarkModel model,
        LokadCodePatternMode patternMode,
        string pattern,
        RegexOptions options,
        int? expectedCount,
        string intent,
        string notes)
    {
        Id = id;
        Group = group;
        Model = model;
        PatternMode = patternMode;
        Pattern = pattern;
        Options = options;
        ExpectedCount = expectedCount;
        Intent = intent;
        Notes = notes;
    }

    public string Id { get; }

    public string Group { get; }

    public LokadReplicaCodeBenchmarkModel Model { get; }

    public LokadCodePatternMode PatternMode { get; }

    public string Pattern { get; }

    public RegexOptions Options { get; }

    public int? ExpectedCount { get; }

    public string Intent { get; }

    public string Notes { get; }
}
