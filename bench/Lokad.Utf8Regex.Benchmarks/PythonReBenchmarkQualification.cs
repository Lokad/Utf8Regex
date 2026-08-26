using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const int PythonReQualificationProtocolVersion = 2;
    private const int PythonReQualificationBootstrapSeed = 31302;
    private const int PythonReQualificationBootstrapResamples = 10_000;
    private const int PythonReQualificationMaximumIterations = 10_000_000;
    private const double PythonReQualificationTargetSampleMilliseconds = 40;
    private const double PythonReQualificationPilotMilliseconds = 5;
    private const double PythonReQualificationMinimumSampleMilliseconds = 20;
    private const double PythonReQualificationMaximumSpread = 1.10;
    private static readonly TimeSpan s_cpythonResponseTimeout = TimeSpan.FromSeconds(10);

    private static int MeasurePairedCase(
        string caseId,
        int samples,
        bool cpythonFirst,
        PythonReQualificationWriteMode writeMode)
    {
#if DEBUG
        Console.Error.WriteLine("PythonRe paired measurement requires a Release build.");
        return 1;
#else
        if (samples < 9)
        {
            Console.Error.WriteLine("PythonRe paired measurement requires at least nine samples.");
            return 1;
        }

        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(caseId, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case '{caseId}'.");
            return 1;
        }

        var worktreeState = RunGit(
            "status",
            "--porcelain=v1",
            "--untracked-files=all",
            "--",
            ".",
            ":(exclude)PythonRe.Benchmarks.json",
            ":(exclude)src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md",
            ":(exclude)UTF8REGEX-PERFORMANCE-ROADMAP.md");
        if (worktreeState is null)
        {
            Console.Error.WriteLine("Could not verify the worktree before PythonRe paired measurement.");
            return 1;
        }

        var worktreeQualified = string.IsNullOrWhiteSpace(worktreeState);
        if (writeMode == PythonReQualificationWriteMode.Snapshot && !worktreeQualified)
        {
            Console.Error.WriteLine("PythonRe qualification requires a clean source worktree.");
            Console.Error.WriteLine(worktreeState);
            return 1;
        }

        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        using var currentProcess = Process.GetCurrentProcess();
        var context = new PythonReBenchmarkContext(benchmarkCase);
        var expectedChecksum = context.ExecutePythonRe();
        var expectedSemanticDigest = context.ExecutePythonReSemanticDigest();
        using var worker = new CpythonStreamWorker();
        var prepared = worker.Prepare(benchmarkCase, context.InputBytes);
        if (prepared.Checksum != expectedChecksum || prepared.SemanticDigest != expectedSemanticDigest)
        {
            throw new InvalidOperationException(
                $"PythonRe benchmark '{caseId}' disagrees with CPython streaming preflight: " +
                $"checksum={expectedChecksum}/{prepared.Checksum}, " +
                $"semantic digest={expectedSemanticDigest:X16}/{prepared.SemanticDigest:X16}.");
        }

        for (var warmupCall = 0; warmupCall < 64; warmupCall++)
        {
            var batch = context.MeasurePythonReBatch(1);
            VerifyManagedResult(batch, expectedChecksum, expectedSemanticDigest, "tiering warmup");
        }

        var preliminaryManagedIterations = CalibrateManagedBatch(
            context,
            expectedChecksum,
            expectedSemanticDigest);
        var preliminaryCpythonCalibration = worker.Calibrate(
            CpythonStreamLane.Predecoded,
            PythonReQualificationTargetSampleMilliseconds,
            PythonReQualificationMaximumIterations);
        var managedWarmup = WarmManagedLane(
            context,
            preliminaryManagedIterations,
            expectedChecksum,
            expectedSemanticDigest);
        var cpythonWarmup = worker.Warm(
            CpythonStreamLane.Predecoded,
            preliminaryCpythonCalibration.Iterations,
            minimumMilliseconds: 100,
            maximumBatches: 32);
        var managedIterations = CalibrateManagedBatch(context, expectedChecksum, expectedSemanticDigest);
        var cpythonCalibration = worker.Calibrate(
            CpythonStreamLane.Predecoded,
            PythonReQualificationTargetSampleMilliseconds,
            PythonReQualificationMaximumIterations);
        var cpythonIterations = cpythonCalibration.Iterations;

        var laneOrders = new List<PythonRePairLaneOrder>(samples);
        var managedMicroseconds = new List<double>(samples);
        var cpythonMicroseconds = new List<double>(samples);
        var managedMilliseconds = new List<double>(samples);
        var cpythonMilliseconds = new List<double>(samples);
        var managedProcessCpuMilliseconds = new List<double>(samples);
        var cpythonProcessCpuMilliseconds = new List<double>(samples);
        var managedGcCollections = new List<int[]>(samples);
        var cpythonGcCollections = new List<int[]>(samples);
        var managedAllocatedBytes = new List<long>(samples);
        var ratios = new List<double>(samples);

        for (var sample = 0; sample < samples; sample++)
        {
            var order = (sample + (cpythonFirst ? 1 : 0)) % 2 == 0
                ? PythonRePairLaneOrder.ManagedFirst
                : PythonRePairLaneOrder.CpythonFirst;
            PythonReManagedSample managed;
            CpythonStreamResponse cpython;
            if (order == PythonRePairLaneOrder.ManagedFirst)
            {
                managed = MeasureManagedSample(
                    context,
                    managedIterations,
                    expectedChecksum,
                    expectedSemanticDigest,
                    currentProcess);
                cpython = worker.Measure(CpythonStreamLane.Predecoded, cpythonIterations);
            }
            else
            {
                cpython = worker.Measure(CpythonStreamLane.Predecoded, cpythonIterations);
                managed = MeasureManagedSample(
                    context,
                    managedIterations,
                    expectedChecksum,
                    expectedSemanticDigest,
                    currentProcess);
            }

            var managedPerOperation = managed.Batch.Elapsed.TotalMicroseconds / managedIterations;
            var cpythonPerOperation = cpython.ElapsedNanoseconds / (double)cpythonIterations / 1_000;
            laneOrders.Add(order);
            managedMicroseconds.Add(managedPerOperation);
            cpythonMicroseconds.Add(cpythonPerOperation);
            managedMilliseconds.Add(managed.Batch.Elapsed.TotalMilliseconds);
            cpythonMilliseconds.Add(cpython.ElapsedNanoseconds / 1_000_000d);
            managedProcessCpuMilliseconds.Add(managed.ProcessCpu.TotalMilliseconds);
            cpythonProcessCpuMilliseconds.Add(cpython.ProcessCpuNanoseconds / 1_000_000d);
            managedGcCollections.Add(managed.GcCollections);
            cpythonGcCollections.Add(cpython.GcCollections);
            managedAllocatedBytes.Add(managed.Batch.AllocatedBytes / managedIterations);
            ratios.Add(managedPerOperation / cpythonPerOperation);
        }

        const int floorSamples = 3;
        var managedFloorMicroseconds = new List<double>(floorSamples);
        var cpythonFloorMicroseconds = new List<double>(floorSamples);
        for (var sample = 0; sample < floorSamples; sample++)
        {
            var managedFloor = MeasureManagedEmptyLoop(managedIterations);
            var cpythonFloor = worker.Measure(CpythonStreamLane.EmptyLoop, cpythonIterations);
            managedFloorMicroseconds.Add(managedFloor.Elapsed.TotalMicroseconds / managedIterations);
            cpythonFloorMicroseconds.Add(
                cpythonFloor.ElapsedNanoseconds / (double)cpythonIterations / 1_000);
            s_sink ^= managedFloor.Checksum ^ cpythonFloor.Checksum;
        }

        var logRatios = ratios.Select(static ratio => Math.Log(ratio)).ToArray();
        var interval = BenchmarkPairedStatistics.BootstrapMedianLogRatio(
            logRatios,
            PythonReQualificationBootstrapSeed,
            PythonReQualificationBootstrapResamples);
        var ratioMedian = Math.Exp(BenchmarkPairedStatistics.Median(logRatios));
        var ratioLower = Math.Exp(interval.Lower);
        var ratioUpper = Math.Exp(interval.Upper);
        var managedMedian = BenchmarkPairedStatistics.Median(managedMicroseconds);
        var cpythonMedian = BenchmarkPairedStatistics.Median(cpythonMicroseconds);
        var managedFloorMedian = BenchmarkPairedStatistics.Median(managedFloorMicroseconds);
        var cpythonFloorMedian = BenchmarkPairedStatistics.Median(cpythonFloorMicroseconds);
        var managedFloorFraction = managedFloorMedian / managedMedian;
        var cpythonFloorFraction = cpythonFloorMedian / cpythonMedian;
        var managedFirstRatios = laneOrders
            .Select((order, index) => new PythonReOrderedRatio(order, ratios[index]))
            .Where(static sample => sample.Order == PythonRePairLaneOrder.ManagedFirst)
            .Select(static sample => sample.Ratio)
            .ToArray();
        var cpythonFirstRatios = laneOrders
            .Select((order, index) => new PythonReOrderedRatio(order, ratios[index]))
            .Where(static sample => sample.Order == PythonRePairLaneOrder.CpythonFirst)
            .Select(static sample => sample.Ratio)
            .ToArray();
        var orderEffect = BenchmarkPairedStatistics.Median(managedFirstRatios) /
            BenchmarkPairedStatistics.Median(cpythonFirstRatios);
        var managedSpread = BenchmarkPairedStatistics.InterquartileSpread(managedMicroseconds);
        var cpythonSpread = BenchmarkPairedStatistics.InterquartileSpread(cpythonMicroseconds);
        var durationsQualified = managedMilliseconds.All(
                                     static duration => duration >= PythonReQualificationMinimumSampleMilliseconds) &&
                                 cpythonMilliseconds.All(
                                     static duration => duration >= PythonReQualificationMinimumSampleMilliseconds);
        var placementQualified = processorScope.Policy == "single-highest-efficiency-processor";
        var status = DeriveStatus(
            worktreeQualified,
            placementQualified,
            durationsQualified,
            structuredDigestQualified: true,
            ratioLower,
            ratioUpper,
            orderEffect,
            managedSpread,
            cpythonSpread,
            managedFloorFraction,
            cpythonFloorFraction);
        var measuredAtUtc = DateTimeOffset.UtcNow;
        var managedEnvironment = CaptureEnvironment();
        var caseDefinitionSha256 = ComputePythonReCaseDefinitionSha256(benchmarkCase, context.InputBytes);
        var catalogSha256 = ComputePythonReCatalogSha256();
        var pairedEvidence = new PythonRePairedEvidence
        {
            ProtocolVersion = PythonReQualificationProtocolVersion,
            QualificationId = ComputePythonReQualificationId(
                caseDefinitionSha256,
                catalogSha256,
                managedEnvironment.SourceCommit,
                worker.Environment,
                processorScope.Policy,
                processorScope.AffinityMask,
                processorScope.EfficiencyClass,
                cpythonFirst,
                samples),
            MeasuredAtUtc = measuredAtUtc,
            SourceCommit = managedEnvironment.SourceCommit,
            Baseline = "CPythonPredecodedElapsed",
            InitialLane = cpythonFirst ? "CPython" : "PythonRe",
            WorktreeQualified = worktreeQualified,
            CaseDefinitionSha256 = caseDefinitionSha256,
            CatalogSha256 = catalogSha256,
            SemanticDigestAlgorithm = "structured-u64-mix-v1",
            SemanticDigest = expectedSemanticDigest.ToString("X16", System.Globalization.CultureInfo.InvariantCulture),
            CpuPolicy = processorScope.Policy,
            CpuAffinityMask = processorScope.AffinityMask,
            CpuEfficiencyClass = processorScope.EfficiencyClass,
            ManagedIterations = managedIterations,
            CpythonIterations = cpythonIterations,
            ManagedWarmupCalls = managedWarmup.Iterations,
            ManagedWarmupMilliseconds = managedWarmup.Elapsed.TotalMilliseconds,
            CpythonWarmupCalls = cpythonWarmup.Iterations,
            CpythonWarmupMilliseconds = cpythonWarmup.ElapsedNanoseconds / 1_000_000d,
            ManagedMedianMicroseconds = managedMedian,
            CpythonMedianMicroseconds = cpythonMedian,
            StrongRatioMedian = ratioMedian,
            StrongRatioLower95 = ratioLower,
            StrongRatioUpper95 = ratioUpper,
            StrongDifferenceMicroseconds = managedMedian - cpythonMedian,
            OrderEffect = orderEffect,
            ManagedInterquartileSpread = managedSpread,
            CpythonInterquartileSpread = cpythonSpread,
            ManagedHarnessFloorFraction = managedFloorFraction,
            CpythonHarnessFloorFraction = cpythonFloorFraction,
            ManagedMedianAllocatedBytes = checked((long)Math.Round(
                BenchmarkPairedStatistics.Median(managedAllocatedBytes.Select(static value => (double)value)))),
            Samples = Enumerable.Range(0, samples)
                .Select(index => new PythonRePairedSampleEvidence
                {
                    Order = laneOrders[index].ToString(),
                    ManagedMicroseconds = managedMicroseconds[index],
                    CpythonMicroseconds = cpythonMicroseconds[index],
                    StrongRatio = ratios[index],
                    ManagedElapsedMilliseconds = managedMilliseconds[index],
                    CpythonElapsedMilliseconds = cpythonMilliseconds[index],
                    ManagedProcessCpuMilliseconds = managedProcessCpuMilliseconds[index],
                    CpythonProcessCpuMilliseconds = cpythonProcessCpuMilliseconds[index],
                    ManagedGcCollections = managedGcCollections[index],
                    CpythonGcCollections = cpythonGcCollections[index],
                    ManagedAllocatedBytes = managedAllocatedBytes[index],
                })
                .ToArray(),
            ManagedEmptyLoopMicroseconds = managedFloorMicroseconds.ToArray(),
            CpythonEmptyLoopMicroseconds = cpythonFloorMicroseconds.ToArray(),
            CpythonEnvironment = worker.Environment,
            ManagedEnvironment = managedEnvironment,
        };
        var qualification = new PythonReQualificationMeasurement
        {
            Status = status.Status.ToString(),
            StatusReason = status.Reason ?? "Qualified paired evidence.",
            EngineEvidenceBasis = "Not engine-comparable",
            EngineConclusion = "Unqualified",
            PairedEvidence = pairedEvidence,
        };

        Console.WriteLine($"CaseId             : {caseId}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine(
            $"Protocol           : streaming v{PythonReQualificationProtocolVersion}; " +
            "alternating paired samples");
        Console.WriteLine($"CPU placement      : {processorScope.Description}");
        Console.WriteLine($"Worktree qualified : {worktreeQualified}");
        Console.WriteLine($"Initial lane       : {(cpythonFirst ? "CPython" : "PythonRe")}");
        Console.WriteLine($"Samples            : {samples}");
        Console.WriteLine($"Iterations         : managed={managedIterations}; CPython={cpythonIterations}");
        Console.WriteLine(
            $"Warmup             : managed={managedWarmup.Iterations} calls/" +
            $"{managedWarmup.Elapsed.TotalMilliseconds:F1} ms; " +
            $"CPython={cpythonWarmup.Iterations} calls/" +
            $"{cpythonWarmup.ElapsedNanoseconds / 1_000_000d:F1} ms");
        Console.WriteLine($"PythonRe elapsed   : {managedMedian:F3} us/op");
        Console.WriteLine($"CPython elapsed    : {cpythonMedian:F3} us/op");
        Console.WriteLine($"Rstrong            : {ratioMedian:F3} [{ratioLower:F3}, {ratioUpper:F3}]");
        Console.WriteLine($"Estrong            : {managedMedian - cpythonMedian:+0.000;-0.000;0.000} us/op");
        Console.WriteLine($"Order effect       : {orderEffect:F3}");
        Console.WriteLine($"IQR spread         : managed={managedSpread:F3}; CPython={cpythonSpread:F3}");
        Console.WriteLine($"Harness floor      : managed={managedFloorFraction:P2}; CPython={cpythonFloorFraction:P2}");
        Console.WriteLine(
            $"Managed alloc      : " +
            $"{BenchmarkPairedStatistics.Median(managedAllocatedBytes.Select(static value => (double)value)):F0} B/op");
        Console.WriteLine(
            $"Process CPU/sample : managed=" +
            $"{BenchmarkPairedStatistics.Median(managedProcessCpuMilliseconds):F1} ms; " +
            $"CPython={BenchmarkPairedStatistics.Median(cpythonProcessCpuMilliseconds):F1} ms (diagnostic)");
        Console.WriteLine($"Status             : {FormatStatus(status.Status)}");
        Console.WriteLine($"Status reason      : {status.Reason ?? "qualified paired evidence"}");
        Console.WriteLine($"CPython            : {worker.Environment.Implementation} " +
                          $"{worker.Environment.Version}; {worker.Environment.ExecutableSha256}");
        Console.WriteLine("Raw pairs:");
        for (var sample = 0; sample < samples; sample++)
        {
            Console.WriteLine(
                $"  {sample + 1,2}: {laneOrders[sample],12}; " +
                $"managed={managedMicroseconds[sample],10:F3} us; " +
                $"CPython={cpythonMicroseconds[sample],10:F3} us; R={ratios[sample]:F3}; " +
                $"GC={FormatGcCollections(managedGcCollections[sample])}/" +
                $"{FormatGcCollections(cpythonGcCollections[sample])}");
        }

        if (writeMode == PythonReQualificationWriteMode.Snapshot)
        {
            PersistPythonReQualification(caseId, benchmarkCase, context.InputBytes.Length, qualification, measuredAtUtc);
            Console.WriteLine($"Snapshot           : {Path.GetFullPath(SnapshotFileName)}");
        }

        return 0;

        static string FormatGcCollections(IReadOnlyList<int> collections) =>
            collections.Count == 3
                ? $"{collections[0]},{collections[1]},{collections[2]}"
                : "unavailable";
#endif
    }

    private static int CalibrateManagedBatch(
        PythonReBenchmarkContext context,
        int expectedChecksum,
        ulong expectedSemanticDigest)
    {
        var iterations = 1;
        var pilot = context.MeasurePythonReBatch(iterations);
        VerifyManagedResult(pilot, expectedChecksum, expectedSemanticDigest, "calibration");
        while (pilot.Elapsed.TotalMilliseconds < PythonReQualificationPilotMilliseconds &&
               iterations < PythonReQualificationMaximumIterations)
        {
            var elapsed = Math.Max(pilot.Elapsed.TotalMilliseconds, 0.000_001);
            var growth = Math.Max(2, (int)Math.Ceiling(PythonReQualificationPilotMilliseconds / elapsed));
            iterations = (int)Math.Min(
                PythonReQualificationMaximumIterations,
                (long)iterations * growth);
            pilot = context.MeasurePythonReBatch(iterations);
            VerifyManagedResult(pilot, expectedChecksum, expectedSemanticDigest, "calibration");
        }

        iterations = (int)Math.Clamp(
            Math.Round(iterations * PythonReQualificationTargetSampleMilliseconds /
                       Math.Max(pilot.Elapsed.TotalMilliseconds, 0.000_001)),
            1,
            PythonReQualificationMaximumIterations);
        const int confirmationAttempts = 2;
        for (var attempt = 0; attempt < confirmationAttempts; attempt++)
        {
            var confirmation = context.MeasurePythonReBatch(iterations);
            VerifyManagedResult(
                confirmation,
                expectedChecksum,
                expectedSemanticDigest,
                "calibration confirmation");
            if (confirmation.Elapsed.TotalMilliseconds is >= 30 and <= 50)
            {
                break;
            }

            iterations = (int)Math.Clamp(
                Math.Round(
                    iterations * PythonReQualificationTargetSampleMilliseconds /
                    Math.Max(confirmation.Elapsed.TotalMilliseconds, 0.000_001)),
                1,
                PythonReQualificationMaximumIterations);
        }

        return iterations;
    }

    private static int VerifyPythonReSemanticDigests()
    {
        var supplementaryCase = new PythonReBenchmarkCase(
            "supplementary/search-detailed",
            "(é+)-(𝒜𝒜|𝒜)",
            PythonReCompileOptions.None,
            PythonReBenchmarkOperation.SearchDetailed,
            "préfixe éé-𝒜𝒜 suffixe",
            string.Empty,
            IncludesResultMaterialization: true);
        var cases = PythonReBenchmarkCatalog.Cases.Append(supplementaryCase).ToArray();
        using var worker = new CpythonStreamWorker();
        foreach (var benchmarkCase in cases)
        {
            var context = new PythonReBenchmarkContext(benchmarkCase);
            var expectedChecksum = context.ExecutePythonRe();
            var expectedSemanticDigest = context.ExecutePythonReSemanticDigest();
            var prepared = worker.Prepare(benchmarkCase, context.InputBytes);
            if (prepared.Checksum != expectedChecksum || prepared.SemanticDigest != expectedSemanticDigest)
            {
                Console.Error.WriteLine(
                    $"{benchmarkCase.Id}: checksum={expectedChecksum}/{prepared.Checksum}; " +
                    $"semantic digest={expectedSemanticDigest:X16}/{prepared.SemanticDigest:X16}");
                return 1;
            }
        }

        Console.WriteLine(
            $"Verified structured semantic digests for {cases.Length} PythonRe cases, " +
            "including supplementary-plane detailed coordinates.");
        return 0;
    }

    private static int VerifyPythonReQualifications()
    {
        var snapshot = LoadPythonReBenchmarkSnapshot();
        var currentCatalogSha256 = ComputePythonReCatalogSha256();
        var currentRunnerSha256 = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(FindRepositoryFile(CpythonRunnerRelativePath))));
        var verified = 0;
        foreach (var (caseId, measurement) in snapshot.Cases)
        {
            var qualification = measurement.Qualification;
            if (qualification is null)
            {
                Console.Error.WriteLine($"{caseId}: schema-4 row has no qualification state.");
                return 1;
            }

            var evidence = qualification.PairedEvidence;
            if (evidence is null)
            {
                if (!qualification.Status.Equals("Unqualified", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"{caseId}: a row without paired evidence must be Unqualified.");
                    return 1;
                }

                continue;
            }

            var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
                candidate => candidate.Id.Equals(caseId, StringComparison.Ordinal));
            if (benchmarkCase is null)
            {
                Console.Error.WriteLine($"{caseId}: paired evidence has no current catalog case.");
                return 1;
            }

            var inputBytes = Encoding.UTF8.GetBytes(benchmarkCase.Input);
            if (!evidence.CaseDefinitionSha256.Equals(
                    ComputePythonReCaseDefinitionSha256(benchmarkCase, inputBytes),
                    StringComparison.Ordinal) ||
                !evidence.CatalogSha256.Equals(currentCatalogSha256, StringComparison.Ordinal) ||
                !evidence.CpythonEnvironment.RunnerSha256.Equals(currentRunnerSha256, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"{caseId}: catalog, case, or runner fingerprint is stale.");
                return 1;
            }

            var managedSourceChanges = RunGit(
                "diff",
                "--name-only",
                evidence.SourceCommit,
                "HEAD",
                "--",
                "Directory.Build.props",
                "global.json",
                "src/Lokad.Utf8Regex",
                "src/Lokad.Utf8Regex.PythonRe",
                "bench/Lokad.Utf8Regex.Benchmarks",
                ":(exclude)src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md");
            if (managedSourceChanges is null || !string.IsNullOrWhiteSpace(managedSourceChanges))
            {
                Console.Error.WriteLine($"{caseId}: managed source differs from measured commit {evidence.SourceCommit}.");
                if (!string.IsNullOrWhiteSpace(managedSourceChanges))
                {
                    Console.Error.WriteLine(managedSourceChanges);
                }
                return 1;
            }

            if (!VerifyPythonReInterpreterFile(
                    evidence.CpythonEnvironment.Executable,
                    evidence.CpythonEnvironment.ExecutableSha256) ||
                !VerifyPythonReInterpreterFile(
                    evidence.CpythonEnvironment.RuntimeLibrary,
                    evidence.CpythonEnvironment.RuntimeLibrarySha256))
            {
                Console.Error.WriteLine($"{caseId}: CPython executable or runtime-library fingerprint is stale.");
                return 1;
            }

            if (evidence.ProtocolVersion != PythonReQualificationProtocolVersion ||
                !evidence.SemanticDigestAlgorithm.Equals("structured-u64-mix-v1", StringComparison.Ordinal) ||
                evidence.Samples.Length is not 9 and not 17 ||
                !evidence.WorktreeQualified)
            {
                Console.Error.WriteLine($"{caseId}: paired protocol metadata is not qualification-compatible.");
                return 1;
            }

            var managedMicroseconds = evidence.Samples.Select(static sample => sample.ManagedMicroseconds).ToArray();
            var cpythonMicroseconds = evidence.Samples.Select(static sample => sample.CpythonMicroseconds).ToArray();
            var ratios = evidence.Samples.Select(static sample => sample.StrongRatio).ToArray();
            for (var index = 0; index < evidence.Samples.Length; index++)
            {
                VerifyPythonReStatistic(
                    $"{caseId} sample {index + 1} ratio",
                    managedMicroseconds[index] / cpythonMicroseconds[index],
                    ratios[index]);
            }

            var logRatios = ratios.Select(static ratio => Math.Log(ratio)).ToArray();
            var interval = BenchmarkPairedStatistics.BootstrapMedianLogRatio(
                logRatios,
                PythonReQualificationBootstrapSeed,
                PythonReQualificationBootstrapResamples);
            var managedFirstRatios = evidence.Samples
                .Where(static sample => sample.Order.Equals("ManagedFirst", StringComparison.Ordinal))
                .Select(static sample => sample.StrongRatio)
                .ToArray();
            var cpythonFirstRatios = evidence.Samples
                .Where(static sample => sample.Order.Equals("CpythonFirst", StringComparison.Ordinal))
                .Select(static sample => sample.StrongRatio)
                .ToArray();
            var managedFloor = BenchmarkPairedStatistics.Median(evidence.ManagedEmptyLoopMicroseconds);
            var cpythonFloor = BenchmarkPairedStatistics.Median(evidence.CpythonEmptyLoopMicroseconds);
            var managedMedian = BenchmarkPairedStatistics.Median(managedMicroseconds);
            var cpythonMedian = BenchmarkPairedStatistics.Median(cpythonMicroseconds);
            var ratioLower = Math.Exp(interval.Lower);
            var ratioUpper = Math.Exp(interval.Upper);
            var orderEffect = BenchmarkPairedStatistics.Median(managedFirstRatios) /
                BenchmarkPairedStatistics.Median(cpythonFirstRatios);
            var managedSpread = BenchmarkPairedStatistics.InterquartileSpread(managedMicroseconds);
            var cpythonSpread = BenchmarkPairedStatistics.InterquartileSpread(cpythonMicroseconds);
            var managedFloorFraction = managedFloor / managedMedian;
            var cpythonFloorFraction = cpythonFloor / cpythonMedian;
            VerifyPythonReStatistic(caseId + " managed median", managedMedian, evidence.ManagedMedianMicroseconds);
            VerifyPythonReStatistic(caseId + " CPython median", cpythonMedian, evidence.CpythonMedianMicroseconds);
            VerifyPythonReStatistic(
                caseId + " ratio median",
                Math.Exp(BenchmarkPairedStatistics.Median(logRatios)),
                evidence.StrongRatioMedian);
            VerifyPythonReStatistic(caseId + " interval lower", ratioLower, evidence.StrongRatioLower95);
            VerifyPythonReStatistic(caseId + " interval upper", ratioUpper, evidence.StrongRatioUpper95);
            VerifyPythonReStatistic(caseId + " order effect", orderEffect, evidence.OrderEffect);
            VerifyPythonReStatistic(caseId + " managed spread", managedSpread, evidence.ManagedInterquartileSpread);
            VerifyPythonReStatistic(caseId + " CPython spread", cpythonSpread, evidence.CpythonInterquartileSpread);
            VerifyPythonReStatistic(
                caseId + " managed floor",
                managedFloorFraction,
                evidence.ManagedHarnessFloorFraction);
            VerifyPythonReStatistic(
                caseId + " CPython floor",
                cpythonFloorFraction,
                evidence.CpythonHarnessFloorFraction);

            var durationsQualified = evidence.Samples.All(sample =>
                sample.ManagedElapsedMilliseconds >= PythonReQualificationMinimumSampleMilliseconds &&
                sample.CpythonElapsedMilliseconds >= PythonReQualificationMinimumSampleMilliseconds);
            var expectedStatus = DeriveStatus(
                evidence.WorktreeQualified,
                evidence.CpuPolicy.Equals("single-highest-efficiency-processor", StringComparison.Ordinal),
                durationsQualified,
                structuredDigestQualified: true,
                ratioLower,
                ratioUpper,
                orderEffect,
                managedSpread,
                cpythonSpread,
                managedFloorFraction,
                cpythonFloorFraction);
            if (!qualification.Status.Equals(expectedStatus.Status.ToString(), StringComparison.Ordinal) ||
                !qualification.StatusReason.Equals(
                    expectedStatus.Reason ?? "Qualified paired evidence.",
                    StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"{caseId}: stored Status does not match paired evidence.");
                return 1;
            }

            var expectedQualificationId = ComputePythonReQualificationId(
                evidence.CaseDefinitionSha256,
                evidence.CatalogSha256,
                evidence.SourceCommit,
                evidence.CpythonEnvironment,
                evidence.CpuPolicy,
                evidence.CpuAffinityMask,
                evidence.CpuEfficiencyClass,
                evidence.InitialLane.Equals("CPython", StringComparison.Ordinal),
                evidence.Samples.Length);
            if (!evidence.QualificationId.Equals(expectedQualificationId, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"{caseId}: QualificationId does not match its evidence identity.");
                return 1;
            }

            verified++;
        }

        Console.WriteLine(
            $"Verified {verified} paired PythonRe qualifications; " +
            $"{snapshot.Cases.Count - verified} rows remain explicitly Unqualified.");
        return 0;
    }

    private static bool VerifyPythonReInterpreterFile(string path, string expectedSha256)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(expectedSha256) || !File.Exists(path))
        {
            return false;
        }

        var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        return actual.Equals(expectedSha256, StringComparison.Ordinal);
    }

    private static void VerifyPythonReStatistic(string name, double expected, double actual)
    {
        var scale = Math.Max(1, Math.Max(Math.Abs(expected), Math.Abs(actual)));
        if (Math.Abs(expected - actual) > scale * 1e-12)
        {
            throw new InvalidOperationException(
                $"PythonRe paired statistic '{name}' differs: expected {expected:R}, actual {actual:R}.");
        }
    }

    private static PythonReWarmup WarmManagedLane(
        PythonReBenchmarkContext context,
        int iterations,
        int expectedChecksum,
        ulong expectedSemanticDigest)
    {
        const int maximumBatches = 32;
        var started = Stopwatch.GetTimestamp();
        var batches = 0;
        do
        {
            var batch = context.MeasurePythonReBatch(iterations);
            VerifyManagedResult(batch, expectedChecksum, expectedSemanticDigest, "warmup");
            s_sink ^= batch.Checksum;
            batches++;
        }
        while (batches < maximumBatches && Stopwatch.GetElapsedTime(started).TotalMilliseconds < 100);

        return new PythonReWarmup(
            batches * iterations,
            Stopwatch.GetElapsedTime(started));
    }

    private static PythonReManagedSample MeasureManagedSample(
        PythonReBenchmarkContext context,
        int iterations,
        int expectedChecksum,
        ulong expectedSemanticDigest,
        Process process)
    {
        var processCpuBefore = process.TotalProcessorTime;
        var collectionsBefore = new[]
        {
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
        };
        var batch = context.MeasurePythonReBatch(iterations);
        var processCpu = process.TotalProcessorTime - processCpuBefore;
        var collectionsAfter = new[]
        {
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
        };
        VerifyManagedResult(batch, expectedChecksum, expectedSemanticDigest, "paired sample");
        s_sink ^= batch.Checksum;
        return new PythonReManagedSample(
            batch,
            processCpu,
            [
                collectionsAfter[0] - collectionsBefore[0],
                collectionsAfter[1] - collectionsBefore[1],
                collectionsAfter[2] - collectionsBefore[2],
            ]);
    }

    private static PythonReEmptyBatch MeasureManagedEmptyLoop(int iterations)
    {
        var checksum = 0;
        var started = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            checksum ^= iteration;
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        GC.KeepAlive(checksum);
        return new PythonReEmptyBatch(elapsed, checksum);
    }

    private static void VerifyManagedResult(
        PythonReBenchmarkBatch batch,
        int expectedChecksum,
        ulong expectedSemanticDigest,
        string phase)
    {
        if (batch.Checksum != expectedChecksum)
        {
            throw new InvalidOperationException(
                $"PythonRe {phase} checksum {batch.Checksum} does not match preflight {expectedChecksum}.");
        }

        if (batch.SemanticDigest != expectedSemanticDigest)
        {
            throw new InvalidOperationException(
                $"PythonRe {phase} semantic digest {batch.SemanticDigest:X16} " +
                $"does not match preflight {expectedSemanticDigest:X16}.");
        }
    }

    private static PythonReStatusResult DeriveStatus(
        bool worktreeQualified,
        bool placementQualified,
        bool durationsQualified,
        bool structuredDigestQualified,
        double lowerRatio,
        double upperRatio,
        double orderEffect,
        double managedSpread,
        double cpythonSpread,
        double managedFloorFraction,
        double cpythonFloorFraction)
    {
        if (!worktreeQualified)
        {
            return new(PythonRePublicStatus.Unqualified, "The source worktree is not clean.");
        }

        if (!placementQualified)
        {
            return new(
                PythonRePublicStatus.Unqualified,
                "A single common highest-efficiency processor was unavailable.");
        }

        if (!durationsQualified)
        {
            return new(
                PythonRePublicStatus.Unqualified,
                $"At least one lane sample was shorter than {PythonReQualificationMinimumSampleMilliseconds:F0} ms.");
        }

        if (!structuredDigestQualified)
        {
            return new(
                PythonRePublicStatus.Unqualified,
                "The paired protocol does not yet use the required structured 64-bit semantic digest.");
        }

        if (managedSpread > PythonReQualificationMaximumSpread ||
            cpythonSpread > PythonReQualificationMaximumSpread)
        {
            return new(
                PythonRePublicStatus.Inconclusive,
                $"Lane interquartile spreads are {managedSpread:F3}/{cpythonSpread:F3}; " +
                $"the maximum is {PythonReQualificationMaximumSpread:F2}.");
        }

        if (orderEffect is < 0.98 or > 1.02)
        {
            return new(
                PythonRePublicStatus.Inconclusive,
                $"Lane-order median ratios differ by {Math.Abs(orderEffect - 1) * 100:F2}%.");
        }

        var floorContaminated = managedFloorFraction > 0.05 || cpythonFloorFraction > 0.05;
        if (floorContaminated)
        {
            var sensitiveLower = lowerRatio * Math.Max(0, 1 - managedFloorFraction);
            var sensitiveUpper = upperRatio / Math.Max(0.001, 1 - cpythonFloorFraction);
            if (sensitiveUpper < 0.98)
            {
                return new(
                    PythonRePublicStatus.ManagedFaster,
                    "The conclusion survives the conservative harness-floor bound.");
            }

            if (sensitiveLower > 1.02)
            {
                return new(
                    PythonRePublicStatus.CpythonFaster,
                    "The conclusion survives the conservative harness-floor bound.");
            }

            return new(
                PythonRePublicStatus.Unqualified,
                $"Removable harness floors are {managedFloorFraction:P2}/{cpythonFloorFraction:P2} of the lanes.");
        }

        if (upperRatio < 0.98)
        {
            return new(PythonRePublicStatus.ManagedFaster, null);
        }

        if (lowerRatio > 1.02)
        {
            return new(PythonRePublicStatus.CpythonFaster, null);
        }

        return lowerRatio >= 0.98 && upperRatio <= 1.02
            ? new(PythonRePublicStatus.Equivalent, null)
            : new(PythonRePublicStatus.Inconclusive, "The paired 95% interval crosses a Status boundary.");
    }

    private static string FormatStatus(PythonRePublicStatus status) => status switch
    {
        PythonRePublicStatus.Unqualified => "Unqualified",
        PythonRePublicStatus.Inconclusive => "Inconclusive",
        PythonRePublicStatus.Equivalent => "Equivalent",
        PythonRePublicStatus.ManagedFaster => "Managed faster",
        PythonRePublicStatus.CpythonFaster => "CPython faster",
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static void PersistPythonReQualification(
        string caseId,
        PythonReBenchmarkCase benchmarkCase,
        int inputUtf8Bytes,
        PythonReQualificationMeasurement qualification,
        DateTimeOffset measuredAtUtc)
    {
        var snapshotPath = FindRepositoryFile(SnapshotFileName);
        var snapshot = JsonSerializer.Deserialize<PythonReBenchmarkSnapshot>(File.ReadAllText(snapshotPath));
        if (snapshot is null || snapshot.SchemaVersion != PythonReBenchmarkSchemaVersion)
        {
            throw new InvalidOperationException(
                $"PythonRe qualification requires a schema-{PythonReBenchmarkSchemaVersion} snapshot.");
        }

        if (!snapshot.Cases.TryGetValue(caseId, out var measurement))
        {
            throw new InvalidOperationException($"PythonRe snapshot does not contain case '{caseId}'.");
        }

        if (!measurement.Pattern.Equals(benchmarkCase.Pattern, StringComparison.Ordinal) ||
            !measurement.Options.Equals(benchmarkCase.Options.ToString(), StringComparison.Ordinal) ||
            !measurement.Operation.Equals(benchmarkCase.Operation.ToString(), StringComparison.Ordinal) ||
            measurement.InputUtf8Bytes != inputUtf8Bytes)
        {
            throw new InvalidOperationException(
                $"PythonRe snapshot case '{caseId}' does not match the current catalog definition.");
        }

        measurement.Qualification = qualification;
        WriteSnapshot(new PythonReBenchmarkSnapshot
        {
            SchemaVersion = PythonReBenchmarkSchemaVersion,
            GeneratedAtUtc = measuredAtUtc,
            Corpus = CaptureCorpusProvenance(),
            Cases = snapshot.Cases,
        });
    }

    private static string ComputePythonReCaseDefinitionSha256(
        PythonReBenchmarkCase benchmarkCase,
        byte[] inputBytes) => ComputePythonReSha256(
            string.Join(
                '\n',
                benchmarkCase.Id,
                benchmarkCase.Pattern,
                ((int)benchmarkCase.Options).ToString(System.Globalization.CultureInfo.InvariantCulture),
                benchmarkCase.Operation.ToString(),
                benchmarkCase.Replacement,
                benchmarkCase.IncludesResultMaterialization.ToString(),
                Convert.ToHexString(SHA256.HashData(inputBytes))));

    private static string ComputePythonReCatalogSha256()
    {
        var definitions = PythonReBenchmarkCatalog.Cases.Select(benchmarkCase =>
            ComputePythonReCaseDefinitionSha256(
                benchmarkCase,
                Encoding.UTF8.GetBytes(benchmarkCase.Input)));
        return ComputePythonReSha256(string.Join('\n', definitions));
    }

    private static string ComputePythonReQualificationId(
        string caseDefinitionSha256,
        string catalogSha256,
        string sourceCommit,
        CpythonStreamEnvironment cpython,
        string cpuPolicy,
        string cpuAffinityMask,
        int? cpuEfficiencyClass,
        bool cpythonFirst,
        int samples) => ComputePythonReSha256(
            string.Join(
                '\n',
                PythonReQualificationProtocolVersion.ToString(System.Globalization.CultureInfo.InvariantCulture),
                caseDefinitionSha256,
                catalogSha256,
                sourceCommit,
                cpython.VersionDetail,
                string.Join('|', cpython.Git),
                cpython.ExecutableSha256,
                cpython.RuntimeLibrarySha256,
                cpython.RunnerSha256,
                cpuPolicy,
                cpuAffinityMask,
                cpuEfficiencyClass?.ToString(System.Globalization.CultureInfo.InvariantCulture) ??
                    "unavailable",
                cpythonFirst ? "CPython" : "PythonRe",
                samples.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static string ComputePythonReSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class CpythonStreamWorker : IDisposable
    {
        private readonly Process _process;
        private readonly Task<string> _standardError;
        private bool _disposed;

        internal CpythonStreamWorker()
        {
            var executable = System.Environment.GetEnvironmentVariable("UTF8REGEX_CPYTHON");
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
            startInfo.ArgumentList.Add("--stream");
            _process = Process.Start(startInfo) ??
                throw new InvalidOperationException($"Could not start CPython executable '{executable}'.");
            if (OperatingSystem.IsWindows())
            {
                using var parent = Process.GetCurrentProcess();
                _process.ProcessorAffinity = parent.ProcessorAffinity;
            }

            _standardError = _process.StandardError.ReadToEndAsync();
            try
            {
                Ready = Read("Ready");
                Environment = Ready.Environment ??
                    throw new InvalidOperationException("CPython streaming ready response has no environment.");
            }
            catch
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }

                _process.Dispose();
                throw;
            }
        }

        internal CpythonStreamResponse Ready { get; }

        internal CpythonStreamEnvironment Environment { get; }

        internal CpythonStreamResponse Prepare(PythonReBenchmarkCase benchmarkCase, byte[] inputBytes) => Send(
            new CpythonStreamCommand
            {
                ProtocolVersion = PythonReQualificationProtocolVersion,
                Kind = "Prepare",
                Pattern = benchmarkCase.Pattern,
                Options = (int)benchmarkCase.Options,
                Operation = benchmarkCase.Operation.ToString(),
                InputBase64 = Convert.ToBase64String(inputBytes),
                Replacement = benchmarkCase.Replacement,
            },
            "Prepared");

        internal CpythonStreamResponse Calibrate(
            CpythonStreamLane lane,
            double targetMilliseconds,
            int maximumIterations) => Send(
                new CpythonStreamCommand
                {
                    ProtocolVersion = PythonReQualificationProtocolVersion,
                    Kind = "Calibrate",
                    Lane = lane.ToString(),
                    TargetNanoseconds = checked((long)(targetMilliseconds * 1_000_000)),
                    MaximumIterations = maximumIterations,
                },
                "Calibrated");

        internal CpythonStreamResponse Warm(
            CpythonStreamLane lane,
            int iterations,
            int minimumMilliseconds,
            int maximumBatches) => Send(
                new CpythonStreamCommand
                {
                    ProtocolVersion = PythonReQualificationProtocolVersion,
                    Kind = "Warm",
                    Lane = lane.ToString(),
                    Iterations = iterations,
                    MinimumMilliseconds = minimumMilliseconds,
                    MaximumBatches = maximumBatches,
                },
                "Warmed");

        internal CpythonStreamResponse Measure(CpythonStreamLane lane, int iterations) => Send(
            new CpythonStreamCommand
            {
                ProtocolVersion = PythonReQualificationProtocolVersion,
                Kind = "Measure",
                Lane = lane.ToString(),
                Iterations = iterations,
            },
            "Measured");

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                if (!_process.HasExited)
                {
                    _ = Send(
                        new CpythonStreamCommand
                        {
                            ProtocolVersion = PythonReQualificationProtocolVersion,
                            Kind = "Shutdown",
                        },
                        "Shutdown");
                    _process.StandardInput.Close();
                    if (!_process.WaitForExit(2_000))
                    {
                        _process.Kill(entireProcessTree: true);
                    }
                }
            }
            catch
            {
                if (!_process.HasExited)
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
            finally
            {
                _disposed = true;
                _process.Dispose();
            }
        }

        private CpythonStreamResponse Send(CpythonStreamCommand command, string expectedKind)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CpythonStreamWorker));
            }

            _process.StandardInput.WriteLine(JsonSerializer.Serialize(command));
            _process.StandardInput.Flush();
            return Read(expectedKind);
        }

        private CpythonStreamResponse Read(string expectedKind)
        {
            string? line;
            using (var cancellation = new CancellationTokenSource(s_cpythonResponseTimeout))
            {
                try
                {
                    line = _process.StandardOutput.ReadLineAsync(cancellation.Token).AsTask().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException exception)
                {
                    if (!_process.HasExited)
                    {
                        _process.Kill(entireProcessTree: true);
                    }

                    throw new TimeoutException(
                        "CPython streaming worker did not answer within " +
                        $"{s_cpythonResponseTimeout.TotalSeconds:F0} seconds.",
                        exception);
                }
            }

            if (line is null)
            {
                var error = _standardError.IsCompletedSuccessfully ? _standardError.Result.Trim() : string.Empty;
                throw new InvalidOperationException(
                    $"CPython streaming worker exited before '{expectedKind}'. {error}".Trim());
            }

            var envelope = JsonSerializer.Deserialize<CpythonStreamResponse>(line) ??
                throw new InvalidOperationException("CPython streaming worker returned invalid JSON.");
            if (envelope.ProtocolVersion != PythonReQualificationProtocolVersion)
            {
                throw new InvalidOperationException(
                    $"CPython streaming worker returned protocol {envelope.ProtocolVersion}.");
            }

            if (envelope.Kind == "Error")
            {
                throw new InvalidOperationException(
                    $"CPython streaming worker failed with {envelope.ErrorType}: {envelope.Message}");
            }

            if (!envelope.Kind.Equals(expectedKind, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"CPython streaming worker returned '{envelope.Kind}', expected '{expectedKind}'.");
            }

            return envelope;
        }
    }
}

