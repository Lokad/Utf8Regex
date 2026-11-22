namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ReplacementPlanLowerer
{
    // The analyzer has already validated/resolved group references.
    internal static Utf8ReplacementPlan Lower(Utf8ConstructionSession storage, ReadOnlySpan<Utf8ReplacementToken> tokens,
        int wholeLiteralCaptureNumber)
    {
        var instructions = storage.Buffer<Utf8ReplacementInstruction>();
        foreach (var token in tokens)
        {
            storage.Budget.ChargeWork(1);
            instructions.Add(LowerToken(token, wholeLiteralCaptureNumber));
        }
        return Utf8ReplacementPlan.Create(storage, instructions.Span);
    }

    private static Utf8ReplacementInstruction LowerToken(
        Utf8ReplacementToken token, int wholeLiteralCaptureNumber)
    {
        return token.Kind switch
        {
            Utf8ReplacementTokenKind.Literal => Utf8ReplacementInstruction.Literal(token.LiteralBytes!),
            // The capture encloses the entire consuming path, so the same
            // instruction works for both the range and the scalar writers.
            Utf8ReplacementTokenKind.Group when wholeLiteralCaptureNumber > 0 && token.GroupNumber == wholeLiteralCaptureNumber
                => Utf8ReplacementInstruction.WholeMatch(),
            Utf8ReplacementTokenKind.Group => Utf8ReplacementInstruction.Group(
                token.GroupNumber),
            Utf8ReplacementTokenKind.WholeMatch => Utf8ReplacementInstruction.WholeMatch(),
            Utf8ReplacementTokenKind.LeftPortion => Utf8ReplacementInstruction.LeftPortion(),
            Utf8ReplacementTokenKind.RightPortion => Utf8ReplacementInstruction.RightPortion(),
            Utf8ReplacementTokenKind.LastGroup => Utf8ReplacementInstruction.LastGroup(),
            Utf8ReplacementTokenKind.WholeString => Utf8ReplacementInstruction.WholeString(),
            _ => throw new InvalidOperationException("Unsupported replacement token kind."),
        };
    }
}
