using Lokad.Utf8Regex.Internal.Search;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class LokadReplicaScriptBenchmarkContext
{
    private static ReadOnlySpan<byte> ImportPrefix => "import"u8;
    private static ReadOnlySpan<byte> ImportOptionalSegment => "shared"u8;
    private static ReadOnlySpan<byte> RegionMarkerPrefix => "///#"u8;
    private static readonly Utf8AsciiLiteralFinder s_importFinder = new(ImportPrefix);
    private static readonly Utf8AsciiLiteralFinder s_regionMarkerFinder = new(RegionMarkerPrefix);

    public LokadReplicaScriptBenchmarkContext(LokadReplicaScriptBenchmarkCase benchmarkCase)
    {
        BenchmarkCase = benchmarkCase;
        Pattern = benchmarkCase.Pattern;
        Regex = new Regex(Pattern, benchmarkCase.Utf8Options, Regex.InfiniteMatchTimeout);
        CompiledRegex = new Regex(Pattern, benchmarkCase.DotNetOptions | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
        Utf8Regex = new Utf8Regex(Pattern, options: benchmarkCase.Utf8Options);
        CompiledUtf8Regex = new Utf8Regex(Pattern, options: benchmarkCase.Utf8Options | RegexOptions.Compiled);
        if (Regex.Options != Utf8Regex.Options || CompiledRegex.Options != CompiledUtf8Regex.Options)
            throw new InvalidOperationException("Benchmark producer/comparator compilation options must match.");

        switch (benchmarkCase.Model)
        {
            case LokadReplicaScriptBenchmarkModel.Count:
                InputString = LoadCorpus();
                InputBytes = Encoding.UTF8.GetBytes(InputString);
                Samples = [];
                SampleBytes = [];
                break;

            case LokadReplicaScriptBenchmarkModel.PrefixMatchLoop:
                Samples = LoadSamples(benchmarkCase.SampleRelativePath, benchmarkCase.AppendNewLineToSamples);
                SampleBytes = Samples.Select(static s => Encoding.UTF8.GetBytes(s)).ToArray();
                InputString = string.Empty;
                InputBytes = [];
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(benchmarkCase.Model));
        }
    }

    public LokadReplicaScriptBenchmarkCase BenchmarkCase { get; }

    public string Pattern { get; }

    public Regex Regex { get; }

    public Regex CompiledRegex { get; }

    public Utf8Regex Utf8Regex { get; }

    public Utf8Regex CompiledUtf8Regex { get; }

    public string InputString { get; }

    public byte[] InputBytes { get; }

    public string[] Samples { get; }

    public byte[][] SampleBytes { get; }

    public int InputChars => BenchmarkCase.Model == LokadReplicaScriptBenchmarkModel.Count ? InputString.Length : Samples.Sum(static s => s.Length);

    public int TotalInputBytes => BenchmarkCase.Model == LokadReplicaScriptBenchmarkModel.Count ? InputBytes.Length : SampleBytes.Sum(static s => s.Length);

    // Lexer callers consume native coordinates: UTF-8 bytes versus UTF-16 units.
    // Verify equivalent matched text before timing, then normalize each lane's
    // sink against its own expected units without charging a conversion that
    // neither caller requests.
    internal (int Bytes, int Units) VerifyPrefixMatchResults()
    {
        EnsurePrefixMatchLoop();
        var bytes = 0;
        var units = 0;
        for (var index = 0; index < Samples.Length; index++)
        {
            var expected = Verify(Regex, Utf8Regex, Samples[index], SampleBytes[index]);
            _ = Verify(CompiledRegex, CompiledUtf8Regex, Samples[index], SampleBytes[index]);
            bytes += expected.Bytes;
            units += expected.Units;
        }
        return (bytes, units);

        static (int Bytes, int Units) Verify(Regex oracle, Utf8Regex actual, string text, byte[] input)
        {
            var matches = oracle.EnumerateMatches(text);
            var match = actual.Match(input);
            if (match.Success != matches.MoveNext())
                throw new InvalidOperationException("Lexer benchmark match success disagrees.");
            if (!match.Success)
                return (0, 0);

            var expected = matches.Current;
            var bytes = Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length));
            if (match.StartOffsetInBytes != Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)) ||
                match.EndOffsetInBytes - match.StartOffsetInBytes != bytes)
                throw new InvalidOperationException("Lexer benchmark match ranges disagree after UTF-8 coordinate projection.");
            return (bytes, expected.Length);
        }
    }

    public int ExecuteUtf8Regex()
    {
        return BenchmarkCase.Model switch
        {
            LokadReplicaScriptBenchmarkModel.Count => Utf8Regex.Count(InputBytes),
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecuteUtf8PrefixMatchLoop(),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    public int ExecuteDecodeThenRegex()
    {
        return BenchmarkCase.Model switch
        {
            LokadReplicaScriptBenchmarkModel.Count => Regex.Count(Encoding.UTF8.GetString(InputBytes)),
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecuteDecodeThenDotNetPrefixMatchLoop(Regex),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    public int ExecuteDecodeThenCompiledRegex()
    {
        return BenchmarkCase.Model switch
        {
            LokadReplicaScriptBenchmarkModel.Count => CompiledRegex.Count(Encoding.UTF8.GetString(InputBytes)),
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecuteDecodeThenDotNetPrefixMatchLoop(CompiledRegex),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    public int ExecutePredecodedRegex()
    {
        return BenchmarkCase.Model switch
        {
            LokadReplicaScriptBenchmarkModel.Count => Regex.Count(InputString),
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecutePredecodedDotNetPrefixMatchLoop(Regex),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    public int ExecutePredecodedCompiledRegex()
    {
        return BenchmarkCase.Model switch
        {
            LokadReplicaScriptBenchmarkModel.Count => CompiledRegex.Count(InputString),
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecutePredecodedDotNetPrefixMatchLoop(CompiledRegex),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    public int ExecuteUtf8PrefixPrefilterOnly()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            if (!Utf8PrefilterReplay.Rejects(Utf8Regex.Inspection.SearchPlan, sample))
            {
                total++;
            }
        }

        return total;
    }

    public int ExecuteUtf8PrefixCompiledMatchOnly()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            var match = Utf8MatchInspection.MatchCompiled(CompiledUtf8Regex, sample);
            if (match.Success)
            {
                total += match.Success ? (match.EndOffsetInBytes - match.StartOffsetInBytes) : (match.EndOffsetInBytes - match.StartOffsetInBytes);
            }
        }

        return total;
    }

    public int ExecuteLokadScriptDirectUrlMatcher()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        if (BenchmarkCase.Id is not ("lokad/langserv/url-dashboard" or "lokad/langserv/url-download"))
        {
            return 0;
        }

        foreach (var sample in SampleBytes)
        {
            var matched = BenchmarkCase.Id switch
            {
                "lokad/langserv/url-dashboard" => LokadScriptUrlPatternMatcher.IsDashboardMatch(sample),
                "lokad/langserv/url-download" => LokadScriptUrlPatternMatcher.IsDownloadMatch(sample),
                _ => false,
            };

            if (matched)
            {
                total += sample.Length;
            }
        }

        return total;
    }

    public int ExecuteLinePrefixModel()
    {
        EnsureCountModel();
        return BenchmarkCase.Id switch
        {
            "lokad/imports/module-imports" => Utf8LinePrefixExecutor.CountLinesWithPrefix(InputBytes, ImportPrefix, trimLeadingAsciiWhitespace: false),
            "lokad/folding/region-marker" => Utf8LinePrefixExecutor.CountLinesWithPrefix(InputBytes, RegionMarkerPrefix, trimLeadingAsciiWhitespace: true),
            _ => 0,
        };
    }

    public int ExecuteLiteralFinderModel()
    {
        EnsureCountModel();
        return BenchmarkCase.Id switch
        {
            "lokad/imports/module-imports" => CountLiteralHits(s_importFinder),
            "lokad/folding/region-marker" => CountLiteralHits(s_regionMarkerFinder),
            _ => 0,
        };
    }

    public int ExecuteLineVerifierModel()
    {
        EnsureCountModel();
        return BenchmarkCase.Id switch
        {
            "lokad/imports/module-imports" => ExecuteImportLineVerifierModel(),
            "lokad/folding/region-marker" => Utf8LinePrefixExecutor.CountMatchingLines(
                InputBytes,
                s_regionMarkerFinder,
                trimLeadingWhitespace: true),
            _ => 0,
        };
    }

    public int ExecuteImportLineWalkModel()
    {
        EnsureCountModel();
        return BenchmarkCase.Id == "lokad/imports/module-imports"
            ? (Utf8QuotedLineSegmentExecutor.TryCountViaLineWalk(InputBytes, ImportPrefix, ImportOptionalSegment, out var count) ? count : Regex.Count(InputString))
            : 0;
    }

    public int ExecuteUtf8CountPrefilterOnly()
    {
        EnsureCountModel();
        return Utf8PrefilterReplay.Rejects(Utf8Regex.Inspection.SearchPlan, InputBytes) ? 1 : 0;
    }

    public int ExecuteUtf8CountCompiled()
    {
        EnsureCountModel();
        return CompiledUtf8Regex.Count(InputBytes);
    }


    public int ExecuteUtf8CountFallbackCandidates()
    {
        EnsureCountModel();
        return Utf8FallbackReplay.CountCandidates(Utf8Regex.Inspection.SearchPlan, InputBytes, false);
    }

    public int ExecuteUtf8CountFallbackBoundaryCandidates()
    {
        EnsureCountModel();
        return Utf8FallbackReplay.CountCandidates(Utf8Regex.Inspection.SearchPlan, InputBytes, true);
    }

    public int ExecuteUtf8CountFallbackVerified()
    {
        EnsureCountModel();
        return Utf8FallbackReplay.Count(Utf8Regex, InputBytes);
    }

    private int ExecuteUtf8PrefixMatchLoop()
    {
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            var match = Utf8Regex.Match(sample);
            if (match.Success)
            {
                total += match.EndOffsetInBytes - match.StartOffsetInBytes;
            }
        }

        return total;
    }

    public int ExecuteUtf8PrefixDirectHookOnly()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            Utf8Regex.TryMatchDirectWithoutValidation(sample, out var match);
            if (match.Success)
            {
                total += match.Success ? (match.EndOffsetInBytes - match.StartOffsetInBytes) : (match.EndOffsetInBytes - match.StartOffsetInBytes);
            }
        }

        return total;
    }

    private int ExecuteImportLineVerifierModel()
    {
        return Utf8QuotedLineSegmentExecutor.TryCountViaLineWalk(InputBytes, ImportPrefix, ImportOptionalSegment, out var count)
            ? count : Regex.Count(InputString);
    }

    private int CountLiteralHits(Utf8AsciiLiteralFinder finder)
    {
        var count = 0;
        var startIndex = 0;
        while (finder.TryFindNext(InputBytes, startIndex, out var index))
        {
            count++;
            startIndex = index + 1;
        }

        return count;
    }

    public int ExecuteUtf8Compiled()
    {
        return BenchmarkCase.Model switch
        {
            LokadReplicaScriptBenchmarkModel.Count => CompiledUtf8Regex.Count(InputBytes),
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecuteUtf8CompiledPrefixMatchLoop(),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    private void EnsurePrefixMatchLoop()
    {
        if (BenchmarkCase.Model != LokadReplicaScriptBenchmarkModel.PrefixMatchLoop)
        {
            throw new InvalidOperationException("This Lokad script drilldown only applies to PrefixMatchLoop cases.");
        }
    }

    private void EnsureCountModel()
    {
        if (BenchmarkCase.Model != LokadReplicaScriptBenchmarkModel.Count)
        {
            throw new InvalidOperationException("This Lokad script drilldown only applies to Count cases.");
        }
    }

    private int ExecuteDecodeThenDotNetPrefixMatchLoop(Regex regex)
    {
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            var matches = regex.EnumerateMatches(Encoding.UTF8.GetString(sample));
            if (matches.MoveNext())
                total += matches.Current.Length;
        }

        return total;
    }

    private int ExecutePredecodedDotNetPrefixMatchLoop(Regex regex)
    {
        var total = 0;
        foreach (var sample in Samples)
        {
            var matches = regex.EnumerateMatches(sample);
            if (matches.MoveNext())
                total += matches.Current.Length;
        }

        return total;
    }

    private int ExecuteUtf8CompiledPrefixMatchLoop()
    {
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            var match = CompiledUtf8Regex.Match(sample);
            if (match.Success)
            {
                total += match.EndOffsetInBytes - match.StartOffsetInBytes;
            }
        }

        return total;
    }

    private static string LoadCorpus()
    {
        var dataRoot = Path.Combine(AppContext.BaseDirectory, "LokadReplica", "Script", "Data", "Corpus");
        if (!Directory.Exists(dataRoot))
        {
            throw new DirectoryNotFoundException($"Lokad script corpus root not found: {dataRoot}");
        }

        var files = Directory.GetFiles(dataRoot, "*.nvn", SearchOption.AllDirectories)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException($"Lokad script corpus root is empty: {dataRoot}");
        }

        var builder = new StringBuilder(capacity: 1_000_000);
        foreach (var file in files)
        {
            var relativePath = Path.GetRelativePath(dataRoot, file).Replace('\\', '/');
            builder.Append("// file: ");
            builder.Append(relativePath);
            builder.Append('\n');
            builder.Append(File.ReadAllText(file, Encoding.UTF8));
            if (builder.Length == 0 || builder[^1] != '\n')
            {
                builder.Append('\n');
            }

            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string[] LoadSamples(string? sampleRelativePath, bool appendNewLineToSamples)
    {
        if (string.IsNullOrWhiteSpace(sampleRelativePath))
        {
            throw new InvalidOperationException("PrefixMatchLoop cases must define a sample file.");
        }

        var samplePath = Path.Combine(AppContext.BaseDirectory, "LokadReplica", "Script", "Data", sampleRelativePath);
        var lines = File.ReadAllLines(samplePath, Encoding.UTF8)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();
        if (appendNewLineToSamples)
        {
            for (var i = 0; i < lines.Length; i++)
            {
                lines[i] += "\n";
            }
        }

        return lines;
    }
}

