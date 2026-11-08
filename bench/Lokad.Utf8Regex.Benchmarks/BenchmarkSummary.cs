using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Lokad.Utf8Regex.Benchmarks;

// Presentation data only. Raw qualification still runs against the full snapshots.
internal sealed class BenchmarkSummary
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public int SchemaVersion { get; init; } = 1;
    public required string Profile { get; init; }
    public required BenchmarkSummaryEvidence Evidence { get; init; }
    public string? CatalogSha256 { get; init; }
    public BenchmarkSummaryComparator? Comparator { get; init; }
    public SortedDictionary<string, JsonElement> Sources { get; init; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, SortedDictionary<string, BenchmarkSummaryRow>> Sections { get; init; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, BenchmarkSummaryLifecycle> Lifecycle { get; init; } = new(StringComparer.Ordinal);
    public SortedDictionary<string, BenchmarkSummaryScaling> Scaling { get; init; } = new(StringComparer.Ordinal);

    internal static string RawPath(string repository, string fileName) =>
        Path.Combine(repository, "artifacts", "benchmark-refresh", "current", fileName);

    internal static string SummaryPath(string repository, string profile) =>
        Path.Combine(repository, "bench", "Snapshots", profile + ".json");

    internal static BenchmarkSummary Read(string repository, string profile)
    {
        var path = SummaryPath(repository, profile);
        var summary = JsonSerializer.Deserialize<BenchmarkSummary>(File.ReadAllText(path, Encoding.UTF8), JsonOptions);
        return summary is { SchemaVersion: 1 } && summary.Profile == profile
            ? summary : throw new InvalidOperationException($"Invalid {profile} benchmark summary: {path}");
    }

    internal void Write(string repository)
    {
        var path = SummaryPath(repository, Profile);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        BenchmarkFileWriter.WriteTextAtomically(path, JsonSerializer.Serialize(this, JsonOptions) + "\n");
    }

    internal string AddSource(object? source)
    {
        var element = JsonSerializer.SerializeToElement(source);
        var id = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(element.GetRawText())));
        Sources[id] = element;
        return id;
    }

    internal void AddRow(string section, string caseId, BenchmarkSummaryRow row)
    {
        if (!Sections.TryGetValue(section, out var rows))
        {
            Sections[section] = rows = new(StringComparer.Ordinal);
        }
        rows[caseId] = row;
    }

    internal static BenchmarkSummaryEvidence Archive(string repository, string snapshotName)
    {
        var path = RawPath(repository, snapshotName);
        var bytes = Encoding.UTF8.GetBytes(File.ReadAllText(path, Encoding.UTF8).ReplaceLineEndings("\n"));
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var asset = Path.GetFileNameWithoutExtension(snapshotName) + "." + hash.ToLowerInvariant() + ".json";
        var evidenceDirectory = Path.Combine(repository, "artifacts", "benchmark-refresh", "evidence");
        Directory.CreateDirectory(evidenceDirectory);
        var destination = Path.Combine(evidenceDirectory, asset);
        if (File.Exists(destination))
        {
            if (!File.ReadAllBytes(destination).AsSpan().SequenceEqual(bytes))
                throw new InvalidOperationException($"Immutable benchmark evidence differs: {destination}");
        }
        else
        {
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
            output.Write(bytes);
        }
        return new()
        {
            Snapshot = snapshotName,
            SnapshotSha256 = hash,
            Bytes = bytes.Length,
            Asset = asset,
            Url = "https://github.com/Lokad/Utf8Regex/releases/download/benchmark-evidence/" + asset,
        };
    }
}

internal sealed class BenchmarkSummaryEvidence
{
    public required string Snapshot { get; init; }
    public required string SnapshotSha256 { get; init; }
    public long Bytes { get; init; }
    public required string Asset { get; init; }
    public required string Url { get; init; }
}

internal sealed class BenchmarkSummaryRow
{
    internal double? DisplayRatio => Status is "Excluded" or "Unqualified" ? null : Ratio;
    internal string DisplayStatus => Status switch
    {
        "ManagedFaster" => "Managed faster",
        "CpythonFaster" => "CPython faster",
        "NativeFaster" => "Native faster",
        _ => Status,
    };

    public string? Operation { get; init; }
    public required string Source { get; init; }
    public string? InputSha256 { get; init; }
    public string? ResultContract { get; init; }
    public double ManagedUs { get; init; }
    public double? ReferenceUs { get; init; }
    public double? DecodeUs { get; init; }
    public double? Ratio { get; init; }
    public double? Lower95 { get; init; }
    public double? Upper95 { get; init; }
    public required string Status { get; init; }
    public string? Reason { get; init; }
    public double? ManagedBytes { get; init; }
    public double? ReferenceBytes { get; init; }
    public double? DecodeBytes { get; init; }
}

internal sealed class BenchmarkSummaryComparator
{
    public required string PackageId { get; init; }
    public required string PackageVersion { get; init; }
    public required string Engine { get; init; }
    public required string License { get; init; }
    public string? BuildFingerprintSha256 { get; init; }
}

internal sealed class BenchmarkSummaryLifecycle
{
    public required string Source { get; init; }
    public required BenchmarkSummaryTiming Managed { get; init; }
    public required BenchmarkSummaryTiming Reference { get; init; }
    public required BenchmarkSummaryTiming ManagedFirst { get; init; }
    public required BenchmarkSummaryTiming ReferenceFirst { get; init; }
}

internal sealed class BenchmarkSummaryTiming
{
    public double Microseconds { get; init; }
    public long? AllocatedBytes { get; init; }
}

internal sealed class BenchmarkSummaryScaling
{
    public string? Source { get; init; }
    public required string Operation { get; init; }
    public string? Dimension { get; init; }
    public int Points { get; init; }
    public string? FitGate { get; init; }
    public string? FitGateReason { get; init; }
}
