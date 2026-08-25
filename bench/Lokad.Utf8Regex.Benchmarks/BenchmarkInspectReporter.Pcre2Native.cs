namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
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
        if (!PcreNetNativeBenchmarkBaseline.Supports(operation))
        {
            measurement.PcreNetNativeMeasuredAtUtc = null;
            measurement.PcreNetNativeEnvironment = null;
            measurement.PcreNetNativeEffectiveIterations = null;
            measurement.PcreNetNative = null;
            measurement.PcreNetNativeUnavailableReason =
                "PCRE.NET does not expose equivalent UTF-8 span replacement output.";
            measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Excluded;
            return;
        }

        try
        {
            using var baseline = new PcreNetNativeBenchmarkBaseline(benchmarkCase);
            var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
            var expected = ExecutePcre2SnapshotOperation(context.Utf8Pcre2Regex, context, operation);
            var actual = baseline.Execute(operation);
            if (actual != expected)
            {
                measurement.PcreNetNativeMeasuredAtUtc = null;
                measurement.PcreNetNativeEnvironment = null;
                measurement.PcreNetNativeEffectiveIterations = null;
                measurement.PcreNetNative = null;
                measurement.PcreNetNativeUnavailableReason =
                    $"Checksum mismatch: managed={expected}, native={actual}.";
                measurement.PcreNetNativeStatus = Pcre2NativeComparisonStatus.Excluded;
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

    private static PcreNetNativeBaselineDependencyJson CapturePcreNetNativeBaselineDependency() => new()
    {
        PackageId = PcreNetNativeBenchmarkBaseline.PackageId,
        PackageVersion = PcreNetNativeBenchmarkBaseline.PackageVersion,
        PackageSha512 = PcreNetNativeBenchmarkBaseline.PackageSha512,
        NativePcre2Version = PcreNetNativeBenchmarkBaseline.NativePcre2Version,
        SourceRepository = "https://github.com/ltrzesniewski/pcre-net",
        SourceRevision = PcreNetNativeBenchmarkBaseline.SourceRevision,
        License = "BSD-3-Clause WITH PCRE2-exception",
        Profile = "UTF-8 standard NFA matcher, validation enabled, reusable match buffer, JIT disabled",
    };
}
