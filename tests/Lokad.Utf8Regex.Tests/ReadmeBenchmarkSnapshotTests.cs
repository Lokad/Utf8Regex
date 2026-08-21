using System.Text.Json;

namespace Lokad.Utf8Regex.Tests;

public sealed class ReadmeBenchmarkSnapshotTests
{
    [Fact]
    public void SnapshotContainsCompleteCleanCalibratedSectionPairs()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("README.Benchmarks.json")));
        var root = document.RootElement;
        Assert.Equal(3, root.GetProperty("SchemaVersion").GetInt32());

        var sections = root.GetProperty("Sections");
        AssertSection(sections, "dotnet-performance", 55);
        AssertSection(sections, "dotnet-performance-compiled", 55);
        AssertSection(sections, "lokad", 34);
        AssertSection(sections, "lokad-compiled", 34);
        AssertPairedSections(sections, "dotnet-performance", "dotnet-performance-compiled");
        AssertPairedSections(sections, "lokad", "lokad-compiled");
    }

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
    }

    private static void AssertSection(JsonElement sections, string sectionName, int expectedCount)
    {
        var cases = sections.GetProperty(sectionName).GetProperty("Cases");
        Assert.Equal(expectedCount, cases.EnumerateObject().Count());
        foreach (var benchmarkCase in cases.EnumerateObject())
        {
            var measurement = benchmarkCase.Value;
            var requestedIterations = measurement.GetProperty("RequestedIterations").GetInt32();
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

    private static (int Requested, int Effective, int Samples, double Utf8, double Utf8Compiled, double Predecoded, double Compiled, double Decode, double DecodeCompiled) ProjectPairedMeasurement(JsonElement measurement)
        => (
            measurement.GetProperty("RequestedIterations").GetInt32(),
            measurement.GetProperty("EffectiveIterations").GetInt32(),
            measurement.GetProperty("Samples").GetInt32(),
            measurement.GetProperty("Utf8Regex").GetDouble(),
            measurement.GetProperty("Utf8Compiled").GetDouble(),
            measurement.GetProperty("PredecodedRegex").GetDouble(),
            measurement.GetProperty("CompiledRegex").GetDouble(),
            measurement.GetProperty("DecodeThenRegex").GetDouble(),
            measurement.GetProperty("DecodeThenCompiledRegex").GetDouble());

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
