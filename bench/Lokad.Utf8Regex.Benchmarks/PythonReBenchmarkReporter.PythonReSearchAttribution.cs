using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class PythonReBenchmarkReporter
{
    private readonly record struct PythonReSearchAttributionLane(string Label, Func<int> Action, int ExpectedResult);

    private static int RunMeasurePythonReSearchAttribution(string caseId, int requestedIterations, int samples)
    {
        var benchmarkCase = PythonReBenchmarkCatalog.Cases.SingleOrDefault(
            candidate => candidate.Id.Equals(caseId, StringComparison.Ordinal));
        if (benchmarkCase is null)
        {
            Console.Error.WriteLine($"Unknown PythonRe benchmark case {caseId}.");
            return 1;
        }

        if (benchmarkCase.Operation is not PythonReBenchmarkOperation.Search and
            not PythonReBenchmarkOperation.Match and
            not PythonReBenchmarkOperation.FullMatch and
            not PythonReBenchmarkOperation.IsMatch)
        {
            Console.Error.WriteLine(
                $"PythonRe search attribution requires Search, Match, FullMatch, or IsMatch; " +
                $"{caseId} uses {benchmarkCase.Operation}.");
            return 1;
        }

        if (benchmarkCase.Coverage.StartOffsetInBytes != 0)
        {
            Console.Error.WriteLine(
                $"PythonRe search attribution requires a zero start offset; " +
                $"{caseId} starts at byte {benchmarkCase.Coverage.StartOffsetInBytes}.");
            return 1;
        }

        var effectiveIterations = GetEffectiveIterations(benchmarkCase, requestedIterations);
        var input = Encoding.UTF8.GetBytes(benchmarkCase.Input);
        var adapter = new Utf8PythonRegex(benchmarkCase.Pattern, benchmarkCase.Options);
        var parsed = new PythonReParser(benchmarkCase.Pattern).Parse(benchmarkCase.Options);
        var translation = PythonReTranslator.Translate(parsed, true);
        var core = new Utf8Regex(translation.NativePattern, translation.NativeRegexOptions);

        var adapterOutcome = ExecuteAdapterSearchAttribution(adapter, input, benchmarkCase.Operation);
        var coreOutcome = ExecuteCoreSearchAttribution(core, input, benchmarkCase.Operation);
        RequireSearchAttributionOutcome("AdapterVsCore", adapterOutcome, coreOutcome);

        var hasKernel = TryExecuteCoreSearchKernel(core, input, benchmarkCase.Operation, out var kernelOutcome);
        if (hasKernel)
        {
            RequireSearchAttributionOutcome("KernelVsCore", coreOutcome, kernelOutcome);
        }

        var prefilterRejects = Utf8PrefilterExecutor.Rejects(core.Inspection.PreparedRegex.SearchPlan.PrefilterPlan, input);
        if (prefilterRejects && adapterOutcome.Success)
        {
            throw new InvalidOperationException($"Search attribution prefilter rejects {caseId} but the adapter matched.");
        }

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Operation         : {benchmarkCase.Operation}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"InputBytes        : {input.Length}");
        Console.WriteLine($"Iterations        : {effectiveIterations} (requested {requestedIterations})");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine("AttributionProtocol: AlternatingSearchPhasesV1");
        Console.WriteLine("WarmupPerLane     : at least 250 ms and 64 calls");
        Console.WriteLine($"ExpectedResult    : {EncodeSearchAttributionOutcome(adapterOutcome)}");
        Console.WriteLine($"ExecutionKind     : {core.Inspection.ExecutionKind}");
        Console.WriteLine($"CompiledEngine    : {core.Inspection.CompiledEngineKind}");
        Console.WriteLine($"EngineRuntime     : {core.Inspection.DebugCompiledEngineRuntimeType}");
        Console.WriteLine(hasKernel
            ? "KernelLane        : separable selected kernel without validation"
            : "KernelLane        : unavailable for selected route (validation plus public lanes only)");
        WriteSearchAttributionSnapshotContext(caseId);

        var lanes = new List<PythonReSearchAttributionLane>
        {
            new("ThrowIfInvalidOnly", () => ExecuteSearchAttributionValidationOnly(input), input.Length),
            new("BclIsValidOnly", () => ExecuteBclIsValidOnly(input), input.Length),
            new("FullValidate", () => ExecuteSearchAttributionFullValidation(input), ExecuteSearchAttributionFullValidation(input)),
            new("PrefilterOnly", () => ExecutePrefilterOnly(core, input), ExecutePrefilterOnly(core, input)),
        };

        if (hasKernel)
        {
            lanes.Add(new PythonReSearchAttributionLane(
                "CoreKernel",
                () => ExecuteCoreSearchKernel(core, input, benchmarkCase.Operation),
                EncodeSearchAttributionOutcome(kernelOutcome)));
            lanes.Add(new PythonReSearchAttributionLane(
                "ValidationThenKernel",
                () => ExecuteSearchAttributionValidationThenKernel(core, input, benchmarkCase.Operation),
                EncodeSearchAttributionOutcome(kernelOutcome)));
        }

        lanes.Add(new PythonReSearchAttributionLane(
            "CorePublic",
            () => EncodeSearchAttributionOutcome(ExecuteCoreSearchAttribution(core, input, benchmarkCase.Operation)),
            EncodeSearchAttributionOutcome(coreOutcome)));
        lanes.Add(new PythonReSearchAttributionLane(
            "AdapterPublic",
            () => EncodeSearchAttributionOutcome(ExecuteAdapterSearchAttribution(adapter, input, benchmarkCase.Operation)),
            EncodeSearchAttributionOutcome(adapterOutcome)));

        var medians = MeasureAlternatingSearchAttribution(lanes, effectiveIterations, samples);
        WriteSearchAttributionComposition(medians, hasKernel);
        return 0;
    }

    private static (bool Success, int Start, int End) ExecuteAdapterSearchAttribution(
        Utf8PythonRegex adapter,
        byte[] input,
        PythonReBenchmarkOperation operation)
    {
        if (operation == PythonReBenchmarkOperation.IsMatch)
        {
            var matched = adapter.IsMatch(input);
            return (matched, 0, matched ? input.Length : 0);
        }

        var match = operation switch
        {
            PythonReBenchmarkOperation.Match => adapter.Match(input),
            PythonReBenchmarkOperation.FullMatch => adapter.FullMatch(input),
            _ => adapter.Search(input),
        };
        return (match.Success, match.StartOffsetInBytes, match.EndOffsetInBytes);
    }

    private static (bool Success, int Start, int End) ExecuteCoreSearchAttribution(
        Utf8Regex core,
        byte[] input,
        PythonReBenchmarkOperation operation)
    {
        if (operation == PythonReBenchmarkOperation.IsMatch)
        {
            var matched = core.IsMatch(input);
            return (matched, 0, matched ? input.Length : 0);
        }

        var match = core.Match(input);
        if (!match.Success)
        {
            return (false, 0, 0);
        }

        if (!match.TryGetByteRange(out var start, out var length))
        {
            return (true, match.IndexInUtf16, match.IndexInUtf16 + match.LengthInUtf16);
        }

        var end = start + length;
        if (operation == PythonReBenchmarkOperation.FullMatch && (start != 0 || end != input.Length))
        {
            return (false, 0, 0);
        }

        return (true, start, end);
    }

    private static bool TryExecuteCoreSearchKernel(
        Utf8Regex core,
        byte[] input,
        PythonReBenchmarkOperation operation,
        out (bool Success, int Start, int End) outcome)
    {
        if (operation == PythonReBenchmarkOperation.IsMatch)
        {
            if (!core.Inspection.DebugTryIsMatchExactLiteral(input, out var isMatch))
            {
                outcome = default;
                return false;
            }

            outcome = (isMatch, 0, isMatch ? input.Length : 0);
            return true;
        }

        if (!core.Inspection.DebugTryMatchExactLiteral(input, out var match) || !match.Success)
        {
            // A miss is a separable kernel outcome only when the hook is available; unavailable means no lane.
            if (core.Inspection.DebugTryMatchExactLiteral(input, out _))
            {
                outcome = (false, 0, 0);
                return true;
            }

            outcome = default;
            return false;
        }

        if (!match.TryGetByteRange(out var start, out var length))
        {
            outcome = (true, match.IndexInUtf16, match.IndexInUtf16 + match.LengthInUtf16);
            return true;
        }

        outcome = (true, start, start + length);
        return true;
    }

    private static int ExecuteCoreSearchKernel(
        Utf8Regex core,
        byte[] input,
        PythonReBenchmarkOperation operation)
    {
        TryExecuteCoreSearchKernel(core, input, operation, out var outcome);
        return EncodeSearchAttributionOutcome(outcome);
    }

    private static int ExecuteSearchAttributionValidationThenKernel(
        Utf8Regex core,
        byte[] input,
        PythonReBenchmarkOperation operation)
    {
        Utf8Validation.ThrowIfInvalidOnly(input);
        return ExecuteCoreSearchKernel(core, input, operation);
    }

    private static int ExecuteSearchAttributionValidationOnly(byte[] input)
    {
        Utf8Validation.ThrowIfInvalidOnly(input);
        return input.Length;
    }

    private static int ExecuteBclIsValidOnly(byte[] input)
    {
        return Utf8.IsValid(input) ? input.Length : 0;
    }

    private static int ExecuteSearchAttributionFullValidation(byte[] input)
    {
        return Utf8Validation.Validate(input).Utf16Length;
    }

    private static int ExecutePrefilterOnly(Utf8Regex core, byte[] input)
    {
        return Utf8PrefilterExecutor.Rejects(core.Inspection.PreparedRegex.SearchPlan.PrefilterPlan, input) ? 1 : 0;
    }

    private static int EncodeSearchAttributionOutcome((bool Success, int Start, int End) outcome)
    {
        return outcome.Success ? checked((outcome.Start + 1) * 1000003 ^ outcome.End) : 0;
    }

    private static void RequireSearchAttributionOutcome(
        string lane,
        (bool Success, int Start, int End) expected,
        (bool Success, int Start, int End) actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                $"Search attribution lane {lane} returned ({actual.Success}, {actual.Start}, {actual.End}); " +
                $"expected ({expected.Success}, {expected.Start}, {expected.End}).");
        }
    }

    private static void WriteSearchAttributionSnapshotContext(string caseId)
    {
        PythonRePairedEvidence? evidence = null;
        string status = "Unqualified";
        try
        {
            var snapshot = LoadPythonReBenchmarkSnapshot();
            if (snapshot.Cases.TryGetValue(caseId, out var measurement) &&
                measurement.Qualification is not null)
            {
                status = measurement.Qualification.Status ?? "Unqualified";
                evidence = measurement.Qualification.PairedEvidence;
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine($"SnapshotContext   : unavailable ({exception.GetType().Name})");
            return;
        }

        if (evidence is null)
        {
            Console.WriteLine($"SnapshotContext   : {status}, no paired evidence");
            return;
        }

        Console.WriteLine(
            $"SnapshotContext   : {status} Rstrong={evidence.StrongRatioMedian:F3} " +
            $"[{evidence.StrongRatioLower95:F3}, {evidence.StrongRatioUpper95:F3}] " +
            $"managed={evidence.ManagedMedianMicroseconds:F3}us " +
            $"cpython={evidence.CpythonMedianMicroseconds:F3}us " +
            $"order={evidence.OrderEffect:F3}");
    }

    private static Dictionary<string, double> MeasureAlternatingSearchAttribution(
        List<PythonReSearchAttributionLane> lanes,
        int iterations,
        int samples)
    {
        Console.WriteLine($"Runtime           : {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OperatingSystem   : {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Processor         : {RuntimeInformation.ProcessArchitecture}");
        var elapsedByLane = new TimeSpan[lanes.Count][];
        var warmupCalls = new int[lanes.Count];
        for (var laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
        {
            elapsedByLane[laneIndex] = new TimeSpan[samples];
            warmupCalls[laneIndex] = WarmupSearchAttributionLane(lanes[laneIndex].Action);
        }

        for (var sample = 0; sample < samples; sample++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            for (var position = 0; position < lanes.Count; position++)
            {
                var laneIndex = (sample & 1) == 0 ? position : lanes.Count - 1 - position;
                var measured = MeasureSearchAttributionLane(iterations, lanes[laneIndex].Action);
                elapsedByLane[laneIndex][sample] = measured.Elapsed;
                GC.KeepAlive(measured.Sink);
            }
        }

        var medians = new Dictionary<string, double>(lanes.Count, StringComparer.Ordinal);
        for (var laneIndex = 0; laneIndex < lanes.Count; laneIndex++)
        {
            var elapsed = elapsedByLane[laneIndex];
            Array.Sort(elapsed);
            var medianMicroseconds = elapsed[elapsed.Length / 2].TotalMicroseconds / iterations;
            var minimumMicroseconds = elapsed[0].TotalMicroseconds / iterations;
            var maximumMicroseconds = elapsed[elapsed.Length - 1].TotalMicroseconds / iterations;
            medians.Add(lanes[laneIndex].Label, medianMicroseconds);
            Console.WriteLine(
                $"{lanes[laneIndex].Label,-32}: " +
                $"{medianMicroseconds:F3} us/op | " +
                $"range={minimumMicroseconds:F3}..{maximumMicroseconds:F3} | " +
                $"result={lanes[laneIndex].ExpectedResult} | warmupCalls={warmupCalls[laneIndex]}");
        }

        return medians;
    }

    private static int WarmupSearchAttributionLane(Func<int> action)
    {
        const int maximumIterations = 262144;
        var target = TimeSpan.FromMilliseconds(250);
        var iterations = 0;
        var sink = 0;
        var stopwatch = Stopwatch.StartNew();
        do
        {
            sink ^= action();
            iterations++;
        }
        while (iterations < maximumIterations &&
               (iterations < 64 || stopwatch.Elapsed < target));

        GC.KeepAlive(sink);
        return iterations;
    }

    private static (TimeSpan Elapsed, int Sink) MeasureSearchAttributionLane(int iterations, Func<int> action)
    {
        var sink = 0;
        var stopwatch = Stopwatch.StartNew();
        for (var iteration = 0; iteration < iterations; iteration++)
        {
            sink ^= action();
        }

        stopwatch.Stop();
        return (stopwatch.Elapsed, sink);
    }

    private static void WriteSearchAttributionComposition(Dictionary<string, double> medians, bool hasKernel)
    {
        if (hasKernel &&
            medians.TryGetValue("ThrowIfInvalidOnly", out var validation) &&
            medians.TryGetValue("CoreKernel", out var kernel) &&
            medians.TryGetValue("ValidationThenKernel", out var validationThenKernel) &&
            medians.TryGetValue("AdapterPublic", out var adapterPublic))
        {
            Console.WriteLine($"Composition       : validation {validation:F3} + kernel {kernel:F3} = {validation + kernel:F3} vs measured validation-then-kernel {validationThenKernel:F3} vs public {adapterPublic:F3}");
        }

        if (medians.TryGetValue("PrefilterOnly", out var prefilter) &&
            medians.TryGetValue("AdapterPublic", out var adapterOnly))
        {
            Console.WriteLine($"PrefilterShare      : prefilter {prefilter:F3} of public {adapterOnly:F3}");
        }

        Console.WriteLine("CompositionNote   : independently timed phase sums are attribution ceilings, not realizable speedups");
    }
}

