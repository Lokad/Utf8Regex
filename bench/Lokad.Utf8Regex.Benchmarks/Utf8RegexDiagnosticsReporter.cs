namespace Lokad.Utf8Regex.Benchmarks;

internal static class Utf8RegexDiagnosticsReporter
{
    public static int RunIsMatch(string caseId)
    {
        var benchmarkCase = Utf8RegexBenchmarkCatalog.Get(caseId);
        var context = new Utf8RegexBenchmarkContext(benchmarkCase);
        var diagnostics = context.Utf8Regex.CollectIsMatchDiagnostics(context.InputBytes);

        Console.WriteLine($"CaseId            : {caseId}");
        Console.WriteLine($"Pattern           : {benchmarkCase.Pattern}");
        Console.WriteLine($"ExecutionKind     : {diagnostics.ExecutionKind}");
        Console.WriteLine($"SearchKind        : {diagnostics.SearchKind}");
        Console.WriteLine($"VerifierMode      : {diagnostics.FallbackVerifierMode}");
        Console.WriteLine($"VerifierNeedsEnd  : {diagnostics.RequiresCandidateEndCoverage}");
        Console.WriteLine($"Result            : {diagnostics.Result}");
        Console.WriteLine($"SearchCandidates  : {diagnostics.SearchCandidates}");
        Console.WriteLine($"FixedCheckRejects : {diagnostics.FixedCheckRejects}");
        Console.WriteLine($"VerifierInvokes   : {diagnostics.VerifierInvocations}");
        Console.WriteLine($"VerifierMatches   : {diagnostics.VerifierMatches}");
        Console.WriteLine($"ProbeWindows      : {diagnostics.PrefilterWindows}");
        Console.WriteLine($"ProbeSkipped      : {diagnostics.PrefilterSkippedWindows}");
        Console.WriteLine($"ProbePromoted     : {diagnostics.PrefilterPromotedWindows}");
        Console.WriteLine($"ProbeSkippedBytes : {diagnostics.PrefilterSkippedBytes}");
        Console.WriteLine($"ProbePromotedBytes: {diagnostics.PrefilterPromotedBytes}");
        Console.WriteLine($"EngineDemotions   : {diagnostics.EngineDemotions}");
        return 0;
    }
}
