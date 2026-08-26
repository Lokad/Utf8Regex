using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const int PythonReQualificationProtocolVersion = 3;
    private const string PythonReQualificationProcessorPolicy =
        "single-least-contended-highest-efficiency-processor";
    private const int PythonReQualificationBootstrapSeed = 31302;
    private const int PythonReQualificationBootstrapResamples = 10_000;
    private const int PythonReQualificationMaximumIterations = 10_000_000;
    private const int PythonReQualificationOneShotWarmupCalls = 100_000;
    private const int PythonReQualificationFindAllWarmupCalls = 10_000;
    private const int PythonReQualificationShortFindAllWarmupCalls = 100_000;
    private const int PythonReQualificationShortFindAllCalibrationIterations = 2_000;
    private const int PythonReQualificationReplacementWarmupCalls = 10_000;
    private const int PythonReQualificationSplitWarmupCalls = 10_000;
    private const int PythonReQualificationShortOneShotMinimumIterations = 1_000_000;
    private const int PythonReQualificationShortOneShotWarmupCalls = 5_000_000;
    private const int PythonReQualificationShortOneShotCalibrationIterations = 50_000;
    private const int PythonReQualificationMinimumWarmupCalls = 1_024;
    private const double PythonReQualificationTargetSampleMilliseconds = 50;
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
        var byteControlEligibility = PythonReBenchmarkCatalog.GetByteControlEligibility(
            benchmarkCase,
            context.InputBytes);
        var expectedChecksum = context.ExecutePythonRe();
        var expectedSemanticDigest = context.ExecutePythonReSemanticDigest();
        var expectedConsumptionToken = context.ExecutePythonReConsumptionToken();
        using var worker = new CpythonStreamWorker();
        var prepared = worker.Prepare(
            benchmarkCase,
            context.InputBytes,
            byteControlEligibility.IsEligible);
        if (prepared.Checksum != expectedChecksum ||
            prepared.SemanticDigest != expectedSemanticDigest ||
            prepared.ConsumptionChecksum != expectedConsumptionToken)
        {
            throw new InvalidOperationException(
                $"PythonRe benchmark '{caseId}' disagrees with CPython streaming preflight: " +
                $"checksum={expectedChecksum}/{prepared.Checksum}, " +
                $"semantic digest={expectedSemanticDigest:X16}/{prepared.SemanticDigest:X16}, " +
                $"consumption={expectedConsumptionToken}/{prepared.ConsumptionChecksum}.");
        }

        if (prepared.ByteControlAvailable != byteControlEligibility.IsEligible ||
            byteControlEligibility.IsEligible &&
            (prepared.ByteControlChecksum != expectedChecksum ||
             prepared.ByteControlSemanticDigest != expectedSemanticDigest ||
             prepared.ByteControlConsumptionChecksum != expectedConsumptionToken))
        {
            throw new InvalidOperationException(
                $"PythonRe benchmark '{caseId}' disagrees with its CPython bytes-control preflight: " +
                $"available={byteControlEligibility.IsEligible}/{prepared.ByteControlAvailable}, " +
                $"checksum={expectedChecksum}/{prepared.ByteControlChecksum}, " +
                $"semantic digest={expectedSemanticDigest:X16}/{prepared.ByteControlSemanticDigest:X16}, " +
                $"consumption={expectedConsumptionToken}/{prepared.ByteControlConsumptionChecksum}.");
        }

        for (var warmupCall = 0; warmupCall < 64; warmupCall++)
        {
            var batch = context.MeasurePythonReQualificationBatch(1);
            VerifyManagedResult(
                batch,
                expectedChecksum,
                expectedSemanticDigest,
                expectedConsumptionToken,
                1,
                "tiering warmup");
        }

        var preliminaryManagedIterations = CalibrateManagedBatch(
            context,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken);
        var preliminaryCpythonCalibration = worker.Calibrate(
            CpythonStreamLane.Predecoded,
            PythonReQualificationTargetSampleMilliseconds,
            PythonReQualificationMaximumIterations);
        var preliminaryByteCalibration = byteControlEligibility.IsEligible
            ? worker.Calibrate(
                CpythonStreamLane.Bytes,
                PythonReQualificationTargetSampleMilliseconds,
                PythonReQualificationMaximumIterations)
            : null;
        var managedWarmup = WarmManagedLane(
            context,
            preliminaryManagedIterations,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken,
            GetManagedWarmupCalls(benchmarkCase));
        var cpythonWarmup = worker.Warm(
            CpythonStreamLane.Predecoded,
            preliminaryCpythonCalibration.Iterations,
            minimumMilliseconds: 100,
            minimumCalls: PythonReQualificationMinimumWarmupCalls,
            maximumBatches: 32);
        var byteWarmup = preliminaryByteCalibration is not null
            ? worker.Warm(
                CpythonStreamLane.Bytes,
                preliminaryByteCalibration.Iterations,
                minimumMilliseconds: 100,
                minimumCalls: PythonReQualificationMinimumWarmupCalls,
                maximumBatches: 32)
            : null;
        var calibratedManagedIterations = CalibrateManagedBatch(
            context,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken);
        var shortOneShotMinimumIterations = GetShortOneShotMinimumIterations(
            benchmarkCase,
            calibratedManagedIterations);
        var managedIterations = Math.Max(
            calibratedManagedIterations,
            shortOneShotMinimumIterations);
        managedWarmup = WarmManagedLane(
            context,
            managedIterations,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken,
            shortOneShotMinimumIterations > 1
                ? PythonReQualificationShortOneShotWarmupCalls
                : GetManagedWarmupCalls(benchmarkCase, calibratedManagedIterations));

        managedIterations = ConfirmManagedSampleDuration(
            context,
            managedIterations,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken);
        var cpythonCalibration = worker.Calibrate(
            CpythonStreamLane.Predecoded,
            PythonReQualificationTargetSampleMilliseconds,
            PythonReQualificationMaximumIterations);
        var cpythonIterations = cpythonCalibration.Iterations;
        var byteCalibration = byteControlEligibility.IsEligible
            ? worker.Calibrate(
                CpythonStreamLane.Bytes,
                PythonReQualificationTargetSampleMilliseconds,
                PythonReQualificationMaximumIterations)
            : null;
        var byteIterations = byteCalibration?.Iterations ?? 0;

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
        var byteMicroseconds = new List<double>(samples);
        var byteMilliseconds = new List<double>(samples);
        var byteProcessCpuMilliseconds = new List<double>(samples);
        var byteGcCollections = new List<int[]>(samples);
        var byteRatios = new List<double>(samples);

        for (var sample = 0; sample < samples; sample++)
        {
            var order = (sample + (cpythonFirst ? 1 : 0)) % 2 == 0
                ? PythonRePairLaneOrder.ManagedFirst
                : PythonRePairLaneOrder.CpythonFirst;
            PythonReManagedSample managed;
            CpythonStreamResponse cpython;
            CpythonStreamResponse? bytes = null;
            if (order == PythonRePairLaneOrder.ManagedFirst)
            {
                managed = MeasureManagedSample(
                    context,
                    managedIterations,
                    expectedChecksum,
                    expectedSemanticDigest,
                    expectedConsumptionToken,
                    currentProcess);
                cpython = worker.Measure(CpythonStreamLane.Predecoded, cpythonIterations);
                if (byteControlEligibility.IsEligible)
                {
                    bytes = worker.Measure(CpythonStreamLane.Bytes, byteIterations);
                }
            }
            else
            {
                if (byteControlEligibility.IsEligible)
                {
                    bytes = worker.Measure(CpythonStreamLane.Bytes, byteIterations);
                }

                cpython = worker.Measure(CpythonStreamLane.Predecoded, cpythonIterations);
                managed = MeasureManagedSample(
                    context,
                    managedIterations,
                    expectedChecksum,
                    expectedSemanticDigest,
                    expectedConsumptionToken,
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
            if (bytes is not null)
            {
                var bytePerOperation = bytes.ElapsedNanoseconds / (double)byteIterations / 1_000;
                byteMicroseconds.Add(bytePerOperation);
                byteMilliseconds.Add(bytes.ElapsedNanoseconds / 1_000_000d);
                byteProcessCpuMilliseconds.Add(bytes.ProcessCpuNanoseconds / 1_000_000d);
                byteGcCollections.Add(bytes.GcCollections);
                byteRatios.Add(managedPerOperation / bytePerOperation);
            }
        }

        const int floorSamples = 3;
        var managedFloorMicroseconds = new List<double>(floorSamples);
        var cpythonFloorMicroseconds = new List<double>(floorSamples);
        var managedTrivialMicroseconds = new List<double>(floorSamples);
        var cpythonTrivialMicroseconds = new List<double>(floorSamples);
        var byteEmptyMicroseconds = new List<double>(floorSamples);
        var byteTrivialMicroseconds = new List<double>(floorSamples);
        for (var sample = 0; sample < floorSamples; sample++)
        {
            var managedFloor = MeasureManagedEmptyLoop(managedIterations);
            var cpythonFloor = worker.Measure(CpythonStreamLane.EmptyLoop, cpythonIterations);
            var managedTrivial = MeasureManagedTrivialCall(managedIterations);
            var cpythonTrivial = worker.Measure(CpythonStreamLane.BoundTrivialCall, cpythonIterations);
            managedFloorMicroseconds.Add(managedFloor.Elapsed.TotalMicroseconds / managedIterations);
            cpythonFloorMicroseconds.Add(
                cpythonFloor.ElapsedNanoseconds / (double)cpythonIterations / 1_000);
            managedTrivialMicroseconds.Add(managedTrivial.Elapsed.TotalMicroseconds / managedIterations);
            cpythonTrivialMicroseconds.Add(
                cpythonTrivial.ElapsedNanoseconds / (double)cpythonIterations / 1_000);
            if (byteControlEligibility.IsEligible)
            {
                var byteEmpty = worker.Measure(CpythonStreamLane.EmptyLoop, byteIterations);
                var byteTrivial = worker.Measure(CpythonStreamLane.BoundTrivialCall, byteIterations);
                byteEmptyMicroseconds.Add(
                    byteEmpty.ElapsedNanoseconds / (double)byteIterations / 1_000);
                byteTrivialMicroseconds.Add(
                    byteTrivial.ElapsedNanoseconds / (double)byteIterations / 1_000);
                s_sink ^= byteEmpty.Checksum ^ byteTrivial.Checksum;
            }

            s_sink ^= managedFloor.Checksum ^ cpythonFloor.Checksum ^
                managedTrivial.Checksum ^ cpythonTrivial.Checksum;
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
        var placementQualified = processorScope.Policy == PythonReQualificationProcessorPolicy;
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
        var byteControlEvidence = CreatePythonReByteControlEvidence(
            byteControlEligibility,
            context.DescribeManagedRoute(),
            worktreeQualified,
            placementQualified,
            byteIterations,
            byteWarmup,
            laneOrders,
            managedMicroseconds,
            managedMilliseconds,
            byteMicroseconds,
            byteMilliseconds,
            byteProcessCpuMilliseconds,
            byteGcCollections,
            byteRatios,
            byteEmptyMicroseconds,
            managedTrivialMicroseconds,
            byteTrivialMicroseconds);
        var measuredAtUtc = DateTimeOffset.UtcNow;
        var managedEnvironment = CaptureEnvironment();
        var caseDefinitionSha256 = ComputePythonReCaseDefinitionSha256(benchmarkCase, context.InputBytes);
        var catalogSha256 = ComputePythonReCatalogSha256();
        var managedProductSha256 = ComputePythonReManagedProductSha256();
        var managedOperationProtocolSha256 =
            ComputePythonReManagedOperationProtocolSha256(benchmarkCase.Operation);
        var cpythonOperationProtocolSha256 =
            ComputePythonReCpythonOperationProtocolSha256(benchmarkCase.Operation);
        var sharedProtocolSha256 = ComputePythonReSharedProtocolSha256();
        var pairedEvidence = new PythonRePairedEvidence
        {
            ProtocolVersion = PythonReQualificationProtocolVersion,
            QualificationId = ComputePythonReQualificationId(
                caseDefinitionSha256,
                managedProductSha256,
                managedOperationProtocolSha256,
                cpythonOperationProtocolSha256,
                sharedProtocolSha256,
                managedEnvironment,
                worker.Environment,
                processorScope.Policy,
                processorScope.AffinityMask,
                processorScope.EfficiencyClass,
                cpythonFirst,
                samples),
            MeasuredAtUtc = measuredAtUtc,
            SourceCommit = managedEnvironment.SourceCommit,
            Baseline = "CPythonPredecodedElapsed",
            ResultContract = GetPythonReResultContract(benchmarkCase),
            InitialLane = cpythonFirst ? "CPython" : "PythonRe",
            WorktreeQualified = worktreeQualified,
            CaseDefinitionSha256 = caseDefinitionSha256,
            CatalogSha256 = catalogSha256,
            ManagedProductSha256 = managedProductSha256,
            ManagedOperationProtocolSha256 = managedOperationProtocolSha256,
            CpythonOperationProtocolSha256 = cpythonOperationProtocolSha256,
            SharedProtocolSha256 = sharedProtocolSha256,
            SemanticDigestAlgorithm = PythonReSemanticDigestAlgorithm,
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
            ManagedTrivialCallMicroseconds = managedTrivialMicroseconds.ToArray(),
            CpythonTrivialCallMicroseconds = cpythonTrivialMicroseconds.ToArray(),
            ByteControl = byteControlEvidence,
            CpythonEnvironment = worker.Environment,
            ManagedEnvironment = managedEnvironment,
        };
        var qualification = new PythonReQualificationMeasurement
        {
            Status = status.Status.ToString(),
            StatusReason = status.Reason ?? "Qualified paired evidence.",
            EngineEvidenceBasis = byteControlEvidence is null
                ? "Not engine-comparable"
                : "Byte control",
            EngineConclusion = byteControlEvidence?.EngineConclusion ?? "NotApplicable",
            EngineConclusionReason = byteControlEvidence?.EngineConclusionReason ??
                byteControlEligibility.Reason,
            PairedEvidence = pairedEvidence,
        };

        Console.WriteLine($"CaseId             : {caseId}");
        Console.WriteLine($"Operation          : {benchmarkCase.Operation}");
        Console.WriteLine($"Result contract    : {pairedEvidence.ResultContract}");
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
            $"Bound trivial call : managed=" +
            $"{BenchmarkPairedStatistics.Median(managedTrivialMicroseconds) / managedMedian:P2}; " +
            $"CPython={BenchmarkPairedStatistics.Median(cpythonTrivialMicroseconds) / cpythonMedian:P2}");
        Console.WriteLine(
            $"Managed alloc      : " +
            $"{BenchmarkPairedStatistics.Median(managedAllocatedBytes.Select(static value => (double)value)):F0} B/op");
        Console.WriteLine(
            $"Process CPU/sample : managed=" +
            $"{BenchmarkPairedStatistics.Median(managedProcessCpuMilliseconds):F1} ms; " +
            $"CPython={BenchmarkPairedStatistics.Median(cpythonProcessCpuMilliseconds):F1} ms (diagnostic)");
        Console.WriteLine($"Status             : {FormatStatus(status.Status)}");
        Console.WriteLine($"Status reason      : {status.Reason ?? "qualified paired evidence"}");
        Console.WriteLine(
            $"Byte control       : {(byteControlEligibility.IsEligible ? "eligible" : "excluded")}; " +
            byteControlEligibility.Reason);
        if (byteControlEvidence is not null)
        {
            Console.WriteLine(
                $"Rbyte             : {byteControlEvidence.RatioMedian:F3} " +
                $"[{byteControlEvidence.RatioLower95:F3}, {byteControlEvidence.RatioUpper95:F3}]");
            Console.WriteLine(
                $"Engine conclusion : {byteControlEvidence.EngineConclusion}; " +
                byteControlEvidence.EngineConclusionReason);
        }

        Console.WriteLine($"CPython            : {worker.Environment.Implementation} " +
                          $"{worker.Environment.Version}; {worker.Environment.ExecutableSha256}");
        Console.WriteLine("Raw pairs:");
        for (var sample = 0; sample < samples; sample++)
        {
            Console.WriteLine(
                $"  {sample + 1,2}: {laneOrders[sample],12}; " +
                $"managed={managedMicroseconds[sample],10:F3} us; " +
                $"CPython={cpythonMicroseconds[sample],10:F3} us; R={ratios[sample]:F3}; " +
                (byteControlEvidence is null
                    ? string.Empty
                    : $"bytes={byteMicroseconds[sample],10:F3} us; Rbyte={byteRatios[sample]:F3}; ") +
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

    private static int ResumePythonReQualifications(
        int initialSamples,
        int extendedSamples,
        int maximumCases)
    {
#if DEBUG
        Console.Error.WriteLine("PythonRe qualification resume requires a Release build.");
        return 1;
#else
        if (initialSamples < 9 || extendedSamples < initialSamples)
        {
            Console.Error.WriteLine(
                "PythonRe qualification resume requires 9-17 initial samples and an equal or larger extension.");
            return 1;
        }

        CpythonStreamEnvironment currentCpython;
        using (var worker = new CpythonStreamWorker())
        {
            currentCpython = worker.Environment;
        }

        var currentManaged = CaptureEnvironment();
        var currentManagedProductSha256 = ComputePythonReManagedProductSha256();
        var measured = 0;
        var skipped = 0;
        foreach (var benchmarkCase in PythonReBenchmarkCatalog.Cases)
        {
            var snapshot = LoadPythonReBenchmarkSnapshot();
            snapshot.Cases.TryGetValue(benchmarkCase.Id, out var measurement);
            var freshness = GetPythonReQualificationFreshness(
                    benchmarkCase,
                    measurement,
                    currentManagedProductSha256,
                    currentCpython,
                    currentManaged);
            if (freshness == PythonReQualificationFreshness.Current &&
                measurement is not null &&
                !ShouldExtendPythonReQualification(measurement.Qualification, extendedSamples))
            {
                Console.WriteLine($"Skip current         : {benchmarkCase.Id}");
                skipped++;
                continue;
            }

            Console.WriteLine(
                $"Qualification state  : {benchmarkCase.Id}: " +
                PythonReQualificationFreshnessEvaluator.Format(freshness));

            if (measurement is null)
            {
                Console.Error.WriteLine(
                    $"PythonRe snapshot has no row for new catalog case '{benchmarkCase.Id}'. " +
                    "Refresh that case before qualification.");
                continue;
            }

            if (measured >= maximumCases)
            {
                break;
            }

            Console.WriteLine(
                $"Qualify             : {benchmarkCase.Id} ({initialSamples} paired samples)");
            var result = MeasurePairedCase(
                benchmarkCase.Id,
                initialSamples,
                cpythonFirst: false,
                PythonReQualificationWriteMode.Snapshot);
            if (result != 0)
            {
                return result;
            }

            measurement = LoadPythonReBenchmarkSnapshot().Cases[benchmarkCase.Id];
            if (ShouldExtendPythonReQualification(measurement.Qualification, extendedSamples))
            {
                Console.WriteLine(
                    $"Extend              : {benchmarkCase.Id} ({extendedSamples} paired samples)");
                result = MeasurePairedCase(
                    benchmarkCase.Id,
                    extendedSamples,
                    cpythonFirst: false,
                    PythonReQualificationWriteMode.Snapshot);
                if (result != 0)
                {
                    return result;
                }
            }

            measured++;
        }

        var remaining = LoadPythonReBenchmarkSnapshot().Cases.Values.Count(
            measurement => measurement.Qualification?.PairedEvidence is null);
        Console.WriteLine(
            $"Qualification batch : measured={measured}; skipped={skipped}; " +
            $"remaining-unqualified={remaining}");
        return 0;
#endif
    }

    private static PythonReQualificationFreshness GetPythonReQualificationFreshness(
        PythonReBenchmarkCase benchmarkCase,
        PythonReCaseMeasurement? measurement,
        string currentManagedProductSha256,
        CpythonStreamEnvironment currentCpython,
        PythonReBenchmarkEnvironment currentManaged)
    {
        var qualification = measurement?.Qualification;
        var evidence = qualification?.PairedEvidence;
        var managedMetadataMatches = measurement is not null &&
            measurement.ComparatorOwner.Equals(
                PythonReBenchmarkCatalog.GetComparatorOwner(benchmarkCase),
                StringComparison.Ordinal) &&
            measurement.ManagedRoute.Equals(
                new PythonReBenchmarkContext(benchmarkCase).DescribeManagedRoute(),
                StringComparison.Ordinal);
        var resultContractMatches = evidence is not null &&
            evidence.ResultContract.Equals(GetPythonReResultContract(benchmarkCase), StringComparison.Ordinal);
        return PythonReQualificationFreshnessEvaluator.Evaluate(
            new PythonReQualificationFreshnessInput(
                HasEvidence: evidence is not null,
                StoredCaseDefinitionSha256: evidence?.CaseDefinitionSha256 ?? string.Empty,
                CurrentCaseDefinitionSha256: ComputePythonReCaseDefinitionSha256(
                    benchmarkCase,
                    Encoding.UTF8.GetBytes(benchmarkCase.Input)),
                StoredManagedProductSha256: evidence?.ManagedProductSha256 ?? string.Empty,
                CurrentManagedProductSha256: currentManagedProductSha256,
                StoredManagedOperationProtocolSha256: resultContractMatches
                    ? evidence!.ManagedOperationProtocolSha256
                    : string.Empty,
                CurrentManagedOperationProtocolSha256:
                    ComputePythonReManagedOperationProtocolSha256(benchmarkCase.Operation),
                StoredCpythonOperationProtocolSha256:
                    evidence?.CpythonOperationProtocolSha256 ?? string.Empty,
                CurrentCpythonOperationProtocolSha256:
                    ComputePythonReCpythonOperationProtocolSha256(benchmarkCase.Operation),
                StoredSharedProtocolSha256: evidence is
                    {
                        ProtocolVersion: PythonReQualificationProtocolVersion,
                        WorktreeQualified: true,
                    }
                    ? evidence.SharedProtocolSha256
                    : string.Empty,
                CurrentSharedProtocolSha256: ComputePythonReSharedProtocolSha256(),
                ManagedMetadataMatches: managedMetadataMatches,
                RuntimeMatches: evidence is not null &&
                    PythonReManagedRuntimeMatches(evidence.ManagedEnvironment, currentManaged),
                InterpreterMatches: evidence is not null &&
                    PythonReInterpreterMatches(evidence.CpythonEnvironment, currentCpython)));
    }

    private static bool ShouldExtendPythonReQualification(
        PythonReQualificationMeasurement? qualification,
        int extendedSamples)
    {
        var evidence = qualification?.PairedEvidence;
        if (qualification is null || evidence is null || evidence.Samples.Length >= extendedSamples)
        {
            return false;
        }

        return qualification.Status.Equals("Inconclusive", StringComparison.Ordinal) ||
            qualification.Status.Equals("Unqualified", StringComparison.Ordinal) &&
            (qualification.StatusReason.StartsWith("At least one lane sample", StringComparison.Ordinal) ||
             qualification.StatusReason.StartsWith("Removable harness floors", StringComparison.Ordinal));
    }

    private static int EmitPythonReFreshnessReport()
    {
        var snapshot = LoadPythonReBenchmarkSnapshot();
        using var worker = new CpythonStreamWorker();
        var currentManaged = CaptureEnvironment();
        var currentManagedProductSha256 = ComputePythonReManagedProductSha256();
        var counts = new Dictionary<PythonReQualificationFreshness, int>();

        foreach (var benchmarkCase in PythonReBenchmarkCatalog.Cases)
        {
            snapshot.Cases.TryGetValue(benchmarkCase.Id, out var measurement);
            var freshness = GetPythonReQualificationFreshness(
                benchmarkCase,
                measurement,
                currentManagedProductSha256,
                worker.Environment,
                currentManaged);
            counts[freshness] = counts.GetValueOrDefault(freshness) + 1;
            Console.WriteLine(
                $"{benchmarkCase.Id,-46} " +
                PythonReQualificationFreshnessEvaluator.Format(freshness));
        }

        foreach (var snapshotCase in snapshot.Cases.Keys.Except(
                     PythonReBenchmarkCatalog.Cases.Select(static benchmarkCase => benchmarkCase.Id),
                     StringComparer.Ordinal))
        {
            Console.WriteLine($"{snapshotCase,-46} snapshot-only case");
        }

        Console.WriteLine();
        Console.WriteLine(
            string.Join(
                "; ",
                counts.OrderBy(static pair => pair.Key).Select(pair =>
                    $"{PythonReQualificationFreshnessEvaluator.Format(pair.Key)}={pair.Value}")));
        return counts.Keys.All(static freshness =>
            freshness is PythonReQualificationFreshness.Current or
                PythonReQualificationFreshness.NewCase)
            ? 0
            : 1;
    }

    private static int EmitPythonRePriorityReport()
    {
        var snapshot = LoadPythonReBenchmarkSnapshot();
        var qualifiedWins = snapshot.Cases
            .Where(static pair =>
                pair.Value.Qualification is
                {
                    Status: "CpythonFaster",
                    PairedEvidence: not null,
                })
            .ToArray();
        var engineWins = snapshot.Cases
            .Where(static pair =>
                pair.Value.Qualification is
                {
                    EngineConclusion: "CpythonFaster",
                    PairedEvidence.ByteControl: not null,
                })
            .ToArray();

        Console.WriteLine("Qualified public CPython wins by relative gap:");
        PrintPublicPriorities(qualifiedWins.OrderByDescending(
            static pair => pair.Value.Qualification!.PairedEvidence!.StrongRatioMedian));
        Console.WriteLine("Qualified public CPython wins by absolute recurring gap:");
        PrintPublicPriorities(qualifiedWins.OrderByDescending(
            static pair => pair.Value.Qualification!.PairedEvidence!.StrongDifferenceMicroseconds));
        Console.WriteLine("Qualified bytes-control CPython engine wins by relative gap:");
        foreach (var (caseId, measurement) in engineWins.OrderByDescending(
                     static pair => pair.Value.Qualification!.PairedEvidence!.ByteControl!.RatioMedian))
        {
            var evidence = measurement.Qualification!.PairedEvidence!;
            var bytes = evidence.ByteControl!;
            Console.WriteLine(
                $"  {caseId}: Rbyte={bytes.RatioMedian:F3} " +
                $"[{bytes.RatioLower95:F3}, {bytes.RatioUpper95:F3}]; " +
                $"route={measurement.ManagedRoute}");
        }

        if (engineWins.Length == 0)
        {
            Console.WriteLine("  none");
        }

        return 0;

        static void PrintPublicPriorities(
            IEnumerable<KeyValuePair<string, PythonReCaseMeasurement>> priorities)
        {
            var count = 0;
            foreach (var (caseId, measurement) in priorities)
            {
                var evidence = measurement.Qualification!.PairedEvidence!;
                Console.WriteLine(
                    $"  {caseId}: Rstrong={evidence.StrongRatioMedian:F3} " +
                    $"[{evidence.StrongRatioLower95:F3}, {evidence.StrongRatioUpper95:F3}]; " +
                    $"Estrong={evidence.StrongDifferenceMicroseconds:+0.000;-0.000;0.000} us; " +
                    $"owner={measurement.ComparatorOwner}; route={measurement.ManagedRoute}");
                count++;
            }

            if (count == 0)
            {
                Console.WriteLine("  none");
            }
        }
    }

    private static int CalibrateManagedBatch(
        PythonReBenchmarkContext context,
        int expectedChecksum,
        ulong expectedSemanticDigest,
        ulong expectedConsumptionToken)
    {
        var iterations = 1;
        var pilot = context.MeasurePythonReQualificationBatch(iterations);
        VerifyManagedResult(
            pilot,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken,
            iterations,
            "calibration");
        while (pilot.Elapsed.TotalMilliseconds < PythonReQualificationPilotMilliseconds &&
               iterations < PythonReQualificationMaximumIterations)
        {
            var elapsed = Math.Max(pilot.Elapsed.TotalMilliseconds, 0.000_001);
            var growth = Math.Max(2, (int)Math.Ceiling(PythonReQualificationPilotMilliseconds / elapsed));
            iterations = (int)Math.Min(
                PythonReQualificationMaximumIterations,
                (long)iterations * growth);
            pilot = context.MeasurePythonReQualificationBatch(iterations);
            VerifyManagedResult(
                pilot,
                expectedChecksum,
                expectedSemanticDigest,
                expectedConsumptionToken,
                iterations,
                "calibration");
        }

        iterations = (int)Math.Clamp(
            Math.Round(iterations * PythonReQualificationTargetSampleMilliseconds /
                       Math.Max(pilot.Elapsed.TotalMilliseconds, 0.000_001)),
            1,
            PythonReQualificationMaximumIterations);
        const int confirmationAttempts = 3;
        var fastestMillisecondsPerOperation = double.PositiveInfinity;
        for (var attempt = 0; attempt < confirmationAttempts; attempt++)
        {
            var confirmation = context.MeasurePythonReQualificationBatch(iterations);
            VerifyManagedResult(
                confirmation,
                expectedChecksum,
                expectedSemanticDigest,
                expectedConsumptionToken,
                iterations,
                "calibration confirmation");
            fastestMillisecondsPerOperation = Math.Min(
                fastestMillisecondsPerOperation,
                confirmation.Elapsed.TotalMilliseconds / iterations);
            if (confirmation.Elapsed.TotalMilliseconds >= 30)
            {
                continue;
            }

            iterations = (int)Math.Clamp(
                Math.Round(
                    iterations * PythonReQualificationTargetSampleMilliseconds /
                    Math.Max(confirmation.Elapsed.TotalMilliseconds, 0.000_001)),
                1,
                PythonReQualificationMaximumIterations);
        }

        iterations = (int)Math.Clamp(
            Math.Ceiling(PythonReQualificationTargetSampleMilliseconds /
                         Math.Max(fastestMillisecondsPerOperation, 0.000_000_001)),
            1,
            PythonReQualificationMaximumIterations);

        return iterations;
    }

    private static int GetShortOneShotMinimumIterations(
        PythonReBenchmarkCase benchmarkCase,
        int calibratedIterations)
    {
        if (calibratedIterations < PythonReQualificationShortOneShotCalibrationIterations ||
            benchmarkCase.Operation is not (PythonReBenchmarkOperation.IsMatch or
                PythonReBenchmarkOperation.Search or
                PythonReBenchmarkOperation.SearchFromOffset or
                PythonReBenchmarkOperation.Match or
                PythonReBenchmarkOperation.FullMatch))
        {
            return 1;
        }

        // Very short routes can tier again after ordinary calibration even
        // when SIMD validation makes their input look too large for a size
        // heuristic. The measured-rate gate avoids lengthening slower rows.
        return PythonReQualificationShortOneShotMinimumIterations;
    }

    private static int GetManagedWarmupCalls(
        PythonReBenchmarkCase benchmarkCase,
        int calibratedIterations = 0) =>
        benchmarkCase.Operation switch
        {
            PythonReBenchmarkOperation.IsMatch or
            PythonReBenchmarkOperation.Search or
                PythonReBenchmarkOperation.SearchFromOffset or
                PythonReBenchmarkOperation.Match or
                PythonReBenchmarkOperation.FullMatch => PythonReQualificationOneShotWarmupCalls,
            PythonReBenchmarkOperation.FindAllStrings or
                PythonReBenchmarkOperation.FindAllStringsFromOffset or
                PythonReBenchmarkOperation.FindAllUtf8 or
                PythonReBenchmarkOperation.FindAllStructural =>
                calibratedIterations >= PythonReQualificationShortFindAllCalibrationIterations
                    ? PythonReQualificationShortFindAllWarmupCalls
                    : PythonReQualificationFindAllWarmupCalls,
            PythonReBenchmarkOperation.ReplaceString or
                PythonReBenchmarkOperation.ReplaceUtf8 or
                PythonReBenchmarkOperation.ReplaceStringLimited or
                PythonReBenchmarkOperation.ReplaceEvaluatorString or
                PythonReBenchmarkOperation.SubnString or
                PythonReBenchmarkOperation.SubnUtf8 or
                PythonReBenchmarkOperation.SubnEvaluatorString or
                PythonReBenchmarkOperation.SubnEvaluatorUtf8 => PythonReQualificationReplacementWarmupCalls,
            // The direct split routes can tier after the default 1,024 calls.
            // Warming through that transition keeps the calibrated batch above
            // the qualification duration floor after its steady-state speedup.
            PythonReBenchmarkOperation.SplitStrings or
                PythonReBenchmarkOperation.SplitDetailed => PythonReQualificationSplitWarmupCalls,
            _ => PythonReQualificationMinimumWarmupCalls,
        };

    private static int ConfirmManagedSampleDuration(
        PythonReBenchmarkContext context,
        int iterations,
        int expectedChecksum,
        ulong expectedSemanticDigest,
        ulong expectedConsumptionToken)
    {
        const int maximumRounds = 3;
        const int confirmationsPerRound = 3;
        for (var round = 0; round < maximumRounds; round++)
        {
            var fastestMilliseconds = double.PositiveInfinity;
            for (var attempt = 0; attempt < confirmationsPerRound; attempt++)
            {
                var confirmation = context.MeasurePythonReQualificationBatch(iterations);
                VerifyManagedResult(
                    confirmation,
                    expectedChecksum,
                    expectedSemanticDigest,
                    expectedConsumptionToken,
                    iterations,
                    "post-warm calibration confirmation");
                fastestMilliseconds = Math.Min(
                    fastestMilliseconds,
                    confirmation.Elapsed.TotalMilliseconds);
            }

            if (fastestMilliseconds >= 30)
            {
                return iterations;
            }

            iterations = (int)Math.Clamp(
                Math.Ceiling(
                    iterations * PythonReQualificationTargetSampleMilliseconds /
                    Math.Max(fastestMilliseconds, 0.000_001)),
                1,
                PythonReQualificationMaximumIterations);
        }

        return iterations;
    }

    private static PythonReByteControlEvidence? CreatePythonReByteControlEvidence(
        PythonReByteControlEligibility eligibility,
        string managedRoute,
        bool worktreeQualified,
        bool placementQualified,
        int iterations,
        CpythonStreamResponse? warmup,
        IReadOnlyList<PythonRePairLaneOrder> laneOrders,
        IReadOnlyList<double> managedMicroseconds,
        IReadOnlyList<double> managedMilliseconds,
        IReadOnlyList<double> byteMicroseconds,
        IReadOnlyList<double> byteMilliseconds,
        IReadOnlyList<double> byteProcessCpuMilliseconds,
        IReadOnlyList<int[]> byteGcCollections,
        IReadOnlyList<double> ratios,
        IReadOnlyList<double> byteEmptyMicroseconds,
        IReadOnlyList<double> managedTrivialMicroseconds,
        IReadOnlyList<double> byteTrivialMicroseconds)
    {
        if (!eligibility.IsEligible)
        {
            return null;
        }

        if (warmup is null || byteMicroseconds.Count != managedMicroseconds.Count)
        {
            throw new InvalidOperationException("An eligible PythonRe byte control has incomplete samples.");
        }

        var logRatios = ratios.Select(static ratio => Math.Log(ratio)).ToArray();
        var interval = BenchmarkPairedStatistics.BootstrapMedianLogRatio(
            logRatios,
            PythonReQualificationBootstrapSeed,
            PythonReQualificationBootstrapResamples);
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
        var managedMedian = BenchmarkPairedStatistics.Median(managedMicroseconds);
        var byteMedian = BenchmarkPairedStatistics.Median(byteMicroseconds);
        var managedTrivialFraction =
            BenchmarkPairedStatistics.Median(managedTrivialMicroseconds) / managedMedian;
        var byteTrivialFraction =
            BenchmarkPairedStatistics.Median(byteTrivialMicroseconds) / byteMedian;
        var ratioLower = Math.Exp(interval.Lower);
        var ratioUpper = Math.Exp(interval.Upper);
        var orderEffect = BenchmarkPairedStatistics.Median(managedFirstRatios) /
            BenchmarkPairedStatistics.Median(cpythonFirstRatios);
        var managedSpread = BenchmarkPairedStatistics.InterquartileSpread(managedMicroseconds);
        var byteSpread = BenchmarkPairedStatistics.InterquartileSpread(byteMicroseconds);
        var durationQualified = managedMilliseconds.All(
                                    static duration =>
                                        duration >= PythonReQualificationMinimumSampleMilliseconds) &&
                                byteMilliseconds.All(
                                    static duration =>
                                        duration >= PythonReQualificationMinimumSampleMilliseconds);
        var nativeManagedRoute = managedRoute.StartsWith("Utf8Regex/", StringComparison.Ordinal) &&
                                 !managedRoute.Contains("/FallbackRegex", StringComparison.Ordinal);
        PythonReStatusResult engineStatus;
        if (!nativeManagedRoute)
        {
            engineStatus = new PythonReStatusResult(
                PythonRePublicStatus.Unqualified,
                "The managed byte control does not use a native UTF-8 core route.");
        }
        else
        {
            engineStatus = DeriveStatus(
                worktreeQualified,
                placementQualified,
                durationQualified,
                structuredDigestQualified: true,
                ratioLower,
                ratioUpper,
                orderEffect,
                managedSpread,
                byteSpread,
                managedTrivialFraction,
                byteTrivialFraction);
        }

        return new PythonReByteControlEvidence
        {
            EligibilityReason = eligibility.Reason,
            CpythonIterations = iterations,
            CpythonWarmupCalls = warmup.Iterations,
            CpythonWarmupMilliseconds = warmup.ElapsedNanoseconds / 1_000_000d,
            CpythonMedianMicroseconds = byteMedian,
            RatioMedian = Math.Exp(BenchmarkPairedStatistics.Median(logRatios)),
            RatioLower95 = ratioLower,
            RatioUpper95 = ratioUpper,
            OrderEffect = orderEffect,
            ManagedInterquartileSpread = managedSpread,
            CpythonInterquartileSpread = byteSpread,
            ManagedTrivialCallFraction = managedTrivialFraction,
            CpythonTrivialCallFraction = byteTrivialFraction,
            EngineConclusion = nativeManagedRoute
                ? engineStatus.Status.ToString()
                : "NotApplicable",
            EngineConclusionReason = engineStatus.Reason ?? "Qualified byte-control evidence.",
            Samples = Enumerable.Range(0, byteMicroseconds.Count)
                .Select(index => new PythonReByteControlSampleEvidence
                {
                    Order = laneOrders[index].ToString(),
                    CpythonMicroseconds = byteMicroseconds[index],
                    Ratio = ratios[index],
                    CpythonElapsedMilliseconds = byteMilliseconds[index],
                    CpythonProcessCpuMilliseconds = byteProcessCpuMilliseconds[index],
                    CpythonGcCollections = byteGcCollections[index],
                })
                .ToArray(),
            CpythonEmptyLoopMicroseconds = byteEmptyMicroseconds.ToArray(),
            CpythonTrivialCallMicroseconds = byteTrivialMicroseconds.ToArray(),
        };
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
            IncludesResultMaterialization: true,
            new PythonReBenchmarkCoverage(
                "Detailed and scalar projections",
                "Mixed-width captures",
                "Short mixed-width early hit",
                "One",
                0,
                -1,
                -1,
                "DetailedMatch",
                "_sre C Pattern.search + Python detailed projection",
                "OperationExcluded",
                "ManagedFallback",
                "Semantic diagnostic fixture",
                "Composed",
                FirstMilestoneSentinel: false));
        var cases = PythonReBenchmarkCatalog.Cases.Append(supplementaryCase).ToArray();
        using var worker = new CpythonStreamWorker();
        foreach (var benchmarkCase in cases)
        {
            var context = new PythonReBenchmarkContext(benchmarkCase);
            var byteControlEligibility = PythonReBenchmarkCatalog.GetByteControlEligibility(
                benchmarkCase,
                context.InputBytes);
            var expectedChecksum = context.ExecutePythonRe();
            var expectedSemanticDigest = context.ExecutePythonReSemanticDigest();
            var expectedConsumptionToken = context.ExecutePythonReConsumptionToken();
            var prepared = worker.Prepare(
                benchmarkCase,
                context.InputBytes,
                byteControlEligibility.IsEligible);
            if (prepared.Checksum != expectedChecksum ||
                prepared.SemanticDigest != expectedSemanticDigest ||
                prepared.ConsumptionChecksum != expectedConsumptionToken ||
                prepared.ByteControlAvailable != byteControlEligibility.IsEligible ||
                byteControlEligibility.IsEligible &&
                (prepared.ByteControlChecksum != expectedChecksum ||
                 prepared.ByteControlSemanticDigest != expectedSemanticDigest ||
                 prepared.ByteControlConsumptionChecksum != expectedConsumptionToken))
            {
                Console.Error.WriteLine(
                    $"{benchmarkCase.Id}: checksum={expectedChecksum}/{prepared.Checksum}; " +
                    $"semantic digest={expectedSemanticDigest:X16}/{prepared.SemanticDigest:X16}; " +
                    $"consumption={expectedConsumptionToken}/{prepared.ConsumptionChecksum}");
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
        if (snapshot.SchemaVersion != PythonReBenchmarkSchemaVersion ||
            !snapshot.CatalogSha256.Equals(currentCatalogSha256, StringComparison.Ordinal))
        {
            Console.Error.WriteLine(
                $"PythonRe qualification verification requires a current schema-{PythonReBenchmarkSchemaVersion} " +
                "snapshot and catalog provenance.");
            return 1;
        }

        using var worker = new CpythonStreamWorker();
        var currentCpython = worker.Environment;
        var currentManaged = CaptureEnvironment();
        var currentManagedProductSha256 = ComputePythonReManagedProductSha256();
        var pairedEvidenceRows = 0;
        var missingEvidenceRows = 0;
        var statisticallyUnqualifiedRows = 0;
        foreach (var (caseId, measurement) in snapshot.Cases)
        {
            var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
                candidate => candidate.Id.Equals(caseId, StringComparison.Ordinal));
            if (benchmarkCase is null)
            {
                Console.Error.WriteLine($"{caseId}: snapshot row has no current catalog case.");
                return 1;
            }

            var inputBytes = Encoding.UTF8.GetBytes(benchmarkCase.Input);
            var byteControlEligibility = PythonReBenchmarkCatalog.GetByteControlEligibility(
                benchmarkCase,
                inputBytes);
            var currentContext = new PythonReBenchmarkContext(benchmarkCase);
            if (measurement.ByteControlEligible != byteControlEligibility.IsEligible ||
                !measurement.ByteControlReason.Equals(byteControlEligibility.Reason, StringComparison.Ordinal) ||
                !measurement.ComparatorOwner.Equals(
                    PythonReBenchmarkCatalog.GetComparatorOwner(benchmarkCase),
                    StringComparison.Ordinal) ||
                !measurement.ManagedRoute.Equals(currentContext.DescribeManagedRoute(), StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"{caseId}: ownership, route, or byte eligibility is stale.");
                return 1;
            }

            var qualification = measurement.Qualification;
            if (qualification is null)
            {
                Console.Error.WriteLine(
                    $"{caseId}: schema-{PythonReBenchmarkSchemaVersion} row has no qualification state.");
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

                if (!qualification.EngineEvidenceBasis.Equals(
                        "Not engine-comparable",
                        StringComparison.Ordinal) ||
                    !qualification.EngineConclusion.Equals("NotApplicable", StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"{caseId}: a row without paired evidence has an engine conclusion.");
                    return 1;
                }

                missingEvidenceRows++;
                continue;
            }
            var freshness = GetPythonReQualificationFreshness(
                benchmarkCase,
                measurement,
                currentManagedProductSha256,
                currentCpython,
                currentManaged);
            if (freshness != PythonReQualificationFreshness.Current)
            {
                Console.Error.WriteLine(
                    $"{caseId}: " + PythonReQualificationFreshnessEvaluator.Format(freshness) + ".");
                return 1;
            }

            if (evidence.ProtocolVersion != PythonReQualificationProtocolVersion ||
                !evidence.SemanticDigestAlgorithm.Equals(PythonReSemanticDigestAlgorithm, StringComparison.Ordinal) ||
                evidence.Samples.Length is not 9 and not 17 ||
                evidence.ManagedTrivialCallMicroseconds.Length != 3 ||
                evidence.CpythonTrivialCallMicroseconds.Length != 3 ||
                !evidence.WorktreeQualified)
            {
                Console.Error.WriteLine($"{caseId}: paired protocol metadata is not qualification-compatible.");
                return 1;
            }

            var expectedResultContract = GetPythonReResultContract(benchmarkCase);
            if (!evidence.ResultContract.Equals(expectedResultContract, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"{caseId}: paired result contract is stale.");
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
                IsPythonReQualifiedProcessorPolicy(evidence.CpuPolicy),
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

            VerifyPythonReByteControl(
                caseId,
                measurement,
                qualification,
                evidence,
                byteControlEligibility,
                managedMicroseconds);

            var expectedQualificationId = ComputePythonReQualificationId(
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
            if (!evidence.QualificationId.Equals(expectedQualificationId, StringComparison.Ordinal))
            {
                Console.Error.WriteLine($"{caseId}: QualificationId does not match its evidence identity.");
                return 1;
            }

            pairedEvidenceRows++;
            if (qualification.Status.Equals("Unqualified", StringComparison.Ordinal))
            {
                statisticallyUnqualifiedRows++;
            }
        }

        Console.WriteLine(
            $"Verified {pairedEvidenceRows} paired PythonRe evidence rows; " +
            $"missing paired-evidence rows: {missingEvidenceRows}; " +
            $"statistically Unqualified paired rows: {statisticallyUnqualifiedRows}.");
        return 0;
    }

    private static void VerifyPythonReByteControl(
        string caseId,
        PythonReCaseMeasurement measurement,
        PythonReQualificationMeasurement qualification,
        PythonRePairedEvidence evidence,
        PythonReByteControlEligibility eligibility,
        IReadOnlyList<double> managedMicroseconds)
    {
        var byteControl = evidence.ByteControl;
        if (!eligibility.IsEligible)
        {
            if (byteControl is not null ||
                !qualification.EngineEvidenceBasis.Equals(
                    "Not engine-comparable",
                    StringComparison.Ordinal) ||
                !qualification.EngineConclusion.Equals("NotApplicable", StringComparison.Ordinal) ||
                !qualification.EngineConclusionReason.Equals(eligibility.Reason, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"{caseId}: an ineligible byte control has engine evidence.");
            }

            return;
        }

        if (byteControl is null || byteControl.Samples.Length != evidence.Samples.Length ||
            byteControl.CpythonEmptyLoopMicroseconds.Length != 3 ||
            byteControl.CpythonTrivialCallMicroseconds.Length != 3)
        {
            throw new InvalidOperationException($"{caseId}: eligible byte-control evidence is incomplete.");
        }

        var byteMicroseconds = byteControl.Samples
            .Select(static sample => sample.CpythonMicroseconds)
            .ToArray();
        var ratios = byteControl.Samples.Select(static sample => sample.Ratio).ToArray();
        for (var index = 0; index < byteControl.Samples.Length; index++)
        {
            if (!byteControl.Samples[index].Order.Equals(
                    evidence.Samples[index].Order,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"{caseId}: byte-control lane order is stale.");
            }

            VerifyPythonReStatistic(
                $"{caseId} byte sample {index + 1} ratio",
                managedMicroseconds[index] / byteMicroseconds[index],
                ratios[index]);
        }

        var logRatios = ratios.Select(static ratio => Math.Log(ratio)).ToArray();
        var interval = BenchmarkPairedStatistics.BootstrapMedianLogRatio(
            logRatios,
            PythonReQualificationBootstrapSeed,
            PythonReQualificationBootstrapResamples);
        var managedFirstRatios = byteControl.Samples
            .Where(static sample => sample.Order.Equals("ManagedFirst", StringComparison.Ordinal))
            .Select(static sample => sample.Ratio)
            .ToArray();
        var cpythonFirstRatios = byteControl.Samples
            .Where(static sample => sample.Order.Equals("CpythonFirst", StringComparison.Ordinal))
            .Select(static sample => sample.Ratio)
            .ToArray();
        var managedMedian = BenchmarkPairedStatistics.Median(managedMicroseconds);
        var byteMedian = BenchmarkPairedStatistics.Median(byteMicroseconds);
        var ratioLower = Math.Exp(interval.Lower);
        var ratioUpper = Math.Exp(interval.Upper);
        var orderEffect = BenchmarkPairedStatistics.Median(managedFirstRatios) /
            BenchmarkPairedStatistics.Median(cpythonFirstRatios);
        var managedSpread = BenchmarkPairedStatistics.InterquartileSpread(managedMicroseconds);
        var byteSpread = BenchmarkPairedStatistics.InterquartileSpread(byteMicroseconds);
        var managedTrivialFraction =
            BenchmarkPairedStatistics.Median(evidence.ManagedTrivialCallMicroseconds) / managedMedian;
        var byteTrivialFraction =
            BenchmarkPairedStatistics.Median(byteControl.CpythonTrivialCallMicroseconds) / byteMedian;
        VerifyPythonReStatistic(caseId + " byte median", byteMedian, byteControl.CpythonMedianMicroseconds);
        VerifyPythonReStatistic(
            caseId + " byte ratio median",
            Math.Exp(BenchmarkPairedStatistics.Median(logRatios)),
            byteControl.RatioMedian);
        VerifyPythonReStatistic(caseId + " byte interval lower", ratioLower, byteControl.RatioLower95);
        VerifyPythonReStatistic(caseId + " byte interval upper", ratioUpper, byteControl.RatioUpper95);
        VerifyPythonReStatistic(caseId + " byte order", orderEffect, byteControl.OrderEffect);
        VerifyPythonReStatistic(
            caseId + " byte managed spread",
            managedSpread,
            byteControl.ManagedInterquartileSpread);
        VerifyPythonReStatistic(caseId + " byte spread", byteSpread, byteControl.CpythonInterquartileSpread);
        VerifyPythonReStatistic(
            caseId + " byte managed trivial floor",
            managedTrivialFraction,
            byteControl.ManagedTrivialCallFraction);
        VerifyPythonReStatistic(
            caseId + " byte trivial floor",
            byteTrivialFraction,
            byteControl.CpythonTrivialCallFraction);

        var nativeManagedRoute = measurement.ManagedRoute.StartsWith("Utf8Regex/", StringComparison.Ordinal) &&
                                 !measurement.ManagedRoute.Contains("/FallbackRegex", StringComparison.Ordinal);
        var durationsQualified = evidence.Samples.All(sample =>
                                     sample.ManagedElapsedMilliseconds >=
                                     PythonReQualificationMinimumSampleMilliseconds) &&
                                 byteControl.Samples.All(sample =>
                                     sample.CpythonElapsedMilliseconds >=
                                     PythonReQualificationMinimumSampleMilliseconds);
        var expectedEngineStatus = nativeManagedRoute
            ? DeriveStatus(
                evidence.WorktreeQualified,
                IsPythonReQualifiedProcessorPolicy(evidence.CpuPolicy),
                durationsQualified,
                structuredDigestQualified: true,
                ratioLower,
                ratioUpper,
                orderEffect,
                managedSpread,
                byteSpread,
                managedTrivialFraction,
                byteTrivialFraction)
            : new PythonReStatusResult(
                PythonRePublicStatus.Unqualified,
                "The managed byte control does not use a native UTF-8 core route.");
        var expectedConclusion = nativeManagedRoute
            ? expectedEngineStatus.Status.ToString()
            : "NotApplicable";
        var expectedReason = expectedEngineStatus.Reason ?? "Qualified byte-control evidence.";
        if (!qualification.EngineEvidenceBasis.Equals("Byte control", StringComparison.Ordinal) ||
            !qualification.EngineConclusion.Equals(expectedConclusion, StringComparison.Ordinal) ||
            !qualification.EngineConclusionReason.Equals(expectedReason, StringComparison.Ordinal) ||
            !byteControl.EngineConclusion.Equals(expectedConclusion, StringComparison.Ordinal) ||
            !byteControl.EngineConclusionReason.Equals(expectedReason, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"{caseId}: byte-control engine conclusion is stale.");
        }
    }

    private static bool IsPythonReQualifiedProcessorPolicy(string policy) =>
        policy.Equals(PythonReQualificationProcessorPolicy, StringComparison.Ordinal) ||
        policy.Equals("single-highest-efficiency-processor", StringComparison.Ordinal);

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
        ulong expectedSemanticDigest,
        ulong expectedConsumptionToken,
        int minimumCalls = PythonReQualificationMinimumWarmupCalls)
    {
        var batchesRequiredForCallFloor = checked(
            (int)(((long)minimumCalls + iterations - 1) / iterations));
        var maximumBatches = Math.Max(32, batchesRequiredForCallFloor);
        var started = Stopwatch.GetTimestamp();
        var batches = 0;
        do
        {
            var batch = context.MeasurePythonReQualificationBatch(iterations);
            VerifyManagedResult(
                batch,
                expectedChecksum,
                expectedSemanticDigest,
                expectedConsumptionToken,
                iterations,
                "warmup");
            s_sink ^= batch.Checksum;
            batches++;
        }
        while (batches < maximumBatches &&
               (Stopwatch.GetElapsedTime(started).TotalMilliseconds < 100 ||
                (long)batches * iterations < minimumCalls));

        return new PythonReWarmup(
            batches * iterations,
            Stopwatch.GetElapsedTime(started));
    }

    private static PythonReManagedSample MeasureManagedSample(
        PythonReBenchmarkContext context,
        int iterations,
        int expectedChecksum,
        ulong expectedSemanticDigest,
        ulong expectedConsumptionToken,
        Process process)
    {
        var processCpuBefore = process.TotalProcessorTime;
        var collectionsBefore = new[]
        {
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
        };
        var batch = context.MeasurePythonReQualificationBatch(iterations);
        var processCpu = process.TotalProcessorTime - processCpuBefore;
        var collectionsAfter = new[]
        {
            GC.CollectionCount(0),
            GC.CollectionCount(1),
            GC.CollectionCount(2),
        };
        VerifyManagedResult(
            batch,
            expectedChecksum,
            expectedSemanticDigest,
            expectedConsumptionToken,
            iterations,
            "paired sample");
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

    private static PythonReEmptyBatch MeasureManagedTrivialCall(int iterations)
    {
        var checksum = 0;
        var started = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            checksum ^= InvokePythonReTrivial(iteration);
        }

        var elapsed = Stopwatch.GetElapsedTime(started);
        GC.KeepAlive(checksum);
        return new PythonReEmptyBatch(elapsed, checksum);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static int InvokePythonReTrivial(int value) => value;

    private static void VerifyManagedResult(
        PythonReBenchmarkBatch batch,
        int expectedChecksum,
        ulong expectedSemanticDigest,
        ulong expectedConsumptionToken,
        int iterations,
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

        var expectedConsumptionChecksum = checked(expectedConsumptionToken * (ulong)iterations);
        if (batch.ConsumptionChecksum != expectedConsumptionChecksum)
        {
            throw new InvalidOperationException(
                $"PythonRe {phase} consumption checksum {batch.ConsumptionChecksum} " +
                $"does not match {expectedConsumptionChecksum}.");
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
            CatalogSha256 = ComputePythonReCatalogSha256(),
            CatalogCaseIds = GetPythonReCatalogCaseIds(),
            Corpus = CaptureCorpusProvenance(),
            Cases = snapshot.Cases,
            Lifecycle = snapshot.Lifecycle,
            ScalingFamilies = snapshot.ScalingFamilies,
        });
    }

    private static string ComputePythonReCaseDefinitionSha256(
        PythonReBenchmarkCase benchmarkCase,
        byte[] inputBytes)
    {
        var identity = string.Join(
                '\n',
                benchmarkCase.Id,
                benchmarkCase.Pattern,
                ((int)benchmarkCase.Options).ToString(System.Globalization.CultureInfo.InvariantCulture),
                benchmarkCase.Operation.ToString(),
                benchmarkCase.Replacement,
                benchmarkCase.IncludesResultMaterialization.ToString(),
                GetPythonReResultContract(benchmarkCase),
                Convert.ToHexString(SHA256.HashData(inputBytes)));
        if (benchmarkCase.Coverage.StartOffsetInBytes != 0 ||
            benchmarkCase.Coverage.ReplacementCount != -1 ||
            benchmarkCase.Coverage.MaxSplit != -1)
        {
            identity = string.Join(
                '\n',
                identity,
                $"start={benchmarkCase.Coverage.StartOffsetInBytes}",
                $"replacement-count={benchmarkCase.Coverage.ReplacementCount}",
                $"max-split={benchmarkCase.Coverage.MaxSplit}");
        }

        return ComputePythonReSha256(identity);
    }

    private static string ComputeLegacyPythonReCaseDefinitionSha256(
        PythonReBenchmarkCase benchmarkCase,
        byte[] inputBytes) => ComputePythonReSha256(
            string.Join(
                '\n',
                benchmarkCase.Id,
                benchmarkCase.Pattern,
                ((int)benchmarkCase.Options).ToString(CultureInfo.InvariantCulture),
                benchmarkCase.Operation.ToString(),
                benchmarkCase.Replacement,
                benchmarkCase.IncludesResultMaterialization.ToString(),
                Convert.ToHexString(SHA256.HashData(inputBytes))));

    private static string ComputePythonReCatalogSha256()
    {
        var definitions = PythonReBenchmarkCatalog.Cases.Select(benchmarkCase =>
            string.Join(
                ':',
                ComputePythonReCaseDefinitionSha256(
                    benchmarkCase,
                    Encoding.UTF8.GetBytes(benchmarkCase.Input)),
                ComputePythonReCoverageSha256(benchmarkCase.Coverage)));
        return ComputePythonReSha256(string.Join('\n', definitions));
    }

    private static string[] GetPythonReCatalogCaseIds() =>
        PythonReBenchmarkCatalog.Cases.Select(static benchmarkCase => benchmarkCase.Id).ToArray();

    private static string ComputePythonReCoverageSha256(PythonReBenchmarkCoverage coverage) =>
        ComputePythonReSha256(
            string.Join(
                '\n',
                coverage.Section,
                coverage.FeatureFamily,
                coverage.InputShape,
                coverage.ExpectedResultCardinality,
                coverage.StartOffsetInBytes.ToString(CultureInfo.InvariantCulture),
                coverage.ReplacementCount.ToString(CultureInfo.InvariantCulture),
                coverage.MaxSplit.ToString(CultureInfo.InvariantCulture),
                coverage.ProjectionKind,
                coverage.ComparatorOwner,
                coverage.ByteControlExpectation,
                coverage.IntendedManagedRouteClass,
                coverage.CorpusProvenance,
                coverage.ClaimClass,
                coverage.FirstMilestoneSentinel.ToString()));

    private static string ComputePythonReQualificationId(
        string caseDefinitionSha256,
        string managedProductSha256,
        string managedOperationProtocolSha256,
        string cpythonOperationProtocolSha256,
        string sharedProtocolSha256,
        PythonReBenchmarkEnvironment managed,
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
                managedProductSha256,
                managedOperationProtocolSha256,
                cpythonOperationProtocolSha256,
                sharedProtocolSha256,
                managed.Runtime,
                managed.OperatingSystem,
                managed.Processor,
                cpython.VersionDetail,
                string.Join('|', cpython.Git),
                cpython.ExecutableSha256,
                cpython.RuntimeLibrarySha256,
                cpuPolicy,
                cpuAffinityMask,
                cpuEfficiencyClass?.ToString(System.Globalization.CultureInfo.InvariantCulture) ??
                    "unavailable",
                cpythonFirst ? "CPython" : "PythonRe",
                samples.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    private static string ComputeLegacyPythonReQualificationId(
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
                PythonReQualificationProtocolVersion.ToString(CultureInfo.InvariantCulture),
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
                cpuEfficiencyClass?.ToString(CultureInfo.InvariantCulture) ?? "unavailable",
                cpythonFirst ? "CPython" : "PythonRe",
                samples.ToString(CultureInfo.InvariantCulture)));

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

        internal CpythonStreamResponse Prepare(
            PythonReBenchmarkCase benchmarkCase,
            byte[] inputBytes,
            bool enableByteControl) => Send(
            new CpythonStreamCommand
            {
                ProtocolVersion = PythonReQualificationProtocolVersion,
                Kind = "Prepare",
                Pattern = benchmarkCase.Pattern,
                Options = (int)benchmarkCase.Options,
                Operation = benchmarkCase.Operation.ToString(),
                InputBase64 = Convert.ToBase64String(inputBytes),
                Replacement = benchmarkCase.Replacement,
                StartOffsetInBytes = benchmarkCase.Coverage.StartOffsetInBytes,
                ReplacementCount = benchmarkCase.Coverage.ReplacementCount,
                MaxSplit = benchmarkCase.Coverage.MaxSplit,
                EnableByteControl = enableByteControl,
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
            int minimumCalls,
            int maximumBatches) => Send(
                new CpythonStreamCommand
                {
                    ProtocolVersion = PythonReQualificationProtocolVersion,
                    Kind = "Warm",
                    Lane = lane.ToString(),
                    Iterations = iterations,
                    MinimumMilliseconds = minimumMilliseconds,
                    MinimumCalls = minimumCalls,
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

        internal CpythonStreamResponse MeasureLifecycle(
            string pattern,
            string input,
            int options,
            int iterations,
            int samples) => Send(
            new CpythonStreamCommand
            {
                ProtocolVersion = PythonReQualificationProtocolVersion,
                Kind = "MeasureLifecycle",
                Pattern = pattern,
                InputBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(input)),
                Options = options,
                Iterations = iterations,
                Samples = samples,
            },
            "LifecycleMeasured");

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
    BoundTrivialCall = 2,
    Bytes = 3,
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
    public int? StartOffsetInBytes { get; init; }
    public int? ReplacementCount { get; init; }
    public int? MaxSplit { get; init; }
    public bool EnableByteControl { get; init; }
    public string? Lane { get; init; }
    public int? Iterations { get; init; }
    public long? TargetNanoseconds { get; init; }
    public int? MaximumIterations { get; init; }
    public int? MinimumMilliseconds { get; init; }
    public int? MinimumCalls { get; init; }
    public int? MaximumBatches { get; init; }
    public int? Samples { get; init; }
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
    public ulong ConsumptionChecksum { get; init; }
    public bool ByteControlAvailable { get; init; }
    public int? ByteControlChecksum { get; init; }
    public ulong? ByteControlSemanticDigest { get; init; }
    public ulong? ByteControlConsumptionChecksum { get; init; }
    public PythonReCpythonLifecycleResponse? Lifecycle { get; init; }
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
