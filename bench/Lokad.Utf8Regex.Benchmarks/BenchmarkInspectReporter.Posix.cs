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
            case "--refresh-posix-benchmarks":
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
            if (context.Execute(lane) != expected) throw new InvalidOperationException($"POSIX {row.Id}/{PosixLanes[lane]} differs from the pinned result.");
        if (context.OutstandingWorkingBytes != 0) throw new InvalidOperationException("POSIX preflight leaked working storage.");
        if (row.Id == "capture/sku007" &&
            (1 + expected.Group1.LengthInBytes + expected.Group2.IndexInBytes + expected.Group2.LengthInBytes) * expected.Count != 40960)
            throw new InvalidOperationException("The sku007 capture checksum changed.");
        return context.Execute(4) == expected && context.ReadTrace(false).SequenceEqual(context.ReadTrace(true))
            ? null : "Ordered native NFA returns different ranges or captures for this input; no POSIX ratio.";
    }

    private static int RefreshPosix(string? filter, int samples)
    {
#if DEBUG
        throw new InvalidOperationException("POSIX measurements require Release binaries.");
#else
        if (samples is < 9 or > 31) throw new ArgumentOutOfRangeException(nameof(samples));
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
        var snapshot = File.Exists(path) ? ReadPosixSnapshot() : new PosixBenchmarkSnapshot();
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
            var laneCount = exclusion is null ? 5 : 4;
            var counts = new int[laneCount];
            for (var lane = 0; lane < laneCount; lane++)
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
                for (var position = 0; position < laneCount; position++)
                {
                    var lane = (sample & 1) == 0 ? position : laneCount - 1 - position;
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
            result.Status = "Excluded";
            result.StatusReason = exclusion;
            if (result.Lanes.Values.Any(lane => lane.Milliseconds.Min() < 20))
            { result.Status = "Unqualified"; result.StatusReason = "A sample fell below the existing 20 ms duration floor."; }
            else if (exclusion is null)
            {
                var logRatios = result.Lanes["Unlimited"].Microseconds.Zip(result.Lanes["NativeNfa"].Microseconds,
                    (managed, native) => Math.Log(managed / native)).ToArray();
                var confidence = BenchmarkPairedStatistics.BootstrapMedianLogRatio(logRatios, Pcre2QualificationBootstrapSeed, Pcre2QualificationBootstrapResamples);
                result.Ratio = Math.Exp(BenchmarkPairedStatistics.Median(logRatios));
                result.Lower95 = Math.Exp(confidence.Lower); result.Upper95 = Math.Exp(confidence.Upper);
                result.OrderEffect = Median(logRatios.Where((_, index) => (index & 1) == 0).Select(Math.Exp)) /
                    Median(logRatios.Where((_, index) => (index & 1) != 0).Select(Math.Exp));
                var (status, reason) = DerivePairedStatus(confidence.Lower, confidence.Upper, result.OrderEffect,
                    BenchmarkPairedStatistics.InterquartileSpread(result.Lanes["Unlimited"].Microseconds),
                    BenchmarkPairedStatistics.InterquartileSpread(result.Lanes["NativeNfa"].Microseconds), true);
                result.Status = status.ToString(); result.StatusReason = reason;
            }
            snapshot.Cases[benchmarkCase.Id] = result;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            BenchmarkFileWriter.WriteTextAtomically(path, JsonSerializer.Serialize(snapshot, PosixJsonOptions) + "\n");
            Console.WriteLine($"{benchmarkCase.Id}: {result.Status}; managed={Median(result.Lanes["Unlimited"].Microseconds):F3} us; records={benchmarkCase.Records}; host peak={result.PeakFundedWorkingBytes} B");
        }
        WritePosixSummary(snapshot);
        RewritePosixMarkdown(verify: false);
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

    private static JsonElement PosixResultJson(PosixBenchmarkResult result)
    {
        static object? Range(Utf8ValueMatch value) => value.Success ? new
        { value.IndexInBytes, value.LengthInBytes, value.IndexInUtf16, value.LengthInUtf16 } : null;
        return JsonSerializer.SerializeToElement(new { result.Count, Whole = Range(result.Whole), Group1 = Range(result.Group1), Group2 = Range(result.Group2) });
    }

    private static string PosixRepository() => Path.GetDirectoryName(FindRepoFile("README.md"))!;
    private static string PosixCatalogHash() => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(PosixBenchmarkCatalog.Cases))));
    private static double Median(IEnumerable<double> values) => BenchmarkPairedStatistics.Median(values);
    private static readonly JsonSerializerOptions PosixJsonOptions = new() { WriteIndented = true };

    private static PosixBenchmarkSnapshot ReadPosixSnapshot() => JsonSerializer.Deserialize<PosixBenchmarkSnapshot>(
        File.ReadAllText(BenchmarkSummary.RawPath(PosixRepository(), PosixSnapshotName)), PosixJsonOptions)
        ?? throw new InvalidOperationException("Missing POSIX raw benchmark evidence.");

    private static void WritePosixSummary(PosixBenchmarkSnapshot snapshot)
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
            summary.AddRow("Operations", id, new()
            {
                Source = source, Operation = row.Records > 1 ? $"{row.Operation} × {row.Records}" : row.Operation,
                InputSha256 = row.InputSha256, ResultContract = row.Expected.GetRawText(),
                ManagedUs = Median(row.Lanes["Unlimited"].Microseconds), ManagedBytes = Median(row.Lanes["Unlimited"].AllocatedBytes),
                ReferenceUs = row.Lanes.TryGetValue("NativeNfa", out var native) ? Median(native.Microseconds) : null,
                ReferenceBytes = native is not null ? Median(native.AllocatedBytes) : null,
                Ratio = row.Ratio, Lower95 = row.Lower95, Upper95 = row.Upper95, Status = row.Status, Reason = row.StatusReason,
            });
            foreach (var lane in new[] { "FiniteLive", "FundedFiniteLive", "FiniteDeadlineLive" })
                summary.AddRow(lane, id, new()
                {
                    Source = source, Operation = row.Operation, ManagedUs = Median(row.Lanes[lane].Microseconds),
                    ManagedBytes = Median(row.Lanes[lane].AllocatedBytes), Status = row.Status == "Unqualified" ? "Unqualified" : "Measured",
                    Ratio = Median(row.Lanes[lane].Microseconds) / Median(row.Lanes["Unlimited"].Microseconds),
                    Reason = lane == "FundedFiniteLive" ? $"Peak covered host working storage {row.PeakFundedWorkingBytes} bytes; zero outstanding loans." : null,
                });
        }
        summary.Write(repository);
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
        text.AppendLine("Warm .NET 11 public operations over prepared UTF-8; compilation and input preparation are excluded. Times are per catalog operation; sku007 performs 4,096 independent Match calls with one reused caller capture span. Native PCRE2 NFA uses a prepared reusable match buffer, UTF validation and equivalent byte/UTF-16 result projection. Its ratios apply only to verified equal-result inputs, not general POSIX semantics. Native allocations below cover CLR traffic, excluding native workspace.").AppendLine();
        text.AppendLine("| Case | Operation | Utf8Regex (µs) | Native NFA (µs) | Managed B/op | Status | Ratio (lower is better) |");
        text.AppendLine("|---|---|---:|---:|---:|---|---:|");
        foreach (var (id, row) in summary.Sections["Operations"].OrderByDescending(pair => pair.Value.DisplayRatio ?? double.NegativeInfinity).ThenBy(pair => pair.Key, StringComparer.Ordinal))
            text.AppendLine($"| `{id}` | {row.Operation} | {Number(row.ManagedUs)} | {Number(row.ReferenceUs)} | {row.ManagedBytes?.ToString("F0", CultureInfo.InvariantCulture)} | {row.DisplayStatus} | {Number(row.DisplayRatio)} |");
        text.AppendLine().AppendLine("The ratio is the median paired managed/native time; raw samples retain 95% bootstrap intervals and lane-order observations. Excluded rows have different native ranges/captures and no ratio. All rows retain ordinary costs, even when no equivalent native result exists.").AppendLine();
        text.AppendLine("| Case | Finite + live token | Funded finite + live token | Finite + deadline + live token | Funded / unlimited |");
        text.AppendLine("|---|---:|---:|---:|---:|");
        foreach (var (id, funded) in summary.Sections["FundedFiniteLive"].OrderByDescending(pair => pair.Value.Ratio).ThenBy(pair => pair.Key, StringComparer.Ordinal))
            text.AppendLine($"| `{id}` | {Number(summary.Sections["FiniteLive"][id].ManagedUs)} µs | {Number(funded.ManagedUs)} µs | {Number(summary.Sections["FiniteDeadlineLive"][id].ManagedUs)} µs | {Number(funded.Ratio)} |");
        text.AppendLine().AppendLine("Finite lanes enforce work/depth/working limits and a positive result limit under an original live token. Funded lanes add a thread-safe 16 MiB host allowance; the deadline lane adds a 30-second invocation timeout. Values/caller-owned capture destinations create no producer result payload. These are absolute public-operation costs, including useful metering; the unlimited native comparator has no corresponding host policy.");
        text.AppendLine().AppendLine($"[Compact summary](../../bench/Snapshots/Posix.json). Snapshot SHA-256: `{summary.Evidence.SnapshotSha256}`.");
        return text.ToString();
        static string Number(double? value) => value?.ToString("F2", CultureInfo.InvariantCulture) ?? "—";
    }

    private static void VerifyPosixEvidence()
    {
        var snapshot = ReadPosixSnapshot();
        var current = CaptureBenchmarkEnvironment();
        if (snapshot.SchemaVersion != 1 || snapshot.CatalogSha256 != PosixCatalogHash()) throw new InvalidOperationException("POSIX benchmark catalog changed.");
        foreach (var row in PosixBenchmarkCatalog.Cases)
        {
            if (!snapshot.Cases.TryGetValue(row.Id, out var evidence) || evidence.Status == "Unqualified" ||
                JsonSerializer.Serialize(evidence.Expected) != JsonSerializer.Serialize(PosixResultJson(PosixBenchmarkCatalog.Expected(row.Id))) ||
                evidence.InputSha256 != Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(row.Input))) ||
                evidence.Operation != row.Operation.ToString() || evidence.Records != row.Records || evidence.StartOffsetInBytes != row.StartOffsetInBytes ||
                evidence.Lanes.Values.Any(lane => lane.Microseconds.Count < 9 || lane.Milliseconds.Min() < 20))
                throw new InvalidOperationException($"POSIX benchmark evidence is incomplete: {row.Id}");
            if (evidence.Environment.CoreAssembly != current.CoreAssembly || evidence.Environment.BenchmarkAssembly != current.BenchmarkAssembly ||
                evidence.Environment.Runtime != RuntimeInformation.FrameworkDescription ||
                evidence.PosixAssembly != GetAssemblyIdentity(typeof(Utf8PosixRegex).Assembly))
                throw new InvalidOperationException($"POSIX benchmark evidence does not identify this loaded source/runtime: {row.Id}");
        }
        RewritePosixMarkdown(verify: true);
        Console.WriteLine("POSIX catalog, result contracts, durations and loaded identities verified.");
    }

    private sealed class PosixBenchmarkSnapshot
    {
        public int SchemaVersion { get; set; } = 1;
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
