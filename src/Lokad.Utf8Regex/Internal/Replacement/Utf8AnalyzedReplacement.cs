namespace Lokad.Utf8Regex.Internal.Replacement;

internal readonly struct Utf8AnalyzedReplacement
{
    private Utf8AnalyzedReplacement(
        Utf8ReplacementPattern pattern,
        Utf8ReplacementPlan plan,
        byte[] literalUtf8,
        bool isLiteral)
    {
        Plan = plan;
        LiteralUtf8 = literalUtf8;
        IsLiteral = isLiteral;
        ContainsSubstitutions = pattern.ContainsSubstitutions;
        ContainsGroupReferences = pattern.ContainsGroupReferences;
        ContainsNamedGroups = pattern.ContainsNamedGroups;
        ContainsSpecialSubstitutions = pattern.ContainsSpecialSubstitutions;
    }

    public Utf8ReplacementPlan Plan { get; }

    public byte[] LiteralUtf8 { get; }

    public bool IsLiteral { get; }

    public bool ContainsSubstitutions { get; }

    public bool ContainsGroupReferences { get; }

    public bool ContainsNamedGroups { get; }

    public bool ContainsSpecialSubstitutions { get; }

    public static Utf8AnalyzedReplacement Literal(
        Utf8ReplacementPattern pattern,
        Utf8ReplacementPlan plan,
        byte[] literalUtf8) =>
        new(pattern, plan, literalUtf8, isLiteral: true);

    public static Utf8AnalyzedReplacement Structured(
        Utf8ReplacementPattern pattern,
        Utf8ReplacementPlan plan) =>
        new(pattern, plan, [], isLiteral: false);
}
