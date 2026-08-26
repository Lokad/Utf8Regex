using System.Text.Json;
using System.Security.Cryptography;

namespace Lokad.Utf8Regex.PythonRe.Tests;

public sealed class PythonReBenchmarkSnapshotTests
{
    private static readonly string[] s_caseIds =
    [
        "capture/search-detailed",
        "class-run/count",
        "family/count",
        "findall/full-strings",
        "findall/full-utf8",
        "findall/many-capture-strings",
        "findall/many-capture-utf8",
        "findall/one-capture-strings",
        "findall/unicode-capture-utf8",
        "findall/unicode-full-strings",
        "findall/unicode-full-utf8",
        "iteration/finditer-detailed",
        "literal/fullmatch",
        "literal/ismatch",
        "literal/search",
        "literal/search-miss",
        "prefix/match",
        "replacement/evaluator-string",
        "replacement/evaluator-utf8",
        "replacement/fixed-string",
        "replacement/fixed-utf8",
        "replacement/subn-string",
        "replacement/subn-utf8",
        "split/captures",
        "split/no-captures",
        "unicode/count",
        "unicode/fullmatch",
        "zero-width/count",
    ];

    [Fact]
    public void PythonReBenchmarkSnapshotPreservesComparativeEvidence()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("PythonRe.Benchmarks.json")));
        var root = document.RootElement;
        Assert.Equal(4, root.GetProperty("SchemaVersion").GetInt32());

        var corpus = root.GetProperty("Corpus");
        Assert.Equal("tests/Lokad.Utf8Regex.PythonRe.Tests/Corpus/ported-core.json", corpus.GetProperty("SourceFile").GetString());
        Assert.Equal("0A77376F84956A732A5B5F5D36EA884347FCBA3704DA32D4ED3F6AAFD2554E8B", corpus.GetProperty("Sha256").GetString());
        Assert.Equal(9, corpus.GetProperty("VectorCount").GetInt32());
        Assert.Equal("not-recorded-in-repository", corpus.GetProperty("UpstreamCpythonRevision").GetString());
        Assert.False(string.IsNullOrWhiteSpace(corpus.GetProperty("Limitation").GetString()));

        var cases = root.GetProperty("Cases");
        Assert.Equal(
            s_caseIds,
            cases.EnumerateObject().Select(static property => property.Name).OrderBy(static name => name, StringComparer.Ordinal).ToArray());

        foreach (var benchmarkCase in cases.EnumerateObject())
        {
            Assert.False(string.IsNullOrWhiteSpace(benchmarkCase.Value.GetProperty("Pattern").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(benchmarkCase.Value.GetProperty("Operation").GetString()));
            Assert.True(benchmarkCase.Value.GetProperty("InputUtf8Bytes").GetInt32() > 0);
            Assert.True(benchmarkCase.Value.GetProperty("EffectiveIterations").GetInt32() > 0);
            Assert.True(benchmarkCase.Value.GetProperty("Samples").GetInt32() >= 5);
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
            Assert.Equal("Not engine-comparable", qualification.GetProperty("EngineEvidenceBasis").GetString());
            Assert.Equal("Unqualified", qualification.GetProperty("EngineConclusion").GetString());
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
        Assert.All(s_caseIds, caseId => Assert.Contains($"`{caseId}`", page, StringComparison.Ordinal));
        Assert.Contains("CPython predecoded elapsed", page, StringComparison.Ordinal);
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

    private static void AssertPairedEvidence(JsonElement evidence, string status)
    {
        Assert.Equal(2, evidence.GetProperty("ProtocolVersion").GetInt32());
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
