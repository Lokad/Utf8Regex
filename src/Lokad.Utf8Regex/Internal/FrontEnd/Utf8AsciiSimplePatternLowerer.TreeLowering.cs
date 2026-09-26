using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Internal.FrontEnd;


internal static partial class Utf8AsciiSimplePatternLowerer
{
    private static bool TryLowerBranches(Utf8TreeNode node, Utf8PreparationBudget budget, int depth, out LoweredBranch[] branches)
    {
        if (depth > MaxLoweringDepth)
        {
            branches = [];
            return false;
        }
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.Capture:
            case Utf8TreeNodeKind.Group:
                if (node.ChildCount != 1)
                {
                    branches = [];
                    return false;
                }

                return TryLowerBranches(node.Child(0), budget, depth + 1, out branches);

            case Utf8TreeNodeKind.Empty:
                branches = [new LoweredBranch([], [], false, false)];
                return true;

            case Utf8TreeNodeKind.Bol:
            case Utf8TreeNodeKind.Beginning:
            case Utf8TreeNodeKind.Start:
                branches = [new LoweredBranch([], [], true, false)];
                return true;

            case Utf8TreeNodeKind.Eol:
            case Utf8TreeNodeKind.EndZ:
            case Utf8TreeNodeKind.End:
                branches = [new LoweredBranch([], [], false, true)];
                return true;

            case Utf8TreeNodeKind.One:
                if (!TryLowerLiteral(node.Ch, out var literalToken))
                {
                    branches = [];
                    return false;
                }

                branches = [new LoweredBranch([literalToken], [], false, false)];
                return true;

            case Utf8TreeNodeKind.Multi:
                if (!TryLowerLiteralText(node.Str, out var literalTokens))
                {
                    branches = [];
                    return false;
                }

                branches = [new LoweredBranch(literalTokens, [], false, false)];
                return true;

            case Utf8TreeNodeKind.Set:
                if (!TryLowerSet(node.Str, (node.Options & RegexOptions.Singleline) != 0, out var setToken))
                {
                    branches = [];
                    return false;
                }

                branches = [new LoweredBranch([setToken], [], false, false)];
                return true;

            case Utf8TreeNodeKind.PositiveLookaround:
                return TryLowerPositiveLookaround(node, budget, depth + 1, out branches);

            case Utf8TreeNodeKind.Concatenate:
                return TryLowerConcatenation(node, budget, depth + 1, out branches);

            case Utf8TreeNodeKind.Alternate:
                return TryLowerAlternation(node, budget, depth + 1, out branches);

            case Utf8TreeNodeKind.Oneloop:
                if (!TryLowerLiteral(node.Ch, out var repeatedLiteral))
                {
                    branches = [];
                    return false;
                }

                return TryExpandFiniteRepeat(
                    [new LoweredBranch([repeatedLiteral], [], false, false)],
                    node.M,
                    node.N,
                    budget,
                    out branches);

            case Utf8TreeNodeKind.Setloop:
                if (!TryLowerSet(node.Str, (node.Options & RegexOptions.Singleline) != 0, out var repeatedSet))
                {
                    branches = [];
                    return false;
                }

                return TryExpandFiniteRepeat(
                    [new LoweredBranch([repeatedSet], [], false, false)],
                    node.M,
                    node.N,
                    budget,
                    out branches);

            case Utf8TreeNodeKind.Loop:
                if (node.ChildCount != 1 || node.N == int.MaxValue || node.N < node.M)
                {
                    branches = [];
                    return false;
                }

                if (!TryLowerBranches(node.Child(0), budget, depth + 1, out var loopBranches))
                {
                    branches = [];
                    return false;
                }

                return TryExpandFiniteRepeat(loopBranches, node.M, node.N, budget, out branches);

            default:
                branches = [];
                return false;
        }
    }

    private static bool AllowsTrailingNewlineBeforeEnd(Utf8TreeNode node)
    {
        switch (node.Kind)
        {
            case Utf8TreeNodeKind.Eol:
            case Utf8TreeNodeKind.EndZ:
                return true;

            case Utf8TreeNodeKind.Capture:
            case Utf8TreeNodeKind.Group:
                return node.ChildCount == 1 && AllowsTrailingNewlineBeforeEnd(node.Child(0));

            case Utf8TreeNodeKind.Concatenate:
                return node.ChildCount > 0 && AllowsTrailingNewlineBeforeEnd(node.Child(node.ChildCount - 1));

            case Utf8TreeNodeKind.Alternate:
                if (node.ChildCount == 0)
                {
                    return false;
                }

                for (var i = 0; i < node.ChildCount; i++)
                {
                    if (!AllowsTrailingNewlineBeforeEnd(node.Child(i)))
                    {
                        return false;
                    }
                }

                return true;

            default:
                return false;
        }
    }

    private static bool TryLowerConcatenation(Utf8TreeNode node, Utf8PreparationBudget budget, int depth, out LoweredBranch[] branches)
    {
        if (depth > MaxLoweringDepth)
        {
            branches = [];
            return false;
        }
        var accumulated = new List<LoweredBranch> { new([], [], false, false) };

        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryLowerBranches(node.Child(i), budget, depth + 1, out var childBranches))
            {
                branches = [];
                return false;
            }

            if (accumulated.Count == 1 &&
                childBranches.Length == 1 &&
                !accumulated[0].IsStartAnchored &&
                !accumulated[0].IsEndAnchored &&
                !childBranches[0].IsStartAnchored &&
                !childBranches[0].IsEndAnchored)
            {
                if (!TryAppendBranch(accumulated[0], childBranches[0]))
                {
                    branches = [];
                    return false;
                }

                continue;
            }

            if (!TryConcatenate(accumulated, childBranches, budget, out accumulated))
            {
                branches = [];
                return false;
            }
        }

        branches = [.. accumulated];
        return true;
    }

    private static bool TryLowerAlternation(Utf8TreeNode node, Utf8PreparationBudget budget, int depth, out LoweredBranch[] branches)
    {
        if (depth > MaxLoweringDepth)
        {
            branches = [];
            return false;
        }
        var alternates = new List<LoweredBranch>(node.ChildCount);
        for (var i = 0; i < node.ChildCount; i++)
        {
            if (!TryLowerBranches(node.Child(i), budget, depth + 1, out var childBranches) ||
                !budget.TryReserve(childBranches.Length, childBranches.Length))
            {
                branches = [];
                return false;
            }

            alternates.AddRange(childBranches);
            if (alternates.Count > MaxExpandedBranches)
            {
                branches = [];
                return false;
            }
        }

        branches = [.. alternates];
        return true;
    }

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
                if (!TryCombineBranches(left, right, out var branch))
                {
                    return false;
                }

                combined.Add(branch);
            }
        }

        return true;
    }

    private static bool TryAppendBranch(LoweredBranch left, LoweredBranch right)
    {
        var leftLength = left.Tokens.Count;
        left.Tokens.AddRange(right.Tokens);
        foreach (var check in right.FixedChecks)
        {
            left.FixedChecks.Add(new AsciiFixedLiteralCheck(leftLength + check.Offset, check.Literal));
        }

        return true;
    }

    private static bool TryCombineBranches(LoweredBranch left, LoweredBranch right, out LoweredBranch branch)
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
            left.IsEndAnchored || right.IsEndAnchored);
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
            var expanded = new List<LoweredBranch> { new([], [], false, false) };
            for (var i = 0; i < count; i++)
            {
                if (!TryConcatenate(expanded, repeatedBranches, budget, out expanded))
                {
                    return false;
                }
            }

            variants.AddRange(expanded);
            if (variants.Count > MaxExpandedBranches)
            {
                return false;
            }
        }

        branches = [.. variants];
        return true;
    }

    private static bool TryLowerPositiveLookaround(Utf8TreeNode node, Utf8PreparationBudget budget, int depth, out LoweredBranch[] branches)
    {
        if (depth > MaxLoweringDepth)
        {
            branches = [];
            return false;
        }
        branches = [];
        if ((node.Options & RegexOptions.RightToLeft) != 0 ||
            node.ChildCount != 1 ||
            !TryLowerBranches(node.Child(0), budget, depth + 1, out var childBranches) ||
            childBranches.Length != 1)
        {
            return false;
        }

        var child = childBranches[0];
        if (child.IsStartAnchored || child.IsEndAnchored || child.FixedChecks.Count != 0)
        {
            return false;
        }

        var literal = ExtractFullLiteral(child.Tokens);
        if (literal.Length == 0)
        {
            return false;
        }

        branches = [new LoweredBranch([], [new AsciiFixedLiteralCheck(0, literal)], false, false)];
        return true;
    }
}