internal enum CpythonStreamLane : byte
{
    Predecoded = 0,
    EmptyLoop = 1,
}

internal enum PythonRePairLaneOrder : byte
{
    ManagedFirst = 0,
    CpythonFirst = 1,
}

internal enum PythonReQualificationWriteMode : byte
{
    None = 0,
    Snapshot = 1,
}

internal enum PythonRePublicStatus : byte
{
    Unqualified = 0,
    Inconclusive = 1,
    Equivalent = 2,
    ManagedFaster = 3,
    CpythonFaster = 4,
}

internal readonly record struct PythonReManagedSample(
    PythonReBenchmarkBatch Batch,
    TimeSpan ProcessCpu,
    int[] GcCollections);

internal readonly record struct PythonReEmptyBatch(TimeSpan Elapsed, int Checksum);

internal readonly record struct PythonReWarmup(int Iterations, TimeSpan Elapsed);

internal readonly record struct PythonReOrderedRatio(PythonRePairLaneOrder Order, double Ratio);

internal readonly record struct PythonReStatusResult(PythonRePublicStatus Status, string? Reason);

internal sealed class CpythonStreamCommand
{
    public required int ProtocolVersion { get; init; }
    public required string Kind { get; init; }
    public string? Pattern { get; init; }
    public int? Options { get; init; }
    public string? Operation { get; init; }
    public string? InputBase64 { get; init; }
    public string? Replacement { get; init; }
    public string? Lane { get; init; }
    public int? Iterations { get; init; }
    public long? TargetNanoseconds { get; init; }
    public int? MaximumIterations { get; init; }
    public int? MinimumMilliseconds { get; init; }
    public int? MaximumBatches { get; init; }
}

