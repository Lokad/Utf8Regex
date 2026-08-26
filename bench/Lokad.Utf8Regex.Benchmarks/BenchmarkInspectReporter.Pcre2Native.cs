using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    public static int RunMeasurePcre2NativeAutoPossessCost(
        string caseId,
        string? iterationsText,
        string? samplesText)
    {
        var requestedIterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        using var processorSet = BenchmarkProcessorScope.EnterHighestEfficiencyClass();

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"RequestedIterations: {requestedIterations}");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"CPU set           : {processorSet.Description}");

        foreach (var section in GetPcre2SectionsForCase(caseId))
        {
            var operation = GetPcre2SectionRequirements(section).Operation;
            if (!PcreNetNativeBenchmarkBaseline.Supports(operation))
            {
                continue;
            }

            try
            {
                using var defaultBaseline = new PcreNetNativeBenchmarkBaseline(benchmarkCase);
                using var disabledBaseline = new PcreNetNativeBenchmarkBaseline(
                    benchmarkCase,
                    PCRE.PcreOptions.NoAutoPossess);
                var defaultChecksum = defaultBaseline.ComputeChecksum(operation);
                var disabledChecksum = disabledBaseline.ComputeChecksum(operation);
                if (defaultChecksum != disabledChecksum)
                {
                    Console.Error.WriteLine(
                        $"{operation}: default/NoAutoPossess checksum mismatch; " +
                        $"default={defaultChecksum}; disabled={disabledChecksum}.");
                    return 1;
                }

                Func<int> defaultAction = () => defaultBaseline.Execute(operation);
                Func<int> disabledAction = () => disabledBaseline.Execute(operation);
                var defaultWarmup = WarmPcre2QualificationLane(defaultAction);
                var disabledWarmup = WarmPcre2QualificationLane(disabledAction);
                var defaultIterations = Math.Max(
                    requestedIterations,
                    CalibratePcre2QualificationBatch(defaultAction));
                var disabledIterations = Math.Max(
                    requestedIterations,
                    CalibratePcre2QualificationBatch(disabledAction));
                var defaultSamples = new List<double>(samples);
                var disabledSamples = new List<double>(samples);
                var sink = 0;
                for (var sample = 0; sample < samples; sample++)
                {
                    var order = sample % 2 == 0
                        ? Pcre2PairLaneOrder.ManagedFirst
                        : Pcre2PairLaneOrder.ComparatorFirst;
                    var pair = MeasurePcre2QualificationPair(
                        defaultAction,
                        defaultIterations,
                        disabledAction,
                        disabledIterations,
                        order);
                    defaultSamples.Add(pair.Managed.Elapsed.TotalMicroseconds / defaultIterations);
                    disabledSamples.Add(pair.Comparator.Elapsed.TotalMicroseconds / disabledIterations);
                    sink ^= pair.Managed.Sink ^ pair.Comparator.Sink;
                }

                GC.KeepAlive(sink);
                var defaultMedian = Median(defaultSamples);
                var disabledMedian = Median(disabledSamples);
                var defaultPlan = defaultBaseline.CapturePlanFingerprint();
                var disabledPlan = disabledBaseline.CapturePlanFingerprint();

                Console.WriteLine($"Operation         : {operation}");
                Console.WriteLine($"EffectiveIterations: {defaultIterations:N0}/{disabledIterations:N0} default/disabled");
                Console.WriteLine(
                    $"Default           : {defaultMedian:F3} us/op " +
                    $"({defaultSamples.Min():F3}..{defaultSamples.Max():F3})");
                Console.WriteLine(
                    $"NoAutoPossess     : {disabledMedian:F3} us/op " +
                    $"({disabledSamples.Min():F3}..{disabledSamples.Max():F3})");
                Console.WriteLine($"Disabled/Default  : {disabledMedian / defaultMedian:F3}x");
                Console.WriteLine($"DefaultPlan       : {defaultPlan.Sha256[..12]}; {defaultPlan.EffectiveOptions}");
                Console.WriteLine($"DisabledPlan      : {disabledPlan.Sha256[..12]}; {disabledPlan.EffectiveOptions}");
                Console.WriteLine(
                    $"Warmup calls      : {defaultWarmup.Iterations:N0}/{disabledWarmup.Iterations:N0} " +
                    $"default/disabled in {defaultWarmup.Elapsed.TotalMilliseconds:F0}/" +
                    $"{disabledWarmup.Elapsed.TotalMilliseconds:F0} ms");
            }
            catch (PCRE.PcreException exception)
            {
                Console.WriteLine($"{operation}: Excluded; {exception.Message}");
            }
        }

        return 0;
    }

    public static int RunMeasurePcre2NativeBufferCost(
        string caseId,
        string? iterationsText,
        string? samplesText)
    {
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        using var processorSet = BenchmarkProcessorScope.EnterHighestEfficiencyClass();

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"CPU set           : {processorSet.Description}");
        Console.WriteLine("Fresh contract    : create and dispose one match buffer per operation; compiled regex reused");

        foreach (var section in GetPcre2SectionsForCase(caseId))
        {
            var operation = GetPcre2SectionRequirements(section).Operation;
            if (!PcreNetNativeBenchmarkBaseline.Supports(operation))
            {
                continue;
            }

            try
            {
                using var baseline = new PcreNetNativeBenchmarkBaseline(benchmarkCase);
                var reusedChecksum = baseline.ComputeChecksum(operation);
                var freshChecksum = baseline.ComputeChecksumWithFreshMatchBuffer(operation);
                if (reusedChecksum != freshChecksum)
                {
                    Console.Error.WriteLine(
                        $"{operation}: fresh/reused checksum mismatch; reused={reusedChecksum}; fresh={freshChecksum}.");
                    return 1;
                }

                Func<int> reusedAction = () => baseline.Execute(operation);
                Func<int> freshAction = () => baseline.ExecuteWithFreshMatchBuffer(operation);
                var reusedWarmup = WarmPcre2QualificationLane(reusedAction);
                var freshWarmup = WarmPcre2QualificationLane(freshAction);
                var reusedSamples = new List<double>(samples);
                var freshSamples = new List<double>(samples);
                var sink = 0;
                for (var sample = 0; sample < samples; sample++)
                {
                    var order = sample % 2 == 0
                        ? Pcre2PairLaneOrder.ManagedFirst
                        : Pcre2PairLaneOrder.ComparatorFirst;
                    var pair = MeasurePcre2QualificationPair(
                        reusedAction,
                        iterations,
                        freshAction,
                        iterations,
                        order);
                    reusedSamples.Add(pair.Managed.Elapsed.TotalMicroseconds / iterations);
                    freshSamples.Add(pair.Comparator.Elapsed.TotalMicroseconds / iterations);
                    sink ^= pair.Managed.Sink ^ pair.Comparator.Sink;
                }

                GC.KeepAlive(sink);
                var reusedMedian = Median(reusedSamples);
                var freshMedian = Median(freshSamples);
                var reusedAllocation = MeasurePcre2QualificationAllocation(
                    GetPcre2QualificationAllocationProbeIterations(reusedMedian),
                    reusedAction);
                var freshAllocation = MeasurePcre2QualificationAllocation(
                    GetPcre2QualificationAllocationProbeIterations(freshMedian),
                    freshAction);

                Console.WriteLine($"Operation         : {operation}");
                Console.WriteLine(
                    $"ReusedBuffer      : {reusedMedian:F3} us/op " +
                    $"({reusedSamples.Min():F3}..{reusedSamples.Max():F3})");
                Console.WriteLine(
                    $"FreshBuffer       : {freshMedian:F3} us/op " +
                    $"({freshSamples.Min():F3}..{freshSamples.Max():F3})");
                Console.WriteLine($"Fresh/Reused      : {freshMedian / reusedMedian:F3}x");
                Console.WriteLine($"ReusedManagedAlloc: {reusedAllocation.BytesPerOperation:N0} B/op");
                Console.WriteLine($"FreshManagedAlloc : {freshAllocation.BytesPerOperation:N0} B/op");
                Console.WriteLine(
                    $"Warmup calls      : {reusedWarmup.Iterations:N0}/{freshWarmup.Iterations:N0} " +
                    $"reused/fresh in {reusedWarmup.Elapsed.TotalMilliseconds:F0}/{freshWarmup.Elapsed.TotalMilliseconds:F0} ms");
            }
            catch (PCRE.PcreException exception)
            {
                Console.WriteLine($"{operation}: Excluded; {exception.Message}");
            }
        }

        return 0;
    }

    public static int RunVerifyPcre2ComparatorCase(string caseId)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        var sections = GetPcre2SectionsForCase(caseId).ToArray();
        var exitCode = 0;
        foreach (var section in sections)
        {
            var operation = GetPcre2SectionRequirements(section).Operation;
            if (!PcreNetNativeBenchmarkBaseline.Supports(operation))
            {
                Console.WriteLine($"{GetPcre2SectionToken(section)}: Excluded");
                continue;
            }

            try
            {
                var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
                using var baseline = new PcreNetNativeBenchmarkBaseline(benchmarkCase);
                var managed = ComputePcre2ManagedResultChecksum(
                    context.Utf8Pcre2Regex,
                    context.InputBytes,
                    operation);
                var comparator = baseline.ComputeChecksum(operation);
                var equal = managed == comparator;
                Console.WriteLine(
                    $"{GetPcre2SectionToken(section)}: {(equal ? "Equal" : "Mismatch")}; " +
                    $"managed={managed}; comparator={comparator}");
                if (!equal)
                {
                    exitCode = 1;
                }
            }
            catch (PCRE.PcreException exception)
            {
                Console.WriteLine($"{GetPcre2SectionToken(section)}: Excluded; {exception.Message}");
            }
        }

        var hasCount = sections.Any(static section =>
            GetPcre2SectionRequirements(section).Operation == Utf8Pcre2BenchmarkOperation.Count);
        var hasEnumeration = sections.Any(static section =>
            GetPcre2SectionRequirements(section).Operation == Utf8Pcre2BenchmarkOperation.EnumerateMatches);
        if (hasCount && hasEnumeration)
        {
            try
            {
                var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
                using var baseline = new PcreNetNativeBenchmarkBaseline(benchmarkCase);
                var expected = ComputeManagedPcre2ProgressionAudit(context);
                Pcre2ProgressionAudit? previousManaged = null;
                Pcre2ProgressionAudit? previousComparator = null;
                var progressionStable = true;
                const int repeats = 3;
                for (var repeat = 0; repeat < repeats; repeat++)
                {
                    var managed = ComputeManagedPcre2ProgressionAudit(context);
                    var comparator = baseline.ComputeProgressionAudit();
                    if (managed.CountResult != managed.EnumeratedCount ||
                        comparator.CountResult != comparator.EnumeratedCount ||
                        managed != expected ||
                        comparator != expected ||
                        (previousManaged.HasValue && previousManaged.Value != managed) ||
                        (previousComparator.HasValue && previousComparator.Value != comparator))
                    {
                        progressionStable = false;
                        exitCode = 1;
                    }

                    previousManaged = managed;
                    previousComparator = comparator;
                }

                Console.WriteLine(
                    $"Progression: {(progressionStable ? "Stable" : "Mismatch")}; repeats={repeats}; " +
                    $"count/enumerate={expected.CountResult}/{expected.EnumeratedCount}; " +
                    $"checksum={expected.EnumerationChecksum}");
            }
            catch (PCRE.PcreException exception)
            {
                Console.WriteLine($"Progression: Excluded; {exception.Message}");
            }
        }

        return exitCode;
    }

    private static Pcre2ProgressionAudit ComputeManagedPcre2ProgressionAudit(
        Utf8Pcre2BenchmarkContext context)
    {
        var checksum = new Pcre2BenchmarkChecksumBuilder(Utf8Pcre2BenchmarkOperation.EnumerateMatches);
        var enumeratedCount = 0;
        var enumerator = context.Utf8Pcre2Regex.EnumerateMatches(context.InputBytes);
        while (enumerator.MoveNext())
        {
            var match = enumerator.Current;
            checksum.AddRange(
                match.StartOffsetInBytes,
                match.EndOffsetInBytes - match.StartOffsetInBytes);
            enumeratedCount++;
        }

        return new Pcre2ProgressionAudit(
            context.Utf8Pcre2Regex.Count(context.InputBytes),
            enumeratedCount,
            checksum.Complete(enumeratedCount, false));
    }

    public static int RunRefreshPcre2NativeBaselineCase(
        string caseId,
        string? iterationsText,
        string? samplesText)
    {
        var requestedIterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var snapshot = LoadPcre2BenchmarkSnapshot();
        snapshot.SchemaVersion = Pcre2BenchmarkSchemaVersion;
        snapshot.PcreNetNativeBaseline = CapturePcreNetNativeBaselineDependency();
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);

        foreach (var section in GetPcre2SectionsForCase(caseId))
        {
            var sectionName = GetPcre2SectionToken(section);
            if (!snapshot.Sections.TryGetValue(sectionName, out var sectionSnapshot) ||
                !sectionSnapshot.Cases.TryGetValue(caseId, out var measurement))
            {
                throw new InvalidOperationException(
                    $"The snapshot does not contain case '{caseId}' in section '{sectionName}'. Refresh that section first.");
            }

            Console.WriteLine($"Measuring PCRE.NET / PCRE2 NFA comparator for {sectionName}: {caseId}");
            var operation = GetPcre2SectionRequirements(section).Operation;
            var maximumIterations = ParsePcre2SnapshotIterations(benchmarkCase, section, requestedIterations);
            MeasurePcreNetNativeBaseline(measurement, benchmarkCase, operation, maximumIterations, samples);
        }

        SavePcre2BenchmarkSnapshot(snapshot);
        Console.WriteLine($"Updated PCRE.NET / PCRE2 NFA benchmark case: {caseId}");
        return 0;
    }

    public static int RunRefreshPcre2NativeBaselines(
        string? sectionsText,
        string? iterationsText,
        string? samplesText)
    {
        var requestedIterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var snapshot = LoadPcre2BenchmarkSnapshot();
        snapshot.SchemaVersion = Pcre2BenchmarkSchemaVersion;
        snapshot.PcreNetNativeBaseline = CapturePcreNetNativeBaselineDependency();
        var sections = ParsePcre2Sections(sectionsText);

        foreach (var section in sections)
        {
            if (!snapshot.Sections.TryGetValue(GetPcre2SectionToken(section), out var sectionSnapshot))
            {
                throw new InvalidOperationException(
                    $"The snapshot does not contain section '{GetPcre2SectionToken(section)}'. Refresh it before adding native baselines.");
            }

            var operation = GetPcre2SectionRequirements(section).Operation;
            foreach (var (caseId, measurement) in sectionSnapshot.Cases.OrderBy(static row => row.Key, StringComparer.Ordinal))
            {
                Console.WriteLine($"Measuring PCRE.NET / PCRE2 NFA comparator for {GetPcre2SectionToken(section)}: {caseId}");
                var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
                var maximumIterations = ParsePcre2SnapshotIterations(benchmarkCase, section, requestedIterations);
                MeasurePcreNetNativeBaseline(measurement, benchmarkCase, operation, maximumIterations, samples);
                SavePcre2BenchmarkSnapshot(snapshot);
            }
        }

        SavePcre2BenchmarkSnapshot(snapshot);
        Console.WriteLine(
            $"Updated PCRE.NET / PCRE2 NFA comparators: {string.Join(", ", sections.Select(GetPcre2SectionToken))}");
        return 0;
    }

    private static void MeasurePcreNetNativeBaseline(
        Pcre2CaseMeasurementJson measurement,
        Utf8Pcre2BenchmarkCase benchmarkCase,
        Utf8Pcre2BenchmarkOperation operation,
        int maximumIterations,
        int samples)
    {
        measurement.PcreNetNativePair = null;
        measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Unqualified;
        measurement.PcreNetNativeStatusReason =
            "The managed and comparator lanes were not collected by the paired qualification protocol.";
        if (!PcreNetNativeBenchmarkBaseline.Supports(operation))
        {
            measurement.PcreNetNativeMeasuredAtUtc = null;
            measurement.PcreNetNativeEnvironment = null;
            measurement.PcreNetNativeEffectiveIterations = null;
            measurement.PcreNetNative = null;
            measurement.PcreNetNativeUnavailableReason =
                "PCRE.NET does not expose equivalent UTF-8 span replacement output.";
            measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Excluded;
            measurement.PcreNetNativeStatusReason = measurement.PcreNetNativeUnavailableReason;
            return;
        }

        try
        {
            using var baseline = new PcreNetNativeBenchmarkBaseline(benchmarkCase);
            var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
            var expected = ComputePcre2ManagedResultChecksum(
                context.Utf8Pcre2Regex,
                context.InputBytes,
                operation);
            var actual = baseline.ComputeChecksum(operation);
            if (actual != expected)
            {
                measurement.PcreNetNativeMeasuredAtUtc = null;
                measurement.PcreNetNativeEnvironment = null;
                measurement.PcreNetNativeEffectiveIterations = null;
                measurement.PcreNetNative = null;
                measurement.PcreNetNativeUnavailableReason =
                    $"Structured checksum mismatch: managed={expected}, comparator={actual}.";
                measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Excluded;
                measurement.PcreNetNativeStatusReason = measurement.PcreNetNativeUnavailableReason;
                Console.WriteLine($"  Skipped: {measurement.PcreNetNativeUnavailableReason}");
                return;
            }

            var effectiveIterations = CalibratePcreNetNativeIterations(baseline, operation, maximumIterations);
            measurement.PcreNetNativeMeasuredAtUtc = DateTimeOffset.UtcNow;
            measurement.PcreNetNativeEnvironment = CaptureBenchmarkEnvironment();
            measurement.PcreNetNativeEffectiveIterations = effectiveIterations;
            measurement.PcreNetNative = MeasurePcre2SnapshotMicroseconds(
                samples,
                effectiveIterations,
                () => baseline.Execute(operation));
            measurement.PcreNetNativeUnavailableReason = null;
            measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Unqualified;
        }
        catch (PCRE.PcreException exception)
        {
            measurement.PcreNetNativeMeasuredAtUtc = null;
            measurement.PcreNetNativeEnvironment = null;
            measurement.PcreNetNativeEffectiveIterations = null;
            measurement.PcreNetNative = null;
            measurement.PcreNetNativeUnavailableReason =
                $"Native PCRE2 rejected the mapped profile: {exception.Message}";
            measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Excluded;
            measurement.PcreNetNativeStatusReason = measurement.PcreNetNativeUnavailableReason;
            Console.WriteLine($"  Skipped: {measurement.PcreNetNativeUnavailableReason}");
        }
    }

    private static int CalibratePcreNetNativeIterations(
        PcreNetNativeBenchmarkBaseline baseline,
        Utf8Pcre2BenchmarkOperation operation,
        int maximumIterations)
    {
        const int probeIterations = 3;
        const double targetMicrosecondsPerSample = 100_000;
        var checksum = 0;
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        for (var probe = 0; probe < probeIterations; probe++)
        {
            checksum ^= baseline.Execute(operation);
        }

        var elapsedMicroseconds = System.Diagnostics.Stopwatch.GetElapsedTime(start).TotalMicroseconds / probeIterations;
        GC.KeepAlive(checksum);
        if (elapsedMicroseconds <= 0)
        {
            return maximumIterations;
        }

        return (int)Math.Clamp(targetMicrosecondsPerSample / elapsedMicroseconds, 1, maximumIterations);
    }

    private static Pcre2BenchmarkResultChecksum ComputePcre2ManagedResultChecksum(
        Utf8Pcre2Regex regex,
        byte[] input,
        Utf8Pcre2BenchmarkOperation operation)
    {
        var checksum = new Pcre2BenchmarkChecksumBuilder(operation);
        switch (operation)
        {
            case Utf8Pcre2BenchmarkOperation.IsMatch:
                return checksum.Complete(regex.IsMatch(input) ? 1 : 0, false);
            case Utf8Pcre2BenchmarkOperation.Count:
                return checksum.Complete(regex.Count(input), false);
            case Utf8Pcre2BenchmarkOperation.EnumerateMatches:
            {
                var count = 0;
                var enumerator = regex.EnumerateMatches(input);
                while (enumerator.MoveNext())
                {
                    var match = enumerator.Current;
                    checksum.AddRange(
                        match.StartOffsetInBytes,
                        match.EndOffsetInBytes - match.StartOffsetInBytes);
                    count++;
                }

                return checksum.Complete(count, false);
            }
            case Utf8Pcre2BenchmarkOperation.MatchMany:
            {
                Span<Utf8Pcre2MatchData> matches = stackalloc Utf8Pcre2MatchData[8];
                var written = regex.MatchMany(input, matches, out var isMore);
                for (var i = 0; i < written; i++)
                {
                    checksum.AddRange(
                        matches[i].StartOffsetInBytes,
                        matches[i].EndOffsetInBytes - matches[i].StartOffsetInBytes);
                }

                return checksum.Complete(written, isMore);
            }
            default:
                throw new NotSupportedException($"No structured checksum is available for {operation}.");
        }
    }

    private static PcreNetNativeBaselineDependencyJson CapturePcreNetNativeBaselineDependency() => new()
    {
        PackageId = PcreNetNativeBenchmarkBaseline.PackageId,
        PackageVersion = PcreNetNativeBenchmarkBaseline.PackageVersion,
        PackageSha512 = PcreNetNativeBenchmarkBaseline.PackageSha512,
        NativePcre2Version = PcreNetNativeBenchmarkBaseline.NativePcre2Version,
        SourceRepository = "https://github.com/ltrzesniewski/pcre-net",
        SourceRevision = PcreNetNativeBenchmarkBaseline.SourceRevision,
        License = "BSD-3-Clause WITH PCRE2-exception",
        Profile = "UTF-8 standard NFA matcher, validation enabled, managed-profile LF newline and Unicode \\R, reusable match buffer, JIT disabled",
        BuildFingerprint = PcreNetNativeBenchmarkBaseline.CaptureBuildFingerprint(),
        WorkspaceContract = PcreNetNativeBenchmarkBaseline.CaptureWorkspaceContract(),
    };
}
