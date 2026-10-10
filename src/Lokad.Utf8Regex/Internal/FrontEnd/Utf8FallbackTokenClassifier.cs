using Lokad.Utf8Regex.Internal.Execution;
using static Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackTreeQuery;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// These shortcuts consume ASCII intersections only on ASCII subjects. The word
// email specialization separately proves its complete Unicode word language.
internal static class Utf8FallbackTokenClassifier
{
    private readonly record struct Shape(Utf8FallbackDirectFamilyKind Kind, Utf8TreeNode Literal,
        Utf8TreeNode? Secondary, Utf8TreeNode? Tertiary, Utf8FallbackTokenMasks Masks);

    internal static Utf8FallbackDirectFamilyPlan Classify(Utf8OwnedRegexTree? tree)
    {
        if (tree is null || (tree.Options & (RegexOptions.IgnoreCase | RegexOptions.RightToLeft | RegexOptions.ECMAScript)) != 0)
            return default;
        using var scope = Utf8ConstructionScope.Enter();
        if (!TryShape(tree.Root, out var shape)) return default;
        var literal = CopyLiteral(shape.Literal, scope.Budget);
        var secondary = shape.Secondary is null ? [] : CopyLiteral(shape.Secondary, scope.Budget);
        var tertiary = shape.Tertiary is null ? [] : CopyLiteral(shape.Tertiary, scope.Budget);
        var plan = shape.Kind == Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount
            ? Utf8FallbackDirectFamilyPlan.ForLiteralStructuredTokenCount(literal, secondary, tertiary, shape.Masks)
            : shape.Kind == Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount
                ? Utf8FallbackDirectFamilyPlan.ForUtf8WordDelimitedTokenCount(literal, secondary, shape.Masks)
                : Utf8FallbackDirectFamilyPlan.ForDelimitedTokenCount(literal, secondary, shape.Masks);
        scope.Complete();
        return plan;
    }

    private static bool TryShape(Utf8TreeNode root, out Shape shape)
    {
        shape = default;
        // Scratch descriptors, capacity and live depth roll back before final
        // byte arrays are acquired. Every actual wrapper remains in the walk.
        if (!Admit(root, false)) return false;
        var body = Unwrap(root);
        if (body.Kind != Utf8TreeNodeKind.Concatenate || body.ChildCount is < 4 or > 6 ||
            !TryAsciiRepeat(body.Child(0), 1, out var head) ||
            !TryLiteral(body.Child(1), out var literal) || head.Contains(FirstByte(literal)) ||
            !TryAsciiRepeat(body.Child(2), 1, out var middle)) return false;
        if (body.ChildCount == 5 && TryLiteral(body.Child(3), out var secondary) &&
            LiteralLength(secondary) == 1 && TryAsciiRepeat(body.Child(4), 1, out var tail))
        {
            var masks = new Utf8FallbackTokenMasks(head, middle, tail, default, default);
            var kind = IsWordEmail(body, literal, secondary, masks)
                ? Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount : Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount;
            shape = new(kind, literal, secondary, null, masks); return true;
        }
        if (!TryAsciiRepeat(body.Child(3), 1, out var second)) return false;
        Utf8TreeNode? optional1 = null, optional2 = null;
        AsciiCharClass tail1 = default, tail2 = default;
        if (body.ChildCount > 4 && !TryOptionalTail(body.Child(4), out optional1, out tail1)) return false;
        if (body.ChildCount > 5 && !TryOptionalTail(body.Child(5), out optional2, out tail2)) return false;
        shape = new(Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount, literal, optional1, optional2,
            new(head, middle, second, tail1, tail2)); return true;
    }

    private static bool TryOptionalTail(Utf8TreeNode source, out Utf8TreeNode? delimiter, out AsciiCharClass tail)
    {
        delimiter = null; tail = default; var node = Unwrap(source);
        if (node.Kind != Utf8TreeNodeKind.Loop || node.M != 0 || node.N != 1 || node.ChildCount != 1) return false;
        var body = Unwrap(node.Child(0));
        if (body.Kind != Utf8TreeNodeKind.Concatenate || body.ChildCount != 2 ||
            !TryLiteral(body.Child(0), out var literal) || LiteralLength(literal) != 1 ||
            !TryAsciiRepeat(body.Child(1), 0, out tail)) return false;
        delimiter = literal; return true;
    }

    private static bool IsWordEmail(Utf8TreeNode body, Utf8TreeNode literal, Utf8TreeNode secondary, Utf8FallbackTokenMasks masks)
    {
        if (LiteralLength(literal) != 1 || FirstByte(literal) != (byte)'@' || FirstByte(secondary) != (byte)'.' ||
            masks.Head != AsciiCharClass.FromBytes("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.+-"u8) ||
            masks.Middle != AsciiCharClass.FromBytes("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_.-"u8) ||
            masks.Tail != masks.Middle) return false;
        return IsWordClass(body.Child(0)) && IsWordClass(body.Child(2)) && IsWordClass(body.Child(4));
    }

    private static bool IsWordClass(Utf8TreeNode source)
    {
        var node = Unwrap(source);
        if (node.Kind == Utf8TreeNodeKind.Loop && node.ChildCount == 1) node = Unwrap(node.Child(0));
        if (node.ScalarClass is not { } scalarClass) return false;
        var data = scalarClass.Data.AsSpan(); Utf8ConstructionScope.ChargeWork((ulong)data.Length);
        if (data.Length < 3 || data[0] != 0 || data[2] != 10 || data[1] < 0 || (data[1] & 1) != 0 ||
            3L + data[1] + 10 != data.Length || !data[(3 + data[1])..].SequenceEqual([0, 2, 4, 5, 3, 1, 6, 9, 19, 0])) return false;
        for (var index = 3; index < 3 + data[1]; index += 2)
            if (data[index] < 0 || data[index + 1] > 128 || data[index] >= data[index + 1]) return false;
        return true;
    }
}
