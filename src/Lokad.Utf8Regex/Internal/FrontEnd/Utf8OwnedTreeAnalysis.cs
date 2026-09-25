using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

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
        RuntimeFrontEnd.RegexNode node,
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
            (results.InLoops ??= new HashSet<RuntimeFrontEnd.RegexNode>()).Add(node);
        }

        if (isAtomicByAncestor)
        {
            results.IsAtomicByAncestorSet.Add(node);
        }
        else if (node.IsBacktrackingConstruct)
        {
            (results.MayBacktrackSet ??= new HashSet<RuntimeFrontEnd.RegexNode>()).Add(node);
        }

        var isAtomicBySelf = node.Kind is RuntimeFrontEnd.RegexNodeKind.Atomic
            or RuntimeFrontEnd.RegexNodeKind.NegativeLookaround
            or RuntimeFrontEnd.RegexNodeKind.PositiveLookaround;
        if (node.Kind == RuntimeFrontEnd.RegexNodeKind.Capture)
        {
            results.ContainsCaptureSet.Add(node);
        }

        if (node.Kind is RuntimeFrontEnd.RegexNodeKind.Loop or RuntimeFrontEnd.RegexNodeKind.Lazyloop)
        {
            isInLoop = true;
        }

        for (var i = 0; i < node.ChildCount; i++)
        {
            var child = node.Child(i);
            var treatChildAsAtomic = (isAtomicByAncestor || isAtomicBySelf) && node.Kind switch
            {
                RuntimeFrontEnd.RegexNodeKind.Atomic
                    or RuntimeFrontEnd.RegexNodeKind.NegativeLookaround
                    or RuntimeFrontEnd.RegexNodeKind.PositiveLookaround => true,
                RuntimeFrontEnd.RegexNodeKind.Alternate
                    or RuntimeFrontEnd.RegexNodeKind.BackreferenceConditional
                    or RuntimeFrontEnd.RegexNodeKind.ExpressionConditional => true,
                RuntimeFrontEnd.RegexNodeKind.Capture => true,
                RuntimeFrontEnd.RegexNodeKind.Concatenate => i == node.ChildCount - 1,
                RuntimeFrontEnd.RegexNodeKind.Loop or RuntimeFrontEnd.RegexNodeKind.Lazyloop when node.N == 1 => true,
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
                (results.MayBacktrackSet ??= new HashSet<RuntimeFrontEnd.RegexNode>()).Add(node);
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

    internal HashSet<RuntimeFrontEnd.RegexNode> IsAtomicByAncestorSet { get; } = new();

    internal HashSet<RuntimeFrontEnd.RegexNode> ContainsCaptureSet { get; } = new();

    internal HashSet<RuntimeFrontEnd.RegexNode>? MayBacktrackSet { get; set; }

    internal HashSet<RuntimeFrontEnd.RegexNode>? InLoops { get; set; }

    public bool IsAtomicByAncestor(RuntimeFrontEnd.RegexNode node) => Complete && IsAtomicByAncestorSet.Contains(node);

    public bool MayContainCapture(RuntimeFrontEnd.RegexNode node) => !Complete || ContainsCaptureSet.Contains(node);

    public bool MayBacktrack(RuntimeFrontEnd.RegexNode node) => !Complete || (MayBacktrackSet?.Contains(node) ?? false);

    public bool IsInLoop(RuntimeFrontEnd.RegexNode node) => !Complete || (InLoops?.Contains(node) ?? false);
}