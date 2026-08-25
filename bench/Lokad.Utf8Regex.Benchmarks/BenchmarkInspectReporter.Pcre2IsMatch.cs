using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Pcre2;

namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkInspectReporter
{
    public static int RunMeasurePcre2IsMatchScaling(
        string familyName,
        string? iterationsText,
        string? samplesText)
    {
        var iterations = ParseIterations(iterationsText);
        var samples = ParseSamples(samplesText);
        var (pattern, inputFactory) = familyName switch
        {
            "email" => (
                @"^([a-zA-Z0-9_\-\.]+)@((\[[0-9]{1,3}\.[0-9]{1,3}\.[0-9]{1,3}\.)|(([a-zA-Z0-9\-]+\.)+))([a-zA-Z]{2,12}|[0-9]{1,3})(\]?)$",
                (Func<int, bool, string>)CreatePcre2EmailScalingInput),
            "uri" => (
                @"[\w]+://[^/\s?#]+[^\s?#]+(?:\?[^\s#]*)?(?:#[^\s]*)?",
                CreatePcre2UriScalingInput),
            "finite-backref" => (
                @"(?|(abc)|(xyz))\1",
                static (int size, bool isMatch) =>
                    (isMatch ? "abcabc" : "abcxyz") + new string('q', size - 6)),
            _ => throw new InvalidOperationException(
                $"Unknown PCRE2 IsMatch scaling family '{familyName}'. " +
                "Expected email, uri, or finite-backref."),
        };
        using var processorSet = Pcre2QualificationProcessorSet.Enter();

        Console.WriteLine($"Family            : {familyName}");
        Console.WriteLine($"Pattern           : {pattern}");
        Console.WriteLine($"Iterations        : {iterations}");
        Console.WriteLine($"Samples           : {samples}");
        Console.WriteLine($"CPU set           : {processorSet.Description}");
        Console.WriteLine("Protocol          : same-size hit/miss subjects; same-trip paired lanes; alternating order");
        Console.WriteLine();
        Console.WriteLine("| Bytes | Outcome | Candidates | VM steps | Tokens | Direct scans | Scan chars | Pool rents | Managed us | Native us | M/N |");
        Console.WriteLine("|---:|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

        foreach (var size in new[] { 16, 32, 64, 128, 256, 512 })
        {
            foreach (var expectedMatch in new[] { true, false })
            {
                var input = inputFactory(size, expectedMatch);
                var benchmarkCase = new Utf8Pcre2BenchmarkCase(
                    $"diagnostic/{familyName}/{size}/{(expectedMatch ? "hit" : "miss")}",
                    pattern,
                    input,
                    RegexOptions.None,
                    default,
                    supportedOperations: Utf8Pcre2BenchmarkOperation.IsMatch,
                    supportedBackends: Utf8Pcre2BenchmarkBackend.Pcre2Only);
                var context = new Utf8Pcre2BenchmarkContext(benchmarkCase);
                using var baseline = new PcreNetNativeBenchmarkBaseline(benchmarkCase);
                var managedResult = context.Utf8Pcre2Regex.IsMatch(context.InputBytes);
                var managedChecksum = ComputePcre2ManagedResultChecksum(
                    context.Utf8Pcre2Regex,
                    context.InputBytes,
                    Utf8Pcre2BenchmarkOperation.IsMatch);
                var nativeChecksum = baseline.ComputeChecksum(Utf8Pcre2BenchmarkOperation.IsMatch);
                if (managedResult != expectedMatch || managedChecksum != nativeChecksum)
                {
                    throw new InvalidOperationException(
                        $"PCRE2 IsMatch scaling mismatch for {familyName}, {size} bytes, " +
                        $"expected={expectedMatch}: managed={managedResult}; " +
                        $"managed checksum={managedChecksum}; native checksum={nativeChecksum}.");
                }

                var diagnostics = context.Utf8Pcre2Regex.DebugIsMatchWithDiagnostics(
                    context.InputBytes,
                    0).Execution;
                var pair = MeasurePcre2DiagnosticPair(
                    () => context.Utf8Pcre2Regex.IsMatch(context.InputBytes) ? 1 : 0,
                    () => baseline.Execute(Utf8Pcre2BenchmarkOperation.IsMatch),
                    iterations,
                    samples);

                Console.WriteLine(
                    $"| {context.InputBytes.Length} | {(expectedMatch ? "hit" : "miss")} | " +
                    $"{diagnostics.CandidateAttempts:N0} | {diagnostics.BacktrackingSteps:N0} | " +
                    $"{diagnostics.VmTokenSteps:N0} | {diagnostics.VmPossessiveTokenScanSteps:N0} | " +
                    $"{diagnostics.VmPossessiveTokenScanCharacters:N0} | {diagnostics.WorkspacePoolRents:N0} | " +
                    $"{pair.FirstMicroseconds:F3} | " +
                    $"{pair.SecondMicroseconds:F3} | " +
                    $"{pair.FirstMicroseconds / pair.SecondMicroseconds:F2}x |");
            }
        }

        return 0;
    }

    private static string CreatePcre2EmailScalingInput(int size, bool isMatch)
    {
        const string hitSuffix = "@b.co";
        const string missSuffix = "@b.co#";
        var suffix = isMatch ? hitSuffix : missSuffix;
        return new string('a', size - suffix.Length) + suffix;
    }

    private static string CreatePcre2UriScalingInput(int size, bool isMatch)
    {
        const string hitSuffix = "://a/b";
        const string missSuffix = "://a";
        var suffix = isMatch ? hitSuffix : missSuffix;
        return new string('a', size - suffix.Length) + suffix;
    }

}
