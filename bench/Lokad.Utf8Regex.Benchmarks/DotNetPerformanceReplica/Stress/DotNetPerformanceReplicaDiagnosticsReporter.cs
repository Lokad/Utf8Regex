namespace Lokad.Utf8Regex.Benchmarks;

internal static class DotNetPerformanceReplicaDiagnosticsReporter
{
    public static int RunCount(string caseId)
    {
        var benchmarkCase = DotNetPerformanceReplicaBenchmarkCatalog.Get(caseId);
        var context = new DotNetPerformanceReplicaBenchmarkContext(benchmarkCase);

        var diagnostics = context.Utf8Regex.CollectCountDiagnostics(context.InputBytes);
        var dotnetCount = context.Regex.Count(context.InputString);

        Console.WriteLine($"CaseId            : {benchmarkCase.Id}");
        Console.WriteLine($"Group             : {benchmarkCase.Group}");
        Console.WriteLine($"Model             : {benchmarkCase.Model}");
        Console.WriteLine($"Origin            : {benchmarkCase.Origin}");
        Console.WriteLine($"Pattern           : {context.Pattern}");
        Console.WriteLine($"Options           : {benchmarkCase.Options}");
        Console.WriteLine($"InputChars        : {context.InputString.Length}");
        Console.WriteLine($"InputBytes        : {context.InputBytes.Length}");
        Console.WriteLine($"ExpectedCount     : {benchmarkCase.ExpectedCount?.ToString() ?? "<n/a>"}");
        Console.WriteLine($"Utf8RegexKind     : {diagnostics.ExecutionKind}");
        Console.WriteLine($"Utf8SearchKind    : {diagnostics.SearchKind}");
        Console.WriteLine($"Utf8VerifierMode  : {diagnostics.FallbackVerifierMode}");
        Console.WriteLine($"Utf8VerifierEnd   : {diagnostics.RequiresCandidateEndCoverage}");
        Console.WriteLine($"Utf8PrefilterKind : {context.Utf8Regex.SearchPlan.RequiredPrefilterSearcher.Kind}");
        Console.WriteLine($"Utf8Fallback      : {context.Utf8Regex.FallbackReason ?? "<native>"}");
        Console.WriteLine($"Utf8CountRoute    : {diagnostics.ExecutionRoute}");
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
        Console.WriteLine($"Utf8RegexCount    : {diagnostics.Result}");
        Console.WriteLine($"DotNetRegexCount  : {dotnetCount}");
        return diagnostics.Result == dotnetCount && (benchmarkCase.ExpectedCount is null || diagnostics.Result == benchmarkCase.ExpectedCount.Value) ? 0 : 1;
    }
}
