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

    // The downstream handoff's fixed linear captured workload, not a new catalog
    // or a Status qualification. Outputs remain owned until the next invocation.
    public static int RunMeasurePythonReHandoffBulk(string? iterationsText, string? samplesText)
    {
        var iterations = string.IsNullOrWhiteSpace(iterationsText) ? 20 : ParseIterations(iterationsText);
        var samples = Math.Clamp(ParseSamples(samplesText), 1, 9);
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 50_000_000, MaxDepth = 1_024,
            MaxWorkingBytes = 64 * 1_048_576, MaxResultBytes = 128 * 1_048_576,
        };
        var regex = new Utf8PythonRegex("(a)(b)");
        var finite = new Utf8PythonRegex("(a)(b)", PythonReCompileOptions.None,
            Regex.InfiniteMatchTimeout, policy);
        var core = new Utf8Regex("(a)(b)", RegexOptions.CultureInvariant);
        var coreFinite = new Utf8Regex("(a)(b)", RegexOptions.CultureInvariant,
            Regex.InfiniteMatchTimeout, policy);
        var baseline = new Regex("(a)(b)", RegexOptions.CultureInvariant);
        using var source = new CancellationTokenSource();
        Console.WriteLine("PythonRe handoff bulk: (a)(b), ab repeated with spaces; work=50000000 depth=1024 working=67108864 result=134217728");
        foreach (var count in new[] { 1_000, 5_000, 10_000 })
        {
            var text = string.Concat(Enumerable.Repeat("ab ", count));
            var input = Encoding.UTF8.GetBytes(text);
            Utf8PythonDetailedMatchData[] detailed = [];
            Utf8PythonFindAllUtf8Result values = default;
            byte[][][] baselineValues = [];
            int Detailed(Utf8PythonRegex pattern, CancellationToken token)
            {
                detailed = pattern.FindIterDetailed(input, 0, token);
                return detailed.Length;
            }
            int Values(Utf8PythonRegex pattern, CancellationToken token)
            {
                values = pattern.FindAllToUtf8(input, 0, token);
                return values.Count;
            }
            int BaselineValues(string subject)
            {
                baselineValues = baseline.Matches(subject).Select(match => new[]
                {
                    Encoding.UTF8.GetBytes(match.Groups[1].Value),
                    Encoding.UTF8.GetBytes(match.Groups[2].Value),
                }).ToArray();
                return baselineValues.Length;
            }
            int Prefix(Utf8PythonRegex pattern, int maximum)
            {
                using var cursor = pattern.CreateMatchCursor(input);
                var found = 0;
                while (found < maximum && cursor.MoveNext())
                {
                    if (cursor.Current.Value.ValueText != "ab")
                        throw new InvalidOperationException("Unexpected cursor value.");
                    found++;
                }
                return found;
            }
            if (Detailed(regex, default) != count || Values(regex, default) != count)
                throw new InvalidOperationException("Unexpected handoff count.");
            for (var index = 0; index < count; index++)
            {
                var match = detailed[index];
                if (match.Value.StartOffsetInBytes != index * 3 || match.Value.EndOffsetInBytes != index * 3 + 2 ||
                    match.Value.ValueText != "ab" || match.LastIndex != 2 ||
                    !match.TryGetGroup(1, out var a) || a.ValueText != "a" ||
                    !match.TryGetGroup(2, out var b) || b.ValueText != "b" ||
                    !values.TupleValues[index][0].AsSpan().SequenceEqual("a"u8) ||
                    !values.TupleValues[index][1].AsSpan().SequenceEqual("b"u8))
                    throw new InvalidOperationException("Unexpected handoff capture/range/value.");
            }
            Console.WriteLine("Case: matches=" + count + " inputBytes=" + input.Length);
            MeasureSlicePolicyOperation("Count", [
                new("count", "core/unlimited", () => core.Count(input), () => count),
                new("count", "core/finite", () => coreFinite.Count(input), () => count),
                new("count", "dotnet/predecoded", () => baseline.Count(text), () => count),
                new("count", "dotnet/decode", () => baseline.Count(Encoding.UTF8.GetString(input)), () => count),
            ], iterations, samples);
            MeasureSlicePolicyOperation("FindIterDetailed", [
                new("detailed", "python/unlimited", () => Detailed(regex, default), () => count),
                new("detailed", "python/finite", () => Detailed(finite, default), () => count),
                new("detailed", "python/finite+token", () => Detailed(finite, source.Token), () => count),
            ], iterations, samples);
            MeasureSlicePolicyOperation("FindAllToUtf8", [
                new("values", "python/unlimited", () => Values(regex, default), () => count),
                new("values", "python/finite", () => Values(finite, default), () => count),
                new("values", "python/finite+token", () => Values(finite, source.Token), () => count),
                new("values", "dotnet/predecoded", () => BaselineValues(text), () => count),
                new("values", "dotnet/decode", () => BaselineValues(Encoding.UTF8.GetString(input)), () => count),
            ], iterations, samples);
            // Prefix measurements include full-input validation and early Dispose.
            var prefixIterations = Math.Max(iterations, 2_000);
            MeasureSlicePolicyOperation("CursorFirstAndDispose", [
                new("first", "python/unlimited", () => Prefix(regex, 1), () => 1),
                new("first", "python/finite", () => Prefix(finite, 1), () => 1),
            ], prefixIterations, samples);
            MeasureSlicePolicyOperation("CursorTenAndDispose", [
                new("prefix", "python/unlimited", () => Prefix(regex, 10), () => 10),
                new("prefix", "python/finite", () => Prefix(finite, 10), () => 10),
            ], prefixIterations, samples);
            GC.KeepAlive(detailed);
            GC.KeepAlive(values);
            GC.KeepAlive(baselineValues);
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
        var policy = new Utf8ResourcePolicy
        {
            MaxWorkSteps = 1_000_000,
            MaxDepth = 1_024,
            MaxWorkingBytes = 1_048_576,
            MaxResultBytes = 1_048_576,
        };
        var coreFinite = new Utf8Regex(
            sliceCase.Pattern, RegexOptions.None, Regex.InfiniteMatchTimeout, policy);
        var baseline = new Regex(sliceCase.Pattern, RegexOptions.CultureInvariant);
        Console.WriteLine(
            "  Core routes: ownedSlice=" +
            core.Inspection.DebugUsesOwnedSliceProgram +
            " kind=" + core.Inspection.ExecutionKind);

        var python = new Utf8PythonRegex(
            sliceCase.Pattern, PythonReCompileOptions.None);
        var pythonFinite = new Utf8PythonRegex(
            sliceCase.Pattern, PythonReCompileOptions.None, Regex.InfiniteMatchTimeout, policy);
        Console.WriteLine(
            "  PythonRe routes: ownedSlice=" + python.DebugUsesOwnedSlice +
            " search=" + python.DebugSearchBackend +
            " count=" + python.DebugCountBackend);
        Console.WriteLine("  Core/PythonRe finite: work=1000000 depth=1024 working=1048576 result=1048576");
        Console.WriteLine("  Finite routes: core=" + coreFinite.Inspection.ExecutionKind +
            " pythonSearch=" + pythonFinite.DebugSearchBackend +
            " pythonCount=" + pythonFinite.DebugCountBackend);

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
                new("core", "core/finite",
                    () => coreFinite.IsMatch(input) ? 1 : 0,
                    () => core.IsMatch(input) ? 1 : 0),
                new("core", "core/finite+token",
                    () => coreFinite.IsMatch(input, token) ? 1 : 0,
                    () => core.IsMatch(input) ? 1 : 0),
                new("python", "python/unlimited",
                    () => python.Search(input, 0).Success ? 1 : 0,
                    () => python.Search(input, 0).Success ? 1 : 0),
                new("python", "python/token",
                    () => python.Search(input, 0, token).Success ? 1 : 0,
                    () => python.Search(input, 0).Success ? 1 : 0),
                new("python", "python/finite",
                    () => pythonFinite.Search(input, 0).Success ? 1 : 0,
                    () => python.Search(input, 0).Success ? 1 : 0),
                new("python", "python/finite+token",
                    () => pythonFinite.Search(input, 0, token).Success ? 1 : 0,
                    () => python.Search(input, 0).Success ? 1 : 0),
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

        foreach (var action in actions) _ = WarmupCore(action);
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
                var measured = MeasureTimedCore(iterations, actions[index]);
                timings[index][sample] = measured.Elapsed.TotalMicroseconds / iterations;
                sink ^= measured.Sink;
                Console.WriteLine("  Raw: operation=" + operation + " lane=" + lanes[index].Name +
                    " sample=" + sample + " iterations=" + iterations + " us/op=" +
                    timings[index][sample].ToString("F6", CultureInfo.InvariantCulture));
            }
        }
        GC.KeepAlive(sink);
        Console.WriteLine("  Operation: " + operation);
        for (var index = 0; index < lanes.Length; index++)
        {
            var allocated = MeasureAllocatedBytesPerOperation(
                iterations, actions[index]);
            Console.WriteLine(
                "    " + lanes[index].Name +
                ": result=" + lanes[index].ExpectedResult +
                " us/op=" + timings[index].Order().ElementAt(samples / 2).ToString("N3", CultureInfo.InvariantCulture) +
                " bytes/op=" + allocated.ToString("N1", CultureInfo.InvariantCulture));
        }
    }
}
