using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal readonly struct Utf8RegexAnalysis
{
    private Utf8RegexAnalysis(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        NativeExecutionKind executionKind,
        AsciiSimplePatternPlan simplePatternPlan,
        AsciiStructuralIdentifierFamilyPlan asyncIdentifierFamilyPlan,
        AsciiStructuralTokenWindowPlan tokenWindowPlan,
        AsciiStructuralRepeatedSegmentPlan repeatedSegmentPlan,
        AsciiStructuralQuotedRelationPlan quotedRelationPlan,
        AsciiOrderedLiteralWindowPlan orderedLiteralWindowPlan,
        byte[]? literalUtf8,
        string? fallbackReason)
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

    public static Utf8RegexAnalysis Create(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        NativeExecutionKind executionKind)
        => new(semanticRegex, executionPattern, features, searchInfo, executionKind, default, default, default, default, default, default, null, null);

    public static Utf8RegexAnalysis CreateFallback(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        string fallbackReason)
        => new(semanticRegex, executionPattern, features, searchInfo, NativeExecutionKind.FallbackRegex, default, default, default, default, default, default, null, fallbackReason);

    public static Utf8RegexAnalysis CreateLiteral(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        NativeExecutionKind executionKind,
        byte[] literalUtf8)
        => new(semanticRegex, executionPattern, features, searchInfo, executionKind, default, default, default, default, default, default, literalUtf8, null);

    public static Utf8RegexAnalysis CreateSimple(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        AsciiSimplePatternPlan simplePatternPlan)
        => new(semanticRegex, executionPattern, features, searchInfo, NativeExecutionKind.AsciiSimplePattern, simplePatternPlan, default, default, default, default, default, null, null);

    public static Utf8RegexAnalysis CreateSimple(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        AsciiSimplePatternPlan simplePatternPlan,
        byte[] literalUtf8)
        => new(semanticRegex, executionPattern, features, searchInfo, NativeExecutionKind.AsciiSimplePattern, simplePatternPlan, default, default, default, default, default, literalUtf8, null);

    public static Utf8RegexAnalysis CreateStructuralIdentifier(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        AsciiStructuralIdentifierFamilyPlan plan)
        => new(semanticRegex, executionPattern, features, searchInfo, NativeExecutionKind.AsciiStructuralIdentifierFamily, default, plan, default, default, default, default, null, null);

    public static Utf8RegexAnalysis CreateStructuralTokenWindow(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        AsciiStructuralTokenWindowPlan plan,
        byte[] literalUtf8)
        => new(semanticRegex, executionPattern, features, searchInfo, NativeExecutionKind.AsciiStructuralTokenWindow, default, default, plan, default, default, default, literalUtf8, null);

    public static Utf8RegexAnalysis CreateStructuralRepeatedSegment(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        AsciiStructuralRepeatedSegmentPlan plan)
        => new(semanticRegex, executionPattern, features, searchInfo, NativeExecutionKind.AsciiStructuralRepeatedSegment, default, default, default, plan, default, default, null, null);

    public static Utf8RegexAnalysis CreateStructuralQuotedRelation(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        AsciiStructuralQuotedRelationPlan plan)
        => new(semanticRegex, executionPattern, features, searchInfo, NativeExecutionKind.AsciiStructuralQuotedRelation, default, default, default, default, plan, default, null, null);

    public static Utf8RegexAnalysis CreateOrderedLiteralWindow(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        AsciiOrderedLiteralWindowPlan plan)
        => new(semanticRegex, executionPattern, features, searchInfo, NativeExecutionKind.AsciiOrderedLiteralWindow, default, default, default, default, default, plan, null, null);

    public static Utf8RegexAnalysis CreateOrderedLiteralWindow(
        Utf8SemanticRegex semanticRegex,
        string executionPattern,
        Utf8RegexFeatures features,
        Utf8AnalyzedSearchInfo searchInfo,
        AsciiOrderedLiteralWindowPlan plan,
        byte[] literalUtf8)
        => new(semanticRegex, executionPattern, features, searchInfo, NativeExecutionKind.AsciiOrderedLiteralWindow, default, default, default, default, default, plan, literalUtf8, null);
}
