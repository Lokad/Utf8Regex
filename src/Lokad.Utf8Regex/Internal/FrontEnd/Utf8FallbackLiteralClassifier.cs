using System.Numerics;
using Lokad.Utf8Regex.Internal.Execution;
using static Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackTreeQuery;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Literal edge shortcuts borrow scalar grammar. Folded bytes require complete
// ASCII case fibers and one consistent sensitivity across all literal fragments.
internal static class Utf8FallbackLiteralClassifier
{
    internal static Utf8FallbackDirectFamilyPlan Classify(Utf8OwnedRegexTree? tree)
    {
        if (tree is null || (tree.Options & (RegexOptions.RightToLeft | RegexOptions.ECMAScript)) != 0) return default;
        using var scope = Utf8ConstructionScope.Enter();
        if (!Admit(tree.Root, true)) return default;
        var body = Unwrap(tree.Root);
        if (body.Kind != Utf8TreeNodeKind.Concatenate || body.ChildCount < 2) return default;
        var first = Unwrap(body.Child(0)); var last = Unwrap(body.Child(body.ChildCount - 1));
        var prefix = first.Kind == Utf8TreeNodeKind.Beginning || first.Kind == Utf8TreeNodeKind.Bol && (first.Options & RegexOptions.Multiline) == 0;
        var suffix = last.Kind is Utf8TreeNodeKind.End or Utf8TreeNodeKind.EndZ || last.Kind == Utf8TreeNodeKind.Eol && (last.Options & RegexOptions.Multiline) == 0;
        var leadingDot = !prefix && !suffix && IsDotStar(first);
        if (!prefix && !suffix && !leadingDot || prefix && suffix) return default;
        var start = suffix ? 0 : 1; var count = body.ChildCount - 1;
        if (!VisitLiteral(body, start, count, Span<byte>.Empty, false, out var length, out var folded) || leadingDot && folded) return default;
        var bytes = scope.Budget.AllocateArray<byte>(length, false);
        if (!VisitLiteral(body, start, count, bytes, true, out var written, out var copiedFold) || written != length || copiedFold != folded)
            throw new InvalidOperationException("An immutable literal proof changed during copying.");
        var lax = last.Kind != Utf8TreeNodeKind.End;
        var kind = leadingDot ? Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral : prefix
            ? folded ? Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix : Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix
            : folded ? lax ? Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix : Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd
            : lax ? Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix : Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffixStrictEnd;
        var plan = Utf8FallbackDirectFamilyPlan.ForLiteral(kind, suffix ? Utf8FallbackFindModeKind.MatchAtEnd : Utf8FallbackFindModeKind.MatchAtStart, bytes);
        scope.Complete(); return plan;
    }

    private static bool IsDotStar(Utf8TreeNode source)
    {
        if (!TryRepeat(source, out var atom, out var minimum, out var maximum) || minimum != 0 || maximum != int.MaxValue) return false;
        if (atom.Kind is Utf8TreeNodeKind.Notone or Utf8TreeNodeKind.Notoneloop) return atom.Ch == '\n';
        if (atom.ScalarClass is not { } scalarClass) return false;
        Utf8ConstructionScope.ChargeWork((ulong)scalarClass.Data.Length);
        return scalarClass.Data.AsSpan().SequenceEqual([1, 2, 0, 10, 11]);
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
