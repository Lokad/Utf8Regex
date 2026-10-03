using System.Text;

namespace Lokad.Utf8Regex.Internal.Execution;

// R05: ordered expansion-span maps for single-alternation literal shapes whose branches
// may diverge (different lengths or group spans), and for pure multi-loop shapes with two
// to six optional loops over literal runs. Each pattern records the literal expansion
// text plus every enclosed capture fixed UTF-16 range, so detailed shaping identifies
// the winning pattern by ordinal text comparison against the owned discovery hit and
// projects its spans without executing the fallback engine. Stored order is the BCL
// choice order (source order for branches; per-loop preference with rightmost unwinding
// for loops, as probed on .NET 10), so the first ordinal match wins. Nested divergent
// alternations and (0,1) loops inside alternation branches expand the same way. Exact-count
// loops over literal alternations expand per-iteration leftmost-major (the last iteration
// backtracks first), with loop-body captures taking their span from the last iteration
// exactly like BCL. Nested loops, other quantifiers, empty entries, more than 64 entries, sets, anchors,
// lookarounds, backreferences, and empty nodes decline; a null map means
// the shape keeps the existing BCL routes. Construction is eager and pure over the owned
// tree: no fallback engine is built here.
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

            if (node.Kind == Utf8ExecutionNodeKind.Capture && node.CaptureNumber > 0 && node.Children.Count == 1 &&
                !SkipsToCountedLoop(node.Children[0]))
            {
                wholeMatchGroups.Add(node.CaptureNumber);
                node = node.Children[0];
                continue;
            }

            break;
        }

        // Captures wrapping a counted loop take their span from the last iteration
        // (never the whole match), so they route to the exact-count expander below
        // instead of the whole-match list above.
        if (SkipsToCountedLoop(node))
        {
            return TryCreateExactCountPatterns(root.Children[0], node, wholeMatchGroups);
        }

        Utf8ExecutionNode? alternate = null;
        var alternateCount = 0;
        CountAlternates(node, ref alternate, ref alternateCount);
        if (alternateCount == 0)
        {
            return TryCreateLoopPatterns(root.Children[0], node, wholeMatchGroups);
        }

        if (alternateCount == 1 && alternate is not null && alternate.Children.Count > 0)
        {
            var single = TryCreateSingleAlternate(root.Children[0], node, alternate, wholeMatchGroups);
            if (single is not null)
            {
                return single;
            }
        }

        return TryCreateNestedPatterns(root.Children[0], node, wholeMatchGroups, alternateCount);
    }

    // True when the node reaches a counted loop (Min == Max >= 1) through only
    // transparent group wrappers. Optional (0,1) loops, unbounded repetitions,
    // and ranged counts return false so the existing single/nested/loop paths
    // keep their shapes; only exact counts route to the expander below.
    private static bool SkipsToCountedLoop(Utf8ExecutionNode node)
    {
        while (node.Kind == Utf8ExecutionNodeKind.Group && node.Children.Count == 1)
        {
            node = node.Children[0];
        }

        return (node.Kind == Utf8ExecutionNodeKind.Loop || node.Kind == Utf8ExecutionNodeKind.LazyLoop) &&
            node.Children.Count == 1 && node.Min == node.Max && node.Min >= 1;
    }

    private static Utf8AlternationBranchSpanMap? TryCreateExactCountPatterns(
        Utf8ExecutionNode body,
        Utf8ExecutionNode node,
        List<int> wholeMatchGroups)
    {
        // Unwrap captures around the loop itself: every one takes its span from
        // the last iteration exactly like BCL (a repeated capture never spans
        // the whole match), while outer whole-match wrappers were already
        // collected above. Captures inside alternation branches stay out.
        var lastIterationGroups = new List<int>();
        while (true)
        {
            if (node.Kind == Utf8ExecutionNodeKind.Group && node.Children.Count == 1)
            {
                node = node.Children[0];
                continue;
            }

            if (node.Kind == Utf8ExecutionNodeKind.Capture && node.CaptureNumber > 0 && node.Children.Count == 1)
            {
                if (lastIterationGroups.Contains(node.CaptureNumber))
                {
                    return null;
                }

                lastIterationGroups.Add(node.CaptureNumber);
                node = node.Children[0];
                continue;
            }

            break;
        }

        if ((node.Kind != Utf8ExecutionNodeKind.Loop && node.Kind != Utf8ExecutionNodeKind.LazyLoop) ||
            node.Children.Count != 1 || node.Min != node.Max || node.Min < 1)
        {
            return null;
        }

        // The loop body must be a literal alternation: every branch unwraps
        // through transparent groups to a non-empty literal run. Captures,
        // nested loops, sets, anchors, and lookarounds inside branches stay out.
        var repetitions = node.Min;
        var loopBody = node.Children[0];
        while (true)
        {
            if (loopBody.Kind == Utf8ExecutionNodeKind.Group && loopBody.Children.Count == 1)
            {
                loopBody = loopBody.Children[0];
                continue;
            }

            if (loopBody.Kind == Utf8ExecutionNodeKind.Capture && loopBody.CaptureNumber > 0 && loopBody.Children.Count == 1)
            {
                if (lastIterationGroups.Contains(loopBody.CaptureNumber))
                {
                    return null;
                }

                lastIterationGroups.Add(loopBody.CaptureNumber);
                loopBody = loopBody.Children[0];
                continue;
            }

            break;
        }

        if (loopBody.Kind != Utf8ExecutionNodeKind.Alternate || loopBody.Children.Count == 0)
        {
            return null;
        }

        var branchTexts = new string[loopBody.Children.Count];
        for (var branch = 0; branch < loopBody.Children.Count; branch++)
        {
            if (!TryReadLiteralRun(loopBody.Children[branch], out branchTexts[branch]) ||
                branchTexts[branch].Length == 0)
            {
                return null;
            }
        }

        // Cross iterations leftmost-major: BCL backtracks the last iteration
        // first, so earlier iterations vary slowest and source order holds
        // within each iteration (greedy and lazy enumerate identically for
        // fixed counts). More than 64 combinations decline like elsewhere.
        var entries = new List<(string Text, int LastStart, int LastLength)> { ("", 0, 0) };
        for (var iteration = 0; iteration < repetitions; iteration++)
        {
            var next = new List<(string Text, int LastStart, int LastLength)>();
            foreach (var entry in entries)
            {
                for (var branch = 0; branch < branchTexts.Length; branch++)
                {
                    next.Add((entry.Text + branchTexts[branch], entry.Text.Length, branchTexts[branch].Length));
                    if (next.Count > 64)
                    {
                        return null;
                    }
                }
            }

            entries = next;
        }

        var anyGroups = wholeMatchGroups.Count > 0 || lastIterationGroups.Count > 0;
        if (!anyGroups)
        {
            return null;
        }

        var slots = Utf8BclSlotTranslator.TryCreateMap(body);
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

        var branchExpansions = new string[entries.Count];
        var branchGroups = new int[entries.Count][];
        var branchStarts = new int[entries.Count][];
        var branchLengths = new int[entries.Count][];
        for (var entry = 0; entry < entries.Count; entry++)
        {
            if (entries[entry].Text.Length == 0)
            {
                return null;
            }

            var groups = new int[lastIterationGroups.Count];
            var starts = new int[lastIterationGroups.Count];
            var lengths = new int[lastIterationGroups.Count];
            for (var i = 0; i < lastIterationGroups.Count; i++)
            {
                if (!slots.TryGetValue(lastIterationGroups[i], out var slot))
                {
                    return null;
                }

                groups[i] = slot;
                starts[i] = entries[entry].LastStart;
                lengths[i] = entries[entry].LastLength;
            }

            branchExpansions[entry] = entries[entry].Text;
            branchGroups[entry] = groups;
            branchStarts[entry] = starts;
            branchLengths[entry] = lengths;
        }

        return new Utf8AlternationBranchSpanMap(
            translatedWhole,
            branchExpansions,
            branchGroups,
            branchStarts,
            branchLengths);
    }

    // Reads a literal run (single characters, literal text, and concatenations
    // thereof, through transparent groups) as text. Anything else declines.
    private static bool TryReadLiteralRun(Utf8ExecutionNode node, out string text)
    {
        var builder = new System.Text.StringBuilder();
        if (!AppendLiteralRun(node, builder))
        {
            text = string.Empty;
            return false;
        }

        text = builder.ToString();
        return true;
    }

    private static bool AppendLiteralRun(Utf8ExecutionNode node, System.Text.StringBuilder builder)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.One:
                builder.Append(node.Ch);
                return true;
            case Utf8ExecutionNodeKind.Multi:
                if (node.Text is null)
                {
                    return false;
                }

                builder.Append(node.Text);
                return true;
            case Utf8ExecutionNodeKind.Concatenate:
                foreach (var child in node.Children)
                {
                    if (!AppendLiteralRun(child, builder))
                    {
                        return false;
                    }
                }

                return node.Children.Count > 0;
            case Utf8ExecutionNodeKind.Group:
                if (node.Children.Count != 1)
                {
                    return false;
                }

                return AppendLiteralRun(node.Children[0], builder);
            default:
                return false;
        }
    }

    private static Utf8AlternationBranchSpanMap? TryCreateSingleAlternate(
        Utf8ExecutionNode body,
        Utf8ExecutionNode node,
        Utf8ExecutionNode alternate,
        List<int> wholeMatchGroups)
    {
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
        var slots = Utf8BclSlotTranslator.TryCreateMap(body);
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

    private static Utf8AlternationBranchSpanMap? TryCreateNestedPatterns(
        Utf8ExecutionNode body,
        Utf8ExecutionNode node,
        List<int> wholeMatchGroups,
        int alternateCount)
    {
        // Nested divergent alternations and loops inside alternation branches expand
        // recursively into one flat BCL-ordered entry list: sequential children combine
        // leftmost-major with each child's preference-ordered options in turn, alternate
        // branches concatenate in source order, and (0,1) loops list present options
        // before absent when greedy and absent before present when lazy, mirroring the
        // probed backtracking order. Nested loops, other bounds, empty entries, and more
        // than 64 entries decline; shapes without any alternate already ran the single
        // and loop paths, so a null map keeps the existing BCL routes.
        if (alternateCount < 1)
        {
            return null;
        }

        var entries = TryExpandNested(node, false);
        if (entries is null || entries.Count == 0 || entries.Count > 64)
        {
            return null;
        }

        var branchExpansions = new string[entries.Count];
        var branchGroups = new int[entries.Count][];
        var branchStarts = new int[entries.Count][];
        var branchLengths = new int[entries.Count][];
        var anyGroups = wholeMatchGroups.Count > 0;
        for (var entry = 0; entry < entries.Count; entry++)
        {
            if (entries[entry].Text.Length == 0)
            {
                return null;
            }

            branchExpansions[entry] = entries[entry].Text;
            branchGroups[entry] = entries[entry].Groups.ToArray();
            branchStarts[entry] = entries[entry].Starts.ToArray();
            branchLengths[entry] = entries[entry].Lengths.ToArray();
            anyGroups |= entries[entry].Groups.Count > 0;
        }

        if (!anyGroups)
        {
            return null;
        }

        // Same BCL slot translation as the single-alternation path: owned-tree capture
        // numbers diverge from BCL group numbers for nested named captures.
        var slots = Utf8BclSlotTranslator.TryCreateMap(body);
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

    private sealed class NestedEntry
    {
        internal string Text = string.Empty;
        internal readonly List<int> Groups = new();
        internal readonly List<int> Starts = new();
        internal readonly List<int> Lengths = new();
    }

    private static List<NestedEntry>? TryExpandNested(Utf8ExecutionNode node, bool inLoop)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.One:
            {
                return new List<NestedEntry> { new NestedEntry { Text = new string(node.Ch, 1) } };
            }
            case Utf8ExecutionNodeKind.Multi:
            {
                if (node.Text is null)
                {
                    return null;
                }

                return new List<NestedEntry> { new NestedEntry { Text = node.Text } };
            }
            case Utf8ExecutionNodeKind.Concatenate:
            {
                if (node.Children.Count == 0)
                {
                    return null;
                }

                var combined = new List<NestedEntry> { new NestedEntry() };
                for (var child = 0; child < node.Children.Count; child++)
                {
                    var part = TryExpandNested(node.Children[child], inLoop);
                    if (part is null || part.Count == 0)
                    {
                        return null;
                    }

                    var next = new List<NestedEntry>(combined.Count * part.Count);
                    for (var left = 0; left < combined.Count; left++)
                    {
                        for (var right = 0; right < part.Count; right++)
                        {
                            var entry = new NestedEntry
                            {
                                Text = combined[left].Text + part[right].Text,
                            };
                            entry.Groups.AddRange(combined[left].Groups);
                            entry.Starts.AddRange(combined[left].Starts);
                            entry.Lengths.AddRange(combined[left].Lengths);
                            for (var span = 0; span < part[right].Groups.Count; span++)
                            {
                                if (combined[left].Groups.Contains(part[right].Groups[span]))
                                {
                                    return null;
                                }

                                entry.Groups.Add(part[right].Groups[span]);
                                entry.Starts.Add(combined[left].Text.Length + part[right].Starts[span]);
                                entry.Lengths.Add(part[right].Lengths[span]);
                            }

                            next.Add(entry);
                            if (next.Count > 64)
                            {
                                return null;
                            }
                        }
                    }

                    combined = next;
                }

                return combined;
            }
            case Utf8ExecutionNodeKind.Group:
            {
                if (node.Children.Count != 1)
                {
                    return null;
                }

                return TryExpandNested(node.Children[0], inLoop);
            }
            case Utf8ExecutionNodeKind.Capture:
            {
                if (node.CaptureNumber <= 0 || node.Children.Count != 1)
                {
                    return null;
                }

                var expanded = TryExpandNested(node.Children[0], inLoop);
                if (expanded is null)
                {
                    return null;
                }

                foreach (var entry in expanded)
                {
                    if (entry.Groups.Contains(node.CaptureNumber))
                    {
                        return null;
                    }

                    entry.Groups.Add(node.CaptureNumber);
                    entry.Starts.Add(0);
                    entry.Lengths.Add(entry.Text.Length);
                }

                return expanded;
            }
            case Utf8ExecutionNodeKind.Alternate:
            {
                if (node.Children.Count == 0)
                {
                    return null;
                }

                var branches = new List<NestedEntry>();
                for (var branch = 0; branch < node.Children.Count; branch++)
                {
                    var expanded = TryExpandNested(node.Children[branch], inLoop);
                    if (expanded is null || expanded.Count == 0)
                    {
                        return null;
                    }

                    branches.AddRange(expanded);
                    if (branches.Count > 64)
                    {
                        return null;
                    }
                }

                return branches;
            }
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
            {
                if (inLoop || node.Min != 0 || node.Max != 1 || node.Children.Count != 1)
                {
                    return null;
                }

                var expanded = TryExpandNested(node.Children[0], true);
                if (expanded is null || expanded.Count == 0)
                {
                    return null;
                }

                var options = new List<NestedEntry>(expanded.Count + 1);
                if (node.Kind == Utf8ExecutionNodeKind.Loop)
                {
                    options.AddRange(expanded);
                    options.Add(new NestedEntry());
                }
                else
                {
                    options.Add(new NestedEntry());
                    options.AddRange(expanded);
                }

                return options;
            }
            default:
                return null;
        }
    }

    private static Utf8AlternationBranchSpanMap? TryCreateLoopPatterns(
        Utf8ExecutionNode body,
        Utf8ExecutionNode node,
        List<int> wholeMatchGroups)
    {
        // Pure multi-loop shapes: two to six optional loops over literal runs, no
        // alternation. Single loops belong to the span table; deeper counts stay BCL.
        // Six bounds the eager per-pattern expansion (64 patterns) while covering
        // the witnessed five-loop debt; the ordering rule below is count-generic.
        // Stored pattern order is the BCL choice order (per-loop preference with the
        // rightmost loop unwinding first), so the shared first-ordinal-match shaping
        // attributes exactly like the oracle.
        var greedy = new List<bool>();
        if (!TryCollectLoops(node, greedy) || greedy.Count < 2 || greedy.Count > 6)
        {
            return null;
        }

        var order = BuildPatternOrder(greedy);
        var expansions = new string[order.Length];
        var groups = new int[order.Length][];
        var starts = new int[order.Length][];
        var lengths = new int[order.Length][];
        var anyGroups = wholeMatchGroups.Count > 0;
        for (var pattern = 0; pattern < order.Length; pattern++)
        {
            var walker = new PatternWalker(ToPresent(greedy, order[pattern]));
            if (!walker.TryWalk(node, inLoop: false))
            {
                return null;
            }

            expansions[pattern] = walker.Expansion.ToString();
            groups[pattern] = walker.Groups.ToArray();
            starts[pattern] = walker.Starts.ToArray();
            lengths[pattern] = walker.Lengths.ToArray();
            anyGroups |= walker.Groups.Count > 0;
        }

        if (!anyGroups)
        {
            return null;
        }

        var slots = Utf8BclSlotTranslator.TryCreateMap(body);
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

        for (var pattern = 0; pattern < groups.Length; pattern++)
        {
            for (var i = 0; i < groups[pattern].Length; i++)
            {
                if (!slots.TryGetValue(groups[pattern][i], out var branchSlot))
                {
                    return null;
                }

                groups[pattern][i] = branchSlot;
            }
        }

        return new Utf8AlternationBranchSpanMap(
            translatedWhole,
            expansions,
            groups,
            starts,
            lengths);
    }

    private static bool TryCollectLoops(Utf8ExecutionNode node, List<bool> greedy)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                greedy.Add(node.Kind == Utf8ExecutionNodeKind.Loop);
                return true;
            case Utf8ExecutionNodeKind.Alternate:
                return false;
            default:
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (!TryCollectLoops(node.Children[i], greedy))
                    {
                        return false;
                    }
                }

                return true;
        }
    }

    private static int[] BuildPatternOrder(List<bool> greedy)
    {
        var order = new List<int>();
        CollectPatternOrder(greedy, 0, 0, order);
        return order.ToArray();
    }

    private static void CollectPatternOrder(List<bool> greedy, int index, int mask, List<int> order)
    {
        if (index >= greedy.Count)
        {
            order.Add(mask);
            return;
        }

        var preferred = greedy[index] ? 1 : 0;
        CollectPatternOrder(greedy, index + 1, mask | (preferred << index), order);
        CollectPatternOrder(greedy, index + 1, mask | ((1 - preferred) << index), order);
    }

    private static bool[] ToPresent(List<bool> greedy, int mask)
    {
        var present = new bool[greedy.Count];
        for (var i = 0; i < greedy.Count; i++)
        {
            present[i] = (mask & (1 << i)) != 0;
        }

        return present;
    }

    private sealed class PatternWalker
    {
        private readonly bool[] _present;
        private int _cursor;
        internal readonly StringBuilder Expansion = new();
        internal int Position;
        internal readonly List<int> Groups = new();
        internal readonly List<int> Starts = new();
        internal readonly List<int> Lengths = new();

        internal PatternWalker(bool[] present)
        {
            _present = present;
        }

        internal bool TryWalk(Utf8ExecutionNode node, bool inLoop)
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
                        if (!TryWalk(node.Children[i], inLoop))
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

                    return TryWalk(node.Children[0], inLoop);
                case Utf8ExecutionNodeKind.Capture:
                    if (node.CaptureNumber <= 0 || node.Children.Count != 1 || Groups.Contains(node.CaptureNumber))
                    {
                        return false;
                    }

                    var start = Position;
                    if (!TryWalk(node.Children[0], inLoop))
                    {
                        return false;
                    }

                    Groups.Add(node.CaptureNumber);
                    Starts.Add(start);
                    Lengths.Add(Position - start);
                    return true;
                case Utf8ExecutionNodeKind.Loop:
                case Utf8ExecutionNodeKind.LazyLoop:
                    if (inLoop || node.Min != 0 || node.Max != 1 || node.Children.Count != 1)
                    {
                        return false;
                    }

                    var index = _cursor++;
                    if ((uint)index >= (uint)_present.Length)
                    {
                        return false;
                    }

                    if (!_present[index])
                    {
                        return true;
                    }

                    return TryWalk(node.Children[0], inLoop: true);
                default:
                    return false;
            }
        }
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
