using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8ExecutionTreeLowerer
{
    public static Utf8ExecutionTree? Lower(Utf8SemanticRegex semanticRegex)
    {
        return semanticRegex.RuntimeTree?.Root is { } root
            ? new Utf8ExecutionTree(LowerNode(root))
            : null;
    }

    private static Utf8ExecutionNode LowerNode(RuntimeFrontEnd.RegexNode node)
    {
        var children = node.ChildCount == 0
            ? []
            : node.ChildList.Select(LowerNode).ToArray();

        return new Utf8ExecutionNode(
            MapKind(node.Kind),
            node.Options,
            children,
            node.Str,
            node.Ch,
            GetCaptureNumber(node),
            node.M,
            node.N);
    }

    private static int GetCaptureNumber(RuntimeFrontEnd.RegexNode node)
    {
        return node.Kind switch
        {
            RuntimeFrontEnd.RegexNodeKind.Capture or RuntimeFrontEnd.RegexNodeKind.Backreference => node.M,
            _ => 0,
        };
    }

    private static Utf8ExecutionNodeKind MapKind(RuntimeFrontEnd.RegexNodeKind kind)
    {
        return kind switch
        {
            RuntimeFrontEnd.RegexNodeKind.Empty => Utf8ExecutionNodeKind.Empty,
            RuntimeFrontEnd.RegexNodeKind.One => Utf8ExecutionNodeKind.One,
            RuntimeFrontEnd.RegexNodeKind.Multi => Utf8ExecutionNodeKind.Multi,
            RuntimeFrontEnd.RegexNodeKind.Set => Utf8ExecutionNodeKind.Set,
            RuntimeFrontEnd.RegexNodeKind.Concatenate => Utf8ExecutionNodeKind.Concatenate,
            RuntimeFrontEnd.RegexNodeKind.Alternate => Utf8ExecutionNodeKind.Alternate,
            RuntimeFrontEnd.RegexNodeKind.Capture => Utf8ExecutionNodeKind.Capture,
            RuntimeFrontEnd.RegexNodeKind.Group => Utf8ExecutionNodeKind.Group,
            RuntimeFrontEnd.RegexNodeKind.Oneloop or
            RuntimeFrontEnd.RegexNodeKind.Setloop or
            RuntimeFrontEnd.RegexNodeKind.Loop => Utf8ExecutionNodeKind.Loop,
            RuntimeFrontEnd.RegexNodeKind.Onelazy or
            RuntimeFrontEnd.RegexNodeKind.Setlazy or
            RuntimeFrontEnd.RegexNodeKind.Lazyloop => Utf8ExecutionNodeKind.LazyLoop,
            RuntimeFrontEnd.RegexNodeKind.Bol => Utf8ExecutionNodeKind.Bol,
            RuntimeFrontEnd.RegexNodeKind.Eol => Utf8ExecutionNodeKind.Eol,
            RuntimeFrontEnd.RegexNodeKind.Boundary => Utf8ExecutionNodeKind.Boundary,
            RuntimeFrontEnd.RegexNodeKind.NonBoundary => Utf8ExecutionNodeKind.NonBoundary,
            RuntimeFrontEnd.RegexNodeKind.ECMABoundary => Utf8ExecutionNodeKind.ECMABoundary,
            RuntimeFrontEnd.RegexNodeKind.NonECMABoundary => Utf8ExecutionNodeKind.NonECMABoundary,
            RuntimeFrontEnd.RegexNodeKind.Beginning => Utf8ExecutionNodeKind.Beginning,
            RuntimeFrontEnd.RegexNodeKind.Start => Utf8ExecutionNodeKind.Start,
            RuntimeFrontEnd.RegexNodeKind.EndZ => Utf8ExecutionNodeKind.EndZ,
            RuntimeFrontEnd.RegexNodeKind.End => Utf8ExecutionNodeKind.End,
            RuntimeFrontEnd.RegexNodeKind.PositiveLookaround => Utf8ExecutionNodeKind.PositiveLookaround,
            RuntimeFrontEnd.RegexNodeKind.NegativeLookaround => Utf8ExecutionNodeKind.NegativeLookaround,
            RuntimeFrontEnd.RegexNodeKind.Atomic => Utf8ExecutionNodeKind.Atomic,
            RuntimeFrontEnd.RegexNodeKind.Backreference => Utf8ExecutionNodeKind.Backreference,
            _ => Utf8ExecutionNodeKind.Unsupported,
        };
    }
}
