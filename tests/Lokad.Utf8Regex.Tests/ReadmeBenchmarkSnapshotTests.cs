using System.Security.Cryptography;
using System.Text.Json;

namespace Lokad.Utf8Regex.Tests;

public sealed class ReadmeBenchmarkSnapshotTests
{
    [Fact]
    public void SnapshotContainsCompleteCleanCalibratedSectionPairs()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("README.Benchmarks.json")));
        var root = document.RootElement;
        Assert.Equal(5, root.GetProperty("SchemaVersion").GetInt32());

        var sections = root.GetProperty("Sections");
        AssertSection(sections, "dotnet-performance", 55);
        AssertSection(sections, "dotnet-performance-compiled", 55);
        AssertSection(sections, "lokad", 34);
        AssertSection(sections, "lokad-compiled", 34);
        AssertPairedSections(sections, "dotnet-performance", "dotnet-performance-compiled");
        AssertPairedSections(sections, "lokad", "lokad-compiled");

        var environments = sections.EnumerateObject()
            .SelectMany(static section => section.Value.GetProperty("Cases").EnumerateObject())
            .Select(static benchmarkCase => benchmarkCase.Value.GetProperty("Environment"))
            .ToArray();
        Assert.Single(environments.Select(static environment => environment.GetProperty("SourceCommit").GetString()).Distinct());
        Assert.Single(environments.Select(static environment => environment.GetProperty("Runtime").GetString()).Distinct());
        Assert.Single(environments.Select(static environment => environment.GetProperty("OperatingSystem").GetString()).Distinct());
        Assert.Single(environments.Select(static environment => environment.GetProperty("Processor").GetString()).Distinct());
        Assert.Single(environments.Select(static environment => environment.GetProperty("TieredPgo").GetString()).Distinct());
    }

    [Fact]
    public void ParityReportMatchesSnapshotAndContainsAllQualifiedRows()
    {
        var snapshotPath = FindRepositoryFile("README.Benchmarks.json");
        var expectedHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotPath)));
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("README.Parity.json")));
        var root = document.RootElement;
        Assert.Equal(2, root.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("README.Benchmarks.json", root.GetProperty("GeneratedFrom").GetString());
        Assert.Equal(expectedHash, root.GetProperty("SnapshotSha256").GetString());
        Assert.Equal("PredecodedRegex", root.GetProperty("PrimaryCpuComparator").GetString());
        Assert.Equal("DecodeThenRegex", root.GetProperty("SecondaryCpuComparator").GetString());

        var summary = root.GetProperty("Summary");
        Assert.Equal(178, summary.GetProperty("Rows").GetInt32());
        Assert.Equal(0, summary.GetProperty("Unqualified").GetInt32());
        Assert.Equal(
            178,
            summary.GetProperty("Wins").GetInt32() +
            summary.GetProperty("TieCandidates").GetInt32() +
            summary.GetProperty("Gaps").GetInt32());

        var rows = root.GetProperty("Rows").EnumerateArray().ToArray();
        Assert.Equal(178, rows.Length);
        Assert.All(rows, row =>
        {
            Assert.Contains(row.GetProperty("Status").GetString(), new[] { "Win", "TieCandidate", "Gap" });
            Assert.Equal("AlternatingSixLaneV1", row.GetProperty("MeasurementProtocol").GetString());
            Assert.True(row.GetProperty("RatioToDecode").GetDouble() > 0);
            var ratioToPredecoded = row.GetProperty("RatioToPredecoded").GetDouble();
            Assert.True(ratioToPredecoded > 0);
            Assert.Equal(ClassifyRatio(ratioToPredecoded), row.GetProperty("Status").GetString());
            Assert.True(row.GetProperty("Utf8AllocatedBytes").GetDouble() >= 0);
            Assert.True(row.GetProperty("PredecodedRegexAllocatedBytes").GetDouble() >= 0);
            Assert.True(row.GetProperty("DecodeThenRegexAllocatedBytes").GetDouble() >= 0);
        });
    }

    private static string ClassifyRatio(double ratio)
        => ratio <= 0.98
            ? "Win"
            : ratio <= 1.02
                ? "TieCandidate"
                : "Gap";

    [Fact]
    public void PublicOperationRowsContainMeasuredValues()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("README.Benchmarks.json")));
        var sections = document.RootElement.GetProperty("Sections");

        AssertPositiveRow(sections.GetProperty("dotnet-performance"), "common/split-words");
        AssertPositiveRow(sections.GetProperty("dotnet-performance"), "common/replace-words");
        AssertPositiveRow(sections.GetProperty("dotnet-performance-compiled"), "common/split-words");
        AssertPositiveRow(sections.GetProperty("dotnet-performance-compiled"), "common/replace-words");
    }

    private static void AssertPositiveRow(JsonElement section, string caseId)
    {
        var measurement = section.GetProperty("Cases").GetProperty(caseId);
        Assert.True(measurement.GetProperty("Utf8Regex").GetDouble() > 0);
        Assert.True(measurement.GetProperty("Utf8Compiled").GetDouble() > 0);
        Assert.True(measurement.GetProperty("PredecodedRegex").GetDouble() > 0);
        Assert.True(measurement.GetProperty("CompiledRegex").GetDouble() > 0);
        Assert.True(measurement.GetProperty("DecodeThenRegex").GetDouble() > 0);
        Assert.True(measurement.GetProperty("DecodeThenCompiledRegex").GetDouble() > 0);
        Assert.True(measurement.GetProperty("Utf8RegexAllocatedBytes").GetDouble() >= 0);
        Assert.True(measurement.GetProperty("Utf8CompiledAllocatedBytes").GetDouble() >= 0);
        Assert.True(measurement.GetProperty("PredecodedRegexAllocatedBytes").GetDouble() >= 0);
        Assert.True(measurement.GetProperty("CompiledRegexAllocatedBytes").GetDouble() >= 0);
        Assert.True(measurement.GetProperty("DecodeThenRegexAllocatedBytes").GetDouble() >= 0);
        Assert.True(measurement.GetProperty("DecodeThenCompiledRegexAllocatedBytes").GetDouble() >= 0);
    }

    private static void AssertSection(JsonElement sections, string sectionName, int expectedCount)
    {
        var cases = sections.GetProperty(sectionName).GetProperty("Cases");
        Assert.Equal(expectedCount, cases.EnumerateObject().Count());
        foreach (var benchmarkCase in cases.EnumerateObject())
        {
            var measurement = benchmarkCase.Value;
            var requestedIterations = measurement.GetProperty("RequestedIterations").GetInt32();
            Assert.Equal("AlternatingSixLaneV1", measurement.GetProperty("MeasurementProtocol").GetString());
            Assert.True(requestedIterations > 0, benchmarkCase.Name);
            Assert.True(measurement.GetProperty("EffectiveIterations").GetInt32() >= requestedIterations, benchmarkCase.Name);
            Assert.True(measurement.GetProperty("Samples").GetInt32() >= 5, benchmarkCase.Name);
            AssertPositiveRow(sections.GetProperty(sectionName), benchmarkCase.Name);

            var environment = measurement.GetProperty("Environment");
            Assert.False(environment.GetProperty("TrackedDirty").GetBoolean(), benchmarkCase.Name);
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("SourceCommit").GetString()), benchmarkCase.Name);
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("Runtime").GetString()), benchmarkCase.Name);
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("OperatingSystem").GetString()), benchmarkCase.Name);
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("Processor").GetString()), benchmarkCase.Name);
            Assert.False(string.IsNullOrWhiteSpace(environment.GetProperty("TieredPgo").GetString()), benchmarkCase.Name);
        }
    }

    private static void AssertPairedSections(JsonElement sections, string firstName, string secondName)
    {
        var first = sections.GetProperty(firstName).GetProperty("Cases");
        var second = sections.GetProperty(secondName).GetProperty("Cases");
        foreach (var benchmarkCase in first.EnumerateObject())
        {
            var other = second.GetProperty(benchmarkCase.Name);
            Assert.Equal(
                ProjectPairedMeasurement(benchmarkCase.Value),
                ProjectPairedMeasurement(other));
        }
    }

    private static string ProjectPairedMeasurement(JsonElement measurement)
        => (
            measurement.GetProperty("RequestedIterations").GetRawText() + ";" +
            measurement.GetProperty("EffectiveIterations").GetRawText() + ";" +
            measurement.GetProperty("Samples").GetRawText() + ";" +
            measurement.GetProperty("Utf8Regex").GetRawText() + ";" +
            measurement.GetProperty("Utf8Compiled").GetRawText() + ";" +
            measurement.GetProperty("PredecodedRegex").GetRawText() + ";" +
            measurement.GetProperty("CompiledRegex").GetRawText() + ";" +
            measurement.GetProperty("DecodeThenRegex").GetRawText() + ";" +
            measurement.GetProperty("DecodeThenCompiledRegex").GetRawText() + ";" +
            measurement.GetProperty("Utf8RegexAllocatedBytes").GetRawText() + ";" +
            measurement.GetProperty("Utf8CompiledAllocatedBytes").GetRawText() + ";" +
            measurement.GetProperty("PredecodedRegexAllocatedBytes").GetRawText() + ";" +
            measurement.GetProperty("CompiledRegexAllocatedBytes").GetRawText() + ";" +
            measurement.GetProperty("DecodeThenRegexAllocatedBytes").GetRawText() + ";" +
            measurement.GetProperty("DecodeThenCompiledRegexAllocatedBytes").GetRawText());

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
