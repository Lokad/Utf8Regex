using System.Globalization;
using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private const string Pcre2BenchmarkPageRelativePath = "src/Lokad.Utf8Regex.Pcre2/README.md";

    public static int RunEmitPcre2BenchmarkMarkdown()
    {
        Console.Write(BuildPcre2BenchmarkMarkdown(LoadPcre2Summary()));
        return 0;
    }

    public static int RunRewritePcre2BenchmarkMarkdown()
    {
        RewritePcre2BenchmarkMarkdown();
        Console.WriteLine($"Benchmark README: {GetPcre2BenchmarkPagePath()}");
        return 0;
    }

    public static int RunVerifyPcre2BenchmarkMarkdown()
    {
        var path = GetPcre2BenchmarkPagePath();
        var actual = File.ReadAllText(path, Encoding.UTF8);
        var expected = BenchmarkFileWriter.ReplaceGeneratedSection(
            actual, "PCRE2", BuildPcre2BenchmarkMarkdown(LoadPcre2Summary()));
        if (actual.ReplaceLineEndings("\n") == expected.ReplaceLineEndings("\n"))
        {
            Console.WriteLine($"PCRE2 benchmark README is current: {path}");
            return 0;
        }
        Console.Error.WriteLine("PCRE2 benchmark README is stale; run --rewrite-pcre2-benchmark-markdown.");
        return 1;
    }

    private static void RewritePcre2BenchmarkMarkdown()
    {
        var path = GetPcre2BenchmarkPagePath();
        BenchmarkFileWriter.WriteTextAtomically(path, BenchmarkFileWriter.ReplaceGeneratedSection(
            File.ReadAllText(path, Encoding.UTF8), "PCRE2", BuildPcre2BenchmarkMarkdown(LoadPcre2Summary())));
    }

    private static string GetPcre2BenchmarkPagePath() =>
        Path.Combine(Path.GetDirectoryName(FindRepoFile("README.md"))!, Pcre2BenchmarkPageRelativePath);

    private static string BuildPcre2BenchmarkMarkdown(BenchmarkSummary snapshot)
    {
        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var sections = snapshot.Sections.OrderBy(static section => GetPcre2MarkdownSectionOrder(section.Key))
            .ThenBy(static section => section.Key, StringComparer.Ordinal).ToArray();
        var rows = sections.SelectMany(static section => section.Value.Values).ToArray();
        var counts = rows.GroupBy(static row => row.Status)
            .ToDictionary(static group => group.Key, static group => group.Count());
        writer.WriteLine("## Benchmarks");
        writer.WriteLine();
        writer.WriteLine("Public managed PCRE2 operations over UTF-8, compared with the native PCRE2 NFA through the benchmark-only PCRE.NET reusable UTF-8 buffer. Both lanes include equivalent requested work; pattern compilation is excluded. Native substitution is excluded where the wrapper cannot provide equivalent UTF-8 output. No native dependency ships in this library.");
        writer.WriteLine();
        writer.WriteLine("Times are elapsed microseconds per operation from paired measurements when available. Tables rank `Utf8Pcre2 / PCRE2 NFA` from highest to lowest; lower is better. Status retains paired 95% uncertainty, lane stability and result compatibility. A point ratio is not a winner. Excluded/unqualified rows appear last without a qualified ratio; independently measured discovery values remain observations only.");
        writer.WriteLine();
        writer.WriteLine($"Snapshot SHA-256: `{snapshot.Evidence.SnapshotSha256}`.");
        writer.WriteLine();
        writer.WriteLine($"Comparator Status: `{Count(Pcre2NativeComparisonStatus.ManagedFaster)}` managed faster, " +
            $"`{Count(Pcre2NativeComparisonStatus.Equivalent)}` equivalent, `{Count(Pcre2NativeComparisonStatus.NativeFaster)}` native faster, " +
            $"`{Count(Pcre2NativeComparisonStatus.Inconclusive)}` inconclusive, `{Count(Pcre2NativeComparisonStatus.Unqualified)}` unqualified, " +
            $"`{Count(Pcre2NativeComparisonStatus.Excluded)}` excluded.");
        writer.WriteLine();
        foreach (var source in rows.Select(row => snapshot.Sources[row.Source])
            .Where(static item => item.ValueKind == System.Text.Json.JsonValueKind.Object)
            .GroupBy(static item => item.GetProperty("SourceCommit").GetString(), StringComparer.Ordinal)
            .OrderBy(static group => group.Key, StringComparer.Ordinal))
            writer.WriteLine($"Measured source `{source.Key}`: {string.Join(", ", source.Select(static item => item.GetProperty("Runtime").GetString()).Distinct(StringComparer.Ordinal))}. Selective updates retain each row's identity.");
        foreach (var (sectionName, section) in sections)
        {
            writer.WriteLine();
            writer.WriteLine($"### {GetPcre2MarkdownSectionTitle(sectionName)}");
            writer.WriteLine();
            writer.WriteLine("| Case | Utf8Pcre2 elapsed | PCRE2 NFA elapsed | Status | Ratio (lower is better) |");
            writer.WriteLine("| --- | ---: | ---: | --- | ---: |");
            foreach (var (caseId, row) in section
                .OrderByDescending(static row => row.Value.DisplayRatio ?? double.NegativeInfinity)
                .ThenBy(static row => row.Key, StringComparer.Ordinal))
            {
                var managed = row.ManagedUs;
                var reference = row.ReferenceUs;
                var ratio = row.DisplayRatio;
                writer.WriteLine($"| `{caseId}` | {managed:N3} us | " +
                    $"{(reference is > 0 ? $"{reference:N3} us" : "—")} | {row.DisplayStatus} | " +
                    $"{(ratio is > 0 ? $"{ratio:F2}x" : "—")} |");
            }
        }
        writer.WriteLine();
        writer.WriteLine("<details>");
        writer.WriteLine("<summary>Qualification, allocations and scaling evidence</summary>");
        writer.WriteLine();
        writer.WriteLine("The [summary and raw-evidence reference](../../bench/Snapshots/Pcre2.json) retains per-row confidence intervals, exclusion reasons, input/source/binary identities, allocation probes, native build/plan fingerprints and managed/comparator workspace contracts. Managed-thread allocation probes do not measure retained native memory. Reusable match buffers are neither thread-safe nor reentrant; finite/token/deadline costs remain separate from these ordinary rows.");
        writer.WriteLine();
        if (snapshot.Comparator is { } dependency)
            writer.WriteLine($"Benchmark comparator: [PCRE.NET {dependency.PackageVersion}](https://www.nuget.org/packages/PCRE.NET/{dependency.PackageVersion}), native PCRE2 `{dependency.Engine}`, license `{dependency.License}`. This private benchmark dependency is absent from shipped projects.");
        writer.WriteLine();
        foreach (var (sectionName, section) in sections)
            foreach (var (caseId, row) in section.Where(static row => row.Value.DisplayRatio is null)
                .OrderBy(static row => row.Key, StringComparer.Ordinal))
                writer.WriteLine($"- `{sectionName}/{caseId}`: {row.Reason ?? "No paired qualification evidence."}");
        writer.WriteLine();
        writer.WriteLine("### Scaling evidence");
        writer.WriteLine();
        writer.WriteLine("Contextual bounded families retain construction, first-call, warm and allocation observations in the snapshot; their point timings do not set paired Status.");
        writer.WriteLine();
        writer.WriteLine("| Family | Operation | Points |");
        writer.WriteLine("| --- | --- | ---: |");
        foreach (var (id, row) in snapshot.Scaling.OrderBy(static row => row.Key, StringComparer.Ordinal))
            writer.WriteLine($"| `{id}` | {row.Operation} | {row.Points} |");
        writer.WriteLine();
        writer.WriteLine("Regenerate with `--rewrite-pcre2-benchmark-markdown`; validate with `--verify-pcre2-benchmark-markdown`. Use `--verify-pcre2-qualification-consistency` and `--emit-pcre2-priority-report` for the full evidence through the repository benchmark script.");
        writer.WriteLine();
        writer.WriteLine("</details>");
        return writer.ToString();

        int Count(Pcre2NativeComparisonStatus status) => counts.GetValueOrDefault(status.ToString());
    }

    private static int GetPcre2MarkdownSectionOrder(string section) => section switch
    {
        "pcre2-managed-compatible-ismatch" => 0,
        "pcre2-managed-compatible-count" => 1,
        "pcre2-managed-compatible-enumerate" => 2,
        "pcre2-managed-compatible-matchmany" => 3,
        "pcre2-managed-compatible-replace" => 4,
        "pcre2-special-ismatch" => 5,
        "pcre2-special-count" => 6,
        "pcre2-special-enumerate" => 7,
        "pcre2-special-matchmany" => 8,
        "pcre2-special-replace" => 9,
        _ => int.MaxValue,
    };

    private static string GetPcre2MarkdownSectionTitle(string section) => section switch
    {
        "pcre2-managed-compatible-ismatch" => "Compatible IsMatch",
        "pcre2-managed-compatible-count" => "Compatible Count",
        "pcre2-managed-compatible-enumerate" => "Compatible EnumerateMatches",
        "pcre2-managed-compatible-matchmany" => "Compatible MatchMany",
        "pcre2-managed-compatible-replace" => "Compatible Replace",
        "pcre2-special-ismatch" => "PCRE2-only IsMatch",
        "pcre2-special-count" => "PCRE2-only Count",
        "pcre2-special-enumerate" => "PCRE2-only EnumerateMatches",
        "pcre2-special-matchmany" => "PCRE2-only MatchMany",
        "pcre2-special-replace" => "PCRE2-only Replace",
        _ => section,
    };

    private static BenchmarkSummary LoadPcre2Summary() =>
        BenchmarkSummary.Read(Path.GetDirectoryName(FindRepoFile("README.md"))!, "Pcre2");
}