using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ReplacementPlanLowerer
{
    public static Utf8ReplacementPlan Lower(RuntimeFrontEnd.RegexReplacementPattern pattern)
    {
        return Lower(pattern, [], []);
    }

    public static Utf8ReplacementPlan Lower(
        RuntimeFrontEnd.RegexReplacementPattern pattern,
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
        RuntimeFrontEnd.RegexReplacementToken token,
        ReadOnlySpan<int> validGroupNumbers,
        ReadOnlySpan<string> validGroupNames)
    {
        return token.Kind switch
        {
            RuntimeFrontEnd.RegexReplacementTokenKind.Literal => Utf8ReplacementInstruction.Literal(
                token.Literal is null ? [] : Encoding.UTF8.GetBytes(token.Literal)),
            RuntimeFrontEnd.RegexReplacementTokenKind.Group => Utf8ReplacementInstruction.Group(
                ResolveGroupNumber(token, validGroupNumbers, validGroupNames),
                token.IsBraceEnclosed),
            RuntimeFrontEnd.RegexReplacementTokenKind.WholeMatch => Utf8ReplacementInstruction.WholeMatch(),
            RuntimeFrontEnd.RegexReplacementTokenKind.LeftPortion => Utf8ReplacementInstruction.LeftPortion(),
            RuntimeFrontEnd.RegexReplacementTokenKind.RightPortion => Utf8ReplacementInstruction.RightPortion(),
            RuntimeFrontEnd.RegexReplacementTokenKind.LastGroup => Utf8ReplacementInstruction.LastGroup(),
            RuntimeFrontEnd.RegexReplacementTokenKind.WholeString => Utf8ReplacementInstruction.WholeString(),
            _ => throw new InvalidOperationException("Unsupported replacement token kind."),
        };
    }

    private static int ResolveGroupNumber(
        RuntimeFrontEnd.RegexReplacementToken token,
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
