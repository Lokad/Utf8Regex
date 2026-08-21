namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ReplacementPlanInterpreter
{
    public static string Apply(Utf8ReplacementPlan plan, Match match, string input)
    {
        if (plan.Instructions.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var instruction in plan.Instructions)
        {
            AppendInstruction(builder, instruction, match, input);
        }

        return builder.ToString();
    }

    private static void AppendInstruction(StringBuilder builder, Utf8ReplacementInstruction instruction, Match match, string input)
    {
        switch (instruction.Kind)
        {
            case Utf8ReplacementInstructionKind.Literal:
                if (instruction.LiteralUtf8.Length > 0)
                {
                    builder.Append(Encoding.UTF8.GetString(instruction.LiteralUtf8));
                }

                return;

            case Utf8ReplacementInstructionKind.Group:
                AppendGroup(builder, instruction, match);
                return;

            case Utf8ReplacementInstructionKind.WholeMatch:
                builder.Append(match.Value);
                return;

            case Utf8ReplacementInstructionKind.LeftPortion:
                builder.Append(input, 0, match.Index);
                return;

            case Utf8ReplacementInstructionKind.RightPortion:
                builder.Append(input, match.Index + match.Length, input.Length - (match.Index + match.Length));
                return;

            case Utf8ReplacementInstructionKind.LastGroup:
                AppendLastGroup(builder, match);
                return;

            case Utf8ReplacementInstructionKind.WholeString:
                builder.Append(input);
                return;

            default:
                throw new InvalidOperationException("Unsupported replacement instruction kind.");
        }
    }

    private static void AppendGroup(StringBuilder builder, Utf8ReplacementInstruction instruction, Match match)
    {
        Group? group = (uint)instruction.GroupNumber < (uint)match.Groups.Count
            ? match.Groups[instruction.GroupNumber]
            : null;

        if (group is { Success: true })
        {
            builder.Append(group.Value);
        }
    }

    private static void AppendLastGroup(StringBuilder builder, Match match)
    {
        if (match.Groups.Count <= 1)
        {
            return;
        }

        var group = match.Groups[match.Groups.Count - 1];
        if (group.Success)
        {
            builder.Append(group.Value);
        }
    }
}
