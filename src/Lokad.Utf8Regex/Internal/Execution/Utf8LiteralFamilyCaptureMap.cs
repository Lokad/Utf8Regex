using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

// Branch-to-group projection for literal alternatives wrapped in groups. Owned
// discovery supplies the winning branch; unsupported shapes use scalar capture
// execution. Every collected capture spans its complete branch match.
internal sealed class Utf8LiteralFamilyCaptureMap
{
    private Utf8LiteralFamilyCaptureMap(int[] wholeMatchGroups, int[][] branchGroups)
    {
        WholeMatchGroups = wholeMatchGroups;
        BranchGroups = branchGroups;
    }

    public int[] WholeMatchGroups { get; }
    public int[][] BranchGroups { get; }

    public static Utf8LiteralFamilyCaptureMap? TryCreate(Utf8ExecutionTree? tree)
    {
        if (tree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root || root.Children.Count != 1)
            return null;

        using var construction = Utf8ConstructionScope.Enter();
        using var storage = Utf8CaptureMapConstruction.Create(construction.Budget);
        var whole = storage.Buffer<int>();
        var node = Unwrap(root.Children[0], whole, storage.Budget);
        int[][] branches;
        if (node.Kind == Utf8ExecutionNodeKind.Alternate && node.Children.Count > 0)
        {
            branches = storage.Allocate<int[]>(node.Children.Count);
            for (var index = 0; index < node.Children.Count; index++)
            {
                storage.Budget.ChargeWork(1);
                var found = storage.Buffer<int>();
                if (!IsLiteralRun(Unwrap(node.Children[index], found, storage.Budget), storage.Budget)) return null;
                branches[index] = storage.Copy(found);
            }
        }
        else
        {
            if (!IsLiteralRun(node, storage.Budget)) return null;
            branches = storage.Allocate<int[]>(1);
            branches[0] = [];
        }

        var groups = storage.Copy(whole);
        var result = storage.Budget.AllocateObject((groups, branches), static state =>
            new Utf8LiteralFamilyCaptureMap(state.groups, state.branches));
        storage.Complete();
        construction.Complete();
        return result;
    }

    private static Utf8ExecutionNode Unwrap(Utf8ExecutionNode node, Utf8ConstructionBuffer<int> groups, Utf8ConstructionBudget budget)
    {
        while (true)
        {
            budget.ChargeWork(1);
            if (node.Kind == Utf8ExecutionNodeKind.Group && node.Children.Count == 1)
                node = node.Children[0];
            else if (node.Kind == Utf8ExecutionNodeKind.Capture && node.CaptureNumber > 0 && node.Children.Count == 1)
            {
                groups.Add(node.CaptureNumber);
                node = node.Children[0];
            }
            else return node;
        }
    }

    private struct Frame(Utf8ExecutionNode node)
    {
        internal readonly Utf8ExecutionNode Node = node;
        internal int Index;
    }

    private static bool IsLiteralRun(Utf8ExecutionNode node, Utf8ConstructionBudget budget)
    {
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var current = frame.Node;
            if (current.Kind is Utf8ExecutionNodeKind.One or Utf8ExecutionNodeKind.Multi or Utf8ExecutionNodeKind.Set)
            {
                frames.Pop();
                continue;
            }
            if (current.Kind != Utf8ExecutionNodeKind.Concatenate || current.Children.Count == 0) return false;
            if (frame.Index == current.Children.Count) { frames.Pop(); continue; }
            var child = current.Children[frame.Index++];
            frames[index] = frame;
            frames.Push(new(child));
        }
        return true;
    }
}
