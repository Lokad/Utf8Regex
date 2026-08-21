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
        Assert.Equal(2, root.GetProperty("SchemaVersion").GetInt32());

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

            var environment = benchmarkCase.Value.GetProperty("Environment");
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("SourceCommit").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("Runtime").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("OperatingSystem").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("Processor").GetString()));
        }

        Assert.True(cases.GetProperty("capture/search-detailed").GetProperty("EffectiveIterations").GetInt32() >= 20_000);
        Assert.True(cases.GetProperty("literal/fullmatch").GetProperty("EffectiveIterations").GetInt32() >= 20_000);

        var snapshotPath = FindRepositoryFile("PythonRe.Benchmarks.json");
        var page = File.ReadAllText(FindRepositoryFile("src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md"));
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotPath)));
        Assert.Contains($"Snapshot SHA-256: `{hash}`", page, StringComparison.Ordinal);
        Assert.All(s_caseIds, caseId => Assert.Contains($"`{caseId}`", page, StringComparison.Ordinal));
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
