using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

// R05: per-branch capture spans for single-alternation literal shapes whose branches may
// diverge (different lengths or group spans). Each branch records the literal expansion
// text plus every enclosed capture fixed UTF-16 range, so detailed shaping identifies
// the winning branch by ordinal text comparison against the owned discovery hit and
// projects its spans without executing the fallback engine. Multiple matching branches
// with different span maps decline as ambiguous, as do nested alternations, loops, sets,
// anchors, lookarounds, backreferences, and empty nodes; a null map means the shape keeps
// the existing BCL routes. Construction is eager and pure over the owned tree: no
// fallback engine is built here.
internal sealed class Utf8AlternationBranchSpanMap
{
    private Utf8AlternationBranchSpanMap(
        int[] wholeMatchGroups,
        string[] branchExpansions,
        int[][] branchGroups,
        int[][] branchStarts,
        int[][] branchLengths)
    {
        WholeMatchGroups = wholeMatchGroups;
        BranchExpansions = branchExpansions;
        BranchGroups = branchGroups;
        BranchStarts = branchStarts;
        BranchLengths = branchLengths;
    }

    // Groups spanning the entire match (captures wrapping the whole alternation).
    public int[] WholeMatchGroups { get; }

    // Per-branch literal expansion text used to identify the winning branch.
    public string[] BranchExpansions { get; }

    // Per-branch parallel arrays: BranchGroups[i][j] spans
    // (BranchStarts[i][j], BranchLengths[i][j]) inside branch i.
    public int[][] BranchGroups { get; }

    public int[][] BranchStarts { get; }

    public int[][] BranchLengths { get; }

    public static Utf8AlternationBranchSpanMap? TryCreate(Utf8ExecutionTree? tree)
    {
        if (tree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root ||
            root.Children.Count != 1)
        {
            return null;
        }

        var wholeMatchGroups = new List<int>();
        var node = root.Children[0];
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

        Utf8ExecutionNode? alternate = null;
        var alternateCount = 0;
        CountAlternates(node, ref alternate, ref alternateCount);
        if (alternateCount != 1 || alternate is null || alternate.Children.Count == 0)
        {
            return null;
        }

        var branchExpansions = new string[alternate.Children.Count];
        var branchGroups = new int[alternate.Children.Count][];
        var branchStarts = new int[alternate.Children.Count][];
        var branchLengths = new int[alternate.Children.Count][];
        var anyGroups = wholeMatchGroups.Count > 0;
        for (var branch = 0; branch < alternate.Children.Count; branch++)
        {
            var walker = new BranchWalker(alternate, branch);
            if (!walker.TryWalk(node))
            {
                return null;
            }

            if (walker.Position == 0)
            {
                return null;
            }

            branchExpansions[branch] = walker.Expansion.ToString();
            branchGroups[branch] = walker.Groups.ToArray();
            branchStarts[branch] = walker.Starts.ToArray();
            branchLengths[branch] = walker.Lengths.ToArray();
            anyGroups |= walker.Groups.Count > 0;
        }

        if (!anyGroups)
        {
            return null;
        }

        // Owned-tree capture numbers diverge from BCL group numbers for nested named
        // captures; translate every recorded group (branch spans and whole-match
        // wrappers) up front so shaping projects BCL slots, declining on surprise.
        var slots = Utf8BclSlotTranslator.TryCreateMap(root.Children[0]);
        if (slots is null)
        {
            return null;
        }

        var translatedWhole = new int[wholeMatchGroups.Count];
        for (var i = 0; i < wholeMatchGroups.Count; i++)
        {
            if (!slots.TryGetValue(wholeMatchGroups[i], out var wholeSlot))
            {
                return null;
            }

            translatedWhole[i] = wholeSlot;
        }

        for (var branch = 0; branch < branchGroups.Length; branch++)
        {
            for (var i = 0; i < branchGroups[branch].Length; i++)
            {
                if (!slots.TryGetValue(branchGroups[branch][i], out var branchSlot))
                {
                    return null;
                }

                branchGroups[branch][i] = branchSlot;
            }
        }

        return new Utf8AlternationBranchSpanMap(
            translatedWhole,
            branchExpansions,
            branchGroups,
            branchStarts,
            branchLengths);
    }

    private static void CountAlternates(Utf8ExecutionNode node, ref Utf8ExecutionNode? found, ref int count)
    {
        if (node.Kind == Utf8ExecutionNodeKind.Alternate)
        {
            count++;
            if (found is null)
            {
                found = node;
            }
        }

        for (var i = 0; i < node.Children.Count; i++)
        {
            CountAlternates(node.Children[i], ref found, ref count);
        }
    }

    private sealed class BranchWalker
    {
        private readonly Utf8ExecutionNode _alternate;
        private readonly int _branch;
        internal readonly StringBuilder Expansion = new();
        internal int Position;
        internal readonly List<int> Groups = new();
        internal readonly List<int> Starts = new();
        internal readonly List<int> Lengths = new();

        internal BranchWalker(Utf8ExecutionNode alternate, int branch)
        {
            _alternate = alternate;
            _branch = branch;
        }

        internal bool TryWalk(Utf8ExecutionNode node)
        {
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.One:
                    Position += 1;
                    Expansion.Append(node.Ch);
                    return true;
                case Utf8ExecutionNodeKind.Multi:
                    if (node.Text is null)
                    {
                        return false;
                    }

                    Position += node.Text.Length;
                    Expansion.Append(node.Text);
                    return true;
                case Utf8ExecutionNodeKind.Concatenate:
                    for (var i = 0; i < node.Children.Count; i++)
                    {
                        if (!TryWalk(node.Children[i]))
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

                    return TryWalk(node.Children[0]);
                case Utf8ExecutionNodeKind.Capture:
                    if (node.CaptureNumber <= 0 || node.Children.Count != 1 || Groups.Contains(node.CaptureNumber))
                    {
                        return false;
                    }

                    var start = Position;
                    if (!TryWalk(node.Children[0]))
                    {
                        return false;
                    }

                    Groups.Add(node.CaptureNumber);
                    Starts.Add(start);
                    Lengths.Add(Position - start);
                    return true;
                case Utf8ExecutionNodeKind.Alternate:
                    if (!ReferenceEquals(node, _alternate) || _branch < 0 || _branch >= node.Children.Count)
                    {
                        return false;
                    }

                    return TryWalk(node.Children[_branch]);
                default:
                    return false;
            }
        }
    }
}
