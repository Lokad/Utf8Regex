using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    private static bool TryLowerBranches(Utf8TreeNode node, Utf8PreparationBudget budget, int depth, out LoweredBranch[] branches)
        => new BranchLowerer(budget).TryLower(node, depth, out branches);

    private static bool AllowsTrailingNewlineBeforeEnd(Utf8TreeNode node)
    {
        var pending = new Stack<(Utf8TreeNode Node, int Child)>();
        while (true)
        {
            switch (node.Kind)
            {
                case Utf8TreeNodeKind.Eol:
                case Utf8TreeNodeKind.EndZ:
                    break;
                case Utf8TreeNodeKind.Capture:
                case Utf8TreeNodeKind.Group:
                    if (node.ChildCount != 1) return false;
                    node = node.Child(0);
                    continue;
                case Utf8TreeNodeKind.Concatenate:
                    if (node.ChildCount == 0) return false;
                    node = node.Child(node.ChildCount - 1);
                    continue;
                case Utf8TreeNodeKind.Alternate:
                    if (node.ChildCount == 0) return false;
                    if (node.ChildCount > 1) pending.Push((node, 1));
                    node = node.Child(0);
                    continue;
                default:
                    return false;
            }
            if (!pending.TryPop(out var next)) return true;
            if (next.Child + 1 < next.Node.ChildCount) pending.Push((next.Node, next.Child + 1));
            node = next.Node.Child(next.Child);
        }
    }

    private static bool TryReserveBranchCells(Utf8PreparationBudget budget, long count)
        => count <= int.MaxValue && budget.TryReserve((int)count, (int)count);

    private static bool TryConcatenate(
        List<LoweredBranch> leftBranches,
        IReadOnlyList<LoweredBranch> rightBranches,
        Utf8PreparationBudget budget,
        out List<LoweredBranch> combined)
    {
        if (leftBranches.Count == 0 || rightBranches.Count == 0)
        {
            combined = [];
            return false;
        }

        if (!budget.TryReserveProduct(leftBranches.Count, rightBranches.Count, MaxExpandedBranches, out var product))
        {
            combined = [];
            return false;
        }

        combined = new List<LoweredBranch>(product);
        foreach (var left in leftBranches)
        {
            foreach (var right in rightBranches)
            {
                if (!TryCombineBranches(left, right, budget, out var branch))
                {
                    return false;
                }

                combined.Add(branch);
            }
        }

        return true;
    }

    private static bool TryAppendBranch(LoweredBranch left, LoweredBranch right, Utf8PreparationBudget budget)
    {
        if (!TryReserveBranchCells(budget, (long)right.Tokens.Count + right.FixedChecks.Count)) return false;
        var leftLength = left.Tokens.Count;
        left.Tokens.AddRange(right.Tokens);
        foreach (var check in right.FixedChecks)
        {
            left.FixedChecks.Add(new AsciiFixedLiteralCheck(leftLength + check.Offset, check.Literal));
        }

        return true;
    }

    private static bool TryCombineBranches(LoweredBranch left, LoweredBranch right, Utf8PreparationBudget budget, out LoweredBranch branch)
    {
        if (right.IsStartAnchored && (left.IsStartAnchored || left.IsEndAnchored || left.Tokens.Count != 0))
        {
            branch = default;
            return false;
        }

        if (left.IsEndAnchored && (right.IsStartAnchored || right.IsEndAnchored || right.Tokens.Count != 0))
        {
            branch = default;
            return false;
        }

        if (!TryReserveBranchCells(budget, (long)left.Tokens.Count + right.Tokens.Count + left.FixedChecks.Count + right.FixedChecks.Count))
        {
            branch = default;
            return false;
        }

        var combinedTokens = new List<AsciiSimplePatternToken>(left.Tokens.Count + right.Tokens.Count);
        combinedTokens.AddRange(left.Tokens);
        combinedTokens.AddRange(right.Tokens);

        var combinedChecks = new List<AsciiFixedLiteralCheck>(left.FixedChecks.Count + right.FixedChecks.Count);
        combinedChecks.AddRange(left.FixedChecks);
        foreach (var check in right.FixedChecks)
        {
            combinedChecks.Add(new AsciiFixedLiteralCheck(left.Tokens.Count + check.Offset, check.Literal));
        }

        branch = new LoweredBranch(
            combinedTokens,
            combinedChecks,
            left.IsStartAnchored || right.IsStartAnchored,
            left.IsEndAnchored || right.IsEndAnchored,
            left.IsAbsoluteStartAnchored || right.IsAbsoluteStartAnchored,
            left.IsAbsoluteEndAnchored || right.IsAbsoluteEndAnchored);
        return true;
    }

    private static bool TryExpandFiniteRepeat(
        IReadOnlyList<LoweredBranch> repeatedBranches,
        int minCount,
        int maxCount,
        Utf8PreparationBudget budget,
        out LoweredBranch[] branches)
    {
        branches = [];
        if (minCount < 0 || maxCount < minCount || maxCount == int.MaxValue)
        {
            return false;
        }

        var variantCount = (long)maxCount - minCount + 1;
        if (variantCount > MaxExpandedBranches ||
            !budget.TryReserve((int)variantCount, (int)variantCount))
        {
            return false;
        }

        var variants = new List<LoweredBranch>();
        for (var count = maxCount; count >= minCount; count--)
        {
            var expanded = new List<LoweredBranch> { new([], [], false, false, false, false) };
            for (var i = 0; i < count; i++)
            {
                if (!TryConcatenate(expanded, repeatedBranches, budget, out expanded))
                {
                    return false;
                }
            }

            if (expanded.Count > MaxExpandedBranches - variants.Count)
            {
                return false;
            }
            variants.AddRange(expanded);
        }

        branches = [.. variants];
        return true;
    }

}
