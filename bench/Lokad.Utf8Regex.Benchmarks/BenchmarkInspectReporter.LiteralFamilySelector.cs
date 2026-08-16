using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    public static int RunMeasureShortAsciiLiteralFamilyCountControls(
        string? iterationsText,
        string? samplesText)
    {
        var iterations = iterationsText is null ? 200 : ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var controls = new (string Name, string Pattern, string Input)[]
        {
            ("three-dense-target", "cat|dog|yak", RepeatToLength("cat dog yak ", 49_152)),
            ("three-dense-alternate", "foo|bar|zip", RepeatToLength("zip foo bar ", 49_152)),
            ("three-collision-miss", "cat|dog|yak", RepeatToLength("caq doq yaq ", 49_152)),
            ("three-plain-miss", "cat|dog|yak", new string('x', 49_152)),
            ("three-late-sparse", "cat|dog|yak", new string('x', 49_149) + "yak"),
            ("three-two-literal", "cat|dog", RepeatToLength("cat dog ", 49_152)),
            ("three-four-literal", "cat|dog|yak|emu", RepeatToLength("emu cat dog yak ", 49_152)),
            ("four-byte-control", "cats|dogs|yaks", RepeatToLength("cats dogs yaks ", 49_152)),
            ("two-byte-excluded", "go|up|no", RepeatToLength("go up no ", 49_152)),
            ("prefix-overlap-excluded", "cat|catalog|dog", RepeatToLength("catalog cat dog ", 49_152)),
            ("short-three-dense", "cat|dog|yak", "cat dog yak cat"),
            ("one-byte-selector-4096", "a|needle", new string('a', 4_096)),
            ("one-byte-selector-8192", "a|needle", new string('a', 8_192)),
        };

        WriteMeasurementEnvironment();
        Console.WriteLine($"Iterations         : {iterations}");
        Console.WriteLine($"Samples            : {samples}");

        foreach (var control in controls)
        {
            var input = Encoding.UTF8.GetBytes(control.Input);
            var literals = control.Pattern.Split('|').Select(Encoding.UTF8.GetBytes).ToArray();
            var hasCounter = PreparedShortAsciiLiteralFamilyCounter.TryCreate(literals, out var counter);
            var ordinary = new Utf8Regex(control.Pattern, RegexOptions.CultureInvariant);
            var compiled = new Utf8Regex(
                control.Pattern,
                RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var finite = new Utf8Regex(
                control.Pattern,
                RegexOptions.CultureInvariant | RegexOptions.Compiled,
                TimeSpan.FromSeconds(10));
            var regex = new Regex(
                control.Pattern,
                RegexOptions.CultureInvariant,
                Regex.InfiniteMatchTimeout);
            var compiledRegex = new Regex(
                control.Pattern,
                RegexOptions.CultureInvariant | RegexOptions.Compiled,
                Regex.InfiniteMatchTimeout);
            var expected = regex.Count(control.Input);

            AssertSelectorResult(control.Name, "ordinary", expected, ordinary.Count(input));
            AssertSelectorResult(control.Name, "compiled", expected, compiled.Count(input));
            AssertSelectorResult(control.Name, "finite", expected, finite.Count(input));
            if (hasCounter)
            {
                AssertSelectorResult(control.Name, "direct counter", expected, counter.Count(input));
            }

            var effectiveIterations = input.Length <= 128
                ? Math.Max(iterations, 20_000)
                : iterations;
            Console.WriteLine();
            Console.WriteLine($"Case               : {control.Name}");
            Console.WriteLine($"Pattern            : {control.Pattern}");
            Console.WriteLine($"InputBytes         : {input.Length}");
            Console.WriteLine($"ExpectedCount      : {expected}");
            Console.WriteLine($"CounterEligible    : {hasCounter}");
            Console.WriteLine($"EffectiveIterations: {effectiveIterations}");
            Measure("Ordinary", samples, effectiveIterations, () => ordinary.Count(input));
            Measure("Compiled", samples, effectiveIterations, () => compiled.Count(input));
            if (hasCounter)
            {
                Measure("DirectCounter", samples, effectiveIterations, () => counter.Count(input));
            }

            Measure("FiniteCompiled", samples, effectiveIterations, () => finite.Count(input));
            Measure("DecodeThenRegex", samples, effectiveIterations, () => regex.Count(Encoding.UTF8.GetString(input)));
            Measure("DecodeThenCompiledRegex", samples, effectiveIterations, () => compiledRegex.Count(Encoding.UTF8.GetString(input)));
            Measure("PredecodedRegex", samples, effectiveIterations, () => regex.Count(control.Input));
            Measure("PredecodedCompiledRegex", samples, effectiveIterations, () => compiledRegex.Count(control.Input));

            var allocationIterations = Math.Min(effectiveIterations, 1_000);
            Console.WriteLine(
                $"Warm allocation    : ordinary={MeasureAllocatedBytesPerInvocation(allocationIterations, () => ordinary.Count(input))} B, " +
                $"compiled={MeasureAllocatedBytesPerInvocation(allocationIterations, () => compiled.Count(input))} B, " +
                $"finite={MeasureAllocatedBytesPerInvocation(allocationIterations, () => finite.Count(input))} B");

            if (control.Name == "three-dense-target")
            {
                var constructionIterations = Math.Min(iterations, 100);
                Measure("ConstructOrdinary", samples, constructionIterations, () =>
                    (int)new Utf8Regex(control.Pattern, RegexOptions.CultureInvariant).Inspection.CompiledEngineKind);
                Measure("ConstructCompiled", samples, constructionIterations, () =>
                    (int)new Utf8Regex(
                        control.Pattern,
                        RegexOptions.CultureInvariant | RegexOptions.Compiled).Inspection.CompiledEngineKind);
            }
        }

        return 0;
    }

    public static int RunMeasureLiteralFamilySelector(string? iterationsText, string? samplesText)
    {
        const string pattern = "(?:a|needle)";
        var iterations = iterationsText is null ? 100 : ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var cases = new (string Name, byte[] Input)[]
        {
            ("dense-4095", Encoding.UTF8.GetBytes(new string('a', 4095))),
            ("dense-4096", Encoding.UTF8.GetBytes(new string('a', 4096))),
            ("dense-8192", Encoding.UTF8.GetBytes(new string('a', 8192))),
            ("collision-4095", Encoding.UTF8.GetBytes(new string('n', 4095))),
            ("plain-miss", Encoding.UTF8.GetBytes(new string('x', 8192))),
            ("collision-miss", Encoding.UTF8.GetBytes(new string('n', 8192))),
            ("late-sparse", Encoding.UTF8.GetBytes(new string('n', 8186) + "needle")),
            ("early-collision", Encoding.UTF8.GetBytes("needle" + new string('n', 8186))),
            ("mixed-dense", Encoding.UTF8.GetBytes("needle" + new string('a', 8186))),
        };

        WriteMeasurementEnvironment();
        Console.WriteLine($"Pattern            : {pattern}");
        Console.WriteLine($"Iterations         : {iterations}");
        Console.WriteLine($"Samples            : {samples}");
        Console.WriteLine();

        foreach (var benchmarkCase in cases)
        {
            var oracleInput = Encoding.UTF8.GetString(benchmarkCase.Input);
            var expected = Regex.Count(oracleInput, pattern, RegexOptions.CultureInvariant);
            var ordinary = new Utf8Regex(pattern, RegexOptions.CultureInvariant);
            var compiled = new Utf8Regex(pattern, RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var finite = new Utf8Regex(
                pattern,
                RegexOptions.CultureInvariant | RegexOptions.Compiled,
                TimeSpan.FromSeconds(10));
            var pcre2 = new Utf8Pcre2Regex(pattern);

            AssertSelectorResult(benchmarkCase.Name, "ordinary", expected, ordinary.Count(benchmarkCase.Input));
            AssertSelectorResult(benchmarkCase.Name, "compiled", expected, compiled.Count(benchmarkCase.Input));
            AssertSelectorResult(benchmarkCase.Name, "finite", expected, finite.Count(benchmarkCase.Input));
            AssertSelectorResult(benchmarkCase.Name, "pcre2", expected, pcre2.Count(benchmarkCase.Input));

            Console.WriteLine($"Case               : {benchmarkCase.Name} ({benchmarkCase.Input.Length} B, count={expected})");
            Measure("Ordinary", samples, iterations, () => ordinary.Count(benchmarkCase.Input));
            Measure("Compiled", samples, iterations, () => compiled.Count(benchmarkCase.Input));
            Measure("Finite compiled", samples, iterations, () => finite.Count(benchmarkCase.Input));
            Measure("PCRE2", samples, iterations, () => pcre2.Count(benchmarkCase.Input));
            var allocationIterations = Math.Min(iterations, 32);
            Console.WriteLine(
                $"Warm allocation    : ordinary={MeasureAllocatedBytesPerInvocation(allocationIterations, () => ordinary.Count(benchmarkCase.Input))} B, " +
                $"compiled={MeasureAllocatedBytesPerInvocation(allocationIterations, () => compiled.Count(benchmarkCase.Input))} B, " +
                $"finite={MeasureAllocatedBytesPerInvocation(allocationIterations, () => finite.Count(benchmarkCase.Input))} B, " +
                $"pcre2={MeasureAllocatedBytesPerInvocation(allocationIterations, () => pcre2.Count(benchmarkCase.Input))} B");
            Console.WriteLine();
        }

        return 0;
    }

    private static void AssertSelectorResult(string caseName, string lane, int expected, int actual)
    {
        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"Selector case '{caseName}' produced {actual} in the {lane} lane; expected {expected}.");
        }
    }
}
