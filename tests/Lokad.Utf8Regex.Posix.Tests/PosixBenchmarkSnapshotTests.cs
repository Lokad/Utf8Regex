using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex.Benchmarks;

namespace Lokad.Utf8Regex.Posix.Tests;

[Trait("Category", "SourceOnly")]
public sealed class PosixBenchmarkSnapshotTests
{
    [Fact]
    public void CompactSummaryKeepsCatalogScopeAndRankedReadmeWithoutRawEvidence()
    {
        using var document = JsonDocument.Parse(Read("bench/Snapshots/Posix.json"));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("Posix", root.GetProperty("Profile").GetString());
        Assert.Equal("PCRE.NET", root.GetProperty("Comparator").GetProperty("PackageId").GetString());
        var hash = root.GetProperty("Evidence").GetProperty("SnapshotSha256").GetString()!;
        Assert.Matches("^[0-9A-F]{64}$", hash);
        var page = Read("src/Lokad.Utf8Regex.Posix/README.md").ReplaceLineEndings("\n");
        Assert.Contains($"Snapshot SHA-256: `{hash}`", page, StringComparison.Ordinal);
        Assert.DoesNotContain(".NET predecoded", page, StringComparison.Ordinal);
        var sections = root.GetProperty("Sections");
        Assert.Equal(PosixBenchmarkCatalog.Cases.Length, new[] { "Operations", "HistoryScaling", "AdapterControl" }
            .Sum(section => sections.GetProperty(section).EnumerateObject().Count()));
        var previous = -1;
        foreach (var section in new[] { "Operations", "HistoryScaling", "AdapterControl" })
        foreach (var item in sections.GetProperty(section).EnumerateObject()
            .OrderByDescending(item => section == "Operations" ? DisplayRatio(item.Value) ?? double.NegativeInfinity
                : section == "HistoryScaling" ? -PosixBenchmarkCatalog.Get(item.Name).Input.Length : 0)
            .ThenBy(static item => item.Name, StringComparer.Ordinal))
        {
            var row = item.Value;
            var definition = PosixBenchmarkCatalog.Get(item.Name);
            Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition.Input))), row.GetProperty("InputSha256").GetString());
            Assert.False(root.GetProperty("Sources").GetProperty(row.GetProperty("Source").GetString()!)
                .GetProperty("Environment").GetProperty("TrackedDirty").GetBoolean());
            var status = row.GetProperty("Status").GetString();
            Assert.Contains(status, new[] { "ManagedFaster", "NativeFaster", "Equivalent", "Inconclusive", "Excluded", "Unqualified" });
            if (status == "Excluded") Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("Reason").GetString()));
            else if (status != "Unqualified")
                Assert.InRange(row.GetProperty("Ratio").GetDouble(), row.GetProperty("Lower95").GetDouble(), row.GetProperty("Upper95").GetDouble());
            var at = page.IndexOf($"| `{item.Name}` |", StringComparison.Ordinal);
            Assert.True(at > previous, $"Missing/misordered row: {item.Name}");
            previous = at;
            var cells = page[at..].Split('\n')[0].Split('|');
            var scale = section == "AdapterControl" ? definition.Records : 1;
            Assert.Equal((row.GetProperty("ManagedUs").GetDouble() / scale).ToString("F2", CultureInfo.InvariantCulture),
                cells[section == "AdapterControl" ? 2 : 3].Trim());
            if (section != "HistoryScaling")
                Assert.Equal(DisplayRatio(row)?.ToString("F2", CultureInfo.InvariantCulture) ?? "—",
                    cells[section == "AdapterControl" ? 5 : 7].Trim());
            else Assert.Equal(row.GetProperty("PeakWorkingBytes").GetUInt64().ToString(CultureInfo.InvariantCulture), cells[5].Trim());
            foreach (var lane in new[] { "FiniteLive", "FundedFiniteLive", "FiniteDeadlineLive" })
            {
                var measured = sections.GetProperty(lane).TryGetProperty(item.Name, out var policy);
                Assert.Equal(definition.MeasurePolicies, measured);
                if (measured) Assert.True(policy.GetProperty("ManagedUs").GetDouble() > 0);
            }
        }
    }

    private static double? DisplayRatio(JsonElement row) => row.GetProperty("Status").GetString() is "Excluded" or "Unqualified"
        ? null : row.TryGetProperty("Ratio", out var ratio) ? ratio.GetDouble() : null;

    private static string Read(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, name);
            if (File.Exists(path)) return File.ReadAllText(path);
        }
        throw new FileNotFoundException(name);
    }
}
