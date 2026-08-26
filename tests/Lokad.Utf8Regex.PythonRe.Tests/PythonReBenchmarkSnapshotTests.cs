using System.Text.Json;
using System.Security.Cryptography;
using Lokad.Utf8Regex.Benchmarks;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReBenchmarkSnapshotTests
{
    private static readonly string[] s_mandatorySentinelIds =
    [
        "capture/search-detailed",
        "family/count",
        "findall/one-capture-strings",
        "findall/unicode-capture-utf8",
        "literal/ismatch",
        "literal/search-miss",
        "prefix/match",
        "replacement/subn-utf8",
        "split/captures",
    ];

    private static readonly string[] s_scalingFamilyIds =
    [
        "candidate-position",
        "capture-count",
        "input-length",
        "match-count",
        "output-growth",
        "unicode-coordinate-density",
        "zero-width-progression",
    ];

    [Fact]
    public void PythonReBenchmarkSnapshotPreservesComparativeEvidence()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("PythonRe.Benchmarks.json")));
        var root = document.RootElement;
        Assert.Equal(10, root.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal(JsonValueKind.Object, root.GetProperty("Lifecycle").ValueKind);
        var scalingFamilies = root.GetProperty("ScalingFamilies");
        Assert.Equal(JsonValueKind.Object, scalingFamilies.ValueKind);
        Assert.Equal(
            s_scalingFamilyIds,
            scalingFamilies.EnumerateObject().Select(static family => family.Name).ToArray());
        foreach (var family in scalingFamilies.EnumerateObject())
        {
            AssertScalingFamily(family.Value);
        }
        var catalogSha256 = root.GetProperty("CatalogSha256").GetString() ?? string.Empty;
        Assert.Matches("^[0-9A-F]{64}$", catalogSha256);

        var corpus = root.GetProperty("Corpus");
        Assert.Equal("tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json", corpus.GetProperty("SourceFile").GetString());
        Assert.Equal("0A77376F84956A732A5B5F5D36EA884347FCBA3704DA32D4ED3F6AAFD2554E8B", corpus.GetProperty("Sha256").GetString());
        Assert.Equal(9, corpus.GetProperty("VectorCount").GetInt32());
        Assert.Equal("not-recorded-in-repository", corpus.GetProperty("UpstreamCpythonRevision").GetString());
        Assert.False(string.IsNullOrWhiteSpace(corpus.GetProperty("Limitation").GetString()));

        var cases = root.GetProperty("Cases");
        var catalogCaseIds = PythonReBenchmarkCatalog.Cases
            .Select(static benchmarkCase => benchmarkCase.Id)
            .ToArray();
        Assert.Equal(
            catalogCaseIds,
            root.GetProperty("CatalogCaseIds").EnumerateArray()
                .Select(static element => element.GetString())
                .ToArray());
        Assert.Equal(
            catalogCaseIds.OrderBy(static id => id, StringComparer.Ordinal),
            cases.EnumerateObject().Select(static property => property.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray());
        Assert.True(catalogCaseIds.Length >= 28);
        Assert.True(PythonReBenchmarkCatalog.Cases
            .Select(static benchmarkCase => benchmarkCase.Pattern)
            .Distinct(StringComparer.Ordinal)
            .Count() >= 15);
        Assert.All(s_mandatorySentinelIds, id => Assert.Contains(id, catalogCaseIds));

        foreach (var benchmarkCase in cases.EnumerateObject())
        {
            Assert.False(string.IsNullOrWhiteSpace(benchmarkCase.Value.GetProperty("Pattern").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(benchmarkCase.Value.GetProperty("Operation").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(benchmarkCase.Value.GetProperty("ComparatorOwner").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(benchmarkCase.Value.GetProperty("ManagedRoute").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(benchmarkCase.Value.GetProperty("ByteControlReason").GetString()));
            Assert.True(benchmarkCase.Value.GetProperty("InputUtf8Bytes").GetInt32() > 0);
            Assert.Matches("^[0-9A-F]{64}$", benchmarkCase.Value.GetProperty("InputSha256").GetString() ?? string.Empty);
            Assert.True(benchmarkCase.Value.GetProperty("EffectiveIterations").GetInt32() > 0);
            Assert.True(benchmarkCase.Value.GetProperty("Samples").GetInt32() >= 5);
            var coverage = benchmarkCase.Value.GetProperty("Coverage");
            Assert.False(string.IsNullOrWhiteSpace(coverage.GetProperty("Section").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(coverage.GetProperty("FeatureFamily").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(coverage.GetProperty("InputShape").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(coverage.GetProperty("ProjectionKind").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(coverage.GetProperty("ClaimClass").GetString()));
            AssertCompleteMeasurement(benchmarkCase.Value.GetProperty("PythonRe"));
            AssertCompleteMeasurement(benchmarkCase.Value.GetProperty("DecodeThenRegex"));
            AssertCompleteMeasurement(benchmarkCase.Value.GetProperty("PredecodedRegex"));

            var cpython = benchmarkCase.Value.GetProperty("Cpython");
            Assert.Equal(1, cpython.GetProperty("ProtocolVersion").GetInt32());
            AssertCpythonMeasurement(cpython.GetProperty("PredecodedRe"), benchmarkCase.Value.GetProperty("EffectiveIterations").GetInt32());
            AssertCpythonMeasurement(cpython.GetProperty("DecodeThenRe"), benchmarkCase.Value.GetProperty("EffectiveIterations").GetInt32());
            var cpythonEnvironment = cpython.GetProperty("Environment");
            Assert.Equal("CPython", cpythonEnvironment.GetProperty("Implementation").GetString());
            Assert.False(string.IsNullOrWhiteSpace(cpythonEnvironment.GetProperty("Version").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(cpythonEnvironment.GetProperty("Executable").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(cpythonEnvironment.GetProperty("Platform").GetString()));

            var environment = benchmarkCase.Value.GetProperty("Environment");
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("SourceCommit").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("Runtime").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("OperatingSystem").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("Processor").GetString()));

            var qualification = benchmarkCase.Value.GetProperty("Qualification");
            var status = qualification.GetProperty("Status").GetString();
            Assert.Contains(
                status,
                new[] { "Unqualified", "Inconclusive", "Equivalent", "ManagedFaster", "CpythonFaster" });
            Assert.False(string.IsNullOrWhiteSpace(qualification.GetProperty("StatusReason").GetString()));
            Assert.Contains(
                qualification.GetProperty("EngineEvidenceBasis").GetString(),
                new[] { "Not engine-comparable", "Byte control" });
            Assert.Contains(
                qualification.GetProperty("EngineConclusion").GetString(),
                new[] { "NotApplicable", "Unqualified", "Inconclusive", "Equivalent", "ManagedFaster", "CpythonFaster" });
            Assert.False(string.IsNullOrWhiteSpace(
                qualification.GetProperty("EngineConclusionReason").GetString()));
            var pairedEvidence = qualification.GetProperty("PairedEvidence");
            if (pairedEvidence.ValueKind == JsonValueKind.Null)
            {
                Assert.Equal("Unqualified", status);
            }
            else
            {
                AssertPairedEvidence(pairedEvidence, status!);
            }
        }

        Assert.True(cases.GetProperty("capture/search-detailed").GetProperty("EffectiveIterations").GetInt32() >= 20_000);
        Assert.True(cases.GetProperty("literal/fullmatch").GetProperty("EffectiveIterations").GetInt32() >= 20_000);
        Assert.Contains(
            "FallbackRegex",
            cases.GetProperty("prefix/match").GetProperty("ManagedRoute").GetString(),
            StringComparison.Ordinal);
        Assert.Contains(
            "ExactAsciiLiteral",
            cases.GetProperty("literal/search-miss").GetProperty("ManagedRoute").GetString(),
            StringComparison.Ordinal);
        Assert.True(cases.GetProperty("literal/search-miss").GetProperty("ByteControlEligible").GetBoolean());
        Assert.True(cases.GetProperty("prefix/match").GetProperty("ByteControlEligible").GetBoolean());
        Assert.False(cases.GetProperty("unicode/fullmatch").GetProperty("ByteControlEligible").GetBoolean());
        Assert.False(cases.GetProperty("family/count").GetProperty("ByteControlEligible").GetBoolean());
        Assert.Contains(
            "Utf8Regex/ExactAsciiLiteral",
            cases.GetProperty("replacement/subn-string").GetProperty("ManagedRoute").GetString(),
            StringComparison.Ordinal);
        Assert.Equal(
            "_sre scanner + Python finditer/sum",
            cases.GetProperty("family/count").GetProperty("ComparatorOwner").GetString());

        var snapshotPath = FindRepositoryFile("PythonRe.Benchmarks.json");
        var page = File.ReadAllText(FindRepositoryFile("src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md"));
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotPath)));
        var cpythonVersions = cases.EnumerateObject()
            .Select(static benchmarkCase => benchmarkCase.Value
                .GetProperty("Cpython")
                .GetProperty("Environment")
                .GetProperty("Version")
                .GetString())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Assert.Contains($"Snapshot SHA-256: `{hash}`", page, StringComparison.Ordinal);
        Assert.Contains($"Catalog SHA-256: `{catalogSha256}`", page, StringComparison.Ordinal);
        Assert.All(catalogCaseIds, caseId => Assert.Contains($"`{caseId}`", page, StringComparison.Ordinal));
        Assert.Contains("CPython predecoded elapsed", page, StringComparison.Ordinal);
        Assert.Contains("`Rbyte` is representation-neutral engine evidence", page, StringComparison.Ordinal);
        Assert.Contains("## Operation ownership and managed route", page, StringComparison.Ordinal);
        Assert.Contains("## Coverage summary", page, StringComparison.Ordinal);
        Assert.Contains("### Reused subjects and corpus identities", page, StringComparison.Ordinal);
        Assert.Contains("### Direct matching", page, StringComparison.Ordinal);
        Assert.Contains("### Scaling evidence", page, StringComparison.Ordinal);
        Assert.Contains("every alternating paired sample", page, StringComparison.Ordinal);
        Assert.Contains("--verify-pythonre-scaling", page, StringComparison.Ordinal);
        Assert.All(s_scalingFamilyIds, familyId =>
            Assert.Contains($"#### `{familyId}`", page, StringComparison.Ordinal));
        Assert.Contains("contextual uncached-construction throughput", page, StringComparison.Ordinal);
        var statusCounts = cases.EnumerateObject()
            .GroupBy(
                static benchmarkCase => benchmarkCase.Value
                    .GetProperty("Qualification")
                    .GetProperty("Status")
                    .GetString() ?? throw new InvalidOperationException("PythonRe Status must be a string."),
                StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        Assert.Contains(
            $"Public Status: `{statusCounts.GetValueOrDefault("ManagedFaster")}` managed faster, " +
            $"`{statusCounts.GetValueOrDefault("Equivalent")}` equivalent, " +
            $"`{statusCounts.GetValueOrDefault("CpythonFaster")}` CPython faster, " +
            $"`{statusCounts.GetValueOrDefault("Inconclusive")}` inconclusive, " +
            $"`{statusCounts.GetValueOrDefault("Unqualified")}` unqualified",
            page,
            StringComparison.Ordinal);
        Assert.DoesNotContain(" CPU |", page, StringComparison.Ordinal);
        Assert.All(cpythonVersions, version => Assert.Contains($"CPython {version}", page, StringComparison.Ordinal));
    }

    private static void AssertCompleteMeasurement(JsonElement measurement)
    {
        Assert.True(measurement.GetProperty("MedianMicroseconds").GetDouble() > 0);
        Assert.True(measurement.GetProperty("MinimumMicroseconds").GetDouble() > 0);
        Assert.True(measurement.GetProperty("MaximumMicroseconds").GetDouble() > 0);
        Assert.True(measurement.GetProperty("MedianAllocatedBytes").GetInt64() >= 0);
        Assert.True(measurement.GetProperty("WarmupCalls").GetInt32() > 0);
        Assert.True(measurement.GetProperty("WarmupMilliseconds").GetDouble() > 0);
    }

    private static void AssertCpythonMeasurement(JsonElement measurement, int maximumIterations)
    {
        Assert.True(measurement.GetProperty("MedianMicroseconds").GetDouble() > 0);
        Assert.True(measurement.GetProperty("MinimumMicroseconds").GetDouble() > 0);
        Assert.True(measurement.GetProperty("MaximumMicroseconds").GetDouble() > 0);
        Assert.InRange(measurement.GetProperty("EffectiveIterations").GetInt32(), 1, maximumIterations);
        Assert.True(measurement.GetProperty("WarmupCalls").GetInt32() > 0);
        Assert.True(measurement.GetProperty("WarmupMilliseconds").GetDouble() > 0);
    }

    private static void AssertScalingFamily(JsonElement family)
    {
        Assert.False(string.IsNullOrWhiteSpace(family.GetProperty("Dimension").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(family.GetProperty("Operation").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(family.GetProperty("ResultContract").GetString()));
        Assert.Equal(5, family.GetProperty("Samples").GetInt32());
        Assert.Contains(family.GetProperty("FitGate").GetString(), new[] { "Pass", "Reject" });
        Assert.False(string.IsNullOrWhiteSpace(family.GetProperty("FitGateReason").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(family.GetProperty("ManagedRoute").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(family.GetProperty("CpuPolicy").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(family.GetProperty("CpuAffinityMask").GetString()));
        Assert.True(family.GetProperty("MaximumOrderEffect").GetDouble() > 0);
        Assert.True(family.GetProperty("MinimumLaneElapsedMilliseconds").GetDouble() > 0);

        var managedEnvironment = family.GetProperty("ManagedEnvironment");
        Assert.False(managedEnvironment.GetProperty("TrackedDirty").GetBoolean());
        Assert.False(managedEnvironment.GetProperty("HasUntrackedFiles").GetBoolean());
        Assert.Matches("^[0-9a-fA-F]{12,64}$", managedEnvironment.GetProperty("SourceCommit").GetString() ?? string.Empty);
        var cpythonEnvironment = family.GetProperty("CpythonEnvironment");
        Assert.Equal("CPython", cpythonEnvironment.GetProperty("Implementation").GetString());
        Assert.Matches("^[0-9A-F]{64}$", cpythonEnvironment.GetProperty("ExecutableSha256").GetString() ?? string.Empty);
        Assert.Matches("^[0-9A-F]{64}$", cpythonEnvironment.GetProperty("RunnerSha256").GetString() ?? string.Empty);

        var points = family.GetProperty("Points").EnumerateArray().ToArray();
        Assert.Equal(4, points.Length);
        foreach (var point in points)
        {
            Assert.False(string.IsNullOrWhiteSpace(point.GetProperty("Label").GetString()));
            Assert.True(point.GetProperty("Scale").GetInt32() >= 0);
            Assert.True(point.GetProperty("InputUtf8Bytes").GetInt32() > 0);
            Assert.Matches("^[0-9A-F]{64}$", point.GetProperty("InputSha256").GetString() ?? string.Empty);
            Assert.Matches("^[0-9A-F]{16}$", point.GetProperty("SemanticDigest").GetString() ?? string.Empty);
            Assert.True(point.GetProperty("ManagedIterations").GetInt32() > 0);
            Assert.True(point.GetProperty("CpythonIterations").GetInt32() > 0);
            Assert.True(point.GetProperty("ManagedWarmupCalls").GetInt32() > 0);
            Assert.True(point.GetProperty("CpythonWarmupCalls").GetInt32() > 0);
            Assert.True(point.GetProperty("ManagedMedianMicroseconds").GetDouble() > 0);
            Assert.True(point.GetProperty("CpythonMedianMicroseconds").GetDouble() > 0);
            Assert.True(point.GetProperty("RatioMedian").GetDouble() > 0);
            Assert.True(point.GetProperty("RatioLower95").GetDouble() > 0);
            Assert.True(point.GetProperty("RatioUpper95").GetDouble() > 0);
            Assert.True(point.GetProperty("OrderEffect").GetDouble() > 0);
            Assert.True(point.GetProperty("ManagedAllocatedBytes").GetInt64() >= 0);

            var samples = point.GetProperty("Samples").EnumerateArray().ToArray();
            Assert.Equal(5, samples.Length);
            for (var index = 0; index < samples.Length; index++)
            {
                var sample = samples[index];
                Assert.Contains(sample.GetProperty("Order").GetString(), new[] { "ManagedFirst", "CpythonFirst" });
                if (index > 0)
                {
                    Assert.NotEqual(samples[index - 1].GetProperty("Order").GetString(), sample.GetProperty("Order").GetString());
                }

                Assert.True(sample.GetProperty("ManagedMicroseconds").GetDouble() > 0);
                Assert.True(sample.GetProperty("CpythonMicroseconds").GetDouble() > 0);
                Assert.True(sample.GetProperty("Ratio").GetDouble() > 0);
                Assert.True(sample.GetProperty("ManagedElapsedMilliseconds").GetDouble() > 0);
                Assert.True(sample.GetProperty("CpythonElapsedMilliseconds").GetDouble() > 0);
                Assert.True(sample.GetProperty("ManagedAllocatedBytes").GetInt64() >= 0);
                Assert.Equal(3, sample.GetProperty("ManagedGcCollections").GetArrayLength());
                Assert.Equal(3, sample.GetProperty("CpythonGcCollections").GetArrayLength());
            }
        }
    }

    private static void AssertPairedEvidence(JsonElement evidence, string status)
    {
        Assert.Equal(3, evidence.GetProperty("ProtocolVersion").GetInt32());
        Assert.Equal("CPythonPredecodedElapsed", evidence.GetProperty("Baseline").GetString());
        Assert.Contains(
            evidence.GetProperty("ResultContract").GetString(),
            new[] { "ConsumedGroupZeroRanges", "EagerMaterializedResult", "ScalarResult" });
        Assert.True(evidence.GetProperty("WorktreeQualified").GetBoolean());
        Assert.Equal("structured-u64-mix-v1", evidence.GetProperty("SemanticDigestAlgorithm").GetString());
        Assert.Matches("^[0-9A-F]{16}$", evidence.GetProperty("SemanticDigest").GetString() ?? string.Empty);
        Assert.Matches("^[0-9A-F]{64}$", evidence.GetProperty("QualificationId").GetString() ?? string.Empty);
        Assert.Matches("^[0-9A-F]{64}$", evidence.GetProperty("CaseDefinitionSha256").GetString() ?? string.Empty);
        Assert.Matches("^[0-9A-F]{64}$", evidence.GetProperty("CatalogSha256").GetString() ?? string.Empty);
        Assert.Matches("^[0-9A-F]{64}$", evidence.GetProperty("ManagedProductSha256").GetString() ?? string.Empty);
        Assert.Matches("^[0-9A-F]{64}$", evidence.GetProperty("ManagedOperationProtocolSha256").GetString() ?? string.Empty);
        Assert.Matches("^[0-9A-F]{64}$", evidence.GetProperty("CpythonOperationProtocolSha256").GetString() ?? string.Empty);
        Assert.Matches("^[0-9A-F]{64}$", evidence.GetProperty("SharedProtocolSha256").GetString() ?? string.Empty);
        Assert.Contains(
            evidence.GetProperty("CpuPolicy").GetString(),
            new[]
            {
                "single-highest-efficiency-processor",
                "single-least-contended-highest-efficiency-processor",
            });
        Assert.True(evidence.GetProperty("ManagedIterations").GetInt32() > 0);
        Assert.True(evidence.GetProperty("CpythonIterations").GetInt32() > 0);
        Assert.True(evidence.GetProperty("ManagedWarmupCalls").GetInt32() > 0);
        Assert.True(evidence.GetProperty("CpythonWarmupCalls").GetInt32() > 0);
        Assert.True(evidence.GetProperty("StrongRatioMedian").GetDouble() > 0);
        Assert.True(evidence.GetProperty("StrongRatioLower95").GetDouble() > 0);
        Assert.True(evidence.GetProperty("StrongRatioUpper95").GetDouble() > 0);
        Assert.True(evidence.GetProperty("ManagedMedianAllocatedBytes").GetInt64() >= 0);

        var samples = evidence.GetProperty("Samples").EnumerateArray().ToArray();
        Assert.Contains(samples.Length, new[] { 9, 17 });
        Assert.All(samples, sample =>
        {
            Assert.Contains(sample.GetProperty("Order").GetString(), new[] { "ManagedFirst", "CpythonFirst" });
            Assert.True(sample.GetProperty("ManagedMicroseconds").GetDouble() > 0);
            Assert.True(sample.GetProperty("CpythonMicroseconds").GetDouble() > 0);
            Assert.True(sample.GetProperty("StrongRatio").GetDouble() > 0);
            if (!string.Equals(status, "Unqualified", StringComparison.Ordinal))
            {
                Assert.True(sample.GetProperty("ManagedElapsedMilliseconds").GetDouble() >= 20);
                Assert.True(sample.GetProperty("CpythonElapsedMilliseconds").GetDouble() >= 20);
            }
            Assert.Equal(3, sample.GetProperty("ManagedGcCollections").GetArrayLength());
            Assert.Equal(3, sample.GetProperty("CpythonGcCollections").GetArrayLength());
        });
        Assert.Equal(3, evidence.GetProperty("ManagedEmptyLoopMicroseconds").GetArrayLength());
        Assert.Equal(3, evidence.GetProperty("CpythonEmptyLoopMicroseconds").GetArrayLength());
        Assert.Equal(3, evidence.GetProperty("ManagedTrivialCallMicroseconds").GetArrayLength());
        Assert.Equal(3, evidence.GetProperty("CpythonTrivialCallMicroseconds").GetArrayLength());
        var byteControl = evidence.GetProperty("ByteControl");
        if (byteControl.ValueKind != JsonValueKind.Null)
        {
            Assert.True(byteControl.GetProperty("CpythonIterations").GetInt32() > 0);
            Assert.True(byteControl.GetProperty("CpythonMedianMicroseconds").GetDouble() > 0);
            Assert.True(byteControl.GetProperty("RatioMedian").GetDouble() > 0);
            Assert.Equal(samples.Length, byteControl.GetProperty("Samples").GetArrayLength());
            Assert.Equal(3, byteControl.GetProperty("CpythonEmptyLoopMicroseconds").GetArrayLength());
            Assert.Equal(3, byteControl.GetProperty("CpythonTrivialCallMicroseconds").GetArrayLength());
            Assert.False(string.IsNullOrWhiteSpace(
                byteControl.GetProperty("EngineConclusionReason").GetString()));
        }

        var cpython = evidence.GetProperty("CpythonEnvironment");
        Assert.Equal("CPython", cpython.GetProperty("Implementation").GetString());
        Assert.Matches("^[0-9A-F]{64}$", cpython.GetProperty("ExecutableSha256").GetString() ?? string.Empty);
        Assert.Matches("^[0-9A-F]{64}$", cpython.GetProperty("RunnerSha256").GetString() ?? string.Empty);
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
