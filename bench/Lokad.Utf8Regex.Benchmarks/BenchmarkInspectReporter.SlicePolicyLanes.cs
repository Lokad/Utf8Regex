using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex;
using Lokad.Utf8Regex.Pcre2;
using Lokad.Utf8Regex.PythonRe;

namespace Lokad.Utf8Regex.Benchmarks;

// R02a focused cost controls: slice shapes measured under unlimited/no-token,
// finite-limits/no-token (PCRE2 only; core and PythonRe reject finite budgets),
// and finite-limits/live-uncanceled-token lanes (unlimited/live-token where no
// finite surface exists). Lanes alternate sample-by-sample with warmup; results
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

        var core = new Utf8Regex(sliceCase.Pattern, RegexOptions.None);
        Console.WriteLine(
            "  Core routes: ownedSlice=" +
            core.Inspection.DebugUsesOwnedSliceProgram +
            " kind=" + core.Inspection.ExecutionKind);

        var python = new Utf8PythonRegex(
            sliceCase.Pattern, PythonReCompileOptions.None);
        Console.WriteLine(
            "  PythonRe routes: ownedSlice=" + python.DebugUsesOwnedSlice +
            " search=" + python.DebugSearchBackend +
            " count=" + python.DebugCountBackend);

        var pcre2 = new Utf8Pcre2Regex(sliceCase.Pattern, Pcre2CompileOptions.None);
        var pcre2Finite = new Utf8Pcre2Regex(
            sliceCase.Pattern,
            Pcre2CompileOptions.None,
            default,
            new Utf8Pcre2ExecutionLimits { MatchLimit = SlicePolicyLaneMatchLimit },
            Utf8Pcre2Regex.DefaultMatchTimeout);
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
                new("python", "python/unlimited",
                    () => python.Search(input, 0).Success ? 1 : 0,
                    () => python.Search(input, 0).Success ? 1 : 0),
                new("python", "python/token",
                    () => python.Search(input, 0, token).Success ? 1 : 0,
                    () => python.Search(input, 0).Success ? 1 : 0),
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
                new("python", "python/unlimited",
                    () => python.Count(input, 0),
                    () => python.Count(input, 0)),
                new("python", "python/token",
                    () => python.Count(input, 0, token),
                    () => python.Count(input, 0)),
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

        var medians = MeasureReadmeAlternatingMedianMicroseconds(
            samples, iterations, actions, false);
        Console.WriteLine("  Operation: " + operation);
        for (var index = 0; index < lanes.Length; index++)
        {
            var allocated = MeasureAllocatedBytesPerOperation(
                iterations, actions[index]);
            Console.WriteLine(
                "    " + lanes[index].Name +
                ": result=" + lanes[index].ExpectedResult +
                " us/op=" + medians[index].ToString("N3", CultureInfo.InvariantCulture) +
                " bytes/op=" + allocated.ToString("N1", CultureInfo.InvariantCulture));
        }
    }
}
