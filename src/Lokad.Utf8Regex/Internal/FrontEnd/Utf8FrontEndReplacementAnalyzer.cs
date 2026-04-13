namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

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
        var pattern = RuntimeFrontEnd.RegexParser.ParseReplacement(replacement);
        var resolvedPattern = pattern.ResolveGroupReferences(validGroupNumbers, validGroupNames);
        var plan = Utf8ReplacementPlanLowerer.Lower(resolvedPattern);
        if (pattern.TryGetLiteralText(validGroupNumbers, validGroupNames, out var literalText))
        {
            return new Utf8AnalyzedReplacement(replacement, pattern, plan, Encoding.UTF8.GetBytes(literalText));
        }

        return new Utf8AnalyzedReplacement(replacement, pattern, plan);
    }
}
