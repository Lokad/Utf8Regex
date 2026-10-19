using Lokad.Utf8Regex.Internal.Execution;
using static Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackTreeQuery;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Borrow complete scalar grammar. Captures and ordinary groups are transparent
// to these discovery/count proofs; public capture projection still uses the tree.
internal static class Utf8FallbackLineClassifier
{
    private readonly record struct Shape(Utf8FallbackDirectFamilyKind Kind, LiteralSlice Primary,
        LiteralSlice? Secondary, AsciiCharClass Separators, byte Terminator);

    internal static Utf8FallbackDirectFamilyPlan Classify(Utf8OwnedRegexTree? tree)
    {
        if (tree is null) return default;
        using var scope = Utf8ConstructionScope.Enter();
        if (!TryShape(tree.Root, out var shape)) return default;
        var primary = shape.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail
            ? CopySeparators(shape.Separators, scope.Budget) : CopySlice(shape.Primary, scope.Budget);
        var secondary = shape.Secondary is { } slice ? CopySlice(slice, scope.Budget) : null;
        var plan = shape.Kind switch
        {
            Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte => Utf8FallbackDirectFamilyPlan.ForPrefixUntilByte(primary, shape.Terminator),
            Utf8FallbackDirectFamilyKind.AnchoredTrimmedOptionalLiteralPrefixTail => Utf8FallbackDirectFamilyPlan.ForTrimmedOptionalLiteralPrefixTail(primary, secondary),
            _ => Utf8FallbackDirectFamilyPlan.ForLiteral(shape.Kind, Utf8FallbackFindModeKind.MatchAtStart, primary),
        };
        scope.Complete(); return plan;
    }

    private static bool TryShape(Utf8TreeNode root, out Shape shape)
    {
        using var scratch = Utf8ConstructionScope.Enter(); shape = default;
        if (!Admit(root, false)) return false;
        return TryUntil(root, out shape) || TryTrimmed(root, out shape) || TryDigits(root, out shape);
    }

    private static bool TryUntil(Utf8TreeNode root, out Shape shape)
    {
        shape = default; using var cursor = new Cursor(root);
        if (!cursor.Next(out var begin) || begin.Kind != Utf8TreeNodeKind.Start || !cursor.ReadLiteral(root, out var prefix) ||
            !cursor.Next(out var run) || !TryRepeat(run, out var atom, out var min, out var max) || min != 0 || max != int.MaxValue ||
            !TryExcludedByte(atom, out var excluded) || !cursor.Next(out var end) || !TryLiteral(end, out var literal) ||
            LiteralLength(literal) != 1 || FirstByte(literal) != excluded || cursor.Peek(out _)) return false;
        shape = new(Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte, prefix, null, default, excluded); return true;
    }

    private static bool TryTrimmed(Utf8TreeNode root, out Shape shape)
    {
        shape = default; using var cursor = new Cursor(root);
        if (!cursor.Next(out var begin) || !IsWholeBegin(begin) || !cursor.Next(out var whitespace) || !IsWhitespace(whitespace, 0, int.MaxValue)) return false;
        LiteralSlice? optional = null;
        if (cursor.Peek(out var candidate) && candidate.Kind == Utf8TreeNodeKind.Loop)
        { cursor.Next(out _); if (!TryOptionalLiteral(candidate, out var slice)) return false; optional = slice; }
        if (!cursor.ReadLiteral(root, out var prefix) || IsWhiteSpace(First(prefix)) ||
            optional is { } other && (IsWhiteSpace(First(other)) || First(other) == First(prefix)) ||
            !cursor.Next(out whitespace) || !IsWhitespace(whitespace, 0, 1) || !cursor.Next(out var tail) || !IsDotTail(tail) ||
            !cursor.Next(out var end) || !IsWholeEnd(end) || cursor.Peek(out _)) return false;
        shape = new(Utf8FallbackDirectFamilyKind.AnchoredTrimmedOptionalLiteralPrefixTail, prefix, optional, default, 0); return true;
    }

    private static bool TryDigits(Utf8TreeNode root, out Shape shape)
    {
        shape = default; using var cursor = new Cursor(root);
        if (!cursor.Next(out var begin) || !IsWholeBegin(begin) || !cursor.Next(out var digits) ||
            !TryAsciiRepeat(digits, 1, out var mask) || mask != AsciiCharClass.FromBytes("0123456789"u8) ||
            !cursor.Next(out var choice) || !TrySeparators(choice, out var separators) || !cursor.Next(out var tail) || !IsDotTail(tail) ||
            !cursor.Next(out var end) || !IsWholeEnd(end) || cursor.Peek(out _)) return false;
        shape = new(Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail, default, null, separators, 0); return true;
    }

