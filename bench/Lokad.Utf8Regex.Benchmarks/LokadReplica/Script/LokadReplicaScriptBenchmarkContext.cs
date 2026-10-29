using Lokad.Utf8Regex.Internal.Search;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Benchmarks;

internal sealed class LokadReplicaScriptBenchmarkContext
{
    private static readonly PreparedByteSetState s_asciiLetters = PreparedByteSetState.FromValues(
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);
    private static readonly PreparedByteSetState s_asciiAlphaNumeric = PreparedByteSetState.FromValues(
        "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8);

    private static ReadOnlySpan<byte> ImportPrefix => "import"u8;
    private static ReadOnlySpan<byte> ImportOptionalSegment => "shared"u8;
    private static ReadOnlySpan<byte> RegionMarkerPrefix => "///#"u8;
    private static ReadOnlySpan<byte> DocLinePrefix => "///"u8;
    private static readonly Utf8AsciiLiteralFinder s_importFinder = new(ImportPrefix);
    private static readonly Utf8AsciiLiteralFinder s_regionMarkerFinder = new(RegionMarkerPrefix);

    public LokadReplicaScriptBenchmarkContext(LokadReplicaScriptBenchmarkCase benchmarkCase)
    {
        BenchmarkCase = benchmarkCase;
        Pattern = benchmarkCase.Pattern;
        Regex = new Regex(Pattern, benchmarkCase.Utf8Options, Regex.InfiniteMatchTimeout);
        CompiledRegex = new Regex(Pattern, benchmarkCase.DotNetOptions | RegexOptions.Compiled, Regex.InfiniteMatchTimeout);
        Utf8Regex = new Utf8Regex(Pattern, benchmarkCase.Utf8Options);
        CompiledUtf8Regex = new Utf8Regex(Pattern, benchmarkCase.Utf8Options | RegexOptions.Compiled);
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
            if (expected.Success)
            {
                units += expected.Length;
                bytes += Encoding.UTF8.GetByteCount(Samples[index].AsSpan(expected.Index, expected.Length));
            }
        }
        return (bytes, units);

        static Match Verify(Regex oracle, Utf8Regex actual, string text, byte[] input)
        {
            var expected = oracle.Match(text);
            var match = actual.Match(input);
            if (match.Success != expected.Success || expected.Success &&
                (match.IndexInUtf16 != expected.Index || match.LengthInUtf16 != expected.Length ||
                 match.IndexInBytes != Encoding.UTF8.GetByteCount(text.AsSpan(0, expected.Index)) ||
                 match.LengthInBytes != Encoding.UTF8.GetByteCount(text.AsSpan(expected.Index, expected.Length))))
                throw new InvalidOperationException("Lexer benchmark match ranges disagree after UTF-8 coordinate projection.");
            return expected;
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
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecuteDecodeThenRegexPrefixMatchLoop(),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    public int ExecuteDecodeThenCompiledRegex()
    {
        return BenchmarkCase.Model switch
        {
            LokadReplicaScriptBenchmarkModel.Count => CompiledRegex.Count(Encoding.UTF8.GetString(InputBytes)),
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecuteDecodeThenCompiledRegexPrefixMatchLoop(),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    public int ExecutePredecodedRegex()
    {
        return BenchmarkCase.Model switch
        {
            LokadReplicaScriptBenchmarkModel.Count => Regex.Count(InputString),
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecutePredecodedRegexPrefixMatchLoop(),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    public int ExecutePredecodedCompiledRegex()
    {
        return BenchmarkCase.Model switch
        {
            LokadReplicaScriptBenchmarkModel.Count => CompiledRegex.Count(InputString),
            LokadReplicaScriptBenchmarkModel.PrefixMatchLoop => ExecutePredecodedCompiledRegexPrefixMatchLoop(),
            _ => throw new ArgumentOutOfRangeException(nameof(BenchmarkCase.Model)),
        };
    }

    public int ExecuteUtf8PrefixValidationOnly()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            total += Utf8Validation.Validate(sample).Utf16Length;
        }

        return total;
    }

    public int ExecuteUtf8PrefixWellFormedOnly()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            Utf8Validation.ThrowIfInvalidOnly(sample);
            total += sample.Length;
        }

        return total;
    }

    public int ExecuteUtf8PrefixPrefilterOnly()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            if (!Utf8PrefilterExecutor.Rejects(Utf8Regex.Inspection.SearchPlan.PrefilterPlan, sample))
            {
                total++;
            }
        }

        return total;
    }

