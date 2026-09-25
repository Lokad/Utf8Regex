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
    {
        var pattern = Utf8ReplacementPattern.Parse(replacement);
        var resolvedPattern = pattern.ResolveGroupReferences(validGroupNumbers, validGroupNames);
        var plan = Utf8ReplacementPlanLowerer.Lower(resolvedPattern);
        if (pattern.TryGetLiteralText(validGroupNumbers, validGroupNames, out var literalText))
        {
            return Utf8AnalyzedReplacement.Literal(replacement, pattern, plan, Encoding.UTF8.GetBytes(literalText));
        }

        return Utf8AnalyzedReplacement.Structured(replacement, pattern, plan);
    }
}
