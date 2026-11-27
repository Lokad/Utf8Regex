using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Posix;

// Keep semantic subpattern tags, including non-public concatenation/repetition
// parts. Final public capture slots alone cannot order repeated POSIX parses.
internal sealed partial class PosixProgram
{
    // Core uses positive values in these fields for terminal byte bounds.
    internal const int RepeatExitWithoutRewind = -1;
    internal const int ForkTargetFirst = -1;
    internal const int ForkSelectSingleLiteral = -2;

    private PosixProgram(Utf8SliceProgram scalar, int[] parents, int[] captureTags, bool firstAcceptanceIsLongest, bool directGreedyRuns,
        bool useCaptureOffsets, bool startsAtInputStart,
        int firstRequiredRunAtom, int terminalLineEndRepeat,
        ChartNode[] chart, Utf8SliceNode.CategorySetNode[] chartClasses)
    {
        Scalar = scalar; Parents = parents; CaptureTags = captureTags;
        FirstAcceptanceIsLongest = firstAcceptanceIsLongest; DirectGreedyRuns = directGreedyRuns;
        UseCaptureOffsets = useCaptureOffsets;
        StartsAtInputStart = startsAtInputStart;
        FirstRequiredRunAtom = firstRequiredRunAtom;
        TerminalLineEndRepeat = terminalLineEndRepeat;
        Chart = chart; ChartClasses = chartClasses;
    }

    internal Utf8SliceProgram Scalar { get; }
    internal int[] Parents { get; }
    internal int[] CaptureTags { get; }
    internal bool FirstAcceptanceIsLongest { get; }
    internal bool DirectGreedyRuns { get; }
    internal bool UseCaptureOffsets { get; }
    internal bool StartsAtInputStart { get; }
    internal int FirstRequiredRunAtom { get; }
    internal int TerminalLineEndRepeat { get; }
    internal ChartNode[] Chart { get; }
    internal Utf8SliceNode.CategorySetNode[] ChartClasses { get; }