internal sealed class CpythonStreamResponse
{
    public int ProtocolVersion { get; init; }
    public string Kind { get; init; } = string.Empty;
    public CpythonStreamEnvironment? Environment { get; init; }
    public string? Lane { get; init; }
    public int Iterations { get; init; }
    public int Batches { get; init; }
    public long ElapsedNanoseconds { get; init; }
    public long ProcessCpuNanoseconds { get; init; }
    public int Checksum { get; init; }
    public ulong SemanticDigest { get; init; }
    public int[] GcCollections { get; init; } = [];
    public string? ErrorType { get; init; }
    public string? Message { get; init; }
}

internal sealed class CpythonStreamEnvironment
{
    public string Implementation { get; init; } = string.Empty;
    public string Version { get; init; } = string.Empty;
    public string VersionDetail { get; init; } = string.Empty;
    public long HexVersion { get; init; }
    public string[] Git { get; init; } = [];
    public string CacheTag { get; init; } = string.Empty;
    public string Compiler { get; init; } = string.Empty;
    public string SoAbi { get; init; } = string.Empty;
    public bool DebugBuild { get; init; }
    public bool? GilEnabled { get; init; }
    public string Executable { get; init; } = string.Empty;
    public string ExecutableSha256 { get; init; } = string.Empty;
    public string RuntimeLibrary { get; init; } = string.Empty;
    public string RuntimeLibrarySha256 { get; init; } = string.Empty;
    public string Platform { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string RunnerSha256 { get; init; } = string.Empty;
    public CpythonStreamTimer Timer { get; init; } = new();
}

internal sealed class CpythonStreamTimer
{
    public string Implementation { get; init; } = string.Empty;
    public double ResolutionSeconds { get; init; }
    public bool Monotonic { get; init; }
    public bool Adjustable { get; init; }
}
