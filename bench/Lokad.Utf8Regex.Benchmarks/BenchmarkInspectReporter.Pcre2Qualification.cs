using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private const int Pcre2QualificationBootstrapSeed = 24301;
    private const int Pcre2QualificationBootstrapResamples = 10_000;
    private const int Pcre2QualificationProtocolVersion = 5;
    private const int Pcre2QualificationInterleaveSlices = 8;
    private const double Pcre2QualificationTargetSampleMilliseconds = 35;
    private const double Pcre2QualificationMinimumSampleMilliseconds = 20;

    public static int RunQualifyPcre2ComparatorCase(
        string caseId,
        string? samplesText,
        bool comparatorFirst = false)
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
            ":(exclude)PCRE2.Benchmarks.json",
            ":(exclude)src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md",
            ":(exclude)UTF8REGEX-PERFORMANCE-ROADMAP.md");
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

        using var processorSet = Pcre2QualificationProcessorSet.Enter();
        Console.WriteLine($"Qualification CPU set: {processorSet.Description}");
        Console.WriteLine(
            $"Initial paired lane: {(comparatorFirst ? "PCRE.NET / PCRE2 NFA" : "Utf8Pcre2")}");
        var environment = CaptureBenchmarkEnvironment();
        var snapshot = LoadPcre2BenchmarkSnapshot();
        snapshot.SchemaVersion = Pcre2BenchmarkSchemaVersion;
        var comparatorDependency = CapturePcreNetNativeBaselineDependency();
        snapshot.PcreNetNativeBaseline = comparatorDependency;
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);

        foreach (var section in GetPcre2SectionsForCase(caseId))
        {
            var sectionName = GetPcre2SectionToken(section);
            if (!snapshot.Sections.TryGetValue(sectionName, out var sectionSnapshot) ||
                !sectionSnapshot.Cases.TryGetValue(caseId, out var measurement))
            {
                Console.Error.WriteLine(
                    $"The snapshot does not contain case '{caseId}' in section '{sectionName}'. Refresh that section first.");
                return 1;
            }

            var operation = GetPcre2SectionRequirements(section).Operation;
            if (!PcreNetNativeBenchmarkBaseline.Supports(operation))
            {
                measurement.PcreNetNativePair = null;
                measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Excluded;
                measurement.PcreNetNativeStatusReason = measurement.PcreNetNativeUnavailableReason;
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
                    var reason = $"Structured checksum mismatch: managed={expected}, comparator={actual}.";
                    measurement.PcreNetNativePair = null;
                    measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Excluded;
                    measurement.PcreNetNativeStatusReason = reason;
                    measurement.PcreNetNativeUnavailableReason = reason;
                    Console.WriteLine($"{sectionName}: Excluded; {reason}");
                    continue;
                }

                Func<int> managedAction = () => ExecutePcre2SnapshotOperation(
                    context.Utf8Pcre2Regex,
                    context,
                    operation);
                Func<int> comparatorAction = () => comparator.Execute(operation);
                var managedWarmup = WarmPcre2QualificationLane(managedAction);
                var comparatorWarmup = WarmPcre2QualificationLane(comparatorAction);
                var managedBatchCount = CalibratePcre2QualificationBatch(managedAction);
                var comparatorBatchCount = CalibratePcre2QualificationBatch(comparatorAction);
                var pair = MeasureSection();

                measurement.MeasuredAtUtc = pair.MeasuredAtUtc;
                measurement.Environment = environment;
                measurement.EffectiveIterations = managedBatchCount;
                measurement.Utf8Pcre2 = pair.ManagedMedianMicroseconds;
                measurement.PcreNetNativeMeasuredAtUtc = pair.MeasuredAtUtc;
                measurement.PcreNetNativeEnvironment = environment;
                measurement.PcreNetNativeEffectiveIterations = comparatorBatchCount;
                measurement.PcreNetNative = pair.ComparatorMedianMicroseconds;
                measurement.PcreNetNativeUnavailableReason = null;
                measurement.PcreNetNativePair = pair;
                measurement.PcreNetNativeStatus = pair.Status;
                measurement.PcreNetNativeStatusReason = pair.StatusReason;

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
                    var bootstrap = BootstrapMedianLogRatio(logRatios);
                    var ratioMedian = Math.Exp(Median(logRatios));
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
                    var orderEffectRatio = Median(managedFirstRatios) / Median(comparatorFirstRatios);
                    var sampleDurationsQualified = managedMilliseconds.All(
                                                       static duration => duration >= Pcre2QualificationMinimumSampleMilliseconds) &&
                                                   comparatorMilliseconds.All(
                                                       static duration => duration >= Pcre2QualificationMinimumSampleMilliseconds);
                    var (status, statusReason) = DeriveStatus(
                        bootstrap.Lower,
                        bootstrap.Upper,
                        orderEffectRatio,
                        sampleDurationsQualified);
                    var excesses = managedMicroseconds
                        .Select((value, index) => value - comparatorMicroseconds[index])
                        .ToArray();

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
                        LaneOrders = laneOrders,
                        ManagedSampleMicroseconds = managedMicroseconds,
                        ComparatorSampleMicroseconds = comparatorMicroseconds,
                        ManagedSampleMilliseconds = managedMilliseconds,
                        ComparatorSampleMilliseconds = comparatorMilliseconds,
                        PairedRatios = ratios,
                        ManagedMedianMicroseconds = Median(managedMicroseconds),
                        ComparatorMedianMicroseconds = Median(comparatorMicroseconds),
                        RatioMedian = ratioMedian,
                        RatioLower95 = Math.Exp(bootstrap.Lower),
                        RatioUpper95 = Math.Exp(bootstrap.Upper),
                        ExcessMedianMicroseconds = Median(excesses),
                        OrderEffectRatio = orderEffectRatio,
                        BootstrapSeed = Pcre2QualificationBootstrapSeed,
                        BootstrapResamples = Pcre2QualificationBootstrapResamples,
                        ResultChecksum = expected.ToString(),
                        ManagedRoute = context.Utf8Pcre2Regex.DebugExecutionKindName,
                        ManagedPlan = context.Utf8Pcre2Regex.DebugDescribeExecutionPlan(),
                        Status = status,
                        StatusReason = statusReason,
                    };
                }

                (double Lower, double Upper) BootstrapMedianLogRatio(double[] logRatios)
                {
                    var random = new Random(Pcre2QualificationBootstrapSeed);
                    var bootstrapMedians = new double[Pcre2QualificationBootstrapResamples];
                    var resample = new double[logRatios.Length];
                    for (var bootstrapIndex = 0; bootstrapIndex < bootstrapMedians.Length; bootstrapIndex++)
                    {
                        for (var sampleIndex = 0; sampleIndex < resample.Length; sampleIndex++)
                        {
                            resample[sampleIndex] = logRatios[random.Next(logRatios.Length)];
                        }

                        bootstrapMedians[bootstrapIndex] = Median(resample);
                    }

                    Array.Sort(bootstrapMedians);
                    var lowerIndex = (int)Math.Floor((bootstrapMedians.Length - 1) * 0.025);
                    var upperIndex = (int)Math.Ceiling((bootstrapMedians.Length - 1) * 0.975);
                    return (bootstrapMedians[lowerIndex], bootstrapMedians[upperIndex]);
                }

                static (Pcre2NativeComparisonStatus Status, string? Reason) DeriveStatus(
                    double lowerLogRatio,
                    double upperLogRatio,
                    double orderEffectRatio,
                    bool sampleDurationsQualified)
                {
                    if (!sampleDurationsQualified)
                    {
                        return (
                            Pcre2NativeComparisonStatus.Unqualified,
                            $"At least one paired lane sample was shorter than {Pcre2QualificationMinimumSampleMilliseconds:F0} ms.");
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
            }
            catch (PCRE.PcreException exception)
            {
                var reason = $"PCRE.NET / PCRE2 NFA rejected the mapped profile: {exception.Message}";
                measurement.PcreNetNativePair = null;
                measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Excluded;
                measurement.PcreNetNativeStatusReason = reason;
                measurement.PcreNetNativeUnavailableReason = reason;
                Console.WriteLine($"{sectionName}: Excluded; {reason}");
            }
        }

        SavePcre2BenchmarkSnapshot(snapshot);
        Console.WriteLine($"Qualified PCRE.NET / PCRE2 NFA benchmark case: {caseId}");
        return 0;
