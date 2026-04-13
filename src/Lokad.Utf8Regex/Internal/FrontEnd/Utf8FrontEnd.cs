namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEnd
{
    public static Utf8FrontEndAnalysis Analyze(string pattern, RegexOptions options)
    {
        var effectiveOptions = options;
        var executionPattern = Runtime.RegexParser.NormalizeLeadingGlobalOptions(pattern, ref effectiveOptions);
        var runtimeTree = Utf8RuntimeTreeProvider.TryParse(pattern, options);
        var runtimeAnalysis = runtimeTree is null ? null : Runtime.RegexTreeAnalyzer.Analyze(runtimeTree);
        var semanticRegex = new Utf8SemanticRegex(
            pattern,
            options,
            Utf8SemanticSource.FallbackRegex,
            executionPattern,
            effectiveOptions,
            runtimeTree,
            runtimeAnalysis);
        var analyzedRegex = Utf8FrontEndAnalyzer.Analyze(semanticRegex, executionPattern, effectiveOptions);
        var semanticSource = analyzedRegex.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals => Utf8SemanticSource.NativeLiteral,
            NativeExecutionKind.AsciiSimplePattern or NativeExecutionKind.AsciiStructuralIdentifierFamily or NativeExecutionKind.AsciiStructuralTokenWindow or NativeExecutionKind.AsciiStructuralRepeatedSegment or NativeExecutionKind.AsciiStructuralQuotedRelation => Utf8SemanticSource.NativeSimplePattern,
            _ => Utf8SemanticSource.FallbackRegex,
        };
        if (semanticSource != semanticRegex.Source)
        {
            semanticRegex = new Utf8SemanticRegex(
                pattern,
                options,
                semanticSource,
                executionPattern,
                effectiveOptions,
                runtimeTree,
                runtimeAnalysis);
            analyzedRegex = new Utf8AnalyzedRegex(
                semanticRegex,
                analyzedRegex.ExecutionPattern,
                analyzedRegex.Features,
                analyzedRegex.SearchInfo,
                analyzedRegex.ExecutionKind,
                analyzedRegex.SimplePatternPlan,
                analyzedRegex.StructuralIdentifierFamilyPlan,
                analyzedRegex.StructuralTokenWindowPlan,
                analyzedRegex.StructuralRepeatedSegmentPlan,
                analyzedRegex.StructuralQuotedRelationPlan,
                analyzedRegex.OrderedLiteralWindowPlan,
                analyzedRegex.LiteralUtf8,
                analyzedRegex.FallbackReason,
                analyzedRegex.FallbackDirectFamily);
        }

        analyzedRegex = new Utf8AnalyzedRegex(
            analyzedRegex.SemanticRegex,
            analyzedRegex.ExecutionPattern,
            analyzedRegex.Features,
            analyzedRegex.SearchInfo,
            analyzedRegex.ExecutionKind,
            analyzedRegex.SimplePatternPlan,
            analyzedRegex.StructuralIdentifierFamilyPlan,
            analyzedRegex.StructuralTokenWindowPlan,
            analyzedRegex.StructuralRepeatedSegmentPlan,
            analyzedRegex.StructuralQuotedRelationPlan,
            analyzedRegex.OrderedLiteralWindowPlan,
            analyzedRegex.LiteralUtf8,
            analyzedRegex.FallbackReason,
            Utf8FallbackRegexFamilyAnalyzer.Classify(analyzedRegex));

        if (analyzedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            string.Equals(analyzedRegex.FallbackReason, "unsupported_loop", StringComparison.Ordinal) &&
            analyzedRegex.FallbackDirectFamily.HasValue &&
            analyzedRegex.FallbackDirectFamily.SupportsNativeFallbackRoute)
        {
            analyzedRegex = new Utf8AnalyzedRegex(
                analyzedRegex.SemanticRegex,
                analyzedRegex.ExecutionPattern,
                analyzedRegex.Features,
                analyzedRegex.SearchInfo,
                analyzedRegex.ExecutionKind,
                analyzedRegex.SimplePatternPlan,
                analyzedRegex.StructuralIdentifierFamilyPlan,
                analyzedRegex.StructuralTokenWindowPlan,
                analyzedRegex.StructuralRepeatedSegmentPlan,
                analyzedRegex.StructuralQuotedRelationPlan,
                analyzedRegex.OrderedLiteralWindowPlan,
                analyzedRegex.LiteralUtf8,
                fallbackReason: null,
                analyzedRegex.FallbackDirectFamily);
        }

        var regexPlan = Utf8FrontEndLowerer.Lower(analyzedRegex);

        return new Utf8FrontEndAnalysis(
            semanticRegex,
            analyzedRegex,
            regexPlan);
    }
}

