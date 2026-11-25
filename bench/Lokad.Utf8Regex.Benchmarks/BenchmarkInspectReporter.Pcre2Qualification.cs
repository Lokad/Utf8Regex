using System.Diagnostics;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private const int Pcre2QualificationBootstrapSeed = 24301;
    private const int Pcre2QualificationBootstrapResamples = 10_000;
    // Caller capture slots change managed Match bindings; old paired bytes stay historical.
    private const int Pcre2QualificationProtocolVersion = 12;
    private const int Pcre2QualificationAllocationProbeSamples = 5;
    private const int Pcre2QualificationInterleaveSlices = 8;
    private const double Pcre2QualificationTargetSampleMilliseconds = 40;
    private const double Pcre2QualificationMinimumSampleMilliseconds = 20;
    private const double Pcre2QualificationCalibrationMinimumMilliseconds = 30;
    private const double Pcre2QualificationCalibrationMaximumMilliseconds = 50;

    private static (Pcre2NativeComparisonStatus Status, string? Reason) DerivePairedStatus(
        double lowerLogRatio,
        double upperLogRatio,
        double orderEffectRatio,
        double managedInterquartileSpread,
        double comparatorInterquartileSpread,
        bool sampleDurationsQualified)
    {
        if (!sampleDurationsQualified)
        {
            return (
                Pcre2NativeComparisonStatus.Unqualified,
                $"At least one paired lane sample was shorter than {Pcre2QualificationMinimumSampleMilliseconds:F0} ms.");
        }

        if (managedInterquartileSpread > Pcre2FastLaneMaximumInterquartileSpread ||
            comparatorInterquartileSpread > Pcre2FastLaneMaximumInterquartileSpread)
        {
            return (
                Pcre2NativeComparisonStatus.Inconclusive,
                $"Lane interquartile spreads are {managedInterquartileSpread:F3}/{comparatorInterquartileSpread:F3}; " +
                $"the maximum is {Pcre2FastLaneMaximumInterquartileSpread:F2}.");
        }

        if (orderEffectRatio is < 0.98 or > 1.02)
        {
            return (
                Pcre2NativeComparisonStatus.Inconclusive,
                $"Lane-order median ratios differ by {Math.Abs(orderEffectRatio - 1) * 100:F2}%.");
        }

        var lowerRatio = Math.Exp(lowerLogRatio);
        var upperRatio = Math.Exp(upperLogRatio);
        if (upperRatio < 0.98)
        {
            return (Pcre2NativeComparisonStatus.ManagedFaster, null);
        }

        if (lowerRatio > 1.02)
        {
            return (Pcre2NativeComparisonStatus.NativeFaster, null);
        }

        if (lowerRatio >= 0.98 && upperRatio <= 1.02)
        {
            return (Pcre2NativeComparisonStatus.Equivalent, null);
        }

        return (
            Pcre2NativeComparisonStatus.Inconclusive,
            "The paired 95% interval crosses a Status decision boundary.");
    }

    public static int RunInvalidateStalePcre2Qualifications()
    {
        var snapshot = LoadPcre2BenchmarkSnapshot();
        snapshot.PcreNetNativeBaseline = CapturePcreNetNativeBaselineDependency();
        SavePcre2BenchmarkSnapshot(snapshot);
        Console.WriteLine("Invalidated PCRE2 qualifications recorded under an older protocol.");
        return 0;
    }

    public static int RunQualifyPcre2ComparatorCase(
        string caseId,
        string? samplesText,
        bool comparatorFirst = false)
        => RunPcre2Qualification(GetPcre2SectionsForCase(caseId).ToArray(), caseId, samplesText, comparatorFirst);

    public static int RunRefreshPcre2Benchmarks(string? sectionsText, string? samplesText)
        => RunPcre2Qualification(ParsePcre2Sections(sectionsText), null, samplesText, comparatorFirst: false);

    private static int RunPcre2Qualification(
        IReadOnlyList<Pcre2BenchmarkSection> sections, string? caseId, string? samplesText, bool comparatorFirst)
    {
#if DEBUG
        Console.Error.WriteLine("PCRE2 qualification requires a Release build.");
        return 1;
#else
        var samples = string.IsNullOrWhiteSpace(samplesText) ? 9 : ParseSamples(samplesText);
        if (samples < 9)
        {
            Console.Error.WriteLine("PCRE2 qualification requires at least nine paired samples.");
            return 1;
        }

        var unexpectedWorktreeState = RunGit(
            "status",
            "--porcelain=v1",
            "--untracked-files=all",
            "--",
            ".",
            // A combined refresh dirties other profiles' generated outputs too.
            // Code, build inputs, corpus data and unknown files remain gated.
            ":(exclude)README.md",
            ":(exclude)bench/Snapshots",
            ":(exclude)src/Lokad.Utf8Regex.PythonRe/README.md",
            ":(exclude)src/Lokad.Utf8Regex.Pcre2/README.md",
            ":(exclude)src/Lokad.Utf8Regex.Posix/README.md");
        if (unexpectedWorktreeState is null)
        {
            Console.Error.WriteLine("Could not verify the worktree before PCRE2 qualification.");
            return 1;
        }

        if (!string.IsNullOrWhiteSpace(unexpectedWorktreeState))
        {
            Console.Error.WriteLine("PCRE2 qualification requires a clean source worktree.");
            Console.Error.WriteLine(unexpectedWorktreeState);
            return 1;
        }

        var startupProcessorCount = Environment.ProcessorCount;
        using var processorSet = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        Console.WriteLine($"Runtime startup processor count: {startupProcessorCount}");
        Console.WriteLine($"Qualification CPU set: {processorSet.Description}");
        Console.WriteLine(
            $"Initial paired lane: {(comparatorFirst ? "PCRE.NET / PCRE2 NFA" : "Utf8Pcre2")}");
        Console.WriteLine(
            "Input contract: both lanes consume caller-valid, unchanged UTF-8. " +
            $"The native UTF-mode matcher uses {PcreNetNativeBenchmarkBaseline.MatchOptions}.");
        var environment = CaptureBenchmarkEnvironment();
        var snapshot = LoadPcre2BenchmarkSnapshot();
        snapshot.SchemaVersion = Pcre2BenchmarkSchemaVersion;
        var comparatorDependency = CapturePcreNetNativeBaselineDependency();
        snapshot.PcreNetNativeBaseline = comparatorDependency;
        var cases = caseId is null
            ? Utf8Pcre2BenchmarkCatalog.GetAllCases().Where(item => sections.Any(section => IsCaseInPcre2Section(item, section)))
                .OrderBy(static item => item.Id, StringComparer.Ordinal).ToArray()
            : [Utf8Pcre2BenchmarkCatalog.Get(caseId)];
        foreach (var benchmarkCase in cases)
        {
            QualifyPcre2Case(snapshot, comparatorDependency, sections, benchmarkCase, environment,
                processorSet, samples, comparatorFirst);
            SavePcre2BenchmarkSnapshot(snapshot, publishPresentation: false);
        }
        if (caseId is null)
        {
            foreach (var section in sections)
            {
                var rows = GetOrAddPcre2SnapshotSection(snapshot, section).Cases;
                var currentIds = cases.Where(item => IsCaseInPcre2Section(item, section))
                    .Select(static item => item.Id).ToHashSet(StringComparer.Ordinal);
                foreach (var stale in rows.Keys.Where(id => !currentIds.Contains(id)).ToArray()) rows.Remove(stale);
            }
        }
        SavePcre2BenchmarkSnapshot(snapshot);
        Console.WriteLine(caseId is null
            ? $"Refreshed PCRE2 benchmark sections: {string.Join(", ", sections.Select(GetPcre2SectionToken))}"
            : $"Qualified PCRE.NET / PCRE2 NFA benchmark case: {caseId}");
        return 0;
#endif
    }

    private static void QualifyPcre2Case(Pcre2BenchmarkSnapshot snapshot, PcreNetNativeBaselineDependencyJson comparatorDependency,
        IReadOnlyList<Pcre2BenchmarkSection> sections, Utf8Pcre2BenchmarkCase benchmarkCase, BenchmarkEnvironmentJson environment,
        BenchmarkProcessorScope processorSet, int samples, bool comparatorFirst)
    {
        var caseId = benchmarkCase.Id;
        var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);

        foreach (var section in sections)
        {
            if (!IsCaseInPcre2Section(benchmarkCase, section)) continue;
            var sectionName = GetPcre2SectionToken(section);
            Console.WriteLine($"Measuring {sectionName}: {caseId}");
            var sectionSnapshot = GetOrAddPcre2SnapshotSection(snapshot, section);
            var operation = GetPcre2SectionRequirements(section).Operation;
            Func<int> managedAction = () => ExecutePcre2SnapshotOperation(context.Utf8Pcre2Regex, context, operation);
            if (!PcreNetNativeBenchmarkBaseline.Supports(operation))
            {
                RecordExcluded("PCRE.NET does not expose equivalent UTF-8 span replacement output.");
                continue;
            }

            try
            {
                using var comparator = new PcreNetNativeBenchmarkBaseline(benchmarkCase);
                var expected = ComputePcre2ManagedResultChecksum(
                    context.Utf8Pcre2Regex,
                    context.InputBytes,
                    operation);
                var actual = comparator.ComputeChecksum(operation);
                if (actual != expected)
                {
                    RecordExcluded($"Structured checksum mismatch: managed={expected}, comparator={actual}.");
                    continue;
                }

                Func<int> comparatorAction = () => comparator.Execute(operation);
                var managedWarmup = WarmPcre2QualificationLane(managedAction);
                var comparatorWarmup = WarmPcre2QualificationLane(comparatorAction);
                var managedBatchCount = CalibratePcre2QualificationBatch(managedAction);
                var comparatorBatchCount = CalibratePcre2QualificationBatch(comparatorAction);
                var pair = MeasureSection();

                var measurement = CreatePcre2CaseMeasurement(benchmarkCase, context.InputBytes.Length,
                    operation, managedBatchCount, pair.ManagedMedianMicroseconds, pair.ManagedAllocatedBytesPerOperation);
                measurement.MeasuredAtUtc = pair.MeasuredAtUtc;
                measurement.Environment = environment;
                measurement.PcreNetNativeMeasuredAtUtc = pair.MeasuredAtUtc;
                measurement.PcreNetNativeEnvironment = environment;
                measurement.PcreNetNativeEffectiveIterations = comparatorBatchCount;
                measurement.PcreNetNativeMatchOptions = PcreNetNativeBenchmarkBaseline.MatchOptions.ToString();
                measurement.PcreNetNative = pair.ComparatorMedianMicroseconds;
                measurement.PcreNetNativePair = pair;
                measurement.PcreNetNativeStatus = pair.Status;
                measurement.PcreNetNativeStatusReason = pair.StatusReason;
                sectionSnapshot.Cases[caseId] = measurement;

                var statusLabel = pair.Status switch
                {
                    Pcre2NativeComparisonStatus.Unqualified => "Unqualified",
                    Pcre2NativeComparisonStatus.Excluded => "Excluded",
                    Pcre2NativeComparisonStatus.Inconclusive => "Inconclusive",
                    Pcre2NativeComparisonStatus.Equivalent => "Equivalent",
                    Pcre2NativeComparisonStatus.ManagedFaster => "Managed faster",
                    Pcre2NativeComparisonStatus.NativeFaster => "Native faster",
                    _ => throw new ArgumentOutOfRangeException(nameof(pair.Status)),
                };
                Console.WriteLine(
                    $"{sectionName}: {statusLabel}; " +
                    $"R={pair.RatioMedian:F3} [{pair.RatioLower95:F3}, {pair.RatioUpper95:F3}]; " +
                    $"managed={pair.ManagedMedianMicroseconds:F3} us; comparator={pair.ComparatorMedianMicroseconds:F3} us");

                Pcre2PairedMeasurementJson MeasureSection()
                {
                    var laneOrders = new List<Pcre2PairLaneOrder>(samples);
                    var managedMicroseconds = new List<double>(samples);
                    var comparatorMicroseconds = new List<double>(samples);
                    var managedMilliseconds = new List<double>(samples);
                    var comparatorMilliseconds = new List<double>(samples);
                    var ratios = new List<double>(samples);
                    var sinks = 0;

                    for (var sample = 0; sample < samples; sample++)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                        GC.Collect();

                        var order = (sample + (comparatorFirst ? 1 : 0)) % 2 == 0
                            ? Pcre2PairLaneOrder.ManagedFirst
                            : Pcre2PairLaneOrder.ComparatorFirst;
                        var interleaved = MeasurePcre2QualificationPair(
                            managedAction,
                            managedBatchCount,
                            comparatorAction,
                            comparatorBatchCount,
                            order);
                        var managedBatch = interleaved.Managed;
                        var comparatorBatch = interleaved.Comparator;

                        var managedPerOperation = managedBatch.Elapsed.TotalMicroseconds / managedBatchCount;
                        var comparatorPerOperation = comparatorBatch.Elapsed.TotalMicroseconds / comparatorBatchCount;
                        laneOrders.Add(order);
                        managedMicroseconds.Add(managedPerOperation);
                        comparatorMicroseconds.Add(comparatorPerOperation);
                        managedMilliseconds.Add(managedBatch.Elapsed.TotalMilliseconds);
                        comparatorMilliseconds.Add(comparatorBatch.Elapsed.TotalMilliseconds);
                        ratios.Add(managedPerOperation / comparatorPerOperation);
                        sinks ^= managedBatch.Sink ^ comparatorBatch.Sink;
                    }

                    GC.KeepAlive(sinks);
                    var logRatios = ratios.Select(static ratio => Math.Log(ratio)).ToArray();
                    var bootstrap = BenchmarkPairedStatistics.BootstrapMedianLogRatio(
                        logRatios,
                        Pcre2QualificationBootstrapSeed,
                        Pcre2QualificationBootstrapResamples);
                    var ratioMedian = Math.Exp(BenchmarkPairedStatistics.Median(logRatios));
                    var managedFirstRatios = laneOrders
                        .Select((order, index) => (Order: order, Ratio: ratios[index]))
                        .Where(static sample => sample.Order == Pcre2PairLaneOrder.ManagedFirst)
                        .Select(static sample => sample.Ratio)
                        .ToArray();
                    var comparatorFirstRatios = laneOrders
                        .Select((order, index) => (Order: order, Ratio: ratios[index]))
                        .Where(static sample => sample.Order == Pcre2PairLaneOrder.ComparatorFirst)
                        .Select(static sample => sample.Ratio)
                        .ToArray();
                    var orderEffectRatio = BenchmarkPairedStatistics.Median(managedFirstRatios) /
                        BenchmarkPairedStatistics.Median(comparatorFirstRatios);
                    var managedInterquartileSpread = BenchmarkPairedStatistics.InterquartileSpread(
                        managedMicroseconds);
                    var comparatorInterquartileSpread = BenchmarkPairedStatistics.InterquartileSpread(
                        comparatorMicroseconds);
                    var sampleDurationsQualified = managedMilliseconds.All(
                                                       static duration => duration >= Pcre2QualificationMinimumSampleMilliseconds) &&
                                                   comparatorMilliseconds.All(
                                                       static duration => duration >= Pcre2QualificationMinimumSampleMilliseconds);
                    var (status, statusReason) = DerivePairedStatus(
                        bootstrap.Lower,
                        bootstrap.Upper,
                        orderEffectRatio,
                        managedInterquartileSpread,
                        comparatorInterquartileSpread,
                        sampleDurationsQualified);
                    var excesses = managedMicroseconds
                        .Select((value, index) => value - comparatorMicroseconds[index])
                        .ToArray();
                    var managedMedianMicroseconds = BenchmarkPairedStatistics.Median(managedMicroseconds);
                    var comparatorMedianMicroseconds = BenchmarkPairedStatistics.Median(comparatorMicroseconds);
                    var managedAllocationProbeIterations = GetPcre2QualificationAllocationProbeIterations(
                        managedMedianMicroseconds);
                    var comparatorAllocationProbeIterations = GetPcre2QualificationAllocationProbeIterations(
                        comparatorMedianMicroseconds);
                    var managedAllocation = MeasurePcre2QualificationAllocation(
                        managedAllocationProbeIterations,
                        managedAction);
                    var comparatorAllocation = MeasurePcre2QualificationAllocation(
                        comparatorAllocationProbeIterations,
                        comparatorAction);
                    var operations = context.Utf8Pcre2Regex.DebugCompiledProgram.Operations;
                    var managedRoute = operation switch
                    {
                        Utf8Pcre2BenchmarkOperation.IsMatch => operations.IsMatch.Kind,
                        Utf8Pcre2BenchmarkOperation.Count => operations.Count.Kind,
                        Utf8Pcre2BenchmarkOperation.EnumerateMatches => operations.Enumerate.Kind,
                        Utf8Pcre2BenchmarkOperation.Replace => operations.Replace.Kind,
                        _ => throw new ArgumentOutOfRangeException(nameof(operation))
                    };

                    return new Pcre2PairedMeasurementJson
                    {
                        PairId = Guid.NewGuid().ToString("N"),
                        ProtocolVersion = Pcre2QualificationProtocolVersion,
                        CaseId = caseId,
                        Section = sectionName,
                        Operation = operation.ToString(),
                        StartOffsetInBytes = 0,
                        MeasuredAtUtc = DateTimeOffset.UtcNow,
                        Environment = environment,
                        ComparatorPackageId = PcreNetNativeBenchmarkBaseline.PackageId,
                        ComparatorPackageVersion = PcreNetNativeBenchmarkBaseline.PackageVersion,
                        ComparatorPackageSha512 = PcreNetNativeBenchmarkBaseline.PackageSha512,
                        ComparatorEngineVersion = PcreNetNativeBenchmarkBaseline.NativePcre2Version,
                        ComparatorProfile = comparatorDependency.Profile,
                        ComparatorBuildFingerprint = comparatorDependency.BuildFingerprint,
                        ComparatorPlanFingerprint = comparator.CapturePlanFingerprint(),
                        ManagedWorkspaceContract = CaptureManagedPcre2WorkspaceContract(),
                        ComparatorWorkspaceContract = comparatorDependency.WorkspaceContract,
                        ProcessorSetPolicy = processorSet.Policy,
                        ProcessorAffinityMask = processorSet.AffinityMask,
                        ProcessorEfficiencyClass = processorSet.EfficiencyClass,
                        WorktreeQualified = true,
                        SampleCount = samples,
                        ManagedBatchCount = managedBatchCount,
                        ComparatorBatchCount = comparatorBatchCount,
                        ManagedWarmupIterations = managedWarmup.Iterations,
                        ComparatorWarmupIterations = comparatorWarmup.Iterations,
                        ManagedWarmupMilliseconds = managedWarmup.Elapsed.TotalMilliseconds,
                        ComparatorWarmupMilliseconds = comparatorWarmup.Elapsed.TotalMilliseconds,
                        ManagedAllocationProbeIterations = managedAllocationProbeIterations,
                        ComparatorAllocationProbeIterations = comparatorAllocationProbeIterations,
                        ManagedAllocatedBytesPerOperation = managedAllocation.BytesPerOperation,
                        ComparatorManagedAllocatedBytesPerOperation = comparatorAllocation.BytesPerOperation,
                        ManagedAllocationSampleBytes = managedAllocation.SampleBytes,
                        ComparatorAllocationSampleBytes = comparatorAllocation.SampleBytes,
                        LaneOrders = laneOrders,
                        ManagedSampleMicroseconds = managedMicroseconds,
                        ComparatorSampleMicroseconds = comparatorMicroseconds,
                        ManagedSampleMilliseconds = managedMilliseconds,
                        ComparatorSampleMilliseconds = comparatorMilliseconds,
                        PairedRatios = ratios,
                        ManagedMedianMicroseconds = managedMedianMicroseconds,
                        ComparatorMedianMicroseconds = comparatorMedianMicroseconds,
                        RatioMedian = ratioMedian,
                        RatioLower95 = Math.Exp(bootstrap.Lower),
                        RatioUpper95 = Math.Exp(bootstrap.Upper),
                        ExcessMedianMicroseconds = BenchmarkPairedStatistics.Median(excesses),
                        OrderEffectRatio = orderEffectRatio,
                        ManagedInterquartileSpreadRatio = managedInterquartileSpread,
                        ComparatorInterquartileSpreadRatio = comparatorInterquartileSpread,
                        BootstrapSeed = Pcre2QualificationBootstrapSeed,
                        BootstrapResamples = Pcre2QualificationBootstrapResamples,
                        ResultChecksum = expected.ToString(),
                        ManagedRoute = managedRoute.ToString(),
                        ManagedPlan = context.Utf8Pcre2Regex.DebugDescribeExecutionPlan(),
                        Status = status,
                        StatusReason = statusReason
                    };
                }


            }
            catch (PCRE.PcreException exception)
            {
                RecordExcluded($"PCRE.NET / PCRE2 NFA rejected the mapped profile: {exception.Message}");
            }

            void RecordExcluded(string reason)
            {
                var maximum = ParsePcre2SnapshotIterations(benchmarkCase, section, 1);
                var comparison = MeasureComparisonLanes(1, maximum, 5, [managedAction], ["Utf8Pcre2"]);
                var iterations = comparison.Iterations[0];
                var measurement = CreatePcre2CaseMeasurement(benchmarkCase, context.InputBytes.Length, operation,
                    iterations, comparison.Medians[0], MeasureAllocatedBytesPerInvocation(Math.Min(iterations, 4096), managedAction));
                measurement.MeasuredAtUtc = DateTimeOffset.UtcNow;
                measurement.Environment = environment;
                measurement.MeasurementProtocol = "ManagedOnlyV1";
                measurement.LaneNames = ["Utf8Pcre2"];
                measurement.LaneIterations = comparison.Iterations;
                measurement.SampleMicroseconds = comparison.RawSamples;
                measurement.PhaseMilliseconds = comparison.Phases;
                measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Excluded;
                measurement.PcreNetNativeStatusReason = reason;
                measurement.PcreNetNativeUnavailableReason = reason;
                sectionSnapshot.Cases[caseId] = measurement;
                Console.WriteLine($"{sectionName}: Excluded; {reason}");
            }
        }
    }

    private static int GetPcre2QualificationAllocationProbeIterations(double microsecondsPerOperation)
    {
        const double targetProbeMicroseconds = 250_000;
        const int maximumProbeIterations = 64;
        return (int)Math.Clamp(
            Math.Floor(targetProbeMicroseconds / Math.Max(microsecondsPerOperation, 0.001)),
            1,
            maximumProbeIterations);
    }

    private static Pcre2QualificationAllocationMeasurement MeasurePcre2QualificationAllocation(
        int iterations,
        Func<int> action)
    {
        _ = action();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var samples = new List<long>(Pcre2QualificationAllocationProbeSamples);
        var sink = 0;
        for (var sample = 0; sample < Pcre2QualificationAllocationProbeSamples; sample++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var iteration = 0; iteration < iterations; iteration++)
            {
                sink ^= action();
            }

            samples.Add(GC.GetAllocatedBytesForCurrentThread() - before);
        }

        GC.KeepAlive(sink);
        var orderedSamples = samples.Order().ToArray();
        return new Pcre2QualificationAllocationMeasurement(
            orderedSamples[orderedSamples.Length / 2] / iterations,
            samples);
    }

    private static Pcre2BenchmarkWorkspaceContract CaptureManagedPcre2WorkspaceContract() => new()
    {
        StateHolder = "operation-local managed state",
        Lifetime = "The compiled regex is reused; each public invocation or global enumerator owns its transient state and returns rented storage on disposal or completion.",
        ConcurrencyContract = "The compiled regex may be invoked concurrently because capture, progress, timeout, and workspace state is invocation-local.",
        RetainedMemoryContract = "The regex retains its immutable compiled plan, not invocation workspace; shared managed pools may retain returned arrays."
    };

    private static Pcre2QualificationWarmup WarmPcre2QualificationLane(Func<int> action)
    {
        const int minimumIterations = 64;
        const double minimumMilliseconds = 750;
        const double maximumMillisecondsForCallFloor = 1500;
        var sink = 0;
        var iterations = 0;
        var start = Stopwatch.GetTimestamp();
        do
        {
            // Do not turn a 1-second public operation into a 64-second warmup.
            // Keep the call floor for short operations and record actual work;
            // sample-duration, lane-order and stability gates stay unchanged.
            sink ^= MeasurePcre2QualificationBatch(action, 1).Sink;
            iterations++;
        }
        while (Stopwatch.GetElapsedTime(start).TotalMilliseconds < minimumMilliseconds ||
               iterations < minimumIterations &&
               Stopwatch.GetElapsedTime(start).TotalMilliseconds < maximumMillisecondsForCallFloor);

        var elapsed = Stopwatch.GetElapsedTime(start);
        GC.KeepAlive(sink);
        return new Pcre2QualificationWarmup(iterations, elapsed);
    }

    private static int CalibratePcre2QualificationBatch(Func<int> action)
    {
        const double minimumProbeMilliseconds = 2;
        const int maximumBatchCount = 100_000_000;
        var batchCount = 1;
        var probe = MeasurePcre2QualificationBatch(action, batchCount);
        while (probe.Elapsed.TotalMilliseconds < minimumProbeMilliseconds && batchCount < maximumBatchCount)
        {
            var scale = probe.Elapsed.TotalMilliseconds <= 0
                ? 32
                : (int)Math.Clamp(
                    Math.Ceiling(minimumProbeMilliseconds / probe.Elapsed.TotalMilliseconds),
                    2,
                    32);
            batchCount = (int)Math.Min((long)batchCount * scale, maximumBatchCount);
            probe = MeasurePcre2QualificationBatch(action, batchCount);
        }

        GC.KeepAlive(probe.Sink);
        if (probe.Elapsed.TotalMilliseconds <= 0)
        {
            return batchCount;
        }

        batchCount = (int)Math.Clamp(
            Math.Round(batchCount * Pcre2QualificationTargetSampleMilliseconds / probe.Elapsed.TotalMilliseconds),
            1,
            maximumBatchCount);
        const int confirmationAttempts = 2;
        for (var attempt = 0; attempt < confirmationAttempts; attempt++)
        {
            var confirmation = MeasurePcre2QualificationBatch(action, batchCount);
            GC.KeepAlive(confirmation.Sink);
            if (confirmation.Elapsed.TotalMilliseconds is >= Pcre2QualificationCalibrationMinimumMilliseconds and
                <= Pcre2QualificationCalibrationMaximumMilliseconds)
            {
                break;
            }

            if (confirmation.Elapsed.TotalMilliseconds <= 0)
            {
                break;
            }

            var adjustedBatchCount = (int)Math.Clamp(
                Math.Round(
                    batchCount *
                    Pcre2QualificationTargetSampleMilliseconds /
                    confirmation.Elapsed.TotalMilliseconds),
                1,
                maximumBatchCount);
            if (adjustedBatchCount == batchCount)
            {
                break;
            }

            batchCount = adjustedBatchCount;
        }

        return batchCount;
    }

    private static Pcre2QualificationBatch MeasurePcre2QualificationBatch(Func<int> action, int batchCount)
    {
        var sink = 0;
        var start = Stopwatch.GetTimestamp();
        for (var iteration = 0; iteration < batchCount; iteration++)
        {
            sink ^= action();
        }

        return new Pcre2QualificationBatch(Stopwatch.GetElapsedTime(start), sink);
    }

    private static Pcre2QualificationPair MeasurePcre2QualificationPair(
        Func<int> managedAction,
        int managedBatchCount,
        Func<int> comparatorAction,
        int comparatorBatchCount,
        Pcre2PairLaneOrder firstLane)
    {
        var sliceCount = Math.Min(
            Pcre2QualificationInterleaveSlices,
            Math.Min(managedBatchCount, comparatorBatchCount));
        var managed = new Pcre2QualificationBatch(TimeSpan.Zero, 0);
        var comparator = new Pcre2QualificationBatch(TimeSpan.Zero, 0);

        for (var slice = 0; slice < sliceCount; slice++)
        {
            var managedSliceCount = GetPcre2QualificationSliceCount(managedBatchCount, sliceCount, slice);
            var comparatorSliceCount = GetPcre2QualificationSliceCount(comparatorBatchCount, sliceCount, slice);
            if (firstLane == Pcre2PairLaneOrder.ManagedFirst)
            {
                managed = Add(managed, MeasurePcre2QualificationBatch(managedAction, managedSliceCount));
                comparator = Add(comparator, MeasurePcre2QualificationBatch(comparatorAction, comparatorSliceCount));
            }
            else
            {
                comparator = Add(comparator, MeasurePcre2QualificationBatch(comparatorAction, comparatorSliceCount));
                managed = Add(managed, MeasurePcre2QualificationBatch(managedAction, managedSliceCount));
            }
        }

        return new Pcre2QualificationPair(managed, comparator);

        static Pcre2QualificationBatch Add(
            Pcre2QualificationBatch left,
            Pcre2QualificationBatch right)
            => new(left.Elapsed + right.Elapsed, left.Sink ^ right.Sink);
    }

    private static int GetPcre2QualificationSliceCount(int total, int slices, int slice)
        => total / slices + (slice < total % slices ? 1 : 0);

    private readonly record struct Pcre2QualificationBatch(TimeSpan Elapsed, int Sink);

    private readonly record struct Pcre2QualificationPair(
        Pcre2QualificationBatch Managed,
        Pcre2QualificationBatch Comparator);

    private readonly record struct Pcre2QualificationWarmup(int Iterations, TimeSpan Elapsed);

    private readonly record struct Pcre2QualificationAllocationMeasurement(
        long BytesPerOperation,
        List<long> SampleBytes);

}
