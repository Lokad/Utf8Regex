using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class DotNetPerformanceReplicaBenchmarkContext
{
    public DotNetPerformanceReplicaBenchmarkContext(DotNetPerformanceReplicaBenchmarkCase benchmarkCase)
    {
        BenchmarkCase = benchmarkCase;
        var dataRoot = Path.Combine(AppContext.BaseDirectory, "DotNetPerformanceReplica", "Stress", "Data");
        Pattern = LoadPattern(dataRoot, benchmarkCase);
        InputString = LoadHaystack(dataRoot, benchmarkCase);
        InputBytes = Encoding.UTF8.GetBytes(InputString);
        Utf8Regex = new Utf8Regex(Pattern, benchmarkCase.Options);
        Regex = new Regex(Pattern, benchmarkCase.Options, Regex.InfiniteMatchTimeout);
        CompiledRegex = new Regex(Pattern, benchmarkCase.Options | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
    }

    public DotNetPerformanceReplicaBenchmarkCase BenchmarkCase { get; }

    public string Pattern { get; }

    public string InputString { get; }

    public byte[] InputBytes { get; }

    public Utf8Regex Utf8Regex { get; }

    public Regex Regex { get; }

    public Regex CompiledRegex { get; }

    public int CountDecodeThenRegex() => Regex.Count(Encoding.UTF8.GetString(InputBytes));

    public int CountDecodeThenCompiledRegex() => CompiledRegex.Count(Encoding.UTF8.GetString(InputBytes));

    public int CountPredecodedRegex() => Regex.Count(InputString);

    public int CountPredecodedCompiledRegex() => CompiledRegex.Count(InputString);

    private static string LoadPattern(string dataRoot, DotNetPerformanceReplicaBenchmarkCase benchmarkCase)
    {
        if (benchmarkCase.Pattern is not null)
        {
            return benchmarkCase.Pattern;
        }

        if (benchmarkCase.RegexRelativePath is null)
        {
            throw new InvalidOperationException("Benchmark case is missing a pattern source.");
        }

        var regexPath = Path.Combine(dataRoot, benchmarkCase.RegexRelativePath);
        if (!benchmarkCase.RegexPerLineAlternate)
        {
            return File.ReadAllText(regexPath, Encoding.UTF8);
        }

        var lines = File.ReadAllLines(regexPath, Encoding.UTF8)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        return string.Join("|", lines.Select(Regex.Escape));
    }

    private static string LoadHaystack(string dataRoot, DotNetPerformanceReplicaBenchmarkCase benchmarkCase)
    {
        if (benchmarkCase.HaystackInline is not null)
        {
            return benchmarkCase.HaystackInline;
        }

        if (benchmarkCase.HaystackRelativePath is null)
        {
            throw new InvalidOperationException("Benchmark case is missing a haystack source.");
        }

        var haystackPath = Path.Combine(dataRoot, benchmarkCase.HaystackRelativePath);
        var text = benchmarkCase.HaystackUtf8Lossy
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetString(File.ReadAllBytes(haystackPath))
            : File.ReadAllText(haystackPath, Encoding.UTF8);
        if (benchmarkCase.HaystackLineStart is null && benchmarkCase.HaystackLineEnd is null)
        {
            return text;
        }

        var lines = text.Split('\n');
        var start = Math.Max(0, benchmarkCase.HaystackLineStart ?? 0);
        var endExclusive = benchmarkCase.HaystackLineEnd is int lineEnd ? Math.Min(lines.Length, lineEnd) : lines.Length;
        if (start >= endExclusive)
        {
            return string.Empty;
        }

        return string.Join('\n', lines[start..endExclusive]);
    }
}
