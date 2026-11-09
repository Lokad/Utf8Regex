using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Posix;

// Keep semantic subpattern tags, including non-public concatenation/repetition
// parts. Final public capture slots alone cannot order repeated POSIX parses.
internal sealed class PosixProgram
{
    private PosixProgram(Utf8SliceProgram scalar, int[] parents, int[] captureTags, bool firstAcceptanceIsLongest)
    { Scalar = scalar; Parents = parents; CaptureTags = captureTags; FirstAcceptanceIsLongest = firstAcceptanceIsLongest; }

    internal Utf8SliceProgram Scalar { get; }
    internal int[] Parents { get; }
    internal int[] CaptureTags { get; }
    internal bool FirstAcceptanceIsLongest { get; }

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
        var firstAcceptanceIsLongest = CanSelectFirst(scalar, budget);
        var result = budget.AllocateObject((scalar, Parents: parents.Take(), captureTags, firstAcceptanceIsLongest),
            static x => new PosixProgram(x.scalar, x.Parents, x.captureTags, x.firstAcceptanceIsLongest));
        construction.Complete();
        return result;
    }

    private static bool CanSelectFirst(Utf8SliceProgram program, Utf8ConstructionBudget budget)
    {
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
