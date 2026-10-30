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
            kind = IsEmail(tree.Root) ? Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole :
                IsBalanced(tree.Root) ? Utf8FallbackDirectFamilyKind.Utf8BalancedBeginEndCount : Utf8FallbackDirectFamilyKind.None;
        }
        if (kind == Utf8FallbackDirectFamilyKind.None) return default;
        scope.Complete();
        return Utf8FallbackDirectFamilyPlan.ForKind(kind, kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole ? Utf8FallbackFindModeKind.MatchAtStart : Utf8FallbackFindModeKind.None);
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

    private static bool IsBalanced(Utf8TreeNode root)
    {
        using var cursor = new Cursor(root);
        if (!cursor.ReadLiteral(root, out var begin) || !EqualsLiteral(begin, "BEGIN"u8) || !cursor.Next(out var loop) ||
            loop.Kind != Utf8TreeNodeKind.Loop || loop.M != 0 || !loop.IsUnboundedRepeat || loop.ChildCount != 1 ||
            !cursor.ReadLiteral(root, out var end) || !EqualsLiteral(end, "END"u8) || !cursor.Next(out var conditional) ||
            conditional.Kind != Utf8TreeNodeKind.BackreferenceConditional || conditional.M <= 0 || conditional.ChildCount != 2 ||
            !IsAlwaysFail(conditional.Child(0)) || !IsEmpty(conditional.Child(1)) || cursor.Peek(out _)) return false;
        var choice = StripOtherCaptures(loop.Child(0), conditional.M);
        if (choice.Kind != Utf8TreeNodeKind.Alternate || choice.ChildCount != 3) return false;
        var push = StripOtherCaptures(choice.Child(0), conditional.M);
        var pop = StripOtherCaptures(choice.Child(1), conditional.M);
        if (push.Kind != Utf8TreeNodeKind.Capture || push.M != conditional.M || push.N != -1 || push.ChildCount != 1 ||
            pop.Kind != Utf8TreeNodeKind.Capture || pop.M != -1 || pop.N != conditional.M || pop.ChildCount != 1 ||
            !IsWord(push.Child(0), "BEGIN"u8) || !IsWord(pop.Child(0), "END"u8) ||
            !IsGuardedScalar(choice.Child(2))) return false;
        // Additional declarations sharing the stack number change balancing
        // effects, even if their visible literal language looks identical.
        using var frames = Utf8ConstructionFrames<Frame>.Create(Utf8ConstructionScope.CurrentBudget, new(root, 0));
        while (frames.Count != 0)
        {
            Utf8ConstructionScope.ChargeWork(1); var index = frames.Count - 1; var frame = frames[index]; var node = frame.Node;
            if (node.Kind == Utf8TreeNodeKind.Capture && (node.M == conditional.M || node.N == conditional.M) &&
                !ReferenceEquals(node, push) && !ReferenceEquals(node, pop)) return false;
            if (frame.NextChild == node.ChildCount) { frames.Pop(); continue; }
            frames[index] = new(node, frame.NextChild + 1); frames.Push(new(node.Child(frame.NextChild), 0));
        }
        return true;
    }

    private static Utf8TreeNode StripOtherCaptures(Utf8TreeNode node, int stack)
    {
        while (node.ChildCount == 1 && (node.Kind == Utf8TreeNodeKind.Group || node.Kind == Utf8TreeNodeKind.Capture && node.N == -1 && node.M != stack))
        { Utf8ConstructionScope.ChargeWork(1); node = node.Child(0); }
        return node;
    }

    private static bool IsGuardedScalar(Utf8TreeNode root)
    {
        using var cursor = new Cursor(root);
        if (!cursor.Next(out var guard) || guard.Kind != Utf8TreeNodeKind.NegativeLookaround || guard.ChildCount != 1 ||
            !cursor.Next(out var scalar) || !TryRepeat(scalar, out var atom, out var min, out var max) || min != 1 || max != 1 ||
            atom.ScalarClass is not { } scalarClass || cursor.Peek(out _)) return false;
        Utf8ConstructionScope.ChargeWork((ulong)scalarClass.Data.Length);
        if (!scalarClass.Data.AsSpan().SequenceEqual([0, 0, 2, 100, -100]) &&
            !scalarClass.Data.AsSpan().SequenceEqual([0, 2, 0, 0, 0x110000])) return false;
        var choice = Unwrap(guard.Child(0));
        return choice.Kind == Utf8TreeNodeKind.Alternate && choice.ChildCount == 2 &&
            (IsWord(choice.Child(0), "BEGIN"u8) && IsWord(choice.Child(1), "END"u8) || IsWord(choice.Child(1), "BEGIN"u8) && IsWord(choice.Child(0), "END"u8));
    }

    private static bool IsWord(Utf8TreeNode root, ReadOnlySpan<byte> text)
    { using var cursor = new Cursor(root); return cursor.ReadLiteral(root, out var slice) && EqualsLiteral(slice, text) && !cursor.Peek(out _); }
    private static bool EqualsLiteral(LiteralSlice slice, ReadOnlySpan<byte> text)
    {
        if (slice.Length != text.Length) return false;
        using var cursor = new Cursor(slice.Root);
        for (var index = 0; index < slice.Offset; index++) cursor.Next(out _);
        var position = 0;
        for (var index = 0; index < slice.Count; index++)
        {
            if (!cursor.Next(out var node)) return false;
            var width = LiteralLength(node);
            for (var offset = 0; offset < width; offset++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (text[position++] != LiteralByte(node, offset)) return false;
            }
        }
        return true;
    }

    private static bool IsEmpty(Utf8TreeNode root)
    { using var cursor = new Cursor(root); return !cursor.Peek(out var node) || node.Kind == Utf8TreeNodeKind.Empty && cursor.Next(out _) && !cursor.Peek(out _); }
    private static bool IsAlwaysFail(Utf8TreeNode source)
    { var node = Unwrap(source); return node.Kind == Utf8TreeNodeKind.Nothing || node.Kind == Utf8TreeNodeKind.NegativeLookaround && node.ChildCount == 1 && IsEmpty(node.Child(0)); }
    private static bool IsByte(Utf8TreeNode node, byte value) => TryLiteral(node, out var literal) && LiteralLength(literal) == 1 && FirstByte(literal) == value;
    private static bool IsRun(Utf8TreeNode node, int minimum, int maximum, ReadOnlySpan<byte> members) =>
        TryRepeat(node, out var atom, out var min, out var max) && min == minimum && max == maximum && TryMask(atom, out var mask) && mask == AsciiCharClass.FromBytes(members);
    private static bool IsWholeBegin(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.Beginning || node.Kind == Utf8TreeNodeKind.Bol && (node.Options & RegexOptions.Multiline) == 0;
    private static bool IsWholeEnd(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.EndZ || node.Kind == Utf8TreeNodeKind.Eol && (node.Options & RegexOptions.Multiline) == 0;
}
