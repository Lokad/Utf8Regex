namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal readonly struct Utf8AnalyzedReplacement
{
    public Utf8AnalyzedReplacement(
        string originalText,
        RuntimeFrontEnd.RegexReplacementPattern pattern,
        Utf8ReplacementPlan plan,
        byte[]? literalUtf8 = null)
    {
        OriginalText = originalText;
        Pattern = pattern;
        Plan = plan;
        LiteralUtf8 = literalUtf8;
    }

    public string OriginalText { get; }

    public RuntimeFrontEnd.RegexReplacementPattern Pattern { get; }

    public Utf8ReplacementPlan Plan { get; }

    public byte[]? LiteralUtf8 { get; }

    public bool IsLiteral => LiteralUtf8 is not null;

    public bool ContainsSubstitutions => Pattern.ContainsSubstitutions;

    public bool ContainsGroupReferences => Pattern.ContainsGroupReferences;

    public bool ContainsNamedGroups => Pattern.ContainsNamedGroups;

    public bool ContainsSpecialSubstitutions => Pattern.ContainsSpecialSubstitutions;
}
