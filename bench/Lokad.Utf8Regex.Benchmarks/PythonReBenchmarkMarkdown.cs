using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const string PythonReBenchmarkPageRelativePath = "src/Lokad.Utf8Regex.PythonRe/README.md";

    private static int EmitPythonReBenchmarkMarkdown()
    {
        Console.Write(BuildPythonReBenchmarkMarkdown(LoadPythonReBenchmarkSnapshot()));
        return 0;
    }

    private static int RewritePythonReBenchmarkMarkdown()
    {
        RewritePythonReBenchmarkMarkdown(LoadPythonReBenchmarkSnapshot());
        Console.WriteLine($"Benchmark README: {GetPythonReBenchmarkPagePath()}");
        return 0;
    }

    private static int VerifyPythonReBenchmarkMarkdown()
    {
        var path = GetPythonReBenchmarkPagePath();
        var actual = File.ReadAllText(path, Encoding.UTF8);
        var expected = BenchmarkFileWriter.ReplaceGeneratedSection(
            actual, "PYTHONRE", BuildPythonReBenchmarkMarkdown(LoadPythonReBenchmarkSnapshot()));
        if (actual.ReplaceLineEndings("\n") == expected.ReplaceLineEndings("\n"))
        {
            Console.WriteLine($"PythonRe benchmark README is current: {path}");
            return 0;
        }
        Console.Error.WriteLine("PythonRe benchmark README is stale; run --rewrite-pythonre-benchmark-markdown.");
        return 1;
    }

    private static PythonReBenchmarkSnapshot LoadPythonReBenchmarkSnapshot()
    {
        var snapshot = JsonSerializer.Deserialize<PythonReBenchmarkSnapshot>(
            File.ReadAllText(FindRepositoryFile(SnapshotFileName), Encoding.UTF8));
        return snapshot is { SchemaVersion: PythonReBenchmarkSchemaVersion }
            ? snapshot
            : throw new InvalidOperationException($"{SnapshotFileName} requires schema {PythonReBenchmarkSchemaVersion}.");
    }

    private static void RewritePythonReBenchmarkMarkdown(PythonReBenchmarkSnapshot snapshot)
    {
        var path = GetPythonReBenchmarkPagePath();
        BenchmarkFileWriter.WriteTextAtomically(path, BenchmarkFileWriter.ReplaceGeneratedSection(
            File.ReadAllText(path, Encoding.UTF8), "PYTHONRE", BuildPythonReBenchmarkMarkdown(snapshot)));
    }

    private static string GetPythonReBenchmarkPagePath() =>
        Path.Combine(Path.GetDirectoryName(FindRepositoryFile("README.md"))!, PythonReBenchmarkPageRelativePath);

    private static string BuildPythonReBenchmarkMarkdown(PythonReBenchmarkSnapshot snapshot)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var rows = snapshot.Cases.OrderBy(static row => row.Key, StringComparer.Ordinal).ToArray();
        var counts = rows.GroupBy(static row => row.Value.Qualification?.Status ?? "Unqualified")
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
        writer.WriteLine("## Benchmarks");
        writer.WriteLine();
        writer.WriteLine("Public PythonRe operations over strict UTF-8, compared with official CPython `re` over an already-decoded `str`. Times are microseconds per operation; startup and pattern compilation are excluded. Required result materialization is included. CPython uses a long-lived worker. Decode-inclusive and .NET controls remain in the [full snapshot](../../PythonRe.Benchmarks.json).");
        writer.WriteLine();
        writer.WriteLine("Tables rank the paired `PythonRe / CPython re` ratio from highest to lowest within each operation group; lower is better. Both displayed times come from the same paired evidence when available. Status retains the paired 95% interval and harness-floor qualification: a point ratio alone does not establish a winner. Unqualified observations appear last without a qualified ratio.");
        writer.WriteLine();
        writer.WriteLine($"Snapshot SHA-256: `{ComputePythonReSnapshotSha256()}`. Catalog SHA-256: `{snapshot.CatalogSha256}`.");
        writer.WriteLine();
        writer.WriteLine($"Public Status: `{counts.GetValueOrDefault("ManagedFaster")}` managed faster, " +
            $"`{counts.GetValueOrDefault("Equivalent")}` equivalent, `{counts.GetValueOrDefault("CpythonFaster")}` CPython faster, " +
            $"`{counts.GetValueOrDefault("Inconclusive")}` inconclusive, `{counts.GetValueOrDefault("Unqualified")}` unqualified.");
        writer.WriteLine();
        foreach (var source in rows.Select(static row => row.Value.Qualification?.PairedEvidence)
            .OfType<PythonRePairedEvidence>().GroupBy(static pair => pair.SourceCommit, StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            var runtimes = string.Join(", ", source.Select(static pair => pair.ManagedEnvironment.Runtime).Distinct(StringComparer.Ordinal));
            var versions = string.Join(", ", source.Select(static pair => pair.CpythonEnvironment.Version).Distinct(StringComparer.Ordinal));
            writer.WriteLine($"Measured source `{source.Key}`: {runtimes}; CPython {versions}. Selective refreshes retain each row's recorded identity.");
        }
        foreach (var section in s_pythonReCoverageSections)
        {
            var sectionRows = rows.Where(row => row.Value.Coverage?.Section == section).ToArray();
            if (sectionRows.Length == 0) continue;
            writer.WriteLine();
            writer.WriteLine($"### {section}");
            writer.WriteLine();
            writer.WriteLine("| Case | Operation | PythonRe elapsed | CPython re elapsed | Status | Ratio (lower is better) |");
            writer.WriteLine("| --- | --- | ---: | ---: | --- | ---: |");
            foreach (var (caseId, row) in sectionRows
                .OrderByDescending(static row => PythonReDisplayRatio(row.Value) ?? double.NegativeInfinity)
                .ThenBy(static row => row.Key, StringComparer.Ordinal))
            {
                var pair = row.Qualification?.PairedEvidence;
                var managed = pair?.ManagedMedianMicroseconds ?? row.PythonRe.MedianMicroseconds;
                var reference = pair?.CpythonMedianMicroseconds ?? row.Cpython?.PredecodedRe.MedianMicroseconds;
                var ratio = PythonReDisplayRatio(row);
                writer.WriteLine($"| `{caseId}` | `{row.Operation}` | {managed:N3} us | " +
                    $"{(reference is > 0 ? $"{reference:N3} us" : "—")} | {FormatPythonReStatus(row.Qualification)} | " +
                    $"{(ratio is > 0 ? $"{ratio:F2}x" : "—")} |");
            }
        }
        writer.WriteLine();
        writer.WriteLine("<details>");
        writer.WriteLine("<summary>Qualification, allocations and lifecycle evidence</summary>");
        writer.WriteLine();
        writer.WriteLine("The full snapshot retains per-row confidence intervals, exclusion reasons, exact input/source/binary identities, allocation probes, CPython operation owners and managed routes. Finite limits and retained-output costs are separate from these ordinary rows. Byte-control ratios are representation-neutral engine diagnostics and do not set public Status.");
        writer.WriteLine();
        foreach (var (caseId, row) in rows.Where(static row => PythonReDisplayRatio(row.Value) is null))
            writer.WriteLine($"- `{caseId}`: {row.Qualification?.StatusReason ?? "No paired qualification evidence."}");
        writer.WriteLine();
        writer.WriteLine("### Construction and first call");
        writer.WriteLine();
        writer.WriteLine("Contextual uncached-construction throughput, separate from warm Status. Values include managed-thread allocations where measured; CPython uses its compiler directly to bypass the compile cache.");
        writer.WriteLine();
        writer.WriteLine("| Family | PythonRe construction | CPython compile | PythonRe first search | CPython first search |");
        writer.WriteLine("| --- | ---: | ---: | ---: | ---: |");
        foreach (var (id, row) in snapshot.Lifecycle.OrderBy(static row => row.Key, StringComparer.Ordinal))
            writer.WriteLine($"| `{id}` | {FormatLifecycle(row.AdapterConstruction)} | {FormatLifecycle(row.CpythonCompile)} | " +
                $"{FormatLifecycle(row.ConstructFirstSearch)} | {FormatLifecycle(row.CpythonCompileFirstSearch)} |");
        writer.WriteLine();
        writer.WriteLine("### Scaling evidence");
        writer.WriteLine();
        writer.WriteLine("Bounded families vary one dimension with equivalent results. Fit gates describe local trends, not implementation winners; full paired samples, allocations and uncertainty remain in the snapshot.");
        writer.WriteLine();
        writer.WriteLine("| Family | Dimension | Points | Fit gate |");
        writer.WriteLine("| --- | --- | ---: | --- |");
        foreach (var (id, row) in snapshot.ScalingFamilies.OrderBy(static row => row.Key, StringComparer.Ordinal))
            writer.WriteLine($"| `{id}` | {row.Dimension} | {row.Points.Count} | {row.FitGate} |");
        writer.WriteLine();
        writer.WriteLine("Regenerate these tables with `--rewrite-pythonre-benchmark-markdown`; validate with `--verify-pythonre-benchmark-markdown`. Use `--verify-pythonre-scaling` for full scaling evidence and `--emit-pythonre-priority-report` for gap details through the repository benchmark script.");
        writer.WriteLine();
        writer.WriteLine("</details>");
        return writer.ToString();

        static string FormatLifecycle(PythonReLifecycleTiming row) => row.ManagedAllocatedBytes is long bytes
            ? $"{row.MedianMicroseconds:N3} us / {bytes:N0} B" : $"{row.MedianMicroseconds:N3} us";
    }

    private static double? PythonReDisplayRatio(PythonReCaseMeasurement row) =>
        row.Qualification is { Status: not "Unqualified", PairedEvidence: { } pair }
            ? pair.StrongRatioMedian : null;

    private static string FormatPythonReStatus(PythonReQualificationMeasurement? row) => row?.Status switch
    {
        "ManagedFaster" => "Managed faster",
        "CpythonFaster" => "CPython faster",
        { } status => status,
        _ => "Unqualified",
    };

    private static string ComputePythonReSnapshotSha256() =>
        BenchmarkSnapshotHash.HashNormalizedFile(FindRepositoryFile(SnapshotFileName));
}
