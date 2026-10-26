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
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(owner.Budget);
        var pattern = Utf8ReplacementPattern.Parse(storage, replacement, validGroupNumbers, ecma);
        var resolved = pattern.ResolveTokens(storage, validGroupNumbers, validGroupNames);
        var plan = Utf8ReplacementPlanLowerer.Lower(storage, resolved.Span);
        // Resolution merges adjacent literals. Reuse the admitted encoded fragment
        // instead of resolving, materializing and encoding the template a second time.
        var literal = plan.Instructions.Count == 0 ||
            (plan.Instructions.Count == 1 && plan.Instructions[0].Kind == Utf8ReplacementInstructionKind.Literal);
        var result = literal
            ? Utf8AnalyzedReplacement.Literal(replacement, pattern, plan,
                plan.Instructions.Count == 0 ? [] : plan.Instructions[0].LiteralUtf8)
            : Utf8AnalyzedReplacement.Structured(replacement, pattern, plan);
        storage.Complete(); owner.Complete();
        return result;
    }
}
