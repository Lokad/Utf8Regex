using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

// R02a focused cost controls: slice shapes measured under unlimited/no-token,
// finite-limits/no-token and finite-limits/live-uncanceled-token lanes. Core and
// PythonRe use all four accepted invocation controls. Lanes alternate with warmup; results
// must agree within each profile before timing. Performance parity is not a
// precondition; this records policy and token costs plus serving routes.
internal static partial class BenchmarkInspectReporter
{
    private const int SlicePolicyLaneDefaultIterations = 20000;

    private const uint SlicePolicyLaneMatchLimit = 1000000;

    private sealed record SlicePolicyLaneCase(string Pattern, string InputText);

    private sealed record SlicePolicyLane(
        string Profile,
        string Name,
        Func<int> Action,
        Func<int> Probe)
    {
        public int ExpectedResult;
    }

    public static int RunMeasureSlicePolicyLanes(
        string? iterationsText,
        string? samplesText)
    {
        var iterations = string.IsNullOrWhiteSpace(iterationsText)
            ? SlicePolicyLaneDefaultIterations
            : ParseIterations(iterationsText);
        var samples = Math.Clamp(ParseSamples(samplesText), 1, 9);
        var cases = new SlicePolicyLaneCase[]
        {
            new("(a+)(b+)", "xxaaabbyy"),
            new("(ab|cd)+", "xxabcdcdyy"),
            new("(a|ab)+c", "xxabcabccyy"),
            new("a+b", "xxaaab"),
            new("x*|y", "y"),
        };

        using var tokenSource = new CancellationTokenSource();
        var token = tokenSource.Token;
        foreach (var sliceCase in cases)
        {
            MeasureSlicePolicyCase(sliceCase, iterations, samples, token);
        }

        return 0;
    }

