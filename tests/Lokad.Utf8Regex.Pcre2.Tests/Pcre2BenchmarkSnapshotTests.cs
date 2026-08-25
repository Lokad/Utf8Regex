using System.Text.Json;
using System.Security.Cryptography;
using System.Globalization;

namespace Lokad.Utf8Regex.Pcre2.Tests;

public sealed class Pcre2BenchmarkSnapshotTests
{
    private static readonly string[] s_scalingFamilies =
    [
        "branch-repeat-linear",
        "branch-reset-coordinate-projection",
        "candidate-heavy-misses",
        "capture-rollback",
        "cartesian-literal-families",
        "character-class-dense",
        "dense-non-ascii-coordinates",
        "dense-plus-sparse-candidate-portfolios",
        "excluded-ascii-repeat-count",
        "leading-word-boundary-run-candidates",
        "literal-family-global-cursor",
        "long-flat-patterns",
        "replacement-growth",
        "required-literal-all-a-miss",
        "single-token-repeat-vm",
        "zero-width-iteration",
    ];

    [Fact]
    public void Pcre2BenchmarkSnapshotPreservesAllocationAndScalingEvidence()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("PCRE2.Benchmarks.json")));
        var root = document.RootElement;
        Assert.Equal(9, root.GetProperty("SchemaVersion").GetInt32());

        var dependency = root.GetProperty("PcreNetNativeBaseline");
        Assert.Equal("PCRE.NET", dependency.GetProperty("PackageId").GetString());
        Assert.Equal("1.5.0", dependency.GetProperty("PackageVersion").GetString());
        Assert.StartsWith("10.47", dependency.GetProperty("NativePcre2Version").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            "Zu3NJGiU1S7tHHaW4UdEK1WZ9LFYqPI+6Y0eiL6YPHVOHSoWjbq0x5j3uN9895DoIgO5XI/50S6dj2ZmRHirNA==",
            dependency.GetProperty("PackageSha512").GetString());
        var buildFingerprint = dependency.GetProperty("BuildFingerprint");
        AssertBuildFingerprint(buildFingerprint);
        var buildFingerprintSha256 = buildFingerprint.GetProperty("Sha256").GetString();
        AssertWorkspaceContract(dependency.GetProperty("WorkspaceContract"), comparator: true);

        var families = root.GetProperty("ScalingFamilies");
        Assert.Equal(
            s_scalingFamilies,
            families.EnumerateObject().Select(static property => property.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray());

        foreach (var family in families.EnumerateObject())
        {
            Assert.False(string.IsNullOrWhiteSpace(family.Value.GetProperty("Operation").GetString()));
            var points = family.Value.GetProperty("Points").EnumerateArray().ToArray();
            var expectedPointCount = family.Name == "dense-plus-sparse-candidate-portfolios" ? 6 : 4;
            Assert.Equal(expectedPointCount, points.Length);
            Assert.True(IsStrictlyIncreasing(points.Select(static point => point.GetProperty("PatternUtf8Bytes").GetInt32())) ||
                        IsStrictlyIncreasing(points.Select(static point => point.GetProperty("InputUtf8Bytes").GetInt32())));
            Assert.All(points, AssertCompleteMeasurement);
        }

        var sections = root.GetProperty("Sections");
        AssertCompleteMeasurement(
            sections.GetProperty("pcre2-managed-compatible-ismatch")
                .GetProperty("Cases")
                .GetProperty("common/email-match"));
        AssertCompleteMeasurement(
            sections.GetProperty("pcre2-special-count")
                .GetProperty("Cases")
                .GetProperty("pcre2/branch-reset-basic"));

        var operationRows = sections.EnumerateObject()
            .SelectMany(static section => section.Value.GetProperty("Cases").EnumerateObject())
            .ToArray();
        Assert.Equal(99, operationRows.Count(static row =>
            row.Value.TryGetProperty("PcreNetNative", out var native) && native.GetDouble() > 0));
        Assert.Equal(27, operationRows.Count(static row =>
            row.Value.GetProperty("PcreNetNativeStatus").GetString() == "Excluded"));
        var pairedRowCount = operationRows.Count(static row =>
            row.Value.TryGetProperty("PcreNetNativePair", out _));
        Assert.All(operationRows, static row =>
        {
            var hasNative = row.Value.TryGetProperty("PcreNetNative", out var native) && native.GetDouble() > 0;
            var hasReason = row.Value.TryGetProperty("PcreNetNativeUnavailableReason", out var reason) &&
                !string.IsNullOrWhiteSpace(reason.GetString());
            Assert.True(hasNative || hasReason, $"Missing native PCRE2 disposition for '{row.Name}'.");
            var status = row.Value.GetProperty("PcreNetNativeStatus").GetString();
            if (row.Value.TryGetProperty("PcreNetNativePair", out _))
            {
                Assert.True(hasNative);
                Assert.NotEqual("Excluded", status);
            }
            else
            {
                Assert.Equal(hasNative ? "Unqualified" : "Excluded", status);
            }
        });

        foreach (var section in sections.EnumerateObject())
        {
            foreach (var row in section.Value.GetProperty("Cases").EnumerateObject())
            {
                if (row.Value.TryGetProperty("PcreNetNativePair", out var pair))
                {
                    AssertPairedMeasurement(section.Name, row.Name, row.Value, pair, buildFingerprintSha256);
                }
            }
        }

        var snapshotPath = FindRepositoryFile("PCRE2.Benchmarks.json");
        var page = File.ReadAllText(FindRepositoryFile("src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md"));
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotPath)));
        var statusCounts = operationRows
            .GroupBy(static row => row.Value.GetProperty("PcreNetNativeStatus").GetString())
            .ToDictionary(static group => group.Key!, static group => group.Count(), StringComparer.Ordinal);
        Assert.Contains($"Snapshot SHA-256: `{hash}`", page, StringComparison.Ordinal);
        Assert.Contains("PCRE.NET / PCRE2 NFA CPU", page, StringComparison.Ordinal);
        Assert.Contains(
            $"Comparator Status: `{statusCounts.GetValueOrDefault("ManagedFaster")}` managed faster, " +
            $"`{statusCounts.GetValueOrDefault("Equivalent")}` equivalent, " +
            $"`{statusCounts.GetValueOrDefault("NativeFaster")}` native faster, " +
            $"`{statusCounts.GetValueOrDefault("Inconclusive")}` inconclusive, " +
            $"`{statusCounts.GetValueOrDefault("Unqualified")}` unqualified, " +
            $"`{statusCounts.GetValueOrDefault("Excluded")}` excluded",
            page,
            StringComparison.Ordinal);
        Assert.Contains($"Rows with paired qualification evidence: `{pairedRowCount}/99`", page, StringComparison.Ordinal);
        if (pairedRowCount > 0)
        {
            Assert.Contains("Qualification processor sets: `highest-efficiency-class ", page, StringComparison.Ordinal);
            Assert.Contains("Managed qualification lifecycle", page, StringComparison.Ordinal);
            Assert.Contains("IQR ", page, StringComparison.Ordinal);
            Assert.Contains("Plan SHA-256", page, StringComparison.Ordinal);
        }
        Assert.Contains("| R | 95% R | E | Paired samples | Managed route |", page, StringComparison.Ordinal);
        Assert.Contains("interquartile spread ratios", page, StringComparison.Ordinal);
        Assert.Contains("| Package | Version | Native engine |", page, StringComparison.Ordinal);
        Assert.Contains($"Native build fingerprint: `{buildFingerprintSha256}`", page, StringComparison.Ordinal);
        Assert.Contains("## Qualified comparator plans", page, StringComparison.Ordinal);
        Assert.Contains("Comparator qualification lifecycle (`PcreMatchBuffer8Bit`)", page, StringComparison.Ordinal);
        Assert.Contains("not thread-safe and not reentrant", page, StringComparison.Ordinal);
        Assert.Contains("Retained native match-data heap-frame high water: unavailable", page, StringComparison.Ordinal);
        Assert.Contains("median of five managed-thread allocation probes", page, StringComparison.Ordinal);
        Assert.Contains("Utf8Pcre2 managed alloc | Comparator managed alloc", page, StringComparison.Ordinal);
        Assert.Contains("--qualify-pcre2-comparator-case-reversed", page, StringComparison.Ordinal);
        Assert.Contains("--verify-pcre2-qualification-consistency", page, StringComparison.Ordinal);
        Assert.Contains("--emit-pcre2-priority-report\",\"relative", page, StringComparison.Ordinal);
        Assert.Contains("--emit-pcre2-priority-report\",\"absolute", page, StringComparison.Ordinal);
        Assert.All(operationRows, row => Assert.Contains($"`{row.Name}`", page, StringComparison.Ordinal));

        Assert.Contains(
            "<PackageReference Include=\"PCRE.NET\" Version=\"1.5.0\" PrivateAssets=\"all\" />",
            File.ReadAllText(FindRepositoryFile("bench/Lokad.Utf8Regex.Benchmarks/Lokad.Utf8Regex.Benchmarks.csproj")),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "PCRE.NET",
            File.ReadAllText(FindRepositoryFile("src/Lokad.Utf8Regex.Pcre2/Lokad.Utf8Regex.Pcre2.csproj")),
            StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertPairedMeasurement(
        string sectionName,
        string caseId,
        JsonElement row,
        JsonElement pair,
        string? expectedBuildFingerprintSha256)
    {
        Assert.Equal(9, pair.GetProperty("ProtocolVersion").GetInt32());
        Assert.Equal(sectionName, pair.GetProperty("Section").GetString());
        Assert.Equal(caseId, pair.GetProperty("CaseId").GetString());
        Assert.Equal(row.GetProperty("PcreNetNativeStatus").GetString(), pair.GetProperty("Status").GetString());
        Assert.True(Guid.TryParseExact(pair.GetProperty("PairId").GetString(), "N", out _));
        Assert.True(pair.GetProperty("WorktreeQualified").GetBoolean());
        Assert.False(pair.GetProperty("Environment").GetProperty("TrackedDirty").GetBoolean());
        Assert.Equal("PCRE.NET", pair.GetProperty("ComparatorPackageId").GetString());
        Assert.Equal("1.5.0", pair.GetProperty("ComparatorPackageVersion").GetString());
        Assert.StartsWith("10.47", pair.GetProperty("ComparatorEngineVersion").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(pair.GetProperty("ComparatorPackageSha512").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(pair.GetProperty("ComparatorProfile").GetString()));
        var buildFingerprint = pair.GetProperty("ComparatorBuildFingerprint");
        AssertBuildFingerprint(buildFingerprint);
        Assert.Equal(expectedBuildFingerprintSha256, buildFingerprint.GetProperty("Sha256").GetString());
        AssertPlanFingerprint(pair.GetProperty("ComparatorPlanFingerprint"));
        AssertWorkspaceContract(pair.GetProperty("ManagedWorkspaceContract"), comparator: false);
        AssertWorkspaceContract(pair.GetProperty("ComparatorWorkspaceContract"), comparator: true);
        Assert.False(string.IsNullOrWhiteSpace(pair.GetProperty("ManagedRoute").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(pair.GetProperty("ManagedPlan").GetString()));
        Assert.Equal("highest-efficiency-class", pair.GetProperty("ProcessorSetPolicy").GetString());
        Assert.StartsWith("0x", pair.GetProperty("ProcessorAffinityMask").GetString(), StringComparison.Ordinal);
        Assert.True(pair.GetProperty("ProcessorEfficiencyClass").GetInt32() >= 0);
        Assert.True(ulong.TryParse(
            pair.GetProperty("ResultChecksum").GetString(),
            NumberStyles.HexNumber,
            CultureInfo.InvariantCulture,
            out _));

        var sampleCount = pair.GetProperty("SampleCount").GetInt32();
        Assert.True(sampleCount >= 9);
        Assert.True(pair.GetProperty("ManagedBatchCount").GetInt32() > 0);
        Assert.True(pair.GetProperty("ComparatorBatchCount").GetInt32() > 0);
        Assert.True(pair.GetProperty("ManagedWarmupIterations").GetInt32() >= 64);
        Assert.True(pair.GetProperty("ComparatorWarmupIterations").GetInt32() >= 64);
        Assert.True(pair.GetProperty("ManagedWarmupMilliseconds").GetDouble() >= 750);
        Assert.True(pair.GetProperty("ComparatorWarmupMilliseconds").GetDouble() >= 750);
        AssertAllocationMeasurement(pair, "Managed");
        AssertAllocationMeasurement(pair, "Comparator");

        var laneOrders = pair.GetProperty("LaneOrders").EnumerateArray().Select(static value => value.GetString()).ToArray();
        var managedMicroseconds = ReadSamples(pair, "ManagedSampleMicroseconds", sampleCount);
        var comparatorMicroseconds = ReadSamples(pair, "ComparatorSampleMicroseconds", sampleCount);
        var managedMilliseconds = ReadSamples(pair, "ManagedSampleMilliseconds", sampleCount);
        var comparatorMilliseconds = ReadSamples(pair, "ComparatorSampleMilliseconds", sampleCount);
        var ratios = ReadSamples(pair, "PairedRatios", sampleCount);
        Assert.Equal(sampleCount, laneOrders.Length);
        for (var sample = 0; sample < sampleCount; sample++)
        {
            Assert.True(laneOrders[sample] is "ManagedFirst" or "ComparatorFirst");
            if (sample > 0)
            {
                Assert.NotEqual(laneOrders[sample - 1], laneOrders[sample]);
            }

            Assert.True(managedMicroseconds[sample] > 0);
            Assert.True(comparatorMicroseconds[sample] > 0);
            Assert.True(managedMilliseconds[sample] > 0);
            Assert.True(comparatorMilliseconds[sample] > 0);
            if (pair.GetProperty("Status").GetString() != "Unqualified")
            {
                Assert.True(managedMilliseconds[sample] >= 20);
                Assert.True(comparatorMilliseconds[sample] >= 20);
            }
            Assert.Equal(managedMicroseconds[sample] / comparatorMicroseconds[sample], ratios[sample], 12);
        }

        var lower = pair.GetProperty("RatioLower95").GetDouble();
        var median = pair.GetProperty("RatioMedian").GetDouble();
        var upper = pair.GetProperty("RatioUpper95").GetDouble();
        Assert.True(lower > 0);
        Assert.InRange(median, lower, upper);
        Assert.True(pair.GetProperty("OrderEffectRatio").GetDouble() > 0);
        Assert.Equal(
            InterquartileSpread(managedMicroseconds),
            pair.GetProperty("ManagedInterquartileSpreadRatio").GetDouble(),
            12);
        Assert.Equal(
            InterquartileSpread(comparatorMicroseconds),
            pair.GetProperty("ComparatorInterquartileSpreadRatio").GetDouble(),
            12);
        Assert.Equal(24301, pair.GetProperty("BootstrapSeed").GetInt32());
        Assert.Equal(10_000, pair.GetProperty("BootstrapResamples").GetInt32());

        if (pair.GetProperty("Status").GetString() is "Inconclusive" or "Unqualified")
        {
            Assert.False(string.IsNullOrWhiteSpace(pair.GetProperty("StatusReason").GetString()));
        }
    }

    private static void AssertBuildFingerprint(JsonElement fingerprint)
    {
        AssertSha256(fingerprint.GetProperty("Sha256").GetString());
        Assert.StartsWith("10.47", fingerprint.GetProperty("EngineVersion").GetString(), StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(fingerprint.GetProperty("ProcessArchitecture").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(fingerprint.GetProperty("OperatingSystemArchitecture").GetString()));
        Assert.True(fingerprint.GetProperty("JitSupported").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(fingerprint.GetProperty("JitTarget").GetString()));
        Assert.True(fingerprint.GetProperty("UnicodeSupported").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(fingerprint.GetProperty("UnicodeVersion").GetString()));
        Assert.True(fingerprint.GetProperty("CompiledWidths").GetUInt32() > 0);
        Assert.True(fingerprint.GetProperty("LinkSizeBytes").GetUInt32() > 0);
        Assert.True(fingerprint.GetProperty("EffectiveLinkSizeBytes").GetUInt32() > 0);
        Assert.True(fingerprint.GetProperty("DefaultHeapLimitKibibytes").GetUInt32() > 0);
        Assert.True(fingerprint.GetProperty("DefaultMatchLimit").GetUInt32() > 0);
        Assert.True(fingerprint.GetProperty("DefaultDepthLimit").GetUInt32() > 0);
        Assert.True(fingerprint.GetProperty("ParenthesesLimit").GetUInt32() > 0);
        Assert.True(fingerprint.GetProperty("CharacterTablesLengthBytes").GetUInt32() > 0);
    }

    private static void AssertWorkspaceContract(JsonElement contract, bool comparator)
    {
        Assert.False(string.IsNullOrWhiteSpace(contract.GetProperty("StateHolder").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(contract.GetProperty("Lifetime").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(contract.GetProperty("ConcurrencyContract").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(contract.GetProperty("RetainedMemoryContract").GetString()));
        Assert.False(contract.TryGetProperty("RetainedNativeHeapHighWaterBytes", out _));

        if (comparator)
        {
            Assert.Equal("PcreMatchBuffer8Bit", contract.GetProperty("StateHolder").GetString());
            Assert.Contains("not thread-safe and not reentrant", contract.GetProperty("ConcurrencyContract").GetString(), StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(
                contract.GetProperty("RetainedNativeHeapHighWaterUnavailableReason").GetString()));
        }
        else
        {
            Assert.Equal("operation-local managed state", contract.GetProperty("StateHolder").GetString());
            Assert.Contains("invoked concurrently", contract.GetProperty("ConcurrencyContract").GetString(), StringComparison.Ordinal);
            Assert.False(contract.TryGetProperty("RetainedNativeHeapHighWaterUnavailableReason", out _));
        }
    }

    private static void AssertAllocationMeasurement(JsonElement pair, string lane)
    {
        var iterations = pair.GetProperty($"{lane}AllocationProbeIterations").GetInt32();
        Assert.InRange(iterations, 1, 64);
        var sampleBytes = pair.GetProperty($"{lane}AllocationSampleBytes")
            .EnumerateArray()
            .Select(static value => value.GetInt64())
            .ToArray();
        Assert.Equal(5, sampleBytes.Length);
        Assert.All(sampleBytes, static value => Assert.True(value >= 0));
        Array.Sort(sampleBytes);
        Assert.Equal(
            sampleBytes[sampleBytes.Length / 2] / iterations,
            pair.GetProperty(lane == "Managed"
                ? "ManagedAllocatedBytesPerOperation"
                : "ComparatorManagedAllocatedBytesPerOperation").GetInt64());
    }

    private static void AssertPlanFingerprint(JsonElement fingerprint)
    {
        AssertSha256(fingerprint.GetProperty("Sha256").GetString());
        Assert.True(fingerprint.GetProperty("PatternSizeBytes").GetUInt64() > 0);
        Assert.True(fingerprint.GetProperty("FrameSizeBytes").GetUInt64() > 0);
        Assert.Equal(0UL, fingerprint.GetProperty("JitSizeBytes").GetUInt64());
        Assert.False(fingerprint.GetProperty("IsJitCompiled").GetBoolean());
        Assert.True(fingerprint.GetProperty("CaptureCount").GetInt32() >= 0);
        Assert.True(fingerprint.GetProperty("MinimumSubjectCharacters").GetUInt32() >= 0);
        Assert.False(string.IsNullOrWhiteSpace(fingerprint.GetProperty("ArgumentOptions").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(fingerprint.GetProperty("EffectiveOptions").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(fingerprint.GetProperty("ExtraOptions").GetString()));

        AssertOptionalCodeUnit(fingerprint, "FirstCodeType", "FirstCodeUnit");
        AssertOptionalCodeUnit(fingerprint, "LastCodeType", "LastCodeUnit");
        AssertOptionalUnsigned(fingerprint, "PatternMatchLimit");
        AssertOptionalUnsigned(fingerprint, "PatternDepthLimit");
        AssertOptionalUnsigned(fingerprint, "PatternHeapLimitKibibytes");
    }

    private static void AssertOptionalCodeUnit(JsonElement fingerprint, string typeName, string unitName)
    {
        var type = fingerprint.GetProperty(typeName).GetUInt32();
        Assert.InRange(type, 0U, 2U);
        Assert.Equal(type == 1, fingerprint.TryGetProperty(unitName, out var unit));
        if (type == 1)
        {
            Assert.True(unit.GetUInt32() <= 0x10FFFF);
        }
    }

    private static void AssertOptionalUnsigned(JsonElement fingerprint, string propertyName)
    {
        if (fingerprint.TryGetProperty(propertyName, out var value))
        {
            Assert.True(value.GetUInt32() >= 0);
        }
    }

    private static void AssertSha256(string? value) =>
        Assert.True(value is { Length: 64 } && value.All(Uri.IsHexDigit));

    private static double[] ReadSamples(JsonElement pair, string propertyName, int sampleCount)
    {
        var samples = pair.GetProperty(propertyName).EnumerateArray().Select(static value => value.GetDouble()).ToArray();
        Assert.Equal(sampleCount, samples.Length);
        return samples;
    }

    private static double InterquartileSpread(IEnumerable<double> values)
    {
        var sorted = values.Order().ToArray();
        var lower = sorted[(int)Math.Floor((sorted.Length - 1) * 0.25)];
        var upper = sorted[(int)Math.Ceiling((sorted.Length - 1) * 0.75)];
        return upper / lower;
    }

    private static void AssertCompleteMeasurement(JsonElement measurement)
    {
        Assert.True(measurement.GetProperty("PatternUtf8Bytes").GetInt32() > 0);
        Assert.True(measurement.GetProperty("InputUtf8Bytes").GetInt32() >= 0);
        Assert.True(measurement.GetProperty("EffectiveIterations").GetInt32() > 0);
        Assert.True(measurement.GetProperty("ConstructionMicroseconds").GetDouble() >= 0);
        Assert.True(measurement.GetProperty("ConstructionAllocatedBytes").GetInt64() >= 0);
        Assert.True(measurement.GetProperty("FirstCallAllocatedBytes").GetInt64() >= 0);
        Assert.True(measurement.GetProperty("WarmAllocatedBytes").GetInt64() >= 0);
    }

    private static bool IsStrictlyIncreasing(IEnumerable<int> values)
    {
        var hasPrevious = false;
        var previous = 0;
        foreach (var value in values)
        {
            if (hasPrevious && value <= previous)
            {
                return false;
            }

            hasPrevious = true;
            previous = value;
        }

        return true;
    }

    private static string FindRepositoryFile(string fileName)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException($"Could not locate repository file '{fileName}'.");
    }
}
