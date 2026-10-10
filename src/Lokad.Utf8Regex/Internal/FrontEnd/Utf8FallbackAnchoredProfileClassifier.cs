using Lokad.Utf8Regex.Internal.Execution;
using static Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackTreeQuery;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Each native profile implements one complete grammar. Optional features must
// exist in the proof even when their minimum is zero. Classes describe only
// the qualified ASCII subject envelope; wider subjects use scalar execution.
internal static class Utf8FallbackAnchoredProfileClassifier
{
    internal static Utf8FallbackDirectFamilyPlan Classify(Utf8OwnedRegexTree? tree)
    {
        if (tree is null) return default;
        using var scope = Utf8ConstructionScope.Enter();
        if (!Admit(tree.Root, true)) return default;
        var body = Unwrap(tree.Root);
        if (body.Kind != Utf8TreeNodeKind.Concatenate || body.ChildCount < 2) return default;
        var first = Unwrap(body.Child(0)); var last = Unwrap(body.Child(body.ChildCount - 1));
        var kind = Utf8FallbackDirectFamilyKind.None;
        if (first.Kind == Utf8TreeNodeKind.Start)
        {
            if (IsNumber(body, tree)) kind = Utf8FallbackDirectFamilyKind.AnchoredNumberPrefix;
            else if (IsQuoted(body)) kind = Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix;
        }
        else if ((first.Kind == Utf8TreeNodeKind.Beginning || first.Kind == Utf8TreeNodeKind.Bol && (first.Options & RegexOptions.Multiline) == 0) &&
            (last.Kind == Utf8TreeNodeKind.EndZ || last.Kind == Utf8TreeNodeKind.Eol && (last.Options & RegexOptions.Multiline) == 0))
        {
            if (IsDecimal(body, tree)) kind = Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole;
            else if (IsColor(body, tree)) kind = Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole;
            else if (body.ChildCount == 4 && IsCell(body.Child(1), body.Child(2), tree)) kind = Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole;
            else if (body.ChildCount == 7 && IsCell(body.Child(1), body.Child(2), tree) && IsRun(body.Child(3), 0, 1, ":"u8, tree) &&
                IsCell(body.Child(4), body.Child(5), tree)) kind = Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole;
        }
        if (kind == Utf8FallbackDirectFamilyKind.None) return default;
        scope.Complete(); return Utf8FallbackDirectFamilyPlan.ForKind(kind, Utf8FallbackFindModeKind.MatchAtStart);
    }

    private static bool IsNumber(Utf8TreeNode body, Utf8OwnedRegexTree tree)
    {
        if (body.ChildCount != 5 || !IsRun(body.Child(1), 0, 1, "-"u8, tree) || !IsRun(body.Child(2), 1, int.MaxValue, "0123456789"u8, tree) ||
            !TryOptionalGroup(body.Child(3), out var fraction) || fraction.Kind != Utf8TreeNodeKind.Concatenate || fraction.ChildCount != 2 ||
            !IsRun(fraction.Child(0), 1, 1, "."u8, tree) || !IsRun(fraction.Child(1), 1, int.MaxValue, "0123456789"u8, tree) ||
            !TryOptionalGroup(body.Child(4), out var exponent) || exponent.Kind != Utf8TreeNodeKind.Concatenate || exponent.ChildCount != 3) return false;
        return IsRun(exponent.Child(0), 1, 1, "e"u8, tree) && IsRun(exponent.Child(1), 0, 1, "+-"u8, tree) && IsRun(exponent.Child(2), 1, int.MaxValue, "0123456789"u8, tree);
    }

    private static bool IsDecimal(Utf8TreeNode body, Utf8OwnedRegexTree tree) => body.ChildCount == 6 &&
        IsRun(body.Child(1), 0, 1, "+-"u8, tree) && IsRun(body.Child(2), 0, int.MaxValue, "0123456789"u8, tree) &&
        IsRun(body.Child(3), 0, 1, "."u8, tree) && IsRun(body.Child(4), 0, int.MaxValue, "0123456789"u8, tree);

    private static bool IsColor(Utf8TreeNode body, Utf8OwnedRegexTree tree)
    {
        if (body.ChildCount != 4 || !IsRun(body.Child(1), 0, 1, "#"u8, tree)) return false;
        var choice = Unwrap(body.Child(2));
        return choice.Kind == Utf8TreeNodeKind.Alternate && choice.ChildCount == 2 &&
            (IsRun(choice.Child(0), 6, 6, "0123456789abcdefABCDEF"u8, tree) && IsRun(choice.Child(1), 3, 3, "0123456789abcdefABCDEF"u8, tree) ||
             IsRun(choice.Child(0), 3, 3, "0123456789abcdefABCDEF"u8, tree) && IsRun(choice.Child(1), 6, 6, "0123456789abcdefABCDEF"u8, tree));
    }

