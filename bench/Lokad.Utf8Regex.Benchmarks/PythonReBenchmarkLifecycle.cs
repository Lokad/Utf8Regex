using System.Security.Cryptography;
using System.Text;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private static readonly PythonReLifecycleDefinition[] s_pythonReLifecycleDefinitions =
    [
        new("literal", "needle", "prefix needle suffix"),
        new("fallback", "header:[0-9]+", "header:12345"),
        new("captured", "(?P<word>[A-Za-z]+)-(?P=word)", "token-token"),
        new("unicode", "Шерлок Холмс", "prefix Шерлок Холмс suffix"),
    ];

    private static int RefreshPythonReLifecycle(int iterations, int samples)
    {
#if DEBUG
        Console.Error.WriteLine("PythonRe lifecycle refresh requires a Release build.");
        return 1;
#else
        using var processorScope = BenchmarkProcessorScope.EnterSingleHighestEfficiencyProcessor();
        using var worker = new CpythonStreamWorker();
        var snapshot = LoadPythonReBenchmarkSnapshot();
        var measurements = new SortedDictionary<string, PythonReLifecycleMeasurement>(StringComparer.Ordinal);
        foreach (var definition in s_pythonReLifecycleDefinitions)
        {
            Console.WriteLine($"Lifecycle          : {definition.Id}");
            measurements.Add(
                definition.Id,
                MeasurePythonReLifecycle(definition, iterations, samples, worker));
        }

        WriteSnapshot(new PythonReBenchmarkSnapshot
        {
            SchemaVersion = PythonReBenchmarkSchemaVersion,
            GeneratedAtUtc = DateTimeOffset.UtcNow,
            CatalogSha256 = snapshot.CatalogSha256,
            CatalogCaseIds = snapshot.CatalogCaseIds,
            Corpus = snapshot.Corpus,
            Cases = snapshot.Cases,
            Lifecycle = measurements,
            ScalingFamilies = snapshot.ScalingFamilies,
        });
        Console.WriteLine(
            $"Updated {measurements.Count} PythonRe lifecycle families on {processorScope.Policy}.");
        return 0;
#endif
    }

    private static PythonReLifecycleMeasurement MeasurePythonReLifecycle(
        PythonReLifecycleDefinition definition,
        int iterations,
        int samples,
        CpythonStreamWorker worker)
    {
        const PythonReCompileOptions options = PythonReCompileOptions.None;
        var parseResult = new PythonReParser(definition.Pattern).Parse(options);
        var translation = PythonReTranslator.Translate(parseResult);
        var inputBytes = Encoding.UTF8.GetBytes(definition.Input);
        if (!new Utf8PythonRegex(definition.Pattern, options).IsMatch(inputBytes))
        {
            throw new InvalidOperationException(
                $"PythonRe lifecycle '{definition.Id}' must retain a successful first-search preflight.");
        }

        var cpython = worker.MeasureLifecycle(
            definition.Pattern,
            definition.Input,
            (int)options,
            iterations,
            samples);
        if (cpython.Lifecycle is null || !cpython.Lifecycle.FirstSearchMatched)
        {
            throw new InvalidOperationException(
                $"CPython lifecycle '{definition.Id}' failed its first-search preflight.");
        }

        return new PythonReLifecycleMeasurement
        {
            Pattern = definition.Pattern,
            InputUtf8Bytes = inputBytes.Length,
            InputSha256 = Convert.ToHexString(SHA256.HashData(inputBytes)),
            Iterations = iterations,
            Samples = samples,
            ManagedEnvironment = CaptureEnvironment(),
            CpythonEnvironment = worker.Environment,
            ParseTranslate = ToLifecycleTiming(MeasureOperation(
                () =>
                {
                    var parsed = new PythonReParser(definition.Pattern).Parse(options);
                    return PythonReTranslator.Translate(parsed).Pattern.Length;
                },
                iterations,
                samples)),
            BackendCreation = ToLifecycleTiming(MeasureOperation(
                () => new Utf8Regex(
                    translation.Pattern,
                    translation.RegexOptions).GetGroupNumbers().Length,
                iterations,
                samples)),
            AdapterConstruction = ToLifecycleTiming(MeasureOperation(
                () => new Utf8PythonRegex(definition.Pattern, options).GetGroupNames().Length,
                iterations,
                samples)),
            ConstructFirstSearch = ToLifecycleTiming(MeasureOperation(
                () => new Utf8PythonRegex(definition.Pattern, options).IsMatch(inputBytes) ? 1 : 0,
                iterations,
                samples)),
            CpythonCompile = cpython.Lifecycle.Compile,
            CpythonCompileFirstSearch = cpython.Lifecycle.CompileFirstSearch,
        };
    }

    private static PythonReLifecycleTiming ToLifecycleTiming(PythonReOperationMeasurement measurement) => new()
    {
        MedianMicroseconds = measurement.MedianMicroseconds,
        MinimumMicroseconds = measurement.MinimumMicroseconds,
        MaximumMicroseconds = measurement.MaximumMicroseconds,
        ManagedAllocatedBytes = measurement.MedianAllocatedBytes,
        WarmupCalls = measurement.WarmupCalls,
        WarmupMilliseconds = measurement.WarmupMilliseconds,
    };
}

