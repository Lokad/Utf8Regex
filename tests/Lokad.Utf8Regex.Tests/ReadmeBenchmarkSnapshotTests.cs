using System.Text.Json;

namespace Lokad.Utf8Regex.Tests;

public sealed class ReadmeBenchmarkSnapshotTests
{
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
