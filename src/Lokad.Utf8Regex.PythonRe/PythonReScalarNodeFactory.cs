using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

// Include the stored base Kind and every derived/initializer field. Referenced
// arrays, nodes and folding tables retain independent acquisition loans.
// These factories serve direct Python scalar conversion; core leaf conversion
// and other IR producers still require their own admission review.
internal static class PythonReScalarNodeFactory
{
    internal static Utf8SliceNode.ConcatNode Concat(Utf8SliceNode[] children) =>
        Create((Utf8SliceNodeKind.Concat, children), static state => new Utf8SliceNode.ConcatNode(state.children));

    internal static Utf8SliceNode.AlternationNode Alternation(Utf8SliceNode[][] branches) =>
        Create((Utf8SliceNodeKind.Alternation, branches), static state => new Utf8SliceNode.AlternationNode(state.branches));

    internal static Utf8SliceNode.RepeatNode Repeat(Utf8SliceNode body, int minimum, int maximum, bool lazy) =>
        Create((Utf8SliceNodeKind.Repeat, body, minimum, maximum, lazy), static state =>
            new Utf8SliceNode.RepeatNode(state.body, state.minimum, state.maximum, state.lazy));

    internal static Utf8SliceNode.AtomicNode Atomic(Utf8SliceNode body) =>
        Create((Utf8SliceNodeKind.Atomic, body), static state => new Utf8SliceNode.AtomicNode(state.body));

    internal static Utf8SliceNode.CaptureNode Capture(int slot, Utf8SliceNode body) =>
        Create((Utf8SliceNodeKind.Capture, slot, body), static state => new Utf8SliceNode.CaptureNode(state.slot, state.body));

    internal static Utf8SliceNode.LookaheadNode Lookahead(Utf8SliceNode body, bool positive) =>
        Create((Utf8SliceNodeKind.Lookahead, body, positive), static state => new Utf8SliceNode.LookaheadNode(state.body, state.positive));

    internal static Utf8SliceNode.LookbehindNode Lookbehind(Utf8SliceNode body, bool positive) =>
        Create((Utf8SliceNodeKind.Lookbehind, body, positive), static state => new Utf8SliceNode.LookbehindNode(state.body, state.positive));

    internal static Utf8SliceNode.BackrefNode Backref(int slot, Func<int, int>? lowerScalar) =>
        Create((Utf8SliceNodeKind.Backref, slot, lowerScalar is not null, false, lowerScalar), static state =>
            new Utf8SliceNode.BackrefNode(state.slot, state.Item3, state.Item4) { LowerScalar = state.lowerScalar });

    internal static Utf8SliceNode.ConditionalNode Conditional(int slot, Utf8SliceNode yes, Utf8SliceNode no) =>
        Create((Utf8SliceNodeKind.Conditional, slot, yes, no, (Utf8SliceNode?)null), static state =>
            new Utf8SliceNode.ConditionalNode(state.slot, state.yes, state.no, state.Item5));

    internal static Utf8SliceNode.EmptyNode Empty() =>
        Create(Utf8SliceNodeKind.Empty, static _ => new Utf8SliceNode.EmptyNode());

    internal static Utf8SliceNode.AnchorNode VeryEnd() =>
        Create((Utf8SliceNodeKind.Anchor, Utf8SliceAnchorKind.VeryEnd, false), static state =>
            new Utf8SliceNode.AnchorNode(state.Item2, state.Item3));

    private static TNode Create<TState, TNode>(TState state, Func<TState, TNode> factory) where TNode : class =>
        Utf8ConstructionScope.CurrentBudget.AllocateObject(state, factory);
}
