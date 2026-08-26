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
        return snapshot is { SchemaVersion: 2 or 3 or 4 or 5 or 6 or 7 or 8 or 9 or 10 }
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
        var orderedIds = snapshot.CatalogCaseIds.Length == 0
            ? snapshot.Cases.Keys.ToArray()
            : snapshot.CatalogCaseIds;
        var rows = orderedIds
            .Where(snapshot.Cases.ContainsKey)
            .Select(caseId => new KeyValuePair<string, PythonReCaseMeasurement>(
                caseId,
                snapshot.Cases[caseId]))
            .Concat(snapshot.Cases.Where(pair => !orderedIds.Contains(pair.Key, StringComparer.Ordinal)))
            .ToArray();
        var catalogById = PythonReBenchmarkCatalog.Cases.ToDictionary(
            static benchmarkCase => benchmarkCase.Id,
            StringComparer.Ordinal);
        var hasCompleteCpythonBaseline = rows.All(static row => row.Value.Cpython is not null);
        var statusCounts = rows
            .GroupBy(
                static row => row.Value.Qualification?.Status ?? "Unqualified",
                StringComparer.Ordinal)
            .ToDictionary(static group => group.Key, static group => group.Count(), StringComparer.Ordinal);
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
        writer.WriteLine("This page is the self-contained performance regression snapshot for the optional Python `re` adapter. It is not a second regex-engine scoreboard: compatible patterns intentionally reuse the managed `Utf8Regex` core, while this catalog measures the Python-facing operation and its required result shaping. The source of truth is [`PythonRe.Benchmarks.json`](../../PythonRe.Benchmarks.json); `--refresh-pythonre-benchmarks` and `--refresh-pythonre-benchmark-case` update the JSON and regenerate this page.");
        writer.WriteLine();
        writer.WriteLine("The comparison is deliberately limited to cases where the requested work has equivalent managed semantics:");
        writer.WriteLine();
        writer.WriteLine("- `PythonRe`: `Utf8PythonRegex` over UTF-8 input.");
        if (hasCompleteCpythonBaseline)
        {
            writer.WriteLine("- `CPython predecoded`: the official CPython `re.Pattern` / `_sre` implementation over an already-decoded `str`; this is the strong Status baseline.");
            writer.WriteLine("- `CPython + decode`: strict UTF-8 decoding on every operation followed by the precompiled CPython `re.Pattern`; this is contextual end-to-end evidence and cannot set Status.");
        }
        writer.WriteLine("- `.NET predecoded`: `System.Text.RegularExpressions.Regex` over an already-decoded `string`.");
        writer.WriteLine("- `.NET + decode`: strict UTF-8 decoding on every operation followed by `.NET Regex`; this is retained as managed-core context.");
        writer.WriteLine();
        writer.WriteLine(hasCompleteCpythonBaseline
            ? "Enumeration, split, and replacement rows include the result materialization needed by the public operation. CPython is measured inside its own long-lived process because `_sre` is a CPython core module, not a standalone engine API; interpreter startup and pattern compilation are excluded. Status requires alternating paired elapsed-time samples against predecoded CPython, equal requested work, stable lanes, bounded harness-floor sensitivity, and exact source/runtime provenance. Historical independent medians remain visible for discovery but are Unqualified. Use the PCRE2 page for the separate native PCRE2 engine comparison."
            : "Enumeration, split, and replacement rows include the result materialization needed by the public operation. This legacy schema-2 snapshot predates the direct CPython baseline; the next complete refresh migrates it to schema 3 with official CPython `re` measurements. Predecoded columns are matcher/runtime lower bounds, not end-to-end parity requirements.");
        writer.WriteLine();
        if (hasCompleteCpythonBaseline)
        {
            writer.WriteLine("Qualified `Search`, `SearchFromOffset`, `Match`, and `FullMatch` rows use the `ConsumedGroupZeroRanges` contract: every timed operation consumes success plus group-zero byte and UTF-16 boundaries. Result hashing and value verification remain outside timing. Other result-producing rows retain their required eager public materialization.");
            writer.WriteLine();
            writer.WriteLine("Convenience overloads are intentionally coalesced when they do not change timed work. Matched-string helpers project the same group-zero value already represented by direct matching rows; `MatchDetailed` and `FullMatchDetailed` use the same detailed capture projection represented by `SearchDetailed`, while their anchored discovery modes are covered separately by `Match` and `FullMatch`. Offset and limit overloads receive distinct rows when they change enumeration or shaping work.");
            writer.WriteLine();
            writer.WriteLine("Eligible ASCII one-shot rows also measure a CPython bytes `Pattern` over the identical bytes. `Rbyte` is representation-neutral engine evidence and never sets public Status; rows without equivalent byte semantics carry an explicit exclusion reason.");
            writer.WriteLine();

        }

        writer.WriteLine("## Snapshot summary");
        writer.WriteLine();
        writer.WriteLine($"- Generated: `{snapshot.GeneratedAtUtc.ToUniversalTime():O}`");
        writer.WriteLine($"- Snapshot SHA-256: `{ComputePythonReSnapshotSha256()}`");
        writer.WriteLine($"- Schema: `{snapshot.SchemaVersion}`");
        if (!string.IsNullOrWhiteSpace(snapshot.CatalogSha256))
        {
            writer.WriteLine($"- Catalog SHA-256: `{snapshot.CatalogSha256}`");
        }
        writer.WriteLine($"- Cases: `{rows.Length}`");
        writer.WriteLine($"- Lifecycle families: `{snapshot.Lifecycle.Count}`");
        writer.WriteLine($"- Scaling families: `{snapshot.ScalingFamilies.Count}`");
        writer.WriteLine(
            $"- Public Status: `{GetStatusCount("ManagedFaster")}` managed faster, " +
            $"`{GetStatusCount("Equivalent")}` equivalent, " +
            $"`{GetStatusCount("CpythonFaster")}` CPython faster, " +
            $"`{GetStatusCount("Inconclusive")}` inconclusive, " +
            $"`{GetStatusCount("Unqualified")}` unqualified");
        writer.WriteLine($"- Historical point measurement environments represented: `{environments.Length}`");
        writer.WriteLine($"- Corpus: [`{snapshot.Corpus.SourceFile}`](../../{snapshot.Corpus.SourceFile}) (`{snapshot.Corpus.VectorCount}` vectors, SHA-256 `{snapshot.Corpus.Sha256}`)");
        writer.WriteLine($"- Corpus provenance limitation: {snapshot.Corpus.Limitation}");
        if (hasCompleteCpythonBaseline)
        {
            var cpythonEnvironments = rows
                .Select(static row => (row.Value.Cpython ??
                    throw new InvalidOperationException("A complete CPython snapshot contains a missing row baseline.")).Environment)
                .DistinctBy(static environment => new
                {
                    environment.Implementation,
                    environment.Version,
                    environment.Executable,
                    environment.Platform,
                })
                .ToArray();
            writer.WriteLine($"- Historical CPython point environments represented: `{cpythonEnvironments.Length}`");
            foreach (var environment in cpythonEnvironments)
            {
                writer.WriteLine($"- Historical CPython point baseline: `{environment.Implementation} {environment.Version}` at `{environment.Executable}` on {environment.Platform}");
            }
        }
        writer.WriteLine();

        if (snapshot.SchemaVersion >= 8)
        {
            var coverageRows = rows.Where(static row => row.Value.Coverage is not null).ToArray();
            writer.WriteLine("## Coverage summary");
            writer.WriteLine();
            writer.WriteLine(
                $"This catalog currently covers `{coverageRows.Length}` operation rows over " +
                $"`{coverageRows.Select(static row => row.Value.Pattern).Distinct(StringComparer.Ordinal).Count()}` " +
                "distinct patterns. Zero-count sections below are deliberate, visible backlog rather than implicit coverage.");
            writer.WriteLine();
            writer.WriteLine("| Axis | Covered values |");
            writer.WriteLine("|---|---|");
            WriteCoverageAxis("Operation families", coverageRows.Select(static row => row.Value.Operation));
            WriteCoverageAxis("Flags", coverageRows.Select(static row => row.Value.Options));
            WriteCoverageAxis("Feature families", coverageRows.Select(static row => row.Value.Coverage!.FeatureFamily));
            WriteCoverageAxis("Managed route classes", coverageRows.Select(static row => row.Value.Coverage!.IntendedManagedRouteClass));
            WriteCoverageAxis("Result cardinalities", coverageRows.Select(static row => row.Value.Coverage!.ExpectedResultCardinality));
            WriteCoverageAxis(
                "Input-width classes",
                coverageRows.Select(row => GetPythonReInputWidthClass(catalogById[row.Key].Input)));
            WriteCoverageAxis("Corpus provenance", coverageRows.Select(static row => row.Value.Coverage!.CorpusProvenance));
            WriteCoverageAxis("Claim classes", coverageRows.Select(static row => row.Value.Coverage!.ClaimClass));
            writer.WriteLine();
            writer.WriteLine("| Result section | Rows |");
            writer.WriteLine("|---|---:|");
            foreach (var section in s_pythonReCoverageSections)
            {
                var count = section switch
                {
                    "Construction and first call" => snapshot.Lifecycle.Count,
                    "Scaling evidence" => snapshot.ScalingFamilies.Count,
                    _ => coverageRows.Count(row => row.Value.Coverage!.Section.Equals(
                        section,
                        StringComparison.Ordinal)),
                };
                writer.WriteLine($"| {section} | {count} |");
            }

            writer.WriteLine();
            var sourcedRows = coverageRows
                .Where(static row => !row.Value.Coverage!.CorpusProvenance.Equals(
                    "Synthetic catalog generator",
                    StringComparison.Ordinal))
                .ToArray();
            writer.WriteLine("### Reused subjects and corpus identities");
            writer.WriteLine();
            writer.WriteLine("Input hashes cover the exact decoded subject re-encoded as strict UTF-8 and timed by all lanes.");
            writer.WriteLine();
            writer.WriteLine("| Case | Source definition or corpus | UTF-8 bytes | SHA-256 |");
            writer.WriteLine("|---|---|---:|---|");
            foreach (var (caseId, measurement) in sourcedRows)
            {
                writer.WriteLine(
                    $"| `{caseId}` | `{measurement.Coverage!.CorpusProvenance}` | " +
                    $"{measurement.InputUtf8Bytes:N0} | `{measurement.InputSha256}` |");
            }

            writer.WriteLine();
        }

        if (environments.Length == 1)
        {
            var environment = environments[0];
            writer.WriteLine(
                $"Historical independent point columns were measured from source `{environment.SourceCommit}` " +
                $"on {environment.Runtime}, {environment.OperatingSystem}, {environment.Processor}. " +
                "Qualified paired rows record their own exact source, runtime, and interpreter provenance in the JSON evidence.");
        }
        else
        {
            writer.WriteLine("Historical point rows span more than one measurement environment. Consult the JSON row metadata before interpreting small differences as regressions or wins; qualified paired rows carry separate exact provenance.");
        }

        writer.WriteLine();
        writer.WriteLine("## Results");
        writer.WriteLine();
        if (hasCompleteCpythonBaseline)
        {
            writer.WriteLine("`Rstrong` is `PythonRe / CPython predecoded`; lower is better. Only a qualified paired 95% interval wholly below `0.98`, wholly within `0.98-1.02`, or wholly above `1.02` can establish Managed faster, Equivalent, or CPython faster. The old scalar medians shown for Unqualified rows are discovery evidence only. All times are elapsed microseconds per public operation, not CPU time.");
            writer.WriteLine();
            var resultSections = snapshot.SchemaVersion >= 8
                ? s_pythonReCoverageSections
                : ["Results"];
            foreach (var section in resultSections)
            {
                if (section.Equals("Construction and first call", StringComparison.Ordinal))
                {
                    WriteLifecycleSection();
                    continue;
                }

                if (section.Equals("Scaling evidence", StringComparison.Ordinal))
                {
                    WriteScalingSection();
                    continue;
                }

                var sectionRows = snapshot.SchemaVersion >= 8
                    ? rows.Where(row => row.Value.Coverage?.Section.Equals(
                        section,
                        StringComparison.Ordinal) == true).ToArray()
                    : rows;
                writer.WriteLine($"### {section}");
                writer.WriteLine();
                if (sectionRows.Length == 0)
                {
                    writer.WriteLine("No benchmark rows are cataloged in this section yet.");
                    writer.WriteLine();
                    continue;
                }

                writer.WriteLine("| Case | Operation | Contract | Status | PythonRe elapsed | CPython predecoded elapsed | Rstrong | CPython + decode elapsed | .NET + decode elapsed | PythonRe alloc |");
                writer.WriteLine("|---|---|---|---|---:|---:|---:|---:|---:|---:|");
                foreach (var (caseId, measurement) in sectionRows)
                {
                    var cpython = measurement.Cpython ??
                        throw new InvalidOperationException("A complete CPython snapshot contains a missing row baseline.");
                    var qualification = measurement.Qualification;
                    var paired = qualification?.PairedEvidence;
                    var managedMicroseconds = paired?.ManagedMedianMicroseconds ??
                        measurement.PythonRe.MedianMicroseconds;
                    var cpythonMicroseconds = paired?.CpythonMedianMicroseconds ??
                        cpython.PredecodedRe.MedianMicroseconds;
                    var strongRatio = paired?.StrongRatioMedian ?? managedMicroseconds / cpythonMicroseconds;
                    writer.WriteLine(
                        $"| `{caseId}` | `{measurement.Operation}` | {paired?.ResultContract ?? "Historical"} | " +
                        $"{FormatPythonReStatus(qualification)} | " +
                        $"{FormatPythonReMicroseconds(managedMicroseconds)} | " +
                        $"{FormatPythonReMicroseconds(cpythonMicroseconds)} | " +
                        $"{strongRatio:F2}x | " +
                        $"{FormatPythonReMicroseconds(cpython.DecodeThenRe.MedianMicroseconds)} | " +
                        $"{FormatPythonReMicroseconds(measurement.DecodeThenRegex.MedianMicroseconds)} | " +
                        $"{(paired?.ManagedMedianAllocatedBytes ?? measurement.PythonRe.MedianAllocatedBytes):N0} B |");
                }

                writer.WriteLine();
            }
        }
        else
        {
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
        }

        writer.WriteLine();
        if (snapshot.SchemaVersion >= 5)
        {
            writer.WriteLine("## Operation ownership and managed route");
            writer.WriteLine();
            writer.WriteLine("These fields prevent a composed host-language operation or a managed decode fallback from being mislabeled as a regex-engine result.");
            writer.WriteLine();
            writer.WriteLine("| Case | CPython operation owner | Managed route | Byte control / engine evidence |");
            writer.WriteLine("|---|---|---|---|");
            foreach (var (caseId, measurement) in rows)
            {
                var byteControl = measurement.Qualification?.PairedEvidence?.ByteControl;
                var byteSummary = byteControl is null
                    ? measurement.ByteControlReason
                    : $"Rbyte {byteControl.RatioMedian:F2}x " +
                      $"[{byteControl.RatioLower95:F2}, {byteControl.RatioUpper95:F2}]; " +
                      $"{byteControl.EngineConclusion}";
                writer.WriteLine(
                    $"| `{caseId}` | `{measurement.ComparatorOwner}` | `{measurement.ManagedRoute}` | " +
                    $"{byteSummary} |");
            }

            writer.WriteLine();
        }

        writer.WriteLine("## Reproduce and refresh");
        writer.WriteLine();
        writer.WriteLine("Run from the repository root in `Release` through `./bench.ps1`:");
        writer.WriteLine();
        writer.WriteLine("```powershell");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--measure-pythonre-paired-case\",\"literal/search\",\"9\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--measure-pythonre-paired-case-reversed\",\"literal/search\",\"9\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--qualify-pythonre-case\",\"literal/search\",\"9\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--resume-pythonre-qualifications\",\"9\",\"17\",\"4\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--emit-pythonre-priority-report\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--emit-pythonre-coverage-report\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--verify-pythonre-coverage-contract\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--measure-pythonre-case\",\"literal/search\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--refresh-pythonre-benchmark-case\",\"literal/search\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--refresh-pythonre-lifecycle\",\"32\",\"5\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--refresh-pythonre-scaling\",\"5\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--verify-pythonre-scaling\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--refresh-pythonre-benchmarks\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--verify-pythonre-benchmark-markdown\"");
        writer.WriteLine("```");
        writer.WriteLine();
        writer.WriteLine("The case definitions live in [`PythonReBenchmarkCatalog.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkCatalog.cs); timed projection logic lives in [`PythonReBenchmarkReporter.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/PythonReBenchmarkReporter.cs).");
        return writer.ToString();

        int GetStatusCount(string status) => statusCounts.GetValueOrDefault(status);

        void WriteCoverageAxis(string axis, IEnumerable<string> values)
        {
            var formatted = string.Join(
                ", ",
                values.Distinct(StringComparer.Ordinal)
                    .OrderBy(static value => value, StringComparer.Ordinal)
                    .Select(static value => $"`{value}`"));
            writer.WriteLine($"| {axis} | {formatted} |");
        }

        void WriteLifecycleSection()
        {
            writer.WriteLine("### Construction and first call");
            writer.WriteLine();
            writer.WriteLine("These are contextual uncached-construction throughput measurements. They never alter warm public Status. CPython construction calls the standard-library compiler directly so the `re.compile` cache cannot turn construction into a cache lookup; first-search rows construct a fresh pattern and execute one successful search in the same timed operation.");
            writer.WriteLine();
            if (snapshot.Lifecycle.Count == 0)
            {
                writer.WriteLine("No lifecycle families have been published yet.");
                writer.WriteLine();
                return;
            }

            writer.WriteLine("| Family | Pattern | Input | Parse + translate | Core backend create | Adapter construct | Adapter construct + first search | CPython compile | CPython compile + first search |");
            writer.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---:|");
            foreach (var (id, lifecycle) in snapshot.Lifecycle)
            {
                writer.WriteLine(
                    $"| `{id}` | `{lifecycle.Pattern}` | {lifecycle.InputUtf8Bytes:N0} B | " +
                    $"{FormatLifecycle(lifecycle.ParseTranslate)} | " +
                    $"{FormatLifecycle(lifecycle.BackendCreation)} | " +
                    $"{FormatLifecycle(lifecycle.AdapterConstruction)} | " +
                    $"{FormatLifecycle(lifecycle.ConstructFirstSearch)} | " +
                    $"{FormatLifecycle(lifecycle.CpythonCompile)} | " +
                    $"{FormatLifecycle(lifecycle.CpythonCompileFirstSearch)} |");
            }

            writer.WriteLine();
        }

        void WriteScalingSection()
        {
            writer.WriteLine("### Scaling evidence");
            writer.WriteLine();
            writer.WriteLine("These bounded, warmed families vary one named dimension while preserving equivalent managed and CPython result contracts. They are mechanism and complexity guards, not extra warm-Status rows: a point ratio never declares an implementation winner, and a passing fit gate only says that the local trend is stable enough to interpret. A rejected family remains visible but cannot support a scaling claim.");
            writer.WriteLine();
            if (snapshot.ScalingFamilies.Count == 0)
            {
                writer.WriteLine("No scaling families have been published yet.");
                writer.WriteLine();
                return;
            }

            writer.WriteLine("| Family | Dimension | Operation | Points | Managed route | Fit gate | Maximum residual M / C | Maximum spread M / C |");
            writer.WriteLine("|---|---|---|---:|---|---|---:|---:|");
            foreach (var (id, family) in snapshot.ScalingFamilies)
            {
                writer.WriteLine(
                    $"| `{id}` | {family.Dimension} | `{family.Operation}` | {family.Points.Count} | " +
                    $"`{EscapeMarkdownTable(family.ManagedRoute)}` | **{family.FitGate}** | " +
                    $"{family.ManagedMaximumRelativeResidual:P1} / {family.CpythonMaximumRelativeResidual:P1} | " +
                    $"{family.ManagedMaximumSpread:F3} / {family.CpythonMaximumSpread:F3} |");
            }

            writer.WriteLine();
            foreach (var (id, family) in snapshot.ScalingFamilies)
            {
                writer.WriteLine($"#### `{id}`");
                writer.WriteLine();
                writer.WriteLine(
                    $"Dimension: {family.Dimension}. Result contract: `{family.ResultContract}`. " +
                    $"Samples: `{family.Samples}`. Fit gate: **{family.FitGate}** — {family.FitGateReason} " +
                    $"Robust slopes are {family.ManagedSlopePerScaleUnit:N6} us/unit managed and " +
                    $"{family.CpythonSlopePerScaleUnit:N6} us/unit CPython.");
                writer.WriteLine();
                writer.WriteLine("| Point | Scale | Input | Work | Output | PythonRe elapsed | CPython elapsed | Rstrong [paired 95%] | Managed allocation |");
                writer.WriteLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|");
                foreach (var point in family.Points)
                {
                    writer.WriteLine(
                        $"| {point.Label} | {point.Scale:N0} | {point.InputUtf8Bytes:N0} B | " +
                        $"{point.WorkUnits:N0} | {point.OutputUtf8Bytes:N0} B | " +
                        $"{point.ManagedMedianMicroseconds:N3} us | {point.CpythonMedianMicroseconds:N3} us | " +
                        $"{point.RatioMedian:F3}x [{point.RatioLower95:F3}, {point.RatioUpper95:F3}] | " +
                        $"{point.ManagedAllocatedBytes:N0} B |");
                }

                writer.WriteLine();
            }
            writer.WriteLine();
        }

        static string EscapeMarkdownTable(string value) => value.Replace("|", "&#124;", StringComparison.Ordinal);

        static string FormatLifecycle(PythonReLifecycleTiming timing) =>
            timing.ManagedAllocatedBytes is long allocated
                ? $"{timing.MedianMicroseconds:N3} us / {allocated:N0} B"
                : $"{timing.MedianMicroseconds:N3} us";
    }

    private static string FormatPythonReStatus(PythonReQualificationMeasurement? qualification)
    {
        var status = qualification?.Status ?? "Unqualified";
        return status switch
        {
            "ManagedFaster" => "Managed faster",
            "CpythonFaster" => "CPython faster",
            _ => status,
        };
    }

    private static string FormatPythonReMicroseconds(double value) => $"{value:N3} us";

    private static string FormatPythonReRatio(double value, double baseline) =>
        baseline > 0 ? $"{value / baseline:F2}x" : "—";

    private static string ComputePythonReSnapshotSha256() =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(FindRepositoryFile(SnapshotFileName))));

}
