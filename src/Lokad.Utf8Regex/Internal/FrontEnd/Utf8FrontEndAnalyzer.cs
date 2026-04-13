namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndAnalyzer
{
    public static Utf8AnalyzedRegex Analyze(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions)
    {
        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex);
        var unsupportedOptions = Utf8RegexSyntax.ClassifyUnsupportedOptions(executionOptions);
        if (unsupportedOptions is not null)
        {
            return new Utf8AnalyzedRegex(
                semanticRegex,
                executionPattern,
                features,
                Utf8FrontEndSearchAnalyzer.Analyze(semanticRegex),
                NativeExecutionKind.FallbackRegex,
                simplePatternPlan: default,
                asyncIdentifierFamilyPlan: default,
                tokenWindowPlan: default,
                repeatedSegmentPlan: default,
                literalUtf8: null,
                fallbackReason: unsupportedOptions);
        }

        if (Utf8NativeExecutionAnalyzer.TryAnalyze(semanticRegex, executionPattern, executionOptions, out var analyzedRegex))
        {
            return analyzedRegex;
        }

        var searchInfo = Utf8FrontEndSearchAnalyzer.Analyze(semanticRegex);
        var fallbackReason = Utf8FallbackReasonClassifier.Classify("unsupported_pattern", features);

        return new Utf8AnalyzedRegex(
            semanticRegex,
            executionPattern,
            features,
            searchInfo,
            NativeExecutionKind.FallbackRegex,
            simplePatternPlan: default,
            asyncIdentifierFamilyPlan: default,
            tokenWindowPlan: default,
            repeatedSegmentPlan: default,
            literalUtf8: null,
            fallbackReason: fallbackReason);
    }
}
