namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Checks the owned regular grammar before planning or lowering.
/// Captures cannot affect acceptance in this mode; all capture readers and
/// constructs needing backtracking or origin state are rejected here.</summary>
internal static class Utf8NonBacktrackingGrammar
{
    private struct Frame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int Index;
        internal bool Entered;
    }

    internal static string? FindUnsupportedConstruct(Utf8TreeNode root)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(root));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var node = frame.Node;
            if (!frame.Entered)
            {
                var unsupported = node.Kind switch
                {
                    Utf8TreeNodeKind.Backreference => "backreferences",
                    Utf8TreeNodeKind.Atomic => "atomic groups",
                    Utf8TreeNodeKind.PositiveLookaround or Utf8TreeNodeKind.NegativeLookaround => "lookarounds",
                    Utf8TreeNodeKind.BackreferenceConditional or Utf8TreeNodeKind.ExpressionConditional => "conditionals",
                    Utf8TreeNodeKind.Start => "contiguous anchors",
                    Utf8TreeNodeKind.Capture when node.N >= 0 => "balancing captures",
                    _ => null,
                };
                if (unsupported is not null) return unsupported;
                frame.Entered = true;
                frame.Index = node.ChildCount;
            }
            if (frame.Index == 0) { frames.Pop(); continue; }
            // Preserve the existing reverse-source diagnostic precedence.
            var child = node.Child(--frame.Index);
            frames[index] = frame;
            frames.Push(new(child));
        }
        return null;
    }
}