    private static bool TrySeparators(Utf8TreeNode source, out AsciiCharClass mask)
    {
        mask = default; var node = Unwrap(source); var hasEnd = false;
        var count = node.Kind == Utf8TreeNodeKind.Alternate ? node.ChildCount : 1;
        for (var index = 0; index < count; index++)
        {
            Utf8ConstructionScope.ChargeWork(1); var arm = Unwrap(node.Kind == Utf8TreeNodeKind.Alternate ? node.Child(index) : node);
            if (IsWholeEnd(arm)) { hasEnd = true; continue; }
            if (arm.Kind is not (Utf8TreeNodeKind.One or Utf8TreeNodeKind.Set) || !TryMask(arm, out var members)) return false;
            mask = new(mask.ActualLowMask | members.ActualLowMask, mask.ActualHighMask | members.ActualHighMask);
        }
        return hasEnd && mask.Count <= 8 && !mask.Contains((byte)'\n') && mask.IsDisjoint(AsciiCharClass.FromBytes("0123456789"u8));
    }

    private static bool TryOptionalLiteral(Utf8TreeNode source, out LiteralSlice slice)
    {
        slice = default; var node = Unwrap(source);
        if (node.Kind != Utf8TreeNodeKind.Loop || node.M != 0 || node.N != 1 || node.ChildCount != 1) return false;
        var root = node.Child(0); using var cursor = new Cursor(root);
        return cursor.ReadLiteral(root, out slice) && cursor.Next(out var whitespace) && IsWhitespace(whitespace, 0, int.MaxValue) && !cursor.Peek(out _);
    }

    private static byte[] CopySeparators(AsciiCharClass mask, Utf8ConstructionBudget budget)
    {
        if (mask.IsEmpty) return [];
        var bytes = budget.AllocateArray<byte>(mask.Count, false); var index = 0;
        for (var value = 0; value < 128; value++) { budget.ChargeWork(1); if (mask.Contains((byte)value)) bytes[index++] = (byte)value; }
        return bytes;
    }

    private static bool IsWholeBegin(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.Beginning || node.Kind == Utf8TreeNodeKind.Bol && (node.Options & RegexOptions.Multiline) == 0;
    private static bool IsWholeEnd(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.EndZ || node.Kind == Utf8TreeNodeKind.Eol && (node.Options & RegexOptions.Multiline) == 0;
    private static bool IsWhiteSpace(byte value) => char.IsWhiteSpace((char)value);
    private static bool IsWhitespace(Utf8TreeNode node, int minimum, int maximum) => TryRepeat(node, out var atom, out var min, out var max) &&
        min == minimum && max == maximum && IsClass(atom, [0, 0, 1, 100]);
    private static bool IsDotTail(Utf8TreeNode node) => TryRepeat(node, out var atom, out var min, out var max) && min == 0 && max == int.MaxValue &&
        TryExcludedByte(atom, out var excluded) && excluded == (byte)'\n';
    private static bool TryExcludedByte(Utf8TreeNode atom, out byte excluded)
    {
        excluded = 0;
        if (atom.Kind is Utf8TreeNodeKind.Notone or Utf8TreeNodeKind.Notoneloop)
        { if (atom.Ch >= 128) return false; excluded = (byte)atom.Ch; return true; }
        if (atom.ScalarClass is not { } scalarClass) return false;
        var data = scalarClass.Data.AsSpan(); Utf8ConstructionScope.ChargeWork((ulong)data.Length);
        if (data.Length != 5 || data[0] != 1 || data[1] != 2 || data[2] != 0 || data[3] >= 128 || data[4] != data[3] + 1) return false;
        excluded = (byte)data[3]; return true;
    }
    private static bool IsClass(Utf8TreeNode atom, ReadOnlySpan<int> expected)
    {
        if (atom.ScalarClass is not { } scalarClass) return false;
        Utf8ConstructionScope.ChargeWork((ulong)scalarClass.Data.Length); return scalarClass.Data.AsSpan().SequenceEqual(expected);
    }
}