internal readonly record struct PythonReLifecycleDefinition(
    string Id,
    string Pattern,
    string Input);

internal sealed class PythonReLifecycleMeasurement
{
    public required string Pattern { get; init; }
    public required int InputUtf8Bytes { get; init; }
    public required string InputSha256 { get; init; }
    public required int Iterations { get; init; }
    public required int Samples { get; init; }
    public required PythonReBenchmarkEnvironment ManagedEnvironment { get; init; }
    public required CpythonStreamEnvironment CpythonEnvironment { get; init; }
    public required PythonReLifecycleTiming ParseTranslate { get; init; }
    public required PythonReLifecycleTiming BackendCreation { get; init; }
    public required PythonReLifecycleTiming AdapterConstruction { get; init; }
    public required PythonReLifecycleTiming ConstructFirstSearch { get; init; }
    public required PythonReLifecycleTiming CpythonCompile { get; init; }
    public required PythonReLifecycleTiming CpythonCompileFirstSearch { get; init; }
}

internal sealed class PythonReLifecycleTiming
{
    public double MedianMicroseconds { get; init; }
    public double MinimumMicroseconds { get; init; }
    public double MaximumMicroseconds { get; init; }
    public long? ManagedAllocatedBytes { get; init; }
    public int WarmupCalls { get; init; }
    public double WarmupMilliseconds { get; init; }
}

internal sealed class PythonReCpythonLifecycleResponse
{
    public bool FirstSearchMatched { get; init; }
    public PythonReLifecycleTiming Compile { get; init; } = new();
    public PythonReLifecycleTiming CompileFirstSearch { get; init; } = new();
}

internal sealed class PythonReScalingFamilyMeasurement
{
    public string Dimension { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string ResultContract { get; init; } = string.Empty;
    public int Samples { get; init; }
    public DateTimeOffset MeasuredAtUtc { get; init; }
    public PythonReBenchmarkEnvironment ManagedEnvironment { get; init; } = null!;
    public CpythonStreamEnvironment CpythonEnvironment { get; init; } = null!;
    public string CpuPolicy { get; init; } = string.Empty;
    public string CpuAffinityMask { get; init; } = string.Empty;
    public int? CpuEfficiencyClass { get; init; }
    public string ManagedRoute { get; init; } = string.Empty;
    public bool RouteStable { get; init; }
    public double ManagedSlopePerScaleUnit { get; init; }
    public double CpythonSlopePerScaleUnit { get; init; }
    public double ManagedMaximumRelativeResidual { get; init; }
    public double CpythonMaximumRelativeResidual { get; init; }
    public double ManagedMaximumSpread { get; init; }
    public double CpythonMaximumSpread { get; init; }
    public double MaximumOrderEffect { get; init; }
    public double MinimumLaneElapsedMilliseconds { get; init; }
    public string FitGate { get; init; } = string.Empty;
    public string FitGateReason { get; init; } = string.Empty;
    public List<PythonReScalingPointMeasurement> Points { get; init; } = [];
}

internal sealed class PythonReScalingPointMeasurement
{
    public string Label { get; init; } = string.Empty;
    public int Scale { get; init; }
    public int WorkUnits { get; init; }
    public int OutputUtf8Bytes { get; init; }
    public int InputUtf8Bytes { get; init; }
    public string InputSha256 { get; init; } = string.Empty;
    public string SemanticDigest { get; init; } = string.Empty;
    public string ManagedRoute { get; init; } = string.Empty;
    public int ManagedIterations { get; init; }
    public int CpythonIterations { get; init; }
    public double ManagedMedianMicroseconds { get; init; }
    public double CpythonMedianMicroseconds { get; init; }
    public double RatioMedian { get; init; }
    public double RatioLower95 { get; init; }
    public double RatioUpper95 { get; init; }
    public double ManagedSpread { get; init; }
    public double CpythonSpread { get; init; }
    public long ManagedAllocatedBytes { get; init; }
    public int ManagedWarmupCalls { get; init; }
    public double ManagedWarmupMilliseconds { get; init; }
    public int CpythonWarmupCalls { get; init; }
    public double CpythonWarmupMilliseconds { get; init; }
    public double OrderEffect { get; init; }
    public List<PythonReScalingSampleMeasurement> Samples { get; init; } = [];
}

internal sealed class PythonReScalingSampleMeasurement
{
    public string Order { get; init; } = string.Empty;
    public double ManagedMicroseconds { get; init; }
    public double CpythonMicroseconds { get; init; }
    public double Ratio { get; init; }
    public double ManagedElapsedMilliseconds { get; init; }
    public double CpythonElapsedMilliseconds { get; init; }
    public long ManagedAllocatedBytes { get; init; }
    public int[] ManagedGcCollections { get; init; } = [];
    public int[] CpythonGcCollections { get; init; } = [];
}
