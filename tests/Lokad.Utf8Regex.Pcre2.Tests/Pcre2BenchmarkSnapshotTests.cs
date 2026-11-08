using System.Globalization;
using System.Text.Json;

namespace Lokad.Utf8Regex.Pcre2.Tests;

[Trait("Category", "SourceOnly")]
public sealed class Pcre2BenchmarkSnapshotTests
{
    [Fact]
    public void CompactSummaryPreservesNativeScopeAndReadme()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(FindRepositoryFile("bench/Snapshots/Pcre2.json")));
        var root = document.RootElement;
        Assert.Equal(1, root.GetProperty("SchemaVersion").GetInt32());
        Assert.Equal("Pcre2", root.GetProperty("Profile").GetString());
        var evidence = root.GetProperty("Evidence");
        var hash = evidence.GetProperty("SnapshotSha256").GetString()!;
        Assert.Matches("^[0-9A-F]{64}$", hash);
        var comparator = root.GetProperty("Comparator");
        Assert.Equal("PCRE.NET", comparator.GetProperty("PackageId").GetString());
        Assert.Equal("1.5.0", comparator.GetProperty("PackageVersion").GetString());
        Assert.StartsWith("10.47", comparator.GetProperty("Engine").GetString(), StringComparison.Ordinal);
        Assert.Matches("^[0-9A-F]{64}$", comparator.GetProperty("BuildFingerprintSha256").GetString());
        var sections = root.GetProperty("Sections");
        Assert.Equal(10, sections.EnumerateObject().Count());
        Assert.Equal(126, sections.EnumerateObject().Sum(static section => section.Value.EnumerateObject().Count()));
        var page = File.ReadAllText(FindRepositoryFile("src/Lokad.Utf8Regex.Pcre2/README.md")).ReplaceLineEndings("\n");
        Assert.Contains($"Snapshot SHA-256: `{hash}`", page, StringComparison.Ordinal);
        Assert.Contains("<!-- BEGIN GENERATED PCRE2 BENCHMARKS -->", page, StringComparison.Ordinal);
        Assert.Contains("<!-- END GENERATED PCRE2 BENCHMARKS -->", page, StringComparison.Ordinal);
        Assert.DoesNotContain(".NET predecoded", page, StringComparison.Ordinal);
        foreach (var section in sections.EnumerateObject())
        {
            var operation = section.Name.Split('-')[^1] switch
            {
                "ismatch" => "IsMatch", "count" => "Count", "enumerate" => "EnumerateMatches",
                "matchmany" => "MatchMany", "replace" => "Replace", _ => throw new InvalidOperationException(),
            };
            var title = section.Name.StartsWith("pcre2-managed-compatible-", StringComparison.Ordinal) ? "Compatible" : "PCRE2-only";
            var start = page.IndexOf($"### {title} {operation}\n", StringComparison.Ordinal);
            Assert.True(start >= 0);
            var end = page.IndexOf("\n### ", start + 1, StringComparison.Ordinal);
            var body = end < 0 ? page[start..] : page[start..end];
            var previous = -1;
            foreach (var item in section.Value.EnumerateObject().Select(static row =>
                (Row: row, Ratio: row.Value.GetProperty("Status").GetString() is "Unqualified" or "Excluded"
                    ? (double?)null : row.Value.GetProperty("Ratio").GetDouble()))
                .OrderByDescending(static item => item.Ratio ?? double.NegativeInfinity)
                .ThenBy(static item => item.Row.Name, StringComparer.Ordinal))
            {
                var row = item.Row.Value;
                var source = root.GetProperty("Sources").GetProperty(row.GetProperty("Source").GetString()!);
                Assert.False(source.GetProperty("TrackedDirty").GetBoolean());
                var status = row.GetProperty("Status").GetString();
                Assert.Contains(status, new[] { "ManagedFaster", "NativeFaster", "Equivalent", "Inconclusive", "Excluded", "Unqualified" });
                if (item.Ratio is double ratio)
                {
                    var lower = row.GetProperty("Lower95").GetDouble();
                    var upper = row.GetProperty("Upper95").GetDouble();
                    Assert.InRange(ratio, lower, upper);
                    if (status == "ManagedFaster") Assert.True(upper < .98);
                    if (status == "NativeFaster") Assert.True(lower > 1.02);
                    if (status == "Equivalent") Assert.True(lower >= .98 && upper <= 1.02);
                }
                else Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("Reason").GetString()));
                Assert.True(row.GetProperty("ManagedBytes").GetDouble() >= 0);
                var at = body.IndexOf($"| `{item.Row.Name}` |", StringComparison.Ordinal);
                Assert.True(at > previous, $"Missing/misordered row: {section.Name}/{item.Row.Name}");
                previous = at;
                var cells = body[at..].Split('\n')[0].Split('|');
                Assert.Equal(row.GetProperty("ManagedUs").GetDouble().ToString("N3", CultureInfo.InvariantCulture) + " us", cells[2].Trim());
                var reference = row.TryGetProperty("ReferenceUs", out var value) ? value.GetDouble() : (double?)null;
                Assert.Equal(reference is > 0 ? reference.Value.ToString("N3", CultureInfo.InvariantCulture) + " us" : "—", cells[3].Trim());
                Assert.Equal(item.Ratio is double r ? r.ToString("F2", CultureInfo.InvariantCulture) + "x" : "—", cells[5].Trim());
            }
        }
        Assert.Equal(16, root.GetProperty("Scaling").EnumerateObject().Count());
        foreach (var family in root.GetProperty("Scaling").EnumerateObject())
            Assert.Contains($"| `{family.Name}` |", page, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"PCRE.NET\" Version=\"1.5.0\" PrivateAssets=\"all\" />",
            File.ReadAllText(FindRepositoryFile("bench/Lokad.Utf8Regex.Benchmarks/Lokad.Utf8Regex.Benchmarks.csproj")), StringComparison.Ordinal);
        Assert.DoesNotContain("PCRE.NET", File.ReadAllText(FindRepositoryFile("src/Lokad.Utf8Regex.Pcre2/Lokad.Utf8Regex.Pcre2.csproj")), StringComparison.OrdinalIgnoreCase);
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
