using Lokad.Utf8Regex.Posix;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private const string PosixSnapshotName = "Posix.Benchmarks.json";
    private static readonly string[] PosixLanes = ["Unlimited", "FiniteLive", "FundedFiniteLive", "FiniteDeadlineLive", "NativeNfa"];

    private static int[] PosixLaneIndices(PosixBenchmarkCase row, string? exclusion)
        => row.MeasurePolicies ? exclusion is null ? [0, 1, 2, 3, 4] : [0, 1, 2, 3]
            : exclusion is null ? [0, 4] : [0];

    internal static bool TryHandlePosixCommand(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0) return false;
        switch (args[0])
        {
            case "--inspect-posix-cases":
                foreach (var row in PosixBenchmarkCatalog.Cases)
                {
                    using var context = new PosixBenchmarkContext(row);
                    var reason = PreflightPosix(row, context);
                    Console.WriteLine($"{row.Id}: expected result verified; native={(reason ?? "equal public results")}");
                }
                return true;
            case "--measure-posix-case":
                if (args.Length is < 2 or > 3) throw new ArgumentException("Select one POSIX valid-text case and optional sample count.");
                exitCode = RefreshPosix(args[1], args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 9,
                    writeSnapshot: false);
                return true;
            case "--refresh-posix-benchmarks":
                if (args.Length > 3) throw new ArgumentException("POSIX comparisons consume existing valid UTF-8 text; no validation-policy argument is accepted.");
                exitCode = RefreshPosix(args.Length > 1 ? args[1] : null, args.Length > 2 ? int.Parse(args[2], CultureInfo.InvariantCulture) : 9);
                return true;
            case "--rewrite-posix-benchmark-markdown": RewritePosixMarkdown(verify: false); return true;
            case "--verify-posix-benchmark-markdown": RewritePosixMarkdown(verify: true); return true;
            case "--verify-posix-benchmark-evidence": VerifyPosixEvidence(); return true;
            default: return false;
        }
    }

    private static string? PreflightPosix(PosixBenchmarkCase row, PosixBenchmarkContext context)
    {
        var expected = PosixBenchmarkCatalog.Expected(row.Id);
        for (var lane = 0; lane < 4; lane++)
        {
            if (context.Execute(lane) != expected) throw new InvalidOperationException($"POSIX {row.Id}/{PosixLanes[lane]} differs from the pinned result.");
            if (row.Records > 1)
            {
                var records = context.ReadCaptureTrace(lane);
                if (records.Length != row.Records || records.Any(actual => actual != expected))
                    throw new InvalidOperationException($"POSIX {row.Id}/{PosixLanes[lane]} repeated captures changed.");
                if (row.Id == "capture/sku007" && records.Sum(actual =>
                    1 + actual.Group1.LengthInBytes + actual.Group2.IndexInBytes + actual.Group2.LengthInBytes) != 40960)
                    throw new InvalidOperationException("The observed sku007 capture checksum changed.");
            }
        }
        if (context.OutstandingWorkingBytes != 0) throw new InvalidOperationException("POSIX preflight leaked working storage.");
        return context.Execute(4) == expected && context.ReadTrace(false).SequenceEqual(context.ReadTrace(true))
            ? null : "Ordered native NFA returns different ranges or captures for this input; no POSIX ratio.";
    }

    private static int RefreshPosix(string? filter, int samples,
        bool writeSnapshot = true)
    {
#if DEBUG
        throw new InvalidOperationException("POSIX measurements require Release binaries.");
#else
        if (samples is < 9 or > 31) throw new ArgumentOutOfRangeException(nameof(samples));
        if (!writeSnapshot && string.IsNullOrEmpty(filter))
            throw new ArgumentException("Standalone measurements select one valid-text case.");
        var environment = CaptureBenchmarkEnvironment();
        if (environment.TrackedDirty || environment.HasUntrackedFiles)
            throw new InvalidOperationException("POSIX measurement requires clean tracked source inputs.");
        foreach (var (project, assembly) in new[]
        {
            ("bench/Lokad.Utf8Regex.Benchmarks", typeof(BenchmarkInspectReporter).Assembly),
            ("src/Lokad.Utf8Regex", typeof(Utf8Regex).Assembly),
            ("src/Lokad.Utf8Regex.Posix", typeof(Utf8PosixRegex).Assembly),
        }) PythonReBenchmarkReporter.RequireLoadedAssemblySources(project, GetAssemblyIdentity(assembly), project, assembly);
        using var placement = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        var repository = PosixRepository();
        var path = BenchmarkSummary.RawPath(repository, PosixSnapshotName);
        var selected = PosixBenchmarkCatalog.Cases.Where(row => filter is null or "" || row.Id == filter).ToArray();
        if (selected.Length == 0) throw new ArgumentException($"Unknown POSIX case: {filter}");
        var snapshot = !writeSnapshot || string.IsNullOrEmpty(filter) || !File.Exists(path) ? new PosixBenchmarkSnapshot() : ReadPosixSnapshot();
        if (snapshot.Cases.Count != 0 && (snapshot.SchemaVersion != 2 || snapshot.CatalogSha256 != PosixCatalogHash()))
            throw new InvalidOperationException("POSIX catalog changed; refresh the selected POSIX catalog once before refreshing individual rows.");
        snapshot.GeneratedAtUtc = DateTimeOffset.UtcNow;
        snapshot.CatalogSha256 = PosixCatalogHash();
        var nativeAssembly = GetAssemblyIdentity(typeof(PCRE.PcreRegexUtf8).Assembly);
        var nativeBuild = PcreNetNativeBenchmarkBaseline.CaptureBuildFingerprint().Sha256;
        foreach (var benchmarkCase in selected)
        {
            using var context = new PosixBenchmarkContext(benchmarkCase);
            var exclusion = PreflightPosix(benchmarkCase, context);
            var result = new PosixBenchmarkRow
            {
                Operation = benchmarkCase.Operation.ToString(), Records = benchmarkCase.Records,
                StartOffsetInBytes = benchmarkCase.StartOffsetInBytes,
                InputSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(benchmarkCase.Input))),
                Environment = environment, PosixAssembly = GetAssemblyIdentity(typeof(Utf8PosixRegex).Assembly),
                NativeAssembly = nativeAssembly, NativeBuild = nativeBuild,
                ProcessorPolicy = placement.Policy, ProcessorAffinityMask = placement.AffinityMask,
                Expected = PosixResultJson(PosixBenchmarkCatalog.Expected(benchmarkCase.Id)),
                NativeExclusion = exclusion,
            };
            var lanes = PosixLaneIndices(benchmarkCase, exclusion);
            var counts = new int[5];
            foreach (var lane in lanes)
            {
                var warm = Stopwatch.StartNew(); var calls = 0;
                do { CheckPosixResult(context.Measure(lane, 1).Result, benchmarkCase.Id); calls++; }
                while (warm.ElapsedMilliseconds < 750 || calls < 64 && warm.ElapsedMilliseconds < 1500);
                warm.Stop();
                counts[lane] = CalibratePosix(context, lane, benchmarkCase.Id);
                result.Lanes[PosixLanes[lane]] = new() { Iterations = counts[lane], WarmCalls = calls, WarmMilliseconds = warm.Elapsed.TotalMilliseconds };
            }
            for (var sample = 0; sample < samples; sample++)
            {
                result.LaneOrders.Add((sample & 1) == 0 ? "forward" : "reverse");
                for (var position = 0; position < lanes.Length; position++)
                {
                    var lane = lanes[(sample & 1) == 0 ? position : lanes.Length - 1 - position];
                    GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                    var batch = context.Measure(lane, counts[lane]);
                    CheckPosixResult(batch.Result, benchmarkCase.Id);
                    var target = result.Lanes[PosixLanes[lane]];
                    target.Microseconds.Add(batch.Elapsed.TotalMicroseconds / counts[lane]);
                    target.Milliseconds.Add(batch.Elapsed.TotalMilliseconds);
                    target.AllocatedBytes.Add((double)batch.AllocatedBytes / counts[lane]);
                }
            }
            result.PeakFundedWorkingBytes = context.PeakWorkingBytes;
            QualifyPosixRow(result);
            if (context.OutstandingWorkingBytes != 0) throw new InvalidOperationException("POSIX measurement leaked working storage.");
            if (!writeSnapshot)
            {
                Console.WriteLine(JsonSerializer.Serialize(new { CaseId = benchmarkCase.Id, InputContract = "Preexisting valid unchanged UTF-8 text; native PCRE2 NFA uses NO_UTF_CHECK",
                    NativeInputContract = "PCRE2 UTF mode with NO_UTF_CHECK; caller guarantees valid text", Records = benchmarkCase.Records, Result = result }, PosixJsonOptions));
                continue;
            }
            snapshot.Cases[benchmarkCase.Id] = result;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            BenchmarkFileWriter.WriteTextAtomically(path, JsonSerializer.Serialize(snapshot, PosixJsonOptions) + "\n");
            Console.WriteLine($"{benchmarkCase.Id}: {result.Status}; managed={Median(result.Lanes["Unlimited"].Microseconds):F3} us; records={benchmarkCase.Records}; host peak={result.PeakFundedWorkingBytes} B");
        }
        if (writeSnapshot)
        {
            WritePosixSummary(snapshot);
            RewritePosixMarkdown(verify: false);
        }
        return 0;
