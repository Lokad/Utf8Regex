namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndAnalyzer
{
    public static Utf8RegexAnalysis Analyze(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions)
    {
        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex);
        // Native discovery remains limited to admitted literal shapes. Other
        // flat graphs receive their owned scalar program at construction;
        // general graphs retain the unsupported-options migration route.
        if ((executionOptions & RegexOptions.NonBacktracking) != 0 &&
            Lokad.Utf8Regex.Internal.Execution.Utf8SliceParser.TryParseNonBacktrackingFlatGraph(executionOptions,
                semanticRegex.RuntimeTree) is { } nativeScalarProof &&
            Utf8NativeExecutionAnalyzer.TryAnalyze(semanticRegex, executionPattern, executionOptions, out var nativeNonBacktracking) &&
            nativeScalarProof.IgnoreCase == ((executionOptions & RegexOptions.IgnoreCase) != 0))
        {
            return nativeNonBacktracking;
        }
        var unsupportedOptions = Utf8RegexSyntax.ClassifyUnsupportedOptions(executionOptions);
        if (unsupportedOptions is not null)
        {
            return Utf8RegexAnalysis.CreateFallback(
                semanticRegex,
                executionPattern,
                features,
                Utf8FrontEndSearchAnalyzer.Analyze(semanticRegex),
                unsupportedOptions);
        }

        if (Utf8NativeExecutionAnalyzer.TryAnalyze(semanticRegex, executionPattern, executionOptions, out var analyzedRegex))
        {
            return analyzedRegex;
        }

        var searchFacts = Utf8FrontEndSearchAnalyzer.Analyze(semanticRegex);
        var fallbackReason = Utf8FallbackReasonClassifier.Classify("unsupported_pattern", features) ?? "unsupported_pattern";

        return Utf8RegexAnalysis.CreateFallback(
            semanticRegex,
            executionPattern,
            features,
            searchFacts,
            fallbackReason);
    }
}
