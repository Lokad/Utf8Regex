namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal readonly struct Utf8AnalyzedRegex
{
    public Utf8AnalyzedRegex(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        NativeExecutionKind executionKind,
        AsciiSimplePatternPlan simplePatternPlan = default,
        AsciiStructuralIdentifierFamilyPlan asyncIdentifierFamilyPlan = default,
        AsciiStructuralTokenWindowPlan tokenWindowPlan = default,
        AsciiStructuralRepeatedSegmentPlan repeatedSegmentPlan = default,
        AsciiStructuralQuotedRelationPlan quotedRelationPlan = default,
        AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan = default,
        byte[]? literalUtf8 = null,
        string? fallbackReason = null,
        Utf8FallbackDirectFamilyPlan fallbackDirectFamily = default)
    {
        SemanticRegex = semanticRegex;
        ExecutionPattern = executionPattern;
        Features = features;
        SearchInfo = searchInfo;
        ExecutionKind = executionKind;
        SimplePatternPlan = simplePatternPlan;
        StructuralIdentifierFamilyPlan = asyncIdentifierFamilyPlan;
        StructuralTokenWindowPlan = tokenWindowPlan;
        StructuralRepeatedSegmentPlan = repeatedSegmentPlan;
        StructuralQuotedRelationPlan = quotedRelationPlan;
        OrderedLiteralWindowPlan = orderedLiteralWindowPlan;
        LiteralUtf8 = literalUtf8;
        FallbackReason = fallbackReason;
        FallbackDirectFamily = fallbackDirectFamily;
    }

    public Utf8SemanticRegex SemanticRegex { get; }

    public string ExecutionPattern { get; }

    public Utf8RegexFeatures Features { get; }

    public Utf8AnalyzedSearchInfo SearchInfo { get; }

    public NativeExecutionKind ExecutionKind { get; }

    public AsciiSimplePatternPlan SimplePatternPlan { get; }

    public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan { get; }

    public AsciiStructuralTokenWindowPlan StructuralTokenWindowPlan { get; }

    public AsciiStructuralRepeatedSegmentPlan StructuralRepeatedSegmentPlan { get; }

    public AsciiStructuralQuotedRelationPlan StructuralQuotedRelationPlan { get; }

    public AsciiOrderedLiteralWindowPlan OrderedLiteralWindowPlan { get; }

    public byte[]? LiteralUtf8 { get; }

    public string? FallbackReason { get; }

    public Utf8FallbackDirectFamilyPlan FallbackDirectFamily { get; }
}

