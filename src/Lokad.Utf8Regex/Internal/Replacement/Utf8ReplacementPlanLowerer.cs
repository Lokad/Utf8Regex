namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ReplacementPlanLowerer
{
    public static Utf8ReplacementPlan Lower(Utf8ReplacementPattern pattern)
    {
        return Lower(pattern, [], []);
    }

    public static Utf8ReplacementPlan Lower(
        Utf8ReplacementPattern pattern,
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames)
    {
        var instructions = new List<Utf8ReplacementInstruction>(pattern.Tokens.Length);
        foreach (var token in pattern.Tokens)
        {
            instructions.Add(LowerToken(token, validGroupNumbers, validGroupNames));
        }

        return new Utf8ReplacementPlan(instructions);
    }

    private static Utf8ReplacementInstruction LowerToken(
        Utf8ReplacementToken token,
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames)
    {
        return token.Kind switch
        {
            Utf8ReplacementTokenKind.Literal => Utf8ReplacementInstruction.Literal(
                token.Literal ?? string.Empty),
            Utf8ReplacementTokenKind.Group => Utf8ReplacementInstruction.Group(
                ResolveGroupNumber(token, validGroupNumbers, validGroupNames),
                token.IsBraceEnclosed),
            Utf8ReplacementTokenKind.WholeMatch => Utf8ReplacementInstruction.WholeMatch(),
            Utf8ReplacementTokenKind.LeftPortion => Utf8ReplacementInstruction.LeftPortion(),
            Utf8ReplacementTokenKind.RightPortion => Utf8ReplacementInstruction.RightPortion(),
            Utf8ReplacementTokenKind.LastGroup => Utf8ReplacementInstruction.LastGroup(),
            Utf8ReplacementTokenKind.WholeString => Utf8ReplacementInstruction.WholeString(),
            _ => throw new InvalidOperationException("Unsupported replacement token kind."),
        };
    }

    private static int ResolveGroupNumber(
        Utf8ReplacementToken token,
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames)
    {
        if (token.GroupNumber >= 0)
        {
            return token.GroupNumber;
        }

        var groupName = token.GroupName;
        if (groupName is null)
        {
            return -1;
        }

        for (var i = 0; i < validGroupNames.Length && i < validGroupNumbers.Length; i++)
        {
            if (string.Equals(validGroupNames[i], groupName, StringComparison.Ordinal))
            {
                return validGroupNumbers[i];
            }
        }

        return -1;
    }
}
