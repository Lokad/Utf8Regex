using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.PythonRe;

// Constructor arguments only. The shared allocator's conservative 128-byte
// minimum covers these node descriptors before acquisition; referenced payloads
// have independent array/text admission. Do not mirror unused instance fields.
internal static class PythonReScalarNodeFactory
{
    internal static Utf8SliceNode.LiteralNode Literal(int scalar) =>
        Create(scalar, static state => new Utf8SliceNode.LiteralNode(state, false));

    internal static Utf8SliceNode.ConcatNode Concat(Utf8SliceNode[] children) =>
        Create(children, static state => new Utf8SliceNode.ConcatNode(state));

    internal static Utf8SliceNode.AlternationNode Alternation(Utf8SliceNode[][] branches) =>
        Create(branches, static state => new Utf8SliceNode.AlternationNode(state));

    internal static Utf8SliceNode.RepeatNode Repeat(Utf8SliceNode body, long minimum, long maximum, bool lazy) =>
        Create((body, minimum, maximum, lazy), static state =>
            new Utf8SliceNode.RepeatNode(state.body, state.minimum, state.maximum, state.lazy)
            { GuardEmptyAfterMinimum = true });

    internal static Utf8SliceNode.AtomicNode Atomic(Utf8SliceNode body) =>
        Create(body, static state => new Utf8SliceNode.AtomicNode(state));

    internal static Utf8SliceNode.CaptureNode Capture(int slot, Utf8SliceNode body) =>
        Create((slot, body), static state => new Utf8SliceNode.CaptureNode(state.slot, state.body));

    internal static Utf8SliceNode.LookaheadNode Lookahead(Utf8SliceNode body, bool positive) =>
        Create((body, positive), static state => new Utf8SliceNode.LookaheadNode(state.body, state.positive));

    internal static Utf8SliceNode.LookbehindNode Lookbehind(Utf8SliceNode body, bool positive, uint scalarCount) =>
        Create((body, positive, scalarCount), static state =>
            new Utf8SliceNode.LookbehindNode(state.body, state.positive) { ForwardScalarCount = state.scalarCount });

    internal static Utf8SliceNode.BackrefNode Backref(int slot, Func<int, int>? lowerScalar) =>
        Create((slot, lowerScalar), static state =>
            new Utf8SliceNode.BackrefNode(state.slot, state.lowerScalar is not null, false) { LowerScalar = state.lowerScalar });

    internal static Utf8SliceNode.ConditionalNode Conditional(int slot, Utf8SliceNode yes, Utf8SliceNode no) =>
        Create((slot, yes, no), static state =>
            new Utf8SliceNode.ConditionalNode(state.slot, state.yes, state.no, null));

    internal static Utf8SliceNode.EmptyNode Empty() =>
        Create(0, static _ => new Utf8SliceNode.EmptyNode());

    internal static Utf8SliceNode.DotNode Dot() =>
        Create(true, static state => new Utf8SliceNode.DotNode(state));

    internal static Utf8SliceNode.AnchorNode Anchor(Utf8SliceAnchorKind kind) =>
        Create(kind, static state => new Utf8SliceNode.AnchorNode(state, false));

    internal static Utf8SliceNode.CategorySetNode Class(byte[] ascii, bool negated, Func<int, bool> predicate, Utf8ConstructionArena arena)
    {
        var members = arena.Allocate<Utf8SliceNode.CategorySetMember>(1, true);
        members[0] = Utf8SliceNode.CategorySetMember.Create(Utf8SliceCategoryTable.ForScalarPredicate(predicate), false);
        return Create((ascii, negated, members),
            static state => new Utf8SliceNode.CategorySetNode(state.ascii, state.negated, Array.Empty<Utf8SliceShorthand>(), state.members, false));
    }

    private static TNode Create<TState, TNode>(TState state, Func<TState, TNode> factory) where TNode : class =>
        Utf8ConstructionScope.CurrentBudget.AllocateObject(state, factory);
}
