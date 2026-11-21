using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const string SnapshotFileName = "PythonRe.Benchmarks.json";
    private const string CpythonRunnerRelativePath = "bench/Lokad.Utf8Regex.Benchmarks/pythonre_cpython_benchmark.py";
    private const int PythonReBenchmarkSchemaVersion = 10;
    private const int CpythonProtocolVersion = 1;
    private static int s_sink;
    private static object? s_retainedSink;

    internal static bool TryHandleCommand(string[] args, out int exitCode)
    {
        if (args.Length > 3 && args[0].StartsWith("--measure-pythonre-paired-case", StringComparison.Ordinal))
            throw new ArgumentException("PythonRe comparisons consume existing text; no validation-policy argument is accepted.");
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

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-replacement-phases", StringComparison.Ordinal))
        {
            exitCode = MeasureReplacementPhases(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 7));
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-split-phases", StringComparison.Ordinal))
        {
            exitCode = MeasureSplitPhases(
                args[1],
                ParsePositive(args, 2, 200),
                ParsePositive(args, 3, 7));
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pythonre-search-attribution", StringComparison.Ordinal))
        {
            exitCode = RunMeasurePythonReSearchAttribution(
                args[1],
                ParsePositive(args, 2, 20),
                Math.Clamp(ParsePositive(args, 3, 5), 1, 9));
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

        if (args.Length >= 1 && args[0].Equals("--measure-pythonre-empty-progression-scaling", StringComparison.Ordinal))
        {
            exitCode = MeasureEmptyProgressionScaling(
                Math.Min(ParsePositive(args, 1, 64), 1_024),
                Math.Min(ParsePositive(args, 2, 50), 500),
                Math.Min(ParsePositive(args, 3, 7), 15));
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

        if (args.Length >= 1 && args[0].Equals("--refresh-pythonre-lifecycle", StringComparison.Ordinal))
        {
            exitCode = RefreshPythonReLifecycle(
                Math.Min(ParsePositive(args, 1, 32), 128),
                Math.Min(ParsePositive(args, 2, 5), 9));
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--refresh-pythonre-scaling", StringComparison.Ordinal))
        {
            exitCode = RefreshPythonReScaling(
                Math.Clamp(ParsePositive(args, 1, PythonRePublishedScalingSamples), 5, 9),
                args.Length >= 3 ? args[2] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--verify-pythonre-scaling", StringComparison.Ordinal))
        {
            exitCode = VerifyPythonReScaling();
            return true;
        }

        exitCode = 0;
        return false;
    }

    private static void WriteSnapshot(PythonReBenchmarkSnapshot snapshot, bool publishPresentation = true)
    {
        var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        var snapshotPath = GetPythonReSnapshotPath();
        Directory.CreateDirectory(Path.GetDirectoryName(snapshotPath)!);
        BenchmarkFileWriter.WriteTextAtomically(snapshotPath, json + Environment.NewLine);
        if (!publishPresentation) return;
        WriteBenchmarkSummary(snapshot);
        WritePythonReBenchmarkMarkdown();
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
            not PythonReBenchmarkOperation.ReplaceStringLimited and
            not PythonReBenchmarkOperation.SubnString and
            not PythonReBenchmarkOperation.SubnUtf8)
        {
            Console.Error.WriteLine(
                $"PythonRe replacement phase diagnostics require a fixed or bounded Replace/Subn operation; " +
                $"'{id}' uses {benchmarkCase.Operation}.");
            return 1;
        }

        var effectiveIterations = GetEffectiveIterations(benchmarkCase, iterations);
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        var context = new PythonReBenchmarkContext(benchmarkCase);
        if (!context.SupportsReplacementPhases)
        {
            Console.Error.WriteLine(
                $"PythonRe replacement phase diagnostics currently require a capture-free pattern " +
                $"and a literal replacement; '{id}' is unsupported.");
            return 1;
        }

        var expected = context.ExecutePythonReReplacementChecksum();
        var predecoded = context.ExecutePredecodedRequiredReplacementChecksum();
        var decoded = context.ExecuteDecodedRequiredReplacementChecksum();
        var isLimited = benchmarkCase.Operation == PythonReBenchmarkOperation.ReplaceStringLimited;
        var utf8Core = isLimited
            ? context.ExecuteBoundedExactLiteralReplacementChecksum()
            : context.ExecuteUtf8CoreRequiredReplacementChecksum();
        var countedUtf8Core = isLimited
            ? expected
            : context.ExecuteUtf8CoreCountedLiteralReplacementChecksum();
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

        if (!isLimited &&
            (context.ExecuteUtf8CoreReplacementOutputChecksum() !=
                 context.ExecutePreparedReplacementOutputChecksum() ||
             context.ExecuteUtf8CoreReplacementCount() != context.PreparedReplacementMatchCount))
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
        if (isLimited)
        {
            PrintOperation("BoundedUtf8Exact", MeasureRetainedPhaseOperation(
                context.ExecuteBoundedExactLiteralReplacement,
                effectiveIterations,
                samples));
        }
        else
        {
            PrintOperation("Utf8CoreReplace", MeasureRetainedPhaseOperation(context.ExecuteUtf8CoreReplacementOutput, effectiveIterations, samples));
            PrintOperation("Utf8CoreRequired", MeasureRetainedPhaseOperation(context.ExecuteUtf8CoreRequiredReplacement, effectiveIterations, samples));
            PrintOperation("Utf8CoreCounted", MeasureRetainedPhaseOperation(context.ExecuteUtf8CoreCountedLiteralReplacement, effectiveIterations, samples));
        }
        PrintOperation("PredecodedOnePass", MeasureRetainedPhaseOperation(context.ExecutePredecodedOnePassLiteralReplacement, effectiveIterations, samples));
        PrintOperation("DecodeOnePass", MeasureRetainedPhaseOperation(context.ExecuteDecodedOnePassLiteralReplacement, effectiveIterations, samples));
        PrintOperation("PythonRePublic", MeasureRetainedPhaseOperation(context.ExecutePythonReReplacement, effectiveIterations, samples));
        PrintOperation("ChecksumTraversal", MeasurePhaseOperation(context.ExecutePreparedReplacementChecksum, effectiveIterations, samples));
        return 0;
    }

    private static int MeasureSplitPhases(string id, int iterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(id, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{id}'.");
            return 1;
        }

        if (benchmarkCase.Operation is not PythonReBenchmarkOperation.SplitStrings and
            not PythonReBenchmarkOperation.SplitStringsLimited)
        {
            Console.Error.WriteLine(
                $"PythonRe split phase diagnostics require a string Split operation; " +
                $"'{id}' uses {benchmarkCase.Operation}.");
            return 1;
        }

        var effectiveIterations = GetEffectiveIterations(benchmarkCase, iterations);
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        var context = new PythonReBenchmarkContext(benchmarkCase);
        if (!context.SupportsSplitPhases)
        {
            Console.Error.WriteLine(
                $"PythonRe split phase diagnostics currently require a capture-free, zero-offset case; " +
                $"'{id}' is unsupported.");
            return 1;
        }

        var expected = context.ExecutePythonReSplitChecksum();
        var predecoded = context.ExecutePredecodedSplitChecksum();
        var direct = context.ExecuteDirectRegexSplitChecksum();
        var prepared = context.ExecutePreparedSplitProjectionChecksum();
        var streaming = context.ExecuteStreamingSplitProjectionChecksum();
        if (expected != predecoded ||
            expected != direct ||
            expected != prepared ||
            expected != streaming)
        {
            throw new InvalidOperationException(
                $"PythonRe split phase diagnostic '{id}' produced incomparable sinks: " +
                $"public={expected}, predecoded={predecoded}, direct={direct}, " +
                $"prepared={prepared}, streaming={streaming}.");
        }

        Console.WriteLine($"CaseId             : {benchmarkCase.Id}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"ManagedRoute       : {context.DescribeManagedRoute()}");
        Console.WriteLine($"InputBytes         : {context.InputBytes.Length}");
        Console.WriteLine($"SplitCount         : {context.PreparedSplitCount}");
        Console.WriteLine($"OutputValueCount   : {context.PreparedSplitValueCount}");
        Console.WriteLine($"OutputUtf8Bytes    : {context.PreparedSplitOutputUtf8Bytes}");
        Console.WriteLine($"Iterations         : {effectiveIterations}");
        Console.WriteLine($"Samples            : {samples}");
        Console.WriteLine($"CpuPolicy          : {processorScope.Policy}");
        Console.WriteLine($"CpuAffinityMask    : {processorScope.AffinityMask}");
        Console.WriteLine("Phase model        : cumulative controls; phase timings are not additive");
        PrintOperation("Utf8DecodeOnly", MeasureRetainedPhaseOperation(context.DecodeInput, effectiveIterations, samples));
        PrintOperation("PredecodedDiscovery", MeasurePhaseOperation(context.ExecuteSplitDiscoveryOnly, effectiveIterations, samples));
        PrintOperation("CollectRanges", MeasureRetainedPhaseOperation(context.CollectSplitRangesArray, effectiveIterations, samples));
        PrintOperation("PreparedProjection", MeasureRetainedPhaseOperation(context.ProjectPreparedSplitStrings, effectiveIterations, samples));
        PrintOperation("StreamingProjection", MeasureRetainedPhaseOperation(context.ProjectStreamingSplitStrings, effectiveIterations, samples));
        PrintOperation("RegexSplitDirect", MeasureRetainedPhaseOperation(context.ExecuteDirectRegexSplitStrings, effectiveIterations, samples));
        PrintOperation("PredecodedRequired", MeasureRetainedPhaseOperation(context.ExecutePredecodedSplitStrings, effectiveIterations, samples));
        PrintOperation("PythonRePublic", MeasureRetainedPhaseOperation(context.ExecutePythonReSplitStrings, effectiveIterations, samples));
        PrintOperation("ChecksumTraversal", MeasurePhaseOperation(context.ExecutePreparedSplitProjectionChecksum, effectiveIterations, samples));
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
            not PythonReBenchmarkOperation.Match and
            not PythonReBenchmarkOperation.FullMatch)
        {
            Console.Error.WriteLine(
                $"PythonRe one-shot phase diagnostics require Search, Match, or FullMatch; " +
                $"'{id}' uses {benchmarkCase.Operation}.");
            return 1;
        }

        var effectiveIterations = GetEffectiveIterations(benchmarkCase, iterations);
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        var context = new PythonReBenchmarkContext(benchmarkCase);
        if (!context.SupportsOneShotPhases)
        {
            Console.Error.WriteLine(
                $"PythonRe one-shot phase diagnostics require Search, Match, or FullMatch; " +
                $"'{id}' is unsupported.");
            return 1;
        }

        var expected = context.ExecutePythonReOneShot();
        var direct = context.ExecuteCoreDirectOneShot();
        var rawExact = context.SupportsExactLiteralOneShotReplay
            ? context.ExecuteCoreRawOneShot()
            : expected;
        var validatedExact = context.SupportsExactLiteralOneShotReplay
            ? context.ExecuteValidatedCoreRawOneShot()
            : expected;
        var predecoded = context.ExecutePredecodedOneShot();
        if (expected != direct ||
            expected != rawExact ||
            expected != validatedExact ||
            expected != predecoded)
        {
            throw new InvalidOperationException(
                $"PythonRe one-shot phase diagnostic '{id}' produced incomparable results: " +
                $"public={expected}, direct={direct}, raw-exact={rawExact}, " +
                $"validated-exact={validatedExact}, " +
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
        var phaseWarmupCalls = context.InputBytes.Length <= 32 * 1_024 ? 250_000 : 1_024;
        PythonReOperationMeasurement MeasurePhase<T>(Func<T> operation) =>
            MeasureRetainedOperation(operation, effectiveIterations, samples, phaseWarmupCalls);
        PrintOperation("ValidationOnly", MeasurePhase(context.ExecuteOneShotValidation));
        PrintOperation("Utf8DecodeOnly", MeasurePhase(context.DecodeInput));
        if (context.SupportsExactLiteralOneShotReplay)
        {
            PrintOperation("RawExactLiteral", MeasurePhase(context.ExecuteCoreRawOneShot));
            PrintOperation("ValidatedExactLiteral", MeasurePhase(context.ExecuteValidatedCoreRawOneShot));
        }

        PrintOperation("CoreDirect", MeasurePhase(context.ExecuteCoreDirectOneShot));
        PrintOperation("PredecodedRegex", MeasurePhase(context.ExecutePredecodedOneShot));
        PrintOperation("PythonRePublic", MeasurePhase(context.ExecutePythonReOneShot));
        return 0;
    }

    private static int MeasureConstructionPattern(string pattern, int iterations, int samples, string fullMatchInput)
    {
        const PythonReCompileOptions options = PythonReCompileOptions.None;
        var parseResult = new PythonReParser(pattern).Parse(options);
        var translation = PythonReTranslator.Translate(parseResult, true);
        var referencePattern = PythonReDotNetReference.Render(parseResult);
        var referenceOptions = PythonReDotNetReference.ToRegexOptions(options);
        var input = Encoding.UTF8.GetBytes("prefix item-123 foo Ð¨ÐµÑ€Ð»Ð¾Ðº suffix");
        var fullMatchInputUtf8 = Encoding.UTF8.GetBytes(fullMatchInput);
        var fullMatchMissUtf8 = "__pythonre_fullmatch_miss__"u8.ToArray();
        var prepared = new Utf8PythonRegex(pattern, options: options);
        var nativePatternText = Encoding.UTF8.GetString(translation.NativePatternUtf8);
        var coreFullPattern = $@"\A(?:{nativePatternText})\z";
        var preparedCoreFull = new Utf8Regex(coreFullPattern, options: translation.NativeRegexOptions);
        var lazyCoreFull = new Lazy<Utf8Regex>(
            () => new Utf8Regex(coreFullPattern, options: translation.NativeRegexOptions),
            LazyThreadSafetyMode.ExecutionAndPublication);
        _ = lazyCoreFull.Value;
        var reuseIterations = Math.Max(iterations, 5_000);

        Console.WriteLine($"Pattern            : {pattern}");
        Console.WriteLine($"NativePattern      : {nativePatternText}");
        Console.WriteLine($"InputBytes         : {input.Length}");
        Console.WriteLine($"FullMatchInputBytes: {fullMatchInputUtf8.Length}");
        Console.WriteLine($"Iterations         : {iterations} (fixed, capped at 512)");
        Console.WriteLine($"ReuseIterations    : {reuseIterations} (minimum 5000)");
        Console.WriteLine($"Samples            : {samples} (capped at 15)");
        PrintOperation("ParseTranslate", MeasureOperation(
            () =>
            {
                var parsed = new PythonReParser(pattern).Parse(options);
                return PythonReTranslator.Translate(parsed, true).NativePatternUtf8.Length;
            },
            iterations,
            samples));
        PrintOperation("ManagedSearch", MeasureOperation(
            () => new Regex(referencePattern, referenceOptions, Regex.InfiniteMatchTimeout).GetHashCode(),
            iterations,
            samples));
        PrintOperation("ManagedFull", MeasureOperation(
            () => new Regex($@"\A(?:{referencePattern})\z", referenceOptions, Regex.InfiniteMatchTimeout).GetHashCode(),
            iterations,
            samples));
        PrintOperation("CoreSearch", MeasureOperation(
            () => new Utf8Regex(translation.NativePatternUtf8, options: translation.NativeRegexOptions, policy: new Utf8RegexPolicy
            {
                MatchTimeout = Timeout.InfiniteTimeSpan,
                ResourcePolicy = default
            }).GetHashCode(),
            iterations,
            samples));
        PrintOperation("CoreFull", MeasureOperation(
            () => new Utf8Regex($@"\A(?:{nativePatternText})\z", options: translation.NativeRegexOptions).GetHashCode(),
            iterations,
            samples));
        PrintOperation("PythonReConstruct", MeasureOperation(
            () => new Utf8PythonRegex(pattern, options: options).GetHashCode(),
            iterations,
            samples));
        PrintOperation("ConstructFirstCall", MeasureOperation(
            () =>
            {
                var regex = new Utf8PythonRegex(pattern, options: options);
                return regex.GetHashCode() ^ (regex.IsMatch(input) ? 1 : 0);
            },
            iterations,
            samples));
        PrintOperation("ConstructFirstFull", MeasureOperation(
            () =>
            {
                var regex = new Utf8PythonRegex(pattern, options: options);
                return regex.GetHashCode() ^ (regex.Match(fullMatchInputUtf8, mode: Utf8PythonMatchMode.FullMatch).Success ? 1 : 0);
            },
            iterations,
            samples));
        PrintOperation("PreparedFullHit", MeasureOperation(
            () => prepared.Match(fullMatchInputUtf8, mode: Utf8PythonMatchMode.FullMatch).Success ? 1 : 0,
            reuseIterations,
            samples));
        PrintOperation("PreparedFullMiss", MeasureOperation(
            () => prepared.Match(fullMatchMissUtf8, mode: Utf8PythonMatchMode.FullMatch).Success ? 1 : 0,
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
        var prefix = new string('x', 65_536) + "Ã©";
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

        var pythonMatch = pythonRegex.Match(input, startOffsetInBytes, mode: Utf8PythonMatchMode.FullMatch);
        var pythonDetailedMiss = PythonReBenchmarkReporter.MaterializeDetailedFullMatch(pythonRegex, missInput, startOffsetInBytes);
        var managedResult = managedFullRegex.IsMatch(subject.AsSpan(prefix.Length));
        var managedMiss = managedFullRegex.IsMatch(missSubject.AsSpan(prefix.Length));
        if (!pythonMatch.Success || pythonMatch.EndOffsetInBytes != input.Length ||
            pythonDetailedMiss.Success || !managedResult || managedMiss)
        {
            throw new InvalidOperationException("PythonRe start-offset FullMatch diagnostic failed its result preflight.");
        }

        Console.WriteLine($"Pattern            : {pattern}");
        Console.WriteLine($"InputBytes         : {input.Length}");
        Console.WriteLine($"StartOffsetInBytes : {startOffsetInBytes}");
        Console.WriteLine($"StartOffsetInUtf16 : {prefix.Length}");
        Console.WriteLine("Implementation     : owned UTF8 engine");
        Console.WriteLine($"Iterations         : {iterations} (capped at 2000)");
        Console.WriteLine($"Samples            : {samples} (capped at 15)");
        PrintOperation("PythonReFullMatch", MeasureOperation(
            () => pythonRegex.Match(input, startOffsetInBytes, mode: Utf8PythonMatchMode.FullMatch).EndOffsetInBytes,
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
            () => pythonRegex.Match(missInput, startOffsetInBytes, mode: Utf8PythonMatchMode.FullMatch).Success ? 1 : 0,
            iterations,
            samples));
        PrintOperation("PythonReDetailedMiss", MeasureOperation(
            () => PythonReBenchmarkReporter.MaterializeDetailedFullMatch(pythonRegex, missInput, startOffsetInBytes).Success ? 1 : 0,
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

    private static int MeasureEmptyProgressionScaling(int baseTokenCount, int iterations, int samples)
    {
        const string pattern = @"\b|\w+";
        var regex = new Utf8PythonRegex(pattern);
        var unsupportedProgression = PythonReBenchmarkReporter.MaterializeStructural(new Utf8PythonRegex("x*|y"), ("y"u8).ToArray())
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
            if (regex.Count(input) != expectedCount || PythonReBenchmarkReporter.MaterializeStructural(regex, input).Length != expectedCount)
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
                () => PythonReBenchmarkReporter.MaterializeStructural(regex, input).Length,
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
            MeasurementProtocol = "RetainedResultBatchV2",
            PythonRe = MeasureResultOperation(context.MeasurePythonReBatch, effectiveIterations, samples, pythonResult),
            DecodeThenRegex = MeasureResultOperation(count => context.MeasureRegexBatch(count, decode: true), effectiveIterations, samples, pythonResult),
            PredecodedRegex = MeasureResultOperation(count => context.MeasureRegexBatch(count, decode: false), effectiveIterations, samples, pythonResult),
            Cpython = cpython
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
            CreateNoWindow = true
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
            Samples = samples
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
            PythonReBenchmarkOperation.SearchDetailed => 5_000,            PythonReBenchmarkOperation.Count when benchmarkCase.Id == "zero-width/count" => 5_000,            PythonReBenchmarkOperation.Count or
            PythonReBenchmarkOperation.CountFromOffset => 500,PythonReBenchmarkOperation.FindIterDetailed or PythonReBenchmarkOperation.FindAllStructural=> 1_000,            _ => 2_000        };
        return Math.Max(requestedIterations, minimum);
    }

    private static PythonReOperationMeasurement MeasureResultOperation(
        Func<int, PythonReBenchmarkBatch> runBatch,
        int iterations,
        int samples,
        int expectedChecksum)
    {
        var warmup = Stopwatch.StartNew();
        var warmupCalls = 0;
        var warmupBatchSize = 1;
        do
        {
            var batch = runBatch(warmupBatchSize);
            if (batch.Checksum != expectedChecksum)
            {
                throw new InvalidOperationException(
                    $"PythonRe warmup result checksum {batch.Checksum} does not match preflight {expectedChecksum}.");
            }

            s_sink ^= batch.Checksum;
            warmupCalls += warmupBatchSize;
            // A slow public call needs one warmup, not an opaque 16-call batch.
            // Amortize the clock only after measuring a cheap batch.
            if (batch.Elapsed.TotalMilliseconds < 20)
                warmupBatchSize = Math.Min(warmupBatchSize * 2, Math.Min(iterations, 16));
        }
        // Warm through the tiering delay even for cheap operations. A call cap
        // can finish this phase early; the clock and small batches bound it.
        while (warmup.ElapsedMilliseconds < 1_000);
        warmup.Stop();

        // Calibrate the retained-result loop independently. Consume results
        // after timing, as in the producer and CPython batch implementations.
        var probeCount = 1;
        PythonReBenchmarkBatch probe;
        do
        {
            probe = runBatch(probeCount);
            if (probe.Elapsed.TotalMilliseconds >= 20 || probeCount >= 10_000_000) break;
            probeCount = Math.Min(probeCount * 2, 10_000_000);
        } while (true);
        iterations = (int)Math.Clamp(Math.Ceiling(probeCount * 50 / Math.Max(probe.Elapsed.TotalMilliseconds, double.Epsilon)), 1, 10_000_000);
        var confirmation = runBatch(iterations);
        if (confirmation.Elapsed.TotalMilliseconds is > 0 and < 40)
            iterations = (int)Math.Clamp(Math.Ceiling(iterations * 50 / confirmation.Elapsed.TotalMilliseconds), 1, 10_000_000);
        var rawMicroseconds = new double[samples];
        var microseconds = new double[samples];
        var allocations = new long[samples];
        for (var sample = 0; sample < samples; sample++)
        {
            var batch = runBatch(iterations);
            if (batch.Checksum != expectedChecksum)
            {
                throw new InvalidOperationException(
                    $"PythonRe timed result checksum {batch.Checksum} does not match preflight {expectedChecksum}.");
            }

            allocations[sample] = batch.AllocatedBytes / iterations;
            rawMicroseconds[sample] = batch.Elapsed.TotalMicroseconds;
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
            EffectiveIterations = iterations,
            SampleMicroseconds = rawMicroseconds,
            WarmupCalls = warmupCalls,
            WarmupMilliseconds = warmup.Elapsed.TotalMilliseconds
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
        warmup.Stop();

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
            WarmupMilliseconds = warmup.Elapsed.TotalMilliseconds
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
        var maximumWarmupCalls = Math.Max(65_536, minimumWarmupCalls);
        do
        {
            s_retainedSink = operation();
            warmupCalls++;
        }
        while ((warmup.ElapsedMilliseconds < 100 || warmupCalls < minimumWarmupCalls) &&
               warmupCalls < maximumWarmupCalls);
        warmup.Stop();

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
            WarmupMilliseconds = warmup.Elapsed.TotalMilliseconds
        };
    }

    private static PythonReOperationMeasurement MeasurePhaseOperation(
        Func<int> operation,
        int iterations,
        int samples) => MeasureOperation(operation, iterations, samples, minimumWarmupCalls: 1_024);

    private static PythonReOperationMeasurement MeasureRetainedPhaseOperation<T>(
        Func<T> operation,
        int iterations,
        int samples,
        int minimumWarmupCalls = 1_024) =>
        MeasureRetainedOperation(operation, iterations, samples, minimumWarmupCalls);

    private static void Print(PythonReBenchmarkCase benchmarkCase, PythonReCaseMeasurement measurement)
    {
        Console.WriteLine($"CaseId             : {benchmarkCase.Id}");
        Console.WriteLine($"Pattern            : {benchmarkCase.Pattern}");
        Console.WriteLine($"Options            : {benchmarkCase.Options}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"InputBytes         : {measurement.InputUtf8Bytes}");
        Console.WriteLine($"Iterations         : {measurement.EffectiveIterations}");
        Console.WriteLine($"Samples            : {measurement.Samples}");
        PrintEnvironment(measurement.Environment);
        PrintOperation("PythonRe", measurement.PythonRe!);
        PrintOperation("DecodeThenRegex", measurement.DecodeThenRegex!);
        PrintOperation("PredecodedRegex", measurement.PredecodedRegex!);
        if (measurement.Cpython is { } cpython)
        {
            PrintOperation("CPythonDecode", cpython.DecodeThenRe);
            PrintOperation("CPythonPredecoded", cpython.PredecodedRe);
        }
    }

    private static void PrintEnvironment(PythonReBenchmarkEnvironment environment)
    {
        Console.WriteLine($"CheckoutCommit     : {environment.SourceCommit}");
        Console.WriteLine($"BenchmarkAssembly  : {environment.BenchmarkAssembly}");
        Console.WriteLine($"CoreAssembly       : {environment.CoreAssembly}");
        Console.WriteLine($"PythonReAssembly   : {environment.PythonReAssembly}");
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
            ":(exclude)README.md",
            ":(exclude)src/Lokad.Utf8Regex.Pcre2/README.md",
            ":(exclude)bench/Snapshots",
            ":(exclude)src/Lokad.Utf8Regex.PythonRe/README.md",
            ":(exclude)src/Lokad.Utf8Regex.Posix/README.md");
        var untrackedStatus = RunGit(
            "ls-files",
            "--others",
            "--exclude-standard",
            "--",
            ".");
        return new PythonReBenchmarkEnvironment
        {
            // Checkout provenance and actual loaded bytes are distinct.
            SourceCommit = RunGit("rev-parse", "--short=12", "HEAD") ?? "<unknown>",
            BenchmarkAssembly = BenchmarkInspectReporter.GetAssemblyIdentity(typeof(PythonReBenchmarkReporter).Assembly),
            CoreAssembly = BenchmarkInspectReporter.GetAssemblyIdentity(typeof(Utf8Regex).Assembly),
            PythonReAssembly = BenchmarkInspectReporter.GetAssemblyIdentity(typeof(Utf8PythonRegex).Assembly),
            TrackedDirty = !string.IsNullOrWhiteSpace(trackedStatus),
            HasUntrackedFiles = !string.IsNullOrWhiteSpace(untrackedStatus),
            Runtime = RuntimeInformation.FrameworkDescription,
            OperatingSystem = RuntimeInformation.OSDescription,
            Processor = Environment.GetEnvironmentVariable("PROCESSOR_IDENTIFIER") ??
                RuntimeInformation.ProcessArchitecture.ToString()
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
            Limitation = "The original upstream CPython version was not recorded; do not infer one from local vector names."
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
            CreateNoWindow = true
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

    // Explicit host materialization over the real cursor. Regex controls cover
    // cursor advances/capture ranges; this aggregate and its strings belong here.
    internal static PythonReStructuralMatch[] MaterializeStructural(Utf8PythonRegex regex,
        ReadOnlyMemory<byte> input, int startOffsetInBytes = 0, CancellationToken cancellationToken = default)
    {
        var matches = new List<PythonReStructuralMatch>();
        using var cursor = regex.CreateMatchCursor(input, startOffsetInBytes, cancellationToken);
        while (cursor.MoveNext(out var match))
        {
            matches.Add(new PythonReStructuralMatch
            {
                Success = match.Success,
                StartOffsetInBytes = match.Value.StartOffsetInBytes,
                EndOffsetInBytes = match.Value.EndOffsetInBytes,
                ValueText = Encoding.UTF8.GetString(match.GetValueMemory().Span)
            });
        }
        return matches.ToArray();
    }
}

internal sealed class PythonReBenchmarkContext
{
    private static readonly Func<PythonReBenchmarkContext, Utf8PythonRangeMatch, byte[]> s_hostConstantReplacement =
        static (context, _) => context._replacementBytes;
    private static readonly UTF8Encoding s_strictUtf8 = new(false, true);
    private readonly PythonReBenchmarkCase _case;
    private readonly Utf8PythonRegex _pythonRegex;
    private readonly Regex _regex;
    private readonly Regex _fullRegex;
    private readonly bool _supportsReplacementPhases;
    private readonly Utf8Regex? _coreReplacementRegex;
    private readonly Regex? _replacementNonEmptyAtSamePositionRegex;
    private readonly PythonReReplacementPlan _preparedReplacementPlan;
    private readonly string _preparedDotNetReplacement;
    private readonly string _preparedLiteralReplacement;
    private readonly byte[] _preparedLiteralReplacementUtf8;
    private readonly byte[] _preparedExactReplacementPatternUtf8;
    private readonly PythonReBenchmarkReplacementResult? _preparedReplacementResult;
    private readonly bool _supportsSplitPhases;
    private readonly PythonReBenchmarkCaptureRange[] _preparedSplitRanges;
    private readonly string?[] _preparedSplitStrings;
    private readonly Utf8Regex? _oneShotCoreRegex;
    private readonly bool _supportsOneShotPhases;
    private readonly bool _supportsExactLiteralOneShotReplay;
    private readonly string _decoded;
    private readonly byte[] _replacementBytes;
    private readonly string _dotNetReplacement;
    private readonly int _captureCount;
    private readonly int _startOffsetInUtf16;
    private readonly MatchEvaluator _constantDotNetEvaluator;
    private int _callbackChecksum;
    private ulong _callbackSemanticDigest;

    internal PythonReBenchmarkContext(PythonReBenchmarkCase benchmarkCase)
    {
        _case = benchmarkCase;
        _constantDotNetEvaluator = _ => _case.Replacement;
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
        _pythonRegex = new Utf8PythonRegex(benchmarkCase.Pattern, options: benchmarkCase.Options);
        var parsed = new PythonReParser(benchmarkCase.Pattern).Parse(benchmarkCase.Options);
        var translation = PythonReTranslator.Translate(parsed, true);
        var translatedPattern = PythonReDotNetReference.Render(parsed);
        var regexOptions = PythonReDotNetReference.ToRegexOptions(parsed.Options);
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
                    _replacementBytes,
                    parsed.CaptureGroupCount,
                    parsed.NamedGroups).ToDotNetReplacementString()
                : benchmarkCase.Replacement;

        if (benchmarkCase.Operation is PythonReBenchmarkOperation.ReplaceString or
                PythonReBenchmarkOperation.ReplaceUtf8 or
                PythonReBenchmarkOperation.ReplaceStringLimited or
                PythonReBenchmarkOperation.SubnString or
                PythonReBenchmarkOperation.SubnUtf8 &&
            _captureCount == 0)
        {
            var plan = PythonReReplacementParser.Parse(
                _replacementBytes,
                _captureCount,
                new Dictionary<string, int>(StringComparer.Ordinal));
            var hasNonemptyExactReplacementPattern =
                (benchmarkCase.Options & PythonReCompileOptions.IgnoreCase) == 0 &&
                PythonReTranslator.IsExactLiteral(parsed.Root) &&
                PythonReTranslator.CanMatchNonEmpty(parsed.Root, parsed);
            _supportsReplacementPhases = plan.Tokens.All(
                static token => token.Kind == PythonReReplacementTokenKind.Literal) &&
                (benchmarkCase.Operation != PythonReBenchmarkOperation.ReplaceStringLimited ||
                 hasNonemptyExactReplacementPattern);
            _coreReplacementRegex = _supportsReplacementPhases
                ? new Utf8Regex(translatedPattern, options: regexOptions)                : null;
            _replacementNonEmptyAtSamePositionRegex = _supportsReplacementPhases &&
                PythonReTranslator.CanMatchEmpty(parsed.Root, parsed) &&
                PythonReTranslator.CanMatchNonEmpty(parsed.Root, parsed)
                    ? new Regex(
                        @"\G(?:(?:" + translatedPattern + @"))(?!\G)",
                        regexOptions,
                        Regex.InfiniteMatchTimeout)
                    : null;
            _preparedReplacementPlan = plan;
            _preparedDotNetReplacement = plan.ToDotNetReplacementString();
            _preparedLiteralReplacement = string.Concat(
                plan.Tokens.Select(PythonReBenchmarkReplacementFormatting.GetLiteralText));
            _preparedLiteralReplacementUtf8 = s_strictUtf8.GetBytes(_preparedLiteralReplacement);
            _preparedExactReplacementPatternUtf8 = hasNonemptyExactReplacementPattern
                ? _coreReplacementRegex?.Inspection.PreparedRegex.LiteralUtf8 ?? []
                : [];
            _preparedReplacementResult = _supportsReplacementPhases
                ? ExecutePredecodedRequiredReplacement()
                : null;
        }
        else
        {
            _supportsReplacementPhases = false;
            _coreReplacementRegex = null;
            _replacementNonEmptyAtSamePositionRegex = null;
            _preparedReplacementPlan = default;
            _preparedDotNetReplacement = string.Empty;
            _preparedLiteralReplacement = string.Empty;
            _preparedLiteralReplacementUtf8 = [];
            _preparedExactReplacementPatternUtf8 = [];
            _preparedReplacementResult = null;
        }

        _supportsSplitPhases = benchmarkCase.Operation is
                PythonReBenchmarkOperation.SplitStrings or
                PythonReBenchmarkOperation.SplitStringsLimited &&
            _captureCount == 0 &&
            benchmarkCase.Coverage.StartOffsetInBytes == 0;
        _preparedSplitRanges = _supportsSplitPhases
            ? CollectSplitRangesArray()
            : [];
        _preparedSplitStrings = _supportsSplitPhases
            ? ProjectSplitStrings(_preparedSplitRanges)
            : [];

        if (benchmarkCase.Operation is PythonReBenchmarkOperation.Search or
                PythonReBenchmarkOperation.Match or
                PythonReBenchmarkOperation.FullMatch)
        {
            var oneShotCorePattern = benchmarkCase.Operation == PythonReBenchmarkOperation.FullMatch
                ? @"\A(?:" + translatedPattern + @")\z"
                : translatedPattern;
            var oneShotCoreRegex = new Utf8Regex(oneShotCorePattern, options: regexOptions);
            _oneShotCoreRegex = oneShotCoreRegex;
            _supportsOneShotPhases = true;
            _supportsExactLiteralOneShotReplay =
                benchmarkCase.Operation != PythonReBenchmarkOperation.FullMatch &&
                oneShotCoreRegex.Inspection.ExecutionKind is
                    NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal &&
                oneShotCoreRegex.Inspection.DebugTryMatchExactLiteral(InputBytes, out _);
        }
        else
        {
            _oneShotCoreRegex = null;
            _supportsOneShotPhases = false;
            _supportsExactLiteralOneShotReplay = false;
        }
    }

    internal byte[] InputBytes { get; }

    internal int CaptureCount => _captureCount;

    internal bool SupportsReplacementPhases => _supportsReplacementPhases;

    internal bool SupportsSplitPhases => _supportsSplitPhases;

    internal int PreparedSplitCount => _preparedSplitRanges.Length;

    internal int PreparedSplitValueCount => _preparedSplitStrings.Length;

    internal int PreparedSplitOutputUtf8Bytes => _preparedSplitStrings.Sum(
        static value => value is null ? 0 : s_strictUtf8.GetByteCount(value));

    internal string ReplacementCoreExecutionKind => GetCoreReplacementRegex()
        .Inspection.ExecutionKind.ToString();

    internal int PreparedReplacementMatchCount => _case.Operation == PythonReBenchmarkOperation.ReplaceStringLimited
        ? Math.Min(_regex.Count(_decoded), _case.Coverage.ReplacementCount)
        : _regex.Count(_decoded);

    internal bool ReturnsReplacementCount => IsSubnOperation;

    internal int PreparedReplacementOutputUtf8Bytes => GetPreparedReplacementResult().ResultBytes?.Length ??
        s_strictUtf8.GetByteCount(GetPreparedReplacementResult().ResultText);

    internal bool SupportsOneShotPhases => _supportsOneShotPhases;

    internal bool SupportsExactLiteralOneShotReplay => _supportsExactLiteralOneShotReplay;

    internal string OneShotCoreExecutionKind => GetOneShotCoreRegex()
        .Inspection.ExecutionKind.ToString();

    internal string DescribeManagedRoute() => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => DescribeBackend(
            _pythonRegex.DebugUtf8ExecutionKind,
            "boolean result"),        PythonReBenchmarkOperation.Search => DescribeBackend(
            _pythonRegex.DebugUtf8ExecutionKind,
            "value ranges"),        PythonReBenchmarkOperation.SearchFromOffset => DescribeBackend(
            _pythonRegex.DebugUtf8ExecutionKind,
            "value ranges from a nonzero byte offset"),        PythonReBenchmarkOperation.Match => DescribeBackend(
            _pythonRegex.DebugUtf8ExecutionKind,
            "anchored value ranges"),        PythonReBenchmarkOperation.FullMatch => DescribeBackend(
            "OwnedScalar",
            "full-match value ranges"),        PythonReBenchmarkOperation.SearchDetailed => DescribeBackend(
            _pythonRegex.DebugUtf8ExecutionKind,
            "detailed capture projection"),        PythonReBenchmarkOperation.Count when !_pythonRegex.DebugUsesZeroOffsetUtf8SearchValueFastPath =>
            "owned scalar VM; byte-driven Python count progression",        PythonReBenchmarkOperation.Count => DescribeBackend(
            _pythonRegex.DebugUtf8ExecutionKind,
            "Python-style count progression"),        PythonReBenchmarkOperation.CountFromOffset =>
            "owned scalar VM; byte-driven Python count from a nonzero offset",        PythonReBenchmarkOperation.FindIterDetailed =>
            "owned scalar VM; detailed iteration shaping",        PythonReBenchmarkOperation.FindAllStructural => DescribeBackend(
            _pythonRegex.DebugUtf8ExecutionKind,
            "group-zero structural match shaping"),        PythonReBenchmarkOperation.ReplaceString or
            PythonReBenchmarkOperation.ReplaceStringLimited or
            PythonReBenchmarkOperation.SubnString =>
            DescribeBackend(
                _pythonRegex.DebugUtf8ExecutionKind,
                "replacement; string shaping"),        PythonReBenchmarkOperation.ReplaceUtf8 or
            PythonReBenchmarkOperation.SubnUtf8 =>
            DescribeBackend(
                _pythonRegex.DebugUtf8ExecutionKind,
                "replacement; UTF-8 shaping"),        PythonReBenchmarkOperation.SubnEvaluatorString =>
            "owned scalar VM; callback replacement; string shaping",        PythonReBenchmarkOperation.ReplaceEvaluatorString =>
            "owned scalar VM; callback replacement; string shaping without count",        PythonReBenchmarkOperation.SubnEvaluatorUtf8 =>
            "owned scalar VM; callback replacement; UTF-8 shaping",        PythonReBenchmarkOperation.SplitStrings =>
            "owned scalar VM; split; string shaping",        PythonReBenchmarkOperation.SplitStringsLimited =>
            "owned scalar VM; bounded split; string shaping",        PythonReBenchmarkOperation.SplitDetailed =>
            "owned scalar VM; split; item-metadata shaping",        _ => throw new ArgumentOutOfRangeException()    };

    // Utf8Regex is the historical component label, not an operation route.
    private static string DescribeBackend(
        string? executionKind,
        string projection) => $"Utf8Regex/{executionKind ?? "OwnedScalar"}; {projection}";

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
                        result = _pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.Search);
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
                        result = _pythonRegex.Match(InputBytes, _case.Coverage.StartOffsetInBytes, mode: Utf8PythonMatchMode.Search);
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
                        result = _pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.FullMatch);
                    }

                    return Complete(
                        Stopwatch.GetTimestamp(),
                        GC.GetAllocatedBytesForCurrentThread(),
                        Checksum(result),
                        SemanticDigest(_case.Operation, result));
                }
            case PythonReBenchmarkOperation.SearchDetailed:
                {
                    PythonReHostDetailedMatch result = default;
                    for (var iteration = 0; iteration < iterations; iteration++)
                    {
                        result = PythonReBenchmarkReporter.MaterializeDetailedSearch(_pythonRegex, InputBytes);
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
            case PythonReBenchmarkOperation.FindIterDetailed:
                {
                    PythonReHostDetailedMatch[] result = [];
                    for (var iteration = 0; iteration < iterations; iteration++)
                    {
                        result = PythonReBenchmarkReporter.MaterializeDetailedMatches(_pythonRegex, InputBytes);
                    }

                    return Complete(
                        Stopwatch.GetTimestamp(),
                        GC.GetAllocatedBytesForCurrentThread(),
                        Checksum(result),
                        SemanticDigest(_case.Operation, result));
                }
            case PythonReBenchmarkOperation.FindAllStructural:
                {
                    PythonReStructuralMatch[] result = [];
                    for (var iteration = 0; iteration < iterations; iteration++)
                    {
                        result = PythonReBenchmarkReporter.MaterializeStructural(_pythonRegex, InputBytes);
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
                        result = Encoding.UTF8.GetString(_pythonRegex.Replace(InputBytes, _replacementBytes));
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
                        result = _pythonRegex.Replace(InputBytes, _replacementBytes);
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
                        result = Encoding.UTF8.GetString(_pythonRegex.Replace(
                            InputBytes,
                            _replacementBytes,
                            _case.Coverage.ReplacementCount));
                    }

                    return Complete(
                        Stopwatch.GetTimestamp(),
                        GC.GetAllocatedBytesForCurrentThread(),
                        Checksum(result),
                        SemanticDigest(_case.Operation, result));
                }
            case PythonReBenchmarkOperation.SubnString:
                {
                    PythonReSubnTextResult result = default;
                    for (var iteration = 0; iteration < iterations; iteration++)
                    {
                        result = PythonReSubnTextResult.Decode(_pythonRegex.Subn(InputBytes, _replacementBytes));
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
                        result = _pythonRegex.Subn(InputBytes, _replacementBytes);
                    }

                    return Complete(
                        Stopwatch.GetTimestamp(),
                        GC.GetAllocatedBytesForCurrentThread(),
                        Checksum(result),
                        SemanticDigest(_case.Operation, result));
                }
            case PythonReBenchmarkOperation.SubnEvaluatorString:
                {
                    PythonReSubnTextResult result = default;
                    for (var iteration = 0; iteration < iterations; iteration++)
                        result = PythonReSubnTextResult.Decode(ComposePythonReReplacement());

                    var completed = Stopwatch.GetTimestamp();
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    // One untimed semantic replay retains callback capture evidence.
                    // The timed host loop copies its prepared replacement; capture replay is untimed.
                    _ = ExecutePythonReEvaluatorString();
                    return Complete(completed, allocated,
                        Combine(Checksum(result), _callbackChecksum),
                        SemanticDigest(_case.Operation, result, _callbackSemanticDigest));
                }
            case PythonReBenchmarkOperation.SubnEvaluatorUtf8:
                {
                    Utf8PythonSubnUtf8Result result = default;
                    for (var iteration = 0; iteration < iterations; iteration++)
                        result = ComposePythonReReplacement();

                    var completed = Stopwatch.GetTimestamp();
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    // One untimed semantic replay retains callback capture evidence.
                    // The timed host loop copies its prepared replacement; capture replay is untimed.
                    _ = ExecutePythonReEvaluatorUtf8();
                    return Complete(completed, allocated,
                        Combine(Checksum(result), _callbackChecksum),
                        SemanticDigest(_case.Operation, result, _callbackSemanticDigest));
                }
            case PythonReBenchmarkOperation.ReplaceEvaluatorString:
                {
                    string result = string.Empty;
                    for (var iteration = 0; iteration < iterations; iteration++)
                        result = Encoding.UTF8.GetString(ComposePythonReReplacement().ResultBytes);

                    var completed = Stopwatch.GetTimestamp();
                    var allocated = GC.GetAllocatedBytesForCurrentThread();
                    // One untimed semantic replay retains callback capture evidence.
                    // The timed host loop copies its prepared replacement; capture replay is untimed.
                    _ = ExecutePythonReReplaceEvaluatorString();
                    return Complete(completed, allocated,
                        Combine(Checksum(result), _callbackChecksum),
                        SemanticDigest(_case.Operation, result, _callbackSemanticDigest));
                }
            case PythonReBenchmarkOperation.SplitStrings:
                {
                    string?[] result = [];
                    for (var iteration = 0; iteration < iterations; iteration++)
                    {
                        result = MaterializePythonSplitStrings();
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
                        result = MaterializePythonSplitStrings(_case.Coverage.MaxSplit);
                    }

                    return Complete(
                        Stopwatch.GetTimestamp(),
                        GC.GetAllocatedBytesForCurrentThread(),
                        Checksum(result),
                        SemanticDigest(_case.Operation, result));
                }
            case PythonReBenchmarkOperation.SplitDetailed:
                {
                    SplitTextItem[] result = [];
                    for (var iteration = 0; iteration < iterations; iteration++)
                    {
                        result = MaterializePythonSplitDetailed();
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
                    result = _pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.Search);
                    consumptionChecksum += GetConsumptionToken(result);
                }
                break;
            case PythonReBenchmarkOperation.SearchFromOffset:
                for (var iteration = 0; iteration < iterations; iteration++)
                {
                    result = _pythonRegex.Match(InputBytes, _case.Coverage.StartOffsetInBytes, mode: Utf8PythonMatchMode.Search);
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
                    result = _pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.FullMatch);
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
        PythonReBenchmarkOperation.Search => GetConsumptionToken(_pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.Search)),        PythonReBenchmarkOperation.SearchFromOffset => GetConsumptionToken(
            _pythonRegex.Match(InputBytes, _case.Coverage.StartOffsetInBytes, mode: Utf8PythonMatchMode.Search)),        PythonReBenchmarkOperation.Match => GetConsumptionToken(_pythonRegex.Match(InputBytes)),        PythonReBenchmarkOperation.FullMatch => GetConsumptionToken(_pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.FullMatch)),        _ => 0    };

    internal int ExecutePythonRe() => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _pythonRegex.IsMatch(InputBytes) ? 1 : 0,        PythonReBenchmarkOperation.Search => Checksum(_pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.Search)),        PythonReBenchmarkOperation.SearchFromOffset => Checksum(
            _pythonRegex.Match(InputBytes, _case.Coverage.StartOffsetInBytes, mode: Utf8PythonMatchMode.Search)),        PythonReBenchmarkOperation.Match => Checksum(_pythonRegex.Match(InputBytes)),        PythonReBenchmarkOperation.FullMatch => Checksum(_pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.FullMatch)),        PythonReBenchmarkOperation.SearchDetailed => Checksum(PythonReBenchmarkReporter.MaterializeDetailedSearch(_pythonRegex, InputBytes)),        PythonReBenchmarkOperation.Count => _pythonRegex.Count(InputBytes),        PythonReBenchmarkOperation.CountFromOffset => _pythonRegex.Count(
            InputBytes,
            _case.Coverage.StartOffsetInBytes),        PythonReBenchmarkOperation.FindIterDetailed => Checksum(PythonReBenchmarkReporter.MaterializeDetailedMatches(_pythonRegex, InputBytes)),        PythonReBenchmarkOperation.FindAllStructural => Checksum(PythonReBenchmarkReporter.MaterializeStructural(_pythonRegex, InputBytes)),        PythonReBenchmarkOperation.ReplaceString => Checksum(Encoding.UTF8.GetString(_pythonRegex.Replace(InputBytes, _replacementBytes))),        PythonReBenchmarkOperation.ReplaceUtf8 => Checksum(_pythonRegex.Replace(InputBytes, _replacementBytes)),        PythonReBenchmarkOperation.ReplaceStringLimited => Checksum(Encoding.UTF8.GetString(_pythonRegex.Replace(
            InputBytes,
            _replacementBytes,
            _case.Coverage.ReplacementCount))),        PythonReBenchmarkOperation.SubnString => Checksum(PythonReSubnTextResult.Decode(_pythonRegex.Subn(InputBytes, _replacementBytes))),        PythonReBenchmarkOperation.SubnUtf8 => Checksum(_pythonRegex.Subn(InputBytes, _replacementBytes)),        PythonReBenchmarkOperation.SubnEvaluatorString => ExecutePythonReEvaluatorString(),        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => ExecutePythonReEvaluatorUtf8(),        PythonReBenchmarkOperation.ReplaceEvaluatorString => ExecutePythonReReplaceEvaluatorString(),        PythonReBenchmarkOperation.SplitStrings => Checksum(MaterializePythonSplitStrings()),        PythonReBenchmarkOperation.SplitStringsLimited => Checksum(MaterializePythonSplitStrings(_case.Coverage.MaxSplit)),        PythonReBenchmarkOperation.SplitDetailed => Checksum(MaterializePythonSplitDetailed()),        _ => throw new InvalidOperationException()    };

    internal ulong ExecutePythonReSemanticDigest() => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => SemanticDigest(
            _case.Operation,
            _pythonRegex.IsMatch(InputBytes)),        PythonReBenchmarkOperation.Search => SemanticDigest(
            _case.Operation,
            _pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.Search)),        PythonReBenchmarkOperation.SearchFromOffset => SemanticDigest(
            _case.Operation,
            _pythonRegex.Match(InputBytes, _case.Coverage.StartOffsetInBytes, mode: Utf8PythonMatchMode.Search)),        PythonReBenchmarkOperation.Match => SemanticDigest(
            _case.Operation,
            _pythonRegex.Match(InputBytes)),        PythonReBenchmarkOperation.FullMatch => SemanticDigest(
            _case.Operation,
            _pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.FullMatch)),        PythonReBenchmarkOperation.SearchDetailed => SemanticDigest(
            _case.Operation,
PythonReBenchmarkReporter.MaterializeDetailedSearch(_pythonRegex, InputBytes)),        PythonReBenchmarkOperation.Count => SemanticDigest(
            _case.Operation,
            _pythonRegex.Count(InputBytes)),        PythonReBenchmarkOperation.CountFromOffset => SemanticDigest(
            _case.Operation,
            _pythonRegex.Count(InputBytes, _case.Coverage.StartOffsetInBytes)),        PythonReBenchmarkOperation.FindIterDetailed => SemanticDigest(
            _case.Operation,
PythonReBenchmarkReporter.MaterializeDetailedMatches(_pythonRegex, InputBytes)),        PythonReBenchmarkOperation.FindAllStructural => SemanticDigest(
            _case.Operation,
            PythonReBenchmarkReporter.MaterializeStructural(_pythonRegex, InputBytes)),        PythonReBenchmarkOperation.ReplaceString => SemanticDigest(
            _case.Operation,
            Encoding.UTF8.GetString(_pythonRegex.Replace(InputBytes, _replacementBytes))),        PythonReBenchmarkOperation.ReplaceUtf8 => SemanticDigest(
            _case.Operation,
            _pythonRegex.Replace(InputBytes, _replacementBytes)),        PythonReBenchmarkOperation.ReplaceStringLimited => SemanticDigest(
            _case.Operation,
            Encoding.UTF8.GetString(_pythonRegex.Replace(InputBytes, _replacementBytes, _case.Coverage.ReplacementCount))),        PythonReBenchmarkOperation.SubnString => SemanticDigest(
            _case.Operation,
            PythonReSubnTextResult.Decode(_pythonRegex.Subn(InputBytes, _replacementBytes))),        PythonReBenchmarkOperation.SubnUtf8 => SemanticDigest(
            _case.Operation,
            _pythonRegex.Subn(InputBytes, _replacementBytes)),        PythonReBenchmarkOperation.SubnEvaluatorString => ExecutePythonReEvaluatorStringSemanticDigest(),        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => ExecutePythonReEvaluatorUtf8SemanticDigest(),        PythonReBenchmarkOperation.ReplaceEvaluatorString => ExecutePythonReReplaceEvaluatorStringSemanticDigest(),        PythonReBenchmarkOperation.SplitStrings => SemanticDigest(
            _case.Operation,
            MaterializePythonSplitStrings()),        PythonReBenchmarkOperation.SplitStringsLimited => SemanticDigest(
            _case.Operation,
            MaterializePythonSplitStrings(_case.Coverage.MaxSplit)),        PythonReBenchmarkOperation.SplitDetailed => SemanticDigest(
            _case.Operation,
            MaterializePythonSplitDetailed()),        _ => throw new InvalidOperationException()    };

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

    internal int ExecuteOneShotValidation()
    {
        EnsureOneShotPhases();
        Utf8Validation.ThrowIfInvalidOnly(InputBytes);
        return InputBytes.Length;
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

    internal PythonReBenchmarkValueResult ExecuteValidatedCoreRawOneShot()
    {
        if (!SupportsExactLiteralOneShotReplay)
        {
            throw new InvalidOperationException("The exact-literal one-shot replay is unavailable.");
        }

        Utf8Validation.ThrowIfInvalidOnly(InputBytes);
        return ExecuteCoreRawOneShot();
    }

    internal PythonReBenchmarkValueResult ExecuteCoreDirectOneShot()
    {
        EnsureOneShotPhases();
        return ProjectCoreOneShot(GetOneShotCoreRegex().Match(InputBytes));
    }

    internal PythonReBenchmarkValueResult ExecutePythonReOneShot()
    {
        EnsureOneShotPhases();
        var match = _case.Operation switch
        {
            PythonReBenchmarkOperation.Search => _pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.Search),            PythonReBenchmarkOperation.Match => _pythonRegex.Match(InputBytes),            PythonReBenchmarkOperation.FullMatch => _pythonRegex.Match(InputBytes, mode: Utf8PythonMatchMode.FullMatch),            _ => throw new InvalidOperationException($"Unsupported one-shot operation {_case.Operation}.")        };
        return match.Success
            ? new PythonReBenchmarkValueResult(true, match.StartOffsetInBytes, match.EndOffsetInBytes)
            : default;
    }

    internal PythonReBenchmarkValueResult ExecutePredecodedOneShot()
    {
        EnsureOneShotPhases();
        var match = _case.Operation == PythonReBenchmarkOperation.FullMatch
            ? _fullRegex.Match(_decoded)
            : _regex.Match(_decoded);
        if (!match.Success ||
            _case.Operation == PythonReBenchmarkOperation.Match && match.Index != 0)
        {
            return default;
        }

        var startOffsetInBytes = match.Index == 0
            ? 0
            : s_strictUtf8.GetByteCount(_decoded.AsSpan(0, match.Index));
        var endOffsetInBytes = match.Index + match.Length == _decoded.Length
            ? InputBytes.Length
            : s_strictUtf8.GetByteCount(_decoded.AsSpan(0, match.Index + match.Length));
        return new PythonReBenchmarkValueResult(true, startOffsetInBytes, endOffsetInBytes);
    }

    private PythonReBenchmarkValueResult ProjectCoreOneShot(Utf8CaptureRange match)
    {
        if (!match.Success ||
            _case.Operation == PythonReBenchmarkOperation.Match && match.StartOffsetInBytes != 0)
        {
            return default;
        }
        var indexInBytes = match.StartOffsetInBytes;
        var lengthInBytes = (match.EndOffsetInBytes - match.StartOffsetInBytes);

        if (!match.Success)
        {
            throw new InvalidOperationException("The exact-literal one-shot control has no byte range.");
        }

        return new PythonReBenchmarkValueResult(true, indexInBytes, indexInBytes + lengthInBytes);
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
            _replacementBytes,
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
        return _case.Operation == PythonReBenchmarkOperation.ReplaceStringLimited
            ? _regex.Replace(_decoded, _preparedDotNetReplacement, _case.Coverage.ReplacementCount)
            : _regex.Replace(_decoded, _preparedDotNetReplacement);
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
                regex.Replace(InputBytes, _replacementBytes),
                replacementCount);
        }

        return new PythonReBenchmarkReplacementResult(
            Encoding.UTF8.GetString(regex.Replace(InputBytes, _replacementBytes)),
            null,
            replacementCount);
    }

    internal PythonReBenchmarkReplacementResult ExecuteUtf8CoreCountedLiteralReplacement()
    {
        EnsureReplacementPhases();
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, Regex.InfiniteMatchTimeout, CancellationToken.None);
        Utf8InvocationResultLedger results = default;
        try
        {
            var bytes = GetCoreReplacementRegex().ReplaceExactLiteralForResult(InputBytes,
                _preparedLiteralReplacementUtf8, int.MaxValue, ref invocation, ref results, out var replacementCount);
            var returnedCount = IsSubnOperation ? replacementCount : (int?)null;
            if (IsUtf8ReplacementOperation)
            {
                if (bytes is null)
                {
                    bytes = results.AllocateArray<byte>(InputBytes.Length, ref invocation);
                    Utf8InvocationOutput.Copy(InputBytes, bytes, new Utf8InvocationBufferControl(ref invocation));
                }
                return new PythonReBenchmarkReplacementResult(string.Empty, bytes, returnedCount);
            }
            var text = Encoding.UTF8.GetString(bytes ?? InputBytes);
            return new PythonReBenchmarkReplacementResult(text, null, returnedCount);
        }
        finally { results.Release(ref invocation); }
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
                regex.Replace(InputBytes, _replacementBytes),
                null)
            : new PythonReBenchmarkReplacementResult(
                Encoding.UTF8.GetString(regex.Replace(InputBytes, _replacementBytes)),
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
                Encoding.UTF8.GetString(_pythonRegex.Replace(InputBytes, _replacementBytes)),
                null,
                null),            PythonReBenchmarkOperation.ReplaceUtf8 => new(
                string.Empty,
                _pythonRegex.Replace(InputBytes, _replacementBytes),
                null),            PythonReBenchmarkOperation.ReplaceStringLimited => new(
                Encoding.UTF8.GetString(_pythonRegex.Replace(
                    InputBytes,
                    _replacementBytes,
                    _case.Coverage.ReplacementCount)),
                null,
                null),            PythonReBenchmarkOperation.SubnString => CreateStringReplacementResult(
                PythonReSubnTextResult.Decode(_pythonRegex.Subn(InputBytes, _replacementBytes))),            PythonReBenchmarkOperation.SubnUtf8 => CreateUtf8ReplacementResult(
                _pythonRegex.Subn(InputBytes, _replacementBytes)),            _ => throw new InvalidOperationException()        };
    }

    internal PythonReBenchmarkReplacementResult ExecuteBoundedExactLiteralReplacement()
    {
        EnsureReplacementPhases();
        if (_case.Operation != PythonReBenchmarkOperation.ReplaceStringLimited ||
            _preparedExactReplacementPatternUtf8.Length == 0)
        {
            throw new InvalidOperationException(
                "The bounded exact-literal replacement replay is not available for this case.");
        }

        Utf8Validation.ThrowIfInvalidOnly(InputBytes);
        var maximumReplacementCount = _case.Coverage.ReplacementCount == 0
            ? int.MaxValue
            : _case.Coverage.ReplacementCount;
        var replacementCount = 0;
        var sourcePosition = 0;
        while (replacementCount < maximumReplacementCount)
        {
            var relativeIndex = InputBytes.AsSpan(sourcePosition).IndexOf(_preparedExactReplacementPatternUtf8);
            if (relativeIndex < 0)
            {
                break;
            }

            replacementCount++;
            sourcePosition += relativeIndex + _preparedExactReplacementPatternUtf8.Length;
        }

        if (replacementCount == 0)
        {
            return new PythonReBenchmarkReplacementResult(
                s_strictUtf8.GetString(InputBytes),
                null,
                null);
        }

        var outputLength = checked(
            InputBytes.Length +
            replacementCount *
            (_preparedLiteralReplacementUtf8.Length - _preparedExactReplacementPatternUtf8.Length));
        var output = new byte[outputLength];
        sourcePosition = 0;
        var destinationPosition = 0;
        for (var replacementIndex = 0; replacementIndex < replacementCount; replacementIndex++)
        {
            var relativeIndex = InputBytes.AsSpan(sourcePosition).IndexOf(_preparedExactReplacementPatternUtf8);
            if (relativeIndex < 0)
            {
                throw new InvalidOperationException(
                    "The bounded exact-literal replacement replay lost a preflighted match.");
            }

            InputBytes.AsSpan(sourcePosition, relativeIndex).CopyTo(output.AsSpan(destinationPosition));
            destinationPosition += relativeIndex;
            _preparedLiteralReplacementUtf8.CopyTo(output.AsSpan(destinationPosition));
            destinationPosition += _preparedLiteralReplacementUtf8.Length;
            sourcePosition += relativeIndex + _preparedExactReplacementPatternUtf8.Length;
        }

        InputBytes.AsSpan(sourcePosition).CopyTo(output.AsSpan(destinationPosition));
        return new PythonReBenchmarkReplacementResult(
            s_strictUtf8.GetString(output),
            null,
            null);
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

    internal int ExecuteBoundedExactLiteralReplacementChecksum() =>
        Checksum(ExecuteBoundedExactLiteralReplacement());

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
        var resultText = _case.Operation == PythonReBenchmarkOperation.ReplaceStringLimited
            ? _regex.Replace(subject, _preparedDotNetReplacement, _case.Coverage.ReplacementCount)
            : _regex.Replace(subject, _preparedDotNetReplacement);
        return CreateReplacementResult(resultText, replacementCount);
    }

    private PythonReBenchmarkReplacementResult ExecuteOnePassLiteralReplacement(string subject)
    {
        var builder = new StringBuilder(subject.Length);
        var replacementCount = 0;
        var maximumReplacementCount = _case.Operation == PythonReBenchmarkOperation.ReplaceStringLimited
            ? _case.Coverage.ReplacementCount
            : int.MaxValue;
        var lastIndex = 0;
        var searchIndex = 0;
        while (searchIndex <= subject.Length && replacementCount < maximumReplacementCount)
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
            if (match.Length > 0)
            {
                searchIndex = lastIndex;
                continue;
            }

            if (_replacementNonEmptyAtSamePositionRegex is not null && match.Index < subject.Length)
            {
                var nonEmptyMatch = _replacementNonEmptyAtSamePositionRegex.Match(subject, match.Index);
                if (nonEmptyMatch.Success && nonEmptyMatch.Index == match.Index && nonEmptyMatch.Length > 0)
                {
                    builder.Append(_preparedLiteralReplacement);
                    replacementCount++;
                    lastIndex = checked(nonEmptyMatch.Index + nonEmptyMatch.Length);
                    searchIndex = lastIndex;
                    continue;
                }
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = checked(
                match.Index + Rune.GetRuneAt(subject, match.Index).Utf16SequenceLength);
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
        PythonReSubnTextResult result) => new(
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

    // Host-owned composition for constant-replacement callback comparisons.
    // Regex admission/interruption covers advances; host output is outside that policy.
    private Utf8PythonSubnUtf8Result ComposePythonReReplacement(Action<PythonReHostDetailedMatch>? observe = null)
    {
        using var cursor = _pythonRegex.CreateMatchCursor(InputBytes);
        var writer = new System.Buffers.ArrayBufferWriter<byte>();
        var last = 0;
        var replaced = 0;
        while (cursor.MoveNext(out var match))
        {
            Append(InputBytes.AsSpan(last, match.Value.StartOffsetInBytes - last));
            if (observe is not null)
                observe(PythonReBenchmarkReporter.ProjectDetailed(match));
            Append(s_hostConstantReplacement(this, match));
            last = match.Value.EndOffsetInBytes;
            replaced++;
        }
        Append(InputBytes.AsSpan(last));
        return new Utf8PythonSubnUtf8Result { ResultBytes = writer.WrittenSpan.ToArray(), ReplacementCount = replaced };

        void Append(ReadOnlySpan<byte> bytes)
        {
            bytes.CopyTo(writer.GetSpan(bytes.Length));
            writer.Advance(bytes.Length);
        }
    }

    private void ObservePythonReMatch(PythonReHostDetailedMatch match)
    {
        _callbackChecksum = Combine(_callbackChecksum, Checksum(match));
        _callbackSemanticDigest = ExtendCallbackSemanticDigest(_callbackSemanticDigest, match);
    }

    private int ExecutePythonReEvaluatorString()
    {
        _callbackChecksum = 0;
        _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
        var result = PythonReSubnTextResult.Decode(ComposePythonReReplacement(ObservePythonReMatch));
        return Combine(Checksum(result), _callbackChecksum);
    }

    private int ExecutePythonReEvaluatorUtf8()
    {
        _callbackChecksum = 0;
        _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
        var result = ComposePythonReReplacement(ObservePythonReMatch);
        return Combine(Checksum(result), _callbackChecksum);
    }

    private int ExecutePythonReReplaceEvaluatorString()
    {
        _callbackChecksum = 0;
        _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
        var result = Encoding.UTF8.GetString(ComposePythonReReplacement(ObservePythonReMatch).ResultBytes);
        return Combine(Checksum(result), _callbackChecksum);
    }

    private ulong ExecutePythonReEvaluatorStringSemanticDigest()
    {
        _callbackChecksum = 0;
        _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
        var result = PythonReSubnTextResult.Decode(ComposePythonReReplacement(ObservePythonReMatch));
        return SemanticDigest(_case.Operation, result, _callbackSemanticDigest);
    }

    private ulong ExecutePythonReEvaluatorUtf8SemanticDigest()
    {
        _callbackChecksum = 0;
        _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
        var result = ComposePythonReReplacement(ObservePythonReMatch);
        return SemanticDigest(_case.Operation, result, _callbackSemanticDigest);
    }

    private ulong ExecutePythonReReplaceEvaluatorStringSemanticDigest()
    {
        _callbackChecksum = 0;
        _callbackSemanticDigest = PythonReSemanticDigestBuilder.Offset;
        var result = Encoding.UTF8.GetString(ComposePythonReReplacement(ObservePythonReMatch).ResultBytes);
        return SemanticDigest(_case.Operation, result, _callbackSemanticDigest);
    }

    private int ExecuteRegex(string input) => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => _regex.IsMatch(input) ? 1 : 0,        PythonReBenchmarkOperation.Search => Checksum(ProjectRegexValue(_regex.Match(input), input)),        PythonReBenchmarkOperation.SearchFromOffset => Checksum(ProjectRegexValue(_regex.Match(input, _startOffsetInUtf16), input)),        PythonReBenchmarkOperation.Match => Checksum(ProjectRegexValue(_regex.Match(input), input, true)),        PythonReBenchmarkOperation.FullMatch => Checksum(ProjectRegexValue(_fullRegex.Match(input), input)),        PythonReBenchmarkOperation.SearchDetailed => Checksum(MaterializeDetailed(_regex.Match(input), input, GetUtf8Offsets(input))),        PythonReBenchmarkOperation.Count => _regex.Count(input),        PythonReBenchmarkOperation.CountFromOffset => _regex.Count(input, _startOffsetInUtf16),        PythonReBenchmarkOperation.FindIterDetailed => Checksum(MaterializeFindIterDetailed(input)),        PythonReBenchmarkOperation.FindAllStructural => Checksum(MaterializeFindAllStructural(input)),        PythonReBenchmarkOperation.ReplaceString => Checksum(_regex.Replace(input, _dotNetReplacement)),        PythonReBenchmarkOperation.ReplaceUtf8 => Checksum(Encoding.UTF8.GetBytes(_regex.Replace(input, _dotNetReplacement))),        PythonReBenchmarkOperation.ReplaceStringLimited => Checksum(_regex.Replace(
            input,
            _dotNetReplacement,
            _case.Coverage.ReplacementCount)),        PythonReBenchmarkOperation.SubnString => Checksum(ReplaceAndCount(input, encodeUtf8: false, useEvaluator: false)),        PythonReBenchmarkOperation.SubnUtf8 => Checksum(ReplaceAndCount(input, encodeUtf8: true, useEvaluator: false)),        PythonReBenchmarkOperation.SubnEvaluatorString => Checksum(ReplaceAndCount(input, encodeUtf8: false, useEvaluator: true) with { CallbackChecksum = ComputeRegexCallbackChecksum(input) }),        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => Checksum(ReplaceAndCount(input, encodeUtf8: true, useEvaluator: true) with { CallbackChecksum = ComputeRegexCallbackChecksum(input) }),        PythonReBenchmarkOperation.ReplaceEvaluatorString => ChecksumRegexReplaceEvaluator(input),        PythonReBenchmarkOperation.SplitStrings => Checksum(MaterializeSplitStrings(input)),        PythonReBenchmarkOperation.SplitStringsLimited => Checksum(MaterializeSplitStrings(
            input,
            _case.Coverage.MaxSplit)),        PythonReBenchmarkOperation.SplitDetailed => Checksum(MaterializeSplitDetailed(input)),        _ => throw new InvalidOperationException()    };

    internal PythonReBenchmarkBatch MeasureRegexBatch(int iterations, bool decode) => _case.Operation switch
    {
        PythonReBenchmarkOperation.IsMatch => MeasureRegexBatch(iterations, decode, input => _regex.IsMatch(input) ? 1 : 0, result => result),        PythonReBenchmarkOperation.Search => MeasureRegexBatch(iterations, decode, input => ProjectRegexValue(_regex.Match(input), input), Checksum),        PythonReBenchmarkOperation.SearchFromOffset => MeasureRegexBatch(iterations, decode, input => ProjectRegexValue(_regex.Match(input, _startOffsetInUtf16), input), Checksum),        PythonReBenchmarkOperation.Match => MeasureRegexBatch(iterations, decode, input => ProjectRegexValue(_regex.Match(input), input, true), Checksum),        PythonReBenchmarkOperation.FullMatch => MeasureRegexBatch(iterations, decode, input => ProjectRegexValue(_fullRegex.Match(input), input), Checksum),        PythonReBenchmarkOperation.SearchDetailed => MeasureRegexBatch(iterations, decode, input => MaterializeDetailed(_regex.Match(input), input, GetUtf8Offsets(input)), Checksum),        PythonReBenchmarkOperation.Count => MeasureRegexBatch(iterations, decode, input => _regex.Count(input), result => result),        PythonReBenchmarkOperation.CountFromOffset => MeasureRegexBatch(iterations, decode, input => _regex.Count(input, _startOffsetInUtf16), result => result),        PythonReBenchmarkOperation.FindIterDetailed => MeasureRegexBatch(iterations, decode, input => MaterializeFindIterDetailed(input), Checksum),        PythonReBenchmarkOperation.FindAllStructural => MeasureRegexBatch(iterations, decode, input => MaterializeFindAllStructural(input), Checksum),        PythonReBenchmarkOperation.ReplaceString => MeasureRegexBatch(iterations, decode, input => _regex.Replace(input, _dotNetReplacement), Checksum),        PythonReBenchmarkOperation.ReplaceUtf8 => MeasureRegexBatch(iterations, decode, input => Encoding.UTF8.GetBytes(_regex.Replace(input, _dotNetReplacement)), Checksum),        PythonReBenchmarkOperation.ReplaceStringLimited => MeasureRegexBatch(iterations, decode, input => _regex.Replace(input, _dotNetReplacement, _case.Coverage.ReplacementCount), Checksum),        PythonReBenchmarkOperation.SubnString => MeasureRegexBatch(iterations, decode, input => ReplaceAndCount(input, encodeUtf8: false, useEvaluator: false), Checksum),        PythonReBenchmarkOperation.SubnUtf8 => MeasureRegexBatch(iterations, decode, input => ReplaceAndCount(input, encodeUtf8: true, useEvaluator: false), Checksum),        PythonReBenchmarkOperation.SubnEvaluatorString => MeasureRegexBatch(iterations, decode, input => ReplaceAndCount(input, encodeUtf8: false, useEvaluator: true), result => Checksum(result with { CallbackChecksum = ComputeRegexCallbackChecksum(_decoded) })),        PythonReBenchmarkOperation.SubnEvaluatorUtf8 => MeasureRegexBatch(iterations, decode, input => ReplaceAndCount(input, encodeUtf8: true, useEvaluator: true), result => Checksum(result with { CallbackChecksum = ComputeRegexCallbackChecksum(_decoded) })),        PythonReBenchmarkOperation.ReplaceEvaluatorString => MeasureRegexBatch(iterations, decode, input => _regex.Replace(input, _constantDotNetEvaluator), result => Combine(Checksum(result), ComputeRegexCallbackChecksum(_decoded))),        PythonReBenchmarkOperation.SplitStrings => MeasureRegexBatch(iterations, decode, input => MaterializeSplitStrings(input), Checksum),        PythonReBenchmarkOperation.SplitStringsLimited => MeasureRegexBatch(iterations, decode, input => MaterializeSplitStrings(input, _case.Coverage.MaxSplit), Checksum),        PythonReBenchmarkOperation.SplitDetailed => MeasureRegexBatch(iterations, decode, input => MaterializeSplitDetailed(input), Checksum),        _ => throw new InvalidOperationException()    };

    private PythonReBenchmarkBatch MeasureRegexBatch<T>(int iterations, bool decode, Func<string, T> execute, Func<T, int> checksum)
    {
        var result = default(T)!;
        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < iterations; iteration++)
            result = execute(decode ? s_strictUtf8.GetString(InputBytes) : _decoded);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return new PythonReBenchmarkBatch(elapsed, allocated, checksum(result), 0, 0);
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

    private PythonReStructuralMatch[] MaterializeFindAllStructural(string input)
    {
        var utf8Offsets = GetUtf8Offsets(input);
        var matches = new List<PythonReStructuralMatch>();
        foreach (Match match in _regex.Matches(input))
        {
            matches.Add(new PythonReStructuralMatch
            {
                Success = true,
                StartOffsetInBytes = utf8Offsets is null ? match.Index : utf8Offsets[match.Index],
                EndOffsetInBytes = utf8Offsets is null ? match.Index + match.Length : utf8Offsets[match.Index + match.Length],
                ValueText = match.Value
            });
        }

        return matches.ToArray();
    }

    internal string?[] ExecutePythonReSplitStrings() => _case.Operation switch
    {
        PythonReBenchmarkOperation.SplitStrings => MaterializePythonSplitStrings(),        PythonReBenchmarkOperation.SplitStringsLimited => MaterializePythonSplitStrings(_case.Coverage.MaxSplit),        _ => throw new InvalidOperationException()    };

    internal string?[] ExecutePredecodedSplitStrings() =>
        MaterializeSplitStrings(_decoded, GetSplitLimit());

    internal string?[] ExecuteDirectRegexSplitStrings() => GetSplitLimit() == 0
        ? _regex.Split(_decoded)
        : _regex.Split(_decoded, GetSplitLimit() + 1);

    internal string?[] ProjectPreparedSplitStrings() =>
        ProjectSplitStrings(_preparedSplitRanges);

    internal string?[] ProjectStreamingSplitStrings()
    {
        EnsureSplitPhases();
        var parts = new List<string?>();
        var lastIndex = 0;
        var splitCount = 0;
        var maxSplit = GetSplitLimit();
        foreach (var match in _regex.EnumerateMatches(_decoded))
        {
            if (maxSplit > 0 && splitCount >= maxSplit)
            {
                break;
            }

            parts.Add(_decoded[lastIndex..match.Index]);
            lastIndex = match.Index + match.Length;
            splitCount++;
        }

        parts.Add(_decoded[lastIndex..]);
        return parts.ToArray();
    }

    internal PythonReBenchmarkCaptureRange[] CollectSplitRangesArray()
    {
        EnsureSplitOperation();
        var ranges = new List<PythonReBenchmarkCaptureRange>();
        var maxSplit = GetSplitLimit();
        foreach (var match in _regex.EnumerateMatches(_decoded))
        {
            if (maxSplit > 0 && ranges.Count >= maxSplit)
            {
                break;
            }

            ranges.Add(new PythonReBenchmarkCaptureRange(true, match.Index, match.Length));
        }

        return ranges.ToArray();
    }

    internal int ExecuteSplitDiscoveryOnly()
    {
        EnsureSplitPhases();
        var checksum = 0;
        var splitCount = 0;
        var maxSplit = GetSplitLimit();
        foreach (var match in _regex.EnumerateMatches(_decoded))
        {
            if (maxSplit > 0 && splitCount >= maxSplit)
            {
                break;
            }

            checksum = Combine(checksum, match.Index);
            checksum = Combine(checksum, match.Length);
            splitCount++;
        }

        return Combine(checksum, splitCount);
    }

    internal int ExecutePythonReSplitChecksum() => Checksum(ExecutePythonReSplitStrings());

    internal int ExecutePredecodedSplitChecksum() => Checksum(ExecutePredecodedSplitStrings());

    internal int ExecuteDirectRegexSplitChecksum() => Checksum(ExecuteDirectRegexSplitStrings());

    internal int ExecutePreparedSplitProjectionChecksum() => Checksum(ProjectPreparedSplitStrings());

    internal int ExecuteStreamingSplitProjectionChecksum() => Checksum(ProjectStreamingSplitStrings());

    private string?[] ProjectSplitStrings(ReadOnlySpan<PythonReBenchmarkCaptureRange> ranges)
    {
        EnsureSplitPhases();
        var parts = new string?[ranges.Length + 1];
        var lastIndex = 0;
        for (var index = 0; index < ranges.Length; index++)
        {
            var range = ranges[index];
            parts[index] = _decoded[lastIndex..range.StartOffsetInUtf16];
            lastIndex = range.EndOffsetInUtf16;
        }

        parts[^1] = _decoded[lastIndex..];
        return parts;
    }

    private int GetSplitLimit() => _case.Operation == PythonReBenchmarkOperation.SplitStringsLimited
        ? _case.Coverage.MaxSplit
        : 0;

    private void EnsureSplitOperation()
    {
        if (_case.Operation is not PythonReBenchmarkOperation.SplitStrings and
            not PythonReBenchmarkOperation.SplitStringsLimited)
        {
            throw new InvalidOperationException("The benchmark case is not a string Split operation.");
        }
    }

    private void EnsureSplitPhases()
    {
        if (!SupportsSplitPhases)
        {
            throw new InvalidOperationException("Split phase controls are not available for this case.");
        }
    }

    // Host materialization is timed for text-output rows on both sides. The regex
    // admits only range metadata; these benchmark-owned strings are outside its limits.
    private string?[] MaterializePythonSplitStrings(int maxSplit = 0)
    {
        var ranges = _pythonRegex.SplitRanges(InputBytes, maxSplit, 0, default);
        var values = new string?[ranges.Length];
        for (var index = 0; index < ranges.Length; index++)
        {
            var range = ranges[index];
            values[index] = range.Success
                ? Encoding.UTF8.GetString(InputBytes.AsSpan(range.StartOffsetInBytes, range.EndOffsetInBytes - range.StartOffsetInBytes)) : null;
        }
        return values;
    }

    private SplitTextItem[] MaterializePythonSplitDetailed(int maxSplit = 0)
    {
        var ranges = _pythonRegex.SplitRanges(InputBytes, maxSplit, 0, default);
        var values = new SplitTextItem[ranges.Length];
        for (var index = 0; index < ranges.Length; index++)
        {
            var range = ranges[index];
            values[index] = new SplitTextItem
            {
                ValueText = range.Success
                    ? Encoding.UTF8.GetString(InputBytes.AsSpan(range.StartOffsetInBytes, range.EndOffsetInBytes - range.StartOffsetInBytes)) : null,
                IsCapture = range.Number > 0,
                CaptureGroupNumber = range.Number
            };
        }
        return values;
    }

    private readonly struct SplitTextItem
    {
        public string? ValueText { get; init; }
        public bool IsCapture { get; init; }
        public int CaptureGroupNumber { get; init; }
    }

    private string?[] MaterializeSplitStrings(string input, int maxSplit = 0) =>
        MaterializeSplitDetailed(input, maxSplit)
            .Select(static item => item.ValueText)
            .ToArray();

    private SplitTextItem[] MaterializeSplitDetailed(string input, int maxSplit = 0)
    {
        var parts = new List<SplitTextItem>();
        var lastIndex = 0;
        var splitCount = 0;
        foreach (Match match in _regex.Matches(input))
        {
            if (maxSplit > 0 && splitCount >= maxSplit)
            {
                break;
            }

            parts.Add(new SplitTextItem
            {
                ValueText = input[lastIndex..match.Index],
                IsCapture = false,
                CaptureGroupNumber = 0
            });
            for (var groupNumber = 1; groupNumber < match.Groups.Count; groupNumber++)
            {
                var group = match.Groups[groupNumber];
                parts.Add(new SplitTextItem
                {
                    ValueText = group.Success ? group.Value : null,
                    IsCapture = true,
                    CaptureGroupNumber = groupNumber
                });
            }

            lastIndex = match.Index + match.Length;
            splitCount++;
        }

        parts.Add(new SplitTextItem
        {
            ValueText = input[lastIndex..],
            IsCapture = false,
            CaptureGroupNumber = 0
        });
        return parts.ToArray();
    }

    private BclSubnResult ReplaceAndCount(string input, bool encodeUtf8, bool useEvaluator)
    {
        var count = 0;
        var result = _regex.Replace(input, match =>
        {
            count++;
            return useEvaluator ? _case.Replacement : match.Result(_dotNetReplacement);
        });
        return new BclSubnResult(result, encodeUtf8 ? Encoding.UTF8.GetBytes(result) : null, count, null);
    }

    private int ChecksumRegexReplaceEvaluator(string input)
    {
        var result = _regex.Replace(input, _constantDotNetEvaluator);
        return Combine(Checksum(result), ComputeRegexCallbackChecksum(input));
    }

    // Verification consumers call this after timing. The measured callback does
    // not request these coordinates or compute checksums in either language.
    private int ComputeRegexCallbackChecksum(string input)
    {
        var checksum = 0;
        var utf8Offsets = GetUtf8Offsets(input);
        foreach (Match match in _regex.Matches(input))
            checksum = Combine(checksum, Checksum(MaterializeDetailed(match, input, utf8Offsets)));
        return checksum;
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
                ? new BclDetailedGroup(true, utf8Offsets is null ? group.Index : utf8Offsets[group.Index], utf8Offsets is null ? group.Index + group.Length : utf8Offsets[group.Index + group.Length], group.Value)
                : new BclDetailedGroup(false, 0, 0, string.Empty);
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
                ? new BclDetailedGroup(true, group.StartOffsetInBytes, group.EndOffsetInBytes, input[group.StartOffsetInUtf16..group.EndOffsetInUtf16])
                : new BclDetailedGroup(false, 0, 0, string.Empty);
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
            (uint)match.EndOffsetInBytes);
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
            digest.AddString(Encoding.UTF8.GetString(match.GetValueBytes()));
        }

        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        PythonReHostDetailedMatch match)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        AppendDetailedSemanticDigest(ref digest, match);
        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        PythonReHostDetailedMatch[] matches)
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
        PythonReStructuralMatch[] matches)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.Add(matches.Length);
        foreach (var match in matches)
        {
            digest.Add(match.Success);
            digest.Add(match.StartOffsetInBytes);
            digest.Add(match.EndOffsetInBytes);
            digest.AddString(match.ValueText);
        }

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
        PythonReSubnTextResult result)
    {
        var digest = new PythonReSemanticDigestBuilder(operation);
        digest.AddString(result.ResultText);
        digest.Add(result.ReplacementCount);
        return digest.Value;
    }

    private static ulong SemanticDigest(
        PythonReBenchmarkOperation operation,
        PythonReSubnTextResult result,
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
        SplitTextItem[] items)
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
        PythonReHostDetailedMatch match)
    {
        var digest = new PythonReSemanticDigestBuilder(semanticDigest);
        digest.Add(0xCA11_BACC);
        AppendDetailedSemanticDigest(ref digest, match);
        return digest.Value;
    }

    private static void AppendDetailedSemanticDigest(
        ref PythonReSemanticDigestBuilder digest,
        PythonReHostDetailedMatch match)
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
            digest.AddString(group.ValueText);
        }
    }

    private PythonReBenchmarkValueResult ProjectRegexValue(Match match, string input, bool anchored = false)
    {
        if (!match.Success || anchored && match.Index != 0) return default;
        if (input.Length == InputBytes.Length) return new(true, match.Index, match.Index + match.Length);
        var start = s_strictUtf8.GetByteCount(input.AsSpan(0, match.Index));
        return new(true, start, start + s_strictUtf8.GetByteCount(input.AsSpan(match.Index, match.Length)));
    }

    private static int Checksum(PythonReBenchmarkValueResult match) => match.Success
        ? Combine(1, match.StartOffsetInBytes, match.EndOffsetInBytes) : 0;

    private static int Checksum(Utf8PythonValueMatch match) => match.Success
        ? Combine(1, match.StartOffsetInBytes, match.EndOffsetInBytes)
        : 0;

    private static int Checksum(Match match) => match.Success
        ? Combine(1, match.Index, match.Index + match.Length)
        : 0;

    private static int Checksum(PythonReHostDetailedMatch match)
    {
        var checksum = match.Success ? 1 : 0;
        foreach (var group in match.Groups ?? [])
        {
            checksum = Combine(checksum, group.Success ? 1 : 0, group.StartOffsetInBytes, group.EndOffsetInBytes, Checksum(group.ValueText));
        }

        return checksum;
    }

    private static int Checksum(BclDetailedMatch match)
    {
        var checksum = match.Groups.Length == 0 ? 0 : 1;
        foreach (var group in match.Groups)
        {
            checksum = Combine(checksum, group.Success ? 1 : 0, group.StartOffsetInBytes, group.EndOffsetInBytes, Checksum(group.Value));
        }

        return checksum;
    }

    private static int Checksum(PythonReHostDetailedMatch[] matches)
    {
        var checksum = matches.Length;
        foreach (var match in matches)
        {
            checksum = Combine(checksum, Checksum(match));
        }

        return checksum;
    }

    private static int Checksum(PythonReStructuralMatch[] matches)
    {
        var checksum = matches.Length;
        foreach (var match in matches)
        {
            checksum = Combine(
                checksum,
                match.Success ? 1 : 0,
                match.StartOffsetInBytes,
                match.EndOffsetInBytes,
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

    private static int Checksum(PythonReSubnTextResult result) =>
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

    private static int Checksum(SplitTextItem[] items)
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
    int EndOffsetInBytes);

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
    public SortedDictionary<string, PythonReLifecycleMeasurement> Lifecycle { get; init; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, PythonReScalingFamilyMeasurement> ScalingFamilies { get; init; } = new(StringComparer.Ordinal);
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
    public string? MeasurementProtocol { get; init; }
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
    // Historical independent measurements. New catalog rows use paired timings.
    public PythonReOperationMeasurement? PythonRe { get; init; }
    public PythonReOperationMeasurement? DecodeThenRegex { get; init; }
    public PythonReOperationMeasurement? PredecodedRegex { get; init; }
    public CpythonBenchmarkMeasurement? Cpython { get; init; }
    public PythonReQualificationMeasurement? Qualification { get; set; }
}

internal sealed class PythonReQualificationMeasurement
{
    public required string Status { get; init; }
    public required string StatusReason { get; init; }
    public required string EngineEvidenceBasis { get; init; }
    public required string EngineConclusion { get; init; }
    public string EngineConclusionReason { get; init; } = "No engine-comparable evidence is available.";
    public PythonRePairedEvidence? PairedEvidence { get; init; }

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
    public int? EffectiveIterations { get; init; }
    public double[]? SampleMicroseconds { get; init; }
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
    public double[]? SampleMicroseconds { get; init; }
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

// Benchmark-host text shaping; byte-template admission ends before this decoding.
internal readonly struct PythonReSubnTextResult
{
    public string ResultText { get; init; }
    public int ReplacementCount { get; init; }
    public static PythonReSubnTextResult Decode(Utf8PythonSubnUtf8Result result) => new()
    { ResultText = Encoding.UTF8.GetString(result.ResultBytes), ReplacementCount = result.ReplacementCount };
}

// Benchmark-owned structural output, also used by the .NET comparator.
// Every successful whole match materializes its fields and text in the host.
internal readonly struct PythonReStructuralMatch
{
    public bool Success { get; init; }
    public int StartOffsetInBytes { get; init; }
    public int EndOffsetInBytes { get; init; }
    public string ValueText { get => field ?? string.Empty; init; }
}
