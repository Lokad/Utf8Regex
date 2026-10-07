using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ExecutionProgramLowerer
{
    private readonly record struct Frame(Utf8ExecutionNode Node, int EnterIndex, int NextChild);

    public static Utf8ExecutionProgram? Lower(Utf8ExecutionTree? tree)
    {
        if (tree is null)
        {
            return null;
        }

        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(tree.Root, -1, 0));
        using var instructions = Utf8ConstructionBuffer<Utf8ExecutionInstruction>.Create(budget, true);
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var frameIndex = frames.Count - 1;
            var frame = frames[frameIndex];
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
                frames[frameIndex] = new(node, enterIndex, frame.NextChild + 1);
                frames.Push(new(node.Children[frame.NextChild], -1, 0));
                continue;
            }

            var exitIndex = instructions.Count;
            instructions.Add(new Utf8ExecutionInstruction(
                Utf8ExecutionInstructionKind.Exit, node.Kind, node.Options,
                node.Text, node.Ch, node.CaptureNumber, node.Min, node.Max, enterIndex));
            instructions[enterIndex] = instructions[enterIndex] with { PartnerIndex = exitIndex };
            frames.Pop();
        }
        var result = budget.AllocateObject(
            (Instructions: (IReadOnlyList<Utf8ExecutionInstruction>)instructions.Take(), Layout: default(Utf8DeterministicCaptureLayout?)),
            static state => new Utf8ExecutionProgram(state.Instructions));
        scope.Complete();
        return result;
    }
}
