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
        using var owner = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(owner.Budget);
        var result = Lower(storage, pattern.Tokens, validGroupNumbers, validGroupNames);
        storage.Complete(); owner.Complete();
        return result;
    }

    internal static Utf8ReplacementPlan Lower(Utf8ConstructionSession storage, ReadOnlySpan<Utf8ReplacementToken> tokens,
        ReadOnlySpan<int> validGroupNumbers, ReadOnlySpan<string> validGroupNames)
    {
        var instructions = storage.Buffer<Utf8ReplacementInstruction>();
        foreach (var token in tokens)
        {
            storage.Budget.ChargeWork(1);
            instructions.Add(LowerToken(storage, token, validGroupNumbers, validGroupNames));
        }
        return Utf8ReplacementPlan.Create(storage, instructions.Span);
    }

    private static Utf8ReplacementInstruction LowerToken(
        Utf8ConstructionSession storage,
        Utf8ReplacementToken token,
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames)
    {
        return token.Kind switch
        {
            Utf8ReplacementTokenKind.Literal => Utf8ReplacementInstruction.Literal(
                storage, token.Literal ?? string.Empty),
            Utf8ReplacementTokenKind.Group => Utf8ReplacementInstruction.Group(
                ResolveGroupNumber(storage, token, validGroupNumbers, validGroupNames)),
            Utf8ReplacementTokenKind.WholeMatch => Utf8ReplacementInstruction.WholeMatch(),
            Utf8ReplacementTokenKind.LeftPortion => Utf8ReplacementInstruction.LeftPortion(),
            Utf8ReplacementTokenKind.RightPortion => Utf8ReplacementInstruction.RightPortion(),
            Utf8ReplacementTokenKind.LastGroup => Utf8ReplacementInstruction.LastGroup(),
            Utf8ReplacementTokenKind.WholeString => Utf8ReplacementInstruction.WholeString(),
            _ => throw new InvalidOperationException("Unsupported replacement token kind."),
        };
    }

    private static int ResolveGroupNumber(
        Utf8ConstructionSession storage,
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
            storage.Budget.ChargeWork(1 + (ulong)Math.Min(validGroupNames[i]?.Length ?? 0, groupName.Length));
            if (string.Equals(validGroupNames[i], groupName, StringComparison.Ordinal))
            {
                return validGroupNumbers[i];
            }
        }

        return -1;
    }
}