    private static void MeasureSlicePolicyCase(
        SlicePolicyLaneCase sliceCase,
        int iterations,
        int samples,
        CancellationToken token)
    {
        var input = Encoding.UTF8.GetBytes(sliceCase.InputText);
        Console.WriteLine(
            "Case: pattern=" + sliceCase.Pattern +
            " inputBytes=" + input.Length);

        var core = new Utf8Regex(sliceCase.Pattern, options: RegexOptions.None);
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000,
            MaxDepth = 1_024,
            MaxWorkingBytes = 1_048_576,
            MaxResultBytes = 1_048_576
        };
        var coreFinite = new Utf8Regex(            sliceCase.Pattern, options: RegexOptions.None, policy: new Utf8RegexPolicy
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        var baseline = new Regex(sliceCase.Pattern, RegexOptions.CultureInvariant);
        Console.WriteLine(
            "  Core routes: ownedSlice=" +
            core.Inspection.DebugUsesOwnedSliceProgram +
            " kind=" + core.Inspection.ExecutionKind);

        var python = new Utf8PythonRegex(sliceCase.Pattern, options: PythonReCompileOptions.None);
        var pythonFinite = new Utf8PythonRegex(sliceCase.Pattern, options: PythonReCompileOptions.None, policy: new Utf8RegexPolicy()
        {
            MatchTimeout = Regex.InfiniteMatchTimeout,
            ResourcePolicy = policy
        });
        Console.WriteLine("  PythonRe preparation: coreKind=" +
            (python.DebugUtf8ExecutionKind ?? "None"));
        Console.WriteLine("  Core/PythonRe finite: work=1000000 depth=1024 working=1048576 result=1048576");
        Console.WriteLine("  Finite preparation: core=" + coreFinite.Inspection.ExecutionKind +
            " pythonCore=" + (pythonFinite.DebugUtf8ExecutionKind ?? "None"));

        var pcre2 = new Utf8Pcre2Regex(sliceCase.Pattern, options: Pcre2CompileOptions.None);
        var pcre2Finite = new Utf8Pcre2Regex(            sliceCase.Pattern, options:             Pcre2CompileOptions.None, compileSettings:             default, policy: new Utf8RegexPolicy
        {
            ResourcePolicy = new Utf8ResourcePolicy
            {
                MaxWorkSteps = SlicePolicyLaneMatchLimit
            },
            MatchTimeout = Utf8Pcre2Regex.DefaultMatchTimeout
        });
        Console.WriteLine("  Pcre2 plan: " + pcre2.DebugDescribeExecutionPlan());
        Console.WriteLine(
            "  Pcre2 finite plan: " + pcre2Finite.DebugDescribeExecutionPlan());

        MeasureSlicePolicyOperation(
            "IsMatch",
            new SlicePolicyLane[]
            {
                new("core", "core/unlimited",
                    () => core.IsMatch(input) ? 1 : 0,
                    () => core.IsMatch(input) ? 1 : 0),
                new("core", "core/token",
                    () => core.IsMatch(input, token) ? 1 : 0,
                    () => core.IsMatch(input) ? 1 : 0),
                new("core", "core/finite",
                    () => coreFinite.IsMatch(input) ? 1 : 0,
                    () => core.IsMatch(input) ? 1 : 0),
                new("core", "core/finite+token",
                    () => coreFinite.IsMatch(input, token) ? 1 : 0,
                    () => core.IsMatch(input) ? 1 : 0),
                new("python", "python/unlimited",
                    () => python.Match(input,0,mode: Utf8PythonMatchMode.Search).Success ? 1 : 0,
                    () => python.Match(input,0,mode: Utf8PythonMatchMode.Search).Success ? 1 : 0),
                new("python", "python/token",
                    () => python.Match(input,0,cancellationToken: token,mode: Utf8PythonMatchMode.Search).Success ? 1 : 0,
                    () => python.Match(input,0,mode: Utf8PythonMatchMode.Search).Success ? 1 : 0),
                new("python", "python/finite",
                    () => pythonFinite.Match(input,0,mode: Utf8PythonMatchMode.Search).Success ? 1 : 0,
                    () => python.Match(input,0,mode: Utf8PythonMatchMode.Search).Success ? 1 : 0),
                new("python", "python/finite+token",
                    () => pythonFinite.Match(input,0,cancellationToken: token,mode: Utf8PythonMatchMode.Search).Success ? 1 : 0,
                    () => python.Match(input,0,mode: Utf8PythonMatchMode.Search).Success ? 1 : 0),
                new("dotnet", "dotnet/predecoded",
                    () => baseline.IsMatch(sliceCase.InputText) ? 1 : 0,
                    () => baseline.IsMatch(sliceCase.InputText) ? 1 : 0),
                new("dotnet", "dotnet/decode",
                    () => baseline.IsMatch(Encoding.UTF8.GetString(input)) ? 1 : 0,
                    () => baseline.IsMatch(sliceCase.InputText) ? 1 : 0),
                new("pcre2", "pcre2/unlimited",
                    () => pcre2.IsMatch(input, 0) ? 1 : 0,
                    () => pcre2.IsMatch(input, 0) ? 1 : 0),
                new("pcre2", "pcre2/finite",
                    () => pcre2Finite.IsMatch(input, 0) ? 1 : 0,
                    () => pcre2.IsMatch(input, 0) ? 1 : 0),
                new("pcre2", "pcre2/finite+token",
                    () => pcre2Finite.IsMatch(input, 0, Pcre2MatchOptions.None, token) ? 1 : 0,
                    () => pcre2.IsMatch(input, 0) ? 1 : 0),
            },
            iterations,
            samples);

        MeasureSlicePolicyOperation(
            "Count",
            new SlicePolicyLane[]
            {
                new("core", "core/unlimited",
                    () => core.Count(input),
                    () => core.Count(input)),
                new("core", "core/token",
                    () => core.Count(input, token),
                    () => core.Count(input)),
                new("core", "core/finite",
                    () => coreFinite.Count(input),
                    () => core.Count(input)),
                new("core", "core/finite+token",
                    () => coreFinite.Count(input, token),
                    () => core.Count(input)),
                new("python", "python/unlimited",
                    () => python.Count(input, 0),
                    () => python.Count(input, 0)),
                new("python", "python/token",
                    () => python.Count(input, 0, token),
                    () => python.Count(input, 0)),
                new("python", "python/finite",
                    () => pythonFinite.Count(input, 0),
                    () => python.Count(input, 0)),
                new("python", "python/finite+token",
                    () => pythonFinite.Count(input, 0, token),
                    () => python.Count(input, 0)),
                new("dotnet", "dotnet/predecoded",
                    () => baseline.Count(sliceCase.InputText),
                    () => baseline.Count(sliceCase.InputText)),
                new("dotnet", "dotnet/decode",
                    () => baseline.Count(Encoding.UTF8.GetString(input)),
                    () => baseline.Count(sliceCase.InputText)),
                new("pcre2", "pcre2/unlimited",
                    () => pcre2.Count(input, 0),
                    () => pcre2.Count(input, 0)),
                new("pcre2", "pcre2/finite",
                    () => pcre2Finite.Count(input, 0),
                    () => pcre2.Count(input, 0)),
                new("pcre2", "pcre2/finite+token",
                    () => pcre2Finite.Count(input, 0, Pcre2MatchOptions.None, token),
                    () => pcre2.Count(input, 0)),
            },
            iterations,
            samples);
    }

