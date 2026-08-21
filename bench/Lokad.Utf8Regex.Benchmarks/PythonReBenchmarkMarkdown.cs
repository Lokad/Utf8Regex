using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const string PythonReBenchmarkPageRelativePath = "src/Lokad.Utf8Regex.PythonRe/BENCHMARKS.md";

    private static int EmitPythonReBenchmarkMarkdown()
    {
        Console.Write(BuildPythonReBenchmarkMarkdown(LoadPythonReBenchmarkSnapshot()));
        return 0;
    }

    private static int RewritePythonReBenchmarkMarkdown()
    {
        var snapshot = LoadPythonReBenchmarkSnapshot();
        RewritePythonReBenchmarkMarkdown(snapshot);
        Console.WriteLine($"Benchmark page      : {GetPythonReBenchmarkPagePath()}");
        return 0;
    }

    private static int VerifyPythonReBenchmarkMarkdown()
    {
        var expected = BuildPythonReBenchmarkMarkdown(LoadPythonReBenchmarkSnapshot());
        var path = GetPythonReBenchmarkPagePath();
        var actual = File.ReadAllText(path, Encoding.UTF8);
        if (actual.ReplaceLineEndings("\n") == expected.ReplaceLineEndings("\n"))
        {
            Console.WriteLine($"PythonRe benchmark page is current: {path}");
            return 0;
        }

        Console.Error.WriteLine(
            $"PythonRe benchmark page is stale: {path}{Environment.NewLine}" +
            "Run --rewrite-pythonre-benchmark-markdown or refresh the PythonRe snapshot.");
        return 1;
    }

    private static PythonReBenchmarkSnapshot LoadPythonReBenchmarkSnapshot()
    {
        var path = FindRepositoryFile(SnapshotFileName);
        var snapshot = JsonSerializer.Deserialize<PythonReBenchmarkSnapshot>(File.ReadAllText(path, Encoding.UTF8));
        return snapshot is { SchemaVersion: 2 }
            ? snapshot
            : throw new InvalidOperationException($"{SnapshotFileName} is missing or has an unsupported schema version.");
    }

    private static void RewritePythonReBenchmarkMarkdown(PythonReBenchmarkSnapshot snapshot)
    {
        var path = GetPythonReBenchmarkPagePath();
        BenchmarkFileWriter.WriteTextAtomically(path, BuildPythonReBenchmarkMarkdown(snapshot));
    }

    private static string GetPythonReBenchmarkPagePath() =>
        Path.Combine(Path.GetDirectoryName(FindRepositoryFile("README.md"))!, PythonReBenchmarkPageRelativePath);

    private static string BuildPythonReBenchmarkMarkdown(PythonReBenchmarkSnapshot snapshot)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var rows = snapshot.Cases.ToArray();
        var comparableRows = rows
            .Where(static row => row.Value.DecodeThenRegex.MedianMicroseconds > 0)
            .ToArray();
        var parityCount = comparableRows.Count(static row =>
            row.Value.PythonRe.MedianMicroseconds <= row.Value.DecodeThenRegex.MedianMicroseconds);
        var environments = rows
            .Select(static row => row.Value.Environment)
            .DistinctBy(static environment => new
            {
                environment.SourceCommit,
                environment.Runtime,
                environment.OperatingSystem,
                environment.Processor,
            })
            .ToArray();

        writer.WriteLine("<!-- This file is generated from ../../PythonRe.Benchmarks.json. Do not edit benchmark rows by hand. -->");
        writer.WriteLine();
        writer.WriteLine("# Lokad.Utf8Regex.PythonRe benchmarks");
        writer.WriteLine();
        writer.WriteLine("This page is the self-contained performance snapshot for the optional Python `re` adapter. The source of truth is [`PythonRe.Benchmarks.json`](../../PythonRe.Benchmarks.json); `--refresh-pythonre-benchmarks` and `--refresh-pythonre-benchmark-case` update the JSON and regenerate this page.");
        writer.WriteLine();
        writer.WriteLine("The comparison is deliberately limited to cases where the requested work has equivalent managed semantics:");
        writer.WriteLine();
        writer.WriteLine("- `PythonRe`: `Utf8PythonRegex` over UTF-8 input.");
        writer.WriteLine("- `.NET predecoded`: `System.Text.RegularExpressions.Regex` over an already-decoded `string`.");
        writer.WriteLine("- `.NET + decode`: strict UTF-8 decoding on every operation followed by `.NET Regex`; this is the primary end-to-end baseline.");
        writer.WriteLine();
        writer.WriteLine("Enumeration, split, and replacement rows include the result materialization needed by the public operation. The predecoded column is a matcher/runtime lower bound, not an end-to-end parity requirement. CPython process startup or interop is intentionally not benchmarked because it would not be an equivalent in-process operation.");
        writer.WriteLine();
        writer.WriteLine("## Snapshot summary");
        writer.WriteLine();
        writer.WriteLine($"- Generated: `{snapshot.GeneratedAtUtc.ToUniversalTime():O}`");
        writer.WriteLine($"- Snapshot SHA-256: `{ComputePythonReSnapshotSha256()}`");
        writer.WriteLine($"- Cases: `{rows.Length}`");
        writer.WriteLine($"- At or below the decode-then-.NET median: `{parityCount}/{comparableRows.Length}`");
        writer.WriteLine($"- Measurement environments represented: `{environments.Length}`");
        writer.WriteLine($"- Corpus: [`{snapshot.Corpus.SourceFile}`](../../{snapshot.Corpus.SourceFile}) (`{snapshot.Corpus.VectorCount}` vectors, SHA-256 `{snapshot.Corpus.Sha256}`)");
        writer.WriteLine($"- Corpus provenance limitation: {snapshot.Corpus.Limitation}");
        writer.WriteLine();

        if (environments.Length == 1)
        {
            var environment = environments[0];
            writer.WriteLine($"Measured from source `{environment.SourceCommit}` on {environment.Runtime}, {environment.OperatingSystem}, {environment.Processor}.");
        }
        else
        {
            writer.WriteLine("Rows span more than one measurement environment. Consult the JSON row metadata before interpreting small differences as regressions or wins.");
        }

        writer.WriteLine();
        writer.WriteLine("## Results");
        writer.WriteLine();
        writer.WriteLine("`vs decode` is `PythonRe / .NET + decode`; lower is better, and `1.00x` is exact median parity. Times are medians in microseconds per public operation.");
        writer.WriteLine();
        writer.WriteLine("| Case | Operation | Input | PythonRe CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | PythonRe alloc | .NET + decode alloc |");
        writer.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
        foreach (var (caseId, measurement) in rows)
        {
            writer.WriteLine(
                $"| `{caseId}` | `{measurement.Operation}` | {measurement.InputUtf8Bytes:N0} B | " +
                $"{FormatPythonReMicroseconds(measurement.PythonRe.MedianMicroseconds)} | " +
                $"{FormatPythonReMicroseconds(measurement.PredecodedRegex.MedianMicroseconds)} | " +
                $"{FormatPythonReMicroseconds(measurement.DecodeThenRegex.MedianMicroseconds)} | " +
                $"{FormatPythonReRatio(measurement.PythonRe.MedianMicroseconds, measurement.DecodeThenRegex.MedianMicroseconds)} | " +
                $"{measurement.PythonRe.MedianAllocatedBytes:N0} B | {measurement.DecodeThenRegex.MedianAllocatedBytes:N0} B |");
        }

        writer.WriteLine();
        writer.WriteLine("## Reproduce and refresh");
        writer.WriteLine();
        writer.WriteLine("Run from the repository root in `Release` through `./bench.ps1`:");
        writer.WriteLine();
        writer.WriteLine("```powershell");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--measure-pythonre-case\",\"literal/search\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--refresh-pythonre-benchmark-case\",\"literal/search\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--refresh-pythonre-benchmarks\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--verify-pythonre-benchmark-markdown\"");
        writer.WriteLine("```");
        writer.WriteLine();
        writer.WriteLine("The benchmark catalog and projection logic live in [`PythonReBenchmarkReporter.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkReporter.cs).");
        return writer.ToString();
    }

    private static string FormatPythonReMicroseconds(double value) => $"{value:N3} us";

    private static string FormatPythonReRatio(double value, double baseline) =>
        baseline > 0 ? $"{value / baseline:F2}x" : "—";

    private static string ComputePythonReSnapshotSha256() =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FindRepositoryFile(SnapshotFileName))));

}
