using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ExecutionTreeLowerer
{
    public static Utf8ExecutionTree? Lower(Utf8SemanticRegex semanticRegex)
    {
        return semanticRegex.RuntimeTree?.Root is { } root
            ? new Utf8ExecutionTree(LowerNode(root))
            : null;
    }

    private static Utf8ExecutionNode LowerNode(Utf8TreeNode node)
    {
        var children = node.ChildCount == 0
            ? []
            : node.ChildList.Select(LowerNode).ToArray();

        return new Utf8ExecutionNode(
            MapKind(node.Kind),
            node.Options,
            children,
            node.Str ?? string.Empty,
            node.Ch,
            GetCaptureNumber(node),
            node.M,
            node.N);
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
