namespace Lokad.Utf8Regex.Internal.Execution;

// R03: branch-to-group capture map for top-level literal alternations whose branches are
// bare literals wrapped only in non-capturing or capturing groups. Each enclosed group
// spans its whole branch match (quantifiers, lookarounds, backreferences, anchors, and
// nested alternations inside branches decline). The search layer already resolves ordered
// choice and reports the winning alternate index; this map turns that index into group
// spans for owned detailed shaping. Construction is eager and pure; a null map means the
// shape keeps the existing BCL routes.
internal sealed class Utf8LiteralFamilyCaptureMap
{
    private Utf8LiteralFamilyCaptureMap(int[] wholeMatchGroups, int[][] branchGroups)
    {
        WholeMatchGroups = wholeMatchGroups;
        BranchGroups = branchGroups;
    }

    // Groups spanning the entire match (captures wrapping the whole alternation).
    public int[] WholeMatchGroups { get; }

    // Per-branch enclosed groups, indexed by alternate position.
    public int[][] BranchGroups { get; }

    public static Utf8LiteralFamilyCaptureMap? TryCreate(Utf8ExecutionTree? tree)
    {
        if (tree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root ||
            root.Children.Count != 1)
        {
            return null;
        }

        var node = root.Children[0];
        var wholeMatchGroups = new List<int>();
        while (true)
        {
            if (node.Kind == Utf8ExecutionNodeKind.Group && node.Children.Count == 1)
            {
                node = node.Children[0];
                continue;
            }

            if (node.Kind == Utf8ExecutionNodeKind.Capture && node.CaptureNumber > 0 && node.Children.Count == 1)
            {
                wholeMatchGroups.Add(node.CaptureNumber);
                node = node.Children[0];
                continue;
            }

            break;
        }

        if (node.Kind == Utf8ExecutionNodeKind.Alternate && node.Children.Count > 0)
        {
            var branchGroups = new int[node.Children.Count][];
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (!TryCollectBranchGroups(node.Children[i], out var groups))
                {
                    return null;
                }

                branchGroups[i] = groups;
            }

            return new Utf8LiteralFamilyCaptureMap(wholeMatchGroups.ToArray(), branchGroups);
        }

        // A lone literal (or literal run) is a single groupless branch; this lets split
        // and other whole-stream consumers share one branch-indexed machinery.
        if (IsLiteralRun(node))
        {
            return new Utf8LiteralFamilyCaptureMap(wholeMatchGroups.ToArray(), [[]]);
        }

        return null;
    }

    private static bool TryCollectBranchGroups(Utf8ExecutionNode node, out int[] groups)
    {
        var found = new List<int>();
        while (true)
        {
            if (node.Kind == Utf8ExecutionNodeKind.Group && node.Children.Count == 1)
            {
                node = node.Children[0];
                continue;
            }

            if (node.Kind == Utf8ExecutionNodeKind.Capture && node.CaptureNumber > 0 && node.Children.Count == 1)
            {
                found.Add(node.CaptureNumber);
                node = node.Children[0];
                continue;
            }

            break;
        }

        // Literal runs cover case-folded branches (concatenated per-character sets) as well as
        // plain literals; every collected capture still spans the whole branch by construction.
        if (!IsLiteralRun(node))
        {
            groups = [];
            return false;
        }

        groups = found.ToArray();
        return true;
    }

    private static bool IsLiteralRun(Utf8ExecutionNode node)
    {
        if (node.Kind is Utf8ExecutionNodeKind.One or Utf8ExecutionNodeKind.Multi or Utf8ExecutionNodeKind.Set)
        {
            return true;
        }

        if (node.Kind == Utf8ExecutionNodeKind.Concatenate)
        {
            for (var i = 0; i < node.Children.Count; i++)
            {
                if (!IsLiteralRun(node.Children[i]))
                {
                    return false;
                }
            }

            return node.Children.Count > 0;
        }

        return false;
    }
}
