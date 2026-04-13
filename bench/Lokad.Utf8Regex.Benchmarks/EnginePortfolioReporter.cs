using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Benchmarks;

internal static class EnginePortfolioReporter
{
    public static int Run()
    {
        WriteUtf8RegexCatalog();
        Console.WriteLine();
        WriteDotNetPerformanceReplicaCatalog();
        return 0;
    }

    private static void WriteUtf8RegexCatalog()
    {
        Console.WriteLine("Utf8Regex Benchmark Catalog");
        Console.WriteLine("---------------------------");

        foreach (var benchmarkCase in Utf8RegexBenchmarkCatalog.GetAllCases()
                     .OrderBy(static c => c.Operation)
                     .ThenBy(static c => c.Family)
                     .ThenBy(static c => c.Id))
        {
            var context = new Utf8RegexBenchmarkContext(benchmarkCase);
            if (benchmarkCase.Operation == Utf8RegexBenchmarkOperation.IsMatch)
            {
                var diagnostics = context.Utf8Regex.CollectIsMatchDiagnostics(context.InputBytes);
                Console.WriteLine(
                    $"{benchmarkCase.Id} | {benchmarkCase.Operation} | Engine={context.Utf8Regex.CompiledEngineKind} | " +
                    $"Exec={diagnostics.ExecutionKind} | Search={diagnostics.SearchKind} | " +
                    $"{DescribeVerifier(context.Utf8Regex)} | " +
                    $"Fallback={context.Utf8Regex.FallbackReason ?? "<native>"}");
            }
            else if (benchmarkCase.Operation == Utf8RegexBenchmarkOperation.Count)
            {
                var diagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);
                Console.WriteLine(
                    $"{benchmarkCase.Id} | {benchmarkCase.Operation} | Engine={context.Utf8Regex.CompiledEngineKind} | " +
                    $"Exec={diagnostics.ExecutionKind} | Search={diagnostics.SearchKind} | " +
                    $"{DescribeVerifier(context.Utf8Regex)} | " +
                    $"Fallback={context.Utf8Regex.FallbackReason ?? "<native>"}");
            }
            else
            {
                Console.WriteLine(
                    $"{benchmarkCase.Id} | {benchmarkCase.Operation} | Engine={context.Utf8Regex.CompiledEngineKind} | " +
                    $"{DescribeVerifier(context.Utf8Regex)} | " +
                    $"Fallback={context.Utf8Regex.FallbackReason ?? "<native>"}");
            }
        }
    }

    private static void WriteDotNetPerformanceReplicaCatalog()
    {
        Console.WriteLine("Rebar Replica Count Catalog");
        Console.WriteLine("--------------------------");

        foreach (var benchmarkCase in DotNetPerformanceReplicaBenchmarkCatalog.GetAllCases()
                     .Where(static c => c.Model == DotNetPerformanceReplicaBenchmarkModel.Count)
                     .OrderBy(static c => c.Group)
                     .ThenBy(static c => c.Id))
        {
            try
            {
                var context = new DotNetPerformanceReplicaBenchmarkContext(benchmarkCase);
                var diagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);

                Console.WriteLine(
                    $"{benchmarkCase.Id} | Engine={context.Utf8Regex.CompiledEngineKind} | " +
                    $"Exec={diagnostics.ExecutionKind} | Search={diagnostics.SearchKind} | " +
                    $"{DescribeVerifier(context.Utf8Regex)} | " +
                    $"Prefilter={(context.Utf8Regex.SearchPlan.RequiredPrefilterSearcher.HasValue ? context.Utf8Regex.SearchPlan.RequiredPrefilterSearcher.Kind : "None")} | " +
                    $"Fallback={context.Utf8Regex.FallbackReason ?? "<native>"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{benchmarkCase.Id} | ERROR={ex.GetType().Name} | {ex.Message}");
            }
        }
    }

    private static string DescribeVerifier(Utf8Regex regex)
    {
        if (regex.CompiledEngineKind == Utf8CompiledEngineKind.ByteSafeLinear)
        {
            if (regex.StructuralVerifierPlan.ByteSafeLazyDfaProgram.HasValue)
            {
                return "Verifier=CompiledByteSafeLazyDfa";
            }

            return regex.StructuralVerifierPlan.ByteSafeLinearProgram.HasValue
                ? $"Verifier=CompiledByteSafeLinear, LazyDfaReject={Utf8ByteSafeLazyDfaVerifierProgram.GetCompileFailureKind(regex.StructuralVerifierPlan.ByteSafeLinearProgram)}"
                : "Verifier=CompatByteSafeLinear";
        }

        return $"Verifier={regex.StructuralVerifierPlan.Kind}";
    }
}
