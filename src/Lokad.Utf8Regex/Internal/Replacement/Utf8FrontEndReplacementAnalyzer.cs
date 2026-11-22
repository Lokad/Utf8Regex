namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8FrontEndReplacementAnalyzer
{
    internal static Utf8AnalyzedReplacement Analyze(ReadOnlySpan<byte> replacement, ReadOnlySpan<int> validGroupNumbers = default,
        ReadOnlySpan<string> validGroupNames = default, bool ecma = false, int wholeLiteralCaptureNumber = 0)
    {
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(owner.Budget);
        var pattern = Utf8ReplacementPattern.Parse(storage, replacement, validGroupNumbers, ecma);
        var resolved = pattern.ResolveTokens(storage, validGroupNumbers, validGroupNames);
        var plan = Utf8ReplacementPlanLowerer.Lower(storage, resolved.Span, wholeLiteralCaptureNumber);
        // Resolution merges adjacent literals. Reuse the admitted encoded fragment
        // instead of resolving, materializing and encoding the template a second time.
        var instructions = plan.Instructions;
        var literal = instructions.IsEmpty ||
            (instructions.Length == 1 && instructions[0].Kind == Utf8ReplacementInstructionKind.Literal);
        var result = literal
            ? Utf8AnalyzedReplacement.Literal(pattern, plan,
                instructions.IsEmpty ? [] : instructions[0].LiteralUtf8)
            : Utf8AnalyzedReplacement.Structured(pattern, plan);
        storage.Complete(); owner.Complete();
        return result;
    }
}
