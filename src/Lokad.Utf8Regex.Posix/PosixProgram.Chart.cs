using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Posix;

internal sealed partial class PosixProgram
{
    internal enum ChartKind { Empty, Literal, Dot, Anchor, Group, Concat, Alternate, Repeat, Class }

    // Flat immutable syntax for closed-interval selection. Classes borrow the
    // parser's immutable leaf tables, not the surrounding parser/tree objects.
    // -1 means unbounded byte consumption; large finite bounds saturate at the
    // maximum representable subject length. Ordering uses actual byte boundaries.
    internal readonly record struct ChartNode(ChartKind Kind, int Left, int Right, int A, int B,
        int LastGroup, int MaximumBytes);
    private readonly record struct ChartBuildFrame(Utf8SliceNode Source, int Index);

    private static (ChartNode[] Nodes, Utf8SliceNode.CategorySetNode[] Classes) CreateChart(
        Utf8SliceNode root, Utf8ConstructionBudget budget)
    {
        using var nodes = Utf8ConstructionBuffer<ChartNode>.Create(budget);
        using var classes = Utf8ConstructionBuffer<Utf8SliceNode.CategorySetNode>.Create(budget);
        using var frames = Utf8ConstructionBuffer<ChartBuildFrame>.Create(budget);
        nodes.Add(default); frames.Push(new(root, 0));
        while (frames.TryPop(out var frame))
        {
            budget.ChargeWork(1);
            ChartKind kind;
            var a = 0; var b = 0; var left = -1; var right = -1; var maximum = 0;
            Utf8SliceNode? first = null; Utf8SliceNode? second = null;
            switch (frame.Source)
            {
                case Utf8SliceNode.EmptyNode: kind = ChartKind.Empty; break;
                case Utf8SliceNode.LiteralNode literal:
                    kind = ChartKind.Literal; a = literal.Value;
                    maximum = new System.Text.Rune(a).Utf8SequenceLength; break;
                case Utf8SliceNode.DotNode dot:
                    kind = ChartKind.Dot; a = dot.MatchNewline ? 1 : 0; maximum = 4; break;
                case Utf8SliceNode.AnchorNode anchor:
                    kind = ChartKind.Anchor; a = (int)anchor.Anchor; b = anchor.MatchMultiline ? 1 : 0; break;
                case Utf8SliceNode.CategorySetNode set:
                    kind = ChartKind.Class; a = classes.Count; classes.Add(set); maximum = 4; break;
                case Utf8SliceNode.CaptureNode capture:
                    kind = ChartKind.Group; a = capture.Slot; first = capture.Body; break;
                case Utf8SliceNode.ConcatNode concat when concat.Children.Length == 2:
                    kind = ChartKind.Concat; first = concat.Children[0]; second = concat.Children[1]; break;
                case Utf8SliceNode.AlternationNode alternate when alternate.Branches.Length == 2 &&
                    alternate.Branches[0].Length == 1 && alternate.Branches[1].Length == 1:
                    kind = ChartKind.Alternate; first = alternate.Branches[0][0]; second = alternate.Branches[1][0]; break;
                case Utf8SliceNode.RepeatNode repeat:
                    kind = ChartKind.Repeat; first = repeat.Body; a = checked((int)repeat.Minimum);
                    b = repeat.Maximum == long.MaxValue ? -1 : checked((int)repeat.Maximum); break;
                default: throw new InvalidOperationException("An unsupported tree entered POSIX closed-interval lowering.");
            }
            if (first is not null) { left = nodes.Count; nodes.Add(default); frames.Push(new(first, left)); }
            if (second is not null) { right = nodes.Count; nodes.Add(default); frames.Push(new(second, right)); }
            nodes[frame.Index] = new(kind, left, right, a, b, kind == ChartKind.Group ? a : 0, maximum);
        }
        for (var index = nodes.Count - 1; index >= 0; index--)
        {
            budget.ChargeWork(1);
            var node = nodes[index]; var last = node.LastGroup; var maximum = node.MaximumBytes;
            if (node.Left >= 0) last = Math.Max(last, nodes[node.Left].LastGroup);
            if (node.Right >= 0) last = Math.Max(last, nodes[node.Right].LastGroup);
            var leftMaximum = node.Left >= 0 ? nodes[node.Left].MaximumBytes : 0;
            switch (node.Kind)
            {
                case ChartKind.Group: maximum = leftMaximum; break;
                case ChartKind.Concat:
                    var rightMaximum = nodes[node.Right].MaximumBytes;
                    maximum = leftMaximum < 0 || rightMaximum < 0 ? -1
                        : (int)Math.Min(int.MaxValue, (long)leftMaximum + rightMaximum);
                    break;
                case ChartKind.Alternate:
                    rightMaximum = nodes[node.Right].MaximumBytes;
                    maximum = leftMaximum < 0 || rightMaximum < 0 ? -1 : Math.Max(leftMaximum, rightMaximum);
                    break;
                case ChartKind.Repeat:
                    maximum = node.B == 0 || leftMaximum == 0 ? 0 : node.B < 0 || leftMaximum < 0 ? -1
                        : (int)Math.Min(int.MaxValue, (long)leftMaximum * node.B);
                    break;
            }
            nodes[index] = node with { LastGroup = last, MaximumBytes = maximum };
        }
        return (nodes.Take(), classes.Take());
    }
}
