using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

// Borrowed grammar queries for qualified direct families. Scalar predicates are
// authoritative; ASCII masks describe an intersection, never a Unicode language.
internal static class Utf8FallbackTreeQuery
{
    internal readonly record struct Frame(Utf8TreeNode Node, int NextChild);

    internal readonly record struct LiteralSlice(Utf8TreeNode Root, int Offset, int Count, int Length);
    // The shared frame owner admits each actual wrapper before traversal. This
    // cursor borrows nodes and leaves alternations/repetitions opaque for proof.
    internal struct Cursor : IDisposable
    {
        private readonly Utf8ConstructionFrames<Frame> _frames;
        private Utf8TreeNode? _pending;
        internal int Position;
        internal Cursor(Utf8TreeNode root)
        { _frames = Utf8ConstructionFrames<Frame>.Create(Utf8ConstructionScope.CurrentBudget, new(root, 0)); _pending = null; Position = 0; }
        internal bool Peek([NotNullWhen(true)] out Utf8TreeNode? node)
        {
            if (_pending is not null) { node = _pending; return true; }
            while (_frames.Count != 0)
            {
                Utf8ConstructionScope.ChargeWork(1);
                var index = _frames.Count - 1; var frame = _frames[index]; var current = frame.Node;
                if (current.Kind is Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Group || current.Kind == Utf8TreeNodeKind.Capture && current.N == -1)
                {
                    if (frame.NextChild == current.ChildCount) { _frames.Pop(); continue; }
                    _frames[index] = new(current, frame.NextChild + 1); _frames.Push(new(current.Child(frame.NextChild), 0));
                }
                else { _frames.Pop(); _pending = current; node = current; return true; }
            }
            node = null; return false;
        }
        internal bool Next([NotNullWhen(true)] out Utf8TreeNode? node)
        { if (!Peek(out node)) return false; _pending = null; Position++; return true; }
        internal bool ReadLiteral(Utf8TreeNode root, out LiteralSlice slice)
        {
            var offset = Position; var count = 0; var length = 0;
            while (Peek(out var node) && TryLiteral(node, out var literal))
            { Next(out _); count++; length = checked(length + LiteralLength(literal)); }
            slice = new(root, offset, count, length); return count != 0;
        }
        internal bool ReadShape(Utf8TreeNode root, Utf8ScalarCaseBehavior behavior, bool allowDot,
            out LiteralSlice slice, out UInt128 wildcards)
        {
            var offset = Position; var count = 0; var length = 0; wildcards = 0;
            while (Peek(out var node) && TryShapeAtom(node, behavior, allowDot, out _, out var width, out var dot))
            {
                if (allowDot && length + (long)width > 128) { slice = default; return false; }
                if (dot) wildcards |= (UInt128)1 << length;
                Next(out _); count++; length = checked(length + width);
            }
            slice = new(root, offset, count, length); return count != 0;
        }
        public void Dispose() => _frames.Dispose();
    }

    internal static byte First(LiteralSlice slice)
    {
        using var scratch = Utf8ConstructionScope.Enter(); using var cursor = new Cursor(slice.Root);
        for (var index = 0; index <= slice.Offset; index++)
        {
            if (!cursor.Next(out var node)) break;
            if (index == slice.Offset) return FirstByte(node);
        }
        throw new InvalidOperationException("Qualified literal lost its node.");
    }

    internal static byte[] CopySlice(LiteralSlice slice, Utf8ConstructionBudget budget)
    {
        var bytes = budget.AllocateArray<byte>(slice.Length, false);
        using var scratch = Utf8ConstructionScope.Enter(); using var cursor = new Cursor(slice.Root);
        for (var index = 0; index < slice.Offset; index++) if (!cursor.Next(out _)) throw new InvalidOperationException("Qualified literal lost its offset.");
        var position = 0;
        for (var index = 0; index < slice.Count; index++)
        {
            if (!cursor.Next(out var node)) throw new InvalidOperationException("Qualified literal lost its payload.");
            var length = LiteralLength(node); budget.ChargeWork((ulong)length);
            if (node.Kind == Utf8TreeNodeKind.One) bytes[position++] = (byte)node.Ch;
            else { var text = node.Str ?? throw new InvalidOperationException("Qualified literal lost its text."); for (var j = 0; j < length; j++) bytes[position++] = (byte)text[j]; }
        }
        return bytes;
    }

