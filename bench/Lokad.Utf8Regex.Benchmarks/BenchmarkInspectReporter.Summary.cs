using System.Text;
using System.Text.Json;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private static string GetReadmeSnapshotPath() =>
        BenchmarkSummary.RawPath(Path.GetDirectoryName(FindRepoFile("README.md"))!, ReadmeBenchmarkSnapshotFileName);

    private static string GetPcre2SnapshotPath() =>
        BenchmarkSummary.RawPath(Path.GetDirectoryName(FindRepoFile("README.md"))!, Pcre2BenchmarkSnapshotFileName);

    public static void ExportReadmeBenchmarkSummary() =>
        WriteReadmeBenchmarkSummary(LoadReadmeBenchmarkSnapshot());

    private static void WriteReadmeBenchmarkSummary(ReadmeBenchmarkSnapshot snapshot)
    {
        var repository = Path.GetDirectoryName(FindRepoFile("README.md"))!;
        var rawPath = BenchmarkSummary.RawPath(repository, Path.GetFileName(ReadmeBenchmarkSnapshotFileName));
        var summary = new BenchmarkSummary
        {
            Profile = "Core",
            Evidence = BenchmarkSummary.DescribeEvidence(repository, Path.GetFileName(ReadmeBenchmarkSnapshotFileName)),
        };
        foreach (var row in BuildReadmeParityReport(snapshot, BenchmarkSnapshotHash.HashNormalizedFile(rawPath)).Rows)
        {
            summary.AddRow(row.Section, row.CaseId, new()
            {
                Operation = row.Operation,
                Source = summary.AddSource(row.Environment),
                ManagedUs = row.Utf8Microseconds,
                ReferenceUs = row.PredecodedRegexMicroseconds,
                DecodeUs = row.DecodeThenRegexMicroseconds,
                Ratio = row.RatioToPredecoded,
                Status = row.Status,
                Reason = row.StatusReason,
                ManagedBytes = row.Utf8AllocatedBytes,
                ReferenceBytes = row.PredecodedRegexAllocatedBytes,
                DecodeBytes = row.DecodeThenRegexAllocatedBytes,
            });
        }
        summary.Write(repository);
    }

    // The existing renderer needs only lane values. This projection is never used
    // for raw qualification, which still requires its original samples/provenance.
    private static ReadmeBenchmarkSnapshot LoadReadmePresentationSnapshot()
    {
        var summary = BenchmarkSummary.Read(Path.GetDirectoryName(FindRepoFile("README.md"))!, "Core");
        var snapshot = new ReadmeBenchmarkSnapshot();
        foreach (var (section, rows) in summary.Sections)
        {
            var target = new ReadmeBenchmarkSectionJson();
            snapshot.Sections[section] = target;
            var compiled = section.EndsWith("-compiled", StringComparison.Ordinal);
            foreach (var (caseId, row) in rows)
            {
                target.Cases[caseId] = compiled ? new()
                {
                    Utf8Compiled = row.ManagedUs,
                    CompiledRegex = row.ReferenceUs ?? 0,
                    DecodeThenCompiledRegex = row.DecodeUs ?? 0,
                    Utf8CompiledAllocatedBytes = row.ManagedBytes ?? 0,
                    CompiledRegexAllocatedBytes = row.ReferenceBytes ?? 0,
                    DecodeThenCompiledRegexAllocatedBytes = row.DecodeBytes ?? 0,
                } : new()
                {
                    Utf8Regex = row.ManagedUs,
                    PredecodedRegex = row.ReferenceUs ?? 0,
                    DecodeThenRegex = row.DecodeUs ?? 0,
                    Utf8RegexAllocatedBytes = row.ManagedBytes ?? 0,
                    PredecodedRegexAllocatedBytes = row.ReferenceBytes ?? 0,
                    DecodeThenRegexAllocatedBytes = row.DecodeBytes ?? 0,
                };
            }
        }
        return snapshot;
    }

    public static void ExportPcre2BenchmarkSummary() =>
        WritePcre2BenchmarkSummary(LoadPcre2BenchmarkSnapshot());

    private static void WritePcre2BenchmarkSummary(Pcre2BenchmarkSnapshot snapshot)
    {
        var repository = Path.GetDirectoryName(FindRepoFile("README.md"))!;
        var dependency = snapshot.PcreNetNativeBaseline;
        var summary = new BenchmarkSummary
        {
            Profile = "Pcre2",
            Evidence = BenchmarkSummary.DescribeEvidence(repository, Path.GetFileName(Pcre2BenchmarkSnapshotFileName)),
            Comparator = dependency is null ? null : new()
            {
                PackageId = dependency.PackageId,
                PackageVersion = dependency.PackageVersion,
                Engine = dependency.NativePcre2Version,
                License = dependency.License,
                BuildFingerprintSha256 = dependency.BuildFingerprint?.Sha256,
            },
        };
        foreach (var (section, data) in snapshot.Sections)
        {
            foreach (var (caseId, row) in data.Cases)
            {
                var pair = row.PcreNetNativePair;
                summary.AddRow(section, caseId, new()
                {
                    Operation = pair?.Operation ?? section[(section.LastIndexOf('-') + 1)..],
                    Source = summary.AddSource(pair?.Environment ?? row.Environment),
                    ManagedUs = pair?.ManagedMedianMicroseconds ?? row.Utf8Pcre2,
                    ReferenceUs = pair?.ComparatorMedianMicroseconds ?? row.PcreNetNative,
                    Ratio = pair?.RatioMedian,
                    Lower95 = pair?.RatioLower95,
                    Upper95 = pair?.RatioUpper95,
                    Status = row.PcreNetNativeStatus.ToString(),
                    Reason = row.PcreNetNativeStatusReason ?? row.PcreNetNativeUnavailableReason,
                    ManagedBytes = pair?.ManagedAllocatedBytesPerOperation ?? row.WarmAllocatedBytes,
                    ReferenceBytes = pair?.ComparatorManagedAllocatedBytesPerOperation,
                });
            }
        }
        foreach (var (id, family) in snapshot.ScalingFamilies)
        {
            summary.Scaling[id] = new()
            {
                Operation = family.Operation,
                Points = family.Points.Count,
            };
        }
        summary.Write(repository);
    }
}

