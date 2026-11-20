using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class DotNetPerformanceReplicaBenchmarkContext
{
    public DotNetPerformanceReplicaBenchmarkContext(DotNetPerformanceReplicaBenchmarkCase benchmarkCase)
    {
        BenchmarkCase = benchmarkCase;
        var dataRoot = BenchmarkDataFiles.GetDirectory("DotNetPerformanceReplica/Stress/Data");
        Pattern = LoadPattern(dataRoot, benchmarkCase);
        InputString = LoadHaystack(dataRoot, benchmarkCase);
        InputBytes = Encoding.UTF8.GetBytes(InputString);
        Utf8Regex = new Utf8Regex(Pattern, options: benchmarkCase.Options);
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
        using var file = File.OpenRead(haystackPath);
        using var decompressed = haystackPath.EndsWith(".gz", StringComparison.Ordinal)
            ? new GZipStream(file, CompressionMode.Decompress, leaveOpen: true) : null;
        var encoding = benchmarkCase.HaystackUtf8Lossy ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false) : Encoding.UTF8;
        using var reader = new StreamReader(decompressed ?? (Stream)file, encoding,
            detectEncodingFromByteOrderMarks: !benchmarkCase.HaystackUtf8Lossy);
        if (benchmarkCase.HaystackLineStart is null or 0 && benchmarkCase.HaystackLineEnd is int prefixLines)
        {
            return ReadLeadingLines(reader, prefixLines);
        }

        var text = reader.ReadToEnd();
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

    private static string ReadLeadingLines(StreamReader reader, int remaining)
    {
        if (remaining <= 0) return string.Empty;

        // Match Split('\n') exactly, including CR and a final empty line.
        var text = new StringBuilder();
        Span<char> buffer = stackalloc char[2048];
        int read;
        while ((read = reader.Read(buffer)) != 0)
        {
            var part = buffer[..read];
            for (var index = 0; index < part.Length; index++)
            {
                if (part[index] == '\n' && --remaining == 0)
                    return text.Append(part[..index]).ToString();
            }
            text.Append(part);
        }
        return text.ToString();
    }
}
