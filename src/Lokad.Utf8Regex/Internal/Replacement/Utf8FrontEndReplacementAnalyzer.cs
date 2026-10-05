namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8FrontEndReplacementAnalyzer
{
    public static Utf8AnalyzedReplacement Analyze(string replacement)
    {
        return Analyze(replacement, [], []);
    }

    public static Utf8AnalyzedReplacement Analyze(
        string replacement,
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames)
        => Analyze(replacement, validGroupNumbers, validGroupNames, false);

    internal static Utf8AnalyzedReplacement Analyze(string replacement, ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames, bool ecma)
    {
        var pattern = Utf8ReplacementPattern.Parse(replacement, validGroupNumbers, ecma);
        var resolvedPattern = pattern.ResolveGroupReferences(validGroupNumbers, validGroupNames);
        var plan = Utf8ReplacementPlanLowerer.Lower(resolvedPattern);
        if (pattern.TryGetLiteralText(validGroupNumbers, validGroupNames, out var literalText))
        {
            return Utf8AnalyzedReplacement.Literal(replacement, pattern, plan, Encoding.UTF8.GetBytes(literalText));
        }

        return Utf8AnalyzedReplacement.Structured(replacement, pattern, plan);
    }
}
