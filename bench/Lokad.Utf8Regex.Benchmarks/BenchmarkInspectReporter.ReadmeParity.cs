using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    private const string ReadmeParityReportFileName = "README.Parity.json";

    public static int RunEmitReadmeParityReport()
    {
        var snapshotPath = FindRepoFile(ReadmeBenchmarkSnapshotFileName);
        var snapshot = LoadReadmeBenchmarkSnapshot();
        Console.Write(SerializeReadmeParityReport(snapshot, snapshotPath));
        return 0;
    }

    public static int RunVerifyReadmeParityReport()
    {
        var snapshotPath = FindRepoFile(ReadmeBenchmarkSnapshotFileName);
        var expected = SerializeReadmeParityReport(LoadReadmeBenchmarkSnapshot(), snapshotPath);
        var reportPath = FindRepoFile(ReadmeParityReportFileName);
        var actual = File.ReadAllText(reportPath, Encoding.UTF8);
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{ReadmeParityReportFileName} is stale. Refresh a README benchmark case or section from a clean source revision.");
        }

        Console.WriteLine($"Verified {ReadmeParityReportFileName} against {ReadmeBenchmarkSnapshotFileName}.");
        return 0;
    }

    private static void WriteReadmeParityReport(ReadmeBenchmarkSnapshot snapshot, string snapshotPath)
    {
        var reportPath = Path.Combine(Path.GetDirectoryName(snapshotPath)!, ReadmeParityReportFileName);
        BenchmarkFileWriter.WriteTextAtomically(reportPath, SerializeReadmeParityReport(snapshot, snapshotPath));
    }

    private static string SerializeReadmeParityReport(ReadmeBenchmarkSnapshot snapshot, string snapshotPath)
    {
        var snapshotHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(snapshotPath)));
        var report = BuildReadmeParityReport(snapshot, snapshotHash);
        return JsonSerializer.Serialize(report, ReadmeBenchmarkSnapshotJsonOptions) + Environment.NewLine;
    }

    private static ReadmeParityReport BuildReadmeParityReport(
        ReadmeBenchmarkSnapshot snapshot,
        string snapshotHash)
    {
        var rows = new List<ReadmeParityRow>();
        foreach (var section in ParseReadmeSections(null))
        {
            var sectionSnapshot = GetRequiredSnapshotSection(snapshot, section);
            foreach (var caseId in GetReadmeCaseIds(section))
            {
                rows.Add(CreateReadmeParityRow(
                    section,
                    caseId,
                    GetRequiredSnapshotMeasurement(sectionSnapshot, caseId),
                    snapshot.SchemaVersion >= 4));
            }
        }

        return new ReadmeParityReport
        {
            SchemaVersion = 1,
            GeneratedFrom = ReadmeBenchmarkSnapshotFileName,
            SnapshotSha256 = snapshotHash,
            Summary = new ReadmeParitySummary
            {
                Rows = rows.Count,
                Wins = rows.Count(static row => row.Status == "Win"),
                TieCandidates = rows.Count(static row => row.Status == "TieCandidate"),
                Gaps = rows.Count(static row => row.Status == "Gap"),
                Unqualified = rows.Count(static row => row.Status == "Unqualified"),
            },
            Rows = rows,
        };
    }

    private static IEnumerable<string> GetReadmeCaseIds(ReadmeBenchmarkSection section)
    {
        if (section is ReadmeBenchmarkSection.DotNetPerformance or ReadmeBenchmarkSection.DotNetPerformanceCompiled)
        {
            foreach (var benchmarkCase in ReplicaCountBenchmarkCase.GetAll(ReplicaBenchmarkSource.DotNetPerformance))
            {
                yield return benchmarkCase.Id;
            }

            foreach (var caseId in LokadPublicBenchmarkContext.GetAllCaseIds())
            {
                yield return caseId;
            }

            yield break;
        }

        foreach (var benchmarkCase in ReplicaCountBenchmarkCase.GetAll(ReplicaBenchmarkSource.Lokad))
        {
            yield return benchmarkCase.Id;
        }
    }

    private static ReadmeParityRow CreateReadmeParityRow(
        ReadmeBenchmarkSection section,
        string caseId,
        ReadmeCaseMeasurementJson measurement,
        bool snapshotHasAllocations)
    {
        var compiled = section is ReadmeBenchmarkSection.DotNetPerformanceCompiled or ReadmeBenchmarkSection.LokadCompiled;
        var utf8Microseconds = compiled ? measurement.Utf8Compiled : measurement.Utf8Regex;
        var predecodedMicroseconds = compiled ? measurement.CompiledRegex : measurement.PredecodedRegex;
        var decodeMicroseconds = compiled ? measurement.DecodeThenCompiledRegex : measurement.DecodeThenRegex;
        var utf8AllocatedBytes = compiled ? measurement.Utf8CompiledAllocatedBytes : measurement.Utf8RegexAllocatedBytes;
        var predecodedAllocatedBytes = compiled ? measurement.CompiledRegexAllocatedBytes : measurement.PredecodedRegexAllocatedBytes;
        var decodeAllocatedBytes = compiled ? measurement.DecodeThenCompiledRegexAllocatedBytes : measurement.DecodeThenRegexAllocatedBytes;
        var qualified = snapshotHasAllocations &&
            measurement.Environment is { TrackedDirty: false } &&
            measurement.RequestedIterations > 0 &&
            measurement.EffectiveIterations >= measurement.RequestedIterations &&
            measurement.Samples >= 5 &&
            utf8Microseconds > 0 &&
            predecodedMicroseconds > 0 &&
            decodeMicroseconds > 0 &&
            utf8AllocatedBytes >= 0 &&
            predecodedAllocatedBytes >= 0 &&
            decodeAllocatedBytes >= 0;
        var ratioToDecode = qualified ? utf8Microseconds / decodeMicroseconds : (double?)null;
        var ratioToPredecoded = qualified ? utf8Microseconds / predecodedMicroseconds : (double?)null;
        var status = !qualified
            ? "Unqualified"
            : ratioToDecode <= 0.98
                ? "Win"
                : ratioToDecode <= 1.02
                    ? "TieCandidate"
                    : "Gap";

        return new ReadmeParityRow
        {
            Section = GetReadmeSectionToken(section),
            CaseId = caseId,
            Operation = GetReadmeOperation(caseId),
            Mode = compiled ? "Compiled" : "Ordinary",
            Status = status,
            Utf8Microseconds = utf8Microseconds,
            PredecodedRegexMicroseconds = predecodedMicroseconds,
            DecodeThenRegexMicroseconds = decodeMicroseconds,
            RatioToDecode = ratioToDecode,
            RatioToPredecoded = ratioToPredecoded,
            Utf8AllocatedBytes = utf8AllocatedBytes,
            PredecodedRegexAllocatedBytes = predecodedAllocatedBytes,
            DecodeThenRegexAllocatedBytes = decodeAllocatedBytes,
            RequestedIterations = measurement.RequestedIterations,
            EffectiveIterations = measurement.EffectiveIterations,
            Samples = measurement.Samples,
            MeasuredAtUtc = measurement.MeasuredAtUtc,
            Environment = measurement.Environment,
        };
    }

    private static string GetReadmeOperation(string caseId)
        => LokadPublicBenchmarkContext.GetAllCaseIds().Contains(caseId, StringComparer.Ordinal)
            ? new LokadPublicBenchmarkContext(caseId).Operation.ToString()
            : "Count";

    private sealed class ReadmeParityReport
    {
        public int SchemaVersion { get; set; }

        public required string GeneratedFrom { get; set; }

        public required string SnapshotSha256 { get; set; }

        public required ReadmeParitySummary Summary { get; set; }

        public required IReadOnlyList<ReadmeParityRow> Rows { get; set; }
    }

    private sealed class ReadmeParitySummary
    {
        public int Rows { get; set; }

        public int Wins { get; set; }

        public int TieCandidates { get; set; }

        public int Gaps { get; set; }

        public int Unqualified { get; set; }
    }

    private sealed class ReadmeParityRow
    {
        public required string Section { get; set; }

        public required string CaseId { get; set; }

        public required string Operation { get; set; }

        public required string Mode { get; set; }

        public required string Status { get; set; }

        public double Utf8Microseconds { get; set; }

        public double PredecodedRegexMicroseconds { get; set; }

        public double DecodeThenRegexMicroseconds { get; set; }

        public double? RatioToDecode { get; set; }

        public double? RatioToPredecoded { get; set; }

        public double Utf8AllocatedBytes { get; set; }

        public double PredecodedRegexAllocatedBytes { get; set; }

        public double DecodeThenRegexAllocatedBytes { get; set; }

        public int RequestedIterations { get; set; }

        public int EffectiveIterations { get; set; }

        public int Samples { get; set; }

        public DateTimeOffset? MeasuredAtUtc { get; set; }

        public BenchmarkEnvironmentJson? Environment { get; set; }
    }
}
