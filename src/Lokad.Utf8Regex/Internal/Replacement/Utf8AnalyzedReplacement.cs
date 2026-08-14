namespace Lokad.Utf8Regex.Internal.Replacement;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal readonly struct Utf8AnalyzedReplacement
{
    private Utf8AnalyzedReplacement(
        string originalText,
        RuntimeFrontEnd.RegexReplacementPattern pattern,
        Utf8ReplacementPlan plan,
        byte[] literalUtf8,
        bool isLiteral)
    {
        OriginalText = originalText;
        Pattern = pattern;
        Plan = plan;
        LiteralUtf8 = literalUtf8;
        IsLiteral = isLiteral;
    }

    public string OriginalText { get; }

    public RuntimeFrontEnd.RegexReplacementPattern Pattern { get; }

    public Utf8ReplacementPlan Plan { get; }

    public byte[] LiteralUtf8 { get; }

    public bool IsLiteral { get; }

    public bool ContainsSubstitutions => Pattern.ContainsSubstitutions;

    public bool ContainsGroupReferences => Pattern.ContainsGroupReferences;

    public bool ContainsNamedGroups => Pattern.ContainsNamedGroups;

    public bool ContainsSpecialSubstitutions => Pattern.ContainsSpecialSubstitutions;

    public static Utf8AnalyzedReplacement Literal(
        string originalText,
        RuntimeFrontEnd.RegexReplacementPattern pattern,
        Utf8ReplacementPlan plan,
        byte[] literalUtf8) =>
        new(originalText, pattern, plan, literalUtf8, isLiteral: true);

    public static Utf8AnalyzedReplacement Structured(
        string originalText,
        RuntimeFrontEnd.RegexReplacementPattern pattern,
        Utf8ReplacementPlan plan) =>
        new(originalText, pattern, plan, [], isLiteral: false);
}
