using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    public static int RunMeasurePcre2EnumeratorPhases(
        string caseId,
        string? iterationsText,
        string? samplesText)
    {
        var benchmarkCase = Utf8Pcre2BenchmarkCatalog.Get(caseId);
        if ((benchmarkCase.SupportedOperations & Utf8Pcre2BenchmarkOperation.EnumerateMatches) == 0)
        {
            Console.Error.WriteLine($"Case '{caseId}' does not support match enumeration.");
            return 1;
        }

        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
        var matchCount = context.Utf8Pcre2Regex.Count(context.InputBytes);
        var enumeratedCount = ExecutePcre2PublicEnumeratorMoveNextCount(
            context.Utf8Pcre2Regex,
            context.InputBytes);
        if (enumeratedCount != matchCount)
        {
            throw new InvalidOperationException(
                $"PCRE2 enumerator-phase count mismatch for {caseId}: " +
                $"Count={matchCount}; enumerated={enumeratedCount}.");
        }

        using var processorSet = BenchmarkProcessorScope.EnterHighestEfficiencyClass();
        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"Matches           : {matchCount}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"CPU set           : {processorSet.Description}");
        Console.WriteLine("Protocol          : adjacent same-trip managed replays; alternating order");
        Console.WriteLine();
        Console.WriteLine("| Transition | Before us | After us | Paired delta us | After/Before |");
        Console.WriteLine("|---|---:|---:|---:|---:|");

        WritePcre2EnumeratorPhasePair(
            "Count -> completed MoveNext",
            () => context.Utf8Pcre2Regex.Count(context.InputBytes),
            () => ExecutePcre2PublicEnumeratorMoveNextCount(context.Utf8Pcre2Regex, context.InputBytes),
            iterations,
            samples);

        Func<int> previous = () => context.Utf8Pcre2Regex.DebugEnumeratePublicConstructionOnly(
            context.InputBytes,
            0);
        var previousLabel = "construct";
        foreach (var prefixLength in GetPcre2EnumeratorPrefixLengths(matchCount))
        {
            var capturedPrefixLength = prefixLength;
            Func<int> current = () => ExecutePcre2PublicEnumeratorMoveNextPrefix(
                context.Utf8Pcre2Regex,
                context.InputBytes,
                capturedPrefixLength);
            var currentLabel = capturedPrefixLength == matchCount
                ? "all successful MoveNext"
                : $"first {capturedPrefixLength} successful MoveNext";
            WritePcre2EnumeratorPhasePair(
                $"{previousLabel} -> {currentLabel}",
                previous,
                current,
                iterations,
                samples);
            previous = current;
            previousLabel = currentLabel;
        }

        Func<int> completed = () => ExecutePcre2PublicEnumeratorMoveNextCount(
            context.Utf8Pcre2Regex,
            context.InputBytes);
        WritePcre2EnumeratorPhasePair(
            $"{previousLabel} -> terminal MoveNext",
            previous,
            completed,
            iterations,
            samples);

        Func<int> currentStarts = () => ExecutePcre2PublicEnumeratorCurrentStartSum(
            context.Utf8Pcre2Regex,
            context.InputBytes);
        WritePcre2EnumeratorPhasePair(
            "completed MoveNext -> Current start",
            completed,
            currentStarts,
            iterations,
            samples);
        WritePcre2EnumeratorPhasePair(
            "Current start -> full range sink",
            currentStarts,
            () => ExecutePcre2PublicEnumeratorRangeSink(context.Utf8Pcre2Regex, context.InputBytes),
            iterations,
            samples);

        return 0;
    }

    private static IEnumerable<int> GetPcre2EnumeratorPrefixLengths(int matchCount)
    {
        if (matchCount <= 0)
        {
            yield break;
        }

        yield return 1;
        if (matchCount > 1)
        {
            yield return 2;
        }

        if (matchCount > 2)
        {
            yield return matchCount;
        }
    }

    private static int ExecutePcre2PublicEnumeratorMoveNextPrefix(
        Utf8Pcre2Regex regex,
        byte[] input,
        int prefixLength)
    {
        var count = 0;
        var enumerator = regex.EnumerateMatches(input);
        while (count < prefixLength && enumerator.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static void WritePcre2EnumeratorPhasePair(
        string transition,
        Func<int> before,
        Func<int> after,
        int iterations,
        int samples)
    {
        var pair = MeasurePcre2DiagnosticPair(before, after, iterations, samples);
        Console.WriteLine(
            $"| {transition} | {pair.FirstMicroseconds:F3} | " +
            $"{pair.SecondMicroseconds:F3} | " +
            $"{pair.MedianSecondMinusFirstMicroseconds:+0.000;-0.000;0.000} | " +
            $"{pair.SecondMicroseconds / pair.FirstMicroseconds:F2}x |");
    }
}
