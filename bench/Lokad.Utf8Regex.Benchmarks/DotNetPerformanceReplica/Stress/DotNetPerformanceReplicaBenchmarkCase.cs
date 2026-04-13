using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal enum DotNetPerformanceReplicaBenchmarkModel : byte
{
    Count = 0,
    Compile = 1,
}

internal sealed class DotNetPerformanceReplicaBenchmarkCase
{
    public DotNetPerformanceReplicaBenchmarkCase(
        string id,
        string group,
        DotNetPerformanceReplicaBenchmarkModel model,
        string? pattern,
        string? regexRelativePath,
        bool regexPerLineAlternate,
        string? haystackRelativePath,
        string? haystackInline,
        bool haystackUtf8Lossy,
        int? haystackLineStart,
        int? haystackLineEnd,
        RegexOptions options,
        int? expectedCount,
        bool isDevelopmentSlice,
        string origin)
    {
        Id = id;
        Group = group;
        Model = model;
        Pattern = pattern;
        RegexRelativePath = regexRelativePath;
        RegexPerLineAlternate = regexPerLineAlternate;
        HaystackRelativePath = haystackRelativePath;
        HaystackInline = haystackInline;
        HaystackUtf8Lossy = haystackUtf8Lossy;
        HaystackLineStart = haystackLineStart;
        HaystackLineEnd = haystackLineEnd;
        Options = options;
        ExpectedCount = expectedCount;
        IsDevelopmentSlice = isDevelopmentSlice;
        Origin = origin;
    }

    public string Id { get; }

    public string Group { get; }

    public DotNetPerformanceReplicaBenchmarkModel Model { get; }

    public string? Pattern { get; }

    public string? RegexRelativePath { get; }

    public bool RegexPerLineAlternate { get; }

    public string? HaystackRelativePath { get; }

    public string? HaystackInline { get; }

    public bool HaystackUtf8Lossy { get; }

    public int? HaystackLineStart { get; }

    public int? HaystackLineEnd { get; }

    public RegexOptions Options { get; }

    public int? ExpectedCount { get; }

    public bool IsDevelopmentSlice { get; }

    public string Origin { get; }
}
