using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const string SnapshotFileName = "PythonRe.Benchmarks.json";
    private const string CpythonRunnerRelativePath = "bench/Lokad.Utf8Regex.Benchmarks/pythonre_cpython_benchmark.py";
    private const int PythonReBenchmarkSchemaVersion = 9;
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

        if (args.Length >= 1 && args[0].Equals("--resume-pythonre-qualifications", StringComparison.Ordinal))
        {
            exitCode = ResumePythonReQualifications(
                Math.Min(ParsePositive(args, 1, 9), 17),
                Math.Min(ParsePositive(args, 2, 17), 17),
                Math.Min(ParsePositive(args, 3, 4), PythonReBenchmarkCatalog.Cases.Count));
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pythonre-priority-report", StringComparison.Ordinal))
        {
            exitCode = EmitPythonRePriorityReport();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pythonre-freshness-report", StringComparison.Ordinal))
        {
            exitCode = EmitPythonReFreshnessReport();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pythonre-coverage-report", StringComparison.Ordinal))
        {
            exitCode = EmitPythonReCoverageReport();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--verify-pythonre-coverage-contract", StringComparison.Ordinal))
        {
            exitCode = VerifyPythonReCoverageContract();
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

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-replacement-phases", StringComparison.Ordinal))
        {
            exitCode = MeasureReplacementPhases(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 7));
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-one-shot-phases", StringComparison.Ordinal))
        {
            exitCode = MeasureOneShotPhases(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 7));
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-one-shot-scaling", StringComparison.Ordinal))
        {
            exitCode = MeasureOneShotScaling(
                args[1],
                Math.Min(ParsePositive(args, 2, 20), 1_000_000),
                Math.Min(ParsePositive(args, 3, 3), 7));
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
            CatalogSha256 = ComputePythonReCatalogSha256(),
            CatalogCaseIds = GetPythonReCatalogCaseIds(),
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
            CatalogSha256 = ComputePythonReCatalogSha256(),
            CatalogCaseIds = GetPythonReCatalogCaseIds(),
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
        if (snapshot is null || snapshot.SchemaVersion is not 3 and not 4 and not 5 and not 6 and not 7 and not 8 and
            not PythonReBenchmarkSchemaVersion)
        {
            Console.Error.WriteLine(
                $"PythonRe migration requires a schema-3 through schema-7 or " +
                $"schema-{PythonReBenchmarkSchemaVersion} snapshot.");
            return 1;
        }

        if (snapshot.SchemaVersion == PythonReBenchmarkSchemaVersion)
        {
            Console.WriteLine($"{SnapshotFileName} is already schema {PythonReBenchmarkSchemaVersion}.");
            return 0;
        }

        var legacyQualificationSetMatches = snapshot.SchemaVersion == 6 &&
            ComputePythonReLegacyQualificationSetSha256(snapshot)
                .Equals(PythonReLegacySchema6QualificationSetSha256, StringComparison.Ordinal);
        var currentManagedProductSha256 = ComputePythonReManagedProductSha256();
        var currentManaged = CaptureEnvironment();
        CpythonStreamWorker? worker = null;
        if (legacyQualificationSetMatches)
        {
            worker = new CpythonStreamWorker();
        }

        var migrated = 0;
        var invalidated = 0;
        foreach (var (caseId, measurement) in snapshot.Cases)
        {
            RefreshMeasurementMetadata(caseId, measurement);
            var evidence = measurement.Qualification?.PairedEvidence;
            if (evidence is null)
            {
                measurement.Qualification = PythonReQualificationMeasurement.CreateUnqualified(
                    measurement.Qualification?.StatusReason ??
                    "Historical independent-median evidence predates paired qualification protocol v3.");
                continue;
            }

            if (snapshot.SchemaVersion is 7 or 8)
            {
                migrated++;
                continue;
            }

            var benchmarkCase = PythonReBenchmarkCatalog.Cases.Single(
                candidate => candidate.Id.Equals(caseId, StringComparison.Ordinal));
            var failure = legacyQualificationSetMatches && worker is not null
                ? TryMigratePythonReSchema6Evidence(
                    benchmarkCase,
                    measurement,
                    evidence,
                    currentManagedProductSha256,
                    worker.Environment,
                    currentManaged)
                : "The paired evidence does not match the exact recognized schema-6 qualification set.";
            if (failure is not null)
            {
                measurement.Qualification = PythonReQualificationMeasurement.CreateUnqualified(failure);
                invalidated++;
                continue;
            }

            migrated++;
        }

        worker?.Dispose();

        WriteSnapshot(new PythonReBenchmarkSnapshot
        {
            SchemaVersion = PythonReBenchmarkSchemaVersion,
            GeneratedAtUtc = snapshot.GeneratedAtUtc,
            CatalogSha256 = ComputePythonReCatalogSha256(),
            CatalogCaseIds = GetPythonReCatalogCaseIds(),
            Corpus = snapshot.Corpus,
            Cases = snapshot.Cases,
        });
        Console.WriteLine(
            $"Migrated {SnapshotFileName} to schema {PythonReBenchmarkSchemaVersion}; " +
            $"preserved {migrated} exact paired rows and invalidated {invalidated} unproven rows.");
        return 0;
    }

    private static string? TryMigratePythonReSchema6Evidence(
        PythonReBenchmarkCase benchmarkCase,
        PythonReCaseMeasurement measurement,
        PythonRePairedEvidence evidence,
        string currentManagedProductSha256,
        CpythonStreamEnvironment currentCpython,
        PythonReBenchmarkEnvironment currentManaged)
    {
        var inputBytes = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        var legacyCaseDefinitionSha256 =
            ComputeLegacyPythonReCaseDefinitionSha256(benchmarkCase, inputBytes);
        var currentRunnerSha256 = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(FindRepositoryFile(CpythonRunnerRelativePath))));
        var expectedLegacyQualificationId = ComputeLegacyPythonReQualificationId(
            evidence.CaseDefinitionSha256,
            evidence.CatalogSha256,
            evidence.SourceCommit,
            evidence.CpythonEnvironment,
            evidence.CpuPolicy,
            evidence.CpuAffinityMask,
            evidence.CpuEfficiencyClass,
            evidence.InitialLane.Equals("CPython", StringComparison.Ordinal),
            evidence.Samples.Length);
        var productChanges = RunGit(
            "diff",
            "--name-only",
            evidence.SourceCommit,
            "HEAD",
            "--",
            "Directory.Build.props",
            "src/Lokad.Utf8Regex",
            "src/Lokad.Utf8Regex.PythonRe",
            ":(exclude)src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md");

        if (evidence.ProtocolVersion != PythonReQualificationProtocolVersion ||
            !evidence.WorktreeQualified ||
            !evidence.CaseDefinitionSha256.Equals(legacyCaseDefinitionSha256, StringComparison.Ordinal) ||
            !evidence.CatalogSha256.Equals(PythonReLegacySchema6CatalogSha256, StringComparison.Ordinal) ||
            !evidence.CpythonEnvironment.RunnerSha256.Equals(
                PythonReLegacySchema6RunnerSha256,
                StringComparison.Ordinal) ||
            !currentRunnerSha256.Equals(PythonReLegacySchema6RunnerSha256, StringComparison.Ordinal) ||
            !evidence.QualificationId.Equals(expectedLegacyQualificationId, StringComparison.Ordinal) ||
            !evidence.ResultContract.Equals(GetPythonReResultContract(benchmarkCase), StringComparison.Ordinal) ||
            !evidence.SemanticDigestAlgorithm.Equals(PythonReSemanticDigestAlgorithm, StringComparison.Ordinal) ||
            !PythonReManagedRuntimeMatches(evidence.ManagedEnvironment, currentManaged) ||
            !PythonReInterpreterMatches(evidence.CpythonEnvironment, currentCpython) ||
            productChanges is null ||
            !string.IsNullOrWhiteSpace(productChanges))
        {
            return "Schema-6 paired evidence failed exact case, product, interpreter, runner, result-contract, or protocol equivalence.";
        }

        evidence.CaseDefinitionSha256 = ComputePythonReCaseDefinitionSha256(benchmarkCase, inputBytes);
        evidence.ManagedProductSha256 = currentManagedProductSha256;
        evidence.ManagedOperationProtocolSha256 =
            ComputePythonReManagedOperationProtocolSha256(benchmarkCase.Operation);
        evidence.CpythonOperationProtocolSha256 =
            ComputePythonReCpythonOperationProtocolSha256(benchmarkCase.Operation);
        evidence.SharedProtocolSha256 = ComputePythonReSharedProtocolSha256();
        evidence.QualificationId = ComputePythonReQualificationId(
            evidence.CaseDefinitionSha256,
            evidence.ManagedProductSha256,
            evidence.ManagedOperationProtocolSha256,
            evidence.CpythonOperationProtocolSha256,
            evidence.SharedProtocolSha256,
            evidence.ManagedEnvironment,
            evidence.CpythonEnvironment,
            evidence.CpuPolicy,
            evidence.CpuAffinityMask,
            evidence.CpuEfficiencyClass,
            evidence.InitialLane.Equals("CPython", StringComparison.Ordinal),
            evidence.Samples.Length);
        return null;
    }

    private static string ComputePythonReLegacyQualificationSetSha256(
        PythonReBenchmarkSnapshot snapshot) => ComputePythonReSha256(
            string.Join(
                '\n',
                snapshot.Cases.Select(pair =>
                    $"{pair.Key}={pair.Value.Qualification?.PairedEvidence?.QualificationId ?? "<none>"}")));

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
        foreach (var (caseId, measurement) in snapshot.Cases)
        {
            RefreshMeasurementMetadata(caseId, measurement);

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
            CatalogSha256 = ComputePythonReCatalogSha256(),
            CatalogCaseIds = GetPythonReCatalogCaseIds(),
            Corpus = snapshot.Corpus,
            Cases = snapshot.Cases,
        });
        Console.WriteLine($"Invalidated {invalidated} PythonRe paired qualifications.");
        return 0;
    }

    private static void RefreshMeasurementMetadata(string caseId, PythonReCaseMeasurement measurement)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(caseId, StringComparison.Ordinal)) ??
            throw new InvalidOperationException($"PythonRe snapshot contains unknown case '{caseId}'.");
        var context = new PythonReBenchmarkContext(benchmarkCase);
        var byteControl = PythonReBenchmarkCatalog.GetByteControlEligibility(
            benchmarkCase,
            context.InputBytes);
        measurement.ComparatorOwner = PythonReBenchmarkCatalog.GetComparatorOwner(benchmarkCase);
        measurement.Coverage = benchmarkCase.Coverage;
        measurement.ManagedRoute = context.DescribeManagedRoute();
        measurement.ByteControlEligible = byteControl.IsEligible;
        measurement.ByteControlReason = byteControl.Reason;
        measurement.InputSha256 = Convert.ToHexString(SHA256.HashData(context.InputBytes));
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
        var repeatedValue = context.SupportsRepeatedCoreStringReplay
            ? context.ExecuteRepeatedCoreStringProjection()
            : expected;
        var countedRepeatedValue = context.SupportsRepeatedCoreStringReplay
            ? context.ExecuteCountedRepeatedCoreStringProjection()
            : expected;
        if (expected != prepared ||
            expected != collected ||
            expected != streaming ||
            expected != repeatedValue ||
            expected != countedRepeatedValue)
        {
            throw new InvalidOperationException(
                $"PythonRe FindAll phase diagnostic '{id}' produced incomparable sinks: " +
                $"public={expected}, prepared={prepared}, collected={collected}, streaming={streaming}, " +
                $"repeated-value={repeatedValue}, counted-repeated-value={countedRepeatedValue}.");
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
            PrintOperation("PythonRePublic", MeasureRetainedPhaseOperation(context.ExecutePythonReFindAllUtf8, effectiveIterations, samples));
        }
        else
        {
            PrintOperation("PythonRePublic", MeasureRetainedPhaseOperation(context.ExecutePythonReFindAllStrings, effectiveIterations, samples));
        }

        PrintOperation("DecodeComparator", MeasurePhaseOperation(context.ExecuteDecodeThenRegex, effectiveIterations, samples));
        PrintOperation("CoreEnumerateOnly", MeasurePhaseOperation(context.ExecuteCoreEnumerationOnly, effectiveIterations, samples));
        PrintOperation("CoreCollectRanges", MeasureRetainedPhaseOperation(context.CollectCoreRangesArray, effectiveIterations, samples));
        if (benchmarkCase.Operation == PythonReBenchmarkOperation.FindAllUtf8)
        {
            PrintOperation("PreparedProjection", MeasureRetainedPhaseOperation(context.ProjectPreparedCoreUtf8, effectiveIterations, samples));
            PrintOperation("CollectedProjection", MeasureRetainedPhaseOperation(context.ProjectCollectedCoreUtf8, effectiveIterations, samples));
            PrintOperation("StreamingProjection", MeasureRetainedPhaseOperation(context.StreamCoreUtf8, effectiveIterations, samples));
        }
        else
        {
            PrintOperation("PreparedProjection", MeasureRetainedPhaseOperation(context.ProjectPreparedCoreStrings, effectiveIterations, samples));
            PrintOperation("CollectedProjection", MeasureRetainedPhaseOperation(context.ProjectCollectedCoreStrings, effectiveIterations, samples));
            PrintOperation("StreamingProjection", MeasureRetainedPhaseOperation(context.StreamCoreStrings, effectiveIterations, samples));
            if (context.SupportsRepeatedCoreStringReplay)
            {
                PrintOperation("RepeatedValueProjection", MeasureRetainedPhaseOperation(context.ProjectRepeatedCoreStrings, effectiveIterations, samples));
                PrintOperation("CountedRepeatedProjection", MeasureRetainedPhaseOperation(context.CountAndProjectRepeatedCoreStrings, effectiveIterations, samples));
            }
        }

        PrintOperation("ChecksumTraversal", MeasurePhaseOperation(context.ExecutePreparedCoreChecksumTraversal, effectiveIterations, samples));
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
        var trailingCapture = context.SupportsTrailingCaptureReplay
            ? context.ExecuteTrailingCaptureReplayChecksum()
            : expected;
        var separatedCaptures = context.SupportsSeparatedCapturesReplay
            ? context.ExecuteSeparatedCapturesReplayChecksum()
            : expected;
        if (expected != predecoded ||
            expected != prepared ||
            expected != finalShape ||
            expected != trailingCapture ||
            expected != separatedCaptures)
        {
            throw new InvalidOperationException(
                $"PythonRe captured FindAll phase diagnostic '{benchmarkCase.Id}' produced incomparable sinks: " +
                $"public={expected}, predecoded={predecoded}, prepared={prepared}, " +
                $"final={finalShape}, trailing-capture={trailingCapture}, " +
                $"separated-captures={separatedCaptures}.");
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
        PrintOperation("Utf8DecodeOnly", MeasureRetainedPhaseOperation(context.DecodeInput, iterations, samples));
        PrintOperation("PredecodedDiscovery", MeasurePhaseOperation(context.ExecuteCapturedDiscoveryOnly, iterations, samples));
        PrintOperation("CaptureRangeCollect", MeasureRetainedPhaseOperation(context.CollectCapturedRanges, iterations, samples));
        if (benchmarkCase.Operation == PythonReBenchmarkOperation.FindAllUtf8)
        {
            PrintOperation("CoordinateMapOnly", MeasureRetainedPhaseOperation(context.CreateUtf8CoordinateMap, iterations, samples));
            PrintOperation("PreparedProjection", MeasureRetainedPhaseOperation(context.ProjectPreparedCapturedUtf8, iterations, samples));
            PrintOperation("PreparedFinalShape", MeasureRetainedPhaseOperation(context.ShapePreparedCapturedUtf8, iterations, samples));
            PrintOperation("PredecodedProjection", MeasureRetainedPhaseOperation(context.ProjectPredecodedCapturedUtf8, iterations, samples));
            PrintOperation("PythonRePublic", MeasureRetainedPhaseOperation(context.ExecutePythonReFindAllUtf8, iterations, samples));
        }
        else
        {
            PrintOperation("PreparedProjection", MeasureRetainedPhaseOperation(context.ProjectPreparedCapturedStrings, iterations, samples));
            PrintOperation("PreparedFinalShape", MeasureRetainedPhaseOperation(context.ShapePreparedCapturedStrings, iterations, samples));
            PrintOperation("PredecodedProjection", MeasureRetainedPhaseOperation(context.ProjectPredecodedCapturedStrings, iterations, samples));
            if (context.SupportsTrailingCaptureReplay)
            {
                PrintOperation("TrailingCaptureReplay", MeasureRetainedPhaseOperation(context.ProjectTrailingCaptureStrings, iterations, samples));
            }

            if (context.SupportsSeparatedCapturesReplay)
            {
                PrintOperation("SeparatedCapturesReplay", MeasureRetainedPhaseOperation(context.ProjectSeparatedCaptureStrings, iterations, samples));
            }

            PrintOperation("PythonRePublic", MeasureRetainedPhaseOperation(context.ExecutePythonReFindAllStrings, iterations, samples));
        }

        PrintOperation("ChecksumTraversal", MeasurePhaseOperation(context.ExecutePreparedCapturedProjectionChecksum, iterations, samples));
        return 0;
    }

    private static int MeasureReplacementPhases(string id, int iterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        if (benchmarkCase.Operation is not PythonReBenchmarkOperation.ReplaceString and
            not PythonReBenchmarkOperation.ReplaceUtf8 and
            not PythonReBenchmarkOperation.SubnString and
            not PythonReBenchmarkOperation.SubnUtf8)
        {
            Console.Error.WriteLine(
                $"PythonRe replacement phase diagnostics require a fixed Replace/Subn operation; " +
                $"'{id}' uses {benchmarkCase.Operation}.");
            return 1;
        }

        var effectiveIterations = GetEffectiveIterations(benchmarkCase, iterations);
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        var context = new PythonReBenchmarkContext(benchmarkCase);
        if (!context.SupportsReplacementPhases)
        {
            Console.Error.WriteLine(
                $"PythonRe replacement phase diagnostics currently require a non-empty-match pattern " +
                $"and a literal replacement; '{id}' is unsupported.");
            return 1;
        }

        var expected = context.ExecutePythonReReplacementChecksum();
        var predecoded = context.ExecutePredecodedRequiredReplacementChecksum();
        var decoded = context.ExecuteDecodedRequiredReplacementChecksum();
        var utf8Core = context.ExecuteUtf8CoreRequiredReplacementChecksum();
        var countedUtf8Core = context.ExecuteUtf8CoreCountedLiteralReplacementChecksum();
        var onePass = context.ExecuteDecodedOnePassLiteralReplacementChecksum();
        if (expected != predecoded ||
            expected != decoded ||
            expected != utf8Core ||
            expected != countedUtf8Core ||
            expected != onePass)
        {
            throw new InvalidOperationException(
                $"PythonRe replacement phase diagnostic '{id}' produced incomparable sinks: " +
                $"public={expected}, predecoded={predecoded}, decoded={decoded}, " +
                $"UTF-8 core={utf8Core}, counted UTF-8 core={countedUtf8Core}, " +
                $"one-pass={onePass}.");
        }

        if (context.ExecuteUtf8CoreReplacementOutputChecksum() !=
                context.ExecutePreparedReplacementOutputChecksum() ||
            context.ExecuteUtf8CoreReplacementCount() != context.PreparedReplacementMatchCount)
        {
            throw new InvalidOperationException(
                $"PythonRe replacement phase diagnostic '{id}' produced incomparable UTF-8 core components.");
        }

        Console.WriteLine($"CaseId             : {benchmarkCase.Id}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"ManagedRoute       : {context.DescribeManagedRoute()}");
        Console.WriteLine($"Utf8CoreRoute      : {context.ReplacementCoreExecutionKind}");
        Console.WriteLine($"InputBytes         : {context.InputBytes.Length}");
        Console.WriteLine($"MatchCount         : {context.PreparedReplacementMatchCount}");
        Console.WriteLine($"ReturnsCount       : {context.ReturnsReplacementCount}");
        Console.WriteLine($"OutputUtf8Bytes    : {context.PreparedReplacementOutputUtf8Bytes}");
        Console.WriteLine($"Iterations         : {effectiveIterations}");
        Console.WriteLine($"Samples            : {samples}");
        Console.WriteLine($"CpuPolicy          : {processorScope.Policy}");
        Console.WriteLine($"CpuAffinityMask    : {processorScope.AffinityMask}");
        Console.WriteLine("Phase model        : cumulative controls; phase timings are not additive");
        PrintOperation("ReplacementParse", MeasureRetainedPhaseOperation(context.ParseReplacement, effectiveIterations, samples));
        PrintOperation("ReplacementTranslate", MeasureRetainedPhaseOperation(context.TranslateReplacement, effectiveIterations, samples));
        PrintOperation("Utf8DecodeOnly", MeasureRetainedPhaseOperation(context.DecodeInput, effectiveIterations, samples));
        if (benchmarkCase.Operation is PythonReBenchmarkOperation.SubnString or PythonReBenchmarkOperation.SubnUtf8)
        {
            PrintOperation("PredecodedCount", MeasurePhaseOperation(context.ExecutePredecodedReplacementCount, effectiveIterations, samples));
            PrintOperation("Utf8CoreCount", MeasurePhaseOperation(context.ExecuteUtf8CoreReplacementCount, effectiveIterations, samples));
        }

        PrintOperation("PredecodedReplace", MeasureRetainedPhaseOperation(context.ExecutePredecodedReplacementText, effectiveIterations, samples));
        if (benchmarkCase.Operation is PythonReBenchmarkOperation.ReplaceUtf8 or PythonReBenchmarkOperation.SubnUtf8)
        {
            PrintOperation("PreparedUtf8Encode", MeasureRetainedPhaseOperation(context.EncodePreparedReplacementUtf8, effectiveIterations, samples));
        }

        PrintOperation("PredecodedRequired", MeasureRetainedPhaseOperation(context.ExecutePredecodedRequiredReplacement, effectiveIterations, samples));
        PrintOperation("DecodeRequired", MeasureRetainedPhaseOperation(context.ExecuteDecodedRequiredReplacement, effectiveIterations, samples));
        PrintOperation("Utf8CoreReplace", MeasureRetainedPhaseOperation(context.ExecuteUtf8CoreReplacementOutput, effectiveIterations, samples));
        PrintOperation("Utf8CoreRequired", MeasureRetainedPhaseOperation(context.ExecuteUtf8CoreRequiredReplacement, effectiveIterations, samples));
        PrintOperation("Utf8CoreCounted", MeasureRetainedPhaseOperation(context.ExecuteUtf8CoreCountedLiteralReplacement, effectiveIterations, samples));
        PrintOperation("PredecodedOnePass", MeasureRetainedPhaseOperation(context.ExecutePredecodedOnePassLiteralReplacement, effectiveIterations, samples));
        PrintOperation("DecodeOnePass", MeasureRetainedPhaseOperation(context.ExecuteDecodedOnePassLiteralReplacement, effectiveIterations, samples));
        PrintOperation("PythonRePublic", MeasureRetainedPhaseOperation(context.ExecutePythonReReplacement, effectiveIterations, samples));
        PrintOperation("ChecksumTraversal", MeasurePhaseOperation(context.ExecutePreparedReplacementChecksum, effectiveIterations, samples));
        return 0;
    }

    private static int MeasureOneShotPhases(string id, int iterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        if (benchmarkCase.Operation is not PythonReBenchmarkOperation.Search and
            not PythonReBenchmarkOperation.Match)
        {
            Console.Error.WriteLine(
                $"PythonRe one-shot phase diagnostics require Search or Match; " +
                $"'{id}' uses {benchmarkCase.Operation}.");
            return 1;
        }

        var effectiveIterations = GetEffectiveIterations(benchmarkCase, iterations);
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        var context = new PythonReBenchmarkContext(benchmarkCase);
        if (!context.SupportsOneShotPhases)
        {
            Console.Error.WriteLine(
                $"PythonRe one-shot phase diagnostics currently require an ASCII Search or Match case; " +
                $"'{id}' is unsupported.");
            return 1;
        }

        var expected = context.ExecutePythonReOneShot();
        var direct = context.ExecuteCoreDirectOneShot();
        var offset = context.ExecuteCoreOffsetZeroOneShot();
        var rawExact = context.SupportsExactLiteralOneShotReplay
            ? context.ExecuteCoreRawOneShot()
            : expected;
        var rawPrefixDigits = context.SupportsAsciiPrefixDigitOneShotReplay
            ? context.ExecuteRawAsciiPrefixDigitOneShot()
            : expected;
        var validatedPrefixDigits = context.SupportsAsciiPrefixDigitOneShotReplay
            ? context.ExecuteValidatedAsciiPrefixDigitOneShot()
            : expected;
        var predecoded = context.ExecutePredecodedOneShot();
        if (expected != direct ||
            expected != offset ||
            expected != rawExact ||
            expected != rawPrefixDigits ||
            expected != validatedPrefixDigits ||
            expected != predecoded)
        {
            throw new InvalidOperationException(
                $"PythonRe one-shot phase diagnostic '{id}' produced incomparable results: " +
                $"public={expected}, direct={direct}, offset={offset}, raw-exact={rawExact}, " +
                $"raw-prefix-digits={rawPrefixDigits}, validated-prefix-digits={validatedPrefixDigits}, " +
                $"predecoded={predecoded}.");
        }

        Console.WriteLine($"CaseId             : {benchmarkCase.Id}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"ManagedRoute       : {context.DescribeManagedRoute()}");
        Console.WriteLine($"CoreRoute          : {context.OneShotCoreExecutionKind}");
        Console.WriteLine($"InputBytes         : {context.InputBytes.Length}");
        Console.WriteLine($"Result             : {expected}");
        Console.WriteLine($"Iterations         : {effectiveIterations}");
        Console.WriteLine($"Samples            : {samples}");
        Console.WriteLine($"CpuPolicy          : {processorScope.Policy}");
        Console.WriteLine($"CpuAffinityMask    : {processorScope.AffinityMask}");
        Console.WriteLine("Phase model        : cumulative controls; phase timings are not additive");
        PrintOperation("ValidationOnly", MeasureRetainedPhaseOperation(context.ExecuteOneShotValidation, effectiveIterations, samples));
        if (context.SupportsExactLiteralOneShotReplay)
        {
            PrintOperation("RawExactLiteral", MeasureRetainedPhaseOperation(context.ExecuteCoreRawOneShot, effectiveIterations, samples));
        }

        if (context.SupportsAsciiPrefixDigitOneShotReplay)
        {
            PrintOperation("RawPrefixDigits", MeasureRetainedPhaseOperation(context.ExecuteRawAsciiPrefixDigitOneShot, effectiveIterations, samples));
            PrintOperation("ValidatedPrefixDigits", MeasureRetainedPhaseOperation(context.ExecuteValidatedAsciiPrefixDigitOneShot, effectiveIterations, samples));
        }

        PrintOperation("CoreDirect", MeasureRetainedPhaseOperation(context.ExecuteCoreDirectOneShot, effectiveIterations, samples));
        PrintOperation("CoreOffsetZero", MeasureRetainedPhaseOperation(context.ExecuteCoreOffsetZeroOneShot, effectiveIterations, samples));
        PrintOperation("PredecodedRegex", MeasureRetainedPhaseOperation(context.ExecutePredecodedOneShot, effectiveIterations, samples));
        PrintOperation("PythonRePublic", MeasureRetainedPhaseOperation(context.ExecutePythonReOneShot, effectiveIterations, samples));
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
            InputSha256 = Convert.ToHexString(SHA256.HashData(context.InputBytes)),
            EffectiveIterations = effectiveIterations,
            Samples = samples,
            IncludesResultMaterialization = benchmarkCase.IncludesResultMaterialization,
            Coverage = benchmarkCase.Coverage,
            ComparatorOwner = PythonReBenchmarkCatalog.GetComparatorOwner(benchmarkCase),
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
            StartOffsetInBytes = benchmarkCase.Coverage.StartOffsetInBytes,
            ReplacementCount = benchmarkCase.Coverage.ReplacementCount,
            MaxSplit = benchmarkCase.Coverage.MaxSplit,
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
        var inputBytes = Encoding.UTF8.GetByteCount(benchmarkCase.Input);
        if (inputBytes > 512 * 1_024)
        {
            return Math.Max(requestedIterations, 2);
        }

        if (inputBytes > 128 * 1_024)
        {
            return Math.Max(requestedIterations, 5);
        }

        if (benchmarkCase.Operation is PythonReBenchmarkOperation.IsMatch or
                PythonReBenchmarkOperation.Search or
                PythonReBenchmarkOperation.SearchFromOffset or
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
            PythonReBenchmarkOperation.SearchFromOffset or
            PythonReBenchmarkOperation.Match or
            PythonReBenchmarkOperation.FullMatch or
            PythonReBenchmarkOperation.SearchDetailed => 5_000,
            PythonReBenchmarkOperation.Count when benchmarkCase.Id == "zero-width/count" => 5_000,
            PythonReBenchmarkOperation.Count or
            PythonReBenchmarkOperation.CountFromOffset => 500,
            PythonReBenchmarkOperation.FindAllStrings or
            PythonReBenchmarkOperation.FindAllStringsFromOffset or
            PythonReBenchmarkOperation.FindAllUtf8 or
            PythonReBenchmarkOperation.FindIterDetailed or
            PythonReBenchmarkOperation.FindAllStructural => 1_000,
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
        int samples,
        int minimumWarmupCalls = 0)
    {
        var warmup = Stopwatch.StartNew();
        var warmupCalls = 0;
        do
        {
            s_sink ^= operation();
            warmupCalls++;
        }
        while ((warmup.ElapsedMilliseconds < 100 || warmupCalls < minimumWarmupCalls) &&
               warmupCalls < 65_536);

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
        int samples,
        int minimumWarmupCalls = 0)
    {
        var warmup = Stopwatch.StartNew();
        var warmupCalls = 0;
        do
        {
            s_retainedSink = operation();
            warmupCalls++;
        }
        while ((warmup.ElapsedMilliseconds < 100 || warmupCalls < minimumWarmupCalls) &&
               warmupCalls < 65_536);

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

    private static PythonReOperationMeasurement MeasurePhaseOperation(
        Func<int> operation,
        int iterations,
        int samples) => MeasureOperation(operation, iterations, samples, minimumWarmupCalls: 1_024);

    private static PythonReOperationMeasurement MeasureRetainedPhaseOperation<T>(
        Func<T> operation,
        int iterations,
        int samples) => MeasureRetainedOperation(operation, iterations, samples, minimumWarmupCalls: 1_024);

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
    private readonly bool _supportsRepeatedCoreStringReplay;
    private readonly string _repeatedCoreString;
    private readonly bool _supportsCapturedFindAllPhases;
    private readonly PythonReBenchmarkCaptureRange[] _preparedCaptureRanges;
    private readonly int[]? _preparedUtf8Offsets;
    private readonly Utf8PythonFindAllResult _preparedCapturedStrings;
    private readonly Utf8PythonFindAllUtf8Result _preparedCapturedUtf8;
    private readonly bool _supportsTrailingCaptureReplay;
    private readonly int _trailingCapturePrefixLength;
    private readonly bool _supportsSeparatedCapturesReplay;
    private readonly char _captureSeparator;
    private readonly bool _supportsReplacementPhases;
    private readonly Utf8Regex? _coreReplacementRegex;
    private readonly PythonReReplacementPlan _preparedReplacementPlan;
    private readonly string _preparedDotNetReplacement;
    private readonly string _preparedLiteralReplacement;
    private readonly byte[] _preparedLiteralReplacementUtf8;
    private readonly PythonReBenchmarkReplacementResult? _preparedReplacementResult;
    private readonly Utf8Regex? _oneShotCoreRegex;
    private readonly bool _supportsOneShotPhases;
    private readonly bool _supportsExactLiteralOneShotReplay;
    private readonly bool _supportsAsciiPrefixDigitOneShotReplay;
    private readonly byte[] _oneShotAsciiPrefix;
    private readonly string _decoded;
    private readonly byte[] _replacementBytes;
    private readonly string _dotNetReplacement;
    private readonly int _captureCount;
    private readonly int _startOffsetInUtf16;
    private int _callbackChecksum;
    private ulong _callbackSemanticDigest;

    internal PythonReBenchmarkContext(PythonReBenchmarkCase benchmarkCase)
    {
        _case = benchmarkCase;
        InputBytes = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        _decoded = benchmarkCase.Input;
        if ((uint)benchmarkCase.Coverage.StartOffsetInBytes > (uint)InputBytes.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(benchmarkCase),
                "The benchmark start offset must be inside the UTF-8 subject.");
        }

        _startOffsetInUtf16 = s_strictUtf8.GetCharCount(
            InputBytes.AsSpan(0, benchmarkCase.Coverage.StartOffsetInBytes));
        _replacementBytes = Encoding.UTF8.GetBytes(benchmarkCase.Replacement);
        _pythonRegex = new Utf8PythonRegex(benchmarkCase.Pattern, benchmarkCase.Options);
        var parsed = new PythonReParser(benchmarkCase.Pattern).Parse(benchmarkCase.Options);
        var translation = PythonReTranslator.Translate(parsed);
        var translatedPattern = translation.Pattern;
        var regexOptions = translation.RegexOptions;
        _regex = new Regex(translatedPattern, regexOptions, Regex.InfiniteMatchTimeout);
        _fullRegex = new Regex($@"\A(?:{translatedPattern})\z", regexOptions, Regex.InfiniteMatchTimeout);
        _captureCount = _regex.GetGroupNumbers().Length - 1;
        _dotNetReplacement = benchmarkCase.Operation is
            PythonReBenchmarkOperation.ReplaceString or
            PythonReBenchmarkOperation.ReplaceUtf8 or
            PythonReBenchmarkOperation.ReplaceStringLimited or
            PythonReBenchmarkOperation.SubnString or
            PythonReBenchmarkOperation.SubnUtf8
                ? PythonReReplacementParser.Parse(
                    benchmarkCase.Replacement,
                    parsed.CaptureGroupCount,
                    parsed.NamedGroups).ToDotNetReplacementString()
                : benchmarkCase.Replacement;
        if (benchmarkCase.Operation is PythonReBenchmarkOperation.FindAllStrings or PythonReBenchmarkOperation.FindAllUtf8 &&
            _captureCount == 0)
        {
            _coreFindAllRegex = new Utf8Regex(translatedPattern, regexOptions);
            _preparedCoreRanges = CollectCoreRanges().ToArray();
            _preparedCoreStrings = ProjectCoreRangeStrings(_preparedCoreRanges);
            _preparedCoreUtf8 = ProjectCoreRangeUtf8(_preparedCoreRanges);
            _supportsRepeatedCoreStringReplay =
                benchmarkCase.Operation == PythonReBenchmarkOperation.FindAllStrings &&
                _preparedCoreStrings.ScalarValues.Length > 0 &&
                _preparedCoreStrings.ScalarValues.AsSpan(1).IndexOfAnyExcept(
                    _preparedCoreStrings.ScalarValues[0]) < 0;
            _repeatedCoreString = _supportsRepeatedCoreStringReplay
                ? _preparedCoreStrings.ScalarValues[0]
                : string.Empty;
            _supportsCapturedFindAllPhases = false;
            _preparedCaptureRanges = [];
            _preparedUtf8Offsets = [];
            _preparedCapturedStrings = default;
            _preparedCapturedUtf8 = default;
            _supportsTrailingCaptureReplay = false;
            _trailingCapturePrefixLength = 0;
            _supportsSeparatedCapturesReplay = false;
            _captureSeparator = default;
        }
        else if (benchmarkCase.Operation is PythonReBenchmarkOperation.FindAllStrings or PythonReBenchmarkOperation.FindAllUtf8 &&
                 _captureCount > 0 && !_regex.IsMatch(string.Empty))
        {
            _coreFindAllRegex = null;
            _preparedCoreRanges = [];
            _preparedCoreStrings = default;
            _preparedCoreUtf8 = default;
            _supportsRepeatedCoreStringReplay = false;
            _repeatedCoreString = string.Empty;
            _supportsCapturedFindAllPhases = true;
            _preparedCaptureRanges = CollectCapturedRanges();
            _preparedUtf8Offsets = GetUtf8Offsets(_decoded);
            _preparedCapturedStrings = ProjectCapturedStrings(_preparedCaptureRanges);
            _preparedCapturedUtf8 = ProjectCapturedUtf8(_preparedCaptureRanges, _preparedUtf8Offsets);
            _supportsTrailingCaptureReplay = benchmarkCase.Operation == PythonReBenchmarkOperation.FindAllStrings &&
                _captureCount == 1;
            var trailingCapturePrefixLength = -1;
            if (_supportsTrailingCaptureReplay)
            {
                foreach (Match match in _regex.Matches(_decoded))
                {
                    var group = match.Groups[1];
                    var prefixLength = group.Index - match.Index;
                    if (!group.Success ||
                        group.Index + group.Length != match.Index + match.Length ||
                        trailingCapturePrefixLength >= 0 && trailingCapturePrefixLength != prefixLength)
                    {
                        _supportsTrailingCaptureReplay = false;
                        break;
                    }

                    trailingCapturePrefixLength = prefixLength;
                }
            }

            _trailingCapturePrefixLength = Math.Max(0, trailingCapturePrefixLength);
            _supportsSeparatedCapturesReplay = benchmarkCase.Operation == PythonReBenchmarkOperation.FindAllStrings &&
                _captureCount == 2;
            var captureSeparator = default(char);
            var hasCaptureSeparator = false;
            if (_supportsSeparatedCapturesReplay)
            {
                foreach (Match match in _regex.Matches(_decoded))
                {
                    var first = match.Groups[1];
                    var second = match.Groups[2];
                    var separatorIndex = first.Index + first.Length;
                    if (!first.Success ||
                        !second.Success ||
                        first.Index != match.Index ||
                        second.Index + second.Length != match.Index + match.Length ||
                        second.Index - separatorIndex != 1 ||
                        hasCaptureSeparator && _decoded[separatorIndex] != captureSeparator)
                    {
                        _supportsSeparatedCapturesReplay = false;
                        break;
                    }

                    captureSeparator = _decoded[separatorIndex];
                    hasCaptureSeparator = true;
                }

                _supportsSeparatedCapturesReplay &= hasCaptureSeparator;
            }

            _captureSeparator = captureSeparator;
        }
        else
        {
            _coreFindAllRegex = null;
            _preparedCoreRanges = [];
            _preparedCoreStrings = default;
            _preparedCoreUtf8 = default;
            _supportsRepeatedCoreStringReplay = false;
            _repeatedCoreString = string.Empty;
            _supportsCapturedFindAllPhases = false;
            _preparedCaptureRanges = [];
            _preparedUtf8Offsets = [];
            _preparedCapturedStrings = default;
            _preparedCapturedUtf8 = default;
            _supportsTrailingCaptureReplay = false;
            _trailingCapturePrefixLength = 0;
            _supportsSeparatedCapturesReplay = false;
            _captureSeparator = default;
        }

        if (benchmarkCase.Operation is PythonReBenchmarkOperation.ReplaceString or
                PythonReBenchmarkOperation.ReplaceUtf8 or
                PythonReBenchmarkOperation.SubnString or
                PythonReBenchmarkOperation.SubnUtf8 &&
            _captureCount == 0 &&
            !_regex.IsMatch(string.Empty))
        {
            var plan = PythonReReplacementParser.Parse(
                benchmarkCase.Replacement,
                _captureCount,
                new Dictionary<string, int>(StringComparer.Ordinal));
            _supportsReplacementPhases = plan.Tokens.All(
                static token => token.Kind == PythonReReplacementTokenKind.Literal);
            _coreReplacementRegex = _supportsReplacementPhases
                ? new Utf8Regex(translatedPattern, regexOptions)
                : null;
            _preparedReplacementPlan = plan;
            _preparedDotNetReplacement = plan.ToDotNetReplacementString();
            _preparedLiteralReplacement = string.Concat(
                plan.Tokens.Select(static token => token.RequiredText));
            _preparedLiteralReplacementUtf8 = s_strictUtf8.GetBytes(_preparedLiteralReplacement);
            _preparedReplacementResult = _supportsReplacementPhases
                ? ExecutePredecodedRequiredReplacement()
                : null;
        }
        else
        {
            _supportsReplacementPhases = false;
            _coreReplacementRegex = null;
            _preparedReplacementPlan = default;
            _preparedDotNetReplacement = string.Empty;
            _preparedLiteralReplacement = string.Empty;
            _preparedLiteralReplacementUtf8 = [];
            _preparedReplacementResult = null;
        }

        if (benchmarkCase.Operation is PythonReBenchmarkOperation.Search or PythonReBenchmarkOperation.Match &&
            InputBytes.AsSpan().ContainsAnyExceptInRange((byte)0, (byte)0x7f) == false)
        {
            var oneShotCoreRegex = new Utf8Regex(translatedPattern, regexOptions);
            _oneShotCoreRegex = oneShotCoreRegex;
            _supportsOneShotPhases = true;
            _supportsExactLiteralOneShotReplay =
                oneShotCoreRegex.Inspection.ExecutionKind == NativeExecutionKind.ExactAsciiLiteral &&
                oneShotCoreRegex.Inspection.DebugTryMatchExactLiteral(InputBytes, out _);
            var oneShotParseResult = new PythonReParser(benchmarkCase.Pattern).Parse(benchmarkCase.Options);
            var oneShotAsciiPrefix = Array.Empty<byte>();
            _supportsAsciiPrefixDigitOneShotReplay =
                benchmarkCase.Operation == PythonReBenchmarkOperation.Match &&
                PythonReTranslator.TryGetAsciiLiteralPrefixDigitRepeat(
                    oneShotParseResult.Root,
                    oneShotParseResult.Options,
                    out oneShotAsciiPrefix);
            _oneShotAsciiPrefix = oneShotAsciiPrefix;
        }
        else
        {
            _oneShotCoreRegex = null;
            _supportsOneShotPhases = false;
            _supportsExactLiteralOneShotReplay = false;
            _supportsAsciiPrefixDigitOneShotReplay = false;
            _oneShotAsciiPrefix = [];
        }
    }

    internal byte[] InputBytes { get; }

    internal bool SupportsCaptureFreeFindAllPhases => _coreFindAllRegex is not null;

    internal bool SupportsCapturedFindAllPhases => _supportsCapturedFindAllPhases;

    internal bool SupportsRepeatedCoreStringReplay => _supportsRepeatedCoreStringReplay;

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

    internal bool SupportsReplacementPhases => _supportsReplacementPhases;

    internal bool SupportsTrailingCaptureReplay => _supportsTrailingCaptureReplay;

    internal bool SupportsSeparatedCapturesReplay => _supportsSeparatedCapturesReplay;

    internal string ReplacementCoreExecutionKind => GetCoreReplacementRegex()
        .Inspection.ExecutionKind.ToString();

    internal int PreparedReplacementMatchCount => _regex.Count(_decoded);

    internal bool ReturnsReplacementCount => IsSubnOperation;

    internal int PreparedReplacementOutputUtf8Bytes => GetPreparedReplacementResult().ResultBytes?.Length ??
        s_strictUtf8.GetByteCount(GetPreparedReplacementResult().ResultText);

    internal bool SupportsOneShotPhases => _supportsOneShotPhases;

    internal bool SupportsExactLiteralOneShotReplay => _supportsExactLiteralOneShotReplay;

    internal bool SupportsAsciiPrefixDigitOneShotReplay => _supportsAsciiPrefixDigitOneShotReplay;

    internal bool UsesZeroOffsetUtf8ValueFastPath => _pythonRegex.DebugUsesZeroOffsetUtf8ValueFastPath;

    internal bool UsesAsciiLiteralPrefixDigitMatchFastPath =>
        _pythonRegex.DebugUsesAsciiLiteralPrefixDigitMatchFastPath;

    internal string OneShotCoreExecutionKind => GetOneShotCoreRegex()
        .Inspection.ExecutionKind.ToString();

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
        PythonReBenchmarkOperation.SearchFromOffset => DescribeBackend(
            _pythonRegex.DebugSearchBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "value ranges from a nonzero byte offset"),
        PythonReBenchmarkOperation.Match => DescribeBackend(
            _pythonRegex.DebugMatchBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            _pythonRegex.DebugUsesAsciiLiteralPrefixDigitMatchFastPath
                ? "strict validation; direct anchored ASCII literal-prefix/digit-repeat value ranges"
                : "anchored value ranges"),
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
        PythonReBenchmarkOperation.CountFromOffset => DescribeBackend(
            _pythonRegex.DebugCountBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "Python-style count progression from a nonzero byte offset"),
        PythonReBenchmarkOperation.FindAllStrings when _pythonRegex.DebugUsesSingleTrailingCaptureFindAllFastPath =>
            "strict UTF-8 decode; .NET Regex ValueMatch enumeration; direct trailing-capture string shaping",
        PythonReBenchmarkOperation.FindAllStrings when _pythonRegex.DebugUsesSeparatedCaptureTupleFindAllFastPath =>
            "strict UTF-8 decode; .NET Regex ValueMatch enumeration; direct separated-capture tuple shaping",
        PythonReBenchmarkOperation.FindAllStrings when _pythonRegex.DebugUsesRepeatedExactStringFindAllFastPath =>
            _pythonRegex.DebugUsesCountedRepeatedExactStringFindAllFastPath
                ? "Utf8Regex; exact-literal count; repeated immutable string shaping"
                : "Utf8Regex; exact-literal range enumeration; repeated immutable string shaping",
        PythonReBenchmarkOperation.FindAllStrings when _captureCount > 0 =>
            "strict UTF-8 decode; .NET Regex; findall string shaping",
        PythonReBenchmarkOperation.FindAllStrings => DescribeBackend(
            _pythonRegex.DebugFindAllBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "findall string shaping"),
        PythonReBenchmarkOperation.FindAllStringsFromOffset => DescribeBackend(
            _pythonRegex.DebugFindAllBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "findall string shaping from a nonzero byte offset"),
        PythonReBenchmarkOperation.FindAllUtf8 when _captureCount > 0 =>
            "strict UTF-8 decode; .NET Regex; findall UTF-8 shaping",
        PythonReBenchmarkOperation.FindAllUtf8 => DescribeBackend(
            _pythonRegex.DebugFindAllBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "findall UTF-8 shaping"),
        PythonReBenchmarkOperation.FindIterDetailed =>
            "strict UTF-8 decode; .NET Regex; detailed iteration shaping",
        PythonReBenchmarkOperation.FindAllStructural => DescribeBackend(
            _pythonRegex.DebugFindAllBackend,
            _pythonRegex.DebugUtf8ExecutionKind,
            "group-zero structural match shaping"),
        PythonReBenchmarkOperation.ReplaceString or
            PythonReBenchmarkOperation.ReplaceStringLimited or
            PythonReBenchmarkOperation.SubnString =>
            DescribeBackend(
                _pythonRegex.DebugReplaceBackend,
                _pythonRegex.DebugUtf8ExecutionKind,
                "replacement; string shaping"),
        PythonReBenchmarkOperation.ReplaceUtf8 or
            PythonReBenchmarkOperation.SubnUtf8 =>
            DescribeBackend(
                _pythonRegex.DebugReplaceBackend,
                _pythonRegex.DebugUtf8ExecutionKind,
                "replacement; UTF-8 shaping"),
        PythonReBenchmarkOperation.SubnEvaluatorString =>
            "strict UTF-8 decode; .NET Regex callback replacement; string shaping",
        PythonReBenchmarkOperation.ReplaceEvaluatorString =>
            "strict UTF-8 decode; .NET Regex callback replacement; string shaping without count",
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 =>
            "strict UTF-8 decode; .NET Regex callback replacement; UTF-8 shaping",
        PythonReBenchmarkOperation.SplitStrings =>
            "strict UTF-8 decode; .NET Regex split; string shaping",
        PythonReBenchmarkOperation.SplitStringsLimited =>
            "strict UTF-8 decode; .NET Regex bounded split; string shaping",
        PythonReBenchmarkOperation.SplitDetailed =>
            "strict UTF-8 decode; .NET Regex split; item-metadata shaping",
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
            case PythonReBenchmarkOperation.SearchFromOffset:
            {
                Utf8PythonValueMatch result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Search(InputBytes, _case.Coverage.StartOffsetInBytes);
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
            case PythonReBenchmarkOperation.CountFromOffset:
            {
                var result = 0;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Count(InputBytes, _case.Coverage.StartOffsetInBytes);
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
            case PythonReBenchmarkOperation.FindAllStringsFromOffset:
            {
                Utf8PythonFindAllResult result = default;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.FindAllToStrings(
                        InputBytes,
                        _case.Coverage.StartOffsetInBytes);
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
            case PythonReBenchmarkOperation.FindAllStructural:
            {
                Utf8PythonMatchData[] result = [];
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.FindAll(InputBytes);
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
            case PythonReBenchmarkOperation.ReplaceStringLimited:
            {
                var result = string.Empty;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.ReplaceToString(
                        InputBytes,
                        _case.Replacement,
                        _case.Coverage.ReplacementCount);
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
            case PythonReBenchmarkOperation.ReplaceEvaluatorString:
            {
                var result = string.Empty;
                var callbackChecksum = 0;
                var callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    _callbackChecksum = 0;
                    _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
                    result = _pythonRegex.ReplaceToString(
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
            case PythonReBenchmarkOperation.SplitStringsLimited:
            {
                string?[] result = [];
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.SplitToStrings(InputBytes, _case.Coverage.MaxSplit);
                }

                return Complete(
                    Stopwatch.GetTimestamp(),
                    GC.GetAllocatedBytesForCurrentThread(),
                    Checksum(result),
                    SemanticDigest(_case.Operation, result));
            }
            case PythonReBenchmarkOperation.SplitDetailed:
            {
                Utf8PythonSplitItem[] result = [];
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.SplitDetailed(InputBytes);
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
            not PythonReBenchmarkOperation.SearchFromOffset and
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
            case PythonReBenchmarkOperation.SearchFromOffset:
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Search(InputBytes, _case.Coverage.StartOffsetInBytes);
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
        PythonReBenchmarkOperation.SearchFromOffset => GetConsumptionToken(
            _pythonRegex.Search(InputBytes, _case.Coverage.StartOffsetInBytes)),
        PythonReBenchmarkOperation.Match => GetConsumptionToken(_pythonRegex.Match(InputBytes)),
        PythonReBenchmarkOperation.FullMatch => GetConsumptionToken(_pythonRegex.FullMatch(InputBytes)),
        _ => 0,
    };

    internal int ExecutePythonRe() => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _pythonRegex.IsMatch(InputBytes) ? 1 : 0,
        PythonReBenchmarkOperation.Search => Checksum(_pythonRegex.Search(InputBytes)),
        PythonReBenchmarkOperation.SearchFromOffset => Checksum(
            _pythonRegex.Search(InputBytes, _case.Coverage.StartOffsetInBytes)),
        PythonReBenchmarkOperation.Match => Checksum(_pythonRegex.Match(InputBytes)),
        PythonReBenchmarkOperation.FullMatch => Checksum(_pythonRegex.FullMatch(InputBytes)),
        PythonReBenchmarkOperation.SearchDetailed => Checksum(_pythonRegex.SearchDetailedData(InputBytes)),
        PythonReBenchmarkOperation.Count => _pythonRegex.Count(InputBytes),
        PythonReBenchmarkOperation.CountFromOffset => _pythonRegex.Count(
            InputBytes,
            _case.Coverage.StartOffsetInBytes),
        PythonReBenchmarkOperation.FindAllStrings => Checksum(_pythonRegex.FindAllToStrings(InputBytes)),
        PythonReBenchmarkOperation.FindAllStringsFromOffset => Checksum(_pythonRegex.FindAllToStrings(
            InputBytes,
            _case.Coverage.StartOffsetInBytes)),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(_pythonRegex.FindAllToUtf8(InputBytes)),
        PythonReBenchmarkOperation.FindIterDetailed => Checksum(_pythonRegex.FindIterDetailed(InputBytes)),
        PythonReBenchmarkOperation.FindAllStructural => Checksum(_pythonRegex.FindAll(InputBytes)),
        PythonReBenchmarkOperation.ReplaceString => Checksum(_pythonRegex.ReplaceToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.ReplaceUtf8 => Checksum(_pythonRegex.Replace(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.ReplaceStringLimited => Checksum(_pythonRegex.ReplaceToString(
            InputBytes,
            _case.Replacement,
            _case.Coverage.ReplacementCount)),
        PythonReBenchmarkOperation.SubnString => Checksum(_pythonRegex.SubnToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnUtf8 => Checksum(_pythonRegex.Subn(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnEvaluatorString => ExecutePythonReEvaluatorString(),
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => ExecutePythonReEvaluatorUtf8(),
        PythonReBenchmarkOperation.ReplaceEvaluatorString => ExecutePythonReReplaceEvaluatorString(),
        PythonReBenchmarkOperation.SplitStrings => Checksum(_pythonRegex.SplitToStrings(InputBytes)),
        PythonReBenchmarkOperation.SplitStringsLimited => Checksum(_pythonRegex.SplitToStrings(
            InputBytes,
            _case.Coverage.MaxSplit)),
        PythonReBenchmarkOperation.SplitDetailed => Checksum(_pythonRegex.SplitDetailed(InputBytes)),
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
        PythonReBenchmarkOperation.SearchFromOffset => SemanticDigest(
            _case.Operation,
            _pythonRegex.Search(InputBytes, _case.Coverage.StartOffsetInBytes)),
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
        PythonReBenchmarkOperation.CountFromOffset => SemanticDigest(
            _case.Operation,
            _pythonRegex.Count(InputBytes, _case.Coverage.StartOffsetInBytes)),
        PythonReBenchmarkOperation.FindAllStrings => SemanticDigest(
            _case.Operation,
            _pythonRegex.FindAllToStrings(InputBytes)),
        PythonReBenchmarkOperation.FindAllStringsFromOffset => SemanticDigest(
            _case.Operation,
            _pythonRegex.FindAllToStrings(InputBytes, _case.Coverage.StartOffsetInBytes)),
        PythonReBenchmarkOperation.FindAllUtf8 => SemanticDigest(
            _case.Operation,
            _pythonRegex.FindAllToUtf8(InputBytes)),
        PythonReBenchmarkOperation.FindIterDetailed => SemanticDigest(
            _case.Operation,
            _pythonRegex.FindIterDetailed(InputBytes)),
        PythonReBenchmarkOperation.FindAllStructural => SemanticDigest(
            _case.Operation,
            _pythonRegex.FindAll(InputBytes)),
        PythonReBenchmarkOperation.ReplaceString => SemanticDigest(
            _case.Operation,
            _pythonRegex.ReplaceToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.ReplaceUtf8 => SemanticDigest(
            _case.Operation,
            _pythonRegex.Replace(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.ReplaceStringLimited => SemanticDigest(
            _case.Operation,
            _pythonRegex.ReplaceToString(InputBytes, _case.Replacement, _case.Coverage.ReplacementCount)),
        PythonReBenchmarkOperation.SubnString => SemanticDigest(
            _case.Operation,
            _pythonRegex.SubnToString(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnUtf8 => SemanticDigest(
            _case.Operation,
            _pythonRegex.Subn(InputBytes, _case.Replacement)),
        PythonReBenchmarkOperation.SubnEvaluatorString => ExecutePythonReEvaluatorStringSemanticDigest(),
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => ExecutePythonReEvaluatorUtf8SemanticDigest(),
        PythonReBenchmarkOperation.ReplaceEvaluatorString => ExecutePythonReReplaceEvaluatorStringSemanticDigest(),
        PythonReBenchmarkOperation.SplitStrings => SemanticDigest(
            _case.Operation,
            _pythonRegex.SplitToStrings(InputBytes)),
        PythonReBenchmarkOperation.SplitStringsLimited => SemanticDigest(
            _case.Operation,
            _pythonRegex.SplitToStrings(InputBytes, _case.Coverage.MaxSplit)),
        PythonReBenchmarkOperation.SplitDetailed => SemanticDigest(
            _case.Operation,
            _pythonRegex.SplitDetailed(InputBytes)),
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

    internal Utf8PythonFindAllResult ProjectTrailingCaptureStrings()
    {
        if (!SupportsTrailingCaptureReplay)
        {
            throw new InvalidOperationException("The trailing-capture replay is not available for this case.");
        }

        List<string> values = new(_preparedCaptureRanges.Length);
        foreach (var match in _regex.EnumerateMatches(_decoded, 0))
        {
            var captureStart = match.Index + _trailingCapturePrefixLength;
            values.Add(_decoded.Substring(captureStart, match.Length - _trailingCapturePrefixLength));
        }

        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.SingleGroup,
            ScalarValues = values.ToArray(),
            TupleValues = [],
        };
    }

    internal Utf8PythonFindAllResult ProjectSeparatedCaptureStrings()
    {
        if (!SupportsSeparatedCapturesReplay)
        {
            throw new InvalidOperationException("The separated-captures replay is not available for this case.");
        }

        List<string[]> values = new(_preparedCaptureRanges.Length / _captureCount);
        foreach (var match in _regex.EnumerateMatches(_decoded, 0))
        {
            var matched = _decoded.AsSpan(match.Index, match.Length);
            var separatorIndex = matched.IndexOf(_captureSeparator);
            if (separatorIndex < 0)
            {
                throw new InvalidOperationException("The prepared capture separator was not found in a replay match.");
            }

            values.Add([
                new string(matched[..separatorIndex]),
                new string(matched[(separatorIndex + 1)..]),
            ]);
        }

        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.GroupTuple,
            ScalarValues = [],
            TupleValues = values.ToArray(),
        };
    }

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

    internal int ExecuteTrailingCaptureReplayChecksum() => Checksum(ProjectTrailingCaptureStrings());

    internal int ExecuteSeparatedCapturesReplayChecksum() => Checksum(ProjectSeparatedCaptureStrings());

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

    internal Utf8ValidationResult ExecuteOneShotValidation()
    {
        EnsureOneShotPhases();
        return Utf8InputAnalyzer.ValidateOnly(InputBytes);
    }

    internal PythonReBenchmarkValueResult ExecuteCoreRawOneShot()
    {
        if (!SupportsExactLiteralOneShotReplay)
        {
            throw new InvalidOperationException("The exact-literal one-shot replay is unavailable.");
        }

        var regex = GetOneShotCoreRegex();
        if (!regex.Inspection.DebugTryMatchExactLiteral(InputBytes, out var match))
        {
            throw new InvalidOperationException("The exact-literal raw control is unavailable.");
        }

        return ProjectCoreOneShot(match);
    }

    internal PythonReBenchmarkValueResult ExecuteRawAsciiPrefixDigitOneShot()
    {
        if (!SupportsAsciiPrefixDigitOneShotReplay)
        {
            throw new InvalidOperationException("The ASCII prefix-digit one-shot replay is unavailable.");
        }

        var end = PythonReAsciiPrefixDigitMatcher.MatchUnchecked(InputBytes, _oneShotAsciiPrefix);
        return end == 0
            ? default
            : new PythonReBenchmarkValueResult(true, 0, end, 0, end);
    }

    internal PythonReBenchmarkValueResult ExecuteValidatedAsciiPrefixDigitOneShot()
    {
        if (!SupportsAsciiPrefixDigitOneShotReplay)
        {
            throw new InvalidOperationException("The ASCII prefix-digit one-shot replay is unavailable.");
        }

        var end = PythonReAsciiPrefixDigitMatcher.MatchValidated(InputBytes, _oneShotAsciiPrefix);
        return end == 0
            ? default
            : new PythonReBenchmarkValueResult(true, 0, end, 0, end);
    }

    internal PythonReBenchmarkValueResult ExecuteCoreDirectOneShot()
    {
        EnsureOneShotPhases();
        return ProjectCoreOneShot(GetOneShotCoreRegex().Match(InputBytes));
    }

    internal PythonReBenchmarkValueResult ExecuteCoreOffsetZeroOneShot()
    {
        EnsureOneShotPhases();
        return ProjectCoreOneShot(GetOneShotCoreRegex().MatchFromUtf16Offset(InputBytes, 0));
    }

    internal PythonReBenchmarkValueResult ExecutePythonReOneShot()
    {
        EnsureOneShotPhases();
        var match = _case.Operation == PythonReBenchmarkOperation.Search
            ? _pythonRegex.Search(InputBytes)
            : _pythonRegex.Match(InputBytes);
        return match.Success
            ? new PythonReBenchmarkValueResult(
                true,
                match.StartOffsetInBytes,
                match.EndOffsetInBytes,
                match.StartOffsetInUtf16,
                match.EndOffsetInUtf16)
            : default;
    }

    internal PythonReBenchmarkValueResult ExecutePredecodedOneShot()
    {
        EnsureOneShotPhases();
        var match = _regex.Match(_decoded);
        if (!match.Success ||
            _case.Operation == PythonReBenchmarkOperation.Match && match.Index != 0)
        {
            return default;
        }

        return new PythonReBenchmarkValueResult(
            true,
            match.Index,
            match.Index + match.Length,
            match.Index,
            match.Index + match.Length);
    }

    private PythonReBenchmarkValueResult ProjectCoreOneShot(Utf8ValueMatch match)
    {
        if (!match.Success ||
            _case.Operation == PythonReBenchmarkOperation.Match && match.IndexInUtf16 != 0)
        {
            return default;
        }

        if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
        {
            throw new InvalidOperationException("The exact-literal one-shot control has no byte range.");
        }

        return new PythonReBenchmarkValueResult(
            true,
            indexInBytes,
            indexInBytes + lengthInBytes,
            match.IndexInUtf16,
            match.IndexInUtf16 + match.LengthInUtf16);
    }

    private Utf8Regex GetOneShotCoreRegex() => _oneShotCoreRegex ??
        throw new InvalidOperationException("One-shot phase controls are not available for this case.");

    private void EnsureOneShotPhases()
    {
        if (!SupportsOneShotPhases)
        {
            throw new InvalidOperationException("One-shot phase controls are not available for this case.");
        }
    }

    internal PythonReReplacementPlan ParseReplacement()
    {
        EnsureReplacementPhases();
        return PythonReReplacementParser.Parse(
            _case.Replacement,
            _captureCount,
            new Dictionary<string, int>(StringComparer.Ordinal));
    }

    internal string TranslateReplacement()
    {
        EnsureReplacementPhases();
        return _preparedReplacementPlan.ToDotNetReplacementString();
    }

    internal int ExecutePredecodedReplacementCount()
    {
        EnsureReplacementPhases();
        return _regex.Count(_decoded);
    }

    internal string ExecutePredecodedReplacementText()
    {
        EnsureReplacementPhases();
        return _regex.Replace(_decoded, _preparedDotNetReplacement);
    }

    internal byte[] EncodePreparedReplacementUtf8()
    {
        EnsureReplacementPhases();
        return s_strictUtf8.GetBytes(GetPreparedReplacementResult().ResultText);
    }

    internal PythonReBenchmarkReplacementResult ExecutePredecodedRequiredReplacement()
    {
        EnsureReplacementPhases();
        return ExecuteRequiredReplacement(_decoded);
    }

    internal PythonReBenchmarkReplacementResult ExecuteDecodedRequiredReplacement()
    {
        EnsureReplacementPhases();
        return ExecuteRequiredReplacement(s_strictUtf8.GetString(InputBytes));
    }

    internal PythonReBenchmarkReplacementResult ExecuteUtf8CoreRequiredReplacement()
    {
        EnsureReplacementPhases();
        var regex = GetCoreReplacementRegex();
        var replacementCount = IsSubnOperation
            ? regex.Count(InputBytes)
            : (int?)null;
        if (IsUtf8ReplacementOperation)
        {
            return new PythonReBenchmarkReplacementResult(
                string.Empty,
                regex.Replace(InputBytes, _case.Replacement),
                replacementCount);
        }

        return new PythonReBenchmarkReplacementResult(
            regex.ReplaceToString(InputBytes, _case.Replacement),
            null,
            replacementCount);
    }

    internal PythonReBenchmarkReplacementResult ExecuteUtf8CoreCountedLiteralReplacement()
    {
        EnsureReplacementPhases();
        var bytes = GetCoreReplacementRegex().ReplaceLiteralWithCount(
            InputBytes,
            _preparedLiteralReplacementUtf8,
            out var replacementCount);
        var returnedCount = IsSubnOperation ? replacementCount : (int?)null;
        return IsUtf8ReplacementOperation
            ? new PythonReBenchmarkReplacementResult(string.Empty, bytes, returnedCount)
            : new PythonReBenchmarkReplacementResult(s_strictUtf8.GetString(bytes), null, returnedCount);
    }

    internal int ExecuteUtf8CoreReplacementCount()
    {
        EnsureReplacementPhases();
        return GetCoreReplacementRegex().Count(InputBytes);
    }

    internal PythonReBenchmarkReplacementResult ExecuteUtf8CoreReplacementOutput()
    {
        EnsureReplacementPhases();
        var regex = GetCoreReplacementRegex();
        return IsUtf8ReplacementOperation
            ? new PythonReBenchmarkReplacementResult(
                string.Empty,
                regex.Replace(InputBytes, _case.Replacement),
                null)
            : new PythonReBenchmarkReplacementResult(
                regex.ReplaceToString(InputBytes, _case.Replacement),
                null,
                null);
    }

    internal PythonReBenchmarkReplacementResult ExecutePredecodedOnePassLiteralReplacement()
    {
        EnsureReplacementPhases();
        return ExecuteOnePassLiteralReplacement(_decoded);
    }

    internal PythonReBenchmarkReplacementResult ExecuteDecodedOnePassLiteralReplacement()
    {
        EnsureReplacementPhases();
        return ExecuteOnePassLiteralReplacement(s_strictUtf8.GetString(InputBytes));
    }

    internal PythonReBenchmarkReplacementResult ExecutePythonReReplacement()
    {
        EnsureReplacementPhases();
        return _case.Operation switch
        {
            PythonReBenchmarkOperation.ReplaceString => new(
                _pythonRegex.ReplaceToString(InputBytes, _case.Replacement),
                null,
                null),
            PythonReBenchmarkOperation.ReplaceUtf8 => new(
                string.Empty,
                _pythonRegex.Replace(InputBytes, _case.Replacement),
                null),
            PythonReBenchmarkOperation.SubnString => CreateStringReplacementResult(
                _pythonRegex.SubnToString(InputBytes, _case.Replacement)),
            PythonReBenchmarkOperation.SubnUtf8 => CreateUtf8ReplacementResult(
                _pythonRegex.Subn(InputBytes, _case.Replacement)),
            _ => throw new InvalidOperationException(),
        };
    }

    internal int ExecutePythonReReplacementChecksum() => Checksum(ExecutePythonReReplacement());

    internal int ExecutePredecodedRequiredReplacementChecksum() =>
        Checksum(ExecutePredecodedRequiredReplacement());

    internal int ExecuteDecodedRequiredReplacementChecksum() =>
        Checksum(ExecuteDecodedRequiredReplacement());

    internal int ExecuteUtf8CoreRequiredReplacementChecksum() =>
        Checksum(ExecuteUtf8CoreRequiredReplacement());

    internal int ExecuteUtf8CoreCountedLiteralReplacementChecksum() =>
        Checksum(ExecuteUtf8CoreCountedLiteralReplacement());

    internal int ExecuteUtf8CoreReplacementOutputChecksum() =>
        Checksum(ExecuteUtf8CoreReplacementOutput());

    internal int ExecuteDecodedOnePassLiteralReplacementChecksum() =>
        Checksum(ExecuteDecodedOnePassLiteralReplacement());

    internal int ExecutePreparedReplacementChecksum() => Checksum(GetPreparedReplacementResult());

    internal int ExecutePreparedReplacementOutputChecksum()
    {
        var result = GetPreparedReplacementResult();
        return Checksum(result with { ReplacementCount = null });
    }

    private PythonReBenchmarkReplacementResult ExecuteRequiredReplacement(string subject)
    {
        var replacementCount = IsSubnOperation
            ? _regex.Count(subject)
            : (int?)null;
        var resultText = _regex.Replace(subject, _preparedDotNetReplacement);
        return CreateReplacementResult(resultText, replacementCount);
    }

    private PythonReBenchmarkReplacementResult ExecuteOnePassLiteralReplacement(string subject)
    {
        var builder = new StringBuilder(subject.Length);
        var replacementCount = 0;
        var lastIndex = 0;
        var searchIndex = 0;
        while (searchIndex <= subject.Length)
        {
            var match = _regex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            builder.Append(subject.AsSpan(lastIndex, match.Index - lastIndex));
            builder.Append(_preparedLiteralReplacement);
            replacementCount++;
            lastIndex = checked(match.Index + match.Length);
            searchIndex = lastIndex;
        }

        builder.Append(subject.AsSpan(lastIndex));
        return CreateReplacementResult(
            builder.ToString(),
            IsSubnOperation ? replacementCount : null);
    }

    private PythonReBenchmarkReplacementResult CreateReplacementResult(
        string resultText,
        int? replacementCount) => IsUtf8ReplacementOperation
        ? new PythonReBenchmarkReplacementResult(
            resultText,
            s_strictUtf8.GetBytes(resultText),
            replacementCount)
        : new PythonReBenchmarkReplacementResult(resultText, null, replacementCount);

    private static PythonReBenchmarkReplacementResult CreateStringReplacementResult(
        Utf8PythonSubnResult result) => new(
            result.ResultText,
            null,
            result.ReplacementCount);

    private static PythonReBenchmarkReplacementResult CreateUtf8ReplacementResult(
        Utf8PythonSubnUtf8Result result) => new(
            string.Empty,
            result.ResultBytes,
            result.ReplacementCount);

    private bool IsSubnOperation => _case.Operation is
        PythonReBenchmarkOperation.SubnString or PythonReBenchmarkOperation.SubnUtf8;

    private bool IsUtf8ReplacementOperation => _case.Operation is
        PythonReBenchmarkOperation.ReplaceUtf8 or PythonReBenchmarkOperation.SubnUtf8;

    private PythonReBenchmarkReplacementResult GetPreparedReplacementResult() =>
        _preparedReplacementResult ??
        throw new InvalidOperationException("Prepared replacement controls are not available for this case.");

    private Utf8Regex GetCoreReplacementRegex() => _coreReplacementRegex ??
        throw new InvalidOperationException("UTF-8 core replacement controls are not available for this case.");

    private void EnsureReplacementPhases()
    {
        if (!SupportsReplacementPhases)
        {
            throw new InvalidOperationException("Replacement phase controls are not available for this case.");
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

    internal int ExecuteRepeatedCoreStringProjection() => Checksum(ProjectRepeatedCoreStrings());

    internal int ExecuteCountedRepeatedCoreStringProjection() =>
        Checksum(CountAndProjectRepeatedCoreStrings());

    internal Utf8PythonFindAllResult ProjectRepeatedCoreStrings()
    {
        if (!SupportsRepeatedCoreStringReplay)
        {
            throw new InvalidOperationException("Repeated-value FindAll replay is not available for this case.");
        }

        var values = new string[_preparedCoreRanges.Length];
        Array.Fill(values, _repeatedCoreString);
        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.FullMatch,
            ScalarValues = values,
            TupleValues = [],
        };
    }

    internal Utf8PythonFindAllResult CountAndProjectRepeatedCoreStrings()
    {
        if (!SupportsRepeatedCoreStringReplay)
        {
            throw new InvalidOperationException("Counted repeated-value FindAll replay is not available for this case.");
        }

        var values = new string[GetCoreFindAllRegex().Count(InputBytes)];
        Array.Fill(values, _repeatedCoreString);
        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.FullMatch,
            ScalarValues = values,
            TupleValues = [],
        };
    }

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

    private int ExecutePythonReReplaceEvaluatorString()
    {
        _callbackChecksum = 0;
        var result = _pythonRegex.ReplaceToString(
            InputBytes,
            this,
            static (context, match) =>
            {
                context._callbackChecksum = Combine(context._callbackChecksum, Checksum(match));
                return context._case.Replacement;
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

    private ulong ExecutePythonReReplaceEvaluatorStringSemanticDigest()
    {
        _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
        var result = _pythonRegex.ReplaceToString(
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

    private int ExecuteRegex(string input) => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _regex.IsMatch(input) ? 1 : 0,
        PythonReBenchmarkOperation.Search => Checksum(_regex.Match(input)),
        PythonReBenchmarkOperation.SearchFromOffset => Checksum(_regex.Match(input, _startOffsetInUtf16)),
        PythonReBenchmarkOperation.Match => ChecksumAtStart(_regex.Match(input)),
        PythonReBenchmarkOperation.FullMatch => Checksum(_fullRegex.Match(input)),
        PythonReBenchmarkOperation.SearchDetailed => Checksum(MaterializeDetailed(_regex.Match(input), input, GetUtf8Offsets(input))),
        PythonReBenchmarkOperation.Count => _regex.Count(input),
        PythonReBenchmarkOperation.CountFromOffset => _regex.Count(input, _startOffsetInUtf16),
        PythonReBenchmarkOperation.FindAllStrings => Checksum(MaterializeFindAllStrings(input)),
        PythonReBenchmarkOperation.FindAllStringsFromOffset => Checksum(MaterializeFindAllStrings(
            input,
            _startOffsetInUtf16)),
        PythonReBenchmarkOperation.FindAllUtf8 => Checksum(MaterializeFindAllUtf8(input)),
        PythonReBenchmarkOperation.FindIterDetailed => Checksum(MaterializeFindIterDetailed(input)),
        PythonReBenchmarkOperation.FindAllStructural => Checksum(MaterializeFindAllStructural(input)),
        PythonReBenchmarkOperation.ReplaceString => Checksum(_regex.Replace(input, _dotNetReplacement)),
        PythonReBenchmarkOperation.ReplaceUtf8 => Checksum(Encoding.UTF8.GetBytes(_regex.Replace(input, _dotNetReplacement))),
        PythonReBenchmarkOperation.ReplaceStringLimited => Checksum(_regex.Replace(
            input,
            _dotNetReplacement,
            _case.Coverage.ReplacementCount)),
        PythonReBenchmarkOperation.SubnString => Checksum(ReplaceAndCount(input, encodeUtf8: false, materializeCallback: false)),
        PythonReBenchmarkOperation.SubnUtf8 => Checksum(ReplaceAndCount(input, encodeUtf8: true, materializeCallback: false)),
        PythonReBenchmarkOperation.SubnEvaluatorString => Checksum(ReplaceAndCount(input, encodeUtf8: false, materializeCallback: true)),
        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => Checksum(ReplaceAndCount(input, encodeUtf8: true, materializeCallback: true)),
        PythonReBenchmarkOperation.ReplaceEvaluatorString => ExecuteRegexReplaceEvaluator(input),
        PythonReBenchmarkOperation.SplitStrings => Checksum(MaterializeSplitStrings(input)),
        PythonReBenchmarkOperation.SplitStringsLimited => Checksum(MaterializeSplitStrings(
            input,
            _case.Coverage.MaxSplit)),
        PythonReBenchmarkOperation.SplitDetailed => Checksum(MaterializeSplitDetailed(input)),
        _ => throw new InvalidOperationException(),
    };

    private BclFindAllResult MaterializeFindAllStrings(string input, int startOffsetInUtf16 = 0)
    {
        if (_captureCount <= 1)
        {
            var values = new List<string>();
            foreach (Match match in _regex.Matches(input, startOffsetInUtf16))
            {
                values.Add(_captureCount == 0 ? match.Value : match.Groups[1].Value);
            }

            return new BclFindAllResult(_captureCount == 0 ? Utf8PythonFindAllShape.FullMatch : Utf8PythonFindAllShape.SingleGroup, values.ToArray(), []);
        }

        var tuples = new List<string[]>();
        foreach (Match match in _regex.Matches(input, startOffsetInUtf16))
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

    private Utf8PythonMatchData[] MaterializeFindAllStructural(string input)
    {
        var utf8Offsets = GetUtf8Offsets(input);
        var matches = new List<Utf8PythonMatchData>();
        foreach (Match match in _regex.Matches(input))
        {
            matches.Add(new Utf8PythonMatchData
            {
                Success = true,
                StartOffsetInBytes = utf8Offsets is null ? match.Index : utf8Offsets[match.Index],
                EndOffsetInBytes = utf8Offsets is null ? match.Index + match.Length : utf8Offsets[match.Index + match.Length],
                StartOffsetInUtf16 = match.Index,
                EndOffsetInUtf16 = match.Index + match.Length,
                ValueText = match.Value,
            });
        }

        return matches.ToArray();
    }

    private string?[] MaterializeSplitStrings(string input, int maxSplit = 0) =>
        MaterializeSplitDetailed(input, maxSplit)
            .Select(static item => item.ValueText)
            .ToArray();

    private Utf8PythonSplitItem[] MaterializeSplitDetailed(string input, int maxSplit = 0)
    {
        var parts = new List<Utf8PythonSplitItem>();
        var lastIndex = 0;
        var splitCount = 0;
        foreach (Match match in _regex.Matches(input))
        {
            if (maxSplit > 0 && splitCount >= maxSplit)
            {
                break;
            }

            parts.Add(new Utf8PythonSplitItem
            {
                ValueText = input[lastIndex..match.Index],
                IsCapture = false,
                CaptureGroupNumber = 0,
            });
            for (var groupNumber = 1; groupNumber < match.Groups.Count; groupNumber++)
            {
                var group = match.Groups[groupNumber];
                parts.Add(new Utf8PythonSplitItem
                {
                    ValueText = group.Success ? group.Value : null,
                    IsCapture = true,
                    CaptureGroupNumber = groupNumber,
                });
            }

            lastIndex = match.Index + match.Length;
            splitCount++;
        }

        parts.Add(new Utf8PythonSplitItem
        {
            ValueText = input[lastIndex..],
            IsCapture = false,
            CaptureGroupNumber = 0,
        });
        return parts.ToArray();
    }

    private BclSubnResult ReplaceAndCount(
        string input,
        bool encodeUtf8,
        bool materializeCallback)
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

            return materializeCallback ? _case.Replacement : match.Result(_dotNetReplacement);
        });
        return new BclSubnResult(
            result,
            encodeUtf8 ? Encoding.UTF8.GetBytes(result) : null,
            count,
            materializeCallback ? callbackChecksum : null);
    }

    private int ExecuteRegexReplaceEvaluator(string input)
    {
        var callbackChecksum = 0;
        var utf8Offsets = GetUtf8Offsets(input);
        var result = _regex.Replace(input, match =>
        {
            callbackChecksum = Combine(
                callbackChecksum,
                Checksum(MaterializeDetailed(match, input, utf8Offsets)));
            return _case.Replacement;
        });
        return Combine(Checksum(result), callbackChecksum);
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
        Utf8PythonMatchData[] matches)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.Add(matches.Length);
        foreach (var match in matches)
        {
            digest.Add(match.Success);
            digest.Add(match.StartOffsetInBytes);
            digest.Add(match.EndOffsetInBytes);
            digest.Add(match.StartOffsetInUtf16);
            digest.Add(match.EndOffsetInUtf16);
            digest.AddString(match.ValueText);
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

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        string value,
        ulong callbackSemanticDigest)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.AddString(value);
        digest.Add(callbackSemanticDigest);
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

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        Utf8PythonSplitItem[] items)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.Add(items.Length);
        foreach (var item in items)
        {
            digest.Add(item.ValueText is not null);
            if (item.ValueText is not null)
            {
                digest.AddString(item.ValueText);
            }

            digest.Add(item.IsCapture);
            digest.Add(item.CaptureGroupNumber);
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

    private static int Checksum(Utf8PythonMatchData[] matches)
    {
        var checksum = matches.Length;
        foreach (var match in matches)
        {
            checksum = Combine(
                checksum,
                match.Success ? 1 : 0,
                match.StartOffsetInUtf16,
                match.EndOffsetInUtf16,
                Checksum(match.ValueText));
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

    private int Checksum(PythonReBenchmarkReplacementResult result)
    {
        var checksum = IsUtf8ReplacementOperation
            ? Checksum(result.ResultBytes ?? [])
            : Checksum(result.ResultText);
        return result.ReplacementCount is int replacementCount
            ? Combine(checksum, replacementCount)
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

    private static int Checksum(Utf8PythonSplitItem[] items)
    {
        var checksum = items.Length;
        foreach (var item in items)
        {
            checksum = Combine(
                checksum,
                item.ValueText is null ? -1 : Checksum(item.ValueText),
                item.IsCapture ? 1 : 0,
                item.CaptureGroupNumber,
                0);
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

internal readonly record struct PythonReBenchmarkReplacementResult(
    string ResultText,
    byte[]? ResultBytes,
    int? ReplacementCount);

internal readonly record struct PythonReBenchmarkValueResult(
    bool Success,
    int StartOffsetInBytes,
    int EndOffsetInBytes,
    int StartOffsetInUtf16,
    int EndOffsetInUtf16);

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
    public string CatalogSha256 { get; init; } = string.Empty;
    public string[] CatalogCaseIds { get; init; } = [];
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
    public string InputSha256 { get; set; } = string.Empty;
    public required int EffectiveIterations { get; init; }
    public required int Samples { get; init; }
    public required bool IncludesResultMaterialization { get; init; }
    public PythonReBenchmarkCoverage? Coverage { get; set; }
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
    public required string QualificationId { get; set; }
    public required DateTimeOffset MeasuredAtUtc { get; init; }
    public required string SourceCommit { get; init; }
    public required string Baseline { get; init; }
    public string ResultContract { get; init; } = string.Empty;
    public required string InitialLane { get; init; }
    public required bool WorktreeQualified { get; init; }
    public required string CaseDefinitionSha256 { get; set; }
    public required string CatalogSha256 { get; init; }
    public string ManagedProductSha256 { get; set; } = string.Empty;
    public string ManagedOperationProtocolSha256 { get; set; } = string.Empty;
    public string CpythonOperationProtocolSha256 { get; set; } = string.Empty;
    public string SharedProtocolSha256 { get; set; } = string.Empty;
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
    public required int StartOffsetInBytes { get; init; }
    public required int ReplacementCount { get; init; }
    public required int MaxSplit { get; init; }
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
