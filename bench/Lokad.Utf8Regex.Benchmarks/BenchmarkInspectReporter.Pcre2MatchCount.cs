using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    public static int RunMeasurePcre2MatchCountScaling(
        string familyName,
        string? iterationsText,
        string? samplesText)
    {
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var (pattern, matchToken, missToken, regexOptions, compileSettings) = familyName switch
        {
            "literal" => ("foo", "foo", "fxx", RegexOptions.CultureInvariant, default(Utf8Pcre2CompileSettings)),
            "character" => ("[0-9]", "7", "x", RegexOptions.CultureInvariant, default(Utf8Pcre2CompileSettings)),
            "duplicate-names" => (
                @"(?:(?<n>foo)|(?<n>bar))\k<n>",
                "foofoo",
                "foobar",
                RegexOptions.CultureInvariant,
                new Utf8Pcre2CompileSettings { AllowDuplicateNames = true }),
            "branch-reset" => (
                @"(?|(?'a'aaa)|(?'a'b))(?'a'cccc)\k'a'",
                "aaaccccaaa",
                "aaaccccbbb",
                RegexOptions.CultureInvariant,
                new Utf8Pcre2CompileSettings { AllowDuplicateNames = true }),
            "multiline-prefix" => (
                "^ERROR: .+$",
                "ERROR: event\n",
                "INFO: event\n",
                RegexOptions.CultureInvariant | RegexOptions.Multiline,
                default(Utf8Pcre2CompileSettings)),
            _ => throw new InvalidOperationException(
                $"Unknown PCRE2 match-count family '{familyName}'. " +
                "Expected literal, character, duplicate-names, branch-reset, or multiline-prefix."),
        };
        const int slotCount = 64;
        var stride = Math.Max(matchToken.Length, missToken.Length) + 1;
        var inputLength = Math.Max(512, checked(slotCount * stride));
        using var processorSet = Pcre2QualificationProcessorSet.Enter();

        Console.WriteLine($"Family            : {familyName}");
        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"InputBytes        : {inputLength}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"CPU set           : {processorSet.Description}");
        Console.WriteLine("Protocol          : fixed-size input; same-trip paired lanes; alternating order");
        Console.WriteLine();
        Console.WriteLine("| Matches | Candidates | VM steps | Pool rents | Managed IsMatch us | Native IsMatch us | M/N | Managed Count us | Native Count us | M/N | Managed Enumerate us | Native Enumerate us | M/N | Managed MatchMany us | Native MatchMany us | M/N |");
        Console.WriteLine("|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach (var requestedMatchCount in new[] { 0, 1, 8, 64 })
        {
            string input;
            if (familyName == "multiline-prefix")
            {
                var lines = new StringBuilder(inputLength);
                for (var slot = 0; slot < slotCount; slot++)
                {
                    lines.Append(slot < requestedMatchCount ? matchToken : missToken);
                }

                input = lines.ToString();
            }
            else
            {
                var inputCharacters = new string('x', inputLength).ToCharArray();
                for (var slot = 0; slot < slotCount; slot++)
                {
                    var token = slot < requestedMatchCount ? matchToken : missToken;
                    token.CopyTo(0, inputCharacters, slot * stride, token.Length);
                }

                input = new string(inputCharacters);
            }

            var benchmarkCase = new Utf8Pcre2BenchmarkCase(
                $"diagnostic/{familyName}/{requestedMatchCount}",
                pattern,
                input,
                regexOptions,
                compileSettings,
                supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch |
                    Utf8Pcre2BenchmarkOperation.Count |
                    Utf8Pcre2BenchmarkOperation.EnumerateMatches |
                    Utf8Pcre2BenchmarkOperation.MatchMany,
                supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only);
            var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
            using var baseline = new PcreNetNativeBenchmarkBaseline(benchmarkCase);
            var actualMatchCount = context.Utf8Pcre2Regex.Count(context.InputBytes);
            if (actualMatchCount != requestedMatchCount)
            {
                throw new InvalidOperationException(
                    $"PCRE2 match-count scaling expected {requestedMatchCount} matches but found {actualMatchCount}.");
            }

            var diagnostics = context.Utf8Pcre2Regex.DebugCountWithDiagnostics(
                context.InputBytes,
                0).Execution;

            foreach (var operation in new[]
                     {
                         Utf8Pcre2BenchmarkOperation.IsMatch,
                         Utf8Pcre2BenchmarkOperation.Count,
                         Utf8Pcre2BenchmarkOperation.EnumerateMatches,
                         Utf8Pcre2BenchmarkOperation.MatchMany,
                     })
            {
                var managedChecksum = ComputePcre2ManagedResultChecksum(
                    context.Utf8Pcre2Regex,
                    context.InputBytes,
                    operation);
                var nativeChecksum = baseline.ComputeChecksum(operation);
                if (managedChecksum != nativeChecksum)
                {
                    throw new InvalidOperationException(
                        $"PCRE2 match-count scaling checksum mismatch for {familyName}, " +
                        $"{requestedMatchCount} matches, {operation}: managed={managedChecksum}; native={nativeChecksum}.");
                }
            }

            var isMatch = MeasurePcre2DiagnosticPair(
                () => context.Utf8Pcre2Regex.IsMatch(context.InputBytes) ? 1 : 0,
                () => baseline.Execute(Utf8Pcre2BenchmarkOperation.IsMatch),
                iterations,
                samples);
            var count = MeasurePcre2DiagnosticPair(
                () => context.Utf8Pcre2Regex.Count(context.InputBytes),
                () => baseline.Execute(Utf8Pcre2BenchmarkOperation.Count),
                iterations,
                samples);
            var enumerate = MeasurePcre2DiagnosticPair(
                () => ExecutePcre2PublicEnumeratorRangeSink(context.Utf8Pcre2Regex, context.InputBytes),
                () => baseline.Execute(Utf8Pcre2BenchmarkOperation.EnumerateMatches),
                iterations,
                samples);
            var matchMany = MeasurePcre2DiagnosticPair(
                () => ExecutePcre2MatchManyRangeSink(context.Utf8Pcre2Regex, context.InputBytes),
                () => baseline.Execute(Utf8Pcre2BenchmarkOperation.MatchMany),
                iterations,
                samples);

            Console.WriteLine(
                $"| {actualMatchCount} | {diagnostics.CandidateAttempts:N0} | " +
                $"{diagnostics.BacktrackingSteps:N0} | {diagnostics.WorkspacePoolRents:N0} | " +
                $"{isMatch.FirstMicroseconds:F3} | {isMatch.SecondMicroseconds:F3} | " +
                $"{isMatch.FirstMicroseconds / isMatch.SecondMicroseconds:F2}x | " +
                $"{count.FirstMicroseconds:F3} | {count.SecondMicroseconds:F3} | " +
                $"{count.FirstMicroseconds / count.SecondMicroseconds:F2}x | " +
                $"{enumerate.FirstMicroseconds:F3} | {enumerate.SecondMicroseconds:F3} | " +
                $"{enumerate.FirstMicroseconds / enumerate.SecondMicroseconds:F2}x | " +
                $"{matchMany.FirstMicroseconds:F3} | {matchMany.SecondMicroseconds:F3} | " +
                $"{matchMany.FirstMicroseconds / matchMany.SecondMicroseconds:F2}x |");
        }

        return 0;
    }

    private static Pcre2DiagnosticPairMeasurement MeasurePcre2DiagnosticPair(
        Func<int> firstAction,
        Func<int> secondAction,
        int iterations,
        int samples)
    {
        var firstWarmup = WarmupCore(firstAction);
        var secondWarmup = WarmupCore(secondAction);
        var firstMicrosecondsBySample = new List<double>(samples);
        var secondMicrosecondsBySample = new List<double>(samples);
        var pairedDeltas = new List<double>(samples);
        var sink = firstWarmup.Sink ^ secondWarmup.Sink;
        for (var sample = 0; sample < samples; sample++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var firstLane = sample % 2 == 0
                ? Pcre2PairLaneOrder.ManagedFirst
                : Pcre2PairLaneOrder.ComparatorFirst;
            var pair = MeasurePcre2QualificationPair(
                firstAction,
                iterations,
                secondAction,
                iterations,
                firstLane);
            var firstMicroseconds = pair.Managed.Elapsed.TotalMicroseconds / iterations;
            var secondMicroseconds = pair.Comparator.Elapsed.TotalMicroseconds / iterations;
            firstMicrosecondsBySample.Add(firstMicroseconds);
            secondMicrosecondsBySample.Add(secondMicroseconds);
            pairedDeltas.Add(secondMicroseconds - firstMicroseconds);
            sink ^= pair.Managed.Sink ^ pair.Comparator.Sink;
        }

        GC.KeepAlive(sink);
        return new Pcre2DiagnosticPairMeasurement(
            Median(firstMicrosecondsBySample),
            Median(secondMicrosecondsBySample),
            Median(pairedDeltas));
    }

    private readonly record struct Pcre2DiagnosticPairMeasurement(
        double FirstMicroseconds,
        double SecondMicroseconds,
        double MedianSecondMinusFirstMicroseconds);
}
