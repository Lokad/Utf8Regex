using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    public static int RunVerifyPcre2ComparatorCase(string caseId)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        var exitCode = 0;
        foreach (var section in GetPcre2SectionsForCase(caseId))
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

        return exitCode;
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
        Profile = "UTF-8 standard NFA matcher, validation enabled, reusable match buffer, JIT disabled",
        BuildFingerprint = PcreNetNativeBenchmarkBaseline.CaptureBuildFingerprint(),
    };
}