    private static void MeasureSlicePolicyOperation(
        string operation,
        SlicePolicyLane[] lanes,
        int iterations,
        int samples)
    {
        foreach (var lane in lanes)
        {
            lane.ExpectedResult = lane.Probe();
            if (lane.Action() != lane.ExpectedResult)
                throw new InvalidOperationException("Policy action disagrees with its control: " + lane.Name);
        }

        foreach (var group in lanes.GroupBy(static lane => lane.Profile))
        {
            var first = group.First().ExpectedResult;
            foreach (var lane in group)
            {
                if (lane.ExpectedResult != first)
                {
                    throw new InvalidOperationException(
                        "Slice policy lanes disagree on " + operation +
                        ": " + lane.Name + " observed " + lane.ExpectedResult +
                        " while " + group.First().Name + " observed " + first + ".");
                }
            }
        }

        var actions = new Func<int>[lanes.Length];
        for (var index = 0; index < lanes.Length; index++)
        {
            actions[index] = lanes[index].Action;
        }

        const int minimumWarmCalls = 256;
        const double targetSampleMicroseconds = 50_000;
        var maximumIterations = Math.Max(iterations, 2_000_000);
        var effectiveIterations = new int[lanes.Length];
        for (var index = 0; index < actions.Length; index++)
        {
            // A duration alone can leave a slow public entry below its tiering
            // call threshold. Warm that entry before the existing timed warmup.
            _ = MeasureTimedCore(minimumWarmCalls, actions[index]);
            _ = WarmupCore(actions[index]);
            var microseconds = MeasureReadmeProbeMicrosecondsPerOperation(actions[index], maximumIterations);
            effectiveIterations[index] = (int)Math.Clamp(
                Math.Ceiling(targetSampleMicroseconds / Math.Max(0.001, microseconds)),
                iterations, maximumIterations);
        }
        for (var index = 0; index < actions.Length; index++)
        {
            // Confirm after every lane has warmed, with sampling's GC preflight.
            // A single allocation-heavy probe can overestimate the settled cost.
            var fastestMicroseconds = double.PositiveInfinity;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                fastestMicroseconds = Math.Min(fastestMicroseconds,
                    MeasureTimedCore(effectiveIterations[index], actions[index]).Elapsed.TotalMicroseconds);
            }
            effectiveIterations[index] = (int)Math.Clamp(
                Math.Ceiling(effectiveIterations[index] * targetSampleMicroseconds / Math.Max(1, fastestMicroseconds)),
                effectiveIterations[index], maximumIterations);
        }
        Console.WriteLine("  Protocol: warmupMinimumCalls=256 warmupTargetMs=250 sampleTargetMs=50 finalDurationChecks=3 requestedMinimumIterations=" + iterations);
        var timings = new double[lanes.Length][];
        for (var index = 0; index < lanes.Length; index++) timings[index] = new double[samples];
        var sink = 0;
        for (var sample = 0; sample < samples; sample++)
        {
            for (var position = 0; position < lanes.Length; position++)
            {
                var index = (sample & 1) == 0 ? position : lanes.Length - position - 1;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                var measured = MeasureTimedCore(effectiveIterations[index], actions[index]);
                timings[index][sample] = measured.Elapsed.TotalMicroseconds / effectiveIterations[index];
                sink ^= measured.Sink;
                Console.WriteLine("  Raw: operation=" + operation + " lane=" + lanes[index].Name +
                    " sample=" + sample + " iterations=" + effectiveIterations[index] +
                    " elapsedMs=" + measured.Elapsed.TotalMilliseconds.ToString("F6", CultureInfo.InvariantCulture) + " us/op=" +
                    timings[index][sample].ToString("F6", CultureInfo.InvariantCulture));
            }
        }
        GC.KeepAlive(sink);
        Console.WriteLine("  Operation: " + operation);
        for (var index = 0; index < lanes.Length; index++)
        {
            var allocated = MeasureAllocatedBytesPerOperation(
                effectiveIterations[index], actions[index]);
            Console.WriteLine(
                "    " + lanes[index].Name +
                ": result=" + lanes[index].ExpectedResult +
                " us/op=" + timings[index].Order().ElementAt(samples / 2).ToString("N3", CultureInfo.InvariantCulture) +
                " bytes/op=" + allocated.ToString("N1", CultureInfo.InvariantCulture));
        }
    }
}
