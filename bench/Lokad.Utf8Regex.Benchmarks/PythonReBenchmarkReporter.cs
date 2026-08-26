using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const string SnapshotFileName = "PythonRe.Benchmarks.json";
    private const string CpythonRunnerRelativePath = "bench/Lokad.Utf8Regex.Benchmarks/pythonre_cpython_benchmark.py";
    private const int PythonReBenchmarkSchemaVersion = 6;
    private const int CpythonProtocolVersion = 1;
    private static int s_sink;
    private static object? s_retainedSink;

    internal static bool TryHandleCommand(string[] args, out int exitCode)
    {
        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-case", StringComparison.Ordinal))
        {
            exitCode = MeasureCase(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 5));
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-paired-case", StringComparison.Ordinal))
        {
            exitCode = MeasurePairedCase(
                args[1],
                Math.Min(ParsePositive(args, 2, 9), 17),
                cpythonFirst: false,
                PythonReQualificationWriteMode.None);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-paired-case-reversed", StringComparison.Ordinal))
        {
            exitCode = MeasurePairedCase(
                args[1],
                Math.Min(ParsePositive(args, 2, 9), 17),
                cpythonFirst: true,
                PythonReQualificationWriteMode.None);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--qualify-pythonre-case", StringComparison.Ordinal))
        {
            exitCode = MeasurePairedCase(
                args[1],
                Math.Min(ParsePositive(args, 2, 9), 17),
                cpythonFirst: false,
                PythonReQualificationWriteMode.Snapshot);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--qualify-pythonre-case-reversed", StringComparison.Ordinal))
        {
            exitCode = MeasurePairedCase(
                args[1],
                Math.Min(ParsePositive(args, 2, 9), 17),
                cpythonFirst: true,
                PythonReQualificationWriteMode.Snapshot);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--verify-pythonre-semantic-digests", StringComparison.Ordinal))
        {
            exitCode = VerifyPythonReSemanticDigests();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--verify-pythonre-qualifications", StringComparison.Ordinal))
        {
            exitCode = VerifyPythonReQualifications();
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-shaping-case", StringComparison.Ordinal))
        {
            exitCode = MeasureShapingCase(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 7));
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-findall-phases", StringComparison.Ordinal))
        {
            exitCode = MeasureFindAllPhases(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 7));
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-construction-pattern", StringComparison.Ordinal))
        {
            exitCode = MeasureConstructionPattern(
                args[1],
                Math.Min(ParsePositive(args, 2, 100), 512),
                Math.Min(ParsePositive(args, 3, 7), 15),
                args.Length >= 5 ? args[4] : args[1]);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--measure-pythonre-fullmatch-start-offset", StringComparison.Ordinal))
        {
            exitCode = MeasureFullMatchStartOffset(
                Math.Min(ParsePositive(args, 1, 500), 2_000),
                Math.Min(ParsePositive(args, 2, 9), 15));
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--measure-pythonre-empty-global-shapes", StringComparison.Ordinal))
        {
            exitCode = MeasureEmptyGlobalShapes(
                Math.Min(ParsePositive(args, 1, 500), 2_000),
                Math.Min(ParsePositive(args, 2, 9), 15));
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--measure-pythonre-empty-progression-scaling", StringComparison.Ordinal))
        {
            exitCode = MeasureEmptyProgressionScaling(
                Math.Min(ParsePositive(args, 1, 64), 1_024),
                Math.Min(ParsePositive(args, 2, 50), 500),
                Math.Min(ParsePositive(args, 3, 7), 15));
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--refresh-pythonre-benchmark-case", StringComparison.Ordinal))
        {
            exitCode = RefreshCase(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 5));
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--refresh-pythonre-benchmarks", StringComparison.Ordinal))
        {
            exitCode = Refresh(
                ParsePositive(args, 1, 200),
                ParsePositive(args, 2, 5));
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--migrate-pythonre-benchmark-snapshot", StringComparison.Ordinal))
        {
            exitCode = MigratePythonReBenchmarkSnapshot();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--invalidate-pythonre-qualifications", StringComparison.Ordinal))
        {
            exitCode = InvalidatePythonReQualifications();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pythonre-benchmark-markdown", StringComparison.Ordinal))
        {
            exitCode = EmitPythonReBenchmarkMarkdown();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--rewrite-pythonre-benchmark-markdown", StringComparison.Ordinal))
        {
            exitCode = RewritePythonReBenchmarkMarkdown();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--verify-pythonre-benchmark-markdown", StringComparison.Ordinal))
        {
            exitCode = VerifyPythonReBenchmarkMarkdown();
            return true;
        }

        exitCode = 0;
        return false;
    }

    private static int Refresh(int iterations, int samples)
    {
        var measurements = new SortedDictionary<string, PythonReCaseMeasurement>(StringComparer.Ordinal);
        foreach (var benchmarkCase in PythonReBenchmarkCatalog.Cases)
        {
            Console.WriteLine();
            var measurement = Measure(benchmarkCase, iterations, samples);
            Print(benchmarkCase, measurement);
            measurements.Add(benchmarkCase.Id, measurement);
        }

        var snapshot = new PythonReBenchmarkSnapshot
        {
            SchemaVersion = PythonReBenchmarkSchemaVersion,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Corpus = CaptureCorpusProvenance(),
            Cases = measurements,
        };
        WriteSnapshot(snapshot);
        Console.WriteLine();
        Console.WriteLine($"Snapshot           : {Path.GetFullPath(SnapshotFileName)}");
        return 0;
    }

    private static int RefreshCase(string id, int iterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        if (!File.Exists(SnapshotFileName))
        {
            Console.Error.WriteLine($"PythonRe snapshot '{Path.GetFullPath(SnapshotFileName)}' does not exist.");
            return 1;
        }

        var snapshot = JsonSerializer.Deserialize<PythonReBenchmarkSnapshot>(File.ReadAllText(SnapshotFileName));
        if (snapshot is null || snapshot.SchemaVersion != PythonReBenchmarkSchemaVersion)
        {
            Console.Error.WriteLine(
                $"PythonRe selective refresh requires a schema-{PythonReBenchmarkSchemaVersion} snapshot. " +
                "Run --migrate-pythonre-benchmark-snapshot once to migrate the current snapshot.");
            return 1;
        }

        Console.WriteLine();
        var measurement = Measure(benchmarkCase, iterations, samples);
        Print(benchmarkCase, measurement);
        snapshot.Cases[id] = measurement;
        WriteSnapshot(new PythonReBenchmarkSnapshot
        {
            SchemaVersion = PythonReBenchmarkSchemaVersion,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Corpus = CaptureCorpusProvenance(),
            Cases = snapshot.Cases,
        });
        Console.WriteLine();
        Console.WriteLine($"Snapshot           : {Path.GetFullPath(SnapshotFileName)}");
        return 0;
    }

    private static int MigratePythonReBenchmarkSnapshot()
    {
        var snapshotPath = FindRepositoryFile(SnapshotFileName);
        var snapshot = JsonSerializer.Deserialize<PythonReBenchmarkSnapshot>(File.ReadAllText(snapshotPath));
        if (snapshot is null || snapshot.SchemaVersion is not 3 and not 4 and not 5 and
            not PythonReBenchmarkSchemaVersion)
        {
            Console.Error.WriteLine(
                $"PythonRe migration requires a schema-3, schema-4, schema-5, or " +
                $"schema-{PythonReBenchmarkSchemaVersion} snapshot.");
            return 1;
        }

        foreach (var (caseId, measurement) in snapshot.Cases)
        {
            var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
                candidate => candidate.Id.Equals(caseId, StringComparison.Ordinal)) ??
                throw new InvalidOperationException($"PythonRe snapshot contains unknown case '{caseId}'.");
            var context = new PythonReBenchmarkContext(benchmarkCase);
            var byteControl = PythonReBenchmarkCatalog.GetByteControlEligibility(
                benchmarkCase,
                context.InputBytes);
            measurement.ComparatorOwner = PythonReBenchmarkCatalog.GetComparatorOwner(benchmarkCase.Operation);
            measurement.ManagedRoute = context.DescribeManagedRoute();
            measurement.ByteControlEligible = byteControl.IsEligible;
            measurement.ByteControlReason = byteControl.Reason;
            measurement.Qualification = measurement.Qualification?.PairedEvidence is null
                ? PythonReQualificationMeasurement.CreateUnqualified(
                    measurement.Qualification?.StatusReason ??
                    "Historical independent-median evidence predates paired qualification protocol v3.")
                : measurement.Qualification;
        }

        WriteSnapshot(new PythonReBenchmarkSnapshot
        {
            SchemaVersion = PythonReBenchmarkSchemaVersion,
            GeneratedAtUtc = snapshot.GeneratedAtUtc,
            Corpus = snapshot.Corpus,
            Cases = snapshot.Cases,
        });
        Console.WriteLine(
            $"Migrated {SnapshotFileName} to schema {PythonReBenchmarkSchemaVersion}; " +
            "historical rows remain Unqualified.");
        return 0;
    }

    private static int InvalidatePythonReQualifications()
    {
        var snapshot = LoadPythonReBenchmarkSnapshot();
        if (snapshot.SchemaVersion != PythonReBenchmarkSchemaVersion)
        {
            Console.Error.WriteLine(
                $"PythonRe invalidation requires a schema-{PythonReBenchmarkSchemaVersion} snapshot.");
            return 1;
        }

        var invalidated = 0;
        foreach (var measurement in snapshot.Cases.Values)
        {
            if (measurement.Qualification?.PairedEvidence is null)
            {
                continue;
            }

            measurement.Qualification = PythonReQualificationMeasurement.CreateUnqualified(
                "Paired evidence predates the current managed source revision.");
            invalidated++;
        }

        WriteSnapshot(new PythonReBenchmarkSnapshot
        {
            SchemaVersion = PythonReBenchmarkSchemaVersion,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            Corpus = snapshot.Corpus,
            Cases = snapshot.Cases,
        });
        Console.WriteLine($"Invalidated {invalidated} PythonRe paired qualifications.");
        return 0;
    }

    private static void WriteSnapshot(PythonReBenchmarkSnapshot snapshot)
    {
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        var snapshotPath = Path.GetFullPath(SnapshotFileName);
        BenchmarkFileWriter.WriteTextAtomically(snapshotPath, json + Environment.NewLine);

        RewritePythonReBenchmarkMarkdown(snapshot);
    }

    private static int MeasureCase(string id, int iterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        var measurement = Measure(benchmarkCase, iterations, samples);
        Print(benchmarkCase, measurement);
        return 0;
    }

    private static int MeasureShapingCase(string id, int iterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        if (benchmarkCase.Operation is not PythonReBenchmarkOperation.SearchDetailed and
            not PythonReBenchmarkOperation.FindIterDetailed)
        {
            Console.Error.WriteLine(
                $"PythonRe shaping diagnostics require SearchDetailed or FindIterDetailed; '{id}' uses {benchmarkCase.Operation}.");
            return 1;
        }

        var effectiveIterations = GetEffectiveIterations(benchmarkCase, iterations);
        var context = new PythonReBenchmarkContext(benchmarkCase);
        var direct = context.ExecutePredecodedRegex();
        var staged = context.ExecutePredecodedStagedDetailedProjection();
        if (direct != staged)
        {
            throw new InvalidOperationException(
                $"PythonRe shaping diagnostic '{id}' produced incomparable direct/staged sinks: {direct} versus {staged}.");
        }

        Console.WriteLine($"CaseId             : {benchmarkCase.Id}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"InputBytes         : {context.InputBytes.Length}");
        Console.WriteLine($"Iterations         : {effectiveIterations}");
        Console.WriteLine($"Samples            : {samples}");
        PrintOperation("PythonRePublic", MeasureOperation(context.ExecutePythonRe, effectiveIterations, samples));
        PrintOperation("DecodeDirect", MeasureOperation(context.ExecuteDecodeThenRegex, effectiveIterations, samples));
        PrintOperation("PredecodedDirect", MeasureOperation(context.ExecutePredecodedRegex, effectiveIterations, samples));
        PrintOperation("DiscoveryOnly", MeasureOperation(context.ExecutePredecodedDetailedDiscovery, effectiveIterations, samples));
        PrintOperation("PredecodedStaged", MeasureOperation(context.ExecutePredecodedStagedDetailedProjection, effectiveIterations, samples));
        return 0;
    }

    private static int MeasureFindAllPhases(string id, int iterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        if (benchmarkCase.Operation is not PythonReBenchmarkOperation.FindAllStrings and
            not PythonReBenchmarkOperation.FindAllUtf8)
        {
            Console.Error.WriteLine(
                $"PythonRe FindAll phase diagnostics require FindAllStrings or FindAllUtf8; '{id}' uses {benchmarkCase.Operation}.");
            return 1;
        }

        var effectiveIterations = GetEffectiveIterations(benchmarkCase, iterations);
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        var context = new PythonReBenchmarkContext(benchmarkCase);
        if (!context.SupportsCaptureFreeFindAllPhases && !context.SupportsCapturedFindAllPhases)
        {
            Console.Error.WriteLine(
                $"PythonRe captured FindAll phase diagnostics require a pattern that cannot match empty; '{id}' is unsupported.");
            return 1;
        }

        if (context.SupportsCapturedFindAllPhases)
        {
            return MeasureCapturedFindAllPhases(
                benchmarkCase,
                context,
                processorScope,
                effectiveIterations,
                samples);
        }

        var expected = context.ExecutePythonRe();
        var prepared = context.ExecutePreparedCoreRangeProjection();
        var collected = context.ExecuteCoreCollectedProjection();
        var streaming = context.ExecuteCoreStreamingProjection();
        if (expected != prepared || expected != collected || expected != streaming)
        {
            throw new InvalidOperationException(
                $"PythonRe FindAll phase diagnostic '{id}' produced incomparable sinks: " +
                $"public={expected}, prepared={prepared}, collected={collected}, streaming={streaming}.");
        }

        Console.WriteLine($"CaseId             : {benchmarkCase.Id}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"InputBytes         : {context.InputBytes.Length}");
        Console.WriteLine($"MatchCount         : {context.PreparedCoreRangeCount}");
        Console.WriteLine($"Iterations         : {effectiveIterations}");
        Console.WriteLine($"Samples            : {samples}");
        Console.WriteLine($"CpuPolicy          : {processorScope.Policy}");
        Console.WriteLine($"CpuAffinityMask    : {processorScope.AffinityMask}");
        Console.WriteLine("Phase model        : cumulative controls; phase timings are not additive");
        if (benchmarkCase.Operation == PythonReBenchmarkOperation.FindAllUtf8)
        {
            PrintOperation("PythonRePublic", MeasureRetainedOperation(context.ExecutePythonReFindAllUtf8, effectiveIterations, samples));
        }
        else
        {
            PrintOperation("PythonRePublic", MeasureRetainedOperation(context.ExecutePythonReFindAllStrings, effectiveIterations, samples));
        }

        PrintOperation("DecodeComparator", MeasureOperation(context.ExecuteDecodeThenRegex, effectiveIterations, samples));
        PrintOperation("CoreEnumerateOnly", MeasureOperation(context.ExecuteCoreEnumerationOnly, effectiveIterations, samples));
        PrintOperation("CoreCollectRanges", MeasureRetainedOperation(context.CollectCoreRangesArray, effectiveIterations, samples));
        if (benchmarkCase.Operation == PythonReBenchmarkOperation.FindAllUtf8)
        {
            PrintOperation("PreparedProjection", MeasureRetainedOperation(context.ProjectPreparedCoreUtf8, effectiveIterations, samples));
            PrintOperation("CollectedProjection", MeasureRetainedOperation(context.ProjectCollectedCoreUtf8, effectiveIterations, samples));
            PrintOperation("StreamingProjection", MeasureRetainedOperation(context.StreamCoreUtf8, effectiveIterations, samples));
        }
        else
        {
            PrintOperation("PreparedProjection", MeasureRetainedOperation(context.ProjectPreparedCoreStrings, effectiveIterations, samples));
            PrintOperation("CollectedProjection", MeasureRetainedOperation(context.ProjectCollectedCoreStrings, effectiveIterations, samples));
            PrintOperation("StreamingProjection", MeasureRetainedOperation(context.StreamCoreStrings, effectiveIterations, samples));
        }

        PrintOperation("ChecksumTraversal", MeasureOperation(context.ExecutePreparedCoreChecksumTraversal, effectiveIterations, samples));
        return 0;
    }

    private static int MeasureCapturedFindAllPhases(
        PythonReBenchmarkCase benchmarkCase,
        PythonReBenchmarkContext context,
        BenchmarkProcessorScope processorScope,
        int iterations,
        int samples)
    {
        var expected = context.ExecutePythonRe();
        var predecoded = context.ExecutePredecodedCapturedProjectionChecksum();
        var prepared = context.ExecutePreparedCapturedProjectionChecksum();
        var finalShape = context.ExecutePreparedCapturedFinalShapeChecksum();
        if (expected != predecoded || expected != prepared || expected != finalShape)
        {
            throw new InvalidOperationException(
                $"PythonRe captured FindAll phase diagnostic '{benchmarkCase.Id}' produced incomparable sinks: " +
                $"public={expected}, predecoded={predecoded}, prepared={prepared}, final={finalShape}.");
        }

        Console.WriteLine($"CaseId             : {benchmarkCase.Id}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"ManagedRoute       : {context.DescribeManagedRoute()}");
        Console.WriteLine($"InputBytes         : {context.InputBytes.Length}");
        Console.WriteLine($"CaptureCount       : {context.CaptureCount}");
        Console.WriteLine($"MatchCount         : {context.PreparedCapturedMatchCount}");
        Console.WriteLine($"OutputValueCount   : {context.PreparedCapturedValueCount}");
        Console.WriteLine($"OutputUtf8Bytes    : {context.PreparedCapturedOutputUtf8Bytes}");
        Console.WriteLine($"Iterations         : {iterations}");
        Console.WriteLine($"Samples            : {samples}");
        Console.WriteLine($"CpuPolicy          : {processorScope.Policy}");
        Console.WriteLine($"CpuAffinityMask    : {processorScope.AffinityMask}");
        Console.WriteLine("Phase model        : cumulative controls; phase timings are not additive");
        PrintOperation("Utf8DecodeOnly", MeasureRetainedOperation(context.DecodeInput, iterations, samples));
        PrintOperation("PredecodedDiscovery", MeasureOperation(context.ExecuteCapturedDiscoveryOnly, iterations, samples));
        PrintOperation("CaptureRangeCollect", MeasureRetainedOperation(context.CollectCapturedRanges, iterations, samples));
        if (benchmarkCase.Operation == PythonReBenchmarkOperation.FindAllUtf8)
        {
            PrintOperation("CoordinateMapOnly", MeasureRetainedOperation(context.CreateUtf8CoordinateMap, iterations, samples));
            PrintOperation("PreparedProjection", MeasureRetainedOperation(context.ProjectPreparedCapturedUtf8, iterations, samples));
            PrintOperation("PreparedFinalShape", MeasureRetainedOperation(context.ShapePreparedCapturedUtf8, iterations, samples));
            PrintOperation("PredecodedProjection", MeasureRetainedOperation(context.ProjectPredecodedCapturedUtf8, iterations, samples));
            PrintOperation("PythonRePublic", MeasureRetainedOperation(context.ExecutePythonReFindAllUtf8, iterations, samples));
        }
        else
        {
            PrintOperation("PreparedProjection", MeasureRetainedOperation(context.ProjectPreparedCapturedStrings, iterations, samples));
            PrintOperation("PreparedFinalShape", MeasureRetainedOperation(context.ShapePreparedCapturedStrings, iterations, samples));
            PrintOperation("PredecodedProjection", MeasureRetainedOperation(context.ProjectPredecodedCapturedStrings, iterations, samples));
            PrintOperation("PythonRePublic", MeasureRetainedOperation(context.ExecutePythonReFindAllStrings, iterations, samples));
        }

        PrintOperation("ChecksumTraversal", MeasureOperation(context.ExecutePreparedCapturedProjectionChecksum, iterations, samples));
        return 0;
    }

    private static int MeasureConstructionPattern(string pattern, int iterations, int samples, string fullMatchInput)
    {
        const PythonReCompileOptions options = PythonReCompileOptions.None;
        var parseResult = new PythonReParser(pattern).Parse(options);
        var translation = PythonReTranslator.Translate(parseResult);
        var input = Encoding.UTF8.GetBytes("prefix item-123 foo Шерлок suffix");
        var fullMatchInputUtf8 = Encoding.UTF8.GetBytes(fullMatchInput);
        var fullMatchMissUtf8 = "__pythonre_fullmatch_miss__"u8.ToArray();
        var prepared = new Utf8PythonRegex(pattern, options);
        var coreFullPattern = $@"\A(?:{translation.Pattern})\z";
        var preparedCoreFull = new Utf8Regex(coreFullPattern, translation.RegexOptions);
        var lazyCoreFull = new Lazy<Utf8Regex>(
            () => new Utf8Regex(coreFullPattern, translation.RegexOptions),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _ = lazyCoreFull.Value;
        var reuseIterations = Math.Max(iterations, 5_000);

        Console.WriteLine($"Pattern            : {pattern}");
        Console.WriteLine($"TranslatedPattern  : {translation.Pattern}");
        Console.WriteLine($"InputBytes         : {input.Length}");
        Console.WriteLine($"FullMatchInputBytes: {fullMatchInputUtf8.Length}");
        Console.WriteLine($"Iterations         : {iterations} (fixed, capped at 512)");
        Console.WriteLine($"ReuseIterations    : {reuseIterations} (minimum 5000)");
        Console.WriteLine($"Samples            : {samples} (capped at 15)");
        PrintOperation("ParseTranslate", MeasureOperation(
            () =>
            {
                var parsed = new PythonReParser(pattern).Parse(options);
                return PythonReTranslator.Translate(parsed).Pattern.Length;
            },
            iterations,
            samples));
        PrintOperation("ManagedSearch", MeasureOperation(
            () => new Regex(translation.Pattern, translation.RegexOptions, Regex.InfiniteMatchTimeout).GetHashCode(),
            iterations,
            samples));
        PrintOperation("ManagedFull", MeasureOperation(
            () => new Regex($@"\A(?:{translation.Pattern})\z", translation.RegexOptions, Regex.InfiniteMatchTimeout).GetHashCode(),
            iterations,
            samples));
        PrintOperation("CoreSearch", MeasureOperation(
            () => new Utf8Regex(translation.Pattern, translation.RegexOptions).GetHashCode(),
            iterations,
            samples));
        PrintOperation("CoreFull", MeasureOperation(
            () => new Utf8Regex($@"\A(?:{translation.Pattern})\z", translation.RegexOptions).GetHashCode(),
            iterations,
            samples));
        PrintOperation("PythonReConstruct", MeasureOperation(
            () => new Utf8PythonRegex(pattern, options).GetHashCode(),
            iterations,
            samples));
        PrintOperation("ConstructFirstCall", MeasureOperation(
            () =>
            {
                var regex = new Utf8PythonRegex(pattern, options);
                return regex.GetHashCode() ^ (regex.IsMatch(input) ? 1 : 0);
            },
            iterations,
            samples));
        PrintOperation("ConstructFirstFull", MeasureOperation(
            () =>
            {
                var regex = new Utf8PythonRegex(pattern, options);
                return regex.GetHashCode() ^ (regex.FullMatch(fullMatchInputUtf8).Success ? 1 : 0);
            },
            iterations,
            samples));
        PrintOperation("PreparedFullHit", MeasureOperation(
            () => prepared.FullMatch(fullMatchInputUtf8).Success ? 1 : 0,
            reuseIterations,
            samples));
        PrintOperation("PreparedFullMiss", MeasureOperation(
            () => prepared.FullMatch(fullMatchMissUtf8).Success ? 1 : 0,
            reuseIterations,
            samples));
        PrintOperation("DirectCoreFullHit", MeasureOperation(
            () => preparedCoreFull.Match(fullMatchInputUtf8).Success ? 1 : 0,
            reuseIterations,
            samples));
        PrintOperation("LazyCoreFullHit", MeasureOperation(
            () => lazyCoreFull.Value.Match(fullMatchInputUtf8).Success ? 1 : 0,
            reuseIterations,
            samples));
        return 0;
    }

    private static int MeasureFullMatchStartOffset(int iterations, int samples)
    {
        const string pattern = @"async\s+Task<";
        var prefix = new string('x', 65_536) + "é";
        var subject = prefix + "async Task<";
        var missSubject = prefix + "async Nope<";
        var input = Encoding.UTF8.GetBytes(subject);
        var missInput = Encoding.UTF8.GetBytes(missSubject);
        var startOffsetInBytes = Encoding.UTF8.GetByteCount(prefix);
        var pythonRegex = new Utf8PythonRegex(pattern);
        var managedFullRegex = new Regex(
            @"\A(?:async\s+Task<)\z",
            RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout);
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        var pythonMatch = pythonRegex.FullMatch(input, startOffsetInBytes);
        var pythonDetailedMiss = pythonRegex.FullMatchDetailedData(missInput, startOffsetInBytes);
        var managedResult = managedFullRegex.IsMatch(subject.AsSpan(prefix.Length));
        var managedMiss = managedFullRegex.IsMatch(missSubject.AsSpan(prefix.Length));
        if (!pythonMatch.Success || pythonMatch.EndOffsetInBytes != input.Length ||
            pythonDetailedMiss.Success || !managedResult || managedMiss ||
            pythonRegex.DebugHasUtf8FullRegex)
        {
            throw new InvalidOperationException("PythonRe start-offset FullMatch diagnostic failed its parity or backend precondition.");
        }

        Console.WriteLine($"Pattern            : {pattern}");
        Console.WriteLine($"InputBytes         : {input.Length}");
        Console.WriteLine($"StartOffsetInBytes : {startOffsetInBytes}");
        Console.WriteLine($"StartOffsetInUtf16 : {prefix.Length}");
        Console.WriteLine($"ExecutionPlan      : {pythonRegex.DebugDescribeExecutionPlan()}");
        Console.WriteLine($"Iterations         : {iterations} (capped at 2000)");
        Console.WriteLine($"Samples            : {samples} (capped at 15)");
        PrintOperation("PythonReFullMatch", MeasureOperation(
            () => pythonRegex.FullMatch(input, startOffsetInBytes).EndOffsetInBytes,
            iterations,
            samples));
        PrintOperation("DecodeFullMatch", MeasureOperation(
            () =>
            {
                var decoded = strictUtf8.GetString(input);
                return managedFullRegex.IsMatch(decoded.AsSpan(prefix.Length)) ? input.Length : 0;
            },
            iterations,
            samples));
        PrintOperation("PredecodedFullMatch", MeasureOperation(
            () => managedFullRegex.IsMatch(subject.AsSpan(prefix.Length)) ? input.Length : 0,
            iterations,
            samples));
        PrintOperation("PythonReFullMiss", MeasureOperation(
            () => pythonRegex.FullMatch(missInput, startOffsetInBytes).Success ? 1 : 0,
            iterations,
            samples));
        PrintOperation("PythonReDetailedMiss", MeasureOperation(
            () => pythonRegex.FullMatchDetailedData(missInput, startOffsetInBytes).Success ? 1 : 0,
            iterations,
            samples));
        PrintOperation("DecodeFullMiss", MeasureOperation(
            () =>
            {
                var decoded = strictUtf8.GetString(missInput);
                return managedFullRegex.IsMatch(decoded.AsSpan(prefix.Length)) ? 1 : 0;
            },
            iterations,
            samples));
        PrintOperation("PredecodedFullMiss", MeasureOperation(
            () => managedFullRegex.IsMatch(missSubject.AsSpan(prefix.Length)) ? 1 : 0,
            iterations,
            samples));
        return 0;
    }

    private static int MeasureEmptyGlobalShapes(int iterations, int samples)
    {
        const string pattern = "needle|needle-long";
        var prefix = new string('x', 65_536) + "é";
        var subject = prefix + " no matching token";
        var hitSubject = prefix + string.Concat(Enumerable.Repeat(" needle-long", 256));
        var progressivePrefix = prefix + " ";
        var progressiveSubject = progressivePrefix + "y";
        var input = Encoding.UTF8.GetBytes(subject);
        var hitInput = Encoding.UTF8.GetBytes(hitSubject);
        var progressiveInput = Encoding.UTF8.GetBytes(progressiveSubject);
        var startOffsetInBytes = Encoding.UTF8.GetByteCount(prefix);
        var progressiveStartOffsetInBytes = Encoding.UTF8.GetByteCount(progressivePrefix);
        var pythonRegex = new Utf8PythonRegex(pattern);
        var capturedPythonRegex = new Utf8PythonRegex($"({pattern})");
        var progressivePythonRegex = new Utf8PythonRegex(@"\b|\w+");
        var managedRegex = new Regex(
            pattern,
            RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout);
        var strictUtf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        string[] expectedProgression = [string.Empty, "y", string.Empty];
        var progressiveStructural = progressivePythonRegex.FindAll(progressiveInput, progressiveStartOffsetInBytes)
            .Select(static match => match.ValueText).ToArray();
        var progressiveStrings = progressivePythonRegex.FindAllToStrings(progressiveInput, progressiveStartOffsetInBytes).ScalarValues;
        var progressiveUtf8 = progressivePythonRegex.FindAllToUtf8(progressiveInput, progressiveStartOffsetInBytes)
            .ScalarValues.Select(Encoding.UTF8.GetString).ToArray();
        var progressiveCount = progressivePythonRegex.Count(progressiveInput, progressiveStartOffsetInBytes);

        if (pythonRegex.DebugFindAllBackend != PythonReDirectBackendKind.ManagedRegex ||
            pythonRegex.FindAll(input, startOffsetInBytes).Length != 0 ||
            pythonRegex.FindAllToStrings(input, startOffsetInBytes).Count != 0 ||
            pythonRegex.FindAllToUtf8(input, startOffsetInBytes).Count != 0 ||
            pythonRegex.FindIterDetailed(input, startOffsetInBytes).Length != 0 ||
            pythonRegex.Count(input, startOffsetInBytes) != 0 ||
            capturedPythonRegex.DebugFindAllBackend != PythonReDirectBackendKind.ManagedRegex ||
            capturedPythonRegex.FindAllToStrings(input, startOffsetInBytes).Count != 0 ||
            capturedPythonRegex.FindAllToUtf8(input, startOffsetInBytes).Count != 0 ||
            managedRegex.Match(subject, prefix.Length).Success ||
            pythonRegex.FindAll(hitInput, startOffsetInBytes).Length != 256 ||
            pythonRegex.FindAllToStrings(hitInput, startOffsetInBytes).Count != 256 ||
            pythonRegex.FindAllToUtf8(hitInput, startOffsetInBytes).Count != 256 ||
            pythonRegex.FindIterDetailed(hitInput, startOffsetInBytes).Length != 256 ||
            pythonRegex.Count(hitInput, startOffsetInBytes) != 256 ||
            capturedPythonRegex.FindAllToStrings(hitInput, startOffsetInBytes).Count != 256 ||
            capturedPythonRegex.FindAllToUtf8(hitInput, startOffsetInBytes).Count != 256 ||
            !progressiveStructural.SequenceEqual(expectedProgression) ||
            !progressiveStrings.SequenceEqual(expectedProgression) ||
            !progressiveUtf8.SequenceEqual(expectedProgression) ||
            progressiveCount != 3)
        {
            throw new InvalidOperationException(
                "PythonRe empty global-shape diagnostic failed its parity or backend precondition. " +
                $"Progressive structural=[{string.Join('|', progressiveStructural)}], " +
                $"strings=[{string.Join('|', progressiveStrings)}], " +
                $"utf8=[{string.Join('|', progressiveUtf8)}], count={progressiveCount}.");
        }

        Console.WriteLine($"Pattern            : {pattern}");
        Console.WriteLine($"InputBytes         : {input.Length}");
        Console.WriteLine($"StartOffsetInBytes : {startOffsetInBytes}");
        Console.WriteLine($"StartOffsetInUtf16 : {prefix.Length}");
        Console.WriteLine($"FindAllBackend     : {pythonRegex.DebugFindAllBackend}");
        Console.WriteLine($"Iterations         : {iterations} (capped at 2000)");
        Console.WriteLine($"Samples            : {samples} (capped at 15)");
        PrintOperation("FindAllEmpty", MeasureOperation(
            () => pythonRegex.FindAll(input, startOffsetInBytes).Length,
            iterations,
            samples));
        PrintOperation("FindAllStringsEmpty", MeasureOperation(
            () => pythonRegex.FindAllToStrings(input, startOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("FindAllUtf8Empty", MeasureOperation(
            () => pythonRegex.FindAllToUtf8(input, startOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("CapturedStringsEmpty", MeasureOperation(
            () => capturedPythonRegex.FindAllToStrings(input, startOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("CapturedUtf8Empty", MeasureOperation(
            () => capturedPythonRegex.FindAllToUtf8(input, startOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("FindIterDetailedEmpty", MeasureOperation(
            () => pythonRegex.FindIterDetailed(input, startOffsetInBytes).Length,
            iterations,
            samples));
        PrintOperation("CountEmpty", MeasureOperation(
            () => pythonRegex.Count(input, startOffsetInBytes),
            iterations,
            samples));
        PrintOperation("DecodeSearchMiss", MeasureOperation(
            () =>
            {
                var decoded = strictUtf8.GetString(input);
                return managedRegex.Match(decoded, prefix.Length).Success ? 1 : 0;
            },
            iterations,
            samples));
        PrintOperation("PredecodedSearchMiss", MeasureOperation(
            () => managedRegex.Match(subject, prefix.Length).Success ? 1 : 0,
            iterations,
            samples));
        PrintOperation("DecodeCountEmpty", MeasureOperation(
            () =>
            {
                var decoded = strictUtf8.GetString(input);
                return managedRegex.Count(decoded, prefix.Length);
            },
            iterations,
            samples));
        PrintOperation("PredecodedCountEmpty", MeasureOperation(
            () => managedRegex.Count(subject, prefix.Length),
            iterations,
            samples));
        PrintOperation("FindAllHit", MeasureOperation(
            () => pythonRegex.FindAll(hitInput, startOffsetInBytes).Length,
            iterations,
            samples));
        PrintOperation("FindAllStringsHit", MeasureOperation(
            () => pythonRegex.FindAllToStrings(hitInput, startOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("FindAllUtf8Hit", MeasureOperation(
            () => pythonRegex.FindAllToUtf8(hitInput, startOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("CapturedStringsHit", MeasureOperation(
            () => capturedPythonRegex.FindAllToStrings(hitInput, startOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("CapturedUtf8Hit", MeasureOperation(
            () => capturedPythonRegex.FindAllToUtf8(hitInput, startOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("FindIterDetailedHit", MeasureOperation(
            () => pythonRegex.FindIterDetailed(hitInput, startOffsetInBytes).Length,
            iterations,
            samples));
        PrintOperation("CountHit", MeasureOperation(
            () => pythonRegex.Count(hitInput, startOffsetInBytes),
            iterations,
            samples));
        PrintOperation("ProgressiveFindAll", MeasureOperation(
            () => progressivePythonRegex.FindAll(progressiveInput, progressiveStartOffsetInBytes).Length,
            iterations,
            samples));
        PrintOperation("ProgressiveStrings", MeasureOperation(
            () => progressivePythonRegex.FindAllToStrings(progressiveInput, progressiveStartOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("ProgressiveUtf8", MeasureOperation(
            () => progressivePythonRegex.FindAllToUtf8(progressiveInput, progressiveStartOffsetInBytes).Count,
            iterations,
            samples));
        PrintOperation("ProgressiveCount", MeasureOperation(
            () => progressivePythonRegex.Count(progressiveInput, progressiveStartOffsetInBytes),
            iterations,
            samples));
        return 0;
    }

    private static int MeasureEmptyProgressionScaling(int baseTokenCount, int iterations, int samples)
    {
        const string pattern = @"\b|\w+";
        var regex = new Utf8PythonRegex(pattern);
        var unsupportedProgression = new Utf8PythonRegex("x*|y").FindAll("y"u8)
            .Select(static match => match.ValueText)
            .ToArray();

        Console.WriteLine($"Pattern            : {pattern}");
        Console.WriteLine($"BaseTokenCount     : {baseTokenCount}");
        Console.WriteLine($"Iterations         : {iterations}");
        Console.WriteLine($"Samples            : {samples}");
        Console.WriteLine($"CurrentXStarPipeY  : [{string.Join('|', unsupportedProgression)}] (CPython: [|y|])");

        foreach (var multiplier in new[] { 1, 2, 4, 8 })
        {
            var tokenCount = checked(baseTokenCount * multiplier);
            var subject = string.Concat(Enumerable.Repeat("y ", tokenCount));
            var input = Encoding.UTF8.GetBytes(subject);
            var expectedCount = checked(tokenCount * 3);
            if (regex.Count(input) != expectedCount || regex.FindAll(input).Length != expectedCount)
            {
                throw new InvalidOperationException(
                    $"PythonRe empty-progression scaling precondition failed at {tokenCount} tokens.");
            }

            Console.WriteLine();
            Console.WriteLine($"Scale              : {multiplier}x");
            Console.WriteLine($"TokenCount         : {tokenCount}");
            Console.WriteLine($"InputBytes         : {input.Length}");
            Console.WriteLine($"ExpectedMatches    : {expectedCount}");
            PrintOperation("Count", MeasureOperation(
                () => regex.Count(input),
                iterations,
                samples));
            PrintOperation("FindAll", MeasureOperation(
                () => regex.FindAll(input).Length,
                iterations,
                samples));
            PrintOperation("FindAllStrings", MeasureOperation(
                () => regex.FindAllToStrings(input).Count,
                iterations,
                samples));
        }

        return 0;
    }

    private static PythonReCaseMeasurement Measure(
        PythonReBenchmarkCase benchmarkCase,
        int iterations,
        int samples)
    {
        var effectiveIterations = GetEffectiveIterations(benchmarkCase, iterations);
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        var context = new PythonReBenchmarkContext(benchmarkCase);
        var byteControl = PythonReBenchmarkCatalog.GetByteControlEligibility(
            benchmarkCase,
            context.InputBytes);
        var pythonResult = context.ExecutePythonRe();
        var decodeResult = context.ExecuteDecodeThenRegex();
        var predecodedResult = context.ExecutePredecodedRegex();
        if (pythonResult != decodeResult || pythonResult != predecodedResult)
        {
            throw new InvalidOperationException(
                $"PythonRe benchmark '{benchmarkCase.Id}' produced incomparable sinks: " +
                $"PythonRe={pythonResult}, decode={decodeResult}, predecoded={predecodedResult}.");
        }

        var cpython = MeasureCpython(benchmarkCase, context.InputBytes, effectiveIterations, samples);
        if (pythonResult != cpython.Checksum)
        {
            throw new InvalidOperationException(
                $"PythonRe benchmark '{benchmarkCase.Id}' disagrees with CPython: " +
                $"PythonRe={pythonResult}, CPython={cpython.Checksum}.");
        }

        return new PythonReCaseMeasurement
        {
            Pattern = benchmarkCase.Pattern,
            Options = benchmarkCase.Options.ToString(),
            Operation = benchmarkCase.Operation.ToString(),
            InputUtf8Bytes = context.InputBytes.Length,
            EffectiveIterations = effectiveIterations,
            Samples = samples,
            IncludesResultMaterialization = benchmarkCase.IncludesResultMaterialization,
            ComparatorOwner = PythonReBenchmarkCatalog.GetComparatorOwner(benchmarkCase.Operation),
            ManagedRoute = context.DescribeManagedRoute(),
            ByteControlEligible = byteControl.IsEligible,
            ByteControlReason = byteControl.Reason,
            Environment = CaptureEnvironment(),
            PythonRe = MeasurePythonReOperation(context, effectiveIterations, samples, pythonResult),
            DecodeThenRegex = MeasureOperation(context.ExecuteDecodeThenRegex, effectiveIterations, samples),
            PredecodedRegex = MeasureOperation(context.ExecutePredecodedRegex, effectiveIterations, samples),
            Cpython = cpython,
            Qualification = PythonReQualificationMeasurement.CreateHistoricalUnqualified(),
        };
    }

    private static CpythonBenchmarkMeasurement MeasureCpython(
        PythonReBenchmarkCase benchmarkCase,
        byte[] inputBytes,
        int iterations,
        int samples)
    {
        var executable = Environment.GetEnvironmentVariable("UTF8REGEX_CPYTHON");
        if (string.IsNullOrWhiteSpace(executable))
        {
            executable = "python";
        }

        var startInfo = new ProcessStartInfo(executable)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        startInfo.ArgumentList.Add("-I");
        startInfo.ArgumentList.Add(FindRepositoryFile(CpythonRunnerRelativePath));

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException($"Could not start CPython executable '{executable}'.");
        var request = new CpythonBenchmarkRequest
        {
            ProtocolVersion = CpythonProtocolVersion,
            Pattern = benchmarkCase.Pattern,
            Options = (int)benchmarkCase.Options,
            Operation = benchmarkCase.Operation.ToString(),
            InputBase64 = Convert.ToBase64String(inputBytes),
            Replacement = benchmarkCase.Replacement,
            Iterations = iterations,
            Samples = samples,
        };
        process.StandardInput.Write(JsonSerializer.Serialize(request));
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"CPython baseline failed for '{benchmarkCase.Id}' with exit code {process.ExitCode}: " +
                error.Trim());
        }

        var measurement = JsonSerializer.Deserialize<CpythonBenchmarkMeasurement>(output);
        if (measurement is null || measurement.ProtocolVersion != CpythonProtocolVersion)
        {
            throw new InvalidOperationException(
                $"CPython baseline returned an unsupported response for '{benchmarkCase.Id}'.");
        }

        return measurement;
    }

    private static int GetEffectiveIterations(PythonReBenchmarkCase benchmarkCase, int requestedIterations)
    {
        if (benchmarkCase.Operation is PythonReBenchmarkOperation.IsMatch or
                PythonReBenchmarkOperation.Search or
                PythonReBenchmarkOperation.Match or
                PythonReBenchmarkOperation.FullMatch or
                PythonReBenchmarkOperation.SearchDetailed &&
            Encoding.UTF8.GetByteCount(benchmarkCase.Input) <= 128)
        {
            return Math.Max(requestedIterations, 20_000);
        }

        var minimum = benchmarkCase.Operation switch
        {
            PythonReBenchmarkOperation.IsMatch or
            PythonReBenchmarkOperation.Search or
            PythonReBenchmarkOperation.Match or
            PythonReBenchmarkOperation.FullMatch or
            PythonReBenchmarkOperation.SearchDetailed => 5_000,
            PythonReBenchmarkOperation.Count when benchmarkCase.Id == "zero-width/count" => 5_000,
            PythonReBenchmarkOperation.Count => 500,
            PythonReBenchmarkOperation.FindAllStrings or
            PythonReBenchmarkOperation.FindAllUtf8 or
            PythonReBenchmarkOperation.FindIterDetailed => 1_000,
            _ => 2_000,
        };
        return Math.Max(requestedIterations, minimum);
    }

    private static PythonReOperationMeasurement MeasurePythonReOperation(
        PythonReBenchmarkContext context,
        int iterations,
        int samples,
        int expectedChecksum)
    {
        var warmup = Stopwatch.StartNew();
        var warmupCalls = 0;
        var warmupBatchSize = Math.Min(iterations, 16);
        do
        {
            var batch = context.MeasurePythonReBatch(warmupBatchSize);
            if (batch.Checksum != expectedChecksum)
            {
                throw new InvalidOperationException(
                    $"PythonRe warmup result checksum {batch.Checksum} does not match preflight {expectedChecksum}.");
            }

            s_sink ^= batch.Checksum;
            warmupCalls += warmupBatchSize;
        }
        while (warmup.ElapsedMilliseconds < 100 && warmupCalls < 65_536);

        var microseconds = new double[samples];
        var allocations = new long[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            var batch = context.MeasurePythonReBatch(iterations);
            if (batch.Checksum != expectedChecksum)
            {
                throw new InvalidOperationException(
                    $"PythonRe timed result checksum {batch.Checksum} does not match preflight {expectedChecksum}.");
            }

            allocations[sample] = batch.AllocatedBytes / iterations;
            microseconds[sample] = batch.Elapsed.TotalMicroseconds / iterations;
            s_sink ^= batch.Checksum;
        }

        Array.Sort(microseconds);
        Array.Sort(allocations);
        return new PythonReOperationMeasurement
        {
            MedianMicroseconds = microseconds[microseconds.Length / 2],
            MinimumMicroseconds = microseconds[0],
            MaximumMicroseconds = microseconds[^1],
            MedianAllocatedBytes = allocations[allocations.Length / 2],
            WarmupCalls = warmupCalls,
            WarmupMilliseconds = warmup.Elapsed.TotalMilliseconds,
        };
    }

    private static PythonReOperationMeasurement MeasureOperation(
        Func<int> operation,
        int iterations,
        int samples)
    {
        var warmup = Stopwatch.StartNew();
        var warmupCalls = 0;
        do
        {
            s_sink ^= operation();
            warmupCalls++;
        }
        while (warmup.ElapsedMilliseconds < 100 && warmupCalls < 65_536);

        var microseconds = new double[samples];
        var allocations = new long[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            var local = 0;
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                local ^= operation();
            }

            stopwatch.Stop();
            allocations[sample] = (GC.GetAllocatedBytesForCurrentThread() - before) / iterations;
            microseconds[sample] = stopwatch.Elapsed.TotalMicroseconds / iterations;
            s_sink ^= local;
        }

        Array.Sort(microseconds);
        Array.Sort(allocations);
        return new PythonReOperationMeasurement
        {
            MedianMicroseconds = microseconds[microseconds.Length / 2],
            MinimumMicroseconds = microseconds[0],
            MaximumMicroseconds = microseconds[^1],
            MedianAllocatedBytes = allocations[allocations.Length / 2],
            WarmupCalls = warmupCalls,
            WarmupMilliseconds = warmup.Elapsed.TotalMilliseconds,
        };
    }

    private static PythonReOperationMeasurement MeasureRetainedOperation<T>(
        Func<T> operation,
        int iterations,
        int samples)
    {
        var warmup = Stopwatch.StartNew();
        var warmupCalls = 0;
        do
        {
            s_retainedSink = operation();
            warmupCalls++;
        }
        while (warmup.ElapsedMilliseconds < 100 && warmupCalls < 65_536);

        var microseconds = new double[samples];
        var allocations = new long[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var stopwatch = Stopwatch.StartNew();
            var retained = default(T);
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                retained = operation();
            }

            stopwatch.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            s_retainedSink = retained;
            allocations[sample] = allocated / iterations;
            microseconds[sample] = stopwatch.Elapsed.TotalMicroseconds / iterations;
        }

        Array.Sort(microseconds);
        Array.Sort(allocations);
        return new PythonReOperationMeasurement
        {
            MedianMicroseconds = microseconds[microseconds.Length / 2],
            MinimumMicroseconds = microseconds[0],
            MaximumMicroseconds = microseconds[^1],
            MedianAllocatedBytes = allocations[allocations.Length / 2],
            WarmupCalls = warmupCalls,
            WarmupMilliseconds = warmup.Elapsed.TotalMilliseconds,
        };
    }

    private static void Print(PythonReBenchmarkCase benchmarkCase, PythonReCaseMeasurement measurement)
    {
        Console.WriteLine($"CaseId             : {benchmarkCase.Id}");
        Console.WriteLine($"Pattern            : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options            : {benchmarkCase.Options}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"InputBytes         : {measurement.InputUtf8Bytes}");
        Console.WriteLine($"Iterations         : {measurement.EffectiveIterations}");
        Console.WriteLine($"Samples            : {measurement.Samples}");
        PrintOperation("PythonRe", measurement.PythonRe);
        PrintOperation("DecodeThenRegex", measurement.DecodeThenRegex);
        PrintOperation("PredecodedRegex", measurement.PredecodedRegex);
        if (measurement.Cpython is { } cpython)
        {
            PrintOperation("CPythonDecode", cpython.DecodeThenRe);
            PrintOperation("CPythonPredecoded", cpython.PredecodedRe);
        }
    }

    private static void PrintOperation(string name, PythonReOperationMeasurement measurement)
    {
        Console.WriteLine(
            $"{name,-19}: {measurement.MedianMicroseconds,10:F3} us/op | " +
            $"range={measurement.MinimumMicroseconds:F3}..{measurement.MaximumMicroseconds:F3} | " +
            $"alloc={measurement.MedianAllocatedBytes} B/op | " +
            $"warmup={measurement.WarmupCalls} calls/{measurement.WarmupMilliseconds:F1} ms");
    }

    private static void PrintOperation(string name, CpythonOperationMeasurement measurement)
    {
        Console.WriteLine(
            $"{name,-19}: {measurement.MedianMicroseconds,10:F3} us/op | " +
            $"range={measurement.MinimumMicroseconds:F3}..{measurement.MaximumMicroseconds:F3} | " +
            $"iterations={measurement.EffectiveIterations} | " +
            $"warmup={measurement.WarmupCalls} calls/{measurement.WarmupMilliseconds:F1} ms");
    }

    private static PythonReBenchmarkEnvironment CaptureEnvironment()
    {
        var trackedStatus = RunGit(
            "status",
            "--porcelain=v1",
            "--untracked-files=no",
            "--",
            ".",
            ":(exclude)PythonRe.Benchmarks.json",
            ":(exclude)src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md");
        var untrackedStatus = RunGit(
            "ls-files",
            "--others",
            "--exclude-standard",
            "--",
            ".",
            ":(exclude)UTF8REGEX-PERFORMANCE-ROADMAP.md");
        return new PythonReBenchmarkEnvironment
        {
            SourceCommit = RunGit("rev-parse", "--short=12", "HEAD") ?? "<unknown>",
            TrackedDirty = !string.IsNullOrWhiteSpace(trackedStatus),
            HasUntrackedFiles = !string.IsNullOrWhiteSpace(untrackedStatus),
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ??
                RuntimeInformation.ProcessArchitecture.ToString(),
        };
    }

    private static PythonReCorpusProvenance CaptureCorpusProvenance()
    {
        const string sourceFile = "tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json";
        var fullPath = FindRepositoryFile(sourceFile);
        var hash = SHA256.HashData(File.ReadAllBytes(fullPath));
        using var corpus = JsonDocument.Parse(File.ReadAllText(fullPath));
        return new PythonReCorpusProvenance
        {
            SourceFile = sourceFile,
            Sha256 = Convert.ToHexString(hash),
            VectorCount = corpus.RootElement.GetArrayLength(),
            UpstreamCpythonRevision = "not-recorded-in-repository",
            Limitation = "The original upstream CPython version was not recorded; do not infer one from local vector names.",
        };
    }

    private static string FindRepositoryFile(string relativePath)
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{relativePath}'.");
    }

    private static string? RunGit(params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return process.ExitCode == 0 ? output.Trim() : null;
    }

    private static int ParsePositive(string[] args, int index, int defaultValue)
    {
        if (index >= args.Length)
        {
            return defaultValue;
        }

        return int.TryParse(args[index], out var value) && value > 0
            ? value
            : throw new ArgumentException($"'{args[index]}' must be a positive integer.");
    }
}

internal enum PythonReBenchmarkOperation : byte
{
    IsMatch = 0,
    Search = 1,
    Match = 2,
    FullMatch = 3,
    SearchDetailed = 4,
    Count = 5,
    FindAllStrings = 6,
    FindAllUtf8 = 7,
    FindIterDetailed = 8,
    ReplaceString = 9,
    ReplaceUtf8 = 10,
    SubnString = 11,
    SubnUtf8 = 12,
    SubnEvaluatorString = 13,
    SplitStrings = 14,
    SubnEvaluatorUtf8 = 15,
}

internal sealed record PythonReBenchmarkCase(
    string Id,
    string Pattern,
    PythonReCompileOptions Options,
    PythonReBenchmarkOperation Operation,
    string Input,
    string Replacement,
    bool IncludesResultMaterialization);

internal static class PythonReBenchmarkCatalog
{
    internal static IReadOnlyList<PythonReBenchmarkCase> Cases { get; } =
    [
        new("literal/ismatch", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.IsMatch,
            new string('x', 65_536) + "needle", string.Empty, false),
        new("literal/search", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.Search,
            new string('x', 65_536) + "needle", string.Empty, false),
        new("literal/search-miss", "needle", PythonReCompileOptions.None, PythonReBenchmarkOperation.Search,
            new string('x', 65_536), string.Empty, false),
        new("prefix/match", "header:[0-9]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.Match,
            "header:12345 " + new string('x', 16_384), string.Empty, false),
        new("literal/fullmatch", "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-", PythonReCompileOptions.None, PythonReBenchmarkOperation.FullMatch,
            "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-", string.Empty, false),
        new("unicode/fullmatch", "(?:Шерлок )+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FullMatch,
            Repeat("Шерлок ", 1_024), string.Empty, false),
        new("capture/search-detailed", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SearchDetailed,
            "prefix item-123 suffix", string.Empty, true),
        new("family/count", "cat|dog|bird", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("cat fox dog owl bird ", 4_096), string.Empty, false),
        new("class-run/count", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma 123 ", 4_096), string.Empty, false),
        new("unicode/count", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.Count,
            Repeat("Шерлок и Ватсон. ", 4_096), string.Empty, false),
        new("findall/full-strings", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("alpha beta gamma 123 ", 1_024), string.Empty, true),
        new("findall/full-utf8", "[a-z]+", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("alpha beta gamma 123 ", 1_024), string.Empty, true),
        new("findall/unicode-full-strings", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("Шерлок и Ватсон. ", 512), string.Empty, true),
        new("findall/unicode-full-utf8", "Шерлок", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("Шерлок и Ватсон. ", 512), string.Empty, true),
        new("findall/one-capture-strings", "item-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("item-12 item-345 ", 1_024), string.Empty, true),
        new("findall/many-capture-strings", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllStrings,
            Repeat("item-12 other-345 ", 1_024), string.Empty, true),
        new("findall/many-capture-utf8", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("item-12 other-345 ", 1_024), string.Empty, true),
        new("findall/unicode-capture-utf8", "(é+)-(𝒜𝒜|𝒜)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindAllUtf8,
            Repeat("éé-𝒜𝒜 é-𝒜 ", 512), string.Empty, true),
        new("iteration/finditer-detailed", "([a-z]+)-([0-9]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.FindIterDetailed,
            Repeat("item-12 other-345 ", 256), string.Empty, true),
        new("zero-width/count", @"\b", PythonReCompileOptions.Ascii, PythonReBenchmarkOperation.Count,
            Repeat("alpha beta gamma ", 1_024), string.Empty, false),
        new("replacement/fixed-string", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.ReplaceString,
            Repeat("cat fox cat dog ", 2_048), "tiger", true),
        new("replacement/fixed-utf8", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.ReplaceUtf8,
            Repeat("cat fox cat dog ", 2_048), "tiger", true),
        new("replacement/subn-string", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnString,
            Repeat("cat fox cat dog ", 1_024), "tiger", true),
        new("replacement/subn-utf8", "cat", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnUtf8,
            Repeat("cat fox cat dog ", 1_024), "tiger", true),
        new("replacement/evaluator-string", "([a-z]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnEvaluatorString,
            Repeat("cat fox dog ", 512), "token", true),
        new("replacement/evaluator-utf8", "([a-z]+)", PythonReCompileOptions.None, PythonReBenchmarkOperation.SubnEvaluatorUtf8,
            Repeat("cat fox dog ", 512), "token", true),
        new("split/no-captures", "[,;]", PythonReCompileOptions.None, PythonReBenchmarkOperation.SplitStrings,
            Repeat("alpha,beta;gamma,delta;", 512), string.Empty, true),
        new("split/captures", "([,;])", PythonReCompileOptions.None, PythonReBenchmarkOperation.SplitStrings,
            Repeat("alpha,beta;gamma,delta;", 512), string.Empty, true),
    ];

    private static string Repeat(string value, int count)
    {
        var builder = new StringBuilder(value.Length * count);
        for (var index = 0; index < count; index++)
        {
            builder.Append(value);
        }

        return builder.ToString();
    }

    internal static string GetComparatorOwner(PythonReBenchmarkOperation operation) => operation switch
    {
        PythonReBenchmarkOperation.IsMatch or PythonReBenchmarkOperation.Search =>
            "_sre C Pattern.search",
        PythonReBenchmarkOperation.Match => "_sre C Pattern.match",
        PythonReBenchmarkOperation.FullMatch => "_sre C Pattern.fullmatch",
        PythonReBenchmarkOperation.SearchDetailed =>
            "_sre C Pattern.search + Python detailed projection",
        PythonReBenchmarkOperation.Count => "_sre scanner + Python finditer/sum",
        PythonReBenchmarkOperation.FindAllStrings => "_sre C Pattern.findall",
        PythonReBenchmarkOperation.FindAllUtf8 =>
            "_sre C Pattern.findall + Python UTF-8 projection",
        PythonReBenchmarkOperation.FindIterDetailed =>
            "_sre scanner + Python detailed projection",
        PythonReBenchmarkOperation.ReplaceString or PythonReBenchmarkOperation.ReplaceUtf8 =>
            "_sre C Pattern.sub",
        PythonReBenchmarkOperation.SubnString or PythonReBenchmarkOperation.SubnUtf8 =>
            "_sre C Pattern.subn",
        PythonReBenchmarkOperation.SubnEvaluatorString or PythonReBenchmarkOperation.SubnEvaluatorUtf8 =>
            "_sre C Pattern.subn + Python callback",
        PythonReBenchmarkOperation.SplitStrings => "_sre C Pattern.split",
        _ => throw new ArgumentOutOfRangeException(nameof(operation)),
    };

    internal static PythonReByteControlEligibility GetByteControlEligibility(
        PythonReBenchmarkCase benchmarkCase,
        ReadOnlySpan<byte> inputBytes)
    {
        if (benchmarkCase.Operation is not PythonReBenchmarkOperation.IsMatch and
            not PythonReBenchmarkOperation.Search and
            not PythonReBenchmarkOperation.Match and
            not PythonReBenchmarkOperation.FullMatch)
        {
            return new(
                false,
                "Excluded: the first byte-control profile is limited to one-shot matching operations.");
        }

        if (benchmarkCase.Pattern.Any(static character => character > 0x7f) ||
            inputBytes.ContainsAnyExceptInRange((byte)0, (byte)0x7f))
        {
            return new(false, "Excluded: pattern or subject is not entirely ASCII.");
        }

        if ((benchmarkCase.Options & ~PythonReCompileOptions.Ascii) != PythonReCompileOptions.None)
        {
            return new(false, "Excluded: flags are not proven equivalent for CPython bytes patterns.");
        }

        return new(
            true,
            "Eligible: ASCII one-shot semantics and byte/UTF-16 coordinates are identical.");
    }
}

internal readonly record struct PythonReByteControlEligibility(bool IsEligible, string Reason);

internal sealed class PythonReBenchmarkContext
{
    private static readonly UTF8Encoding s_strictUtf8 = new(false, true);
    private readonly PythonReBenchmarkCase _case;
    private readonly Utf8PythonRegex _pythonRegex;
    private readonly Regex _regex;
    private readonly Regex _fullRegex;
    private readonly Utf8Regex? _coreFindAllRegex;
    private readonly PythonReBenchmarkRange[] _preparedCoreRanges;
    private readonly Utf8PythonFindAllResult _preparedCoreStrings;
    private readonly Utf8PythonFindAllUtf8Result _preparedCoreUtf8;
    private readonly bool _supportsCapturedFindAllPhases;
    private readonly PythonReBenchmarkCaptureRange[] _preparedCaptureRanges;
    private readonly int[]? _preparedUtf8Offsets;
    private readonly Utf8PythonFindAllResult _preparedCapturedStrings;
    private readonly Utf8PythonFindAllUtf8Result _preparedCapturedUtf8;
    private readonly string _decoded;
    private readonly byte[] _replacementBytes;
    private readonly int _captureCount;
    private int _callbackChecksum;
    private ulong _callbackSemanticDigest;

    internal PythonReBenchmarkContext(PythonReBenchmarkCase benchmarkCase)
    {
        _case = benchmarkCase;
        InputBytes = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        _decoded = benchmarkCase.Input;
        _replacementBytes = Encoding.UTF8.GetBytes(benchmarkCase.Replacement);
        _pythonRegex = new Utf8PythonRegex(benchmarkCase.Pattern, benchmarkCase.Options);
        var regexOptions = ToRegexOptions(benchmarkCase.Options);
        _regex = new Regex(benchmarkCase.Pattern, regexOptions, Regex.InfiniteMatchTimeout);
        _fullRegex = new Regex($@"\A(?:{benchmarkCase.Pattern})\z", regexOptions, Regex.InfiniteMatchTimeout);
        _captureCount = _regex.GetGroupNumbers().Length - 1;
        if (benchmarkCase.Operation is PythonReBenchmarkOperation.FindAllStrings or PythonReBenchmarkOperation.FindAllUtf8 &&
            _captureCount == 0)
        {
            _coreFindAllRegex = new Utf8Regex(benchmarkCase.Pattern, regexOptions);
            _preparedCoreRanges = CollectCoreRanges().ToArray();
            _preparedCoreStrings = ProjectCoreRangeStrings(_preparedCoreRanges);
            _preparedCoreUtf8 = ProjectCoreRangeUtf8(_preparedCoreRanges);
            _supportsCapturedFindAllPhases = false;
            _preparedCaptureRanges = [];
            _preparedUtf8Offsets = [];
            _preparedCapturedStrings = default;
            _preparedCapturedUtf8 = default;
        }
        else if (benchmarkCase.Operation is PythonReBenchmarkOperation.FindAllStrings or PythonReBenchmarkOperation.FindAllUtf8 &&
                 _captureCount > 0 && !_regex.IsMatch(string.Empty))
        {
            _coreFindAllRegex = null;
            _preparedCoreRanges = [];
            _preparedCoreStrings = default;
            _preparedCoreUtf8 = default;
            _supportsCapturedFindAllPhases = true;
            _preparedCaptureRanges = CollectCapturedRanges();
            _preparedUtf8Offsets = GetUtf8Offsets(_decoded);
            _preparedCapturedStrings = ProjectCapturedStrings(_preparedCaptureRanges);
            _preparedCapturedUtf8 = ProjectCapturedUtf8(_preparedCaptureRanges, _preparedUtf8Offsets);
        }
        else
        {
            _coreFindAllRegex = null;
            _preparedCoreRanges = [];
            _preparedCoreStrings = default;
            _preparedCoreUtf8 = default;
            _supportsCapturedFindAllPhases = false;
            _preparedCaptureRanges = [];
            _preparedUtf8Offsets = [];
            _preparedCapturedStrings = default;
            _preparedCapturedUtf8 = default;
        }
    }

    internal byte[] InputBytes { get; }

    internal bool SupportsCaptureFreeFindAllPhases => _coreFindAllRegex is not null;

    internal bool SupportsCapturedFindAllPhases => _supportsCapturedFindAllPhases;

    internal int PreparedCoreRangeCount => _preparedCoreRanges.Length;

    internal int CaptureCount => _captureCount;

    internal int PreparedCapturedMatchCount => _captureCount == 0
        ? 0
        : _preparedCaptureRanges.Length / _captureCount;

    internal int PreparedCapturedValueCount => _preparedCaptureRanges.Length;

    internal int PreparedCapturedOutputUtf8Bytes => _preparedCaptureRanges.Sum(
        range => range.Success
            ? s_strictUtf8.GetByteCount(_decoded.AsSpan(range.StartOffsetInUtf16, range.LengthInUtf16))
            : 0);

    internal string DescribeManagedRoute() => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => DescribeBackend(
            _pythonRegex.DebugSearchBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "boolean result"),
        PythonReBenchmarkOperation.Search => DescribeBackend(
            _pythonRegex.DebugSearchBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "value ranges"),
        PythonReBenchmarkOperation.Match => DescribeBackend(
            _pythonRegex.DebugMatchBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "anchored value ranges"),
        PythonReBenchmarkOperation.FullMatch => DescribeBackend(
            _pythonRegex.DebugFullMatchBackend,
            _pythonRegex.DebugUtf8FullMatchExecutionKind,
            "full-match value ranges"),
        PythonReBenchmarkOperation.SearchDetailed => DescribeBackend(
            _pythonRegex.DebugSearchBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "detailed capture projection"),
        PythonReBenchmarkOperation.Count when _pythonRegex.DebugUsesAsciiWordBoundaryCount =>
            "strict UTF-8 decode; adapter ASCII-boundary loop; scalar result",
        PythonReBenchmarkOperation.Count => DescribeBackend(
            _pythonRegex.DebugCountBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "Python-style count progression"),
        PythonReBenchmarkOperation.FindAllStrings when _captureCount > 0 =>
            "strict UTF-8 decode; .NET Regex; findall string shaping",
        PythonReBenchmarkOperation.FindAllStrings => DescribeBackend(
            _pythonRegex.DebugFindAllBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "findall string shaping"),
        PythonReBenchmarkOperation.FindAllUtf8 when _captureCount > 0 =>
            "strict UTF-8 decode; .NET Regex; findall UTF-8 shaping",
        PythonReBenchmarkOperation.FindAllUtf8 => DescribeBackend(
            _pythonRegex.DebugFindAllBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "findall UTF-8 shaping"),
        PythonReBenchmarkOperation.FindIterDetailed =>
            "strict UTF-8 decode; .NET Regex; detailed iteration shaping",
        PythonReBenchmarkOperation.ReplaceString or
            PythonReBenchmarkOperation.SubnString =>
            "strict UTF-8 decode; .NET Regex replacement; string shaping",
        PythonReBenchmarkOperation.ReplaceUtf8 or
            PythonReBenchmarkOperation.SubnUtf8 =>
            "strict UTF-8 decode; .NET Regex replacement; UTF-8 shaping",
        PythonReBenchmarkOperation.SubnEvaluatorString =>
            "strict UTF-8 decode; .NET Regex callback replacement; string shaping",
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 =>
            "strict UTF-8 decode; .NET Regex callback replacement; UTF-8 shaping",
        PythonReBenchmarkOperation.SplitStrings =>
            "strict UTF-8 decode; .NET Regex split; string shaping",
        _ => throw new ArgumentOutOfRangeException(),
    };

    private static string DescribeBackend(
        PythonReDirectBackendKind backend,
        string? executionKind,
        string projection) => backend == PythonReDirectBackendKind.Utf8Regex
            ? $"Utf8Regex/{executionKind ?? "unknown"}; {projection}"
            : $"strict UTF-8 decode; .NET Regex; {projection}";

    internal PythonReBenchmarkBatch MeasurePythonReBatch(int iterations)
    {
        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        switch (_case.Operation)
        {
            case PythonReBenchmarkOperation.IsMatch:
            {
                var result = false;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.IsMatch(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    result ? 1 : 0,
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.Search:
            {
                Utf8PythonValueMatch result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Search(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.Match:
            {
                Utf8PythonValueMatch result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Match(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.FullMatch:
            {
                Utf8PythonValueMatch result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.FullMatch(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.SearchDetailed:
            {
                Utf8PythonDetailedMatchData result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.SearchDetailedData(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.Count:
            {
                var result = 0;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Count(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    result,
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.FindAllStrings:
            {
                Utf8PythonFindAllResult result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.FindAllToStrings(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.FindAllUtf8:
            {
                Utf8PythonFindAllUtf8Result result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.FindAllToUtf8(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.FindIterDetailed:
            {
                Utf8PythonDetailedMatchData[] result = [];
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.FindIterDetailed(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.ReplaceString:
            {
                var result = string.Empty;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.ReplaceToString(InputBytes, _case.Replacement);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.ReplaceUtf8:
            {
                byte[] result = [];
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Replace(InputBytes, _case.Replacement);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.SubnString:
            {
                Utf8PythonSubnResult result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.SubnToString(InputBytes, _case.Replacement);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.SubnUtf8:
            {
                Utf8PythonSubnUtf8Result result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Subn(InputBytes, _case.Replacement);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.SubnEvaluatorString:
            {
                Utf8PythonSubnResult result = default;
                var callbackChecksum = 0;
                var callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    _callbackChecksum = 0;
                    _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
                    result = _pythonRegex.SubnToString(
                        InputBytes,
                        this,
                        static (context, match) =>
                        {
                            context._callbackChecksum = Combine(context._callbackChecksum, Checksum(match));
                            context._callbackSemanticDigest = ExtendCallbackSemanticDigest(
                                context._callbackSemanticDigest,
                                match);
                            return context._case.Replacement;
                        });
                    callbackChecksum = _callbackChecksum;
                    callbackSemanticDigest = _callbackSemanticDigest;
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Combine(Checksum(result), callbackChecksum),
                    SemanticDigest(_case.Operation, result, callbackSemanticDigest));
            }
            case PythonReBenchmarkOperation.SubnEvaluatorUtf8:
            {
                Utf8PythonSubnUtf8Result result = default;
                var callbackChecksum = 0;
                var callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    _callbackChecksum = 0;
                    _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
                    result = _pythonRegex.Subn(
                        InputBytes,
                        this,
                        static (context, match) =>
                        {
                            context._callbackChecksum = Combine(context._callbackChecksum, Checksum(match));
                            context._callbackSemanticDigest = ExtendCallbackSemanticDigest(
                                context._callbackSemanticDigest,
                                match);
                            return context._replacementBytes;
                        });
                    callbackChecksum = _callbackChecksum;
                    callbackSemanticDigest = _callbackSemanticDigest;
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Combine(Checksum(result), callbackChecksum),
                    SemanticDigest(_case.Operation, result, callbackSemanticDigest));
            }
            case PythonReBenchmarkOperation.SplitStrings:
            {
                string?[] result = [];
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.SplitToStrings(InputBytes);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            default:
                throw new InvalidOperationException();
        }

        PythonReBenchmarkBatch Complete(
            long ended,
            long allocatedAfter,
            int checksum,
            ulong semanticDigest)
        {
            return new PythonReBenchmarkBatch(
                Stopwatch.GetElapsedTime(started, ended),
                allocatedAfter - allocatedBefore,
                checksum,
                semanticDigest,
                ConsumptionChecksum: 0);
        }
    }

    internal PythonReBenchmarkBatch MeasurePythonReQualificationBatch(int iterations)
    {
        if (_case.Operation is not PythonReBenchmarkOperation.Search and
            not PythonReBenchmarkOperation.Match and
            not PythonReBenchmarkOperation.FullMatch)
        {
            return MeasurePythonReBatch(iterations);
        }

        var allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        var consumptionChecksum = 0UL;
        var started = Stopwatch.GetTimestamp();
        Utf8PythonValueMatch result = default;
        switch (_case.Operation)
        {
            case PythonReBenchmarkOperation.Search:
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Search(InputBytes);
                    consumptionChecksum += GetConsumptionToken(result);
                }
                break;
            case PythonReBenchmarkOperation.Match:
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Match(InputBytes);
                    consumptionChecksum += GetConsumptionToken(result);
                }
                break;
            case PythonReBenchmarkOperation.FullMatch:
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.FullMatch(InputBytes);
                    consumptionChecksum += GetConsumptionToken(result);
                }
                break;
            default:
                throw new InvalidOperationException();
        }

        var ended = Stopwatch.GetTimestamp();
        var allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        return new PythonReBenchmarkBatch(
            Stopwatch.GetElapsedTime(started, ended),
            allocatedAfter - allocatedBefore,
            Checksum(result),
            SemanticDigest(_case.Operation, result),
            consumptionChecksum);
    }

    internal ulong ExecutePythonReConsumptionToken() => _case.Operation switch
    {
        PythonReBenchmarkOperation.Search => GetConsumptionToken(_pythonRegex.Search(InputBytes)),
        PythonReBenchmarkOperation.Match => GetConsumptionToken(_pythonRegex.Match(InputBytes)),
        PythonReBenchmarkOperation.FullMatch => GetConsumptionToken(_pythonRegex.FullMatch(InputBytes)),
        _ => 0,
    };

    internal int ExecutePythonRe() => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _pythonRegex.IsMatch(InputBytes) ? 1 : 0,
        PythonReBenchmarkOperation.Search => Checksum(_pythonRegex.Search(InputBytes)),
        PythonReBenchmarkOperation.Match => Checksum(_pythonRegex.Match(InputBytes)),
        PythonReBenchmarkOperation.FullMatch => Checksum(_pythonRegex.FullMatch(InputBytes)),
        PythonReBenchmarkOperation.SearchDetailed => Checksum(_pythonRegex.SearchDetailedData(InputBytes)),
        PythonReBenchmarkOperation.Count => _pythonRegex.Count(InputBytes),
        PythonReBenchmarkOperation.FindAllStrings => Checksum(_pythonRegex.FindAllToStrings(InputBytes)),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(_pythonRegex.FindAllToUtf8(InputBytes)),
        PythonReBenchmarkOperation.FindIterDetailed => Checksum(_pythonRegex.FindIterDetailed(InputBytes)),
        PythonReBenchmarkOperation.ReplaceString => Checksum(_pythonRegex.ReplaceToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.ReplaceUtf8 => Checksum(_pythonRegex.Replace(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnString => Checksum(_pythonRegex.SubnToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnUtf8 => Checksum(_pythonRegex.Subn(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnEvaluatorString => ExecutePythonReEvaluatorString(),
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => ExecutePythonReEvaluatorUtf8(),
        PythonReBenchmarkOperation.SplitStrings => Checksum(_pythonRegex.SplitToStrings(InputBytes)),
        _ => throw new InvalidOperationException(),
    };

    internal ulong ExecutePythonReSemanticDigest() => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => SemanticDigest(
            _case.Operation,
            _pythonRegex.IsMatch(InputBytes)),
        PythonReBenchmarkOperation.Search => SemanticDigest(
            _case.Operation,
            _pythonRegex.Search(InputBytes)),
        PythonReBenchmarkOperation.Match => SemanticDigest(
            _case.Operation,
            _pythonRegex.Match(InputBytes)),
        PythonReBenchmarkOperation.FullMatch => SemanticDigest(
            _case.Operation,
            _pythonRegex.FullMatch(InputBytes)),
        PythonReBenchmarkOperation.SearchDetailed => SemanticDigest(
            _case.Operation,
            _pythonRegex.SearchDetailedData(InputBytes)),
        PythonReBenchmarkOperation.Count => SemanticDigest(
            _case.Operation,
            _pythonRegex.Count(InputBytes)),
        PythonReBenchmarkOperation.FindAllStrings => SemanticDigest(
            _case.Operation,
            _pythonRegex.FindAllToStrings(InputBytes)),
        PythonReBenchmarkOperation.FindAllUtf8 => SemanticDigest(
            _case.Operation,
            _pythonRegex.FindAllToUtf8(InputBytes)),
        PythonReBenchmarkOperation.FindIterDetailed => SemanticDigest(
            _case.Operation,
            _pythonRegex.FindIterDetailed(InputBytes)),
        PythonReBenchmarkOperation.ReplaceString => SemanticDigest(
            _case.Operation,
            _pythonRegex.ReplaceToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.ReplaceUtf8 => SemanticDigest(
            _case.Operation,
            _pythonRegex.Replace(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnString => SemanticDigest(
            _case.Operation,
            _pythonRegex.SubnToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnUtf8 => SemanticDigest(
            _case.Operation,
            _pythonRegex.Subn(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnEvaluatorString => ExecutePythonReEvaluatorStringSemanticDigest(),
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => ExecutePythonReEvaluatorUtf8SemanticDigest(),
        PythonReBenchmarkOperation.SplitStrings => SemanticDigest(
            _case.Operation,
            _pythonRegex.SplitToStrings(InputBytes)),
        _ => throw new InvalidOperationException(),
    };

    internal int ExecuteDecodeThenRegex()
    {
        var decoded = Encoding.UTF8.GetString(InputBytes);
        return ExecuteRegex(decoded);
    }

    internal int ExecutePredecodedRegex() => ExecuteRegex(_decoded);

    internal int ExecutePredecodedDetailedDiscovery()
    {
        if (_case.Operation == PythonReBenchmarkOperation.SearchDetailed)
        {
            var match = _regex.Match(_decoded);
            return match.Success
                ? Combine(1, match.Index, match.Length, match.Groups.Count, 0)
                : 0;
        }

        if (_case.Operation != PythonReBenchmarkOperation.FindIterDetailed)
        {
            throw new InvalidOperationException($"Unsupported shaping operation {_case.Operation}.");
        }

        var checksum = 0;
        for (var match = _regex.Match(_decoded); match.Success; match = match.NextMatch())
        {
            checksum = Combine(checksum, match.Index, match.Length, match.Groups.Count, 0);
        }

        return checksum;
    }

    internal int ExecutePredecodedStagedDetailedProjection()
    {
        var utf8Offsets = GetUtf8Offsets(_decoded);
        if (_case.Operation == PythonReBenchmarkOperation.SearchDetailed)
        {
            return Checksum(MaterializeDetailedStaged(_regex.Match(_decoded), _decoded, utf8Offsets));
        }

        if (_case.Operation != PythonReBenchmarkOperation.FindIterDetailed)
        {
            throw new InvalidOperationException($"Unsupported shaping operation {_case.Operation}.");
        }

        var matches = new List<BclDetailedMatch>();
        for (var match = _regex.Match(_decoded); match.Success; match = match.NextMatch())
        {
            matches.Add(MaterializeDetailedStaged(match, _decoded, utf8Offsets));
        }

        return Checksum(matches.ToArray());
    }

    internal string DecodeInput() => s_strictUtf8.GetString(InputBytes);

    internal int[]? CreateUtf8CoordinateMap() => GetUtf8Offsets(_decoded);

    internal int ExecuteCapturedDiscoveryOnly()
    {
        EnsureCapturedFindAllPhases();
        var checksum = 0;
        var matchCount = 0;
        var searchIndex = 0;
        while (searchIndex <= _decoded.Length)
        {
            var match = _regex.Match(_decoded, searchIndex);
            if (!match.Success)
            {
                break;
            }

            matchCount++;
            checksum = Combine(checksum, match.Index, match.Length);
            searchIndex = checked(match.Index + match.Length);
        }

        return Combine(checksum, matchCount);
    }

    internal PythonReBenchmarkCaptureRange[] CollectCapturedRanges()
    {
        EnsureCapturedFindAllPhases();
        var ranges = new List<PythonReBenchmarkCaptureRange>();
        var searchIndex = 0;
        while (searchIndex <= _decoded.Length)
        {
            var match = _regex.Match(_decoded, searchIndex);
            if (!match.Success)
            {
                break;
            }

            for (var groupIndex = 1; groupIndex <= _captureCount; groupIndex++)
            {
                var group = match.Groups[groupIndex];
                ranges.Add(new PythonReBenchmarkCaptureRange(
                    group.Success,
                    group.Index,
                    group.Length));
            }

            searchIndex = checked(match.Index + match.Length);
        }

        return ranges.ToArray();
    }

    internal Utf8PythonFindAllResult ProjectPreparedCapturedStrings() =>
        ProjectCapturedStrings(_preparedCaptureRanges);

    internal Utf8PythonFindAllUtf8Result ProjectPreparedCapturedUtf8() =>
        ProjectCapturedUtf8(_preparedCaptureRanges, _preparedUtf8Offsets);

    internal Utf8PythonFindAllResult ShapePreparedCapturedStrings() => new()
    {
        Shape = _preparedCapturedStrings.Shape,
        ScalarValues = [.. _preparedCapturedStrings.ScalarValues],
        TupleValues = _preparedCapturedStrings.TupleValues
            .Select(static tuple => tuple.ToArray())
            .ToArray(),
    };

    internal Utf8PythonFindAllUtf8Result ShapePreparedCapturedUtf8() => new()
    {
        Shape = _preparedCapturedUtf8.Shape,
        ScalarValues = [.. _preparedCapturedUtf8.ScalarValues],
        TupleValues = _preparedCapturedUtf8.TupleValues
            .Select(static tuple => tuple.ToArray())
            .ToArray(),
    };

    internal BclFindAllResult ProjectPredecodedCapturedStrings()
    {
        EnsureCapturedFindAllPhases();
        if (_captureCount == 1)
        {
            var values = new List<string>();
            var searchIndex = 0;
            while (searchIndex <= _decoded.Length)
            {
                var match = _regex.Match(_decoded, searchIndex);
                if (!match.Success)
                {
                    break;
                }

                values.Add(match.Groups[1].Value);
                searchIndex = checked(match.Index + match.Length);
            }

            return new BclFindAllResult(
                Utf8PythonFindAllShape.SingleGroup,
                values.ToArray(),
                []);
        }

        var tuples = new List<string[]>();
        var tupleSearchIndex = 0;
        while (tupleSearchIndex <= _decoded.Length)
        {
            var match = _regex.Match(_decoded, tupleSearchIndex);
            if (!match.Success)
            {
                break;
            }

            var tuple = new string[_captureCount];
            for (var groupIndex = 0; groupIndex < tuple.Length; groupIndex++)
            {
                tuple[groupIndex] = match.Groups[groupIndex + 1].Value;
            }

            tuples.Add(tuple);
            tupleSearchIndex = checked(match.Index + match.Length);
        }

        return new BclFindAllResult(
            Utf8PythonFindAllShape.GroupTuple,
            [],
            tuples.ToArray());
    }

    internal BclFindAllUtf8Result ProjectPredecodedCapturedUtf8()
    {
        EnsureCapturedFindAllPhases();
        return MaterializeFindAllUtf8(_decoded);
    }

    internal Utf8PythonFindAllResult ExecutePythonReFindAllStrings()
    {
        return _pythonRegex.FindAllToStrings(InputBytes);
    }

    internal Utf8PythonFindAllUtf8Result ExecutePythonReFindAllUtf8()
    {
        return _pythonRegex.FindAllToUtf8(InputBytes);
    }

    internal int ExecutePredecodedCapturedProjectionChecksum() => _case.Operation switch
    {
        PythonReBenchmarkOperation.FindAllStrings => Checksum(ProjectPredecodedCapturedStrings()),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(ProjectPredecodedCapturedUtf8()),
        _ => throw new InvalidOperationException(),
    };

    internal int ExecutePreparedCapturedProjectionChecksum() => _case.Operation switch
    {
        PythonReBenchmarkOperation.FindAllStrings => Checksum(_preparedCapturedStrings),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(_preparedCapturedUtf8),
        _ => throw new InvalidOperationException(),
    };

    internal int ExecutePreparedCapturedFinalShapeChecksum() => _case.Operation switch
    {
        PythonReBenchmarkOperation.FindAllStrings => Checksum(ShapePreparedCapturedStrings()),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(ShapePreparedCapturedUtf8()),
        _ => throw new InvalidOperationException(),
    };

    private Utf8PythonFindAllResult ProjectCapturedStrings(
        ReadOnlySpan<PythonReBenchmarkCaptureRange> ranges)
    {
        EnsureCapturedFindAllPhases();
        if (_captureCount == 1)
        {
            var values = new string[ranges.Length];
            for (var index = 0; index < values.Length; index++)
            {
                var range = ranges[index];
                values[index] = range.Success
                    ? _decoded.AsSpan(range.StartOffsetInUtf16, range.LengthInUtf16).ToString()
                    : string.Empty;
            }

            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.SingleGroup,
                ScalarValues = values,
                TupleValues = [],
            };
        }

        var matchCount = ranges.Length / _captureCount;
        var tuples = new string[matchCount][];
        for (var matchIndex = 0; matchIndex < matchCount; matchIndex++)
        {
            var tuple = new string[_captureCount];
            for (var groupIndex = 0; groupIndex < tuple.Length; groupIndex++)
            {
                var range = ranges[(matchIndex * _captureCount) + groupIndex];
                tuple[groupIndex] = range.Success
                    ? _decoded.AsSpan(range.StartOffsetInUtf16, range.LengthInUtf16).ToString()
                    : string.Empty;
            }

            tuples[matchIndex] = tuple;
        }

        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.GroupTuple,
            ScalarValues = [],
            TupleValues = tuples,
        };
    }

    private Utf8PythonFindAllUtf8Result ProjectCapturedUtf8(
        ReadOnlySpan<PythonReBenchmarkCaptureRange> ranges,
        int[]? utf8Offsets)
    {
        EnsureCapturedFindAllPhases();
        if (_captureCount == 1)
        {
            var values = new byte[ranges.Length][];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = GetCapturedUtf8Value(ranges[index], utf8Offsets);
            }

            return new Utf8PythonFindAllUtf8Result
            {
                Shape = Utf8PythonFindAllShape.SingleGroup,
                ScalarValues = values,
                TupleValues = [],
            };
        }

        var matchCount = ranges.Length / _captureCount;
        var tuples = new byte[matchCount][][];
        for (var matchIndex = 0; matchIndex < matchCount; matchIndex++)
        {
            var tuple = new byte[_captureCount][];
            for (var groupIndex = 0; groupIndex < tuple.Length; groupIndex++)
            {
                tuple[groupIndex] = GetCapturedUtf8Value(
                    ranges[(matchIndex * _captureCount) + groupIndex],
                    utf8Offsets);
            }

            tuples[matchIndex] = tuple;
        }

        return new Utf8PythonFindAllUtf8Result
        {
            Shape = Utf8PythonFindAllShape.GroupTuple,
            ScalarValues = [],
            TupleValues = tuples,
        };
    }

    private byte[] GetCapturedUtf8Value(
        PythonReBenchmarkCaptureRange range,
        int[]? utf8Offsets)
    {
        if (!range.Success)
        {
            return [];
        }

        var startOffsetInBytes = utf8Offsets is null
            ? range.StartOffsetInUtf16
            : utf8Offsets[range.StartOffsetInUtf16];
        var endOffsetInBytes = utf8Offsets is null
            ? range.EndOffsetInUtf16
            : utf8Offsets[range.EndOffsetInUtf16];
        return InputBytes.AsSpan(startOffsetInBytes, endOffsetInBytes - startOffsetInBytes).ToArray();
    }

    private void EnsureCapturedFindAllPhases()
    {
        if (!SupportsCapturedFindAllPhases)
        {
            throw new InvalidOperationException("Captured FindAll phase controls are not available for this case.");
        }
    }

    internal int ExecuteCoreEnumerationOnly()
    {
        var checksum = 0;
        var enumerator = GetCoreFindAllRegex().EnumerateMatches(InputBytes);
        while (enumerator.MoveNext())
        {
            var match = enumerator.Current;
            checksum = Combine(
                checksum,
                match.IndexInUtf16,
                match.LengthInUtf16,
                match.IndexInBytes,
                match.LengthInBytes);
        }

        return checksum;
    }

    internal int ExecuteCoreRangeCollectionOnly()
    {
        var checksum = 0;
        foreach (var range in CollectCoreRanges())
        {
            checksum = Combine(
                checksum,
                range.IndexInBytes,
                range.LengthInBytes,
                range.IndexInUtf16,
                range.LengthInUtf16);
        }

        return checksum;
    }

    internal PythonReBenchmarkRange[] CollectCoreRangesArray() => CollectCoreRanges().ToArray();

    internal int ExecutePreparedCoreRangeProjection() => ProjectCoreRanges(_preparedCoreRanges);

    internal int ExecutePreparedCoreChecksumTraversal() => _case.Operation switch
    {
        PythonReBenchmarkOperation.FindAllStrings => Checksum(_preparedCoreStrings),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(_preparedCoreUtf8),
        _ => throw new InvalidOperationException(),
    };

    internal Utf8PythonFindAllResult ProjectPreparedCoreStrings() =>
        ProjectCoreRangeStrings(_preparedCoreRanges);

    internal Utf8PythonFindAllUtf8Result ProjectPreparedCoreUtf8() =>
        ProjectCoreRangeUtf8(_preparedCoreRanges);

    internal Utf8PythonFindAllResult ProjectCollectedCoreStrings() =>
        ProjectCoreRangeStrings(CollectionsMarshal.AsSpan(CollectCoreRanges()));

    internal Utf8PythonFindAllUtf8Result ProjectCollectedCoreUtf8() =>
        ProjectCoreRangeUtf8(CollectionsMarshal.AsSpan(CollectCoreRanges()));

    internal int ExecuteCoreCollectedProjection()
    {
        var ranges = CollectCoreRanges();
        return ProjectCoreRanges(CollectionsMarshal.AsSpan(ranges));
    }

    internal int ExecuteCoreStreamingProjection()
    {
        if (_case.Operation == PythonReBenchmarkOperation.FindAllStrings)
        {
            return Checksum(StreamCoreStrings());
        }

        return Checksum(StreamCoreUtf8());
    }

    internal Utf8PythonFindAllResult StreamCoreStrings()
    {
        var values = new List<string>();
        foreach (var match in GetCoreFindAllRegex().EnumerateMatches(InputBytes))
        {
            if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
            {
                throw new InvalidOperationException("Core FindAll phase model produced a non-contiguous byte range.");
            }

            values.Add(Encoding.UTF8.GetString(InputBytes.AsSpan(indexInBytes, lengthInBytes)));
        }

        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.FullMatch,
            ScalarValues = values.ToArray(),
            TupleValues = [],
        };
    }

    internal Utf8PythonFindAllUtf8Result StreamCoreUtf8()
    {
        var byteValues = new List<byte[]>();
        foreach (var match in GetCoreFindAllRegex().EnumerateMatches(InputBytes))
        {
            if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
            {
                throw new InvalidOperationException("Core FindAll phase model produced a non-contiguous byte range.");
            }

            byteValues.Add(InputBytes.AsSpan(indexInBytes, lengthInBytes).ToArray());
        }

        return new Utf8PythonFindAllUtf8Result
        {
            Shape = Utf8PythonFindAllShape.FullMatch,
            ScalarValues = byteValues.ToArray(),
            TupleValues = [],
        };
    }

    private List<PythonReBenchmarkRange> CollectCoreRanges()
    {
        var ranges = new List<PythonReBenchmarkRange>();
        foreach (var match in GetCoreFindAllRegex().EnumerateMatches(InputBytes))
        {
            if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
            {
                throw new InvalidOperationException("Core FindAll phase model produced a non-contiguous byte range.");
            }

            ranges.Add(new PythonReBenchmarkRange(
                indexInBytes,
                lengthInBytes,
                match.IndexInUtf16,
                match.LengthInUtf16));
        }

        return ranges;
    }

    private int ProjectCoreRanges(ReadOnlySpan<PythonReBenchmarkRange> ranges)
    {
        if (_case.Operation == PythonReBenchmarkOperation.FindAllStrings)
        {
            return Checksum(ProjectCoreRangeStrings(ranges));
        }

        return Checksum(ProjectCoreRangeUtf8(ranges));
    }

    private Utf8PythonFindAllResult ProjectCoreRangeStrings(
        ReadOnlySpan<PythonReBenchmarkRange> ranges)
    {
        var values = new string[ranges.Length];
        for (var index = 0; index < values.Length; index++)
        {
            var range = ranges[index];
            values[index] = Encoding.UTF8.GetString(InputBytes.AsSpan(range.IndexInBytes, range.LengthInBytes));
        }

        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.FullMatch,
            ScalarValues = values,
            TupleValues = [],
        };
    }

    private Utf8PythonFindAllUtf8Result ProjectCoreRangeUtf8(
        ReadOnlySpan<PythonReBenchmarkRange> ranges)
    {
        var byteValues = new byte[ranges.Length][];
        for (var index = 0; index < byteValues.Length; index++)
        {
            var range = ranges[index];
            byteValues[index] = InputBytes.AsSpan(range.IndexInBytes, range.LengthInBytes).ToArray();
        }

        return new Utf8PythonFindAllUtf8Result
        {
            Shape = Utf8PythonFindAllShape.FullMatch,
            ScalarValues = byteValues,
            TupleValues = [],
        };
    }

    private Utf8Regex GetCoreFindAllRegex() => _coreFindAllRegex ??
        throw new InvalidOperationException("Capture-free FindAll phase controls are not available for this case.");

    private int ExecutePythonReEvaluatorString()
    {
        _callbackChecksum = 0;
        var result = _pythonRegex.SubnToString(
            InputBytes,
            this,
            static (context, match) =>
            {
                context._callbackChecksum = Combine(context._callbackChecksum, Checksum(match));
                return context._case.Replacement;
            });
        return Combine(Checksum(result), _callbackChecksum);
    }

    private int ExecutePythonReEvaluatorUtf8()
    {
        _callbackChecksum = 0;
        var result = _pythonRegex.Subn(
            InputBytes,
            this,
            static (context, match) =>
            {
                context._callbackChecksum = Combine(context._callbackChecksum, Checksum(match));
                return context._replacementBytes;
            });
        return Combine(Checksum(result), _callbackChecksum);
    }

    private ulong ExecutePythonReEvaluatorStringSemanticDigest()
    {
        _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
        var result = _pythonRegex.SubnToString(
            InputBytes,
            this,
            static (context, match) =>
            {
                context._callbackSemanticDigest = ExtendCallbackSemanticDigest(
                    context._callbackSemanticDigest,
                    match);
                return context._case.Replacement;
            });
        return SemanticDigest(_case.Operation, result, _callbackSemanticDigest);
    }

    private ulong ExecutePythonReEvaluatorUtf8SemanticDigest()
    {
        _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
        var result = _pythonRegex.Subn(
            InputBytes,
            this,
            static (context, match) =>
            {
                context._callbackSemanticDigest = ExtendCallbackSemanticDigest(
                    context._callbackSemanticDigest,
                    match);
                return context._replacementBytes;
            });
        return SemanticDigest(_case.Operation, result, _callbackSemanticDigest);
    }

    private int ExecuteRegex(string input) => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _regex.IsMatch(input) ? 1 : 0,
        PythonReBenchmarkOperation.Search => Checksum(_regex.Match(input)),
        PythonReBenchmarkOperation.Match => ChecksumAtStart(_regex.Match(input)),
        PythonReBenchmarkOperation.FullMatch => Checksum(_fullRegex.Match(input)),
        PythonReBenchmarkOperation.SearchDetailed => Checksum(MaterializeDetailed(_regex.Match(input), input, GetUtf8Offsets(input))),
        PythonReBenchmarkOperation.Count => _regex.Count(input),
        PythonReBenchmarkOperation.FindAllStrings => Checksum(MaterializeFindAllStrings(input)),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(MaterializeFindAllUtf8(input)),
        PythonReBenchmarkOperation.FindIterDetailed => Checksum(MaterializeFindIterDetailed(input)),
        PythonReBenchmarkOperation.ReplaceString => Checksum(_regex.Replace(input, _case.Replacement)),
        PythonReBenchmarkOperation.ReplaceUtf8 => Checksum(Encoding.UTF8.GetBytes(_regex.Replace(input, _case.Replacement))),
        PythonReBenchmarkOperation.SubnString => Checksum(ReplaceAndCount(input, encodeUtf8: false, materializeCallback: false)),
        PythonReBenchmarkOperation.SubnUtf8 => Checksum(ReplaceAndCount(input, encodeUtf8: true, materializeCallback: false)),
        PythonReBenchmarkOperation.SubnEvaluatorString => Checksum(ReplaceAndCount(input, encodeUtf8: false, materializeCallback: true)),
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => Checksum(ReplaceAndCount(input, encodeUtf8: true, materializeCallback: true)),
        PythonReBenchmarkOperation.SplitStrings => Checksum(_regex.Split(input)),
        _ => throw new InvalidOperationException(),
    };

    private BclFindAllResult MaterializeFindAllStrings(string input)
    {
        if (_captureCount <= 1)
        {
            var values = new List<string>();
            foreach (Match match in _regex.Matches(input))
            {
                values.Add(_captureCount == 0 ? match.Value : match.Groups[1].Value);
            }

            return new BclFindAllResult(_captureCount == 0 ? Utf8PythonFindAllShape.FullMatch : Utf8PythonFindAllShape.SingleGroup, values.ToArray(), []);
        }

        var tuples = new List<string[]>();
        foreach (Match match in _regex.Matches(input))
        {
            var tuple = new string[_captureCount];
            for (var group = 0; group < tuple.Length; group++)
            {
                tuple[group] = match.Groups[group + 1].Value;
            }

            tuples.Add(tuple);
        }

        return new BclFindAllResult(Utf8PythonFindAllShape.GroupTuple, [], tuples.ToArray());
    }

    private BclFindAllUtf8Result MaterializeFindAllUtf8(string input)
    {
        var strings = MaterializeFindAllStrings(input);
        if (strings.Shape != Utf8PythonFindAllShape.GroupTuple)
        {
            var values = new byte[strings.ScalarValues.Length][];
            for (var index = 0; index < values.Length; index++)
            {
                values[index] = Encoding.UTF8.GetBytes(strings.ScalarValues[index]);
            }

            return new BclFindAllUtf8Result(strings.Shape, values, []);
        }

        var tuples = new byte[strings.TupleValues.Length][][];
        for (var match = 0; match < tuples.Length; match++)
        {
            tuples[match] = new byte[strings.TupleValues[match].Length][];
            for (var group = 0; group < tuples[match].Length; group++)
            {
                tuples[match][group] = Encoding.UTF8.GetBytes(strings.TupleValues[match][group]);
            }
        }

        return new BclFindAllUtf8Result(Utf8PythonFindAllShape.GroupTuple, [], tuples);
    }

    private BclDetailedMatch[] MaterializeFindIterDetailed(string input)
    {
        var utf8Offsets = GetUtf8Offsets(input);
        var matches = new List<BclDetailedMatch>();
        for (var match = _regex.Match(input); match.Success; match = match.NextMatch())
        {
            matches.Add(MaterializeDetailed(match, input, utf8Offsets));
        }

        return matches.ToArray();
    }

    private BclSubnResult ReplaceAndCount(string input, bool encodeUtf8, bool materializeCallback)
    {
        var count = 0;
        var callbackChecksum = 0;
        var utf8Offsets = materializeCallback ? GetUtf8Offsets(input) : null;
        var result = _regex.Replace(input, match =>
        {
            count++;
            if (materializeCallback)
            {
                callbackChecksum = Combine(callbackChecksum, Checksum(MaterializeDetailed(match, input, utf8Offsets)));
            }

            return _case.Replacement;
        });
        return new BclSubnResult(
            result,
            encodeUtf8 ? Encoding.UTF8.GetBytes(result) : null,
            count,
            materializeCallback ? callbackChecksum : null);
    }

    private static BclDetailedMatch MaterializeDetailed(Match match, string input, int[]? utf8Offsets)
    {
        if (!match.Success)
        {
            return new BclDetailedMatch([]);
        }

        var groups = new BclDetailedGroup[match.Groups.Count];
        for (var index = 0; index < groups.Length; index++)
        {
            var group = match.Groups[index];
            groups[index] = group.Success
                ? new BclDetailedGroup(
                    true,
                    utf8Offsets is null ? group.Index : utf8Offsets[group.Index],
                    utf8Offsets is null ? group.Index + group.Length : utf8Offsets[group.Index + group.Length],
                    group.Index,
                    group.Index + group.Length,
                    group.Value)
                : new BclDetailedGroup(false, 0, 0, 0, 0, string.Empty);
        }

        return new BclDetailedMatch(groups);
    }

    private static BclDetailedMatch MaterializeDetailedStaged(Match match, string input, int[]? utf8Offsets)
    {
        if (!match.Success)
        {
            return new BclDetailedMatch([]);
        }

        var staged = new BclStagedDetailedGroup[match.Groups.Count];
        for (var index = 0; index < staged.Length; index++)
        {
            var group = match.Groups[index];
            staged[index] = group.Success
                ? new BclStagedDetailedGroup(
                    true,
                    utf8Offsets is null ? group.Index : utf8Offsets[group.Index],
                    utf8Offsets is null ? group.Index + group.Length : utf8Offsets[group.Index + group.Length],
                    group.Index,
                    group.Index + group.Length)
                : default;
        }

        var groups = new BclDetailedGroup[staged.Length];
        for (var index = 0; index < groups.Length; index++)
        {
            var group = staged[index];
            groups[index] = group.Success
                ? new BclDetailedGroup(
                    true,
                    group.StartOffsetInBytes,
                    group.EndOffsetInBytes,
                    group.StartOffsetInUtf16,
                    group.EndOffsetInUtf16,
                    input[group.StartOffsetInUtf16..group.EndOffsetInUtf16])
                : new BclDetailedGroup(false, 0, 0, 0, 0, string.Empty);
        }

        return new BclDetailedMatch(groups);
    }

    private static int[] BuildUtf8Offsets(string input)
    {
        var offsets = new int[input.Length + 1];
        var utf16 = 0;
        var utf8 = 0;
        while (utf16 < input.Length)
        {
            offsets[utf16] = utf8;
            var value = input[utf16];
            if (char.IsHighSurrogate(value) && utf16 + 1 < input.Length && char.IsLowSurrogate(input[utf16 + 1]))
            {
                offsets[utf16 + 1] = utf8;
                utf16 += 2;
                utf8 += 4;
                offsets[utf16] = utf8;
                continue;
            }

            utf8 += value <= 0x7f ? 1 : value <= 0x7ff ? 2 : 3;
            utf16++;
            offsets[utf16] = utf8;
        }

        return offsets;
    }

    private int[]? GetUtf8Offsets(string input) => InputBytes.Length == input.Length
        ? null
        : BuildUtf8Offsets(input);

    private static ulong SemanticDigest(PythonReBenchmarkOperation operation, bool value)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.Add(value);
        return digest.Value;
    }

    private static ulong GetConsumptionToken(Utf8PythonValueMatch match)
    {
        if (!match.Success)
        {
            return 1;
        }

        return checked(
            2UL +
            (uint)match.StartOffsetInBytes +
            (uint)match.EndOffsetInBytes +
            (uint)match.StartOffsetInUtf16 +
            (uint)match.EndOffsetInUtf16);
    }

    private static ulong SemanticDigest(PythonReBenchmarkOperation operation, int value)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.Add(value);
        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonValueMatch match)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.Add(match.Success);
        if (match.Success)
        {
            digest.Add(match.StartOffsetInBytes);
            digest.Add(match.EndOffsetInBytes);
            digest.Add(match.StartOffsetInUtf16);
            digest.Add(match.EndOffsetInUtf16);
            digest.AddString(match.GetValueString());
        }

        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonDetailedMatchData match)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        AppendDetailedSemanticDigest(ref digest, match);
        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonDetailedMatchData[] matches)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.Add(matches.Length);
        foreach (var match in matches)
        {
            AppendDetailedSemanticDigest(ref digest, match);
        }

        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonFindAllResult result)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        AppendFindAllSemanticDigest(ref digest, result.Shape, result.ScalarValues, result.TupleValues);
        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonFindAllUtf8Result result)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        AppendFindAllSemanticDigest(ref digest, result.Shape, result.ScalarValues, result.TupleValues);
        return digest.Value;
    }

    private static ulong SemanticDigest(PythonReBenchmarkOperation operation, string value)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.AddString(value);
        return digest.Value;
    }

    private static ulong SemanticDigest(PythonReBenchmarkOperation operation, byte[] value)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.AddBytes(value);
        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonSubnResult result)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.AddString(result.ResultText);
        digest.Add(result.ReplacementCount);
        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonSubnResult result,
        ulong callbackSemanticDigest)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.AddString(result.ResultText);
        digest.Add(result.ReplacementCount);
        digest.Add(callbackSemanticDigest);
        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonSubnUtf8Result result)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.AddBytes(result.ResultBytes);
        digest.Add(result.ReplacementCount);
        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonSubnUtf8Result result,
        ulong callbackSemanticDigest)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.AddBytes(result.ResultBytes);
        digest.Add(result.ReplacementCount);
        digest.Add(callbackSemanticDigest);
        return digest.Value;
    }

    private static ulong SemanticDigest(PythonReBenchmarkOperation operation, string?[] values)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.Add(values.Length);
        foreach (var value in values)
        {
            digest.Add(value is not null);
            if (value is not null)
            {
                digest.AddString(value);
            }
        }

        return digest.Value;
    }

    private static ulong ExtendCallbackSemanticDigest(
        ulong semanticDigest,
        Utf8PythonDetailedMatchData match)
    {
        var digest = new PythonReSemanticDigestBuilder(semanticDigest);
        digest.Add(0xCA11_BACC);
        AppendDetailedSemanticDigest(ref digest, match);
        return digest.Value;
    }

    private static void AppendDetailedSemanticDigest(
        ref PythonReSemanticDigestBuilder digest,
        Utf8PythonDetailedMatchData match)
    {
        digest.Add(match.Success);
        if (!match.Success)
        {
            return;
        }

        var groups = match.Groups ?? [];
        digest.Add(groups.Length);
        foreach (var group in groups)
        {
            digest.Add(group.Success);
            digest.Add(group.StartOffsetInBytes);
            digest.Add(group.EndOffsetInBytes);
            digest.Add(group.StartOffsetInUtf16);
            digest.Add(group.EndOffsetInUtf16);
            digest.AddString(group.ValueText);
        }
    }

    private static void AppendFindAllSemanticDigest(
        ref PythonReSemanticDigestBuilder digest,
        Utf8PythonFindAllShape shape,
        string[] scalarValues,
        string[][] tupleValues)
    {
        digest.Add((int)shape);
        var count = shape == Utf8PythonFindAllShape.GroupTuple
            ? tupleValues.Length
            : scalarValues.Length;
        digest.Add(count);
        foreach (var value in scalarValues)
        {
            digest.AddString(value);
        }

        foreach (var tuple in tupleValues)
        {
            digest.Add(tuple.Length);
            foreach (var value in tuple)
            {
                digest.AddString(value);
            }
        }
    }

    private static void AppendFindAllSemanticDigest(
        ref PythonReSemanticDigestBuilder digest,
        Utf8PythonFindAllShape shape,
        byte[][] scalarValues,
        byte[][][] tupleValues)
    {
        digest.Add((int)shape);
        var count = shape == Utf8PythonFindAllShape.GroupTuple
            ? tupleValues.Length
            : scalarValues.Length;
        digest.Add(count);
        foreach (var value in scalarValues)
        {
            digest.AddBytes(value);
        }

        foreach (var tuple in tupleValues)
        {
            digest.Add(tuple.Length);
            foreach (var value in tuple)
            {
                digest.AddBytes(value);
            }
        }
    }

    private static int Checksum(Utf8PythonValueMatch match) => match.Success
        ? Combine(1, match.StartOffsetInUtf16, match.EndOffsetInUtf16)
        : 0;

    private static int Checksum(Match match) => match.Success
        ? Combine(1, match.Index, match.Index + match.Length)
        : 0;

    private static int ChecksumAtStart(Match match) => match.Success && match.Index == 0
        ? Checksum(match)
        : 0;

    private static int Checksum(Utf8PythonDetailedMatchData match)
    {
        var checksum = match.Success ? 1 : 0;
        foreach (var group in match.Groups ?? [])
        {
            checksum = Combine(checksum, group.Success ? 1 : 0, group.StartOffsetInUtf16, group.EndOffsetInUtf16, Checksum(group.ValueText));
        }

        return checksum;
    }

    private static int Checksum(BclDetailedMatch match)
    {
        var checksum = match.Groups.Length == 0 ? 0 : 1;
        foreach (var group in match.Groups)
        {
            checksum = Combine(checksum, group.Success ? 1 : 0, group.StartOffsetInUtf16, group.EndOffsetInUtf16, Checksum(group.Value));
        }

        return checksum;
    }

    private static int Checksum(Utf8PythonDetailedMatchData[] matches)
    {
        var checksum = matches.Length;
        foreach (var match in matches)
        {
            checksum = Combine(checksum, Checksum(match));
        }

        return checksum;
    }

    private static int Checksum(BclDetailedMatch[] matches)
    {
        var checksum = matches.Length;
        foreach (var match in matches)
        {
            checksum = Combine(checksum, Checksum(match));
        }

        return checksum;
    }

    private static int Checksum(Utf8PythonFindAllResult result) =>
        Checksum(result.Shape, result.ScalarValues, result.TupleValues);

    private static int Checksum(BclFindAllResult result) =>
        Checksum(result.Shape, result.ScalarValues, result.TupleValues);

    private static int Checksum(Utf8PythonFindAllUtf8Result result) =>
        Checksum(result.Shape, result.ScalarValues, result.TupleValues);

    private static int Checksum(BclFindAllUtf8Result result) =>
        Checksum(result.Shape, result.ScalarValues, result.TupleValues);

    private static int Checksum(Utf8PythonFindAllShape shape, string[] scalarValues, string[][] tupleValues)
    {
        var checksum = (int)shape;
        foreach (var value in scalarValues)
        {
            checksum = Combine(checksum, Checksum(value));
        }

        foreach (var tuple in tupleValues)
        {
            checksum = Combine(checksum, tuple.Length);
            foreach (var value in tuple)
            {
                checksum = Combine(checksum, Checksum(value));
            }
        }

        return checksum;
    }

    private static int Checksum(Utf8PythonFindAllShape shape, byte[][] scalarValues, byte[][][] tupleValues)
    {
        var checksum = (int)shape;
        foreach (var value in scalarValues)
        {
            checksum = Combine(checksum, Checksum(value));
        }

        foreach (var tuple in tupleValues)
        {
            checksum = Combine(checksum, tuple.Length);
            foreach (var value in tuple)
            {
                checksum = Combine(checksum, Checksum(value));
            }
        }

        return checksum;
    }

    private static int Checksum(Utf8PythonSubnResult result) =>
        Combine(Checksum(result.ResultText), result.ReplacementCount);

    private static int Checksum(Utf8PythonSubnUtf8Result result) =>
        Combine(Checksum(result.ResultBytes), result.ReplacementCount);

    private static int Checksum(BclSubnResult result)
    {
        var checksum = result.ResultBytes is null
            ? Combine(Checksum(result.ResultText), result.ReplacementCount)
            : Combine(Checksum(result.ResultBytes), result.ReplacementCount);
        return result.CallbackChecksum is int callbackChecksum
            ? Combine(checksum, callbackChecksum)
            : checksum;
    }

    private static int Checksum(string?[] values)
    {
        var checksum = values.Length;
        foreach (var value in values)
        {
            checksum = Combine(checksum, value is null ? -1 : Checksum(value));
        }

        return checksum;
    }

    private static int Checksum(string value)
    {
        var checksum = value.Length;
        foreach (var character in value)
        {
            checksum = Combine(checksum, character);
        }

        return checksum;
    }

    private static int Checksum(byte[] value)
    {
        var checksum = value.Length;
        foreach (var item in value)
        {
            checksum = Combine(checksum, item);
        }

        return checksum;
    }

    private static int Combine(int seed, int value) => unchecked((seed * 31) + value);

    private static int Combine(int seed, int value1, int value2) =>
        Combine(Combine(seed, value1), value2);

    private static int Combine(int seed, int value1, int value2, int value3, int value4) =>
        Combine(Combine(Combine(Combine(seed, value1), value2), value3), value4);

    private static RegexOptions ToRegexOptions(PythonReCompileOptions options)
    {
        var result = RegexOptions.CultureInvariant;
        if ((options & PythonReCompileOptions.IgnoreCase) != 0)
        {
            result |= RegexOptions.IgnoreCase;
        }
        if ((options & PythonReCompileOptions.Multiline) != 0)
        {
            result |= RegexOptions.Multiline;
        }
        if ((options & PythonReCompileOptions.DotAll) != 0)
        {
            result |= RegexOptions.Singleline;
        }
        if ((options & PythonReCompileOptions.Verbose) != 0)
        {
            result |= RegexOptions.IgnorePatternWhitespace;
        }
        if ((options & PythonReCompileOptions.Ascii) != 0)
        {
            result |= RegexOptions.ECMAScript;
        }

        return result;
    }
}

internal sealed record BclFindAllResult(Utf8PythonFindAllShape Shape, string[] ScalarValues, string[][] TupleValues);

internal sealed record BclFindAllUtf8Result(Utf8PythonFindAllShape Shape, byte[][] ScalarValues, byte[][][] TupleValues);

internal sealed record BclDetailedMatch(BclDetailedGroup[] Groups);

internal readonly record struct BclStagedDetailedGroup(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int StartOffsetInUtf16,
    int EndOffsetInUtf16);

internal struct PythonReSemanticDigestBuilder
{
    private const ulong Prime = 0x0000_0100_0000_01B3;
    internal const ulong Offset = 0xCBF2_9CE4_8422_2325;
    private ulong _value;

    internal PythonReSemanticDigestBuilder(PythonReBenchmarkOperation operation)
    {
        _value = Offset;
        Add((int)operation + 1);
    }

    internal PythonReSemanticDigestBuilder(ulong value)
    {
        _value = value;
    }

    internal readonly ulong Value => _value;

    internal void Add(bool value) => Add(value ? 1 : 0);

    internal void Add(int value) => Add(unchecked((ulong)(long)value));

    internal void Add(ulong value) => _value = unchecked((_value ^ value) * Prime);

    internal void AddString(string value)
    {
        Add(1);
        Add(value.Length);
        foreach (var character in value)
        {
            Add(character);
        }
    }

    internal void AddBytes(ReadOnlySpan<byte> value)
    {
        Add(2);
        Add(value.Length);
        foreach (var item in value)
        {
            Add(item);
        }
    }
}

internal readonly record struct PythonReBenchmarkRange(
    int IndexInBytes,
    int LengthInBytes,
    int IndexInUtf16,
    int LengthInUtf16);

internal readonly record struct PythonReBenchmarkCaptureRange(
    bool Success,
    int StartOffsetInUtf16,
    int LengthInUtf16)
{
    internal int EndOffsetInUtf16 => StartOffsetInUtf16 + LengthInUtf16;
}

internal readonly record struct BclDetailedGroup(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int StartOffsetInUtf16,
    int EndOffsetInUtf16,
    string Value);

internal sealed record BclSubnResult(
    string ResultText,
    byte[]? ResultBytes,
    int ReplacementCount,
    int? CallbackChecksum);

internal readonly record struct PythonReBenchmarkBatch(
    TimeSpan Elapsed,
    long AllocatedBytes,
    int Checksum,
    ulong SemanticDigest,
    ulong ConsumptionChecksum);

internal sealed class PythonReBenchmarkSnapshot
{
    public required int SchemaVersion { get; init; }
    public required DateTimeOffset GeneratedAtUtc { get; init; }
    public required PythonReCorpusProvenance Corpus { get; init; }
    public required SortedDictionary<string, PythonReCaseMeasurement> Cases { get; init; }
}

internal sealed class PythonReCorpusProvenance
{
    public required string SourceFile { get; init; }
    public required string Sha256 { get; init; }
    public required int VectorCount { get; init; }
    public required string UpstreamCpythonRevision { get; init; }
    public required string Limitation { get; init; }
}

internal sealed class PythonReCaseMeasurement
{
    public required string Pattern { get; init; }
    public required string Options { get; init; }
    public required string Operation { get; init; }
    public required int InputUtf8Bytes { get; init; }
    public required int EffectiveIterations { get; init; }
    public required int Samples { get; init; }
    public required bool IncludesResultMaterialization { get; init; }
    public string ComparatorOwner { get; set; } = string.Empty;
    public string ManagedRoute { get; set; } = string.Empty;
    public bool ByteControlEligible { get; set; }
    public string ByteControlReason { get; set; } = string.Empty;
    public required PythonReBenchmarkEnvironment Environment { get; init; }
    public required PythonReOperationMeasurement PythonRe { get; init; }
    public required PythonReOperationMeasurement DecodeThenRegex { get; init; }
    public required PythonReOperationMeasurement PredecodedRegex { get; init; }
    public CpythonBenchmarkMeasurement? Cpython { get; init; }
    public PythonReQualificationMeasurement? Qualification { get; set; }
}

internal sealed class PythonReQualificationMeasurement
{
    private const string HistoricalReason =
        "Historical independent-median evidence predates paired qualification protocol v3.";

    public required string Status { get; init; }
    public required string StatusReason { get; init; }
    public required string EngineEvidenceBasis { get; init; }
    public required string EngineConclusion { get; init; }
    public string EngineConclusionReason { get; init; } = "No engine-comparable evidence is available.";
    public PythonRePairedEvidence? PairedEvidence { get; init; }

    internal static PythonReQualificationMeasurement CreateHistoricalUnqualified() => new()
    {
        Status = "Unqualified",
        StatusReason = HistoricalReason,
        EngineEvidenceBasis = "Not engine-comparable",
        EngineConclusion = "NotApplicable",
        EngineConclusionReason = "Historical evidence has no engine-comparable control.",
        PairedEvidence = null,
    };

    internal static PythonReQualificationMeasurement CreateUnqualified(string reason) => new()
    {
        Status = "Unqualified",
        StatusReason = reason,
        EngineEvidenceBasis = "Not engine-comparable",
        EngineConclusion = "NotApplicable",
        EngineConclusionReason = reason,
        PairedEvidence = null,
    };
}

internal sealed class PythonRePairedEvidence
{
    public required int ProtocolVersion { get; init; }
    public required string QualificationId { get; init; }
    public required DateTimeOffset MeasuredAtUtc { get; init; }
    public required string SourceCommit { get; init; }
    public required string Baseline { get; init; }
    public string ResultContract { get; init; } = string.Empty;
    public required string InitialLane { get; init; }
    public required bool WorktreeQualified { get; init; }
    public required string CaseDefinitionSha256 { get; init; }
    public required string CatalogSha256 { get; init; }
    public required string SemanticDigestAlgorithm { get; init; }
    public required string SemanticDigest { get; init; }
    public required string CpuPolicy { get; init; }
    public required string CpuAffinityMask { get; init; }
    public required int? CpuEfficiencyClass { get; init; }
    public required int ManagedIterations { get; init; }
    public required int CpythonIterations { get; init; }
    public required int ManagedWarmupCalls { get; init; }
    public required double ManagedWarmupMilliseconds { get; init; }
    public required int CpythonWarmupCalls { get; init; }
    public required double CpythonWarmupMilliseconds { get; init; }
    public required double ManagedMedianMicroseconds { get; init; }
    public required double CpythonMedianMicroseconds { get; init; }
    public required double StrongRatioMedian { get; init; }
    public required double StrongRatioLower95 { get; init; }
    public required double StrongRatioUpper95 { get; init; }
    public required double StrongDifferenceMicroseconds { get; init; }
    public required double OrderEffect { get; init; }
    public required double ManagedInterquartileSpread { get; init; }
    public required double CpythonInterquartileSpread { get; init; }
    public required double ManagedHarnessFloorFraction { get; init; }
    public required double CpythonHarnessFloorFraction { get; init; }
    public required long ManagedMedianAllocatedBytes { get; init; }
    public required PythonRePairedSampleEvidence[] Samples { get; init; }
    public required double[] ManagedEmptyLoopMicroseconds { get; init; }
    public required double[] CpythonEmptyLoopMicroseconds { get; init; }
    public double[] ManagedTrivialCallMicroseconds { get; init; } = [];
    public double[] CpythonTrivialCallMicroseconds { get; init; } = [];
    public PythonReByteControlEvidence? ByteControl { get; init; }
    public required CpythonStreamEnvironment CpythonEnvironment { get; init; }
    public required PythonReBenchmarkEnvironment ManagedEnvironment { get; init; }
}

internal sealed class PythonReByteControlEvidence
{
    public required string EligibilityReason { get; init; }
    public required int CpythonIterations { get; init; }
    public required int CpythonWarmupCalls { get; init; }
    public required double CpythonWarmupMilliseconds { get; init; }
    public required double CpythonMedianMicroseconds { get; init; }
    public required double RatioMedian { get; init; }
    public required double RatioLower95 { get; init; }
    public required double RatioUpper95 { get; init; }
    public required double OrderEffect { get; init; }
    public required double ManagedInterquartileSpread { get; init; }
    public required double CpythonInterquartileSpread { get; init; }
    public required double ManagedTrivialCallFraction { get; init; }
    public required double CpythonTrivialCallFraction { get; init; }
    public required string EngineConclusion { get; init; }
    public required string EngineConclusionReason { get; init; }
    public required PythonReByteControlSampleEvidence[] Samples { get; init; }
    public required double[] CpythonEmptyLoopMicroseconds { get; init; }
    public required double[] CpythonTrivialCallMicroseconds { get; init; }
}

internal sealed class PythonReByteControlSampleEvidence
{
    public required string Order { get; init; }
    public required double CpythonMicroseconds { get; init; }
    public required double Ratio { get; init; }
    public required double CpythonElapsedMilliseconds { get; init; }
    public required double CpythonProcessCpuMilliseconds { get; init; }
    public required int[] CpythonGcCollections { get; init; }
}

internal sealed class PythonRePairedSampleEvidence
{
    public required string Order { get; init; }
    public required double ManagedMicroseconds { get; init; }
    public required double CpythonMicroseconds { get; init; }
    public required double StrongRatio { get; init; }
    public required double ManagedElapsedMilliseconds { get; init; }
    public required double CpythonElapsedMilliseconds { get; init; }
    public required double ManagedProcessCpuMilliseconds { get; init; }
    public required double CpythonProcessCpuMilliseconds { get; init; }
    public required int[] ManagedGcCollections { get; init; }
    public required int[] CpythonGcCollections { get; init; }
    public required long ManagedAllocatedBytes { get; init; }
}

internal sealed class PythonReOperationMeasurement
{
    public required double MedianMicroseconds { get; init; }
    public required double MinimumMicroseconds { get; init; }
    public required double MaximumMicroseconds { get; init; }
    public required long MedianAllocatedBytes { get; init; }
    public required int WarmupCalls { get; init; }
    public required double WarmupMilliseconds { get; init; }
}

internal sealed class CpythonBenchmarkRequest
{
    public required int ProtocolVersion { get; init; }
    public required string Pattern { get; init; }
    public required int Options { get; init; }
    public required string Operation { get; init; }
    public required string InputBase64 { get; init; }
    public required string Replacement { get; init; }
    public required int Iterations { get; init; }
    public required int Samples { get; init; }
}

internal sealed class CpythonBenchmarkMeasurement
{
    public required int ProtocolVersion { get; init; }
    public required CpythonBenchmarkEnvironment Environment { get; init; }
    public required int Checksum { get; init; }
    public required CpythonOperationMeasurement PredecodedRe { get; init; }
    public required CpythonOperationMeasurement DecodeThenRe { get; init; }
}

internal sealed class CpythonOperationMeasurement
{
    public required double MedianMicroseconds { get; init; }
    public required double MinimumMicroseconds { get; init; }
    public required double MaximumMicroseconds { get; init; }
    public required int EffectiveIterations { get; init; }
    public required int WarmupCalls { get; init; }
    public required double WarmupMilliseconds { get; init; }
}

internal sealed class CpythonBenchmarkEnvironment
{
    public required string Implementation { get; init; }
    public required string Version { get; init; }
    public required string Executable { get; init; }
    public required string Platform { get; init; }
}

internal sealed class PythonReBenchmarkEnvironment
{
    public required string SourceCommit { get; init; }
    public required bool TrackedDirty { get; init; }
    public required bool HasUntrackedFiles { get; init; }
    public required string Runtime { get; init; }
    public required string OperatingSystem { get; init; }
    public required string Processor { get; init; }
}
