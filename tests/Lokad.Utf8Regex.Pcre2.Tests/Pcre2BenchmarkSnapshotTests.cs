using System.Text.Json;
using System.Security.Cryptography;

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
        Assert.Equal(5, root.GetProperty("SchemaVersion").GetInt32());

        var dependency = root.GetProperty("PcreNetNativeBaseline");
        Assert.Equal("PCRE.NET", dependency.GetProperty("PackageId").GetString());
        Assert.Equal("1.5.0", dependency.GetProperty("PackageVersion").GetString());
        Assert.StartsWith("10.47", dependency.GetProperty("NativePcre2Version").GetString(), StringComparison.Ordinal);
        Assert.Equal(
            "Zu3NJGiU1S7tHHaW4UdEK1WZ9LFYqPI+6Y0eiL6YPHVOHSoWjbq0x5j3uN9895DoIgO5XI/50S6dj2ZmRHirNA==",
            dependency.GetProperty("PackageSha512").GetString());

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
        Assert.Equal(100, operationRows.Count(static row =>
            row.Value.TryGetProperty("PcreNetNative", out var native) && native.GetDouble() > 0));
        Assert.Equal(100, operationRows.Count(static row =>
            row.Value.GetProperty("PcreNetNativeStatus").GetString() == "Unqualified"));
        Assert.Equal(26, operationRows.Count(static row =>
            row.Value.GetProperty("PcreNetNativeStatus").GetString() == "Excluded"));
        Assert.All(operationRows, static row =>
        {
            var hasNative = row.Value.TryGetProperty("PcreNetNative", out var native) && native.GetDouble() > 0;
            var hasReason = row.Value.TryGetProperty("PcreNetNativeUnavailableReason", out var reason) &&
                !string.IsNullOrWhiteSpace(reason.GetString());
            Assert.True(hasNative || hasReason, $"Missing native PCRE2 disposition for '{row.Name}'.");
            Assert.Equal(
                hasNative ? "Unqualified" : "Excluded",
                row.Value.GetProperty("PcreNetNativeStatus").GetString());
        });

        var snapshotPath = FindRepositoryFile("PCRE2.Benchmarks.json");
        var page = File.ReadAllText(FindRepositoryFile("src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md"));
        var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotPath)));
        Assert.Contains($"Snapshot SHA-256: `{hash}`", page, StringComparison.Ordinal);
        Assert.Contains("PCRE.NET / PCRE2 NFA CPU", page, StringComparison.Ordinal);
        Assert.Contains("`100` unqualified, `26` excluded", page, StringComparison.Ordinal);
        Assert.Contains("| Package | Version | Native engine |", page, StringComparison.Ordinal);
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
