using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Posix;

// Keep semantic subpattern tags, including non-public concatenation/repetition
// parts. Final public capture slots alone cannot order repeated POSIX parses.
internal sealed partial class PosixProgram
{
    private PosixProgram(Utf8SliceProgram scalar, int[] parents, int[] captureTags, bool firstAcceptanceIsLongest,
        ChartNode[] chart, Utf8SliceNode.CategorySetNode[] chartClasses)
    {
        Scalar = scalar; Parents = parents; CaptureTags = captureTags;
        FirstAcceptanceIsLongest = firstAcceptanceIsLongest; Chart = chart; ChartClasses = chartClasses;
    }

    internal Utf8SliceProgram Scalar { get; }
    internal int[] Parents { get; }
    internal int[] CaptureTags { get; }
    internal bool FirstAcceptanceIsLongest { get; }
    internal ChartNode[] Chart { get; }
    internal Utf8SliceNode.CategorySetNode[] ChartClasses { get; }

    internal static PosixProgram Compile(string pattern, PosixCompileOptions options)
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
            budget.ChargeWork(1);
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
                if (frame.Node is Utf8SliceNode.AlternationNode alternate)
                {
                    frame.Branches = budget.AllocateArray<Utf8SliceNode[]>(alternate.Branches.Length, false);
                    for (var branch = 0; branch < frame.Branches.Length; branch++)
                        frame.Branches[branch] = budget.AllocateArray<Utf8SliceNode>(alternate.Branches[branch].Length, false);
                }
            }
            if (frame.Awaiting)
            {
                if (completed is null) throw new InvalidOperationException("A POSIX child did not complete.");
                switch (frame.Node)
                {
                    case Utf8SliceNode.ConcatNode: frame.Children![frame.Child++] = completed; break;
                    case Utf8SliceNode.AlternationNode: frame.Branches![frame.Branch][frame.Child++] = completed; break;
                    default: frame.Body = completed; frame.Child = 1; break;
                }
                frame.Awaiting = false;
            }
            Utf8SliceNode? child = null;
            switch (frame.Node)
            {
                case Utf8SliceNode.ConcatNode concat when frame.Child < concat.Children.Length:
                    child = concat.Children[frame.Child]; break;
                case Utf8SliceNode.AlternationNode alternate:
                    while (frame.Branch < alternate.Branches.Length && frame.Child == alternate.Branches[frame.Branch].Length)
                    { frame.Branch++; frame.Child = 0; }
                    if (frame.Branch < alternate.Branches.Length) child = alternate.Branches[frame.Branch][frame.Child];
                    break;
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
                Utf8SliceNode.AlternationNode => budget.AllocateObject(frame.Branches!, static x => new Utf8SliceNode.AlternationNode(x)),
                Utf8SliceNode.RepeatNode repeat => budget.AllocateObject((frame.Body!, repeat.Minimum, repeat.Maximum),
                    static x => new Utf8SliceNode.RepeatNode(x.Item1, x.Minimum, x.Maximum, false)),
                Utf8SliceNode.EmptyNode or Utf8SliceNode.LiteralNode or Utf8SliceNode.DotNode or
                    Utf8SliceNode.AnchorNode or Utf8SliceNode.CategorySetNode => frame.Node,
                _ => throw new InvalidOperationException("An unsupported node entered POSIX lowering."),
            };
            completed = budget.AllocateObject((frame.Tag, body), static x => new Utf8SliceNode.CaptureNode(x.Tag, x.body));
            frames.Pop();
        }
        var scalar = Utf8SliceLowerer.Lower(completed!, null, false, false, false)
            ?? throw new InvalidOperationException("The admitted POSIX tree could not be lowered.");
        var firstAcceptanceIsLongest = CanSelectFirst(scalar, budget) || CanSelectDisjointAsciiRuns(scalar, budget) ||
            CanSelectRepeatedLiteralCapture(scalar, budget);
        if (!firstAcceptanceIsLongest) PreferLongerScalarAlternatives(scalar.Instructions, budget);
        var chart = !firstAcceptanceIsLongest && groups != 0 ? CreateChart(root, budget)
            : (Nodes: Array.Empty<ChartNode>(), Classes: Array.Empty<Utf8SliceNode.CategorySetNode>());
        var result = budget.AllocateObject((scalar, Parents: parents.Take(), captureTags, firstAcceptanceIsLongest,
                chart.Nodes, chart.Classes),
            static x => new PosixProgram(x.scalar, x.Parents, x.captureTags, x.firstAcceptanceIsLongest, x.Nodes, x.Classes));
        construction.Complete();
        return result;
    }

    private static void PreferLongerScalarAlternatives(Utf8SliceInstruction[] code, Utf8ConstructionBudget budget)
    {
        // Search order is only a heuristic: semantic tags retain their original
        // preorder. Leave proven first-acceptance programs entirely unchanged.
        // Fork.C is private to this POSIX-owned stream; Core never executes it.
        for (var pc = 0; pc < code.Length; pc++)
        {
            budget.ChargeWork(1);
            var fork = code[pc];
            if (fork.Kind != Utf8SliceInstructionKind.Fork || fork.B != 0 ||
                fork.A <= pc + 1 || fork.A >= code.Length ||
                code[fork.A - 1] is not { Kind: Utf8SliceInstructionKind.Jump } jump ||
                jump.A <= fork.A || jump.A > code.Length) continue;
            var left = ScalarBranchLength(code.AsSpan(pc + 1, fork.A - pc - 2), budget);
            var right = ScalarBranchLength(code.AsSpan(fork.A, jump.A - fork.A), budget);
            if (left >= 0 && right > left)
                code[pc] = new(fork.Kind, fork.A, fork.B, 1, fork.D);
        }
    }

    private static int ScalarBranchLength(ReadOnlySpan<Utf8SliceInstruction> branch, Utf8ConstructionBudget budget)
    {
        if (branch.Length > 64) return -1;
        var length = 0;
        foreach (var instruction in branch)
        {
            budget.ChargeWork(1);
            if (instruction.Kind is Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or
                Utf8SliceInstructionKind.CategorySet) length++;
            else if (instruction.Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd))
                return -1;
        }
        return length;
    }

    private static bool CanSelectFirst(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        // A fixed sequence has only one parse. Empty, dot and bracket captures
        // must keep that native trace rather than acquire general memo storage.
        if (program.RepeatCount == 0)
        {
            var unique = true;
            for (var index = 0; index < program.Instructions.Length; index++)
            {
                budget.ChargeWork(1);
                var kind = program.Instructions[index].Kind;
                if (kind is not (Utf8SliceInstructionKind.Literal or Utf8SliceInstructionKind.Dot or
                    Utf8SliceInstructionKind.CategorySet or Utf8SliceInstructionKind.Anchor or
                    Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or Utf8SliceInstructionKind.Accept) ||
                    kind == Utf8SliceInstructionKind.Accept && index != program.Instructions.Length - 1)
                { unique = false; break; }
            }
            if (unique && program.Instructions.Length != 0 && program.Instructions[^1].Kind == Utf8SliceInstructionKind.Accept)
                return true;
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
            budget.ChargeWork(1);
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
        budget.ChargeWork(12);
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
        return inner.RepeatMinimum <= 1 &&
            (outer.RepeatMaximum != long.MaxValue || outer.RepeatMinimum > 0);
    }

    private static bool CanSelectDisjointAsciiRuns(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
        // Mutually disjoint positive classes give each transition one possible
        // input boundary. Greedy selection maximizes the final run; tags outside
        // the bodies have no competing occurrence history at that endpoint.
        Span<byte> seen = stackalloc byte[16];
        seen.Clear();
        var runs = 0;
        var code = program.Instructions;
        for (var index = 0; index < code.Length; index++)
        {
            budget.ChargeWork(1);
            var instruction = code[index];
            if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd or
                Utf8SliceInstructionKind.Anchor) continue;
            if (instruction.Kind == Utf8SliceInstructionKind.Accept)
                return runs >= 2 && index == code.Length - 1;
            if (instruction.Kind != Utf8SliceInstructionKind.RepeatGreedy || instruction.RepeatMinimum < 1 ||
                index > code.Length - 4 || instruction.A != index + 3 ||
                code[index + 2] is not { Kind: Utf8SliceInstructionKind.RepeatEnd } end || end.A != index ||
                code[index + 3] is not { Kind: Utf8SliceInstructionKind.RepeatExit } exit || exit.A != instruction.B ||
                code[index + 1] is not { Kind: Utf8SliceInstructionKind.CategorySet, B: 0, C: 0 } atom ||
                program.CategorySetMembers[atom.D].Length != 0)
                return false;
            var mask = program.SetMasks[atom.A];
            if (mask.Length != seen.Length) return false;
            var nonempty = false;
            for (var octet = 0; octet < seen.Length; octet++)
            {
                budget.ChargeWork(1);
                if ((seen[octet] & mask[octet]) != 0) return false;
                nonempty |= mask[octet] != 0;
                seen[octet] |= mask[octet];
            }
            if (!nonempty) return false;
            runs++;
            index += 3;
        }
        return false;
    }

    private static bool CanSelectFixedAlternative(ReadOnlySpan<Utf8SliceInstruction> code, int fork,
        Utf8ConstructionBudget budget)
    {
        var right = code[fork].A;
        if (code[fork].B != 0 || right <= fork + 2 || right >= code.Length ||
            code[right - 1] is not { Kind: Utf8SliceInstructionKind.Jump } jump ||
            jump.A <= right || jump.A >= code.Length)
            return false;
        var leftBody = code.Slice(fork + 1, right - fork - 2);
        var rightBody = code.Slice(right, jump.A - right);
        var length = FixedLiteralBranchLength(leftBody, budget, out var leftTag);
        if (length <= 0 || FixedLiteralBranchLength(rightBody, budget, out var rightTag) != length ||
            leftTag >= rightTag)
            return false;
        // Both successful branches consume the same scalar prefix. The earlier
        // whole-branch tag has higher preorder priority and participation beats
        // its absence. No suffix may introduce a longer competing endpoint.
        for (var index = jump.A; index < code.Length; index++)
        {
            budget.ChargeWork(1);
            if (code[index].Kind == Utf8SliceInstructionKind.Accept)
                return index == code.Length - 1;
            if (code[index].Kind is not (Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd))
                return false;
        }
        return false;
    }

    private static int FixedLiteralBranchLength(ReadOnlySpan<Utf8SliceInstruction> body,
        Utf8ConstructionBudget budget, out int tag)
    {
        tag = -1;
        if (body.Length < 3 || body[0] is not { Kind: Utf8SliceInstructionKind.CaptureStart } open ||
            body[^1] is not { Kind: Utf8SliceInstructionKind.CaptureEnd } close || open.A != close.A)
            return -1;
        tag = open.A;
        var length = 0;
        foreach (var instruction in body)
        {
            budget.ChargeWork(1);
            if (instruction.Kind is Utf8SliceInstructionKind.CaptureStart or Utf8SliceInstructionKind.CaptureEnd)
            {
                // Internal descendants follow the whole-branch tag; a shared
                // ancestor cannot masquerade as an earlier participating branch.
                if (instruction.A < tag) return -1;
            }
            else if (instruction.Kind == Utf8SliceInstructionKind.Literal && instruction.B == 0) length++;
            else return -1;
        }
        return length;
    }

    private static bool ContainsCapture(Utf8SliceNode root, Utf8ConstructionBudget budget)
    {
        using var frames = Utf8ConstructionFrames<Frame>.Create(budget, new(root, -1));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (frame.Node is Utf8SliceNode.CaptureNode) return true;
            Utf8SliceNode? child = null;
            switch (frame.Node)
            {
                case Utf8SliceNode.ConcatNode concat when frame.Child < concat.Children.Length:
                    child = concat.Children[frame.Child++]; break;
                case Utf8SliceNode.AlternationNode alternate:
                    while (frame.Branch < alternate.Branches.Length && frame.Child == alternate.Branches[frame.Branch].Length)
                    { frame.Branch++; frame.Child = 0; }
                    if (frame.Branch < alternate.Branches.Length) child = alternate.Branches[frame.Branch][frame.Child++];
                    break;
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
        internal int Child, Branch;
        internal bool Awaiting;
        internal Utf8SliceNode[]? Children;
        internal Utf8SliceNode[][]? Branches;
        internal Utf8SliceNode? Body;
    }
}
