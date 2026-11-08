using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lokad.Utf8Regex.Benchmarks;

namespace Lokad.Utf8Regex.PythonRe.Tests;

[Trait("Category", "SourceOnly")]
public sealed class PythonReBenchmarkSnapshotTests
{
    [Fact]
    public void CompactSummaryPreservesCatalogQualificationAndReadme()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("bench/Snapshots/PythonRe.json")));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("PythonRe", root.GetProperty("Profile").GetString());
        var evidence = root.GetProperty("Evidence");
        var hash = evidence.GetProperty("SnapshotSha256").GetString()!;
        Assert.Matches("^[0-9A-F]{64}$", hash);
        Assert.Matches("^[0-9A-F]{64}$", root.GetProperty("CatalogSha256").GetString());
        var catalog = PythonReBenchmarkCatalog.Cases.ToDictionary(static row => row.Id, StringComparer.Ordinal);
        var sections = root.GetProperty("Sections");
        Assert.Equal(catalog.Count, sections.EnumerateObject().Sum(static section => section.Value.EnumerateObject().Count()));
        var page = File.ReadAllText(FindRepositoryFile("src/Lokad.Utf8Regex.PythonRe/README.md")).ReplaceLineEndings("\n");
        Assert.Contains($"Snapshot SHA-256: `{hash}`", page, StringComparison.Ordinal);
        Assert.Contains("<!-- BEGIN GENERATED PYTHONRE BENCHMARKS -->", page, StringComparison.Ordinal);
        Assert.Contains("<!-- END GENERATED PYTHONRE BENCHMARKS -->", page, StringComparison.Ordinal);
        Assert.DoesNotContain(".NET predecoded", page, StringComparison.Ordinal);
        foreach (var section in sections.EnumerateObject())
        {
            var start = page.IndexOf($"### {section.Name}\n", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var end = page.IndexOf("\n### ", start + 1, StringComparison.Ordinal);
            var body = end < 0 ? page[start..] : page[start..end];
            var previous = -1;
            foreach (var item in section.Value.EnumerateObject().Select(static row =>
                (Row: row, Ratio: row.Value.GetProperty("Status").GetString() == "Unqualified"
                    ? (double?)null : row.Value.GetProperty("Ratio").GetDouble()))
                .OrderByDescending(static item => item.Ratio ?? double.NegativeInfinity)
                .ThenBy(static item => item.Row.Name, StringComparer.Ordinal))
            {
                var row = item.Row.Value;
                var definition = catalog[item.Row.Name];
                Assert.Equal(definition.Operation.ToString(), row.GetProperty("Operation").GetString());
                Assert.Equal(section.Name, definition.Coverage.Section);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(definition.Input))), row.GetProperty("InputSha256").GetString());
                var source = root.GetProperty("Sources").GetProperty(row.GetProperty("Source").GetString()!);
                Assert.False(source.GetProperty("Managed").GetProperty("TrackedDirty").GetBoolean());
                var status = row.GetProperty("Status").GetString();
                Assert.Contains(status, new[] { "ManagedFaster", "CpythonFaster", "Equivalent", "Inconclusive", "Unqualified" });
                if (item.Ratio is double ratio)
                {
                    var lower = row.GetProperty("Lower95").GetDouble();
                    var upper = row.GetProperty("Upper95").GetDouble();
                    Assert.InRange(ratio, lower, upper);
                    if (status == "ManagedFaster") Assert.True(upper < .98);
                    if (status == "CpythonFaster") Assert.True(lower > 1.02);
                    if (status == "Equivalent") Assert.True(lower >= .98 && upper <= 1.02);
                }
                else Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("Reason").GetString()));
                Assert.True(row.GetProperty("ManagedBytes").GetDouble() >= 0);
                var at = body.IndexOf($"| `{item.Row.Name}` |", StringComparison.Ordinal);
                Assert.True(at > previous, $"Missing/misordered row: {item.Row.Name}");
                previous = at;
                var cells = body[at..].Split('\n')[0].Split('|');
                Assert.Equal(row.GetProperty("ManagedUs").GetDouble().ToString("N3", CultureInfo.InvariantCulture) + " us", cells[3].Trim());
                Assert.Equal(row.GetProperty("ReferenceUs").GetDouble().ToString("N3", CultureInfo.InvariantCulture) + " us", cells[4].Trim());
                Assert.Equal(item.Ratio is double value ? value.ToString("F2", CultureInfo.InvariantCulture) + "x" : "—", cells[6].Trim());
            }
        }
        Assert.Equal(4, root.GetProperty("Lifecycle").EnumerateObject().Count());
        Assert.Equal(7, root.GetProperty("Scaling").EnumerateObject().Count());
        foreach (var family in root.GetProperty("Scaling").EnumerateObject())
        {
            Assert.True(family.Value.GetProperty("Points").GetInt32() > 0);
            Assert.Contains($"| `{family.Name}` |", page, StringComparison.Ordinal);
            Assert.False(string.IsNullOrWhiteSpace(family.Value.GetProperty("FitGate").GetString()));
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