    public int ExecuteUtf8PrefixCompiledAfterValidation()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            var validation = Utf8Validation.Validate(sample);
            var match = Utf8MatchInspection.MatchCompiled(CompiledUtf8Regex, sample, validation);
            if (match.Success)
            {
                total += match.IsByteAligned ? match.LengthInBytes : match.LengthInUtf16;
            }
        }

        return total;
    }

    public int ExecuteLokadScriptLexerPrimitiveOnly()
    {
        EnsurePrefixMatchLoop();
        return BenchmarkCase.Id switch
        {
            "lokad/lexer/identifier" => ExecuteAcrossSamples(static sample =>
                Utf8AsciiPrefixTokenExecutor.TryMatchIdentifierPrefixIgnoreCase(sample, out var matchedLength) ? matchedLength : 0),
            "lokad/lexer/number" => ExecuteAcrossSamples(static sample =>
                Utf8AsciiPrefixTokenExecutor.TryMatchNumberPrefix(sample, out var matchedLength) ? matchedLength : 0),
            "lokad/lexer/operator-run" => ExecuteAcrossSamples(static sample =>
                Utf8AsciiPrefixTokenExecutor.TryMatchOperatorRunPrefix(sample, out var matchedLength) ? matchedLength : 0),
            "lokad/lexer/doc-line" => ExecuteAcrossSamples(static sample =>
                Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(sample, DocLinePrefix, (byte)'\n', out var matchedLength) ? matchedLength : 0),
            "lokad/lexer/string" => ExecuteAcrossSamples(static sample =>
                Utf8AsciiPrefixTokenExecutor.TryMatchQuotedStringPrefix(sample, out var matchedLength) ? matchedLength : 0),
            _ => throw new InvalidOperationException($"No direct Lokad script lexer primitive is defined for case '{BenchmarkCase.Id}'."),
        };
    }

    public int ExecuteLokadScriptLexerWellFormedPrimitiveOnly()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            Utf8Validation.ThrowIfInvalidOnly(sample);
            total += BenchmarkCase.Id switch
            {
                "lokad/lexer/identifier" => Utf8AsciiPrefixTokenExecutor.TryMatchIdentifierPrefixIgnoreCase(sample, out var matchedLength) ? matchedLength : 0,
                "lokad/lexer/number" => Utf8AsciiPrefixTokenExecutor.TryMatchNumberPrefix(sample, out var matchedLength) ? matchedLength : 0,
                "lokad/lexer/operator-run" => Utf8AsciiPrefixTokenExecutor.TryMatchOperatorRunPrefix(sample, out var matchedLength) ? matchedLength : 0,
                "lokad/lexer/doc-line" => Utf8AsciiPrefixTokenExecutor.TryMatchAnchoredPrefixUntilByte(sample, DocLinePrefix, (byte)'\n', out var matchedLength) ? matchedLength : 0,
                "lokad/lexer/string" => Utf8AsciiPrefixTokenExecutor.TryMatchQuotedStringPrefix(sample, out var matchedLength) ? matchedLength : 0,
                _ => throw new InvalidOperationException($"No direct Lokad script lexer primitive is defined for case '{BenchmarkCase.Id}'."),
            };
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

    public int ExecuteAsciiTokenFinderModel()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            if (Utf8AsciiTokenFinderExecutor.TryFindNext(sample, 0, s_asciiLetters, s_asciiAlphaNumeric, out _, out var matchedLength))
            {
                total += matchedLength;
            }
        }

        return total;
    }

    public int ExecuteAnchoredValidatorNativeOnly()
    {
        EnsurePrefixMatchLoop();
        var simplePatternPlan = Utf8Regex.Inspection.SimplePatternPlan;
        if (!simplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        var allowTrailingNewline = simplePatternPlan.AllowsTrailingNewlineBeforeEnd;
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            if ((simplePatternPlan.AnchoredHeadTailRunPlan.HasValue &&
                 Utf8AsciiAnchoredValidatorExecutor.TryMatchWhole(sample, simplePatternPlan.AnchoredHeadTailRunPlan, allowTrailingNewline, out var matchedLength)) ||
                Utf8AsciiAnchoredValidatorExecutor.TryMatchWhole(sample, simplePatternPlan.AnchoredValidatorPlan, allowTrailingNewline, out matchedLength))
            {
                total += matchedLength;
            }
        }

        return total;
    }

    public int ExecuteUtf8PrefixCompiledBoolAfterValidation()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            var validation = Utf8Validation.Validate(sample);
            if (Utf8MatchInspection.IsMatchCompiled(CompiledUtf8Regex, sample, validation))
            {
                total += sample.Length;
            }
        }

        return total;
    }

    public int ExecuteUtf8PrefixCompiledDirectHookOnly()
    {
        EnsurePrefixMatchLoop();
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            CompiledUtf8Regex.TryMatchDirectWithoutValidation(sample, out var match);
            if (match.Success)
            {
                total += match.IsByteAligned ? match.LengthInBytes : match.LengthInUtf16;
            }
        }

        return total;
    }

    public int ExecuteAnchoredHeadTailBoolOnly()
    {
        EnsurePrefixMatchLoop();
        if (!Utf8Regex.Inspection.SimplePatternPlan.AnchoredHeadTailRunPlan.HasValue)
        {
            return 0;
        }

        var total = 0;
        foreach (var sample in SampleBytes)
        {
            if (Utf8Regex.TryIsMatchAnchoredHeadTailWithoutValidation(sample, out var isMatch) && isMatch)
            {
                total += sample.Length;
            }
        }

        return total;
    }

    public int ExecuteAnchoredHeadTailMatchOnly()
    {
        EnsurePrefixMatchLoop();
        if (!Utf8Regex.Inspection.SimplePatternPlan.AnchoredHeadTailRunPlan.HasValue)
        {
            return 0;
        }

        var total = 0;
        foreach (var sample in SampleBytes)
        {
            if (Utf8Regex.TryMatchAnchoredHeadTailWithoutValidation(sample, out var match) && match.Success)
            {
                total += match.LengthInUtf16;
            }
        }

        return total;
    }

    public int ExecuteAsciiSimplePatternDirectBoolOnly()
    {
        EnsurePrefixMatchLoop();
        if (!Utf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        var total = 0;
        foreach (var sample in SampleBytes)
        {
            if (Utf8Regex.TryIsMatchAsciiSimplePatternWithoutValidation(sample, out var isMatch) && isMatch)
            {
                total += sample.Length;
            }
        }

        return total;
    }

    public int ExecuteAsciiSimplePatternDirectMatchOnly()
    {
        EnsurePrefixMatchLoop();
        if (!Utf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        var total = 0;
        foreach (var sample in SampleBytes)
        {
            if (Utf8Regex.TryMatchAsciiSimplePatternWithoutValidation(sample, out var match) && match.Success)
            {
                total += match.LengthInUtf16;
            }
        }

        return total;
    }

    public int ExecuteWholeMatchProjectionOnly()
    {
        EnsurePrefixMatchLoop();
        if (!Utf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan.HasValue)
        {
            return 0;
        }

        var total = 0;
        foreach (var sample in SampleBytes)
        {
            total += Utf8MatchInspection.ProjectWholeMatchOnly(sample.Length);
        }

        return total;
    }

    public int ExecuteAnchoredValidatorEmittedOnly()
    {
        EnsurePrefixMatchLoop();
        var plan = CompiledUtf8Regex.Inspection.SimplePatternPlan.AnchoredValidatorPlan;
        if (!plan.HasValue ||
            !Utf8EmittedWholeMatcher.TryCreate(
                plan,
                allowTrailingNewline: BenchmarkCase.Pattern.EndsWith('$'),
                out var matcher) ||
            matcher is null)
        {
            return 0;
        }

        var total = 0;
        foreach (var sample in SampleBytes)
        {
            var matchedLength = matcher.MatchWhole(sample);
            if (matchedLength >= 0)
            {
                total += matchedLength;
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

    public int ExecuteUtf8CountValidationOnly()
    {
        EnsureCountModel();
        return Utf8Validation.Validate(InputBytes).Utf16Length;
    }

    public int ExecuteUtf8CountWellFormedOnly()
    {
        EnsureCountModel();
        Utf8Validation.ThrowIfInvalidOnly(InputBytes);
        return InputBytes.Length;
    }

    public int ExecuteUtf8CountPrefilterOnly()
    {
        EnsureCountModel();
        return Utf8PrefilterExecutor.Rejects(Utf8Regex.Inspection.SearchPlan.PrefilterPlan, InputBytes) ? 1 : 0;
    }

    public int ExecuteUtf8CountCompiled()
    {
        EnsureCountModel();
        return CompiledUtf8Regex.Count(InputBytes);
    }

    public int ExecuteUtf8CountCompiledDirect()
    {
        EnsureCountModel();
        return CompiledUtf8Regex.CountViaCompiledEngine(InputBytes, default, Utf8ExecutionDeadline.Infinite);
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
                total += match.IsByteAligned ? match.LengthInBytes : match.LengthInUtf16;
            }
        }

        return total;
    }

    private int ExecuteAcrossSamples(Func<byte[], int> projector)
    {
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            total += projector(sample);
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
                total += match.IsByteAligned ? match.LengthInBytes : match.LengthInUtf16;
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

    private int ExecuteDecodeThenRegexPrefixMatchLoop()
    {
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            var decoded = Encoding.UTF8.GetString(sample);
            var match = Regex.Match(decoded);
            if (match.Success)
            {
                total += match.Length;
            }
        }

        return total;
    }

    private int ExecuteDecodeThenCompiledRegexPrefixMatchLoop()
    {
        var total = 0;
        foreach (var sample in SampleBytes)
        {
            var decoded = Encoding.UTF8.GetString(sample);
            var match = CompiledRegex.Match(decoded);
            if (match.Success)
            {
                total += match.Length;
            }
        }

        return total;
    }

    private int ExecutePredecodedRegexPrefixMatchLoop()
    {
        var total = 0;
        foreach (var sample in Samples)
        {
            var match = Regex.Match(sample);
            if (match.Success)
            {
                total += match.Length;
            }
        }

        return total;
    }

    private int ExecutePredecodedCompiledRegexPrefixMatchLoop()
    {
        var total = 0;
        foreach (var sample in Samples)
        {
            var match = CompiledRegex.Match(sample);
            if (match.Success)
            {
                total += match.Length;
            }
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
                total += match.IsByteAligned ? match.LengthInBytes : match.LengthInUtf16;
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

    private static bool IsDashboardUrlMatch(ReadOnlySpan<byte> input)
    {
        var dashboardIndex = input.IndexOf("/d/"u8);
        if (dashboardIndex < 0)
        {
            return false;
        }

        var tabIndex = input[(dashboardIndex + 3)..].IndexOf("?t="u8);
        if (tabIndex < 0)
        {
            return false;
        }

        var idStart = dashboardIndex + 3;
        var idLength = tabIndex;
        if (idLength <= 0 || input[idStart..(idStart + idLength)].IndexOfAnyExceptInRange((byte)'0', (byte)'9') >= 0)
        {
            return false;
        }

        return true;
    }

    private static bool IsDownloadUrlMatch(ReadOnlySpan<byte> input)
    {
        var downloadIndex = input.IndexOf("/gateway/BigFiles/Browse/Download?hash="u8);
        if (downloadIndex < 0)
        {
            return false;
        }

        var hashStart = downloadIndex + "/gateway/BigFiles/Browse/Download?hash=".Length;
        if ((uint)hashStart >= (uint)input.Length)
        {
            return false;
        }

        var nameRelative = input[hashStart..].IndexOf("&name="u8);
        if (nameRelative < 0)
        {
            return false;
        }

        var hash = input[hashStart..(hashStart + nameRelative)];
        if (hash.IsEmpty)
        {
            return false;
        }

        foreach (var value in hash)
        {
            var folded = (byte)(value | 0x20);
            if (!((uint)(value - '0') <= 9 || (uint)(folded - 'a') <= ('f' - 'a')))
            {
                return false;
            }
        }

        return true;
    }
}