    // Query-prefix dots retain their scalar predicate separately from bytes.
    // This is an ASCII subject proof; wider subjects keep scalar execution.
    private static bool TryShapeAtom(Utf8TreeNode node, Utf8ScalarCaseBehavior behavior, bool allowDot,
        out byte value, out int width, out bool dot)
    {
        value = 0; width = 1; dot = false; Utf8ConstructionScope.ChargeWork(1);
        if (TryLiteral(node, out var literal))
        {
            width = LiteralLength(literal);
            for (var index = 0; index < width; index++)
            {
                var current = literal.Kind == Utf8TreeNodeKind.One ? (byte)literal.Ch :
                    (byte)(literal.Str ?? throw new InvalidOperationException("Qualified query literal lost text."))[index];
                Utf8ConstructionScope.ChargeWork(1);
                if (current == (byte)'\n' || (uint)((current | 32) - 'a') <= 'z' - 'a' && !literal.MatchCaseInsensitive) return false;
            }
            value = FirstByte(literal); return true;
        }
        if (node.Kind != Utf8TreeNodeKind.Set || node.ScalarClass is not { } scalarClass) return false;
        if (allowDot && scalarClass.Data.AsSpan().SequenceEqual([1, 2, 0, 10, 11]))
        { Utf8ConstructionScope.ChargeWork((ulong)scalarClass.Data.Length); value = (byte)'.'; dot = true; return true; }
        AsciiCharClass mask;
        if (Utf8ScalarAsciiProjector.TryProjectPositiveAsciiQuery(scalarClass, out mask))
        {
            if (mask.Count is 0 or > 2) return false;
            if (node.MatchCaseInsensitive)
            {
                ulong low = mask.ActualLowMask, high = mask.ActualHighMask;
                var remainingLow = low; var remainingHigh = high;
                while ((remainingLow | remainingHigh) != 0)
                {
                    var current = remainingLow != 0 ? BitOperations.TrailingZeroCount(remainingLow) : 64 + BitOperations.TrailingZeroCount(remainingHigh);
                    Utf8ConstructionScope.ChargeWork(1);
                    if ((uint)((current | 32) - 'a') <= 'z' - 'a')
                    { var other = current ^ 32; if (other < 64) low |= 1ul << other; else high |= 1ul << (other - 64); }
                    if (current < 64) remainingLow &= remainingLow - 1; else remainingHigh &= remainingHigh - 1;
                }
                mask = new(low, high);
            }
        }
        else if (!node.MatchCaseInsensitive || !Utf8ScalarAsciiProjector.TryProjectPositiveFoldedAsciiQuery(scalarClass, behavior, out mask)) return false;
        if (mask.Count is 0 or > 2) return false;
        var first = mask.ActualLowMask != 0 ? BitOperations.TrailingZeroCount(mask.ActualLowMask) : 64 + BitOperations.TrailingZeroCount(mask.ActualHighMask);
        Utf8ConstructionScope.ChargeWork(1);
        value = (byte)(first >= 'A' && first <= 'Z' ? first | 32 : first);
        if ((uint)((value | 32) - 'a') <= 'z' - 'a')
        {
            // At most two members remain, so both case variants identify the exact pair.
            return mask.Contains((byte)(value & ~32)) && mask.Contains((byte)(value | 32));
        }
        return first != '\n' && mask.Count == 1;
    }

    internal static byte ShapeByte(LiteralSlice slice, int offset, Utf8ScalarCaseBehavior behavior, bool allowDot)
    {
        using var scratch = Utf8ConstructionScope.Enter(); using var cursor = new Cursor(slice.Root);
        for (var index = 0; index < slice.Offset; index++) cursor.Next(out _);
        for (var index = 0; index < slice.Count; index++)
        {
            if (!cursor.Next(out var node) || !TryShapeAtom(node, behavior, allowDot, out var value, out var width, out _))
                throw new InvalidOperationException("Qualified query shape lost its atom.");
            if (offset < width)
                return node.Kind == Utf8TreeNodeKind.Multi
                    ? (byte)(node.Str ?? throw new InvalidOperationException("Qualified query shape lost text."))[offset] : value;
            offset -= width;
        }
        throw new InvalidOperationException("Qualified query shape lost its byte.");
    }

    internal static void AppendShape(LiteralSlice slice, Span<byte> destination, int skip,
        Utf8ScalarCaseBehavior behavior, bool allowDot)
    {
        using var scratch = Utf8ConstructionScope.Enter(); using var cursor = new Cursor(slice.Root);
        for (var index = 0; index < slice.Offset; index++) cursor.Next(out _);
        var written = 0;
        for (var index = 0; index < slice.Count; index++)
        {
            if (!cursor.Next(out var node) || !TryShapeAtom(node, behavior, allowDot, out var value, out var width, out _))
                throw new InvalidOperationException("Qualified query shape changed during copying.");
            for (var position = 0; position < width; position++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (skip != 0) { skip--; continue; }
                if (written == destination.Length) continue;
                destination[written++] = node.Kind == Utf8TreeNodeKind.Multi
                    ? (byte)(node.Str ?? throw new InvalidOperationException("Qualified query shape lost text."))[position] : value;
            }
        }
        if (written != destination.Length) throw new InvalidOperationException("Qualified query shape lost its extent.");
    }


