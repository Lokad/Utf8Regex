using Lokad.Utf8Regex.Internal.Execution;
using static Lokad.Utf8Regex.Internal.FrontEnd.Utf8FallbackTreeQuery;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FallbackQueryClassifier
{
    private readonly record struct Prefix(LiteralSlice Before, LiteralSlice Optional, LiteralSlice After, LiteralSlice Relative,
        UInt128 BeforeDots, UInt128 OptionalDots, UInt128 AfterDots, UInt128 RelativeDots);
    private readonly record struct Shape(Utf8FallbackDirectFamilyKind Kind, Prefix Prefix, LiteralSlice Route,
        LiteralSlice Required, LiteralSlice? Optional, Utf8ScalarCaseBehavior Behavior);

    internal static Utf8FallbackDirectFamilyPlan Classify(Utf8OwnedRegexTree? tree)
    {
        if (tree is null) return default;
        using var scope = Utf8ConstructionScope.Enter();
        Shape shape;
        using (var scratch = Utf8ConstructionScope.Enter())
        {
            var behavior = Utf8SliceCaseEquivalences.GetBehavior(tree.Culture, tree.Options);
            if (behavior == Utf8ScalarCaseBehavior.Turkic || !Admit(tree.Root, true) ||
                !TryShape(tree.Root, behavior, out shape)) return default;
        }
        var primary = CopyPrefix(shape.Prefix, false, shape.Behavior, scope.Budget);
        var secondary = CopyPrefix(shape.Prefix, true, shape.Behavior, scope.Budget);
        var relative = CopyPart(shape.Prefix.Relative, 0, shape.Prefix.Relative.Length, shape.Behavior, true, scope.Budget);
        var route = CopyPart(shape.Route, 0, shape.Route.Length, shape.Behavior, false, scope.Budget);
        var question = shape.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole ? 1 : 0;
        var required = CopyPart(shape.Required, question, shape.Required.Length - question - 1, shape.Behavior, false, scope.Budget);
        var optional = shape.Optional is { } value ? CopyPart(value, 0, value.Length - 1, shape.Behavior, false, scope.Budget) : null;
        var prefix = shape.Prefix;
        var dots = new Utf8FallbackUrlPrefixWildcards(prefix.BeforeDots | prefix.AfterDots << prefix.Before.Length,
            prefix.BeforeDots | prefix.OptionalDots << prefix.Before.Length | prefix.AfterDots << (prefix.Before.Length + prefix.Optional.Length),
            prefix.RelativeDots);
        var plan = Utf8FallbackDirectFamilyPlan.ForAnchoredAsciiQueryWhole(shape.Kind, primary, secondary, relative, route, required, optional, dots);
        scope.Complete(); return plan;
    }

    private static bool TryShape(Utf8TreeNode root, Utf8ScalarCaseBehavior behavior, out Shape shape)
    {
        shape = default; using var cursor = new Cursor(root);
        if (!cursor.Next(out var begin) || !IsWholeBegin(begin) || !cursor.Next(out var leading) || !IsDotTail(leading) ||
            !cursor.Next(out var choice) || !TryPrefix(choice, behavior, out var prefix) || !cursor.Next(out var trigram) || !IsTrigram(trigram) ||
            !cursor.ReadShape(root, behavior, false, out var route, out _) || ShapeByte(route, 0, behavior, false) != (byte)'/' ||
            !cursor.Next(out var value)) return false;
        var digits = IsRun(value, 1, int.MaxValue, "0123456789"u8);
        if (digits)
        {
            if (!cursor.Next(out var slash) || !IsRun(slash, 0, 1, "/"u8) ||
                !cursor.ReadShape(root, behavior, false, out var required, out _) || required.Length <= 2 ||
                ShapeByte(required, 0, behavior, false) != (byte)'?' || ShapeByte(required, required.Length - 1, behavior, false) != (byte)'=' ||
                !cursor.Next(out var parameter) || !IsNegativeRun(parameter, " ?"u8, 1) || !cursor.Next(out var tail) || !IsDotTail(tail) ||
                !cursor.Next(out var end) || !IsWholeEnd(end) || cursor.Peek(out _)) return false;
            shape = new(Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole, prefix, route, required, null, behavior); return true;
        }
        if (!IsRun(value, 0, int.MaxValue, "0123456789abcdefABCDEF"u8) ||
            !cursor.Next(out var firstOptional) || !TryOptionalParameter(firstOptional, behavior, out var optional) ||
            !cursor.Next(out var marker) || !IsMarker(marker) || !cursor.ReadShape(root, behavior, false, out var requiredHex, out _) ||
            requiredHex.Length < 2 || ShapeByte(requiredHex, requiredHex.Length - 1, behavior, false) != (byte)'=' ||
            !cursor.Next(out var requiredValue) || !IsNegativeRun(requiredValue, "& "u8, 1) ||
            !cursor.Next(out var secondOptional) || !TryOptionalParameter(secondOptional, behavior, out var other) ||
            !SameShape(optional, other, behavior) || !cursor.Next(out var rest) || !IsDotTail(rest) ||
            !cursor.Next(out var finish) || !IsWholeEnd(finish) || cursor.Peek(out _)) return false;
        shape = new(Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole, prefix, route, requiredHex, optional, behavior); return true;
    }

    private static bool TryPrefix(Utf8TreeNode source, Utf8ScalarCaseBehavior behavior, out Prefix prefix)
    {
        prefix = default; var choice = Unwrap(source);
        if (choice.Kind != Utf8TreeNodeKind.Alternate || choice.ChildCount != 2) return false;
        var root = choice.Child(0); using var cursor = new Cursor(root);
        if (!cursor.ReadShape(root, behavior, true, out var before, out var beforeDots) || !cursor.Next(out var loop) ||
            loop.Kind != Utf8TreeNodeKind.Loop || loop.M != 0 || loop.N != 1 || loop.ChildCount != 1 ||
            !cursor.ReadShape(root, behavior, true, out var after, out var afterDots) || cursor.Peek(out _)) return false;
        var optionalRoot = loop.Child(0); using var optionalCursor = new Cursor(optionalRoot);
        if (!optionalCursor.ReadShape(optionalRoot, behavior, true, out var optional, out var optionalDots) || optionalCursor.Peek(out _) ||
            before.Length + (long)optional.Length + after.Length > 128 || (beforeDots & 1) != 0) return false;
        var relativeRoot = choice.Child(1); using var relativeCursor = new Cursor(relativeRoot);
        if (!relativeCursor.ReadShape(relativeRoot, behavior, true, out var relative, out var relativeDots) ||
            relativeCursor.Peek(out _) || (relativeDots & 1) != 0) return false;
        prefix = new(before, optional, after, relative, beforeDots, optionalDots, afterDots, relativeDots); return true;
    }

    private static bool IsTrigram(Utf8TreeNode source)
    {
        var loop = Unwrap(source);
        if (loop.Kind != Utf8TreeNodeKind.Loop || loop.M != 0 || loop.N != 1 || loop.ChildCount != 1) return false;
        using var cursor = new Cursor(loop.Child(0));
        return cursor.Next(out var slash) && TryLiteral(slash, out var literal) && LiteralLength(literal) == 1 && FirstByte(literal) == (byte)'/' &&
            cursor.Next(out var letters) && IsRun(letters, 1, int.MaxValue, "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"u8) && !cursor.Peek(out _);
    }

    private static bool TryOptionalParameter(Utf8TreeNode source, Utf8ScalarCaseBehavior behavior, out LiteralSlice name)
    {
        name = default; var loop = Unwrap(source);
        if (loop.Kind != Utf8TreeNodeKind.Loop || loop.M != 0 || loop.N != 1 || loop.ChildCount != 1) return false;
        var root = loop.Child(0); using var cursor = new Cursor(root);
        return cursor.Next(out var marker) && IsMarker(marker) && cursor.ReadShape(root, behavior, false, out name, out _) &&
            name.Length >= 2 && ShapeByte(name, name.Length - 1, behavior, false) == (byte)'=' &&
            cursor.Next(out var value) && IsNegativeRun(value, "& \n"u8, 1) && !cursor.Peek(out _);
    }

    private static bool SameShape(LiteralSlice left, LiteralSlice right, Utf8ScalarCaseBehavior behavior)
    {
        if (left.Length != right.Length) return false;
        for (var index = 0; index < left.Length; index++)
            if (Internal.Search.AsciiSearch.FoldCase(ShapeByte(left, index, behavior, false)) != Internal.Search.AsciiSearch.FoldCase(ShapeByte(right, index, behavior, false))) return false;
        return true;
    }

    private static byte[] CopyPrefix(Prefix prefix, bool optional, Utf8ScalarCaseBehavior behavior, Utf8ConstructionBudget budget)
    {
        var length = prefix.Before.Length + prefix.After.Length + (optional ? prefix.Optional.Length : 0);
        var bytes = budget.AllocateArray<byte>(length, false); var position = prefix.Before.Length;
        AppendShape(prefix.Before, bytes.AsSpan(0, position), 0, behavior, true);
        if (optional) { AppendShape(prefix.Optional, bytes.AsSpan(position, prefix.Optional.Length), 0, behavior, true); position += prefix.Optional.Length; }
        AppendShape(prefix.After, bytes.AsSpan(position), 0, behavior, true); return bytes;
    }

    private static byte[] CopyPart(LiteralSlice slice, int skip, int length, Utf8ScalarCaseBehavior behavior, bool allowDot, Utf8ConstructionBudget budget)
    { var bytes = budget.AllocateArray<byte>(length, false); AppendShape(slice, bytes, skip, behavior, allowDot); return bytes; }
    private static bool IsMarker(Utf8TreeNode node) => IsRun(node, 1, 1, "?&"u8);
    private static bool IsRun(Utf8TreeNode source, int minimum, int maximum, ReadOnlySpan<byte> members)
    {
        if (!TryRepeat(source, out var atom, out var min, out var max) || min != minimum || max != maximum || !TryMask(atom, out var mask)) return false;
        // These positive ASCII alphabets already contain both letter cases.
        return mask == AsciiCharClass.FromBytes(members);
    }
    private static bool IsNegativeRun(Utf8TreeNode source, ReadOnlySpan<byte> excluded, int minimum)
    {
        if (!TryRepeat(source, out var atom, out var min, out var max) || min != minimum || max != int.MaxValue || atom.ScalarClass is not { } scalarClass) return false;
        var data = scalarClass.Data.AsSpan(); Utf8ConstructionScope.ChargeWork((ulong)data.Length);
        if (data.Length < 3 || data[0] != 1 || data[2] != 0 || data[1] < 0 || (data[1] & 1) != 0 || data.Length != 3L + data[1]) return false;
        if (!TryMask(atom, out var actual)) return false;
        var members = AsciiCharClass.FromBytes(excluded);
        return actual.ActualLowMask == ~members.ActualLowMask && actual.ActualHighMask == ~members.ActualHighMask;
    }
    private static bool IsDotTail(Utf8TreeNode node) => IsNegativeRun(node, "\n"u8, 0);
    private static bool IsWholeBegin(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.Beginning || node.Kind == Utf8TreeNodeKind.Bol && (node.Options & RegexOptions.Multiline) == 0;
    private static bool IsWholeEnd(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.EndZ || node.Kind == Utf8TreeNodeKind.Eol && (node.Options & RegexOptions.Multiline) == 0;
}
