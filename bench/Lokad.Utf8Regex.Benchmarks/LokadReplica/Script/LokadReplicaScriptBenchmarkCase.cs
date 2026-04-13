using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum LokadReplicaScriptBenchmarkModel : byte
{
    Count = 0,
    PrefixMatchLoop = 1,
}

internal sealed class LokadReplicaScriptBenchmarkCase
{
    public LokadReplicaScriptBenchmarkCase(
        string id,
        string group,
        LokadReplicaScriptBenchmarkModel model,
        string pattern,
        RegexOptions dotNetOptions,
        string notes,
        string? sampleRelativePath = null,
        bool appendNewLineToSamples = false)
    {
        Id = id;
        Group = group;
        Model = model;
        Pattern = pattern;
        DotNetOptions = dotNetOptions;
        Utf8Options = dotNetOptions & ~RegexOptions.Compiled;
        Notes = notes;
        SampleRelativePath = sampleRelativePath;
        AppendNewLineToSamples = appendNewLineToSamples;
    }

    public string Id { get; }

    public string Group { get; }

    public LokadReplicaScriptBenchmarkModel Model { get; }

    public string Pattern { get; }

    public RegexOptions DotNetOptions { get; }

    public RegexOptions Utf8Options { get; }

    public string Notes { get; }

    public string? SampleRelativePath { get; }

    public bool AppendNewLineToSamples { get; }
}
