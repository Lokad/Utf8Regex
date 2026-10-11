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
internal sealed partial class Utf8GroupedLiteralSpanTable
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

    public static Utf8GroupedLiteralSpanTable? TryCreate(Utf8ExecutionTree? tree, Utf8SliceProgram captureProgram)
    {
        // Stateful declarations use the owned capture-history engine.
        // No slot renumbering or independent numbering scan is needed here.
        if (captureProgram.HasBalancing || captureProgram.HasSharedCaptureDeclarations || captureProgram.HasExplicitCaptureNumbers) return null;
        using var construction = Utf8ConstructionScope.Enter();
        using var storage = Utf8ConstructionSession.Create(construction.Budget);
        var result = Create(tree, storage);
        if (result is not null) { storage.Complete(); construction.Complete(); }
        return result;

        static Utf8GroupedLiteralSpanTable? Create(Utf8ExecutionTree? tree, Utf8ConstructionSession storage)
        {
            if (tree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root ||
                root.Children.Count != 1)
            {
                return null;
            }

            var walker = SpanWalker.Create(storage, 0);
            if (!walker.TryWalk(storage, root.Children[0], inLoop: false, inAlternate: false))
            {
                return null;
            }

            if (walker.Position == 0 || walker.Groups.Count == 0)
            {
                return null;
            }

            // The shared catalog finalized these capture identities before lowering.
            if (!walker.LoopSeen)
            {
                return Allocate(storage, walker.Position, storage.Copy(walker.Groups),
                    storage.Copy(walker.Starts), storage.Copy(walker.Lengths), false, walker.Position, [], []);
            }

            var absentStarts = storage.Allocate<int>(walker.Groups.Count);
            var absentLengths = storage.Allocate<int>(walker.Groups.Count);
            var loopEnd = walker.LoopStart + walker.LoopLength;
            for (var i = 0; i < walker.Groups.Count; i++)
            {
                storage.Budget.ChargeWork(1);
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

            return Allocate(storage, walker.Position, storage.Copy(walker.Groups),
                storage.Copy(walker.Starts), storage.Copy(walker.Lengths), true,
                walker.Position - walker.LoopLength, absentStarts, absentLengths);

        }
    }

    private static Utf8GroupedLiteralSpanTable Allocate(Utf8ConstructionSession storage,
        int total, int[] groups, int[] starts, int[] lengths, bool optional,
        int withoutLoop, int[] absentStarts, int[] absentLengths) =>
        storage.Budget.AllocateObject((total, groups, starts, lengths, optional, withoutLoop, absentStarts, absentLengths),
            static state => new Utf8GroupedLiteralSpanTable(state.total, state.groups, state.starts, state.lengths,
                state.optional, state.withoutLoop, state.absentStarts, state.absentLengths));

    private sealed class SpanWalker
    {
        internal int Position;
        internal readonly Utf8ConstructionBuffer<int> Groups;
        internal readonly Utf8ConstructionBuffer<int> Starts;
        internal readonly Utf8ConstructionBuffer<int> Lengths;
        internal readonly Utf8ConstructionBuffer<bool> InLoop;

        private SpanWalker(int position, Utf8ConstructionBuffer<int> groups,
            Utf8ConstructionBuffer<int> starts, Utf8ConstructionBuffer<int> lengths, Utf8ConstructionBuffer<bool> inLoop)
        {
            Position = position;
            Groups = groups; Starts = starts; Lengths = lengths; InLoop = inLoop;
        }

        internal static SpanWalker Create(Utf8ConstructionSession storage, int position)
        {
            var groups = storage.Buffer<int>();
            var starts = storage.Buffer<int>();
            var lengths = storage.Buffer<int>();
            var inLoop = storage.Buffer<bool>();
            return storage.Budget.AllocateObject((position, groups, starts, lengths, inLoop, false, -1, 0),
                static state => new SpanWalker(state.position, state.groups, state.starts, state.lengths, state.inLoop));
        }
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

        internal bool TryWalk(Utf8ConstructionSession storage, Utf8ExecutionNode node, bool inLoop, bool inAlternate)
        {
            var budget = storage.Budget;
            using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(node, this, inLoop, inAlternate, 0));
            SpanWalker? completedChild = null;
            while (frames.Count != 0)
            {
                budget.ChargeWork(1);
                var index = frames.Count - 1;
                var frame = frames[index];
                Frame? child;
                bool accepted;
                try { accepted = Advance(ref frame, completedChild, storage, out child); }
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

        // Branch frames precede admitted scratch walkers and their vectors.
        // The factory owns their payloads through the final projection copy.
        private static bool Advance(ref Frame frame, SpanWalker? completedChild,
            Utf8ConstructionSession storage, out Frame? child)
        {
            var budget = storage.Budget;
            child = null;
            var walker = frame.Walker ??= Create(storage, frame.InitialPosition);
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
                    if (!Utf8ScalarAsciiProjector.TryProjectAsciiDefinition(node.ScalarClass, out var set) || set.IsEmpty)
                        return false;
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
                        if (node.CaptureNumber <= 0 || node.Children.Count != 1 || walker.Groups.IndexOf(node.CaptureNumber) >= 0) return false;
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
