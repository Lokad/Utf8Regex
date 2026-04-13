using System.Runtime.CompilerServices;

namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

/// <summary>
/// Vendored/adapted from dotnet/runtime. Analyzes a runtime-shaped regex tree for structural properties
/// that later lowerings and backends can consume without re-walking ad hoc.
/// </summary>
internal static class RegexTreeAnalyzer
{
    public static AnalysisResults Analyze(RegexTree regexTree)
    {
        ArgumentNullException.ThrowIfNull(regexTree);

        var results = new AnalysisResults(regexTree);
        results.Complete = TryAnalyze(regexTree.Root, results, isAtomicByAncestor: true, isInLoop: false);
        return results;

        static bool TryAnalyze(RegexNode node, AnalysisResults results, bool isAtomicByAncestor, bool isInLoop)
        {
            if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
            {
                return false;
            }

            results.HasIgnoreCase |= (node.Options & RegexOptions.IgnoreCase) != 0;
            results.HasRightToLeft |= (node.Options & RegexOptions.RightToLeft) != 0;

            if (isInLoop)
            {
                (results._inLoops ??= new HashSet<RegexNode>()).Add(node);
            }

            if (isAtomicByAncestor)
            {
                results._isAtomicByAncestor.Add(node);
            }
            else if (node.IsBacktrackingConstruct)
            {
                (results._mayBacktrack ??= new HashSet<RegexNode>()).Add(node);
            }

            var isAtomicBySelf = false;
            switch (node.Kind)
            {
                case RegexNodeKind.Atomic:
                case RegexNodeKind.NegativeLookaround:
                case RegexNodeKind.PositiveLookaround:
                    isAtomicBySelf = true;
                    break;

                case RegexNodeKind.Capture:
                    results._containsCapture.Add(node);
                    break;

                case RegexNodeKind.Loop:
                case RegexNodeKind.Lazyloop:
                    isInLoop = true;
                    break;
            }

            for (var i = 0; i < node.ChildCount; i++)
            {
                var child = node.Child(i);
                var treatChildAsAtomic = (isAtomicByAncestor || isAtomicBySelf) && node.Kind switch
                {
                    RegexNodeKind.Atomic or RegexNodeKind.NegativeLookaround or RegexNodeKind.PositiveLookaround => true,
                    RegexNodeKind.Alternate or RegexNodeKind.BackreferenceConditional or RegexNodeKind.ExpressionConditional => true,
                    RegexNodeKind.Capture => true,
                    RegexNodeKind.Concatenate => i == node.ChildCount - 1,
                    RegexNodeKind.Loop or RegexNodeKind.Lazyloop when node.N == 1 => true,
                    _ => false,
                };

                if (!TryAnalyze(child, results, treatChildAsAtomic, isInLoop))
                {
                    return false;
                }

                if (results._containsCapture.Contains(child))
                {
                    results._containsCapture.Add(node);
                }

                if (!isAtomicBySelf && results._mayBacktrack?.Contains(child) == true)
                {
                    (results._mayBacktrack ??= new HashSet<RegexNode>()).Add(node);
                }
            }

            return true;
        }
    }
}

internal sealed class AnalysisResults
{
    internal readonly HashSet<RegexNode> _isAtomicByAncestor = new();
    internal readonly HashSet<RegexNode> _containsCapture = new();
    internal HashSet<RegexNode>? _mayBacktrack;
    internal HashSet<RegexNode>? _inLoops;

    internal AnalysisResults(RegexTree regexTree)
    {
        RegexTree = regexTree;
    }

    internal bool Complete { get; set; }

    public RegexTree RegexTree { get; }

    public bool HasIgnoreCase { get; internal set; }

    public bool HasRightToLeft { get; internal set; }

    public bool IsAtomicByAncestor(RegexNode node) => Complete && _isAtomicByAncestor.Contains(node);

    public bool MayContainCapture(RegexNode node) => !Complete || _containsCapture.Contains(node);

    public bool MayBacktrack(RegexNode node) => !Complete || (_mayBacktrack?.Contains(node) ?? false);

    public bool IsInLoop(RegexNode node) => !Complete || (_inLoops?.Contains(node) ?? false);
}
