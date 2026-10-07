namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Checks the owned regular grammar before planning or lowering.
/// Captures cannot affect acceptance in this mode; all capture readers and
/// constructs needing backtracking or origin state are rejected here.</summary>
internal static class Utf8NonBacktrackingGrammar
{
    internal static string? FindUnsupportedConstruct(Utf8TreeNode root)
    {
        var pending = new Stack<Utf8TreeNode>();
        pending.Push(root);
        while (pending.TryPop(out var node))
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
            for (var index = 0; index < node.ChildCount; index++) pending.Push(node.Child(index));
        }
        return null;
    }
}
