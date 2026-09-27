namespace Lokad.Utf8Regex.Internal.Execution;

// R05: fixed-span capture map for single exact literals whose captures partition the
// literal (concatenated runs wrapped in capturing or non-capturing groups). Each recorded
// group spans a fixed UTF-16 range of the discovered literal, so detailed shaping projects
// every group from the owned discovery hit without executing the fallback engine.
// Quantifiers, alternations, sets, anchors, lookarounds, backreferences, and empty nodes
// decline; a null table means the shape keeps the existing BCL routes. Construction is
// eager and pure over the owned tree: no fallback engine is built here.
internal sealed class Utf8GroupedLiteralSpanTable
{
    private Utf8GroupedLiteralSpanTable(int totalLengthInUtf16, int[] groups, int[] startsInLiteral, int[] lengthsInLiteral)
    {
        TotalLengthInUtf16 = totalLengthInUtf16;
        Groups = groups;
        StartsInLiteral = startsInLiteral;
        LengthsInLiteral = lengthsInLiteral;
    }

    // Total literal length in UTF-16 code units; shaping verifies the discovery hit length.
    public int TotalLengthInUtf16 { get; }

    // Parallel arrays: Groups[i] spans (StartsInLiteral[i], LengthsInLiteral[i]) in the literal.
    public int[] Groups { get; }

    public int[] StartsInLiteral { get; }

    public int[] LengthsInLiteral { get; }

    public static Utf8GroupedLiteralSpanTable? TryCreate(Utf8ExecutionTree? tree)
    {
        if (tree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root ||
            root.Children.Count != 1)
        {
            return null;
        }

        var groups = new List<int>();
        var starts = new List<int>();
        var lengths = new List<int>();
        var position = 0;
        if (!TryWalk(root.Children[0], ref position, groups, starts, lengths))
        {
            return null;
        }

        if (position == 0 || groups.Count == 0)
        {
            return null;
        }

        return new Utf8GroupedLiteralSpanTable(position, groups.ToArray(), starts.ToArray(), lengths.ToArray());
    }

    private static bool TryWalk(
        Utf8ExecutionNode node,
        ref int position,
        List<int> groups,
        List<int> starts,
        List<int> lengths)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.One:
                position += 1;
                return true;
            case Utf8ExecutionNodeKind.Multi:
                if (node.Text is null)
                {
                    return false;
                }

                position += node.Text.Length;
                return true;
            case Utf8ExecutionNodeKind.Concatenate:
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (!TryWalk(node.Children[i], ref position, groups, starts, lengths))
                    {
                        return false;
                    }
                }

                return true;
            case Utf8ExecutionNodeKind.Group:
                if (node.Children.Count != 1)
                {
                    return false;
                }

                return TryWalk(node.Children[0], ref position, groups, starts, lengths);
            case Utf8ExecutionNodeKind.Capture:
                if (node.CaptureNumber <= 0 || node.Children.Count != 1 || groups.Contains(node.CaptureNumber))
                {
                    return false;
                }

                var start = position;
                if (!TryWalk(node.Children[0], ref position, groups, starts, lengths))
                {
                    return false;
                }

                groups.Add(node.CaptureNumber);
                starts.Add(start);
                lengths.Add(position - start);
                return true;
            default:
                return false;
        }
    }
}