    internal static PosixProgram Compile(string pattern, PosixCompileOptions options, bool enableExistenceTier = false)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        var root = PosixParser.Parse(pattern, options, out var groups);
        var captureTags = budget.AllocateArray<int>(checked(groups + 1), false);
        using var parents = Utf8ConstructionBuffer<int>.Create(budget);
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(root, -1));
        Utf8SliceNode? completed = null;
        while (frames.Count != 0)
        {
            budget.Checkpoint();
            var index = frames.Count - 1;
            var frame = frames[index];
            if (frame.Tag < 0)
            {
                frame.Tag = parents.Count;
                parents.Add(frame.Parent);
                if (frame.Node is Utf8SliceNode.CaptureNode capture)
                {
                    captureTags[capture.Slot] = frame.Tag;
                    // With no public descendants, equal occurrence boundaries
                    // leave no observable choice inside this capture. Keep its
                    // complete history, but omit its internal fictive tags.
                    if (!ContainsCapture(capture.Body, budget))
                    {
                        completed = budget.AllocateObject((frame.Tag, capture.Body),
                            static x => new Utf8SliceNode.CaptureNode(x.Tag, x.Body));
                        frames.Pop();
                        continue;
                    }
                    // This body has exactly its enclosing capture's occurrences
                    // and boundaries. Its fictive tag cannot break a tie after
                    // that capture; lower the body under the same tag instead.
                    // A directly nested public capture keeps its own tag.
                    if (capture.Body is not Utf8SliceNode.CaptureNode)
                        frame.Node = capture.Body;
                }
                if (frame.Node is Utf8SliceNode.ConcatNode concat)
                    frame.Children = budget.AllocateArray<Utf8SliceNode>(concat.Children.Length, false);
                else if (frame.Node is Utf8SliceNode.AlternationNode alternate)
                    frame.Children = budget.AllocateArray<Utf8SliceNode>(alternate.Branches.Length, false);
            }
            if (frame.Awaiting)
            {
                if (completed is null) throw new InvalidOperationException("A POSIX child did not complete.");
                switch (frame.Node)
                {
                    case Utf8SliceNode.ConcatNode or Utf8SliceNode.AlternationNode:
                        frame.Children![frame.Child++] = completed; break;
                    default: frame.Body = completed; frame.Child = 1; break;
                }
                frame.Awaiting = false;
            }
            Utf8SliceNode? child = null;
            switch (frame.Node)
            {
                case Utf8SliceNode.ConcatNode concat when frame.Child < concat.Children.Length:
                    child = concat.Children[frame.Child]; break;
                case Utf8SliceNode.AlternationNode alternate when frame.Child < alternate.Branches.Length:
                    child = alternate.Branches[frame.Child]; break;
                case Utf8SliceNode.CaptureNode capture when frame.Child == 0: child = capture.Body; break;
                case Utf8SliceNode.RepeatNode repeat when frame.Child == 0: child = repeat.Body; break;
            }
            if (child is not null)
            {
                frame.Awaiting = true; frames[index] = frame;
                frames.Push(new(child, frame.Tag));
                continue;
            }
            var body = frame.Node switch
            {
                Utf8SliceNode.CaptureNode => frame.Body!,
                Utf8SliceNode.ConcatNode => budget.AllocateObject(frame.Children!, static x => new Utf8SliceNode.ConcatNode(x)),
                Utf8SliceNode.AlternationNode => budget.AllocateObject(frame.Children!, static x => new Utf8SliceNode.AlternationNode(x)),
                Utf8SliceNode.RepeatNode repeat => budget.AllocateObject((frame.Body!, repeat.Minimum, repeat.Maximum),
                    static x => new Utf8SliceNode.RepeatNode(x.Item1, x.Minimum, x.Maximum, false)),
                Utf8SliceNode.EmptyNode or Utf8SliceNode.LiteralNode or Utf8SliceNode.DotNode or
                    Utf8SliceNode.AnchorNode or Utf8SliceNode.CategorySetNode => frame.Node,
                _ => throw new InvalidOperationException("An unsupported node entered POSIX lowering."),
            };
            completed = budget.AllocateObject((frame.Tag, body), static x => new Utf8SliceNode.CaptureNode(x.Tag, x.body));
            frames.Pop();
        }
        var scalar = Utf8SliceLowerer.Lower(completed!, null, false,
            (options & PosixCompileOptions.IgnoreCase) != 0, false, null, false, false, Utf8ScalarCaseBehavior.Invariant,
            false, proofCompilation: enableExistenceTier ? Utf8BoundedProgramEmitter.Compilation.TieredLiteralExistence : default)
            ?? throw new InvalidOperationException("The admitted POSIX tree could not be lowered.");
        var firstAcceptanceIsLongest = CanSelectFirst(scalar, budget, out var capturesOpenOnce);
        var canReplayDirectly = true;
        var firstRunAtom = -1;
        var terminalLineEndRepeat = -1;
        var greedyRuns = scalar.RepeatCount > 0 && CanSelectGreedyRuns(scalar, captureTags.AsSpan(1), budget,
            out canReplayDirectly, out firstRunAtom, out terminalLineEndRepeat);
        var directGreedyRuns = greedyRuns && canReplayDirectly;
        if (!firstAcceptanceIsLongest)
            firstAcceptanceIsLongest = greedyRuns || CanSelectRepeatedLiteralCapture(scalar, budget);
        if (!firstAcceptanceIsLongest) PreferLongerScalarAlternatives(scalar.Instructions, budget);
        var chart = !firstAcceptanceIsLongest && groups != 0 ? CreateChart(root, budget)
            : (Nodes: Array.Empty<ChartNode>(), Classes: Array.Empty<Utf8SliceNode.CategorySetNode>());
        int[] parentTags = directGreedyRuns ? [] : parents.Take();
        var useCaptureOffsets = firstAcceptanceIsLongest;
        if (useCaptureOffsets && !directGreedyRuns && !capturesOpenOnce)
        {
            // The root has one occurrence. A public tag below another
            // parent may need its last capture reconciled with that parent.
            foreach (var tag in captureTags.AsSpan(1))
            {
                budget.Checkpoint();
                if (parentTags[tag] <= 0) continue;
                useCaptureOffsets = false;
                break;
            }
        }
        var startsAtInputStart = RequiresInputStart(scalar.Instructions, budget);
        // Unanchored discovery can reject starts before entering the matcher.
        // A direct, case-sensitive run at byte zero already checks its first byte.
        var firstRequiredRunAtom = greedyRuns && (!directGreedyRuns || scalar.IgnoreCase || !startsAtInputStart)
            ? firstRunAtom : -1;
        if (directGreedyRuns)
        {
            PrepareDirectCaptureSlots(scalar.Instructions, parents, captureTags, budget);
            parents.Dispose();
        }
        var result = budget.AllocateObject((scalar, parentTags, captureTags, firstAcceptanceIsLongest, directGreedyRuns, useCaptureOffsets, firstRequiredRunAtom,
                startsAtInputStart, terminalLineEndRepeat, chart.Nodes, chart.Classes),
            static x => new PosixProgram(x.scalar, x.parentTags, x.captureTags, x.firstAcceptanceIsLongest, x.directGreedyRuns, x.useCaptureOffsets,
                x.startsAtInputStart, x.firstRequiredRunAtom, x.terminalLineEndRepeat, x.Nodes, x.Classes));
        construction.Complete();
        return result;
    }

    private static void PrepareDirectCaptureSlots(Span<Utf8SliceInstruction> code, Utf8ConstructionBuffer<int> tags,
        Span<int> captureTags, Utf8ConstructionBudget budget)
    {
        // Direct runs have no capture-history comparisons. Reuse the parent
        // buffer to map semantic tag IDs to compact public capture slots.
        for (var tag = 0; tag < tags.Count; tag++)
        {
            budget.Checkpoint();
            tags[tag] = -1;
        }
        for (var slot = 1; slot < captureTags.Length; slot++)
        {
            budget.Checkpoint();
            tags[captureTags[slot]] = slot - 1;
            captureTags[slot] = slot - 1;
        }
        for (var index = 0; index < code.Length; index++)
        {
            budget.Checkpoint();
            ref var instruction = ref code[index];
            if (instruction.Kind == Utf8SliceInstructionKind.RepeatGreedy)
            {
                // The direct runner uses the private body markers to locate
                // the repeated atom and advances straight past the repeat.
                index = instruction.A;
            }
            else if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            {
                var slot = tags[instruction.A];
                if (slot >= 0)
                {
                    instruction = new(instruction.Kind, slot, instruction.B, instruction.C, instruction.D);
                    continue;
                }

                // Outside the skipped repeat bodies, the direct proof permits
                // no branches into this run of private markers.
                var destination = index + 1;
                for (; destination < code.Length; destination++)
                {
                    ref var marker = ref code[destination];
                    if (marker.Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) ||
                        tags[marker.A] >= 0) break;
                    budget.Checkpoint();
                    marker = new(Utf8SliceInstructionKind.Jump, destination + 1, 0, 0, 0);
                }
                instruction = new(Utf8SliceInstructionKind.Jump, destination, 0, 0, 0);
                index = destination - 1;
            }
        }
    }

    private static bool RequiresInputStart(ReadOnlySpan<Utf8SliceInstruction> code, Utf8ConstructionBudget budget)
    {
        // Leading capture tags are unconditional and zero-width. Stop at a
        // choice or consuming instruction before inferring a required anchor.
        foreach (var instruction in code)
        {
            budget.Checkpoint();
            if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) continue;
            return instruction is { Kind: Utf8SliceInstructionKind.Anchor, A: (int)Utf8SliceAnchorKind.Beginning, B: 0 };
        }
        return false;
    }

    private static void PreferLongerScalarAlternatives(Utf8SliceInstruction[] code, Utf8ConstructionBudget budget)
    {
        // Search order is only a heuristic: semantic tags retain their original
        // preorder. Leave proven first-acceptance programs entirely unchanged.
        // The negative Fork.C marker is private to this POSIX-owned stream.
        for (var pc = 0; pc < code.Length; pc++)
        {
            budget.Checkpoint();
            var fork = code[pc];
            if (fork.Kind != Utf8SliceInstructionKind.Fork || fork.B != 0 ||
                fork.A <= pc + 1 || fork.A >= code.Length ||
                code[fork.A - 1] is not { Kind: Utf8SliceInstructionKind.Jump } jump ||
                jump.A <= fork.A || jump.A > code.Length) continue;
            var left = ScalarBranchLength(code.AsSpan(pc + 1, fork.A - pc - 2), budget);
            var right = ScalarBranchLength(code.AsSpan(fork.A, jump.A - fork.A), budget);
            if (left >= 0 && right > left)
                code[pc] = new(fork.Kind, fork.A, fork.B, ForkTargetFirst, fork.D);
        }
    }

    private static int ScalarBranchLength(ReadOnlySpan<Utf8SliceInstruction> branch, Utf8ConstructionBudget budget)
    {
        if (branch.Length > 64) return -1;
        var length = 0;
        foreach (var instruction in branch)
        {
            budget.Checkpoint();
            if (instruction.Kind is Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or
                Utf8SliceInstructionKind.CategorySet) length++;
            else if (instruction.Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd))
                return -1;
        }
        return length;
    }

    private static bool CanSelectFirst(Utf8SliceProgram program, Utf8ConstructionBudget budget, out bool capturesOpenOnce)
    {
        capturesOpenOnce = false;
        // Fixed scalar pieces around one single-scalar greedy run consume a
        // fixed scalar count plus the repetitions. First successful descending
        // greedy count is longest and fixes every non-repeated capture edge.
        if (program.RepeatCount <= 1)
        {
            var unique = true;
            var singleRepeat = -1;
            var captureOnlyTail = true;
            var increasingCaptureTags = true;
            var previousCaptureTag = -1;
            for (var index = 0; index < program.Instructions.Length; index++)
            {
                budget.Checkpoint();
                var instruction = program.Instructions[index];
                var kind = instruction.Kind;
                if (kind == Utf8SliceInstructionKind.RepeatGreedy && singleRepeat < 0 &&
                    index <= program.Instructions.Length - 4 && instruction.A == index + 3 &&
                    program.Instructions[index + 1].Kind is (Utf8SliceInstructionKind.Literal or
                        Utf8SliceInstructionKind.Dot or Utf8SliceInstructionKind.CategorySet) &&
                    program.Instructions[index + 2] is { Kind: Utf8SliceInstructionKind.RepeatEnd } end && end.A == index &&
                    program.Instructions[index + 3] is { Kind: Utf8SliceInstructionKind.RepeatExit } exit && exit.A == instruction.B)
                {
                    budget.Checkpoint();
                    singleRepeat = index;
                    index += 3;
                    continue;
                }
                if (singleRepeat >= 0 && kind is not (Utf8SliceInstructionKind.CaptureStart or
                    Utf8SliceInstructionKind.CaptureEnd or Utf8SliceInstructionKind.Accept)) captureOnlyTail = false;
                if (kind is not (Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or
                    Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Anchor or
                    Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or Utf8SliceInstructionKind.Accept) ||
                    kind == Utf8SliceInstructionKind.Accept && index != program.Instructions.Length - 1)
                { unique = false; break; }
                if (kind == Utf8SliceInstructionKind.CaptureStart)
                {
                    increasingCaptureTags &= instruction.A > previousCaptureTag;
                    previousCaptureTag = instruction.A;
                }
            }
            if (unique && program.Instructions.Length != 0 && program.Instructions[^1].Kind == Utf8SliceInstructionKind.Accept)
            {
                if (singleRepeat >= 0 && captureOnlyTail)
                {
                    var exitIndex = program.Instructions[singleRepeat].A;
                    var exit = program.Instructions[exitIndex];
                    // POSIX owns this stream. Every shorter choice here has
                    // the same unconditional tail and cannot improve the match.
                    program.Instructions[exitIndex] = new(exit.Kind, exit.A, RepeatExitWithoutRewind, exit.C, exit.D);
                }
                // The sole admitted loop consumes one scalar without a
                // capture. Strictly increasing opens also rule out repeated
                // tags emitted as separate copies outside that loop.
                capturesOpenOnce = increasingCaptureTags;
                return true;
            }
        }
        // For one scalar R, each piece consumes x_i in [min_i,max_i]. First
        // greedy success reserves remaining minima and consumes the maximum
        // feasible total. Tags outside the loops enclose contiguous pieces:
        // preorder priority first maximizes each parent's total, then its
        // children's left-to-right split. Every tag participates exactly once.
        // Repeated tags, different atoms and control flow need general ranking.
        var code = program.Instructions;
        var literal = -1;
        for (var index = 0; index < code.Length; index++)
        {
            budget.Checkpoint();
            var instruction = code[index];
            if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
                continue;
            if (instruction.Kind == Utf8SliceInstructionKind.Accept)
                return literal >= 0 && index == code.Length - 1;
            if (instruction.Kind == Utf8SliceInstructionKind.Fork && literal < 0)
                return CanSelectFixedAlternative(code, index, budget);
            if (instruction.Kind == Utf8SliceInstructionKind.RepeatGreedy && index <= code.Length - 4 &&
                instruction.A == index + 3 &&
                code[index + 2] is { Kind: Utf8SliceInstructionKind.RepeatEnd } repeatEnd && repeatEnd.A == index &&
                code[index + 3] is { Kind: Utf8SliceInstructionKind.RepeatExit } exit && exit.A == instruction.B)
            {
                // Exactly one literal in the body; no public or fictive tag
                // can acquire repeated occurrence histories through this loop.
                instruction = code[index + 1];
                index += 3;
            }
            if (instruction.Kind != Utf8SliceInstructionKind.Literal || instruction.B != 0 ||
                literal >= 0 && literal != instruction.A)
                return false;
            literal = instruction.A;
        }
        return false;
    }

    private static bool CanSelectRepeatedLiteralCapture(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        var code = program.Instructions;
        if (code.Length != 12) return false;
        budget.Checkpoint();
        if (code[0] is not { Kind: Utf8SliceInstructionKind.CaptureStart, A: 0 } ||
            code[1] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, A: 9 } outer ||
            code[2] is not { Kind: Utf8SliceInstructionKind.CaptureStart, A: 1 } ||
            code[3] is not { Kind: Utf8SliceInstructionKind.RepeatGreedy, A: 6 } inner ||
            code[4] is not { Kind: Utf8SliceInstructionKind.Literal, B: 0 } ||
            code[5] is not { Kind: Utf8SliceInstructionKind.RepeatEnd, A: 3 } ||
            code[6] is not { Kind: Utf8SliceInstructionKind.RepeatExit } innerExit || innerExit.A != inner.B ||
            code[7] is not { Kind: Utf8SliceInstructionKind.CaptureEnd, A: 1 } ||
            code[8] is not { Kind: Utf8SliceInstructionKind.RepeatEnd, A: 1 } ||
            code[9] is not { Kind: Utf8SliceInstructionKind.RepeatExit } outerExit || outerExit.A != outer.B ||
            code[10] is not { Kind: Utf8SliceInstructionKind.CaptureEnd, A: 0 } ||
            code[11].Kind != Utf8SliceInstructionKind.Accept || outer.B == inner.B)
            return false;
        // A single repeated capture, containing only a uniform literal run.
        // Minimum zero/one makes feasible total lengths contiguous: greedy
        // success consumes the longest prefix, then maximizes each occurrence
        // left to right while reserving mandatory minima. Minimum two or more
        // can require a different partition to reach the longest endpoint
        // (e.g. (a{3,4})+ on six a's), so still needs general ranking.
        // Existing empty-repeat pruning keeps mandatory empty tails and removes
        // redundant optional ones, except for the saturated minimum-zero
        // unbounded outer counter. Leave that case to history comparison too.
        if (inner.RepeatMinimum > 1 || outer.RepeatMaximum == long.MaxValue && outer.RepeatMinimum == 0)
            return false;

        // When later occurrences can be empty, this literal run never needs
        // to give back a scalar. The outer repeat still owns empty-occurrence
        // priority and capture rollback.
        if (inner.RepeatMinimum == 0)
            code[inner.A] = new(innerExit.Kind, innerExit.A, RepeatExitWithoutRewind, innerExit.C, innerExit.D);
        return true;
    }

    private static bool CanSelectGreedyRuns(Utf8SliceProgram program, ReadOnlySpan<int> publicTags, Utf8ConstructionBudget budget,
        out bool canReplayDirectly, out int firstRequiredRunAtom, out int terminalLineEndRepeat)
    {
        // Greedy runs maximize each earlier concatenation part. An overlapping
        // successor must be nullable: it can still consume the suffix of any
        // shorter earlier choice. Required overlapping pieces may need rewind.
        // Fixed scalar pieces introduce no choice; compare them with earlier
        // variable runs, but do not make them sources of possible rewind.
        // Public tags outside bodies occur once; no history can change the split.
        canReplayDirectly = true;
        firstRequiredRunAtom = -1;
        terminalLineEndRepeat = -1;
        Span<byte> seen = stackalloc byte[16];
        seen.Clear();
        Span<byte> literalMask = stackalloc byte[16];
        var pieces = 0;
        var hasAnchor = false;
        var hasLineEndAnchor = false;
        var hasNullableRun = false;
        var canRewindAtLineStart = false;
        var precedingLineFeedRepeat = -1;
        const int lineFeedOctet = '\n' >> 3;
        const byte lineFeedBit = 1 << ('\n' & 7);
        var code = program.Instructions;
        for (var index = 0; index < code.Length; index++)
        {
            budget.Checkpoint();
            var instruction = code[index];
            if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd) continue;
            if (instruction.Kind == Utf8SliceInstructionKind.Anchor)
            {
                hasAnchor = true;
                var lineEnd = instruction.A == (int)Utf8SliceAnchorKind.End && instruction.B != 0;
                // Only a sole line end after a repeat, with no later assertion
                // or consuming piece, can use that repeat's last legal exit.
                terminalLineEndRepeat = lineEnd && !hasLineEndAnchor ? precedingLineFeedRepeat : -1;
                precedingLineFeedRepeat = -1;
                hasLineEndAnchor |= lineEnd;
                // A mixed run can consume past its last LF. Only the ordinary
                // scalar loop can return to the earlier beginning-of-line boundary.
                if (instruction.A == (int)Utf8SliceAnchorKind.Beginning && instruction.B != 0 && canRewindAtLineStart)
                    canReplayDirectly = false;
                continue;
            }
            if (instruction.Kind == Utf8SliceInstructionKind.Accept)
            {
                // Nullable pieces can require rewind to satisfy an interior
                // anchor, e.g. (a*)^(a*). Keep all such anchored programs general.
                // Positive disjoint runs have unique intervening boundaries.
                // A sole terminal line end needs only the final run's latest
                // eligible LF/EOF exit. Other line ends can require rewind.
                if (hasLineEndAnchor && terminalLineEndRepeat < 0 && (seen[lineFeedOctet] & lineFeedBit) != 0)
                    canReplayDirectly = false;
                return pieces > 0 && index == code.Length - 1 && !(hasAnchor && hasNullableRun);
            }
            terminalLineEndRepeat = -1;
            var repeated = instruction.Kind == Utf8SliceInstructionKind.RepeatGreedy;
            var required = !repeated || instruction.RepeatMinimum != 0;
            var atomIndex = index;
            var exitIndex = index;
            if (repeated)
            {
                if (instruction.RepeatMinimum < 0 || index > code.Length - 4) return false;
                hasNullableRun |= !required;
                atomIndex++;
                exitIndex += 3;
                if (code[atomIndex] is { Kind: Utf8SliceInstructionKind.CaptureStart } open)
                {
                    // A private single-atom body cannot change the selected greedy
                    // boundary. A repeated public capture still needs its history.
                    if (index > code.Length - 6 ||
                        code[index + 3] is not { Kind: Utf8SliceInstructionKind.CaptureEnd } close || close.A != open.A)
                        return false;
                    budget.Checkpoint();
                    if (publicTags.Contains(open.A)) return false;
                    atomIndex++;
                    exitIndex += 2;
                }
                if (instruction.A != exitIndex ||
                    code[exitIndex - 1] is not { Kind: Utf8SliceInstructionKind.RepeatEnd } end || end.A != index ||
                    code[exitIndex] is not { Kind: Utf8SliceInstructionKind.RepeatExit } exit || exit.A != instruction.B)
                    return false;
            }
            var atom = code[atomIndex];
            scoped ReadOnlySpan<byte> mask;
            bool fold;
            if (atom is { Kind: Utf8SliceInstructionKind.Literal, B: 0 or 1 } && (uint)atom.A < 128)
            {
                literalMask.Clear();
                literalMask[atom.A >> 3] = (byte)(1 << (atom.A & 7));
                mask = literalMask;
                fold = atom.B != 0;
            }
            else if (atom is { Kind: Utf8SliceInstructionKind.CategorySet, B: 0 or 2, C: 0 } &&
                program.CategorySetMembers[atom.D].Length == 0)
            { mask = program.SetMasks[atom.A]; fold = atom.B != 0; }
            else return false;
            if (mask.Length != seen.Length) return false;
            if (fold)
            {
                // For ASCII-origin members, invariant lowercase-fiber closures
                // intersect exactly when these ASCII case keys intersect. Their
                // Unicode partners still require ordinary scalar consumption.
                mask.CopyTo(literalMask);
                budget.Checkpoint();
                for (var upper = (int)'A'; upper <= 'Z'; upper++)
                {
                    var lower = upper + ('a' - 'A');
                    if ((literalMask[upper >> 3] & (1 << (upper & 7))) == 0 &&
                        (literalMask[lower >> 3] & (1 << (lower & 7))) == 0) continue;
                    literalMask[upper >> 3] |= (byte)(1 << (upper & 7));
                    literalMask[lower >> 3] |= (byte)(1 << (lower & 7));
                }
                mask = literalMask;
            }
            var nonempty = false;
            var hasOtherByte = false;
            for (var octet = 0; octet < seen.Length; octet++)
            {
                budget.Checkpoint();
                if (required && (seen[octet] & mask[octet]) != 0) return false;
                nonempty |= mask[octet] != 0;
                hasOtherByte |= (octet == lineFeedOctet ? mask[octet] & ~lineFeedBit : mask[octet]) != 0;
                if (repeated) seen[octet] |= mask[octet];
            }
            if (!nonempty) return false;
            var canShortenAtLineFeed = repeated && instruction.RepeatMinimum < instruction.RepeatMaximum &&
                (mask[lineFeedOctet] & lineFeedBit) != 0;
            precedingLineFeedRepeat = canShortenAtLineFeed ? index : -1;
            canRewindAtLineStart = canShortenAtLineFeed && hasOtherByte;
            if (pieces == 0 && required) firstRequiredRunAtom = atomIndex;
            pieces++;
            index = exitIndex;
        }
        return false;
    }

    private static bool CanSelectFixedAlternative(Span<Utf8SliceInstruction> code, int fork,
        Utf8ConstructionBudget budget)
    {
        var right = code[fork].A;
        if (code[fork].B != 0 || right <= fork + 2 || right >= code.Length ||
            code[right - 1] is not { Kind: Utf8SliceInstructionKind.Jump } jump ||
            jump.A <= right || jump.A >= code.Length)
            return false;
        var leftBody = code.Slice(fork + 1, right - fork - 2);
        var rightBody = code.Slice(right, jump.A - right);
        var length = FixedLiteralBranchLength(leftBody, budget, out var leftTag, out var leftLiteral);
        if (length <= 0 || FixedLiteralBranchLength(rightBody, budget, out var rightTag, out _) != length ||
            leftTag >= rightTag)
            return false;
        // Both successful branches consume the same scalar prefix. The earlier
        // whole-branch tag has higher preorder priority and participation beats
        // its absence. No suffix may introduce a longer competing endpoint.
        for (var index = jump.A; index < code.Length; index++)
        {
            budget.Checkpoint();
            if (code[index].Kind == Utf8SliceInstructionKind.Accept)
            {
                if (index != code.Length - 1) return false;
                // After one literal the winning arm has only capture markers
                // left to run. A hit on the left wins, even if both arms match.
                if (length == 1)
                    code[fork] = new(Utf8SliceInstructionKind.Fork, right, 0, ForkSelectSingleLiteral, leftLiteral);
                return true;
            }
            if (code[index].Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd))
                return false;
        }
        return false;
    }

    private static int FixedLiteralBranchLength(ReadOnlySpan<Utf8SliceInstruction> body,
        Utf8ConstructionBudget budget, out int tag, out int literal)
    {
        tag = -1;
        literal = -1;
        if (body.Length < 3 || body[0] is not { Kind: Utf8SliceInstructionKind.CaptureStart } open ||
            body[^1] is not { Kind: Utf8SliceInstructionKind.CaptureEnd } close || open.A != close.A)
            return -1;
        tag = open.A;
        var length = 0;
        foreach (var instruction in body)
        {
            budget.Checkpoint();
            if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            {
                // Internal descendants follow the whole-branch tag; a shared
                // ancestor cannot masquerade as an earlier participating branch.
                if (instruction.A < tag) return -1;
            }
            else if (instruction.Kind == Utf8SliceInstructionKind.Literal && instruction.B == 0)
            {
                literal = instruction.A;
                length++;
            }
            else return -1;
        }
        return length;
    }

    private static bool ContainsCapture(Utf8SliceNode root, Utf8ConstructionBudget budget)
    {
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(root, -1));
        while (frames.Count != 0)
        {
            budget.Checkpoint();
            var index = frames.Count - 1;
            var frame = frames[index];
            if (frame.Node is Utf8SliceNode.CaptureNode) return true;
            Utf8SliceNode? child = null;
            switch (frame.Node)
            {
                case Utf8SliceNode.ConcatNode concat when frame.Child < concat.Children.Length:
                    child = concat.Children[frame.Child++]; break;
                case Utf8SliceNode.AlternationNode alternate when frame.Child < alternate.Branches.Length:
                    child = alternate.Branches[frame.Child++]; break;
                case Utf8SliceNode.RepeatNode repeat when frame.Child++ == 0: child = repeat.Body; break;
            }
            if (child is null) frames.Pop();
            else { frames[index] = frame; frames.Push(new(child, -1)); }
        }
        return false;
    }

    private struct Frame(Utf8SliceNode node, int parent)
    {
        internal Utf8SliceNode Node = node;
        internal readonly int Parent = parent;
        internal int Tag = -1;
        internal int Child;
        internal bool Awaiting;
        internal Utf8SliceNode[]? Children;
        internal Utf8SliceNode? Body;
    }
}
