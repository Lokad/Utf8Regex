namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

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
            state.HasAlternation);
    }

    private static void Visit(RuntimeFrontEnd.RegexNode node, RuntimeFrontEnd.AnalysisResults? runtimeAnalysis, ref FeatureState state)
    {
        if (runtimeAnalysis?.IsInLoop(node) == true)
        {
            state.HasLoops = true;
        }

        switch (node.Kind)
        {
            case RuntimeFrontEnd.RegexNodeKind.Backreference:
                state.HasBackreferences = true;
                break;

            case RuntimeFrontEnd.RegexNodeKind.PositiveLookaround:
            case RuntimeFrontEnd.RegexNodeKind.NegativeLookaround:
                state.HasLookarounds = true;
                break;

            case RuntimeFrontEnd.RegexNodeKind.Atomic:
                state.HasAtomicGroups = true;
                break;

            case RuntimeFrontEnd.RegexNodeKind.BackreferenceConditional:
            case RuntimeFrontEnd.RegexNodeKind.ExpressionConditional:
                state.HasConditionals = true;
                break;

            case RuntimeFrontEnd.RegexNodeKind.Loop:
            case RuntimeFrontEnd.RegexNodeKind.Lazyloop:
            case RuntimeFrontEnd.RegexNodeKind.Oneloop:
            case RuntimeFrontEnd.RegexNodeKind.Notoneloop:
            case RuntimeFrontEnd.RegexNodeKind.Setloop:
            case RuntimeFrontEnd.RegexNodeKind.Onelazy:
            case RuntimeFrontEnd.RegexNodeKind.Notonelazy:
            case RuntimeFrontEnd.RegexNodeKind.Setlazy:
            case RuntimeFrontEnd.RegexNodeKind.Oneloopatomic:
            case RuntimeFrontEnd.RegexNodeKind.Notoneloopatomic:
            case RuntimeFrontEnd.RegexNodeKind.Setloopatomic:
                state.HasLoops = true;
                break;

            case RuntimeFrontEnd.RegexNodeKind.Alternate:
                state.HasAlternation = true;
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
    }
}
