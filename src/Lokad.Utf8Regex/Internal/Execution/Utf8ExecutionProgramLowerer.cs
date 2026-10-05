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
        var frames = new Stack<(Utf8ExecutionNode Node, int EnterIndex, int NextChild)>();
        frames.Push((tree.Root, -1, 0));
        while (frames.TryPop(out var frame))
        {
            var node = frame.Node;
            var enterIndex = frame.EnterIndex;
            if (enterIndex < 0)
            {
                enterIndex = instructions.Count;
                instructions.Add(new Utf8ExecutionInstruction(
                    Utf8ExecutionInstructionKind.Enter, node.Kind, node.Options,
                    node.Text, node.Ch, node.CaptureNumber, node.Min, node.Max, -1));
            }
            if (frame.NextChild < node.Children.Count)
            {
                frames.Push((node, enterIndex, frame.NextChild + 1));
                frames.Push((node.Children[frame.NextChild], -1, 0));
                continue;
            }

            var exitIndex = instructions.Count;
            instructions.Add(new Utf8ExecutionInstruction(
                Utf8ExecutionInstructionKind.Exit, node.Kind, node.Options,
                node.Text, node.Ch, node.CaptureNumber, node.Min, node.Max, enterIndex));
            instructions[enterIndex] = instructions[enterIndex] with { PartnerIndex = exitIndex };
        }
        return new Utf8ExecutionProgram(instructions);
    }
}
