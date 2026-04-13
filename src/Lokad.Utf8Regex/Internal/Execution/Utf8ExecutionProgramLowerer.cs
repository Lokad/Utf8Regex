namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ExecutionProgramLowerer
{
    public static Utf8ExecutionProgram? Lower(Utf8ExecutionTree? tree)
    {
        if (tree is null)
        {
            return null;
        }

        var instructions = new List<Utf8ExecutionInstruction>();
        AppendNode(tree.Root, instructions);
        return new Utf8ExecutionProgram(instructions);
    }

    private static void AppendNode(Utf8ExecutionNode node, List<Utf8ExecutionInstruction> instructions)
    {
        var enterIndex = instructions.Count;
        instructions.Add(new Utf8ExecutionInstruction(
            Utf8ExecutionInstructionKind.Enter,
            node.Kind,
            node.Options,
            node.Text,
            node.Ch,
            node.CaptureNumber,
            node.Min,
            node.Max));

        foreach (var child in node.Children)
        {
            AppendNode(child, instructions);
        }

        var exitIndex = instructions.Count;
        instructions.Add(new Utf8ExecutionInstruction(
            Utf8ExecutionInstructionKind.Exit,
            node.Kind,
            node.Options,
            node.Text,
            node.Ch,
            node.CaptureNumber,
            node.Min,
            node.Max));

        instructions[enterIndex] = instructions[enterIndex] with { PartnerIndex = exitIndex };
        instructions[exitIndex] = instructions[exitIndex] with { PartnerIndex = enterIndex };
    }
}
