using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
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
