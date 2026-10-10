using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

// Borrowed execution-node facts. Pattern text is interoperability storage;
// consumption is measured in scalar UTF-8 bytes, never UTF-16 units.
internal static class Utf8ExecutionPrefixFacts
{
    internal static bool Folded(Utf8ExecutionNode node) => (node.Options & RegexOptions.IgnoreCase) != 0;
    internal static bool Transparent(Utf8ExecutionNode node) =>
        node.Kind is Utf8ExecutionNodeKind.Capture or Utf8ExecutionNodeKind.Group or Utf8ExecutionNodeKind.Atomic;

    internal static bool ZeroWidth(Utf8ExecutionNode node) => node.Kind is
        Utf8ExecutionNodeKind.Empty or Utf8ExecutionNodeKind.Bol or Utf8ExecutionNodeKind.Beginning or Utf8ExecutionNodeKind.Start or
        Utf8ExecutionNodeKind.Eol or Utf8ExecutionNodeKind.End or Utf8ExecutionNodeKind.EndZ or
        Utf8ExecutionNodeKind.Boundary or Utf8ExecutionNodeKind.NonBoundary or Utf8ExecutionNodeKind.ECMABoundary or
        Utf8ExecutionNodeKind.NonECMABoundary or Utf8ExecutionNodeKind.PositiveLookaround or Utf8ExecutionNodeKind.NegativeLookaround;

    internal static bool ScalarMask(int scalar, Utf8ExecutionNode node, out AsciiCharClass mask)
    {
        mask = default;
        return (uint)scalar < 128 && Utf8ScalarAsciiProjector.TryCloseAsciiCaseFiber(
            AsciiCharClass.ForByte((byte)scalar), Folded(node), node.Options, out mask);
    }

    internal static bool ClassMask(Utf8ExecutionNode node, out AsciiCharClass mask)
    {
        mask = default;
        return Utf8ScalarAsciiProjector.TryProjectPositiveAsciiDefinition(node.ScalarClass, out var definition) &&
            Utf8ScalarAsciiProjector.TryCloseAsciiCaseFiber(definition, Folded(node), node.Options, out mask);
    }

