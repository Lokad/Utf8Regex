namespace Lokad.Utf8Regex.Internal.FrontEnd;


internal static class Utf8RuntimeTreeFeatureAnalyzer
{
    public static Utf8RegexFeatures Analyze(Utf8SemanticRegex semanticRegex)
    {
        if (semanticRegex.RuntimeTree is not { Root: { } root })
        {
            return default;
        }

        var runtimeAnalysis = semanticRegex.RuntimeAnalysis;
        var state = new FeatureState
        {
            CaptureCount = semanticRegex.RuntimeTree.CaptureCount,
            HasNamedCaptures = semanticRegex.RuntimeTree.CaptureNameToNumberMapping is { Count: > 0 },
        };

        Visit(root, runtimeAnalysis, ref state);

        return new Utf8RegexFeatures(
            state.CaptureCount,
            state.HasNamedCaptures,
            state.HasBackreferences,
            state.HasLookarounds,
            state.HasAtomicGroups,
            state.HasConditionals,
            state.HasLoops,
            state.HasAlternation,
            state.HasAbsoluteStartAnchor,
            state.HasWordBoundary,
            state.HasLookbehind);
    }

    private static void Visit(Utf8TreeNode node, Utf8OwnedTreeAnalysis? runtimeAnalysis, ref FeatureState state)
    {
        if (runtimeAnalysis?.IsInLoop(node) == true)
        {
            state.HasLoops = true;
        }

        switch (node.Kind)
        {
            case Utf8TreeNodeKind.Backreference:
                state.HasBackreferences = true;
                break;

            case Utf8TreeNodeKind.PositiveLookaround:
            case Utf8TreeNodeKind.NegativeLookaround:
                state.HasLookarounds = true;
                if ((node.Options & RegexOptions.RightToLeft) != 0)
                {
                    state.HasLookbehind = true;
                }

                break;

            case Utf8TreeNodeKind.Atomic:
                state.HasAtomicGroups = true;
                break;

            case Utf8TreeNodeKind.BackreferenceConditional:
            case Utf8TreeNodeKind.ExpressionConditional:
                state.HasConditionals = true;
                break;

            case Utf8TreeNodeKind.Loop:
            case Utf8TreeNodeKind.Lazyloop:
            case Utf8TreeNodeKind.Oneloop:
            case Utf8TreeNodeKind.Notoneloop:
            case Utf8TreeNodeKind.Setloop:
            case Utf8TreeNodeKind.Onelazy:
            case Utf8TreeNodeKind.Notonelazy:
            case Utf8TreeNodeKind.Setlazy:
            case Utf8TreeNodeKind.Oneloopatomic:
            case Utf8TreeNodeKind.Notoneloopatomic:
            case Utf8TreeNodeKind.Setloopatomic:
                state.HasLoops = true;
                break;

            case Utf8TreeNodeKind.Alternate:
                state.HasAlternation = true;
                break;

            case Utf8TreeNodeKind.Bol:
            case Utf8TreeNodeKind.Beginning:
                state.HasAbsoluteStartAnchor = true;
                break;

            case Utf8TreeNodeKind.Boundary:
            case Utf8TreeNodeKind.NonBoundary:
            case Utf8TreeNodeKind.ECMABoundary:
            case Utf8TreeNodeKind.NonECMABoundary:
                state.HasWordBoundary = true;
                break;
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            Visit(node.Child(i), runtimeAnalysis, ref state);
        }
    }

    private struct FeatureState
    {
        public int CaptureCount;
        public bool HasNamedCaptures;
        public bool HasBackreferences;
        public bool HasLookarounds;
        public bool HasAtomicGroups;
        public bool HasConditionals;
        public bool HasLoops;
        public bool HasAlternation;
        public bool HasAbsoluteStartAnchor;
        public bool HasWordBoundary;
        public bool HasLookbehind;
    }
}
