using System.Numerics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Literal edge shortcuts borrow scalar grammar. Folded bytes require complete
// ASCII case fibers and one consistent sensitivity across all literal fragments.
internal static class Utf8AnchoredLiteralClassifier
{
    private readonly record struct Frame(Utf8TreeNode Node, int NextChild);

    private static bool Admit(Utf8TreeNode root, bool allowFolding)
    {
        using var scratch = Utf8ConstructionScope.Enter(); var budget = scratch.Budget;
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(root, 0));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1); var index = frames.Count - 1; var frame = frames[index];
            if ((!allowFolding && frame.Node.MatchCaseInsensitive) ||
                (frame.Node.Options & (RegexOptions.RightToLeft | RegexOptions.ECMAScript)) != 0) return false;
            if (frame.NextChild == frame.Node.ChildCount) { frames.Pop(); continue; }
            frames[index] = new(frame.Node, frame.NextChild + 1); frames.Push(new(frame.Node.Child(frame.NextChild), 0));
        }
        return true;
    }

    private static Utf8TreeNode Unwrap(Utf8TreeNode node)
    {
        while (node.ChildCount == 1 && (node.Kind == Utf8TreeNodeKind.Group || node.Kind == Utf8TreeNodeKind.Capture && node.N == -1))
        { Utf8ConstructionScope.ChargeWork(1); node = node.Child(0); }
        return node;
    }


    internal static Utf8AnchoredLiteralPlan Classify(Utf8OwnedRegexTree? tree)
    {
        if (tree is null || (tree.Options & (RegexOptions.RightToLeft | RegexOptions.ECMAScript)) != 0) return default;
        using var scope = Utf8ConstructionScope.Enter();
        if (!Admit(tree.Root, true)) return default;
        var body = Unwrap(tree.Root);
        if (body.Kind != Utf8TreeNodeKind.Concatenate || body.ChildCount < 2) return default;
        var first = Unwrap(body.Child(0)); var last = Unwrap(body.Child(body.ChildCount - 1));
        var prefix = first.Kind == Utf8TreeNodeKind.Beginning || first.Kind == Utf8TreeNodeKind.Bol && (first.Options & RegexOptions.Multiline) == 0;
        var suffix = last.Kind is Utf8TreeNodeKind.End or Utf8TreeNodeKind.EndZ || last.Kind == Utf8TreeNodeKind.Eol && (last.Options & RegexOptions.Multiline) == 0;
        if (!prefix && !suffix || prefix && suffix) return default;
        var start = suffix ? 0 : 1; var count = body.ChildCount - 1;
        if (!VisitLiteral(body, start, count, Span<byte>.Empty, false, out var length, out var folded)) return default;
        var bytes = scope.Budget.AllocateArray<byte>(length, false);
        if (!VisitLiteral(body, start, count, bytes, true, out var written, out var copiedFold) || written != length || copiedFold != folded)
            throw new InvalidOperationException("An immutable literal proof changed during copying.");
        var lax = last.Kind != Utf8TreeNodeKind.End;
        // A lax end anchor admits both the subject end and the position before
        // a trailing line feed. A literal ending in LF can match at either,
        // so the single-candidate suffix cursor has no complete proof.
        if (suffix && lax && bytes[^1] == (byte)'\n') return default;
        var kind = prefix
            ? folded ? Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix : Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix
            : folded ? lax ? Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix : Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd
            : lax ? Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffix : Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffixStrictEnd;
        var plan = new Utf8AnchoredLiteralPlan(kind, bytes);
        scope.Complete(); return plan;
    }

    private static bool VisitLiteral(Utf8TreeNode body, int start, int count, Span<byte> destination, bool write, out int length, out bool folded)
    {
        using var scratch = Utf8ConstructionScope.Enter(); var budget = scratch.Budget;
        length = 0; folded = false; var sensitiveLetter = false;
        for (var index = start; index < start + count; index++)
        {
            using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(body.Child(index), 0));
            while (frames.Count != 0)
            {
                budget.ChargeWork(1); var slot = frames.Count - 1; var frame = frames[slot]; var node = frame.Node;
                if (node.Kind is Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Group || node.Kind == Utf8TreeNodeKind.Capture && node.N == -1)
                {
                    if (frame.NextChild == node.ChildCount) { frames.Pop(); continue; }
                    frames[slot] = new(node, frame.NextChild + 1); frames.Push(new(node.Child(frame.NextChild), 0)); continue;
                }
                if (node.Kind == Utf8TreeNodeKind.Multi && node.Str is { Length: > 0 } text)
                {
                    foreach (var value in text)
                        if (!Append(value, node.MatchCaseInsensitive, node.Options, destination, write, ref length, ref folded, ref sensitiveLetter)) return false;
                }
                else if (node.Kind == Utf8TreeNodeKind.One)
                {
                    if (!Append(node.Ch, node.MatchCaseInsensitive, node.Options, destination, write, ref length, ref folded, ref sensitiveLetter)) return false;
                }
                else if (node.Kind == Utf8TreeNodeKind.Set && Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(node.ScalarClass, out var mask) && !mask.IsEmpty)
                {
                    var value = mask.LowMask != 0 ? BitOperations.TrailingZeroCount(mask.LowMask) : 64 + BitOperations.TrailingZeroCount(mask.HighMask);
                    if (!Utf8ScalarAsciiProjector.TryCloseAsciiCaseFiber(mask, node.MatchCaseInsensitive, node.Options, out var closed) ||
                        closed != FoldMask((byte)value, node.MatchCaseInsensitive) ||
                        !Append(value, node.MatchCaseInsensitive, node.Options, destination, write, ref length, ref folded, ref sensitiveLetter)) return false;
                }
                else return false;
                frames.Pop();
            }
        }
        return length > 0 && !(folded && sensitiveLetter);
    }

    private static AsciiCharClass FoldMask(byte value, bool fold)
    {
        var mask = AsciiCharClass.ForByte(value);
        if (!fold || (uint)((value | 32) - 'a') > 'z' - 'a') return mask;
        var other = AsciiCharClass.ForByte((byte)(value ^ 32));
        return new(mask.LowMask | other.LowMask, mask.HighMask | other.HighMask);
    }

    private static bool Append(int value, bool fold, RegexOptions options, Span<byte> destination, bool write, ref int length, ref bool folded, ref bool sensitiveLetter)
    {
        Utf8ConstructionScope.ChargeWork(1);
        if ((uint)value >= 128 || length == int.MaxValue) return false;
        if (fold && (!Utf8ScalarAsciiProjector.TryCloseAsciiCaseFiber(AsciiCharClass.ForByte((byte)value), true, options, out var closed) || closed != FoldMask((byte)value, true))) return false;
        if ((uint)((value | 32) - 'a') <= 'z' - 'a') { folded |= fold; sensitiveLetter |= !fold; }
        if (write) destination[length] = (byte)value;
        length++; return true;
    }
}
