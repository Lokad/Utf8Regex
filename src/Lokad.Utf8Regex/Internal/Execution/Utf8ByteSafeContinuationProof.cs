using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

// A consuming greedy body has iteration endpoints. The linear kernel may keep
// its last endpoint only when a failed suffix cannot become true at an earlier
// endpoint. This is a conditional proof for the compiler's admitted ASCII replay;
// non-ASCII class encounters retain the authoritative scalar dispatch.
internal static class Utf8ByteSafeContinuationProof
{
    private readonly record struct CursorFrame(IReadOnlyList<Utf8ExecutionNode> Children, int Index);
    private readonly record struct NodeFrame(Utf8ExecutionNode Node, int Next);

    internal static bool AllowsGreedyChoice(Utf8ExecutionNode choice, IReadOnlyList<Utf8ExecutionNode> children, int startIndex, bool nullableSuffix)
    {
        using var scope = Utf8ConstructionScope.Enter(); var budget = scope.Budget;
        using var cursor = Utf8ConstructionFrames<CursorFrame>.Create(budget, new(children, startIndex));
        var repeat = BorrowRepeat(choice);
        if (repeat is null) return false;
        for (var index = startIndex; index < children.Count; index++)
        {
            budget.ChargeWork(1); cursor[0] = new(children, index);
            if (!Inspect(children[index], repeat, nullableSuffix)) return false;
        }
        return true;
    }

    private static Utf8ExecutionNode? BorrowRepeat(Utf8ExecutionNode node)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<Utf8ExecutionNode>.Create(scope.Budget, node);
        while (Utf8ExecutionPrefixFacts.Transparent(node) && node.Children.Count == 1)
        { scope.Budget.ChargeWork(1); node = node.Children[0]; frames.Push(node); }
        scope.Budget.ChargeWork(1);
        return node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop ? node : null;
    }

    private static bool Inspect(Utf8ExecutionNode root, Utf8ExecutionNode repeat, bool nullableSuffix)
    {
        using var scope = Utf8ConstructionScope.Enter(); var budget = scope.Budget;
        using var frames = Utf8ConstructionFrames<NodeFrame>.Create(budget, new(root, 0));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1); var index = frames.Count - 1; var frame = frames[index]; var node = frame.Node;
            var wrapper = Utf8ExecutionPrefixFacts.Transparent(node) && node.Children.Count == 1;
            var concatenate = node.Kind == Utf8ExecutionNodeKind.Concatenate;
            var alternate = nullableSuffix && node.Kind == Utf8ExecutionNodeKind.Alternate;
            var loop = nullableSuffix && node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop;
            if (loop && node.Min == 0) { frames.Pop(); continue; } // Its empty path can skip the body.
            if (wrapper || concatenate || alternate || loop && node.Children.Count == 1)
            {
                if (frame.Next == node.Children.Count) { frames.Pop(); continue; }
                var child = node.Children[frame.Next]; frames[index] = frame with { Next = frame.Next + 1 };
                if (alternate)
                {
                    if (!Utf8ExecutionPrefixFacts.TryFirstMask(child, out _, out var nullable, asciiOnly: true)) return false;
                    if (!nullable) continue;
                }
                frames.Push(new(child, 0)); continue;
            }
            if (!AssertionAllows(node, repeat)) return false;
            frames.Pop();
        }
        return true;
    }

    private static bool AssertionAllows(Utf8ExecutionNode node, Utf8ExecutionNode repeat)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Empty:
            case Utf8ExecutionNodeKind.End:
            case Utf8ExecutionNodeKind.EndZ:
                return true;
            case Utf8ExecutionNodeKind.Eol:
                // Non-multiline end conditions are monotone in endpoint order.
                // A multiline endpoint can be crossed only if another iteration
                // can start with LF; internal LF bytes alone do not imply that.
                return (node.Options & RegexOptions.Multiline) == 0 ||
                    Utf8ExecutionPrefixFacts.TryFirstMask(repeat, out var first, out _, asciiOnly: true) && !first.IsEmpty && !first.Contains((byte)'\n');
            case Utf8ExecutionNodeKind.Beginning:
                return repeat.Min > 0;
            case Utf8ExecutionNodeKind.Bol:
                return repeat.Min > 0 && ((node.Options & RegexOptions.Multiline) == 0 ||
                    Utf8ExecutionPrefixFacts.TryLastMask(repeat, out var last, out _, asciiOnly: true) && !last.IsEmpty && !last.Contains((byte)'\n'));
            case Utf8ExecutionNodeKind.Boundary:
            case Utf8ExecutionNodeKind.NonBoundary:
                if (repeat.Min == 0 || !Utf8ExecutionPrefixFacts.TryFirstMask(repeat, out var firstMask, out _, asciiOnly: true) ||
                    !Utf8ExecutionPrefixFacts.TryLastMask(repeat, out var lastMask, out _, asciiOnly: true) ||
                    !UniformWordness(firstMask, out var firstWord) || !UniformWordness(lastMask, out var lastWord)) return false;
                // At every earlier nonempty endpoint, the neighbors are the last
                // scalar of one iteration and the first scalar of the next.
                return node.Kind == Utf8ExecutionNodeKind.Boundary ? firstWord == lastWord : firstWord != lastWord;
            default:
                // Contiguous origin and unsupported assertions need scalar
                // execution; a width proof cannot erase their conditions.
                return false;
        }
    }

    private static bool UniformWordness(AsciiCharClass mask, out bool word)
    {
        word = false; var seen = false;
        for (var value = 0; value < 128; value++)
        {
            Utf8ConstructionScope.ChargeWork(1);
            if (!mask.Contains((byte)value)) continue;
            var current = Utf8AsciiBytePredicates.IsWord((byte)value);
            if (seen && current != word) return false;
            seen = true; word = current;
        }
        return seen;
    }
}