internal static partial class PythonReBenchmarkReporter
{
    private static string GetPythonReSnapshotPath() =>
        BenchmarkSummary.RawPath(Path.GetDirectoryName(FindRepositoryFile("README.md"))!, SnapshotFileName);

    private static PythonReBenchmarkSnapshot LoadPythonReBenchmarkSnapshot()
    {
        var snapshot = JsonSerializer.Deserialize<PythonReBenchmarkSnapshot>(
            File.ReadAllText(GetPythonReSnapshotPath(), Encoding.UTF8));
        return snapshot is { SchemaVersion: PythonReBenchmarkSchemaVersion }
            ? snapshot
            : throw new InvalidOperationException($"{SnapshotFileName} requires schema {PythonReBenchmarkSchemaVersion}.");
    }

    public static void ExportBenchmarkSummary() =>
        WriteBenchmarkSummary(LoadPythonReBenchmarkSnapshot());

    private static void WriteBenchmarkSummary(PythonReBenchmarkSnapshot snapshot)
    {
        var repository = Path.GetDirectoryName(FindRepositoryFile("README.md"))!;
        var summary = new BenchmarkSummary
        {
            Profile = "PythonRe",
            Evidence = BenchmarkSummary.DescribeEvidence(repository, Path.GetFileName(SnapshotFileName)),
            CatalogSha256 = snapshot.CatalogSha256,
        };
        foreach (var (caseId, row) in snapshot.Cases)
        {
            var pair = row.Qualification?.PairedEvidence;
            summary.AddRow(row.Coverage?.Section ?? "Results", caseId, new()
            {
                Operation = row.Operation,
                Source = summary.AddSource(new
                {
                    Managed = pair?.ManagedEnvironment ?? row.Environment,
                    CPython = (object?)pair?.CpythonEnvironment ?? row.Cpython?.Environment,
                    ProductSha256 = pair?.ManagedProductSha256,
                }),
                InputSha256 = row.InputSha256,
                ResultContract = pair?.ResultContract,
                ManagedUs = pair?.ManagedMedianMicroseconds ?? row.PythonRe.MedianMicroseconds,
                ReferenceUs = pair?.CpythonMedianMicroseconds ?? row.Cpython?.PredecodedRe.MedianMicroseconds,
                Ratio = pair?.StrongRatioMedian,
                Lower95 = pair?.StrongRatioLower95,
                Upper95 = pair?.StrongRatioUpper95,
                Status = row.Qualification?.Status ?? "Unqualified",
                Reason = row.Qualification?.StatusReason,
                ManagedBytes = pair?.ManagedMedianAllocatedBytes ?? row.PythonRe.MedianAllocatedBytes,
            });
        }
        foreach (var (id, row) in snapshot.Lifecycle)
        {
            summary.Lifecycle[id] = new()
            {
                Source = summary.AddSource(new { Managed = row.ManagedEnvironment, CPython = row.CpythonEnvironment }),
                Managed = Timing(row.AdapterConstruction),
                Reference = Timing(row.CpythonCompile),
                ManagedFirst = Timing(row.ConstructFirstSearch),
                ReferenceFirst = Timing(row.CpythonCompileFirstSearch),
            };
        }
        foreach (var (id, row) in snapshot.ScalingFamilies)
        {
            summary.Scaling[id] = new()
            {
                Source = summary.AddSource(new { Managed = row.ManagedEnvironment, CPython = row.CpythonEnvironment }),
                Operation = row.Operation,
                Dimension = row.Dimension,
                Points = row.Points.Count,
                FitGate = row.FitGate,
                FitGateReason = row.FitGateReason,
            };
        }
        summary.Write(repository);

        static BenchmarkSummaryTiming Timing(PythonReLifecycleTiming row) => new()
        {
            Microseconds = row.MedianMicroseconds,
            AllocatedBytes = row.ManagedAllocatedBytes,
        };
    }
}