#endif
    }

    private static Pcre2QualificationWarmup WarmPcre2QualificationLane(Func<int> action)
    {
        const int minimumIterations = 64;
        const int iterationsPerClockCheck = 8;
        const double minimumMilliseconds = 750;
        var sink = 0;
        var iterations = 0;
        var start = Stopwatch.GetTimestamp();
        do
        {
            for (var iteration = 0; iteration < iterationsPerClockCheck; iteration++)
            {
                sink ^= action();
            }

            iterations += iterationsPerClockCheck;
        }
        while (iterations < minimumIterations ||
               Stopwatch.GetElapsedTime(start).TotalMilliseconds < minimumMilliseconds);

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

        return (int)Math.Clamp(
            Math.Round(batchCount * Pcre2QualificationTargetSampleMilliseconds / probe.Elapsed.TotalMilliseconds),
            1,
            maximumBatchCount);
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

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        if (sorted.Length == 0)
        {
            throw new InvalidOperationException("Cannot compute the median of an empty sample.");
        }

        var midpoint = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[midpoint - 1] + sorted[midpoint]) / 2
            : sorted[midpoint];
    }

    private readonly record struct Pcre2QualificationBatch(TimeSpan Elapsed, int Sink);

    private readonly record struct Pcre2QualificationPair(
        Pcre2QualificationBatch Managed,
        Pcre2QualificationBatch Comparator);

    private readonly record struct Pcre2QualificationWarmup(int Iterations, TimeSpan Elapsed);

    private sealed class Pcre2QualificationProcessorSet : IDisposable
    {
        private const int CpuSetInformationType = 0;
        private const int MinimumCpuSetInformationSize = 20;
        private readonly Process? _process;
        private readonly nint _originalAffinity;

        private Pcre2QualificationProcessorSet(
            Process? process,
            nint originalAffinity,
            string policy,
            string affinityMask,
            int? efficiencyClass,
            string description)
        {
            _process = process;
            _originalAffinity = originalAffinity;
            Policy = policy;
            AffinityMask = affinityMask;
            EfficiencyClass = efficiencyClass;
            Description = description;
        }

        internal string Policy { get; }

        internal string AffinityMask { get; }

        internal int? EfficiencyClass { get; }

        internal string Description { get; }

        internal static Pcre2QualificationProcessorSet Enter()
        {
            if (!OperatingSystem.IsWindows())
            {
                return new(
                    null,
                    0,
                    "scheduler-default",
                    "unavailable",
                    null,
                    "scheduler default (processor efficiency classes unavailable)");
            }

            var process = Process.GetCurrentProcess();
            var originalAffinity = process.ProcessorAffinity;
            var originalMask = unchecked((ulong)originalAffinity.ToInt64());
            var selected = ReadHighestEfficiencyProcessorMask(originalMask);
            if (selected.Mask == 0 || BitOperations.PopCount(selected.Mask) < 2)
            {
                process.Dispose();
                return new(
                    null,
                    0,
                    "scheduler-default",
                    FormatMask(originalMask),
                    selected.EfficiencyClass,
                    $"scheduler default ({FormatMask(originalMask)})");
            }

            process.ProcessorAffinity = new nint(unchecked((long)selected.Mask));
            return new(
                process,
                originalAffinity,
                "highest-efficiency-class",
                FormatMask(selected.Mask),
                selected.EfficiencyClass,
                $"highest efficiency class {selected.EfficiencyClass} ({FormatMask(selected.Mask)})");
        }

        public void Dispose()
        {
            if (_process is null)
            {
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                _process.ProcessorAffinity = _originalAffinity;
            }

            _process.Dispose();
        }

        private static (ulong Mask, int? EfficiencyClass) ReadHighestEfficiencyProcessorMask(ulong allowedMask)
        {
            _ = GetSystemCpuSetInformation(nint.Zero, 0, out var requiredLength, nint.Zero, 0);
            if (requiredLength == 0 || requiredLength > int.MaxValue)
            {
                return (0, null);
            }

            var buffer = Marshal.AllocHGlobal((int)requiredLength);
            try
            {
                if (!GetSystemCpuSetInformation(buffer, requiredLength, out var returnedLength, nint.Zero, 0))
                {
                    return (0, null);
                }

                int? highestEfficiencyClass = null;
                var selectedMask = 0UL;
                var offset = 0;
                while (offset < returnedLength)
                {
                    var entry = nint.Add(buffer, offset);
                    var entrySize = Marshal.ReadInt32(entry, 0);
                    if (entrySize < MinimumCpuSetInformationSize || entrySize > returnedLength - offset)
                    {
                        return (0, null);
                    }

                    var type = Marshal.ReadInt32(entry, 4);
                    if (type == CpuSetInformationType && Marshal.ReadInt16(entry, 12) == 0)
                    {
                        var logicalProcessor = Marshal.ReadByte(entry, 14);
                        var efficiencyClass = Marshal.ReadByte(entry, 18);
                        if (logicalProcessor < 64)
                        {
                            var processorMask = 1UL << logicalProcessor;
                            if ((processorMask & allowedMask) != 0 &&
                                (highestEfficiencyClass is null || efficiencyClass >= highestEfficiencyClass))
                            {
                                if (efficiencyClass > highestEfficiencyClass)
                                {
                                    selectedMask = 0;
                                }

                                highestEfficiencyClass = efficiencyClass;
                                selectedMask |= processorMask;
                            }
                        }
                    }

                    offset += entrySize;
                }

                return (selectedMask, highestEfficiencyClass);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        private static string FormatMask(ulong mask) => $"0x{mask:X}";

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemCpuSetInformation(
            nint information,
            uint bufferLength,
            out uint returnedLength,
            nint process,
            uint flags);
    }
}