    private readonly record struct WidthFrame(Utf8ExecutionNode Node, int Next);
    internal static Utf8OwnedFindOptimizations.ByteWidth Width(Utf8ExecutionNode root)
    {
        using var scope = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<WidthFrame>.Create(scope.Budget, new(root, 0));
        using var widths = Utf8ConstructionMap<Utf8ExecutionNode, Utf8OwnedFindOptimizations.ByteWidth>.Create(scope.Budget, ReferenceEqualityComparer.Instance);
        while (frames.Count != 0)
        {
            scope.Budget.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; var node = frame.Node;
            if (!ZeroWidth(node) && frame.Next < node.Children.Count)
            {
                frames[index] = frame with { Next = frame.Next + 1 };
                frames.Push(new(node.Children[frame.Next], 0));
                continue;
            }
            widths[node] = Measure(node);
            frames.Pop();
        }
        return widths[root];

        Utf8OwnedFindOptimizations.ByteWidth Measure(Utf8ExecutionNode node)
        {
            if (ZeroWidth(node)) return new(0, 0);
            if (Transparent(node)) return node.Children.Count == 1 ? widths[node.Children[0]] : new(0, null);
            if (node.Kind == Utf8ExecutionNodeKind.One) return Scalar(node.Ch, node);
            if (node.Kind == Utf8ExecutionNodeKind.Multi) return Text(node);
            if (node.Kind == Utf8ExecutionNodeKind.Set) return Class(node);
            if (node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop)
            {
                var body = node.Children.Count == 1 ? widths[node.Children[0]] : node.ScalarClass is not null ? Class(node) :
                    node.Text.Length != 0 ? Text(node) : Scalar(node.Ch, node);
                if (node.Min < 0 || node.Max < node.Min) return new(0, null);
                return new(Cap((long)body.Minimum * node.Min), node.Max == 0 || body.Maximum == 0 ? 0 :
                    node.Max == int.MaxValue || body.Maximum is null ? null : Upper((long)body.Maximum.Value * node.Max));
            }
            if (node.Kind is not (Utf8ExecutionNodeKind.Concatenate or Utf8ExecutionNodeKind.Alternate)) return new(0, null);
            long low = node.Kind == Utf8ExecutionNodeKind.Alternate ? int.MaxValue : 0;
            long high = 0; var bounded = true;
            foreach (var child in node.Children)
            {
                scope.Budget.ChargeWork(1); var width = widths[child];
                if (node.Kind == Utf8ExecutionNodeKind.Alternate)
                { low = Math.Min(low, width.Minimum); high = Math.Max(high, width.Maximum.GetValueOrDefault()); }
                else { low += width.Minimum; high += width.Maximum.GetValueOrDefault(); }
                bounded &= width.Maximum.HasValue;
            }
            return new(Cap(low), bounded ? Upper(high) : null);
        }
        Utf8OwnedFindOptimizations.ByteWidth Text(Utf8ExecutionNode node)
        {
            scope.Budget.ChargeWork((ulong)node.Text.Length);
            long low = 0, high = 0;
            for (var offset = 0; offset < node.Text.Length;)
            {
                if (Rune.DecodeFromUtf16(node.Text.AsSpan(offset), out var rune, out var consumed) != System.Buffers.OperationStatus.Done) return new(0, null);
                var width = Scalar(rune.Value, node); low += width.Minimum; high += width.Maximum.GetValueOrDefault(); offset += consumed;
            }
            return new(Cap(low), Upper(high));
        }
        Utf8OwnedFindOptimizations.ByteWidth Class(Utf8ExecutionNode node)
        {
            if (node.ScalarClass is not { } scalarClass) return new(0, null);
            var data = scalarClass.Data;
            if (data.Length < 3 || data[0] != 0 || data[2] != 0 || data.Length != 3L + data[1]) return new(1, 4);
            var low = 4; var high = 0;
            for (var index = 3; index < data.Length; index += 2)
            {
                scope.Budget.ChargeWork(1); var start = data[index]; var end = data[index + 1];
                if (start < 0 || end > 0x110000 || end <= start) return new(0, null);
                if (Folded(node))
                {
                    if (end - start > 128) return new(1, 4);
                    for (var scalar = start; scalar < end; scalar++)
                    { var width = Scalar(scalar, node); low = Math.Min(low, width.Minimum); high = Math.Max(high, width.Maximum.GetValueOrDefault()); }
                }
                else
                {
                    if (start < 128) { low = 1; high = Math.Max(high, 1); }
                    if (start < 0x800 && end > 128) { low = Math.Min(low, 2); high = Math.Max(high, 2); }
                    if (start < 0xd800 && end > 0x800 || start < 0x10000 && end > 0xe000) { low = Math.Min(low, 3); high = Math.Max(high, 3); }
                    if (end > 0x10000) { low = Math.Min(low, 4); high = 4; }
                }
            }
            return high == 0 ? new(0, null) : new(low, high);
        }
        static Utf8OwnedFindOptimizations.ByteWidth Scalar(int scalar, Utf8ExecutionNode node) =>
            Utf8OwnedFindOptimizations.ComputeScalarByteWidth(scalar, Folded(node), node.Options);
        static int Cap(long value) => (int)Math.Min(int.MaxValue, value);
        static int? Upper(long value) => value > int.MaxValue ? null : (int)value;
    }

    private readonly record struct EdgeFrame(Utf8ExecutionNode Node, int Next, AsciiCharClass Mask, bool Nullable);
    // Nullable children contribute their first sets and also permit the next
    // child. Every alternative participates; an unproved branch declines all.
    internal static bool FirstMask(Utf8ExecutionNode root, out AsciiCharClass result)
    {
        var proved = TryFirstMask(root, out var mask, out var nullable, asciiOnly: false);
        result = proved && !nullable ? mask : default;
        return proved && !nullable && !mask.IsEmpty;
    }

