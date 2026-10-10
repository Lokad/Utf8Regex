using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

// R05: ordered expansion-span maps for single-alternation literal shapes whose branches
// may diverge (different lengths or group spans), and for pure multi-loop shapes with two
// to six optional loops over literal runs. Each pattern records the literal expansion
// text plus derived UTF-16 spans for its existing capture projection consumer.
// Detailed shaping identifies the winning pattern by ordinal text comparison
// against the owned discovery hit and projects scalar-aligned spans. Source order
// for branches and per-loop preference with rightmost unwinding are retained,
// so the first ordinal match wins. Nested divergent
// alternations and (0,1) loops inside alternation branches expand the same way. Exact-count
// loops over literal alternations expand per-iteration leftmost-major (the last iteration
// backtracks first), with loop-body captures taking their span from the last
// completed iteration. Nested loops, other quantifiers, empty entries, more than
// 64 entries, sets, anchors,
// lookarounds, backreferences, and empty nodes decline; a null map means
// the dispatcher selects another route. Construction is eager and pure over the owned
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

    public static Utf8AlternationBranchSpanMap? TryCreate(Utf8ExecutionTree? tree, Utf8SliceProgram captureProgram)
    {
        // The lowering tree already carries the catalog's final group numbers.
        // Stateful declarations use full owned capture histories.
        if (captureProgram.HasBalancing || captureProgram.HasSharedCaptureDeclarations || captureProgram.HasExplicitCaptureNumbers) return null;
        using var construction = Utf8ConstructionScope.Enter();
        using var storage = Utf8CaptureMapConstruction.Create(construction.Budget);
        var result = Create(tree, storage);
        if (result is not null) { storage.Complete(); construction.Complete(); }
        return result;

        static Utf8AlternationBranchSpanMap? Create(Utf8ExecutionTree? tree, Utf8CaptureMapConstruction storage)
        {
            if (tree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root ||
                root.Children.Count != 1)
            {
                return null;
            }

            var wholeMatchGroups = storage.Buffer<int>();
            var node = root.Children[0];
            while (true)
            {
                storage.Budget.ChargeWork(1);
                if (node.Kind == Utf8ExecutionNodeKind.Group && node.Children.Count == 1)
                {
                    node = node.Children[0];
                    continue;
                }

                if (node.Kind == Utf8ExecutionNodeKind.Capture && node.CaptureNumber > 0 && node.Children.Count == 1 &&
                    !SkipsToCountedLoop(node.Children[0], storage.Budget))
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
            if (SkipsToCountedLoop(node, storage.Budget))
            {
                return TryCreateExactCountPatterns(storage, node, wholeMatchGroups);
            }

            Utf8ExecutionNode? alternate = null;
            var alternateCount = 0;
            CountAlternates(node, ref alternate, ref alternateCount);
            if (alternateCount == 0)
            {
                return TryCreateLoopPatterns(storage, node, wholeMatchGroups);
            }

            if (alternateCount == 1 && alternate is not null && alternate.Children.Count > 0)
            {
                var single = TryCreateSingleAlternate(node, alternate, wholeMatchGroups);
                if (single is not null)
                {
                    return single;
                }
            }

            return TryCreateNestedPatterns(storage, node, wholeMatchGroups, alternateCount);

        }
    }

    // True when the node reaches a counted loop (Min == Max >= 1) through only
    // transparent group wrappers. Optional (0,1) loops, unbounded repetitions,
    // and ranged counts return false so the existing single/nested/loop paths
    // keep their shapes; only exact counts route to the expander below.
    private static bool SkipsToCountedLoop(Utf8ExecutionNode node, Utf8ConstructionBudget budget)
    {
        while (node.Kind == Utf8ExecutionNodeKind.Group && node.Children.Count == 1)
        {
            budget.ChargeWork(1);
            node = node.Children[0];
        }

        return (node.Kind == Utf8ExecutionNodeKind.Loop || node.Kind == Utf8ExecutionNodeKind.LazyLoop) &&
            node.Children.Count == 1 && node.Min == node.Max && node.Min >= 1;
    }

    private static Utf8AlternationBranchSpanMap? TryCreateExactCountPatterns(
        Utf8CaptureMapConstruction storage,
        Utf8ExecutionNode node,
        Utf8ConstructionBuffer<int> wholeMatchGroups)
    {
        // Unwrap captures around the loop itself: every one takes its span from
        // the last iteration (a repeated capture never spans
        // the whole match), while outer whole-match wrappers were already
        // collected above. Captures inside alternation branches stay out.
        var lastIterationGroups = storage.Buffer<int>();
        while (true)
        {
            storage.Budget.ChargeWork(1);
            if (node.Kind == Utf8ExecutionNodeKind.Group && node.Children.Count == 1)
            {
                node = node.Children[0];
                continue;
            }

            if (node.Kind == Utf8ExecutionNodeKind.Capture && node.CaptureNumber > 0 && node.Children.Count == 1)
            {
                storage.Budget.ChargeWork((ulong)lastIterationGroups.Count);
                if (lastIterationGroups.IndexOf(node.CaptureNumber) >= 0)
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
            storage.Budget.ChargeWork(1);
            if (loopBody.Kind == Utf8ExecutionNodeKind.Group && loopBody.Children.Count == 1)
            {
                loopBody = loopBody.Children[0];
                continue;
            }

            if (loopBody.Kind == Utf8ExecutionNodeKind.Capture && loopBody.CaptureNumber > 0 && loopBody.Children.Count == 1)
            {
                storage.Budget.ChargeWork((ulong)lastIterationGroups.Count);
                if (lastIterationGroups.IndexOf(loopBody.CaptureNumber) >= 0)
                {
                    return null;
                }

                lastIterationGroups.Add(loopBody.CaptureNumber);
                loopBody = loopBody.Children[0];
                continue;
            }

            break;
        }

        if (loopBody.Kind != Utf8ExecutionNodeKind.Alternate || loopBody.Children.Count == 0 || loopBody.Children.Count > 64)
        {
            return null;
        }

        var branchTexts = storage.Allocate<string>(loopBody.Children.Count, retained: false);
        for (var branch = 0; branch < loopBody.Children.Count; branch++)
        {
            if (!TryReadLiteralRun(storage, loopBody.Children[branch], out branchTexts[branch]) ||
                branchTexts[branch].Length == 0)
            {
                return null;
            }
        }

        // Cross iterations leftmost-major: ordered choice unwinds the last iteration
        // first, so earlier iterations vary slowest and source order holds
        // within each iteration (greedy and lazy enumerate identically for
        // fixed counts). More than 64 combinations decline like elsewhere.
        var entries = storage.Buffer<(string Text, int LastStart, int LastLength)>();
        entries.Add((string.Empty, 0, 0));
        for (var iteration = 0; iteration < repetitions; iteration++)
        {
            storage.Budget.ChargeWork(1);
            if (entries.Count > 64 / branchTexts.Length) return null;
            var next = storage.Buffer<(string Text, int LastStart, int LastLength)>();
            foreach (var entry in entries)
            {
                for (var branch = 0; branch < branchTexts.Length; branch++)
                {
                    storage.Budget.ChargeWork(1);
                    next.Add((storage.Concat(entry.Text, branchTexts[branch]), entry.Text.Length, branchTexts[branch].Length));
                }
            }

            entries = next;
        }

        var anyGroups = wholeMatchGroups.Count > 0 || lastIterationGroups.Count > 0;
        if (!anyGroups)
        {
            return null;
        }

        var wholeGroups = storage.Copy(wholeMatchGroups);

        var branchExpansions = storage.Allocate<string>(entries.Count);
        var branchGroups = storage.Allocate<int[]>(entries.Count);
        var branchStarts = storage.Allocate<int[]>(entries.Count);
        var branchLengths = storage.Allocate<int[]>(entries.Count);
        for (var entry = 0; entry < entries.Count; entry++)
        {
            if (entries[entry].Text.Length == 0)
            {
                return null;
            }

            var groups = storage.Allocate<int>(lastIterationGroups.Count);
            var starts = storage.Allocate<int>(lastIterationGroups.Count);
            var lengths = storage.Allocate<int>(lastIterationGroups.Count);
            for (var i = 0; i < lastIterationGroups.Count; i++)
            {
                storage.Budget.ChargeWork(1);
                groups[i] = lastIterationGroups[i];
                starts[i] = entries[entry].LastStart;
                lengths[i] = entries[entry].LastLength;
            }

            branchExpansions[entry] = entries[entry].Text;
            branchGroups[entry] = groups;
            branchStarts[entry] = starts;
            branchLengths[entry] = lengths;
        }

        return Allocate(storage,
            wholeGroups,
            branchExpansions,
            branchGroups,
            branchStarts,
            branchLengths);
    }

    private static Utf8AlternationBranchSpanMap Allocate(Utf8CaptureMapConstruction storage,
        int[] whole, string[] expansions, int[][] groups, int[][] starts, int[][] lengths) =>
        storage.Budget.AllocateObject((whole, expansions, groups, starts, lengths), static state =>
            new Utf8AlternationBranchSpanMap(state.whole, state.expansions, state.groups, state.starts, state.lengths));

    // Pattern interoperability text only. Every continuation and copy is admitted;
    // no recursive walker or growable BCL builder is needed.
    private static bool TryReadLiteralRun(Utf8CaptureMapConstruction storage, Utf8ExecutionNode node, out string text)
    {
        text = string.Empty;
        var builder = storage.Text();
        using var frames = Utf8ConstructionFrames<DiscoveryFrame>.Create(storage.Budget, new(node));
        while (frames.Count != 0)
        {
            storage.Budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var current = frame.Node;
            if (current.Kind == Utf8ExecutionNodeKind.One)
            {
                builder.Append(current.Ch); frames.Pop(); continue;
            }
            if (current.Kind == Utf8ExecutionNodeKind.Multi)
            {
                if (current.Text is null) return false;
                builder.Append(current.Text); frames.Pop(); continue;
            }
            if (current.Kind == Utf8ExecutionNodeKind.Group)
            {
                if (current.Children.Count != 1) return false;
            }
            else if (current.Kind != Utf8ExecutionNodeKind.Concatenate || current.Children.Count == 0) return false;
            if (frame.Index == current.Children.Count) { frames.Pop(); continue; }
            var child = current.Children[frame.Index++];
            frames[index] = frame;
            frames.Push(new(child));
        }
        text = builder.ToText();
        return true;
    }

    private static Utf8AlternationBranchSpanMap? TryCreateSingleAlternate(
        Utf8ExecutionNode node,
        Utf8ExecutionNode alternate,
        Utf8ConstructionBuffer<int> wholeMatchGroups)
    {
        using var construction = Utf8ConstructionScope.Enter();
        using var storage = Utf8CaptureMapConstruction.Create(construction.Budget);
        var result = CreateSingleAlternate(storage, node, alternate, wholeMatchGroups);
        if (result is not null) { storage.Complete(); construction.Complete(); }
        return result;
    }

    private static Utf8AlternationBranchSpanMap? CreateSingleAlternate(
        Utf8CaptureMapConstruction storage, Utf8ExecutionNode node,
        Utf8ExecutionNode alternate, Utf8ConstructionBuffer<int> wholeMatchGroups)
    {
        var branchExpansions = storage.Allocate<string>(alternate.Children.Count);
        var branchGroups = storage.Allocate<int[]>(alternate.Children.Count);
        var branchStarts = storage.Allocate<int[]>(alternate.Children.Count);
        var branchLengths = storage.Allocate<int[]>(alternate.Children.Count);
        var anyGroups = wholeMatchGroups.Count > 0;
        for (var branch = 0; branch < alternate.Children.Count; branch++)
        {
            var walker = LiteralWalker.Create(storage, -1, 0, alternate, branch);
            if (!walker.TryWalk(storage, node, false))
            {
                return null;
            }

            if (walker.Position == 0)
            {
                return null;
            }

            branchExpansions[branch] = walker.Expansion.ToText();
            branchGroups[branch] = storage.Copy(walker.Groups);
            branchStarts[branch] = storage.Copy(walker.Starts);
            branchLengths[branch] = storage.Copy(walker.Lengths);
            anyGroups |= walker.Groups.Count > 0;
        }

        if (!anyGroups)
        {
            return null;
        }

        var wholeGroups = storage.Copy(wholeMatchGroups);

        return Allocate(storage,
            wholeGroups,
            branchExpansions,
            branchGroups,
            branchStarts,
            branchLengths);
    }

    private static Utf8AlternationBranchSpanMap? TryCreateNestedPatterns(
        Utf8CaptureMapConstruction storage,
        Utf8ExecutionNode node,
        Utf8ConstructionBuffer<int> wholeMatchGroups,
        int alternateCount)
    {
        // Nested divergent alternations and loops inside alternation branches expand
        // into one flat ordered entry list: sequential children combine
        // leftmost-major with each child's preference-ordered options in turn, alternate
        // branches concatenate in source order, and (0,1) loops list present options
        // before absent when greedy and absent before present when lazy, mirroring the
        // probed backtracking order. Nested loops, other bounds, empty entries, and more
        // than 64 entries decline; shapes without any alternate already ran the single
        // and loop paths, so a null map selects the owned scalar capture route.
        if (alternateCount < 1)
        {
            return null;
        }

        var entries = TryExpandNested(storage, node, false);
        if (entries is null || entries.Count == 0 || entries.Count > 64)
        {
            return null;
        }

        var branchExpansions = storage.Allocate<string>(entries.Count);
        var branchGroups = storage.Allocate<int[]>(entries.Count);
        var branchStarts = storage.Allocate<int[]>(entries.Count);
        var branchLengths = storage.Allocate<int[]>(entries.Count);
        var anyGroups = wholeMatchGroups.Count > 0;
        for (var entry = 0; entry < entries.Count; entry++)
        {
            if (entries[entry].Text.Length == 0)
            {
                return null;
            }

            branchExpansions[entry] = entries[entry].Text;
            branchGroups[entry] = storage.Copy(entries[entry].Groups);
            branchStarts[entry] = storage.Copy(entries[entry].Starts);
            branchLengths[entry] = storage.Copy(entries[entry].Lengths);
            anyGroups |= entries[entry].Groups.Count > 0;
        }

        if (!anyGroups)
        {
            return null;
        }

        var wholeGroups = storage.Copy(wholeMatchGroups);

        return Allocate(storage,
            wholeGroups,
            branchExpansions,
            branchGroups,
            branchStarts,
            branchLengths);
    }

    private sealed class NestedEntry(string text, Utf8ConstructionBuffer<int> groups,
        Utf8ConstructionBuffer<int> starts, Utf8ConstructionBuffer<int> lengths)
    {
        internal readonly string Text = text;
        internal readonly Utf8ConstructionBuffer<int> Groups = groups;
        internal readonly Utf8ConstructionBuffer<int> Starts = starts;
        internal readonly Utf8ConstructionBuffer<int> Lengths = lengths;

        internal static NestedEntry Create(Utf8CaptureMapConstruction storage) => Create(storage, string.Empty);

        internal static NestedEntry Create(Utf8CaptureMapConstruction storage, string text)
        {
            var groups = storage.Buffer<int>();
            var starts = storage.Buffer<int>();
            var lengths = storage.Buffer<int>();
            return storage.Budget.AllocateObject((text, groups, starts, lengths), static state =>
                new NestedEntry(state.text, state.groups, state.starts, state.lengths));
        }
    }

    private static Utf8ConstructionBuffer<NestedEntry> Single(Utf8CaptureMapConstruction storage, string text)
    {
        var result = storage.Buffer<NestedEntry>();
        result.Add(NestedEntry.Create(storage, text));
        return result;
    }

    private struct ExpansionFrame(Utf8ExecutionNode node, bool inLoop)
    {
        internal readonly Utf8ExecutionNode Node = node;
        internal readonly bool InLoop = inLoop;
        internal int Stage;
        internal int Index;
        internal Utf8ConstructionBuffer<NestedEntry>? Entries;
    }

    private static Utf8ConstructionBuffer<NestedEntry>? TryExpandNested(Utf8CaptureMapConstruction storage, Utf8ExecutionNode node, bool inLoop)
    {
        var budget = storage.Budget;
        using var frames = Utf8ConstructionFrames<ExpansionFrame>.Create(budget, new(node, inLoop));
        using var results = Utf8ConstructionMap<Utf8ExecutionNode, Utf8ConstructionBuffer<NestedEntry>>.Create(budget, ReferenceEqualityComparer.Instance);
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            ExpansionFrame? child;
            bool accepted;
            try { accepted = Expand(ref frame, results, storage, out child); }
            finally { frames[index] = frame; }
            if (!accepted) return null;
            if (child is { } pending) frames.Push(pending);
            else frames.Pop();
        }
        var result = results[node];
        return result;
    }

    // Entries and their vectors share the admitted factory session. Aliased
    // entries are copied into final projections, never consumed with Take.
    private static bool Expand(ref ExpansionFrame frame,
        Utf8ConstructionMap<Utf8ExecutionNode, Utf8ConstructionBuffer<NestedEntry>> results,
        Utf8CaptureMapConstruction storage, out ExpansionFrame? child)
    {
        var budget = storage.Budget;
        child = null;
        var node = frame.Node;
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.One:
                Span<char> scalarPart = stackalloc char[1];
                scalarPart[0] = node.Ch;
                results[node] = Single(storage, budget.CopyText(scalarPart));
                return true;
            case Utf8ExecutionNodeKind.Multi:
                if (node.Text is null) return false;
                results[node] = Single(storage, node.Text);
                return true;
            case Utf8ExecutionNodeKind.Concatenate:
                if (frame.Stage == 0)
                {
                    if (node.Children.Count == 0) return false;
                    frame.Entries = Single(storage, string.Empty);
                    frame.Stage = 1;
                }
                else
                {
                    var combined = frame.Entries ?? throw new InvalidOperationException();
                    var part = results[node.Children[frame.Index]];
                    if (part.Count == 0) return false;
                    if (combined.Count > 64 / part.Count) return false;
                    var next = storage.Buffer<NestedEntry>();
                    for (var left = 0; left < combined.Count; left++)
                    for (var right = 0; right < part.Count; right++)
                    {
                        budget.ChargeWork(1);
                        var entry = NestedEntry.Create(storage, storage.Concat(combined[left].Text, part[right].Text));
                        entry.Groups.AddRange(combined[left].Groups.Span);
                        entry.Starts.AddRange(combined[left].Starts.Span);
                        entry.Lengths.AddRange(combined[left].Lengths.Span);
                        for (var span = 0; span < part[right].Groups.Count; span++)
                        {
                            budget.ChargeWork((ulong)combined[left].Groups.Count + 1);
                            if (combined[left].Groups.IndexOf(part[right].Groups[span]) >= 0) return false;
                            entry.Groups.Add(part[right].Groups[span]);
                            entry.Starts.Add(combined[left].Text.Length + part[right].Starts[span]);
                            entry.Lengths.Add(part[right].Lengths[span]);
                        }
                        next.Add(entry);
                    }
                    frame.Entries = next;
                    frame.Index++;
                }
                if (frame.Index < node.Children.Count)
                    child = new(node.Children[frame.Index], frame.InLoop);
                else results[node] = frame.Entries ?? throw new InvalidOperationException();
                return true;
            case Utf8ExecutionNodeKind.Group:
            case Utf8ExecutionNodeKind.Capture:
                if (frame.Stage == 0)
                {
                    if (node.Children.Count != 1 || (node.Kind == Utf8ExecutionNodeKind.Capture && node.CaptureNumber <= 0)) return false;
                    frame.Stage = 1;
                    child = new(node.Children[0], frame.InLoop);
                    return true;
                }
                var captured = results[node.Children[0]];
                if (node.Kind == Utf8ExecutionNodeKind.Capture)
                    foreach (var entry in captured)
                    {
                        budget.ChargeWork((ulong)entry.Groups.Count + 1);
                        if (entry.Groups.IndexOf(node.CaptureNumber) >= 0) return false;
                        entry.Groups.Add(node.CaptureNumber);
                        entry.Starts.Add(0);
                        entry.Lengths.Add(entry.Text.Length);
                    }
                results[node] = captured;
                return true;
            case Utf8ExecutionNodeKind.Alternate:
                if (frame.Stage == 0)
                {
                    if (node.Children.Count == 0) return false;
                    frame.Entries = storage.Buffer<NestedEntry>();
                    frame.Stage = 1;
                }
                else
                {
                    var branches = frame.Entries ?? throw new InvalidOperationException();
                    var expanded = results[node.Children[frame.Index]];
                    if (expanded.Count == 0) return false;
                    if (expanded.Count > 64 - branches.Count) return false;
                    branches.AddRange(expanded.Span);
                    frame.Index++;
                }
                if (frame.Index < node.Children.Count)
                    child = new(node.Children[frame.Index], frame.InLoop);
                else results[node] = frame.Entries ?? throw new InvalidOperationException();
                return true;
            case Utf8ExecutionNodeKind.Loop:
            case Utf8ExecutionNodeKind.LazyLoop:
                if (frame.Stage == 0)
                {
                    if (frame.InLoop || node.Min != 0 || node.Max != 1 || node.Children.Count != 1) return false;
                    frame.Stage = 1;
                    child = new(node.Children[0], true);
                    return true;
                }
                var choices = results[node.Children[0]];
                if (choices.Count == 0) return false;
                if (choices.Count >= 64) return false;
                var options = storage.Buffer<NestedEntry>();
                if (node.Kind == Utf8ExecutionNodeKind.Loop)
                {
                    options.AddRange(choices.Span);
                    options.Add(NestedEntry.Create(storage));
                }
                else
                {
                    options.Add(NestedEntry.Create(storage));
                    options.AddRange(choices.Span);
                }
                results[node] = options;
                return true;
            default:
                return false;
        }
    }

    private static Utf8AlternationBranchSpanMap? TryCreateLoopPatterns(
        Utf8CaptureMapConstruction storage,
        Utf8ExecutionNode node,
        Utf8ConstructionBuffer<int> wholeMatchGroups)
    {
        // Pure multi-loop shapes: two to six optional loops over literal runs, no
        // alternation. Single loops belong to the span table; deeper counts need another route.
        // Six bounds the eager per-pattern expansion (64 patterns) while covering
        // the witnessed five-loop debt; the ordering rule below is count-generic.
        // Stored pattern order follows scalar choice (per-loop preference with the
        // rightmost loop unwinding first), so the shared first-ordinal-match shaping
        // attributes exactly like the oracle.
        if (!TryCollectLoops(node, out var loopCount, out var greedyMask) || loopCount < 2)
        {
            return null;
        }

        var count = 1 << loopCount;
        var expansions = storage.Allocate<string>(count);
        var groups = storage.Allocate<int[]>(count);
        var starts = storage.Allocate<int[]>(count);
        var lengths = storage.Allocate<int[]>(count);
        var anyGroups = wholeMatchGroups.Count > 0;
        for (var pattern = 0; pattern < count; pattern++)
        {
            // Reverse ordinal bits so the rightmost loop varies first, then
            // toggle each loop away from its greedy/lazy source preference.
            var presentMask = greedyMask;
            for (var loop = 0; loop < loopCount; loop++)
            {
                storage.Budget.ChargeWork(1);
                if ((pattern & (1 << (loopCount - 1 - loop))) != 0) presentMask ^= 1 << loop;
            }
            var walker = LiteralWalker.Create(storage, loopCount, presentMask, null, -1);
            if (!walker.TryWalk(storage, node, inLoop: false))
            {
                return null;
            }

            expansions[pattern] = walker.Expansion.ToText();
            groups[pattern] = storage.Copy(walker.Groups);
            starts[pattern] = storage.Copy(walker.Starts);
            lengths[pattern] = storage.Copy(walker.Lengths);
            anyGroups |= walker.Groups.Count > 0;
        }

        if (!anyGroups)
        {
            return null;
        }

        var wholeGroups = storage.Copy(wholeMatchGroups);

        return Allocate(storage,
            wholeGroups,
            expansions,
            groups,
            starts,
            lengths);
    }

    private struct DiscoveryFrame(Utf8ExecutionNode node)
    {
        internal Utf8ExecutionNode Node = node;
        internal int Index;
        internal bool Entered;
    }

    private static bool TryCollectLoops(Utf8ExecutionNode node, out int count, out int greedyMask)
    {
        count = 0;
        greedyMask = 0;
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<DiscoveryFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var current = frame.Node;
            if (!frame.Entered)
            {
                if (current.Kind is Utf8ExecutionNodeKind.Loop or Utf8ExecutionNodeKind.LazyLoop)
                {
                    if (count == 6) return false;
                    if (current.Kind == Utf8ExecutionNodeKind.Loop) greedyMask |= 1 << count;
                    count++;
                    frames.Pop();
                    continue;
                }
                if (current.Kind == Utf8ExecutionNodeKind.Alternate) return false;
                frame.Entered = true;
            }
            if (frame.Index == current.Children.Count) { frames.Pop(); continue; }
            var child = current.Children[frame.Index++];
            frames[index] = frame;
            frames.Push(new(child));
        }
        return true;
    }

    // A selected-branch/optional-loop projection. The complete ten-field
    // walker state, expansion text, and span vectors have separate loans.
    private sealed class LiteralWalker
    {
        private readonly int _loopCount;
        private readonly int _presentMask;
        private readonly Utf8ExecutionNode? _alternate;
        private readonly int _branch;
        private int _cursor;
        internal readonly Utf8ConstructionText Expansion;
        internal int Position;
        internal readonly Utf8ConstructionBuffer<int> Groups;
        internal readonly Utf8ConstructionBuffer<int> Starts;
        internal readonly Utf8ConstructionBuffer<int> Lengths;

        private LiteralWalker(int loopCount, int presentMask, Utf8ExecutionNode? alternate, int branch,
            Utf8ConstructionText expansion, Utf8ConstructionBuffer<int> groups,
            Utf8ConstructionBuffer<int> starts, Utf8ConstructionBuffer<int> lengths)
        {
            _loopCount = loopCount; _presentMask = presentMask; _alternate = alternate; _branch = branch;
            Expansion = expansion; Groups = groups; Starts = starts; Lengths = lengths;
        }

        internal static LiteralWalker Create(Utf8CaptureMapConstruction storage, int loopCount,
            int presentMask, Utf8ExecutionNode? alternate, int branch)
        {
            var expansion = storage.Text();
            var groups = storage.Buffer<int>();
            var starts = storage.Buffer<int>();
            var lengths = storage.Buffer<int>();
            return storage.Budget.AllocateObject((loopCount, presentMask, alternate, branch, 0, expansion, 0, groups, starts, lengths),
                static state => new LiteralWalker(state.loopCount, state.presentMask, state.alternate, state.branch,
                    state.expansion, state.groups, state.starts, state.lengths));
        }

        private struct Frame(Utf8ExecutionNode node, bool inLoop)
        {
            internal readonly Utf8ExecutionNode Node = node;
            internal readonly bool InLoop = inLoop;
            internal int Stage;
            internal int Index;
            internal int Start;
        }

        internal bool TryWalk(Utf8CaptureMapConstruction storage, Utf8ExecutionNode node, bool inLoop)
        {
            var budget = storage.Budget;
            using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(node, inLoop));
            while (frames.Count != 0)
            {
                budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                Frame? child;
                bool accepted;
                try { accepted = Advance(ref frame, budget, out child); }
                finally { frames[index] = frame; }
                if (!accepted) return false;
                if (child is { } pending) frames.Push(pending);
                else frames.Pop();
            }
            return true;
        }

        private bool Advance(ref Frame frame, Utf8ConstructionBudget budget, out Frame? child)
        {
            child = null;
            var node = frame.Node;
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.One:
                    Position++;
                    Expansion.Append(node.Ch);
                    return true;
                case Utf8ExecutionNodeKind.Multi:
                    if (node.Text is null) return false;
                    Position += node.Text.Length;
                    Expansion.Append(node.Text);
                    return true;
                case Utf8ExecutionNodeKind.Concatenate:
                    if (frame.Index < node.Children.Count)
                        child = new(node.Children[frame.Index++], frame.InLoop);
                    return true;
                case Utf8ExecutionNodeKind.Group:
                    if (frame.Stage == 0)
                    {
                        if (node.Children.Count != 1) return false;
                        frame.Stage = 1;
                        child = new(node.Children[0], frame.InLoop);
                    }
                    return true;
                case Utf8ExecutionNodeKind.Capture:
                    if (frame.Stage == 0)
                    {
                        budget.ChargeWork((ulong)Groups.Count);
                        if (node.CaptureNumber <= 0 || node.Children.Count != 1 || Groups.IndexOf(node.CaptureNumber) >= 0) return false;
                        frame.Start = Position;
                        frame.Stage = 1;
                        child = new(node.Children[0], frame.InLoop);
                    }
                    else
                    {
                        Groups.Add(node.CaptureNumber);
                        Starts.Add(frame.Start);
                        Lengths.Add(Position - frame.Start);
                    }
                    return true;
                case Utf8ExecutionNodeKind.Alternate:
                    if (!ReferenceEquals(node, _alternate) || _branch < 0 || _branch >= node.Children.Count) return false;
                    if (frame.Stage == 0)
                    {
                        frame.Stage = 1;
                        child = new(node.Children[_branch], false);
                    }
                    return true;
                case Utf8ExecutionNodeKind.Loop:
                case Utf8ExecutionNodeKind.LazyLoop:
                    if (_loopCount < 0 || frame.InLoop || node.Min != 0 || node.Max != 1 || node.Children.Count != 1) return false;
                    if (frame.Stage == 0)
                    {
                        var index = _cursor++;
                        if ((uint)index >= (uint)_loopCount) return false;
                        frame.Stage = 1;
                        if ((_presentMask & (1 << index)) != 0) child = new(node.Children[0], true);
                    }
                    return true;
                default:
                    return false;
            }
        }
    }

    private static void CountAlternates(Utf8ExecutionNode node, ref Utf8ExecutionNode? found, ref int count)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<DiscoveryFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            var current = frame.Node;
            if (!frame.Entered)
            {
                if (current.Kind == Utf8ExecutionNodeKind.Alternate)
                {
                    count++;
                    found ??= current;
                }
                frame.Entered = true;
            }
            if (frame.Index == current.Children.Count) { frames.Pop(); continue; }
            var child = current.Children[frame.Index++];
            frames[index] = frame;
            frames.Push(new(child));
        }
    }

}
