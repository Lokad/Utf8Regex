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

    private struct Frame(Utf8TreeNode node)
    {
        internal Utf8TreeNode Node = node;
        internal int Index;
        internal bool Entered;
    }

    private static void Visit(Utf8TreeNode node, Utf8OwnedTreeAnalysis? runtimeAnalysis, ref FeatureState state)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = frame.Node;
            if (!frame.Entered)
            {
                frame.Entered = true;
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
            }
            if (frame.Index == node.ChildCount) { frames.Pop(); continue; }
            var child = node.Child(frame.Index++);
            frames[index] = frame;
            frames.Push(new(child));
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