    // A consumer proving disjointness also needs nullability: nullable children
    // contribute their first set without excluding the following sibling.
    internal static bool TryFirstMask(Utf8ExecutionNode root, out AsciiCharClass result, out bool canBeEmpty, bool asciiOnly)
        => TryEdgeMask(root, out result, out canBeEmpty, asciiOnly, reverse: false);

    internal static bool TryLastMask(Utf8ExecutionNode root, out AsciiCharClass result, out bool canBeEmpty, bool asciiOnly)
        => TryEdgeMask(root, out result, out canBeEmpty, asciiOnly, reverse: true);

    private static bool TryEdgeMask(Utf8ExecutionNode root, out AsciiCharClass result, out bool canBeEmpty, bool asciiOnly, bool reverse)
    {
        result = default;
        canBeEmpty = false;
        using var scope = Utf8ConstructionScope.Enter();
        using var frames = Utf8ConstructionFrames<EdgeFrame>.Create(scope.Budget, new(root, 0, default, true));
        var mask = default(AsciiCharClass); var nullable = false; var ready = false;
        while (frames.Count != 0)
        {
            scope.Budget.ChargeWork(1);
            var index = frames.Count - 1; var frame = frames[index]; var node = frame.Node;
            var wrapper = Transparent(node) && node.Children.Count == 1;
            var repeat = node.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop;
            if (ready)
            {
                if (wrapper || repeat)
                { nullable |= repeat && node.Min == 0; frames.Pop(); continue; }
                var union = node.Kind == Utf8ExecutionNodeKind.Alternate;
                mask = new(frame.Mask.ActualLowMask | mask.ActualLowMask, frame.Mask.ActualHighMask | mask.ActualHighMask);
                nullable = union ? frame.Nullable || nullable : frame.Nullable && nullable;
                if (!union && !nullable || frame.Next == node.Children.Count) { frames.Pop(); continue; }
                frame = frame with { Mask = mask, Nullable = nullable };
            }
            if (ZeroWidth(node) || repeat && node.Max == 0)
            { mask = default; nullable = true; frames.Pop(); ready = true; continue; }
            if (wrapper || repeat && node.Children.Count == 1 || node.Kind is Utf8ExecutionNodeKind.Concatenate or Utf8ExecutionNodeKind.Alternate)
            {
                if (node.Children.Count == 0) { mask = default; nullable = true; frames.Pop(); ready = true; continue; }
                frames[index] = frame with { Next = frame.Next + 1, Nullable = frame.Next == 0 ? node.Kind != Utf8ExecutionNodeKind.Alternate : frame.Nullable };
                var childIndex = reverse ? node.Children.Count - 1 - frame.Next : frame.Next;
                frames.Push(new(node.Children[childIndex], 0, default, true)); ready = false; continue;
            }
            // Byte-safe replay can use a complete ASCII intersection because its
            // runner requests scalar dispatch on a non-ASCII encounter. General
            // search anchors require coverage of the whole scalar class.
            var proved = node.ScalarClass is not null ? asciiOnly && !Folded(node)
                ? Utf8ScalarAsciiProjector.TryProjectAsciiIntersection(node.ScalarClass, out mask) : ClassMask(node, out mask)
                : node.Kind == Utf8ExecutionNodeKind.One || repeat && node.Text.Length == 0
                ? ScalarMask(node.Ch, node, out mask) : node.Kind == Utf8ExecutionNodeKind.Multi || repeat
                ? TextEdgeMask(node, reverse, out mask)
                : false;
            if (!proved) return false;
            nullable = repeat && node.Min == 0; frames.Pop(); ready = true;
        }
        result = mask;
        canBeEmpty = nullable;
        return true;
    }

    private static bool TextEdgeMask(Utf8ExecutionNode node, bool reverse, out AsciiCharClass mask)
    {
        mask = default;
        if (node.Text.Length == 0) return false;
        Rune scalar;
        var status = reverse ? Rune.DecodeLastFromUtf16(node.Text, out scalar, out _)
            : Rune.DecodeFromUtf16(node.Text, out scalar, out _);
        return status == System.Buffers.OperationStatus.Done && ScalarMask(scalar.Value, node, out mask);
    }
}
