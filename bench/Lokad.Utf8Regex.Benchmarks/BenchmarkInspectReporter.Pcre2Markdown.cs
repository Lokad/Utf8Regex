using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private const string Pcre2BenchmarkPageRelativePath = "src/Lokad.Utf8Regex.Pcre2/BENCHMARKS.md";

    public static int RunEmitPcre2BenchmarkMarkdown()
    {
        Console.Write(BuildPcre2BenchmarkMarkdown(LoadPcre2BenchmarkSnapshot()));
        return 0;
    }

    public static int RunRewritePcre2BenchmarkMarkdown()
    {
        RewritePcre2BenchmarkMarkdown(LoadPcre2BenchmarkSnapshot());
        Console.WriteLine($"Benchmark page      : {GetPcre2BenchmarkPagePath()}");
        return 0;
    }

    public static int RunVerifyPcre2BenchmarkMarkdown()
    {
        var expected = BuildPcre2BenchmarkMarkdown(LoadPcre2BenchmarkSnapshot());
        var path = GetPcre2BenchmarkPagePath();
        var actual = File.ReadAllText(path, Encoding.UTF8);
        if (actual.ReplaceLineEndings("\n") == expected.ReplaceLineEndings("\n"))
        {
            Console.WriteLine($"PCRE2 benchmark page is current: {path}");
            return 0;
        }

        Console.Error.WriteLine(
            $"PCRE2 benchmark page is stale: {path}{Environment.NewLine}" +
            "Run --rewrite-pcre2-benchmark-markdown or refresh the PCRE2 snapshot.");
        return 1;
    }

    private static void RewritePcre2BenchmarkMarkdown(Pcre2BenchmarkSnapshot snapshot)
    {
        BenchmarkFileWriter.WriteTextAtomically(GetPcre2BenchmarkPagePath(), BuildPcre2BenchmarkMarkdown(snapshot));
    }

    private static string GetPcre2BenchmarkPagePath() =>
        Path.Combine(Path.GetDirectoryName(FindRepoFile("README.md"))!, Pcre2BenchmarkPageRelativePath);

    private static string BuildPcre2BenchmarkMarkdown(Pcre2BenchmarkSnapshot snapshot)
    {
        static int GetStatusCount(
            IReadOnlyDictionary<Pcre2NativeComparisonStatus, int> counts,
            Pcre2NativeComparisonStatus status)
            => counts.GetValueOrDefault(status);

        static string FormatNativeStatus(Pcre2NativeComparisonStatus status) => status switch
        {
            Pcre2NativeComparisonStatus.Unqualified => "Unqualified",
            Pcre2NativeComparisonStatus.Excluded => "Excluded",
            Pcre2NativeComparisonStatus.Inconclusive => "Inconclusive",
            Pcre2NativeComparisonStatus.Equivalent => "Equivalent",
            Pcre2NativeComparisonStatus.ManagedFaster => "Managed faster",
            Pcre2NativeComparisonStatus.NativeFaster => "Native faster",
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };

        static string FormatNativeRatio(Pcre2CaseMeasurementJson row) => row.PcreNetNativePair is { } pair
            ? $"{pair.RatioMedian:F2}x"
            : FormatPcre2Ratio(row.Utf8Pcre2, row.PcreNetNative);

        static string FormatNativeInterval(Pcre2CaseMeasurementJson row) => row.PcreNetNativePair is { } pair
            ? $"{pair.RatioLower95:F2}–{pair.RatioUpper95:F2}x"
            : "—";

        static string FormatNativeExcess(Pcre2CaseMeasurementJson row)
        {
            if (row.PcreNetNativePair is { } pair)
            {
                return $"{pair.ExcessMedianMicroseconds:+0.000;-0.000;0.000} us";
            }

            return row.PcreNetNative is > 0
                ? $"{row.Utf8Pcre2 - row.PcreNetNative.Value:+0.000;-0.000;0.000} us"
                : "—";
        }

        static string FormatNativePair(Pcre2CaseMeasurementJson row)
        {
            if (row.PcreNetNativePair is not { } pair)
            {
                return "—";
            }

            var managedMilliseconds = Median(pair.ManagedSampleMilliseconds);
            var comparatorMilliseconds = Median(pair.ComparatorSampleMilliseconds);
            return $"{pair.SampleCount} pairs; {managedMilliseconds:F0}/{comparatorMilliseconds:F0} ms; " +
                   $"{pair.ManagedBatchCount:N0}/{pair.ComparatorBatchCount:N0} ops/lane; " +
                   $"IQR {pair.ManagedInterquartileSpreadRatio:F3}/{pair.ComparatorInterquartileSpreadRatio:F3}";
        }

        static string FormatManagedRoute(Pcre2CaseMeasurementJson row) =>
            row.PcreNetNativePair?.ManagedRoute ?? "—";

        static string FormatManagedAllocation(Pcre2CaseMeasurementJson row) =>
            row.PcreNetNativePair is { ProtocolVersion: >= 6 } pair
                ? FormatPcre2AllocatedBytes(pair.ManagedAllocatedBytesPerOperation)
                : FormatPcre2AllocatedBytes(row.WarmAllocatedBytes);

        static string FormatComparatorManagedAllocation(Pcre2CaseMeasurementJson row) =>
            row.PcreNetNativePair is { ProtocolVersion: >= 6 } pair
                ? FormatPcre2AllocatedBytes(pair.ComparatorManagedAllocatedBytesPerOperation)
                : "—";

        var writer = new StringWriter(CultureInfo.InvariantCulture);
        var sectionRows = snapshot.Sections
            .OrderBy(static section => GetPcre2MarkdownSectionOrder(section.Key))
            .ThenBy(static section => section.Key, StringComparer.Ordinal)
            .ToArray();
        var allRows = sectionRows.SelectMany(static section => section.Value.Cases.Values).ToArray();
        var comparableRows = sectionRows
            .Where(static section => section.Key.StartsWith("pcre2-managed-compatible-", StringComparison.Ordinal))
            .SelectMany(static section => section.Value.Cases.Values)
            .Where(static row => row.DecodeThenRegex is > 0)
            .ToArray();
        var parityCount = comparableRows.Count(static row => row.Utf8Pcre2 <= row.DecodeThenRegex);
        var nativeRows = allRows.Where(static row => row.PcreNetNative is > 0).ToArray();
        var statusCounts = allRows
            .GroupBy(static row => row.PcreNetNativeStatus)
            .ToDictionary(static group => group.Key, static group => group.Count());
        var pairedRows = allRows.Count(static row => row.PcreNetNativePair is not null);
        var managedWorkspaceContract = allRows
            .Select(static row => row.PcreNetNativePair?.ManagedWorkspaceContract)
            .FirstOrDefault(static contract => contract is not null);
        var qualificationProcessorSets = allRows
            .Select(static row => row.PcreNetNativePair)
            .Where(static pair => pair is not null)
            .Select(static pair => $"{pair!.ProcessorSetPolicy} {pair.ProcessorAffinityMask}" +
                                   (pair.ProcessorEfficiencyClass is { } efficiencyClass
                                       ? $" (class {efficiencyClass})"
                                       : string.Empty))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var unavailableNativeRows = sectionRows
            .SelectMany(static section => section.Value.Cases.Select(row => new
            {
                Section = section.Key,
                CaseId = row.Key,
                Reason = row.Value.PcreNetNativeUnavailableReason,
            }))
            .Where(static row => !string.IsNullOrWhiteSpace(row.Reason))
            .ToArray();
        var latestManagedMeasurement = allRows
            .Where(static row => row.MeasuredAtUtc.HasValue)
            .Select(static row => row.MeasuredAtUtc!.Value)
            .DefaultIfEmpty()
            .Max();
        var latestNativeMeasurement = allRows
            .Where(static row => row.PcreNetNativeMeasuredAtUtc.HasValue)
            .Select(static row => row.PcreNetNativeMeasuredAtUtc!.Value)
            .DefaultIfEmpty()
            .Max();
        var managedEnvironments = allRows
            .Select(static row => row.Environment)
            .Where(static environment => environment is not null)
            .DistinctBy(static environment => new
            {
                environment!.SourceCommit,
                environment.Runtime,
                environment.OperatingSystem,
                environment.Processor,
                environment.TieredPgo,
            })
            .ToArray();
        var nativeEnvironments = allRows
            .Select(static row => row.PcreNetNativeEnvironment)
            .Where(static environment => environment is not null)
            .DistinctBy(static environment => new
            {
                environment!.SourceCommit,
                environment.Runtime,
                environment.OperatingSystem,
                environment.Processor,
                environment.TieredPgo,
                environment.TrackedDirty,
                environment.HasUntrackedFiles,
            })
            .ToArray();

        writer.WriteLine("<!-- This file is generated from ../../PCRE2.Benchmarks.json. Do not edit benchmark rows by hand. -->");
        writer.WriteLine();
        writer.WriteLine("# Lokad.Utf8Regex.Pcre2 benchmarks");
        writer.WriteLine();
        writer.WriteLine("This page is the self-contained performance snapshot for the managed PCRE2 10.47 profile. The source of truth is [`PCRE2.Benchmarks.json`](../../PCRE2.Benchmarks.json); selective and full PCRE2 refresh commands update the JSON and regenerate this page.");
        writer.WriteLine();
        writer.WriteLine("Compatible rows compare equivalent work against `Utf8Regex` and .NET 10 `Regex`. `.NET + decode` performs strict UTF-8 decoding for every operation and is the primary end-to-end managed baseline; `.NET predecoded` is a lower bound. PCRE2-only rows cannot use .NET as a semantic comparator and therefore report managed PCRE2 measurements without fabricating a cross-dialect baseline.");
        writer.WriteLine();
        writer.WriteLine("## Snapshot summary");
        writer.WriteLine();
        writer.WriteLine($"- Schema: `{snapshot.SchemaVersion}`");
        writer.WriteLine($"- Snapshot SHA-256: `{ComputePcre2SnapshotSha256()}`");
        writer.WriteLine($"- Latest managed row measurement: `{FormatPcre2Timestamp(latestManagedMeasurement)}`");
        writer.WriteLine($"- Latest PCRE.NET / PCRE2 NFA measurement: `{FormatPcre2Timestamp(latestNativeMeasurement)}`");
        writer.WriteLine($"- Operation rows: `{allRows.Length}` across `{sectionRows.Length}` sections");
        writer.WriteLine($"- Comparable rows at or below the decode-then-.NET median: `{parityCount}/{comparableRows.Length}`");
        writer.WriteLine($"- Rows with a PCRE.NET / PCRE2 NFA comparator: `{nativeRows.Length}/{allRows.Length}`");
        writer.WriteLine(
            $"- Comparator Status: `{GetStatusCount(statusCounts, Pcre2NativeComparisonStatus.ManagedFaster)}` managed faster, " +
            $"`{GetStatusCount(statusCounts, Pcre2NativeComparisonStatus.Equivalent)}` equivalent, " +
            $"`{GetStatusCount(statusCounts, Pcre2NativeComparisonStatus.NativeFaster)}` native faster, " +
            $"`{GetStatusCount(statusCounts, Pcre2NativeComparisonStatus.Inconclusive)}` inconclusive, " +
            $"`{GetStatusCount(statusCounts, Pcre2NativeComparisonStatus.Unqualified)}` unqualified, " +
            $"`{GetStatusCount(statusCounts, Pcre2NativeComparisonStatus.Excluded)}` excluded");
        writer.WriteLine($"- Rows with paired qualification evidence: `{pairedRows}/{nativeRows.Length}`");
        if (qualificationProcessorSets.Length > 0)
        {
            writer.WriteLine($"- Qualification processor sets: {string.Join(", ", qualificationProcessorSets.Select(static value => $"`{value}`"))}");
        }

        writer.WriteLine($"- Scaling families: `{snapshot.ScalingFamilies.Count}`");
        writer.WriteLine($"- Managed/comparator measurement environments represented: `{managedEnvironments.Length}/{nativeEnvironments.Length}`");
        writer.WriteLine();

        if (managedEnvironments.Length == 1)
        {
            var environment = managedEnvironments[0]!;
            writer.WriteLine($"Managed rows were measured from source `{environment.SourceCommit}` on {environment.Runtime}, {environment.OperatingSystem}, {environment.Processor}; Tiered PGO `{environment.TieredPgo}`.");
        }
        else
        {
            writer.WriteLine("Managed rows span more than one measurement environment. Consult the JSON row metadata before interpreting small differences as regressions or wins.");
        }

        if (nativeEnvironments.Length == 1)
        {
            var environment = nativeEnvironments[0]!;
            writer.WriteLine();
            writer.WriteLine($"PCRE.NET / PCRE2 NFA rows were measured from source `{environment.SourceCommit}` on {environment.Runtime}, {environment.OperatingSystem}, {environment.Processor}; Tiered PGO `{environment.TieredPgo}`, tracked dirty `{environment.TrackedDirty}`, untracked files `{environment.HasUntrackedFiles}`.");
        }
        else if (nativeEnvironments.Length > 1)
        {
            writer.WriteLine();
            writer.WriteLine("Comparator rows span more than one measurement environment. Consult each row's `PcreNetNativeEnvironment` metadata before comparing small differences.");
        }

        if (snapshot.PcreNetNativeBaseline is { } dependency)
        {
            writer.WriteLine();
            writer.WriteLine($"NuGet package SHA-512: `{dependency.PackageSha512}`.");
        }

        writer.WriteLine();
        WritePcreNetDependencyReview(writer, snapshot.PcreNetNativeBaseline);
        if (managedWorkspaceContract is not null)
        {
            writer.WriteLine();
            writer.WriteLine(
                $"Managed qualification lifecycle: {managedWorkspaceContract.Lifetime} " +
                $"{managedWorkspaceContract.ConcurrencyContract} {managedWorkspaceContract.RetainedMemoryContract}");
        }
        writer.WriteLine();
        writer.WriteLine("`vs decode` is `Utf8Pcre2 / .NET + decode`; `R` is `Utf8Pcre2 / PCRE.NET-PCRE2 NFA`; lower is better. Rows without a 95% interval and paired-sample description contain independently measured discovery data only and cannot determine a winner. `E` is the paired median managed-minus-comparator excess when paired evidence exists and the difference between discovery medians otherwise. Paired-sample descriptions show managed/comparator median sample durations, frozen operations per lane, and managed/comparator interquartile spread ratios; a spread above 1.10 makes Status inconclusive. Allocation columns report the median of five managed-thread allocation probes per public operation; they do not measure native retained memory. A dash means that the other engine cannot perform equivalent work or the snapshot does not contain that comparator. Times are medians in microseconds per public operation.");

        foreach (var (sectionName, section) in sectionRows)
        {
            var compatible = sectionName.StartsWith("pcre2-managed-compatible-", StringComparison.Ordinal);
            writer.WriteLine();
            writer.WriteLine($"## {GetPcre2MarkdownSectionTitle(sectionName)}");
            writer.WriteLine();
            if (compatible)
            {
                writer.WriteLine("| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Regex CPU | .NET predecoded CPU | .NET + decode CPU | vs decode | Utf8Pcre2 managed alloc | Comparator managed alloc |");
                writer.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|---:|---:|");
                foreach (var (caseId, row) in section.Cases.OrderBy(static row => row.Key, StringComparer.Ordinal))
                {
                    writer.WriteLine(
                        $"| `{caseId}` | **{FormatNativeStatus(row.PcreNetNativeStatus)}** | {FormatPcre2Bytes(row.InputUtf8Bytes)} | {FormatPcre2Microseconds(row.Utf8Pcre2)} | " +
                        $"{FormatPcre2NullableMicroseconds(row.PcreNetNative)} | {FormatNativeRatio(row)} | {FormatNativeInterval(row)} | " +
                        $"{FormatNativeExcess(row)} | {FormatNativePair(row)} | `{FormatManagedRoute(row)}` | " +
                        $"{FormatPcre2NullableMicroseconds(row.Utf8Regex)} | {FormatPcre2NullableMicroseconds(row.PredecodedRegex)} | " +
                        $"{FormatPcre2NullableMicroseconds(row.DecodeThenRegex)} | {FormatPcre2Ratio(row.Utf8Pcre2, row.DecodeThenRegex)} | " +
                        $"{FormatManagedAllocation(row)} | {FormatComparatorManagedAllocation(row)} |");
                }
            }
            else
            {
                writer.WriteLine("| Case | Status | Input | Utf8Pcre2 CPU | PCRE.NET / PCRE2 NFA CPU | R | 95% R | E | Paired samples | Managed route | Utf8Pcre2 managed alloc | Comparator managed alloc | Construction CPU | Construction alloc |");
                writer.WriteLine("|---|---|---:|---:|---:|---:|---:|---:|---|---|---:|---:|---:|---:|");
                foreach (var (caseId, row) in section.Cases.OrderBy(static row => row.Key, StringComparer.Ordinal))
                {
                    writer.WriteLine(
                        $"| `{caseId}` | **{FormatNativeStatus(row.PcreNetNativeStatus)}** | {FormatPcre2Bytes(row.InputUtf8Bytes)} | {FormatPcre2Microseconds(row.Utf8Pcre2)} | " +
                        $"{FormatPcre2NullableMicroseconds(row.PcreNetNative)} | {FormatNativeRatio(row)} | {FormatNativeInterval(row)} | " +
                        $"{FormatNativeExcess(row)} | {FormatNativePair(row)} | `{FormatManagedRoute(row)}` | " +
                        $"{FormatManagedAllocation(row)} | {FormatComparatorManagedAllocation(row)} | {FormatPcre2NullableMicroseconds(row.ConstructionMicroseconds)} | " +
                        $"{FormatPcre2AllocatedBytes(row.ConstructionAllocatedBytes)} |");
                }
            }
        }

        var pairedPlanRows = sectionRows
            .SelectMany(static section => section.Value.Cases.Select(row => new
            {
                Section = section.Key,
                CaseId = row.Key,
                Pair = row.Value.PcreNetNativePair,
            }))
            .Where(static row => row.Pair?.ComparatorPlanFingerprint is not null)
            .ToArray();
        writer.WriteLine();
        writer.WriteLine("## Qualified comparator plans");
        writer.WriteLine();
        if (pairedPlanRows.Length == 0)
        {
            writer.WriteLine("No paired plan fingerprints are recorded.");
        }
        else
        {
            writer.WriteLine("Plan data is captured through the comparator's public compiled-pattern information surface; JIT remains disabled for primary Status.");
            writer.WriteLine();
            writer.WriteLine("| Section | Case | Plan SHA-256 | Pattern | Frame | JIT | Min subject | First type/unit | Last type/unit |");
            writer.WriteLine("|---|---|---|---:|---:|---:|---:|---|---|");
            foreach (var row in pairedPlanRows)
            {
                var plan = row.Pair!.ComparatorPlanFingerprint!;
                writer.WriteLine(
                    $"| `{row.Section}` | `{row.CaseId}` | `{plan.Sha256[..12]}` | {plan.PatternSizeBytes:N0} B | " +
                    $"{plan.FrameSizeBytes:N0} B | {plan.JitSizeBytes:N0} B | {plan.MinimumSubjectCharacters:N0} chars | " +
                    $"{plan.FirstCodeType}/{plan.FirstCodeUnit} | {plan.LastCodeType}/{plan.LastCodeUnit} |");
            }
        }

        writer.WriteLine();
        writer.WriteLine("## Comparator exclusions");
        writer.WriteLine();
        if (unavailableNativeRows.Length == 0)
        {
            writer.WriteLine("No comparator exclusions are recorded.");
        }
        else
        {
            writer.WriteLine("Rows are excluded instead of timed when result checksums differ or PCRE.NET cannot expose equivalent UTF-8 work.");
            writer.WriteLine();
            writer.WriteLine("| Section | Case | Reason |");
            writer.WriteLine("|---|---|---|");
            foreach (var row in unavailableNativeRows)
            {
                writer.WriteLine($"| `{row.Section}` | `{row.CaseId}` | {row.Reason} |");
            }
        }

        writer.WriteLine();
        writer.WriteLine("## Scaling evidence");
        writer.WriteLine();
        writer.WriteLine("Scaling rows are mechanism and complexity guards, not direct .NET parity claims.");
        writer.WriteLine();
        writer.WriteLine("| Family | Operation | Points | Input range | Pattern range | Warm CPU range |");
        writer.WriteLine("|---|---|---:|---:|---:|---:|");
        foreach (var (familyName, family) in snapshot.ScalingFamilies.OrderBy(static family => family.Key, StringComparer.Ordinal))
        {
            var points = family.Points;
            writer.WriteLine(
                $"| `{familyName}` | `{family.Operation}` | {points.Count} | " +
                $"{FormatPcre2Range(points.Select(static point => point.InputUtf8Bytes), "B")} | " +
                $"{FormatPcre2Range(points.Select(static point => point.PatternUtf8Bytes), "B")} | " +
                $"{FormatPcre2MicrosecondRange(points.Select(static point => point.WarmMicroseconds))} |");
        }

        writer.WriteLine();
        writer.WriteLine("## Reproduce and refresh");
        writer.WriteLine();
        writer.WriteLine("Run from the repository root in `Release` through `./bench.ps1`:");
        writer.WriteLine();
        writer.WriteLine("```powershell");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--verify-pcre2-comparator-case\",\"simple/foo-dense\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--verify-pcre2-qualification-consistency\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--qualify-pcre2-comparator-case\",\"simple/foo-dense\",\"9\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--qualify-pcre2-comparator-case-reversed\",\"simple/foo-dense\",\"9\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--measure-pcre2-native-buffer-cost\",\"simple/foo-dense\",\"200\",\"5\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--emit-pcre2-priority-report\",\"relative\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--emit-pcre2-priority-report\",\"absolute\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--measure-pcre2-compatible-case\",\"common/email-match\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--measure-pcre2-special-case\",\"pcre2/branch-reset-basic\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--refresh-pcre2-benchmark-case\",\"common/email-match\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--refresh-pcre2-native-baseline-case\",\"pcre2/branch-reset-basic\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--refresh-pcre2-native-baselines\",\"pcre2-special-ismatch\",\"200\",\"7\"");
        writer.WriteLine("./bench.ps1 -CommandArgs \"--verify-pcre2-benchmark-markdown\"");
        writer.WriteLine("```");
        writer.WriteLine();
        writer.WriteLine("The benchmark catalog is [`Utf8Pcre2BenchmarkCatalog.cs`](../../bench/Lokad.Utf8Regex.Benchmarks/Utf8Pcre2BenchmarkCatalog.cs). The semantic boundary remains the managed profile described in [`SPEC-PCRE2.md`](../../SPEC-PCRE2.md), not the set of rows for which .NET has a comparator.");
        return writer.ToString();
    }

    private static void WritePcreNetDependencyReview(
        StringWriter writer,
        PcreNetNativeBaselineDependencyJson? dependency)
    {
        writer.WriteLine("## Comparator dependency");
        writer.WriteLine();
        writer.WriteLine("The benchmark executable—not either shipped library—uses one additional dependency for PCRE2-dialect rows. It provides the native PCRE2 engine and a UTF-8 span/reusable-buffer API, avoiding a string conversion or per-call wrapper allocation in the comparator.");
        writer.WriteLine();
        writer.WriteLine("| Package | Version | Native engine | License | Source revision | Benchmark profile |");
        writer.WriteLine("|---|---:|---|---|---|---|");
        if (dependency is null)
        {
            writer.WriteLine("| [`PCRE.NET`](https://www.nuget.org/packages/PCRE.NET) | — | — | — | — | Native baseline metadata has not been refreshed. |");
        }
        else
        {
            writer.WriteLine(
                $"| [`{dependency.PackageId}`](https://www.nuget.org/packages/PCRE.NET/{dependency.PackageVersion}) | `{dependency.PackageVersion}` | `{dependency.NativePcre2Version}` | `{dependency.License}` | [`{dependency.SourceRevision[..12]}`]({dependency.SourceRepository}/commit/{dependency.SourceRevision}) | {dependency.Profile} |");
        }

        writer.WriteLine();
        writer.WriteLine("Admission evidence: the package has a dedicated `net10.0` asset with no managed dependencies, is strongly named, is built and tested on Windows/Linux/macOS, and its tagged source has been maintained since 2014. It bundles RID-specific native libraries, so `PrivateAssets=all` and benchmark-project-only placement are mandatory. Native replacement is left blank because PCRE.NET does not expose equivalent UTF-8 span substitution output; routing through its string API would bias the comparison.");
        if (dependency?.BuildFingerprint is { } build)
        {
            writer.WriteLine();
            writer.WriteLine(
                $"Native build fingerprint: `{build.Sha256}`; process/OS architecture `{build.ProcessArchitecture}/{build.OperatingSystemArchitecture}`; " +
                $"JIT support `{build.JitSupported}` targeting `{build.JitTarget}`; Unicode `{build.UnicodeVersion}`; compiled-width mask `{build.CompiledWidths}`; " +
                $"link/effective-link size `{build.LinkSizeBytes}/{build.EffectiveLinkSizeBytes}` bytes.");
            writer.WriteLine();
            writer.WriteLine(
                $"Build defaults: newline `{build.DefaultNewline}`, `\\R` `{build.DefaultBackslashR}`, heap `{build.DefaultHeapLimitKibibytes:N0}` KiB, " +
                $"match/depth/parentheses limits `{build.DefaultMatchLimit:N0}/{build.DefaultDepthLimit:N0}/{build.ParenthesesLimit:N0}`, " +
                $"character tables `{build.CharacterTablesLengthBytes:N0}` bytes.");
        }

        if (dependency?.WorkspaceContract is { } workspace)
        {
            writer.WriteLine();
            writer.WriteLine(
                $"Comparator qualification lifecycle (`{workspace.StateHolder}`): {workspace.Lifetime} " +
                $"{workspace.ConcurrencyContract} {workspace.RetainedMemoryContract}");
            writer.WriteLine();
            writer.WriteLine(workspace.RetainedNativeHeapHighWaterBytes is { } retainedBytes
                ? $"Retained native match-data heap-frame high water: `{retainedBytes:N0}` bytes."
                : $"Retained native match-data heap-frame high water: unavailable — {workspace.RetainedNativeHeapHighWaterUnavailableReason}");
        }
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

    private static string FormatPcre2Microseconds(double value) => $"{value:N3} us";

    private static string FormatPcre2Timestamp(DateTimeOffset value) =>
        value == default ? "not recorded" : value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static string FormatPcre2NullableMicroseconds(double? value) =>
        value is > 0 ? FormatPcre2Microseconds(value.Value) : "—";

    private static string FormatPcre2Ratio(double value, double? baseline) =>
        baseline is > 0 ? $"{value / baseline.Value:F2}x" : "—";

    private static string FormatPcre2Bytes(int? value) => value.HasValue ? $"{value.Value:N0} B" : "—";

    private static string FormatPcre2AllocatedBytes(long? value) => value.HasValue ? $"{value.Value:N0} B" : "—";

    private static string FormatPcre2Range(IEnumerable<int> values, string suffix)
    {
        var range = values.ToArray();
        return range.Length == 0 ? "—" : $"{range.Min():N0}–{range.Max():N0} {suffix}";
    }

    private static string FormatPcre2MicrosecondRange(IEnumerable<double> values)
    {
        var range = values.ToArray();
        return range.Length == 0 ? "—" : $"{range.Min():N3}–{range.Max():N3} us";
    }

    private static string ComputePcre2SnapshotSha256() =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(
            Path.Combine(Path.GetDirectoryName(FindRepoFile("README.md"))!, Pcre2BenchmarkSnapshotFileName))));

}