#endif
    }

    private static int CalibratePosix(PosixBenchmarkContext context, int lane, string id)
    {
        var count = 1;
        var probe = context.Measure(lane, count);
        CheckPosixResult(probe.Result, id);
        while (probe.Elapsed.TotalMilliseconds < 20 && count < 1_000_000)
        {
            count = Math.Min(count * 2, 1_000_000);
            probe = context.Measure(lane, count); CheckPosixResult(probe.Result, id);
        }
        count = (int)Math.Clamp(Math.Ceiling(count * 75 / Math.Max(probe.Elapsed.TotalMilliseconds, .000001)), 1, 1_000_000);
        var confirmation = context.Measure(lane, count); CheckPosixResult(confirmation.Result, id);
        if (confirmation.Elapsed.TotalMilliseconds is < 50 or > 110)
            count = (int)Math.Clamp(Math.Ceiling(count * 75 / Math.Max(confirmation.Elapsed.TotalMilliseconds, .000001)), 1, 1_000_000);
        return count;
    }

    private static void CheckPosixResult(PosixBenchmarkResult result, string id)
    { if (result != PosixBenchmarkCatalog.Expected(id)) throw new InvalidOperationException($"POSIX timed result changed: {id}"); }

    private static void QualifyPosixRow(PosixBenchmarkRow row)
    {
        row.Status = "Excluded"; row.StatusReason = row.NativeExclusion;
        row.Ratio = row.Lower95 = row.Upper95 = null; row.OrderEffect = 0;
        if (row.Lanes.Values.Any(lane => lane.Milliseconds.Min() < 20))
        { row.Status = "Unqualified"; row.StatusReason = "A sample fell below the existing 20 ms duration floor."; }
        else if (row.NativeExclusion is null)
        {
            var logRatios = row.Lanes["Unlimited"].Microseconds.Zip(row.Lanes["NativeNfa"].Microseconds,
                (managed, native) => Math.Log(managed / native)).ToArray();
            var confidence = BenchmarkPairedStatistics.BootstrapMedianLogRatio(logRatios, Pcre2QualificationBootstrapSeed, Pcre2QualificationBootstrapResamples);
            row.Ratio = Math.Exp(BenchmarkPairedStatistics.Median(logRatios));
            row.Lower95 = Math.Exp(confidence.Lower); row.Upper95 = Math.Exp(confidence.Upper);
            row.OrderEffect = Median(logRatios.Where((_, index) => (index & 1) == 0).Select(Math.Exp)) /
                Median(logRatios.Where((_, index) => (index & 1) != 0).Select(Math.Exp));
            var (status, reason) = DerivePairedStatus(confidence.Lower, confidence.Upper, row.OrderEffect,
                BenchmarkPairedStatistics.InterquartileSpread(row.Lanes["Unlimited"].Microseconds),
                BenchmarkPairedStatistics.InterquartileSpread(row.Lanes["NativeNfa"].Microseconds), true);
            row.Status = status.ToString(); row.StatusReason = reason;
        }
    }

    private static JsonElement PosixResultJson(PosixBenchmarkResult result)
    {
        static object? Range(Utf8ValueMatch value) => value.Success ? new
        { value.IndexInBytes, value.LengthInBytes, value.IndexInUtf16, value.LengthInUtf16 } : null;
        return JsonSerializer.SerializeToElement(new { result.Count, Whole = Range(result.Whole),
            Group1 = Range(result.Group1), Group2 = Range(result.Group2), Group3 = Range(result.Group3), Group4 = Range(result.Group4) });
    }

    private static string PosixRepository() => Path.GetDirectoryName(FindRepoFile("README.md"))!;
    private static string PosixCatalogHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(PosixBenchmarkCatalog.Cases))));
    private static double Median(IEnumerable<double> values) => BenchmarkPairedStatistics.Median(values);
    private static readonly JsonSerializerOptions PosixJsonOptions = new() { WriteIndented = true };

    private static PosixBenchmarkSnapshot ReadPosixSnapshot() => JsonSerializer.Deserialize<PosixBenchmarkSnapshot>(
        File.ReadAllText(BenchmarkSummary.RawPath(PosixRepository(), PosixSnapshotName)), PosixJsonOptions)
        ?? throw new InvalidOperationException("Missing POSIX raw benchmark evidence.");

    private static void WritePosixSummary(PosixBenchmarkSnapshot snapshot) => BuildPosixSummary(snapshot).Write(PosixRepository());

    private static BenchmarkSummary BuildPosixSummary(PosixBenchmarkSnapshot snapshot)
    {
        var repository = PosixRepository();
        var summary = new BenchmarkSummary
        {
            Profile = "Posix", CatalogSha256 = snapshot.CatalogSha256,
            Evidence = BenchmarkSummary.DescribeEvidence(repository, PosixSnapshotName),
            Comparator = new() { PackageId = "PCRE.NET", PackageVersion = "1.5.0", Engine = "Native ordered NFA on verified equal-result inputs only", License = "BSD-3-Clause" },
        };
        foreach (var (id, row) in snapshot.Cases)
        {
            var source = summary.AddSource(new { row.Environment, row.PosixAssembly, row.NativeAssembly, row.NativeBuild });
            var section = id == "capture/sku007" ? "AdapterControl" : id.StartsWith("history/", StringComparison.Ordinal) ? "HistoryScaling" : "Operations";
            summary.AddRow(section, id, new()
            {
                Source = source, Operation = row.Records > 1 ? $"{row.Operation} × {row.Records}" : row.Operation,
                // Canonicalize embedded JSON: raw snapshot indentation must not
                // change the result contract after a write/read round trip.
                InputSha256 = row.InputSha256, ResultContract = JsonSerializer.Serialize(row.Expected),
                ManagedUs = Median(row.Lanes["Unlimited"].Microseconds), ManagedBytes = Median(row.Lanes["Unlimited"].AllocatedBytes),
                ReferenceUs = row.Lanes.TryGetValue("NativeNfa", out var native) ? Median(native.Microseconds) : null,
                ReferenceBytes = native is not null ? Median(native.AllocatedBytes) : null,
                Ratio = row.Ratio, Lower95 = row.Lower95, Upper95 = row.Upper95, Status = row.Status, Reason = row.StatusReason,
                PeakWorkingBytes = row.PeakFundedWorkingBytes,
            });
            foreach (var lane in new[] { "FiniteLive", "FundedFiniteLive", "FiniteDeadlineLive" })
            {
                if (!row.Lanes.TryGetValue(lane, out var measured)) continue;
                summary.AddRow(lane, id, new()
                {
                    Source = source, Operation = row.Operation, ManagedUs = Median(measured.Microseconds),
                    ManagedBytes = Median(measured.AllocatedBytes), Status = row.Status == "Unqualified" ? "Unqualified" : "Measured",
                    Ratio = Median(measured.Microseconds) / Median(row.Lanes["Unlimited"].Microseconds),
                    Reason = lane == "FundedFiniteLive" ? $"Peak covered host working storage {row.PeakFundedWorkingBytes} bytes; zero outstanding loans." : null,
                });
            }
        }
        return summary;
    }

    private static void RewritePosixMarkdown(bool verify)
    {
        var repository = PosixRepository(); var summary = BenchmarkSummary.Read(repository, "Posix");
        var path = Path.Combine(repository, "src", "Lokad.Utf8Regex.Posix", "README.md");
        var document = File.ReadAllText(path);
        var updated = BenchmarkFileWriter.ReplaceGeneratedSection(document, "POSIX", RenderPosixMarkdown(summary));
        if (verify && document.ReplaceLineEndings("\n") != updated.ReplaceLineEndings("\n"))
            throw new InvalidOperationException("POSIX README differs from its compact benchmark summary.");
        if (!verify) BenchmarkFileWriter.WriteTextAtomically(path, updated);
        Console.WriteLine(verify ? "POSIX README verified from the compact summary." : "POSIX README rewritten from the compact summary.");
    }

    private static string RenderPosixMarkdown(BenchmarkSummary summary)
    {
        var text = new StringBuilder();
        text.AppendLine("## Benchmarks").AppendLine();
        text.AppendLine("Warm .NET 11 public operations over prepared UTF-8; compilation and input preparation are excluded. These rows cover POSIX longest selection, capture participation and nested/nullable histories. Unicode and nonzero floors are part of a selection case. Core's generic search, validation, dense counting and scalar progression are not repeated here. Both lanes require existing valid unchanged UTF-8 text. Native PCRE2 NFA uses UTF mode with NO_UTF_CHECK, a prepared reusable match buffer and equivalent byte/UTF-16 result projection. Its ratios apply only to verified equal-result inputs, not general POSIX semantics. Native allocations below cover CLR traffic, excluding native workspace.").AppendLine();
        text.AppendLine("| Case | Operation | Utf8Regex (µs) | Native NFA (µs) | Managed B/op | Status | Ratio (lower is better) |");
        text.AppendLine("|---|---|---:|---:|---:|---|---:|");
        foreach (var (id, row) in summary.Sections["Operations"].OrderByDescending(pair => pair.Value.DisplayRatio ?? double.NegativeInfinity).ThenBy(pair => pair.Key, StringComparer.Ordinal))
            text.AppendLine($"| `{id}` | {row.Operation} | {Number(row.ManagedUs)} | {Number(row.ReferenceUs)} | {row.ManagedBytes?.ToString("F0", CultureInfo.InvariantCulture)} | {row.DisplayStatus} | {Number(row.DisplayRatio)} |");
        text.AppendLine().AppendLine("The ratio is the median paired managed/native time; raw samples retain 95% bootstrap intervals and lane-order observations. Excluded rows have different native ranges/captures and no ratio. All rows retain ordinary costs, even when no equivalent native result exists.").AppendLine();
        text.AppendLine("### Bounded history growth").AppendLine();
        text.AppendLine("The same `(a|aa)*` capture operation at three lengths exposes ambiguity cost. Earlier iterations prefer `aa`; ordered native captures differ, so no native ratio is reported. Working peak is observed in untimed funded preflight with a finite policy; it is a conservative admission charge, not exact heap size.").AppendLine();
        text.AppendLine("| Case | Input bytes | Utf8Regex (µs) | Managed B/op | Peak working B |");
        text.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var (id, row) in summary.Sections["HistoryScaling"].OrderBy(pair => PosixBenchmarkCatalog.Get(pair.Key).Input.Length))
            text.AppendLine($"| `{id}` | {PosixBenchmarkCatalog.Get(id).Input.Length} | {Number(row.ManagedUs)} | {row.ManagedBytes?.ToString("F0", CultureInfo.InvariantCulture)} | {row.PeakWorkingBytes} |");
        text.AppendLine().AppendLine("### Historical adapter control").AppendLine();
        text.AppendLine("SKU is an unambiguous consumer control, not a POSIX selection claim. One timed batch performs 4,096 independent Match calls with reused caller capture storage; the table divides time and allocations by 4,096. Every record's captures and checksum are checked outside timing.").AppendLine();
        text.AppendLine("| Case | Utf8Regex / call (µs) | Native NFA / call (µs) | Managed B/call | Ratio (lower is better) |");
        text.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var (id, row) in summary.Sections["AdapterControl"])
        {
            var records = PosixBenchmarkCatalog.Get(id).Records;
            text.AppendLine($"| `{id}` | {Number(row.ManagedUs / records)} | {Number(row.ReferenceUs / records)} | {row.ManagedBytes / records:F0} | {Number(row.DisplayRatio)} |");
        }
        text.AppendLine().AppendLine("### Representative policy costs").AppendLine();
        text.AppendLine("| Case | Finite + live token | Funded finite + live token | Finite + deadline + live token | Funded / unlimited |");
        text.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var (id, funded) in summary.Sections["FundedFiniteLive"].OrderByDescending(pair => pair.Value.Ratio).ThenBy(pair => pair.Key, StringComparer.Ordinal))
            text.AppendLine($"| `{id}` | {Number(summary.Sections["FiniteLive"][id].ManagedUs)} µs | {Number(funded.ManagedUs)} µs | {Number(summary.Sections["FiniteDeadlineLive"][id].ManagedUs)} µs | {Number(funded.Ratio)} |");
        text.AppendLine().AppendLine("Only the empty-capture, longest-cursor and SKU control rows time all policy lanes; SKU costs here remain per 4,096-call batch. Finite lanes enforce work/depth/working limits and a positive result limit under an original live token. Funded lanes add a thread-safe 16 MiB host allowance; the deadline lane adds a 30-second invocation timeout. Values/caller-owned capture destinations create no producer result payload. These are absolute public-operation costs, including useful metering; the unlimited native comparator has no corresponding host policy.");
        text.AppendLine().AppendLine($"[Compact summary](../../bench/Snapshots/Posix.json). Snapshot SHA-256: `{summary.Evidence.SnapshotSha256}`.");
        return text.ToString();
        static string Number(double? value) => value?.ToString("F2", CultureInfo.InvariantCulture) ?? "—";
    }

    private static void VerifyPosixEvidence()
    {
        var snapshot = ReadPosixSnapshot();
        var current = CaptureBenchmarkEnvironment();
        var nativeAssembly = GetAssemblyIdentity(typeof(PCRE.PcreRegexUtf8).Assembly);
        var nativeBuild = PcreNetNativeBenchmarkBaseline.CaptureBuildFingerprint().Sha256;
        if (snapshot.SchemaVersion != 2 || snapshot.CatalogSha256 != PosixCatalogHash() ||
            snapshot.Cases.Count != PosixBenchmarkCatalog.Cases.Length)
            throw new InvalidOperationException("POSIX benchmark catalog changed.");
        foreach (var row in PosixBenchmarkCatalog.Cases)
        {
            if (!snapshot.Cases.TryGetValue(row.Id, out var evidence) || evidence.Status == "Unqualified" ||
                JsonSerializer.Serialize(evidence.Expected) != JsonSerializer.Serialize(PosixResultJson(PosixBenchmarkCatalog.Expected(row.Id))) ||
                evidence.InputSha256 != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.Input))) ||
                evidence.Operation != row.Operation.ToString() || evidence.Records != row.Records || evidence.StartOffsetInBytes != row.StartOffsetInBytes)
                throw new InvalidOperationException($"POSIX benchmark evidence is incomplete: {row.Id}");
            if (evidence.Environment.CoreAssembly != current.CoreAssembly || evidence.Environment.BenchmarkAssembly != current.BenchmarkAssembly ||
                evidence.Environment.Runtime != RuntimeInformation.FrameworkDescription ||
                evidence.Environment.TrackedDirty || evidence.Environment.HasUntrackedFiles ||
                evidence.PosixAssembly != GetAssemblyIdentity(typeof(Utf8PosixRegex).Assembly) ||
                evidence.NativeAssembly != nativeAssembly || evidence.NativeBuild != nativeBuild)
                throw new InvalidOperationException($"POSIX benchmark evidence does not identify this loaded source/runtime: {row.Id}");
            using var context = new PosixBenchmarkContext(row);
            if (PreflightPosix(row, context) != evidence.NativeExclusion)
                throw new InvalidOperationException($"POSIX native exclusion changed: {row.Id}");
            var expectedLanes = PosixLaneIndices(row, evidence.NativeExclusion).Select(index => PosixLanes[index]).Order(StringComparer.Ordinal);
            var sampleCount = evidence.LaneOrders.Count;
            if (sampleCount is < 9 or > 31 || !evidence.Lanes.Keys.SequenceEqual(expectedLanes) ||
                !evidence.LaneOrders.SequenceEqual(Enumerable.Range(0, sampleCount).Select(index => (index & 1) == 0 ? "forward" : "reverse")))
                throw new InvalidOperationException($"POSIX benchmark lanes/orders are incomplete: {row.Id}");
            foreach (var lane in evidence.Lanes.Values)
            {
                if (lane.Iterations <= 0 || lane.Microseconds.Count != sampleCount || lane.Milliseconds.Count != sampleCount ||
                    lane.AllocatedBytes.Count != sampleCount || lane.AllocatedBytes.Any(value => !double.IsFinite(value) || value < 0) ||
                    lane.Milliseconds.Any(value => !double.IsFinite(value) || value < 20) ||
                    lane.Microseconds.Where((value, index) => !double.IsFinite(value) || value <= 0 ||
                        !Same(value, lane.Milliseconds[index] * 1000 / lane.Iterations)).Any())
                    throw new InvalidOperationException($"POSIX benchmark samples are inconsistent: {row.Id}");
            }
            var recorded = (evidence.Status, evidence.StatusReason, evidence.Ratio, evidence.Lower95, evidence.Upper95, evidence.OrderEffect);
            QualifyPosixRow(evidence);
            if (evidence.Status != recorded.Status || evidence.StatusReason != recorded.StatusReason ||
                !Same(evidence.Ratio, recorded.Ratio) || !Same(evidence.Lower95, recorded.Lower95) ||
                !Same(evidence.Upper95, recorded.Upper95) || !Same(evidence.OrderEffect, recorded.OrderEffect))
                throw new InvalidOperationException($"POSIX benchmark qualification differs from its samples: {row.Id}");
            // Keep the recorded observations after a tolerance-aware recomputation:
            // platform libm rounding must not rewrite historical summary values.
            (evidence.Status, evidence.StatusReason, evidence.Ratio, evidence.Lower95, evidence.Upper95, evidence.OrderEffect) = recorded;
        }
        if (JsonSerializer.Serialize(BuildPosixSummary(snapshot), PosixJsonOptions) !=
            JsonSerializer.Serialize(BenchmarkSummary.Read(PosixRepository(), "Posix"), PosixJsonOptions))
            throw new InvalidOperationException("POSIX raw evidence differs from its compact summary.");
        RewritePosixMarkdown(verify: true);
        Console.WriteLine("POSIX catalog, result contracts, durations and loaded identities verified.");

        static bool Same(double? a, double? b) => a is null || b is null ? a == b :
            Math.Abs(a.Value - b.Value) <= Math.Max(1e-9, Math.Abs(b.Value) * 1e-9);
    }

    private sealed class PosixBenchmarkSnapshot
    {
        public int SchemaVersion { get; set; } = 2;
        public DateTimeOffset GeneratedAtUtc { get; set; }
        public string CatalogSha256 { get; set; } = "";
        public SortedDictionary<string, PosixBenchmarkRow> Cases { get; set; } = new(StringComparer.Ordinal);
    }
    private sealed class PosixBenchmarkRow
    {
        public required string Operation { get; init; }
        public int Records { get; init; }
        public int StartOffsetInBytes { get; init; }
        public required string InputSha256 { get; init; }
        public required BenchmarkEnvironmentJson Environment { get; init; }
        public required string PosixAssembly { get; init; }
        public required string NativeAssembly { get; init; }
        public required string NativeBuild { get; init; }
        public required string ProcessorPolicy { get; init; }
        public required string ProcessorAffinityMask { get; init; }
        public required JsonElement Expected { get; init; }
        public string? NativeExclusion { get; set; }
        public string Status { get; set; } = "";
        public string? StatusReason { get; set; }
        public double OrderEffect { get; set; }
        public List<string> LaneOrders { get; set; } = [];
        public double? Ratio { get; set; }
        public double? Lower95 { get; set; }
        public double? Upper95 { get; set; }
        public ulong PeakFundedWorkingBytes { get; set; }
        public SortedDictionary<string, PosixBenchmarkLane> Lanes { get; set; } = new(StringComparer.Ordinal);
    }
    private sealed class PosixBenchmarkLane
    {
        public int Iterations { get; init; }
        public int WarmCalls { get; init; }
        public double WarmMilliseconds { get; init; }
        public List<double> Microseconds { get; set; } = [];
        public List<double> Milliseconds { get; set; } = [];
        public List<double> AllocatedBytes { get; set; } = [];
    }
}