    internal static bool Admit(Utf8TreeNode root, bool allowFolding)
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

    internal static Utf8TreeNode Unwrap(Utf8TreeNode node)
    {
        while (node.ChildCount == 1 && (node.Kind == Utf8TreeNodeKind.Group || node.Kind == Utf8TreeNodeKind.Capture && node.N == -1))
        { Utf8ConstructionScope.ChargeWork(1); node = node.Child(0); }
        return node;
    }

    internal static bool TryRepeat(Utf8TreeNode source, out Utf8TreeNode atom, out int minimum, out int maximum)
    {
        Utf8ConstructionScope.ChargeWork(1); atom = Unwrap(source); minimum = maximum = 1;
        if (atom.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Notone or Utf8TreeNodeKind.Set) return true;
        minimum = atom.M; maximum = atom.N;
        if (minimum < 0 || maximum < minimum) return false;
        // These direct families use int.MaxValue as infinity. An explicit finite
        // maximum at that value belongs to the scalar VM, which keeps it exact.
        if (maximum == int.MaxValue && !atom.IsUnboundedRepeat) return false;
        if (atom.Kind == Utf8TreeNodeKind.Loop && atom.ChildCount == 1)
        {
            atom = Unwrap(atom.Child(0));
            // Nested repetitions need their own composition proof. Do not lose
            // the inner bounds while borrowing its class payload.
            return atom.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Notone or Utf8TreeNodeKind.Set;
        }
        return atom.Kind is Utf8TreeNodeKind.Oneloop or Utf8TreeNodeKind.Notoneloop or Utf8TreeNodeKind.Setloop;
    }

    internal static bool TryMask(Utf8TreeNode atom, out AsciiCharClass mask)
    {
        mask = default; Utf8ConstructionScope.ChargeWork(1);
        if (atom.Kind is Utf8TreeNodeKind.One or Utf8TreeNodeKind.Oneloop)
        { if (atom.Ch >= 128) return false; mask = AsciiCharClass.ForByte((byte)atom.Ch); return true; }
        return atom.Kind is Utf8TreeNodeKind.Set or Utf8TreeNodeKind.Setloop && Utf8ScalarAsciiProjector.TryProjectAsciiQuery(atom.ScalarClass, out mask);
    }

    internal static bool TryAsciiRepeat(Utf8TreeNode source, int minimum, out AsciiCharClass mask)
    {
        mask = default;
        return TryRepeat(source, out var atom, out var min, out var max) && min == minimum && max == int.MaxValue &&
            TryMask(atom, out mask) && (minimum == 0 || !mask.IsEmpty);
    }

    internal static bool TryLiteral(Utf8TreeNode source, out Utf8TreeNode literal)
    {
        literal = Unwrap(source); Utf8ConstructionScope.ChargeWork(1);
        if (literal.Kind == Utf8TreeNodeKind.One) return literal.Ch < 128;
        if (literal.Kind != Utf8TreeNodeKind.Multi || literal.Str is not { Length: > 0 } text) return false;
        foreach (var value in text) { Utf8ConstructionScope.ChargeWork(1); if (value >= 128) return false; }
        return true;
    }

    internal static int LiteralLength(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.One ? 1 : node.Str?.Length ?? 0;
    internal static byte FirstByte(Utf8TreeNode node) => node.Kind == Utf8TreeNodeKind.One ? (byte)node.Ch :
        (byte)(node.Str ?? throw new InvalidOperationException("Qualified literal lost its payload."))[0];

    internal static byte[] CopyLiteral(Utf8TreeNode node, Utf8ConstructionBudget budget)
    {
        var length = LiteralLength(node); var bytes = budget.AllocateArray<byte>(length, false); budget.ChargeWork((ulong)length);
        if (node.Kind == Utf8TreeNodeKind.One) bytes[0] = (byte)node.Ch;
        else
        {
            var text = node.Str ?? throw new InvalidOperationException("Qualified literal lost its payload.");
            for (var index = 0; index < length; index++) bytes[index] = (byte)text[index];
        }
        return bytes;
    }
}
