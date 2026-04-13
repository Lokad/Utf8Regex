using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

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
            RuntimeFrontEnd.RegexReplacementTokenKind.Literal => new Utf8ReplacementInstruction(
                Utf8ReplacementInstructionKind.Literal,
                token.Literal is null ? [] : Encoding.UTF8.GetBytes(token.Literal)),
            RuntimeFrontEnd.RegexReplacementTokenKind.Group => new Utf8ReplacementInstruction(
                Utf8ReplacementInstructionKind.Group,
                GroupNumber: ResolveGroupNumber(token, validGroupNumbers, validGroupNames),
                IsBraceEnclosed: token.IsBraceEnclosed),
            RuntimeFrontEnd.RegexReplacementTokenKind.WholeMatch => new Utf8ReplacementInstruction(Utf8ReplacementInstructionKind.WholeMatch),
            RuntimeFrontEnd.RegexReplacementTokenKind.LeftPortion => new Utf8ReplacementInstruction(Utf8ReplacementInstructionKind.LeftPortion),
            RuntimeFrontEnd.RegexReplacementTokenKind.RightPortion => new Utf8ReplacementInstruction(Utf8ReplacementInstructionKind.RightPortion),
            RuntimeFrontEnd.RegexReplacementTokenKind.LastGroup => new Utf8ReplacementInstruction(Utf8ReplacementInstructionKind.LastGroup),
            RuntimeFrontEnd.RegexReplacementTokenKind.WholeString => new Utf8ReplacementInstruction(Utf8ReplacementInstructionKind.WholeString),
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
