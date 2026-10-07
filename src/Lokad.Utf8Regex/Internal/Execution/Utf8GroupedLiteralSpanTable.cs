using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

// R05: fixed-span capture map for single exact literals whose captures partition the
// literal (concatenated runs wrapped in capturing or non-capturing groups), plus at most
// one optional single-iteration loop over a literal run and branch-invariant alternations
// whose branches agree group for group on spans and end at the same position. Each
// recorded group spans a fixed UTF-16 range of the discovered literal, so detailed shaping
// projects every group from the owned discovery hit without executing the fallback engine;
// the hit length selects the present or absent projection for optional shapes. Line/begin/end
// anchors (Bol, Eol, Beginning, Start, EndZ, End) are zero-width: owned discovery enforces
// hit position while group spans relative to the hit start pass through unchanged. Other
// quantifiers, divergent alternations, loops inside alternations, multi-char sets,
// lookarounds, backreferences, boundaries, and empty nodes decline this optimization;
// the dispatcher selects another route. Construction is eager and pure over the owned tree: no fallback
// engine is built here.
internal sealed class Utf8GroupedLiteralSpanTable
{
    private Utf8GroupedLiteralSpanTable(
        int totalLengthInUtf16,
        int[] groups,
        int[] startsInLiteral,
        int[] lengthsInLiteral,
        bool hasOptionalLoop,
        int totalLengthWithoutLoopInUtf16,
        int[] absentStartsInLiteral,
        int[] absentLengthsInLiteral)
    {
        TotalLengthInUtf16 = totalLengthInUtf16;
        Groups = groups;
        StartsInLiteral = startsInLiteral;
        LengthsInLiteral = lengthsInLiteral;
        HasOptionalLoop = hasOptionalLoop;
        TotalLengthWithoutLoopInUtf16 = totalLengthWithoutLoopInUtf16;
        AbsentStartsInLiteral = absentStartsInLiteral;
        AbsentLengthsInLiteral = absentLengthsInLiteral;
    }

    // Total literal length in UTF-16 code units; shaping verifies the discovery hit length.
    public int TotalLengthInUtf16 { get; }

    // Parallel arrays: Groups[i] spans (StartsInLiteral[i], LengthsInLiteral[i]) in the literal.
    public int[] Groups { get; }

    public int[] StartsInLiteral { get; }

    public int[] LengthsInLiteral { get; }

    public bool HasOptionalLoop { get; }

    // Hit length when the optional loop does not participate; the absent projection applies.
    public int TotalLengthWithoutLoopInUtf16 { get; }

    // Parallel to Groups; AbsentStartsInLiteral[i] below zero marks the group unmatched
    // when the optional loop does not participate. Empty when no optional loop applies.
    public int[] AbsentStartsInLiteral { get; }

    public int[] AbsentLengthsInLiteral { get; }

    public static Utf8GroupedLiteralSpanTable? TryCreate(Utf8ExecutionTree? tree)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var result = Create(tree);
        if (result is not null) construction.Complete();
        return result;

