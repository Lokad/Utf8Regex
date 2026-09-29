using System.Text;
using System.Text.RegularExpressions;

namespace Lokad.Utf8Regex.Internal.Execution;

// R05: unit-space branch span maps for nested divergent simple patterns whose atoms
// are not literal-only (dots, sets, and shorthand classes consume exactly one UTF-16
// unit, halves included, exactly like BCL). Each pattern records whole-match wrapper
// groups plus every enclosed capture as fixed unit offsets, so detailed shaping
// projects group spans onto the owned unit-space discovery hit without executing the
// fallback engine. Only uniform shapes are admitted: every expanded branch must have
// the same token length and identical group spans, so no winning-branch identification
// is needed (first-ordinal-match-wins still selects the hit itself). The expanded
// branch multiset is checked against the lowered plan branches, so any construct the
// walker does not model (finite repeats, anchors, lookarounds, backreferences, empty
// nodes, and anything else) fails closed to the existing BCL routes. Non-uniform
// spans, branch-count or length disagreement, and more than 64 expansions decline;
// a null map means the shape keeps the existing BCL routes. Construction is eager and
// pure over the owned tree plus the lowered plan: no fallback engine is built here.
internal sealed class Utf8UnitBranchSpanMap
{
    private Utf8UnitBranchSpanMap(
        int[] wholeMatchGroups,
        int[] groups,
        int[] starts,
        int[] lengths,
        int width)
    {
        WholeMatchGroups = wholeMatchGroups;
        Groups = groups;
        Starts = starts;
        Lengths = lengths;
        Width = width;
    }

    // Groups spanning the entire match (captures wrapping the whole pattern).
    public int[] WholeMatchGroups { get; }

    // Static branch-invariant spans: Groups[i] spans (Starts[i], Lengths[i])
    // in UTF-16 units relative to the hit start, valid for every branch.
    public int[] Groups { get; }

    public int[] Starts { get; }

    public int[] Lengths { get; }

    // Total hit width in UTF-16 units shared by every branch.
    public int Width { get; }

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

        var width = expansions[0].Width;
        if (width <= 0)
        {
            return null;
        }

        foreach (var expansion in expansions)
        {
            if (expansion.Width != width || !SpansEqual(expansions[0], expansion))
            {
                return null;
            }
        }

        // Shapes without a lowered simple plan (exact literals, slice shapes, and
        // everything else) carry a default plan with no branches and decline here.
        if (plan.Branches is null || plan.Branches.Length != expansions.Count)
        {
            return null;
        }

        foreach (var branch in plan.Branches)
        {
            if (branch.Length != width)
            {
                return null;
            }
        }

        if (wholeMatchGroups.Count == 0 && expansions[0].Groups.Count == 0)
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

        var groups = expansions[0].Groups.ToArray();
        var starts = expansions[0].Starts.ToArray();
        var lengths = expansions[0].Lengths.ToArray();
        for (var i = 0; i < groups.Length; i++)
        {
            if (!slots.TryGetValue(groups[i], out var slot))
            {
                return null;
            }

            groups[i] = slot;
        }

        return new Utf8UnitBranchSpanMap(translatedWhole, groups, starts, lengths, width);
    }

    private static bool SpansEqual(UnitExpansion first, UnitExpansion second)
    {
        if (first.Groups.Count != second.Groups.Count)
        {
            return false;
        }

        for (var i = 0; i < first.Groups.Count; i++)
        {
            if (first.Groups[i] != second.Groups[i] ||
                first.Starts[i] != second.Starts[i] ||
                first.Lengths[i] != second.Lengths[i])
            {
                return false;
            }
        }

        return true;
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
