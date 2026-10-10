namespace Lokad.Utf8Regex.Internal.Replacement;

internal static class Utf8ReplacementPlanInterpreter
{


    public static string ApplyOwned(Utf8ReplacementPlan plan, string input, in Utf8MatchContext context)
    {
        // R05: expands a template against owned group spans projected onto the discovery
        // hit. Unmatched or out-of-range groups contribute nothing.
        if (plan.Instructions.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var instruction in plan.Instructions)
        {
            AppendOwnedInstruction(builder, instruction, input, in context);
        }

        return builder.ToString();
    }

    private static void AppendOwnedInstruction(StringBuilder builder, Utf8ReplacementInstruction instruction, string input, in Utf8MatchContext context)
    {
        switch (instruction.Kind)
        {
            case Utf8ReplacementInstructionKind.Literal:
                if (instruction.LiteralUtf8.Length > 0)
                {
                    builder.Append(instruction.LiteralText);
                }

                return;

            case Utf8ReplacementInstructionKind.Group:
                if (context.TryGetGroup(instruction.GroupNumber, out var group) && group.Success)
                {
                    builder.Append(group.GetValueString());
                }

                return;

            case Utf8ReplacementInstructionKind.WholeMatch:
                builder.Append(context.GetValueString());
                return;

            case Utf8ReplacementInstructionKind.LeftPortion:
                builder.Append(input, 0, context.IndexInUtf16);
                return;

            case Utf8ReplacementInstructionKind.RightPortion:
                builder.Append(input, context.IndexInUtf16 + context.LengthInUtf16, input.Length - (context.IndexInUtf16 + context.LengthInUtf16));
                return;

            case Utf8ReplacementInstructionKind.LastGroup:
                AppendOwnedLastGroup(builder, in context);
                return;

            case Utf8ReplacementInstructionKind.WholeString:
                builder.Append(input);
                return;

            default:
                throw new InvalidOperationException("Unsupported replacement instruction kind.");
        }
    }

    private static void AppendOwnedLastGroup(StringBuilder builder, in Utf8MatchContext context)
    {
        // The last ordinal may have a sparse public number; group zero is last
        // when the pattern has no captures.
        if (context.TryGetGroup(context.OwnedLastGroupNumber, out var group) && group.Success)
        {
            builder.Append(group.GetValueString());
        }
    }







}
