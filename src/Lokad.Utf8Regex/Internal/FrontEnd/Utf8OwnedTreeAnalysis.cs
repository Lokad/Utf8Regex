using System.Runtime.CompilerServices;

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
        results.Complete = TryAnalyze(regexTree.Root, results, isAtomicByAncestor: true, isInLoop: false);
        return results;
    }

    private static bool TryAnalyze(
        Utf8TreeNode node,
        Utf8OwnedTreeAnalysis results,
        bool isAtomicByAncestor,
        bool isInLoop)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return false;
        }

        if ((node.Options & RegexOptions.IgnoreCase) != 0)
        {
            results.HasIgnoreCase = true;
        }

        if ((node.Options & RegexOptions.RightToLeft) != 0)
        {
            results.HasRightToLeft = true;
        }

        if (isInLoop)
        {
            (results.InLoops ??= new HashSet<Utf8TreeNode>()).Add(node);
        }

        if (isAtomicByAncestor)
        {
            results.IsAtomicByAncestorSet.Add(node);
        }
        else if (node.IsBacktrackingConstruct)
        {
            (results.MayBacktrackSet ??= new HashSet<Utf8TreeNode>()).Add(node);
        }

        var isAtomicBySelf = node.Kind is Utf8TreeNodeKind.Atomic
            or Utf8TreeNodeKind.NegativeLookaround
            or Utf8TreeNodeKind.PositiveLookaround;
        if (node.Kind == Utf8TreeNodeKind.Capture)
        {
            results.ContainsCaptureSet.Add(node);
        }

        if (node.Kind is Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop)
        {
            isInLoop = true;
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i);
            var treatChildAsAtomic = (isAtomicByAncestor || isAtomicBySelf) && node.Kind switch
            {
                Utf8TreeNodeKind.Atomic
                    or Utf8TreeNodeKind.NegativeLookaround
                    or Utf8TreeNodeKind.PositiveLookaround => true,
                Utf8TreeNodeKind.Alternate
                    or Utf8TreeNodeKind.BackreferenceConditional
                    or Utf8TreeNodeKind.ExpressionConditional => true,
                Utf8TreeNodeKind.Capture => true,
                Utf8TreeNodeKind.Concatenate => i == node.ChildCount - 1,
                Utf8TreeNodeKind.Loop or Utf8TreeNodeKind.Lazyloop when node.N == 1 => true,
                _ => false,
            };
            if (!TryAnalyze(child, results, treatChildAsAtomic, isInLoop))
            {
                return false;
            }

            if (results.ContainsCaptureSet.Contains(child))
            {
                results.ContainsCaptureSet.Add(node);
            }

            if (!isAtomicBySelf && results.MayBacktrackSet?.Contains(child) == true)
            {
                (results.MayBacktrackSet ??= new HashSet<Utf8TreeNode>()).Add(node);
            }
        }

        return true;
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