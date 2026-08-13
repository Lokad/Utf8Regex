namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndAnalyzer
{
    public static Utf8RegexAnalysis Analyze(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        RegexOptions executionOptions)
    {
        var features = Utf8RuntimeTreeFeatureAnalyzer.Analyze(semanticRegex);
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

        var searchInfo = Utf8FrontEndSearchAnalyzer.Analyze(semanticRegex);
        var fallbackReason = Utf8FallbackReasonClassifier.Classify("unsupported_pattern", features) ?? "unsupported_pattern";

        return Utf8RegexAnalysis.CreateFallback(
            semanticRegex,
            executionPattern,
            features,
            searchInfo,
            fallbackReason);
    }
}
