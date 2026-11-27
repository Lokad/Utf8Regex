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

    internal static Utf8SliceNode Concat(Utf8SliceNode[] children)
    {
        for (var index = 1; index < children.Length; index++)
        {
            Utf8ConstructionScope.Checkpoint();
            if (children[index - 1] is not Utf8SliceNode.AtomicNode atomic ||
                children[index] is not Utf8SliceNode.LiteralNode { FoldCase: false } next ||
                GetAtomicLiteralTail(atomic.Body) is not { Lazy: false,
                    Body: Utf8SliceNode.LiteralNode { FoldCase: false } repeated } run)
                continue;

            // Shorter endpoints still face the repeated scalar. A different
            // required literal makes the atomic frame redundant.
            if (repeated.Value != next.Value)
                children[index - 1] = atomic.Body;
            else if (atomic.Body is Utf8SliceNode.RepeatNode && run.Maximum == long.MaxValue)
                return Create(0, static _ => new Utf8SliceNode.FailNode());
        }
        return Create(children, static state => new Utf8SliceNode.ConcatNode(state));
    }

    private static Utf8SliceNode.RepeatNode? GetAtomicLiteralTail(Utf8SliceNode body)
    {
        if (body is Utf8SliceNode.RepeatNode repeat) return repeat;
        if (body is not Utf8SliceNode.ConcatNode { Children.Length: >= 2 and <= 32 } sequence ||
            sequence.Children[^1] is not Utf8SliceNode.RepeatNode tail)
            return null;
        // The prefix must have one endpoint and no effects or alternatives.
        foreach (var prefix in sequence.Children.AsSpan(0, sequence.Children.Length - 1))
        {
            Utf8ConstructionScope.Checkpoint();
            if (prefix is not Utf8SliceNode.LiteralNode { FoldCase: false }) return null;
        }
        return tail;
    }

    internal static Utf8SliceNode.AlternationNode Alternation(Utf8SliceNode[] branches) =>
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

    internal static Utf8SliceNode.DotNode Dot(bool matchNewline = true) =>
        Create(matchNewline, static state => new Utf8SliceNode.DotNode(state));

    internal static Utf8SliceNode.AnchorNode Anchor(Utf8SliceAnchorKind kind, bool multiline = false) =>
        Create((kind, multiline), static state => new Utf8SliceNode.AnchorNode(state.kind, state.multiline));

    internal static Utf8SliceNode.BoundaryNode AsciiBoundary(bool wordBoundary) =>
        Create(wordBoundary, static state => new Utf8SliceNode.BoundaryNode(state, Utf8SliceWordKind.Ascii));

    internal static Utf8SliceNode.BoundaryNode UnicodeBoundary(bool wordBoundary) =>
        Create(wordBoundary, static state => new Utf8SliceNode.BoundaryNode(state)
            { NonAsciiWordPredicate = PythonReScalarClassData.IsWord });

    internal static Utf8SliceNode.CategorySetNode Class(byte[] ascii, bool negated, Func<int, bool> predicate, Utf8ConstructionArena arena)
    {
        var members = arena.Allocate<Utf8SliceNode.CategorySetMember>(1, true);
        members[0] = new Utf8SliceNode.CategorySetMember(Utf8SliceCategoryTable.ForScalarPredicate(predicate), false);
        return Class(ascii, negated, members);
    }

    internal static Utf8SliceNode.CategorySetNode Class(byte[] ascii, bool negated, Utf8SliceNode.CategorySetMember[] members) =>
        Create((ascii, negated, members),
            static state => new Utf8SliceNode.CategorySetNode(state.ascii, state.negated, Array.Empty<Utf8SliceShorthand>(), state.members, false));

    internal static Utf8SliceNode.SetNode AsciiClass(byte[] mask, bool negated) =>
        Create((mask, negated), static state => new Utf8SliceNode.SetNode(state.mask, state.negated, [], false));

    private static TNode Create<TState, TNode>(TState state, Func<TState, TNode> factory) where TNode : class =>
        Utf8ConstructionScope.CurrentBudget.AllocateObject(state, factory);
}
