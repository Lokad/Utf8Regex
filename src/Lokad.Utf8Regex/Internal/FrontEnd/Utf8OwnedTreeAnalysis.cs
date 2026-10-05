namespace Lokad.Utf8Regex.Internal.FrontEnd;


// Owned per-node tree facts over runtime-shaped regex trees of either
// provenance: tree-wide case/direction flags, atomic-by-ancestor,
// capture containment, backtracking, and loop membership. Incomplete analyses
// degrade to conservative answers, matching the legacy contract.
internal static class Utf8OwnedTreeAnalyzer
{
    public static Utf8OwnedTreeAnalysis Analyze(Utf8OwnedRegexTree regexTree)
    {
        var results = new Utf8OwnedTreeAnalysis(regexTree);
        var frames = new Stack<(Utf8TreeNode Node, bool IsAtomicByAncestor, bool IsInLoop, int NextChild)>();
        frames.Push((regexTree.Root, true, false, 0));
        while (frames.TryPop(out var frame))
        {
            var node = frame.Node;
            var isAtomicBySelf = node.Kind is Utf8TreeNodeKind.Atomic
                or Utf8TreeNodeKind.NegativeLookaround
                or Utf8TreeNodeKind.PositiveLookaround;
            if (frame.NextChild == 0)
            {
                results.HasIgnoreCase |= (node.Options & RegexOptions.IgnoreCase) != 0;
                results.HasRightToLeft |= (node.Options & RegexOptions.RightToLeft) != 0;
                if (frame.IsInLoop)
                    (results.InLoops ??= new HashSet<Utf8TreeNode>()).Add(node);
                if (frame.IsAtomicByAncestor)
                    results.IsAtomicByAncestorSet.Add(node);
                else if (node.IsBacktrackingConstruct)
                    (results.MayBacktrackSet ??= new HashSet<Utf8TreeNode>()).Add(node);
                if (node.Kind == Utf8TreeNodeKind.Capture)
                    results.ContainsCaptureSet.Add(node);
            }

            if (frame.NextChild < node.ChildCount)
            {
                var index = frame.NextChild;
                var treatChildAsAtomic = (frame.IsAtomicByAncestor || isAtomicBySelf) && node.Kind switch
                {
                    Utf8TreeNodeKind.Atomic
                        or Utf8TreeNodeKind.NegativeLookaround
                        or Utf8TreeNodeKind.PositiveLookaround => true,
                    Utf8TreeNodeKind.Alternate
                        or Utf8TreeNodeKind.BackreferenceConditional
                        or Utf8TreeNodeKind.ExpressionConditional => true,
                    Utf8TreeNodeKind.Capture => true,
                    Utf8TreeNodeKind.Concatenate => index == node.ChildCount - 1,
                    Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop when node.N == 1 => true,
                    _ => false,
                };
                frames.Push((node, frame.IsAtomicByAncestor, frame.IsInLoop, index + 1));
                frames.Push((node.Child(index), treatChildAsAtomic,
                    frame.IsInLoop || node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop, 0));
                continue;
            }

            // Every child has finished before containment/backtracking facts
            // propagate to its parent, preserving the original traversal.
            for (var index = 0; index < node.ChildCount; index++)
            {
                var child = node.Child(index);
                if (results.ContainsCaptureSet.Contains(child))
                    results.ContainsCaptureSet.Add(node);
                if (!isAtomicBySelf && results.MayBacktrackSet?.Contains(child) == true)
                    (results.MayBacktrackSet ??= new HashSet<Utf8TreeNode>()).Add(node);
            }
        }
        results.Complete = true;
        return results;
    }
}

internal sealed class Utf8OwnedTreeAnalysis
{
    internal Utf8OwnedTreeAnalysis(Utf8OwnedRegexTree regexTree)
    {
        RegexTree = regexTree;
    }

    internal bool Complete { get; set; }

    public Utf8OwnedRegexTree RegexTree { get; }

    public bool HasIgnoreCase { get; internal set; }

    public bool HasRightToLeft { get; internal set; }

    internal HashSet<Utf8TreeNode> IsAtomicByAncestorSet { get; } = new();

    internal HashSet<Utf8TreeNode> ContainsCaptureSet { get; } = new();

    internal HashSet<Utf8TreeNode>? MayBacktrackSet { get; set; }

    internal HashSet<Utf8TreeNode>? InLoops { get; set; }

    public bool IsAtomicByAncestor(Utf8TreeNode node) => Complete && IsAtomicByAncestorSet.Contains(node);

    public bool MayContainCapture(Utf8TreeNode node) => !Complete || ContainsCaptureSet.Contains(node);

    public bool MayBacktrack(Utf8TreeNode node) => !Complete || (MayBacktrackSet?.Contains(node) ?? false);

    public bool IsInLoop(Utf8TreeNode node) => !Complete || (InLoops?.Contains(node) ?? false);
}
