namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal readonly struct Utf8FrontEndAnalysis
{
    public Utf8FrontEndAnalysis(
        Utf8SemanticRegex semanticRegex,
        Utf8AnalyzedRegex analyzedRegex,
        Utf8RegexPlan regexPlan)
    {
        SemanticRegex = semanticRegex;
        AnalyzedRegex = analyzedRegex;
        RegexPlan = regexPlan;
    }

    public Utf8SemanticRegex SemanticRegex { get; }

    public Utf8AnalyzedRegex AnalyzedRegex { get; }

    public Utf8RegexPlan RegexPlan { get; }

    public NativeExecutionKind ExecutionKind => RegexPlan.ExecutionKind;

    public Utf8SearchPlan SearchPlan => RegexPlan.SearchPlan;

    public AsciiSimplePatternPlan SimplePatternPlan => RegexPlan.SimplePatternPlan;

    public byte[]? LiteralUtf8 => RegexPlan.LiteralUtf8;

    public string? FallbackReason => RegexPlan.FallbackReason;
}
