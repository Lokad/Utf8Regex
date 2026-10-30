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
                    node.LiteralUtf8 is not null || node.Text.Length != 0 ? Text(node) : Scalar(node.Ch, node);
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
            if (node.LiteralUtf8 is { } bytes)
            {
                scope.Budget.ChargeWork((ulong)bytes.Length);
                long nativeLow = 0, nativeHigh = 0;
                for (var offset = 0; offset < bytes.Length;)
                {
                    if (Rune.DecodeFromUtf8(bytes.AsSpan(offset), out var rune, out var consumed) != System.Buffers.OperationStatus.Done)
                        return new(0, null);
                    var width = Scalar(rune.Value, node);
                    nativeLow += width.Minimum; nativeHigh += width.Maximum.GetValueOrDefault(); offset += consumed;
                }
                return new(Cap(nativeLow), Upper(nativeHigh));
            }
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
}
