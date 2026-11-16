using Lokad.Utf8Regex.Internal.Execution;
using static Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackTreeQuery;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FallbackFinalProfileClassifier
{
    internal static Utf8FallbackDirectFamilyPlan Classify(Utf8OwnedRegexTree? tree)
    {
        if (tree is null) return default;
        using var scope = Utf8ConstructionScope.Enter();
        Utf8FallbackDirectFamilyKind kind;
        using (var scratch = Utf8ConstructionScope.Enter())
        {
            if (!Admit(tree.Root, false)) return default;
            kind = IsEmail(tree.Root) ? Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole : Utf8FallbackDirectFamilyKind.None;
        }
        if (kind == Utf8FallbackDirectFamilyKind.None) return default;
        scope.Complete();
        return Utf8FallbackDirectFamilyPlan.ForKind(kind);
    }

    private static bool IsEmail(Utf8TreeNode root)
    {
        using var cursor = new Cursor(root);
        if (!cursor.Next(out var begin) || !IsWholeBegin(begin) || !cursor.Next(out var local) ||
            !IsRun(local, 1, int.MaxValue, "-.0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ_abcdefghijklmnopqrstuvwxyz"u8) ||
            !cursor.Next(out var at) || !IsByte(at, (byte)'@') || !cursor.Next(out var domain) || !IsDomain(domain) ||
            !cursor.Next(out var tail) || !IsEmailTail(tail) || !cursor.Next(out var bracket) || !IsRun(bracket, 0, 1, "]"u8) ||
            !cursor.Next(out var end) || !IsWholeEnd(end) || cursor.Peek(out _)) return false;
        return true;
    }

    private static bool IsDomain(Utf8TreeNode source)
    {
        var node = Unwrap(source);
        return node.Kind == Utf8TreeNodeKind.Alternate && node.ChildCount == 2 &&
            (IsDottedQuadPrefix(node.Child(0)) && IsDomainLabels(node.Child(1)) || IsDottedQuadPrefix(node.Child(1)) && IsDomainLabels(node.Child(0)));
    }

    private static bool IsDottedQuadPrefix(Utf8TreeNode root)
    {
        using var cursor = new Cursor(root);
        if (!cursor.Next(out var bracket) || !IsByte(bracket, (byte)'[')) return false;
        for (var index = 0; index < 3; index++)
            if (!cursor.Next(out var digits) || !IsRun(digits, 1, 3, "0123456789"u8) || !cursor.Next(out var dot) || !IsByte(dot, (byte)'.')) return false;
        return !cursor.Peek(out _);
    }

    private static bool IsDomainLabels(Utf8TreeNode source)
    {
        var loop = Unwrap(source);
        if (loop.Kind != Utf8TreeNodeKind.Loop || loop.M != 1 || !loop.IsUnboundedRepeat || loop.ChildCount != 1) return false;
        using var cursor = new Cursor(loop.Child(0));
        return cursor.Next(out var label) && IsRun(label, 1, int.MaxValue, "-0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8) &&
            cursor.Next(out var dot) && IsByte(dot, (byte)'.') && !cursor.Peek(out _);
    }

    private static bool IsEmailTail(Utf8TreeNode source)
    {
        var node = Unwrap(source);
        return node.Kind == Utf8TreeNodeKind.Alternate && node.ChildCount == 2 &&
            (IsRun(node.Child(0), 2, 12, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8) && IsRun(node.Child(1), 1, 3, "0123456789"u8) ||
             IsRun(node.Child(1), 2, 12, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8) && IsRun(node.Child(0), 1, 3, "0123456789"u8));
    }

    private static bool IsByte(Utf8TreeNode node, byte value) => TryLiteral(node, out var literal) && LiteralLength(literal) == 1 && FirstByte(literal) == value;
    private static bool IsRun(Utf8TreeNode node, int minimum, int maximum, ReadOnlySpan<byte> members) =>
        TryRepeat(node, out var atom, out var min, out var max) && min == minimum && max == maximum && TryMask(atom, out var mask) && mask == AsciiCharClass.FromBytes(members);
    private static bool IsWholeBegin(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.Beginning || node.Kind == Utf8TreeNodeKind.Bol && (node.Options & RegexOptions.Multiline) == 0;
    private static bool IsWholeEnd(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.EndZ || node.Kind == Utf8TreeNodeKind.Eol && (node.Options & RegexOptions.Multiline) == 0;
}