    private static bool IsCell(Utf8TreeNode column, Utf8TreeNode row, Utf8OwnedRegexTree tree) =>
        IsRun(column, 1, 1, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"u8, tree) && IsRun(row, 1, int.MaxValue, "0123456789"u8, tree);

    private static bool TryOptionalGroup(Utf8TreeNode source, out Utf8TreeNode body)
    {
        body = Unwrap(source);
        if (body.Kind != Utf8TreeNodeKind.Loop || body.M != 0 || body.N != 1 || body.ChildCount != 1) return false;
        body = Unwrap(body.Child(0)); return true;
    }

    private static bool IsRun(Utf8TreeNode node, int min, int max, ReadOnlySpan<byte> members, Utf8OwnedRegexTree tree)
    {
        if (!TryRepeat(node, out var atom, out var minimum, out var maximum) || min != minimum || max != maximum) return false;
        AsciiCharClass mask;
        if (!atom.MatchCaseInsensitive) { if (!TryMask(atom, out mask)) return false; }
        else if (atom.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Oneloop)
        {
            if (atom.Ch >= 128 || !Utf8ScalarAsciiProjector.TryCloseAsciiCaseFiber(AsciiCharClass.ForByte((byte)atom.Ch), true, atom.Options, out mask)) return false;
        }
        else if (atom.ScalarClass is { } scalarClass && scalarClass.Data.AsSpan().SequenceEqual([0, 0, 1, 9]))
            mask = AsciiCharClass.FromBytes("0123456789"u8);
        else if (!Utf8ScalarAsciiProjector.TryProjectPositiveFoldedAsciiQuery(atom.ScalarClass,
            Utf8SliceCaseEquivalences.GetBehavior(tree.Culture, atom.Options), out mask)) return false;
        return mask == AsciiCharClass.FromBytes(members);
    }

    private static bool IsQuoted(Utf8TreeNode body)
    {
        if (body.ChildCount != 4 || !IsQuote(body.Child(1)) || !IsQuote(body.Child(3))) return false;
        var loop = Unwrap(body.Child(2));
        if (loop.Kind != Utf8TreeNodeKind.Loop || loop.M != 0 || loop.N != int.MaxValue || loop.ChildCount != 1) return false;
        var choice = Unwrap(loop.Child(0));
        return choice.Kind == Utf8TreeNodeKind.Alternate && choice.ChildCount == 2 &&
            (IsQuotedBody(choice.Child(0)) && IsEscapedScalar(choice.Child(1)) || IsQuotedBody(choice.Child(1)) && IsEscapedScalar(choice.Child(0)));
    }

    private static bool IsQuote(Utf8TreeNode node) => TryLiteral(node, out var literal) && LiteralLength(literal) == 1 && FirstByte(literal) == (byte)'"';
    private static bool IsQuotedBody(Utf8TreeNode source)
    {
        var atom = Unwrap(source); if (atom.Kind != Utf8TreeNodeKind.Set || atom.ScalarClass is not { } scalarClass) return false;
        Utf8ConstructionScope.ChargeWork((ulong)scalarClass.Data.Length);
        return scalarClass.Data.AsSpan().SequenceEqual([1, 4, 0, 34, 35, 92, 93]);
    }

    private static bool IsEscapedScalar(Utf8TreeNode source)
    {
        var pair = Unwrap(source);
        if (pair.Kind != Utf8TreeNodeKind.Concatenate || pair.ChildCount != 2 || !TryLiteral(pair.Child(0), out var literal) || LiteralLength(literal) != 1 || FirstByte(literal) != (byte)'\\') return false;
        var atom = Unwrap(pair.Child(1));
        if (atom.Kind == Utf8TreeNodeKind.Notone) return atom.Ch == '\n';
        if (atom.Kind != Utf8TreeNodeKind.Set || atom.ScalarClass is not { } scalarClass) return false;
        Utf8ConstructionScope.ChargeWork((ulong)scalarClass.Data.Length);
        return scalarClass.Data.AsSpan().SequenceEqual([1, 2, 0, 10, 11]);
    }
}
