using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private const string PythonReSemanticDigestAlgorithm = "structured-u64-byte-ranges-v2";
    private static string ComputePythonReManagedProductSha256()
    {
        var repositoryRoot = Path.GetDirectoryName(FindRepositoryFile("README.md")) ??
            throw new InvalidOperationException("Could not locate the repository root.");
        var files = new List<string>
        {
            Path.Combine(repositoryRoot, "Directory.Build.props"),
        };
        AddProductFiles("src/Lokad.Utf8Regex");
        AddProductFiles("src/Lokad.Utf8Regex.PythonRe");

        var manifest = new StringBuilder();
        foreach (var path in files.OrderBy(static path => path, StringComparer.Ordinal))
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            manifest.Append(relativePath);
            manifest.Append('\0');
            manifest.Append(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
            manifest.Append('\n');
        }

        return ComputePythonReSha256(manifest.ToString());

        void AddProductFiles(string relativeDirectory)
        {
            var directory = Path.Combine(repositoryRoot, relativeDirectory);
            files.AddRange(Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(static path =>
                    !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) &&
                    !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)));
            files.AddRange(Directory.EnumerateFiles(directory, "*.csproj", SearchOption.TopDirectoryOnly));
        }
    }

    // Bump only the affected value when its timed managed call, result consumption,
    // projection, or semantic-digest path changes. The exhaustive switch makes a
    // newly added operation declare an identity without perturbing existing rows.
    private static string ComputePythonReManagedOperationProtocolSha256(
        PythonReBenchmarkOperation operation) => ComputePythonReSha256(operation switch
        {
            PythonReBenchmarkOperation.IsMatch => "managed-ismatch-scalar-v1",
            PythonReBenchmarkOperation.Search => "managed-search-consumed-group-zero-v1",
            PythonReBenchmarkOperation.SearchFromOffset => "managed-search-from-offset-consumed-group-zero-v1",
            PythonReBenchmarkOperation.Match => "managed-match-consumed-group-zero-v1",
            PythonReBenchmarkOperation.FullMatch => "managed-fullmatch-consumed-group-zero-v1",
            PythonReBenchmarkOperation.SearchDetailed => "managed-search-context-host-captures-v2",
            PythonReBenchmarkOperation.Count => "managed-count-scalar-v1",
            PythonReBenchmarkOperation.FindAllStrings => "managed-findall-utf8-host-strings-eager-v3",
            PythonReBenchmarkOperation.FindAllUtf8 => "managed-findall-utf8-eager-v1",
            PythonReBenchmarkOperation.FindIterDetailed => "managed-range-cursor-host-captures-v2",
            PythonReBenchmarkOperation.ReplaceString => "managed-replace-bytes-host-text-eager-v3",
            PythonReBenchmarkOperation.ReplaceUtf8 => "managed-replace-utf8-eager-v1",
            PythonReBenchmarkOperation.SubnString => "managed-subn-bytes-host-text-eager-v3",
            PythonReBenchmarkOperation.SubnUtf8 => "managed-subn-utf8-eager-v1",
            PythonReBenchmarkOperation.SubnEvaluatorString => "managed-range-cursor-host-subn-text-untimed-verification-v3",
            PythonReBenchmarkOperation.SubnEvaluatorUtf8 => "managed-range-cursor-host-subn-bytes-untimed-verification-v3",
            PythonReBenchmarkOperation.SplitStrings => "managed-split-ranges-host-strings-eager-v3",
            PythonReBenchmarkOperation.CountFromOffset => "managed-count-from-offset-scalar-v1",
            PythonReBenchmarkOperation.FindAllStructural => "managed-host-range-cursor-group-zero-structural-v2",
            PythonReBenchmarkOperation.ReplaceStringLimited => "managed-replace-bytes-host-text-limited-eager-v3",
            PythonReBenchmarkOperation.ReplaceEvaluatorString => "managed-range-cursor-host-replace-text-untimed-verification-v3",
            PythonReBenchmarkOperation.SplitStringsLimited =>
                $"managed-split-ranges-host-strings-limited-eager-v3;warmup={PythonReQualificationSplitWarmupCalls}",
            PythonReBenchmarkOperation.SplitDetailed => "managed-split-ranges-host-detailed-eager-v3",
            PythonReBenchmarkOperation.FindAllStringsFromOffset => "managed-findall-utf8-host-strings-from-offset-eager-v3",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        });

    // Keep these values synchronized with the matching operation branches in the
    // streaming runner. Shared timer/process/digest changes belong in the shared
    // identity below instead of every operation value.
    private static string ComputePythonReCpythonOperationProtocolSha256(
        PythonReBenchmarkOperation operation) => ComputePythonReSha256(operation switch
        {
            PythonReBenchmarkOperation.IsMatch => "cpython-pattern-search-bool-v1",
            PythonReBenchmarkOperation.Search => "cpython-pattern-search-group-zero-v1",
            PythonReBenchmarkOperation.SearchFromOffset => "cpython-pattern-search-from-offset-group-zero-v1",
            PythonReBenchmarkOperation.Match => "cpython-pattern-match-group-zero-v1",
            PythonReBenchmarkOperation.FullMatch => "cpython-pattern-fullmatch-group-zero-v1",
            PythonReBenchmarkOperation.SearchDetailed => "cpython-pattern-search-detailed-v1",
            PythonReBenchmarkOperation.Count => "cpython-finditer-sum-v1",
            PythonReBenchmarkOperation.FindAllStrings => "cpython-pattern-findall-strings-v1",
            PythonReBenchmarkOperation.FindAllUtf8 => "cpython-pattern-findall-utf8-projection-v1",
            PythonReBenchmarkOperation.FindIterDetailed => "cpython-finditer-detailed-v1",
            PythonReBenchmarkOperation.ReplaceString => "cpython-pattern-sub-string-v1",
            PythonReBenchmarkOperation.ReplaceUtf8 => "cpython-pattern-sub-utf8-projection-v1",
            PythonReBenchmarkOperation.SubnString => "cpython-pattern-subn-string-v1",
            PythonReBenchmarkOperation.SubnUtf8 => "cpython-pattern-subn-utf8-projection-v1",
            PythonReBenchmarkOperation.SubnEvaluatorString => "cpython-pattern-subn-callback-string-untimed-verification-v2",
            PythonReBenchmarkOperation.SubnEvaluatorUtf8 => "cpython-pattern-subn-callback-utf8-untimed-verification-v2",
            PythonReBenchmarkOperation.SplitStrings => "cpython-pattern-split-strings-v1",
            PythonReBenchmarkOperation.CountFromOffset => "cpython-finditer-from-offset-sum-v1",
            PythonReBenchmarkOperation.FindAllStructural => "cpython-finditer-group-zero-structural-v1",
            PythonReBenchmarkOperation.ReplaceStringLimited => "cpython-pattern-sub-limited-string-v1",
            PythonReBenchmarkOperation.ReplaceEvaluatorString => "cpython-pattern-sub-callback-string-untimed-verification-v2",
            PythonReBenchmarkOperation.SplitStringsLimited => "cpython-pattern-split-limited-strings-v1",
            PythonReBenchmarkOperation.SplitDetailed => "cpython-pattern-split-detailed-projection-v1",
            PythonReBenchmarkOperation.FindAllStringsFromOffset => "cpython-pattern-findall-strings-from-offset-v1",
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        });

    private static string ComputePythonReSharedProtocolSha256() => ComputePythonReSha256(
        string.Join(
            '\n',
            $"qualification-protocol={PythonReQualificationProtocolVersion}",
            $"cpython-stream-protocol={CpythonProtocolVersion}",
            $"processor-policy={PythonReQualificationProcessorPolicy}",
            $"linux-processor-policy={BenchmarkProcessorScope.LinuxSingleProcessorPolicy}",
            $"bootstrap-seed={PythonReQualificationBootstrapSeed}",
            $"bootstrap-resamples={PythonReQualificationBootstrapResamples}",
            $"maximum-iterations={PythonReQualificationMaximumIterations}",
            $"one-shot-warmup={PythonReQualificationOneShotWarmupCalls}",
            $"exact-utf8-one-shot-warmup={PythonReQualificationExactUtf8OneShotWarmupCalls}",
            $"findall-warmup={PythonReQualificationFindAllWarmupCalls}",
            $"short-findall-warmup={PythonReQualificationShortFindAllWarmupCalls}",
            $"short-findall-calibration={PythonReQualificationShortFindAllCalibrationIterations}",
            $"replacement-warmup={PythonReQualificationReplacementWarmupCalls}",
            $"short-one-shot-minimum={PythonReQualificationShortOneShotMinimumIterations}",
            $"short-one-shot-warmup={PythonReQualificationShortOneShotWarmupCalls}",
            $"short-one-shot-calibration={PythonReQualificationShortOneShotCalibrationIterations}",
            $"minimum-warmup={PythonReQualificationMinimumWarmupCalls}",
            $"target-sample-ms={PythonReQualificationTargetSampleMilliseconds.ToString("R", CultureInfo.InvariantCulture)}",
            $"pilot-ms={PythonReQualificationPilotMilliseconds.ToString("R", CultureInfo.InvariantCulture)}",
            $"minimum-sample-ms={PythonReQualificationMinimumSampleMilliseconds.ToString("R", CultureInfo.InvariantCulture)}",
            $"maximum-spread={PythonReQualificationMaximumSpread.ToString("R", CultureInfo.InvariantCulture)}",
            $"response-timeout-ms={s_cpythonResponseTimeout.TotalMilliseconds.ToString("R", CultureInfo.InvariantCulture)}",
            $"semantic-digest={PythonReSemanticDigestAlgorithm}",
            "pair-order=alternating-v1",
            "status-boundaries=0.98-1.02-bootstrap95-order1.02-spread1.10-floor0.10-v1"));

    private static string GetPythonReResultContract(PythonReBenchmarkCase benchmarkCase) =>
        benchmarkCase.Operation is PythonReBenchmarkOperation.Search or
            PythonReBenchmarkOperation.SearchFromOffset or
            PythonReBenchmarkOperation.Match or PythonReBenchmarkOperation.FullMatch
            ? "ConsumedGroupZeroRanges"
            : benchmarkCase.IncludesResultMaterialization
                ? "EagerMaterializedResult"
                : "ScalarResult";

    private static bool PythonReManagedRuntimeMatches(
        PythonReBenchmarkEnvironment? stored,
        PythonReBenchmarkEnvironment current) =>
        stored is not null &&
        string.Equals(stored.Runtime, current.Runtime, StringComparison.Ordinal) &&
        string.Equals(stored.OperatingSystem, current.OperatingSystem, StringComparison.Ordinal) &&
        string.Equals(stored.Processor, current.Processor, StringComparison.Ordinal);

    private static bool PythonReInterpreterMatches(
        CpythonStreamEnvironment? stored,
        CpythonStreamEnvironment current) =>
        stored is not null && stored.Git is not null && stored.Timer is not null &&
        string.Equals(stored.VersionDetail, current.VersionDetail, StringComparison.Ordinal) &&
        stored.Git.SequenceEqual(current.Git, StringComparer.Ordinal) &&
        string.Equals(stored.Compiler, current.Compiler, StringComparison.Ordinal) &&
        string.Equals(stored.SoAbi, current.SoAbi, StringComparison.Ordinal) &&
        stored.DebugBuild == current.DebugBuild &&
        stored.GilEnabled == current.GilEnabled &&
        string.Equals(stored.ExecutableSha256, current.ExecutableSha256, StringComparison.Ordinal) &&
        string.Equals(stored.RuntimeLibrarySha256, current.RuntimeLibrarySha256, StringComparison.Ordinal) &&
        string.Equals(stored.Platform, current.Platform, StringComparison.Ordinal) &&
        string.Equals(stored.Architecture, current.Architecture, StringComparison.Ordinal) &&
        string.Equals(stored.Timer.Implementation, current.Timer.Implementation, StringComparison.Ordinal) &&
        stored.Timer.ResolutionSeconds.Equals(current.Timer.ResolutionSeconds) &&
        stored.Timer.Monotonic == current.Timer.Monotonic &&
        stored.Timer.Adjustable == current.Timer.Adjustable;
}
