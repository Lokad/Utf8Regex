using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Planning;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal readonly struct Utf8RegexAnalysis
{
    private Utf8RegexAnalysis(
        Utf8SemanticRegex semanticRegex, string? executionPattern, Utf8RegexFeatures features, Utf8SearchFacts searchFacts, NativeExecutionKind executionKind, byte[]? literalUtf8, string? fallbackReason)
    {
        SemanticRegex = semanticRegex;
        ExecutionPattern = executionPattern;
        Features = features;
        SearchFacts = searchFacts;
        ExecutionKind = executionKind;
        LiteralUtf8 = literalUtf8;
        FallbackReason = fallbackReason;
    }

    public Utf8SemanticRegex SemanticRegex { get; }

    public string? ExecutionPattern { get; }

    public Utf8RegexFeatures Features { get; }

    public Utf8SearchFacts SearchFacts { get; }

    public NativeExecutionKind ExecutionKind { get; }

    public byte[]? LiteralUtf8 { get; }

    public string? FallbackReason { get; }

    public static Utf8RegexAnalysis Create(
        Utf8SemanticRegex semanticRegex, string? executionPattern, Utf8RegexFeatures features, Utf8SearchFacts searchFacts, NativeExecutionKind executionKind)
        => new(semanticRegex, executionPattern, features, searchFacts, executionKind, null, null);

    public static Utf8RegexAnalysis CreateFallback(
        Utf8SemanticRegex semanticRegex, string? executionPattern, Utf8RegexFeatures features, Utf8SearchFacts searchFacts, string fallbackReason)
        => new(semanticRegex, executionPattern, features, searchFacts, NativeExecutionKind.FallbackRegex, null, fallbackReason);

    public static Utf8RegexAnalysis CreateLiteral(
        Utf8SemanticRegex semanticRegex, string? executionPattern, Utf8RegexFeatures features, Utf8SearchFacts searchFacts, NativeExecutionKind executionKind, byte[] literalUtf8)
        => new(semanticRegex, executionPattern, features, searchFacts, executionKind, literalUtf8, null);
}
