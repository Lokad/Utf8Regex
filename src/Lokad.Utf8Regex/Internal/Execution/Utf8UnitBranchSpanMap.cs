using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

// R05: unit-space branch span maps for nested divergent simple patterns whose atoms
// are not literal-only (dots, sets, and shorthand classes consume exactly one UTF-16
// unit, halves included, exactly like BCL). Each pattern records whole-match wrapper
// groups plus every enclosed capture per expanded branch as fixed unit offsets, so
// detailed shaping projects group spans onto the owned unit-space discovery hit
// without executing the fallback engine. Shaping re-verifies every lowered branch at
// the hit start in plan order and projects the first full-span verifier: plan order
// replicates BCL choice order and verification replays the exact lowered tokens, so
// the first verifier is the BCL winner and disagreement declines to the existing
// BCL routes. The expansion order
// replicates the lowerer (source-order alternation, left-major concatenation) and
// per-index widths pin it to the lowered branches, so any construct the walker does
// not model (finite repeats, anchors, lookarounds, backreferences, empty nodes, and
// anything else) fails closed. Branch-count or length disagreement and more than 64
// expansions decline; a null map means the shape keeps the existing BCL routes.
// Construction is eager and pure over the owned tree plus the lowered plan: no
// fallback engine is built here.
internal sealed class Utf8UnitBranchSpanMap
{
    private Utf8UnitBranchSpanMap(
        int[] wholeMatchGroups,
        int[] branchWidths,
        int[][] branchGroups,
        int[][] branchStarts,
        int[][] branchLengths)
    {
        WholeMatchGroups = wholeMatchGroups;
        BranchWidths = branchWidths;
        BranchGroups = branchGroups;
        BranchStarts = branchStarts;
        BranchLengths = branchLengths;
    }

    // Groups spanning the entire match (captures wrapping the whole pattern).
    public int[] WholeMatchGroups { get; }

    // Per-branch parallel arrays: BranchGroups[i][j] spans
    // (BranchStarts[i][j], BranchLengths[i][j]) in UTF-16 units relative to the
    // hit start, with total width BranchWidths[i]. Branch order replicates the
    // lowered plan branch order, pinned per index at construction.
    public int[] BranchWidths { get; }

    public int[][] BranchGroups { get; }

    public int[][] BranchStarts { get; }

    public int[][] BranchLengths { get; }

    public static Utf8UnitBranchSpanMap? TryCreate(Utf8ExecutionTree? tree, AsciiSimplePatternPlan plan)
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

        var expansions = TryExpand(node);
        if (expansions is null || expansions.Count == 0 || expansions.Count > 64)
        {
            return null;
        }

        foreach (var expansion in expansions)
        {
            if (expansion.Width <= 0)
            {
                return null;
            }
        }

        // Shapes without a lowered simple plan (exact literals, slice shapes, and
        // everything else) carry a default plan with no branches and decline here.
        // Per-index widths pin the replicated expansion order to the lowered branch
        // order, so winning-branch selection below cannot silently misattribute spans.
        if (plan.Branches is null || plan.Branches.Length != expansions.Count)
        {
            return null;
        }

        for (var branch = 0; branch < expansions.Count; branch++)
        {
            if (plan.Branches[branch].Length != expansions[branch].Width)
            {
                return null;
            }
        }

        var anyGroups = wholeMatchGroups.Count > 0;
        foreach (var expansion in expansions)
        {
            anyGroups |= expansion.Groups.Count > 0;
        }

        if (!anyGroups)
        {
            return null;
        }

        // Owned-tree capture numbers diverge from BCL group numbers for nested named
        // captures; translate every recorded group up front so shaping projects BCL
        // slots, declining on surprise (mirrors the literal branch-span map).
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

        var branchWidths = new int[expansions.Count];
        var branchGroups = new int[expansions.Count][];
        var branchStarts = new int[expansions.Count][];
        var branchLengths = new int[expansions.Count][];
        for (var branch = 0; branch < expansions.Count; branch++)
        {
            branchWidths[branch] = expansions[branch].Width;
            var groups = expansions[branch].Groups.ToArray();
            var starts = expansions[branch].Starts.ToArray();
            var lengths = expansions[branch].Lengths.ToArray();
            for (var i = 0; i < groups.Length; i++)
            {
                if (!slots.TryGetValue(groups[i], out var slot))
                {
                    return null;
                }

                groups[i] = slot;
            }

            branchGroups[branch] = groups;
            branchStarts[branch] = starts;
            branchLengths[branch] = lengths;
        }

        return new Utf8UnitBranchSpanMap(translatedWhole, branchWidths, branchGroups, branchStarts, branchLengths);
    }

    private sealed class UnitExpansion
    {
        internal int Width;
        internal readonly List<int> Groups = new();
        internal readonly List<int> Starts = new();
        internal readonly List<int> Lengths = new();
    }

    private static List<UnitExpansion>? TryExpand(Utf8ExecutionNode node)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.One:
                return [new UnitExpansion { Width = 1 }];
            case Utf8ExecutionNodeKind.Multi:
                if (node.Text is null)
                {
                    return null;
                }

                return [new UnitExpansion { Width = node.Text.Length }];
            case Utf8ExecutionNodeKind.Set:
                return [new UnitExpansion { Width = 1 }];
            case Utf8ExecutionNodeKind.Concatenate:
            {
                var combined = new List<UnitExpansion> { new() };
                foreach (var child in node.Children)
                {
                    var childExpansions = TryExpand(child);
                    if (childExpansions is null)
                    {
                        return null;
                    }

                    var crossed = new List<UnitExpansion>(combined.Count * childExpansions.Count);
                    foreach (var left in combined)
                    {
                        foreach (var right in childExpansions)
                        {
                            var merged = new UnitExpansion { Width = left.Width + right.Width };
                            merged.Groups.AddRange(left.Groups);
                            merged.Starts.AddRange(left.Starts);
                            merged.Lengths.AddRange(left.Lengths);
                            for (var i = 0; i < right.Groups.Count; i++)
                            {
                                merged.Groups.Add(right.Groups[i]);
                                merged.Starts.Add(left.Width + right.Starts[i]);
                                merged.Lengths.Add(right.Lengths[i]);
                            }

                            crossed.Add(merged);
                            if (crossed.Count > 64)
                            {
                                return null;
                            }
                        }
                    }

                    combined = crossed;
                }

                return combined;
            }
            case Utf8ExecutionNodeKind.Alternate:
            {
                var alternates = new List<UnitExpansion>();
                foreach (var child in node.Children)
                {
                    var childExpansions = TryExpand(child);
                    if (childExpansions is null)
                    {
                        return null;
                    }

                    alternates.AddRange(childExpansions);
                    if (alternates.Count > 64)
                    {
                        return null;
                    }
                }

                return alternates;
            }
            case Utf8ExecutionNodeKind.Group:
                if (node.Children.Count != 1)
                {
                    return null;
                }

                return TryExpand(node.Children[0]);
            case Utf8ExecutionNodeKind.Capture:
            {
                if (node.CaptureNumber <= 0 || node.Children.Count != 1)
                {
                    return null;
                }

                var body = TryExpand(node.Children[0]);
                if (body is null)
                {
                    return null;
                }

                foreach (var expansion in body)
                {
                    if (expansion.Groups.Contains(node.CaptureNumber))
                    {
                        return null;
                    }

                    expansion.Groups.Add(node.CaptureNumber);
                    expansion.Starts.Add(0);
                    expansion.Lengths.Add(expansion.Width);
                }

                return body;
            }
            default:
                return null;
        }
    }
}
