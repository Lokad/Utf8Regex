namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndAnalyzer
{
    public static Utf8RegexAnalysis Analyze(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions)
    {
        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex);
        // The native analyzer admits NonBacktracking only for literal shapes.
        // Other shapes retain their existing unsupported-options migration route.
        if ((executionOptions & RegexOptions.NonBacktracking) != 0 &&
            !features.HasLoops && !features.HasLookarounds && !features.HasAtomicGroups && !features.HasBackreferences && !features.HasConditionals &&
            Utf8NativeExecutionAnalyzer.TryAnalyze(semanticRegex, executionPattern, executionOptions, out var nativeNonBacktracking) &&
            Lokad.Utf8Regex.Internal.Execution.Utf8SliceParser.TryParse(executionOptions & ~RegexOptions.NonBacktracking,
                semanticRegex.RuntimeTree) is { HasSharedCaptureDeclarations: false, HasBalancing: false } nativeUnitProof &&
            nativeUnitProof.IgnoreCase == ((executionOptions & RegexOptions.IgnoreCase) != 0))
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
