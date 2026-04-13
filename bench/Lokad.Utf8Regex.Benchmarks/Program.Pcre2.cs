namespace Lokad.Utf8Regex.Benchmarks;

internal static partial class BenchmarkProgramRouter
{
    public static bool TryHandlePcre2Command(string[] args, out int exitCode)
    {
        if (args.Length >= 2 && args[0].Equals("--inspect-pcre2-case", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunInspectUtf8Pcre2Case(args[1]);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pcre2-case", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMeasureUtf8Pcre2Case(args[1], args.Length >= 3 ? args[2] : null, args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pcre2-compatible-case", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMeasureUtf8Pcre2CompatibleCase(args[1], args.Length >= 3 ? args[2] : null, args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pcre2-special-case", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMeasureUtf8Pcre2SpecialCase(args[1], args.Length >= 3 ? args[2] : null, args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pcre2-perf-ledger", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunEmitUtf8Pcre2PerfLedger(args.Length >= 2 ? args[1] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pcre2-managed-perf-ledger", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunEmitUtf8Pcre2ManagedPerfLedger(args.Length >= 2 ? args[1] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pcre2-benchmark-json", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunEmitPcre2BenchmarkJson();
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--refresh-pcre2-benchmark-case", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunRefreshPcre2BenchmarkCase(
                args[1],
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--refresh-pcre2-benchmarks", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunRefreshPcre2Benchmarks(
                args.Length >= 2 ? args[1] : null,
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pcre2-priority-report", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunEmitPcre2PriorityReport();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pcre2-translation-report", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunEmitPcre2TranslationReport();
            return true;
        }

        exitCode = 0;
        return false;
    }

    public static bool ShouldUseInProcessForPcre2SpecialBenchmarks(string[] arguments)
    {
        foreach (var argument in arguments)
        {
            if (argument.Contains("Utf8Pcre2SpecialReplaceBenchmarks", StringComparison.Ordinal) ||
                argument.Contains("Utf8Pcre2SpecialEnumerateBenchmarks", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
