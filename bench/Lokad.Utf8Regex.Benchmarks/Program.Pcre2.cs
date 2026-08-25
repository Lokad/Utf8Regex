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

        if (args.Length >= 2 && args[0].Equals("--verify-pcre2-comparator-case", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunVerifyPcre2ComparatorCase(args[1]);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pcre2-native-buffer-cost", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMeasurePcre2NativeBufferCost(
                args[1],
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pcre2-native-auto-possess-cost", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMeasurePcre2NativeAutoPossessCost(
                args[1],
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pcre2-match-count-scaling", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMeasurePcre2MatchCountScaling(
                args[1],
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--verify-pcre2-qualification-consistency", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunVerifyPcre2QualificationConsistency();
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--qualify-pcre2-comparator-case", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunQualifyPcre2ComparatorCase(
                args[1],
                args.Length >= 3 ? args[2] : null);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--qualify-pcre2-comparator-case-reversed", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunQualifyPcre2ComparatorCase(
                args[1],
                args.Length >= 3 ? args[2] : null,
                comparatorFirst: true);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pcre2-workspace-pool-cost", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMeasurePcre2WorkspacePoolCost(
                args[1],
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--measure-pcre2-vm-metering-cost", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMeasurePcre2VmMeteringCost(
                args[1],
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
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

        if (args.Length >= 1 && args[0].Equals("--migrate-pcre2-benchmark-json", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMigratePcre2BenchmarkJson();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pcre2-benchmark-markdown", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunEmitPcre2BenchmarkMarkdown();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--rewrite-pcre2-benchmark-markdown", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunRewritePcre2BenchmarkMarkdown();
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--verify-pcre2-benchmark-markdown", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunVerifyPcre2BenchmarkMarkdown();
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

        if (args.Length >= 1 && args[0].Equals("--refresh-pcre2-scaling-families", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunRefreshPcre2ScalingFamilies(
                args.Length >= 2 ? args[1] : null,
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--refresh-pcre2-native-baselines", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunRefreshPcre2NativeBaselines(
                args.Length >= 2 ? args[1] : null,
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 2 && args[0].Equals("--refresh-pcre2-native-baseline-case", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunRefreshPcre2NativeBaselineCase(
                args[1],
                args.Length >= 3 ? args[2] : null,
                args.Length >= 4 ? args[3] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--measure-literal-family-selector", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunMeasureLiteralFamilySelector(
                args.Length >= 2 ? args[1] : null,
                args.Length >= 3 ? args[2] : null);
            return true;
        }

        if (args.Length >= 1 && args[0].Equals("--emit-pcre2-priority-report", StringComparison.Ordinal))
        {
            exitCode = BenchmarkInspectReporter.RunEmitPcre2PriorityReport(
                args.Length >= 2 ? args[1] : null);
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
