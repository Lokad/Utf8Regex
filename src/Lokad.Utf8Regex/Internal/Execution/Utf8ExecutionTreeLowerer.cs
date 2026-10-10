using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ExecutionTreeLowerer
{
    private readonly record struct Frame(Utf8TreeNode Node, int NextChild);

    public static Utf8ExecutionTree? Lower(Utf8SemanticRegex semanticRegex)
    {
        if (semanticRegex.RuntimeTree?.Root is not { } root) return null;
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(root, 0));
        using var nodes = Utf8ConstructionMap<Utf8TreeNode, Utf8ExecutionNode>.Create(budget, ReferenceEqualityComparer.Instance);
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var frameIndex = frames.Count - 1;
            var frame = frames[frameIndex];
            var node = frame.Node;
            if (frame.NextChild < node.ChildCount)
            {
                frames[frameIndex] = frame with { NextChild = frame.NextChild + 1 };
                frames.Push(new(node.Child(frame.NextChild), 0));
                continue;
            }

            var children = budget.AllocateArray<Utf8ExecutionNode>(node.ChildCount, false);
            for (var index = 0; index < children.Length; index++)
            {
                budget.ChargeWork(1);
                children[index] = nodes[node.Child(index)];
            }
            nodes[node] = budget.AllocateObject(
                (Kind: MapKind(node.Kind), node.Options, Children: (IReadOnlyList<Utf8ExecutionNode>)children,
                    Payload: (object?)node.ScalarClass ?? node.Str ?? string.Empty, node.Ch, Capture: GetCaptureNumber(node), node.M, node.N),
                static state => new Utf8ExecutionNode(state.Kind, state.Options, state.Children,
                    state.Payload, state.Ch, state.Capture, state.M, state.N));
            frames.Pop();
        }
        var result = budget.AllocateObject(nodes[root], static node => new Utf8ExecutionTree(node));
        scope.Complete();
        return result;
    }

    private static int GetCaptureNumber(Utf8TreeNode node)
    {
        return node.Kind switch
        {
            Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Backreference => node.M,
            _ => 0,
        };
    }

    private static Utf8ExecutionNodeKind MapKind(Utf8TreeNodeKind kind)
    {
        return kind switch
        {
            Utf8TreeNodeKind.Empty => Utf8ExecutionNodeKind.Empty,
            Utf8TreeNodeKind.One => Utf8ExecutionNodeKind.One,
            Utf8TreeNodeKind.Multi => Utf8ExecutionNodeKind.Multi,
            Utf8TreeNodeKind.Set => Utf8ExecutionNodeKind.Set,
            Utf8TreeNodeKind.Concatenate => Utf8ExecutionNodeKind.Concatenate,
            Utf8TreeNodeKind.Alternate => Utf8ExecutionNodeKind.Alternate,
            Utf8TreeNodeKind.Capture => Utf8ExecutionNodeKind.Capture,
            Utf8TreeNodeKind.Group => Utf8ExecutionNodeKind.Group,
            Utf8TreeNodeKind.Oneloop or
            Utf8TreeNodeKind.Setloop or
            Utf8TreeNodeKind.Loop => Utf8ExecutionNodeKind.Loop,
            Utf8TreeNodeKind.Onelazy or
            Utf8TreeNodeKind.Setlazy or
            Utf8TreeNodeKind.Lazyloop => Utf8ExecutionNodeKind.LazyLoop,
            Utf8TreeNodeKind.Bol => Utf8ExecutionNodeKind.Bol,
            Utf8TreeNodeKind.Eol => Utf8ExecutionNodeKind.Eol,
            Utf8TreeNodeKind.Boundary => Utf8ExecutionNodeKind.Boundary,
            Utf8TreeNodeKind.NonBoundary => Utf8ExecutionNodeKind.NonBoundary,
            Utf8TreeNodeKind.ECMABoundary => Utf8ExecutionNodeKind.ECMABoundary,
            Utf8TreeNodeKind.NonECMABoundary => Utf8ExecutionNodeKind.NonECMABoundary,
            Utf8TreeNodeKind.Beginning => Utf8ExecutionNodeKind.Beginning,
            Utf8TreeNodeKind.Start => Utf8ExecutionNodeKind.Start,
            Utf8TreeNodeKind.EndZ => Utf8ExecutionNodeKind.EndZ,
            Utf8TreeNodeKind.End => Utf8ExecutionNodeKind.End,
            Utf8TreeNodeKind.PositiveLookaround => Utf8ExecutionNodeKind.PositiveLookaround,
            Utf8TreeNodeKind.NegativeLookaround => Utf8ExecutionNodeKind.NegativeLookaround,
            Utf8TreeNodeKind.Atomic => Utf8ExecutionNodeKind.Atomic,
            Utf8TreeNodeKind.Backreference => Utf8ExecutionNodeKind.Backreference,
            _ => Utf8ExecutionNodeKind.Unsupported,
        };
    }
}