        static Utf8GroupedLiteralSpanTable? Create(Utf8ExecutionTree? tree)
        {
            if (tree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root ||
                root.Children.Count != 1)
            {
                return null;
            }

            var walker = new SpanWalker();
            if (!walker.TryWalk(root.Children[0], inLoop: false, inAlternate: false))
            {
                return null;
            }

            if (walker.Position == 0 || walker.Groups.Count == 0)
            {
                return null;
            }

            // Owned-tree capture numbers diverge from BCL group numbers for nested named
            // captures; translate every recorded group up front so shaping projects BCL
            // slots, declining on any numbering surprise.
            var slots = Utf8BclSlotTranslator.TryCreateMap(root.Children[0]);
            if (slots is null)
            {
                return null;
            }

            for (var i = 0; i < walker.Groups.Count; i++)
            {
                if (!slots.TryGetValue(walker.Groups[i], out var slot))
                {
                    return null;
                }

                walker.Groups[i] = slot;
            }

            if (!walker.LoopSeen)
            {
                return new Utf8GroupedLiteralSpanTable(
                    walker.Position,
                    walker.Groups.ToArray(),
                    walker.Starts.ToArray(),
                    walker.Lengths.ToArray(),
                    hasOptionalLoop: false,
                    walker.Position,
                    [],
                    []);
            }

            var absentStarts = new int[walker.Groups.Count];
            var absentLengths = new int[walker.Groups.Count];
            var loopEnd = walker.LoopStart + walker.LoopLength;
            for (var i = 0; i < walker.Groups.Count; i++)
            {
                if (walker.InLoop[i])
                {
                    absentStarts[i] = -1;
                    absentLengths[i] = 0;
                    continue;
                }

                var start = walker.Starts[i];
                var length = walker.Lengths[i];
                if (start <= walker.LoopStart && start + length >= loopEnd)
                {
                    // The group contains the loop: its absent span is the hit minus the loop.
                    absentStarts[i] = start;
                    absentLengths[i] = length - walker.LoopLength;
                }
                else if (start >= loopEnd)
                {
                    // The group follows the loop: its absent span shifts left by the loop.
                    absentStarts[i] = start - walker.LoopLength;
                    absentLengths[i] = length;
                }
                else if (start + length <= walker.LoopStart)
                {
                    absentStarts[i] = start;
                    absentLengths[i] = length;
                }
                else
                {
                    // Partial loop overlap cannot arise from nested captures; decline.
                    return null;
                }
            }

            return new Utf8GroupedLiteralSpanTable(
                walker.Position,
                walker.Groups.ToArray(),
                walker.Starts.ToArray(),
                walker.Lengths.ToArray(),
                hasOptionalLoop: true,
                walker.Position - walker.LoopLength,
                absentStarts,
                absentLengths);

        }
    }

    private sealed class SpanWalker
    {
        internal int Position;
        internal readonly List<int> Groups = new();
        internal readonly List<int> Starts = new();
        internal readonly List<int> Lengths = new();
        internal readonly List<bool> InLoop = new();
        internal bool LoopSeen;
        internal int LoopStart = -1;
        internal int LoopLength;

        private struct Frame(Utf8ExecutionNode node, SpanWalker? walker, bool inLoop, bool inAlternate, int initialPosition)
        {
            internal readonly Utf8ExecutionNode Node = node;
            internal SpanWalker? Walker = walker;
            internal readonly bool InLoop = inLoop;
            internal readonly bool InAlternate = inAlternate;
            internal readonly int InitialPosition = initialPosition;
            internal int Stage;
            internal int Index;
            internal int Start;
            internal int BaseCount;
            internal int End;
        }

        internal bool TryWalk(Utf8ExecutionNode node, bool inLoop, bool inAlternate)
        {
            using var construction = Utf8ConstructionScope.Enter();
            var budget = construction.Budget;
            using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(node, this, inLoop, inAlternate, 0));
            SpanWalker? completedChild = null;
            while (frames.Count != 0)
            {
                budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                Frame? child;
                bool accepted;
                try { accepted = Advance(ref frame, completedChild, budget, out child); }
                finally { frames[index] = frame; }
                if (!accepted) return false;
                if (child is { } pending) frames.Push(pending);
                else
                {
                    completedChild = frame.Walker;
                    frames.Pop();
                }
            }
                return true;
        }

        // Scratch branch walkers are acquired only after their value frame is
        // admitted. Their lists and the root walker still need separate loans.
        private static bool Advance(ref Frame frame, SpanWalker? completedChild,
            Utf8ConstructionBudget budget, out Frame? child)
        {
            child = null;
            var walker = frame.Walker ??= new SpanWalker { Position = frame.InitialPosition };
            var node = frame.Node;
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.One:
                    walker.Position++;
                    return true;
                case Utf8ExecutionNodeKind.Multi:
                    if (node.Text is null) return false;
                    walker.Position += node.Text.Length;
                    return true;
                case Utf8ExecutionNodeKind.Set:
                    if (node.Text is not { Length: > 0 } set) return false;
                    char[] setChars;
                    try { setChars = Utf8OwnedCharClass.GetSetChars(set); }
                    catch (ArgumentException) { setChars = []; }
                    if (setChars.Length == 0) return false;
                    foreach (var ch in setChars)
                    {
                        budget.ChargeWork(1);
                        if (char.IsSurrogate(ch)) return false;
                    }
                    walker.Position++;
                    return true;
                case Utf8ExecutionNodeKind.Concatenate:
                    if (frame.Index < node.Children.Count)
                        child = new(node.Children[frame.Index++], walker, frame.InLoop, frame.InAlternate, 0);
                    return true;
                case Utf8ExecutionNodeKind.Group:
                    if (frame.Stage == 0)
                    {
                        if (node.Children.Count != 1) return false;
                        frame.Stage = 1;
                        child = new(node.Children[0], walker, frame.InLoop, frame.InAlternate, 0);
                    }
                    return true;
                case Utf8ExecutionNodeKind.Capture:
                    if (frame.Stage == 0)
                    {
                        budget.ChargeWork((ulong)walker.Groups.Count);
                        if (node.CaptureNumber <= 0 || node.Children.Count != 1 || walker.Groups.Contains(node.CaptureNumber)) return false;
                        frame.Start = walker.Position;
                        frame.Stage = 1;
                        child = new(node.Children[0], walker, frame.InLoop, frame.InAlternate, 0);
                    }
                    else
                    {
                        walker.Groups.Add(node.CaptureNumber);
                        walker.Starts.Add(frame.Start);
                        walker.Lengths.Add(walker.Position - frame.Start);
                        walker.InLoop.Add(frame.InLoop);
                    }
                    return true;
                case Utf8ExecutionNodeKind.Alternate:
                    if (frame.Stage == 0)
                    {
                        if (node.Children.Count == 0 || walker.LoopSeen) return false;
                        frame.Start = walker.Position;
                        frame.BaseCount = walker.Groups.Count;
                        frame.Stage = 1;
                        child = new(node.Children[0], walker, frame.InLoop, true, 0);
                        return true;
                    }
                    if (frame.Stage == 1)
                    {
                        frame.End = walker.Position;
                        frame.Index = 1;
                    }
                    else
                    {
                        var scratch = completedChild ?? throw new InvalidOperationException();
                        budget.ChargeWork((ulong)scratch.Groups.Count);
                        if (scratch.Position != frame.End || !walker.BranchMatches(scratch, frame.BaseCount)) return false;
                        frame.Index++;
                    }
                    if (frame.Index < node.Children.Count)
                    {
                        frame.Stage = 2;
                        child = new(node.Children[frame.Index], null, frame.InLoop, true, frame.Start);
                    }
                    return true;
                case Utf8ExecutionNodeKind.Loop:
                case Utf8ExecutionNodeKind.LazyLoop:
                    if (frame.Stage == 0)
                    {
                        if (walker.LoopSeen || frame.InLoop || frame.InAlternate || node.Min != 0 || node.Max != 1 || node.Children.Count != 1) return false;
                        walker.LoopSeen = true;
                        walker.LoopStart = walker.Position;
                        frame.Stage = 1;
                        child = new(node.Children[0], walker, true, false, 0);
                        return true;
                    }
                    walker.LoopLength = walker.Position - walker.LoopStart;
                    return walker.LoopLength != 0;
                case Utf8ExecutionNodeKind.Bol:
                case Utf8ExecutionNodeKind.Eol:
                case Utf8ExecutionNodeKind.Beginning:
                case Utf8ExecutionNodeKind.Start:
                case Utf8ExecutionNodeKind.EndZ:
                case Utf8ExecutionNodeKind.End:
                    return true;
                default:
                    return false;
            }
        }

        private bool BranchMatches(SpanWalker scratch, int baseCount)
        {
            if (scratch.Groups.Count != Groups.Count - baseCount)
            {
                return false;
            }

            for (var i = 0; i < scratch.Groups.Count; i++)
            {
                if (scratch.Groups[i] != Groups[baseCount + i] ||
                    scratch.Starts[i] != Starts[baseCount + i] ||
                    scratch.Lengths[i] != Lengths[baseCount + i] ||
                    scratch.InLoop[i] != InLoop[baseCount + i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
