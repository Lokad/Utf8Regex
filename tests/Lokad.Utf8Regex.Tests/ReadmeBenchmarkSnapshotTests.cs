using System.Globalization;
using System.Text.Json;

namespace Lokad.Utf8Regex.Tests;

[Trait("Category", "SourceOnly")]
public sealed class ReadmeBenchmarkSnapshotTests
{
    [Fact]
    public void SummaryPreservesQualifiedRowsAndEvidenceIdentity()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("bench/Snapshots/Core.json")));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("Core", root.GetProperty("Profile").GetString());
        var evidence = root.GetProperty("Evidence");
        var hash = evidence.GetProperty("SnapshotSha256").GetString()!;
        Assert.Matches("^[0-9A-F]{64}$", hash);
        var sections = root.GetProperty("Sections");
        foreach (var (section, count) in new[] { ("dotnet-performance", 55), ("dotnet-performance-compiled", 55), ("lokad", 34), ("lokad-compiled", 34) })
        {
            var rows = sections.GetProperty(section);
            Assert.Equal(count, rows.EnumerateObject().Count());
            foreach (var row in rows.EnumerateObject())
            {
                var value = row.Value;
                Assert.True(value.GetProperty("ManagedUs").GetDouble() > 0);
                Assert.True(value.GetProperty("ReferenceUs").GetDouble() > 0);
                Assert.True(value.GetProperty("DecodeUs").GetDouble() > 0);
                Assert.True(value.GetProperty("ManagedBytes").GetDouble() >= 0);
                Assert.True(value.GetProperty("ReferenceBytes").GetDouble() >= 0);
                var source = root.GetProperty("Sources").GetProperty(value.GetProperty("Source").GetString()!);
                var status = value.GetProperty("Status").GetString();
                Assert.Contains(status, new[] { "Win", "TieCandidate", "Gap", "Unqualified" });
                if (status == "Unqualified")
                {
                    Assert.False(string.IsNullOrWhiteSpace(value.GetProperty("Reason").GetString()));
                    continue;
                }
                Assert.False(source.GetProperty("TrackedDirty").GetBoolean());
                Assert.False(string.IsNullOrWhiteSpace(source.GetProperty("CoreAssembly").GetString()));
                var ratio = value.GetProperty("ManagedUs").GetDouble() / value.GetProperty("ReferenceUs").GetDouble();
                Assert.Equal(ratio, value.GetProperty("Ratio").GetDouble());
                Assert.Equal(ratio <= .98 ? "Win" : ratio <= 1.02 ? "TieCandidate" : "Gap", status);
            }
        }
        Assert.Equal("MatchLoop", sections.GetProperty("lokad").GetProperty("lokad/lexer/number").GetProperty("Operation").GetString());
        Assert.Equal("Count", sections.GetProperty("lokad").GetProperty("lokad/imports/module-imports").GetProperty("Operation").GetString());
    }

    [Fact]
    public void ReadmeTablesMatchSummaryValuesAndDescendingRatios()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("bench/Snapshots/Core.json")));
        var readme = File.ReadAllText(FindRepositoryFile("README.md")).ReplaceLineEndings("\n");
        foreach (var (section, marker) in new[] { ("dotnet-performance", "DOTNET_PERFORMANCE"), ("dotnet-performance-compiled", "DOTNET_PERFORMANCE_COMPILED"), ("lokad", "LOKAD"), ("lokad-compiled", "LOKAD_COMPILED") })
        {
            var start = readme.IndexOf($"<!-- BEGIN GENERATED {marker} BENCHMARKS -->", StringComparison.Ordinal);
            var end = readme.IndexOf($"<!-- END GENERATED {marker} BENCHMARKS -->", StringComparison.Ordinal);
            Assert.True(start >= 0 && end > start);
            var rows = document.RootElement.GetProperty("Sections").GetProperty(section);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var previous = double.PositiveInfinity;
            var offset = 2;
            foreach (var line in readme[start..end].Split('\n'))
            {
                if (line.StartsWith("| Case |", StringComparison.Ordinal))
                {
                    previous = double.PositiveInfinity;
                    offset = line.Contains("| Operation |", StringComparison.Ordinal) ? 3 : 2;
                }
                if (!line.StartsWith("| `", StringComparison.Ordinal)) continue;
                var cells = line.Split('|');
                var id = cells[1].Trim().Trim('`');
                Assert.True(seen.Add(id), $"Duplicate table row: {section}/{id}");
                var row = rows.GetProperty(id);
                var managed = row.GetProperty("ManagedUs").GetDouble();
                var reference = row.GetProperty("ReferenceUs").GetDouble();
                Assert.Equal(managed.ToString("N3", CultureInfo.InvariantCulture) + " us", cells[offset].Trim());
                Assert.Equal(reference.ToString("N3", CultureInfo.InvariantCulture) + " us", cells[offset + 1].Trim());
                Assert.Equal(row.GetProperty("DecodeUs").GetDouble().ToString("N3", CultureInfo.InvariantCulture) + " us", cells[offset + 2].Trim());
                var ratio = managed / reference;
                Assert.True(ratio <= previous, $"Wrong ratio order: {section}/{id}");
                Assert.Equal(ratio.ToString(ratio is > 0 and < .01 ? "G2" : "F2", CultureInfo.InvariantCulture) + "x", cells[^2].Trim());
                previous = ratio;
            }
            Assert.Equal(rows.EnumerateObject().Count(), seen.Count);
        }
    }

    private static string FindRepositoryFile(string name)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, name);
            if (File.Exists(path)) return path;
        }
        throw new FileNotFoundException(name);
    }
}
