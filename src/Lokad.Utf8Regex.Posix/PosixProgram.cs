using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Posix;

// Keep semantic subpattern tags, including non-public concatenation/repetition
// parts. Final public capture slots alone cannot order repeated POSIX parses.
internal sealed class PosixProgram
{
    private PosixProgram(Utf8SliceProgram scalar, int[] parents, int[] captureTags)
    { Scalar = scalar; Parents = parents; CaptureTags = captureTags; }

    internal Utf8SliceProgram Scalar { get; }
    internal int[] Parents { get; }
    internal int[] CaptureTags { get; }

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
                if (frame.Node is Utf8SliceNode.CaptureNode capture) captureTags[capture.Slot] = frame.Tag;
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
        var result = budget.AllocateObject((scalar, Parents: parents.Take(), captureTags),
            static x => new PosixProgram(x.scalar, x.Parents, x.captureTags));
        construction.Complete();
        return result;
    }

    private struct Frame(Utf8SliceNode node, int parent)
    {
        internal readonly Utf8SliceNode Node = node;
        internal readonly int Parent = parent;
        internal int Tag = -1;
        internal int Child, Branch;
        internal bool Awaiting;
        internal Utf8SliceNode[]? Children;
        internal Utf8SliceNode[][]? Branches;
        internal Utf8SliceNode? Body;
    }
}
