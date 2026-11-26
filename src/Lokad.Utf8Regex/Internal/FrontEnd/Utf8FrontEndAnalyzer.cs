using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndAnalyzer
{
    public static Utf8RegexAnalysis Analyze(Utf8SemanticRegex semanticRegex)
    {
        var executionOptions = semanticRegex.ExecutionOptions;
        var features = semanticRegex.RuntimeTree?.Features ?? default;
        // Flat regular graphs can use the admitted native literal plans.
        if ((executionOptions & RegexOptions.NonBacktracking) != 0 &&
            Lokad.Utf8Regex.Internal.Execution.Utf8SliceParser.IsNonBacktrackingFlatGraph(semanticRegex.RuntimeTree) &&
            Utf8NativeExecutionAnalyzer.TryAnalyze(semanticRegex, features, out var nativeNonBacktracking) &&
            (semanticRegex.RuntimeTree is { UsesIgnoreCase: true } || (executionOptions & RegexOptions.IgnoreCase) != 0) ==
                ((executionOptions & RegexOptions.IgnoreCase) != 0))
        {
            return nativeNonBacktracking;
        }
        if (Utf8RegexSyntax.SupportsNativeOptions(executionOptions) &&
            Utf8NativeExecutionAnalyzer.TryAnalyze(semanticRegex, features, out var analyzedRegex))
        {
            return analyzedRegex;
        }

        var searchFacts = Utf8FrontEndSearchAnalyzer.Analyze(semanticRegex);
        return Utf8RegexAnalysis.Create(
            semanticRegex,
            features,
            searchFacts,
            NativeExecutionKind.FallbackRegex);
    }
}
