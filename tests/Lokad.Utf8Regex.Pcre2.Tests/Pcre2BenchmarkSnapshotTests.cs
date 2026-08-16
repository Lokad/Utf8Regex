using System.Text.Json;

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
        Assert.Equal(3, root.GetProperty("SchemaVersion").GetInt32());

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
