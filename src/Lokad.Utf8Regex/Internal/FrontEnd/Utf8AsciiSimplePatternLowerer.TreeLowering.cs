using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static partial class Utf8AsciiSimplePatternLowerer
{
    private static bool TryLowerBranches(Utf8ConstructionSession storage, Utf8TreeNode node,
        Utf8PreparationBudget budget, int depth, out Utf8ConstructionBuffer<LoweredBranch> branches)
        => BranchLowerer.Create(budget, storage).TryLower(node, depth, out branches);

    private static bool AllowsTrailingNewlineBeforeEnd(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<ScanFrame>.Create(budget, new(node, 0, false));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            node = frame.Node;
            if (!frame.Entered)
            {
                frame = frame with { Entered = true };
                switch (node.Kind)
                {
                    case Utf8TreeNodeKind.Eol:
                    case Utf8TreeNodeKind.EndZ:
                        break;
                    case Utf8TreeNodeKind.Capture:
                    case Utf8TreeNodeKind.Group:
                        if (node.ChildCount != 1) return false;
                        break;
                    case Utf8TreeNodeKind.Concatenate:
                        if (node.ChildCount == 0) return false;
                        frame = frame with { NextChild = node.ChildCount - 1 };
                        break;
                    case Utf8TreeNodeKind.Alternate:
                        if (node.ChildCount == 0) return false;
                        break;
                    default:
                        return false;
                }
            }
            var childCount = node.Kind is Utf8TreeNodeKind.Capture or Utf8TreeNodeKind.Group or Utf8TreeNodeKind.Concatenate or Utf8TreeNodeKind.Alternate
                ? node.ChildCount : 0;
            if (frame.NextChild == childCount) { frames.Pop(); continue; }
            frames[index] = frame with { NextChild = frame.NextChild + 1 };
            frames.Push(new(node.Child(frame.NextChild), 0, false));
        }
        return true;
    }

    private static bool TryReserveBranchCells(Utf8PreparationBudget budget, long count)
        => count <= int.MaxValue && budget.TryReserve((int)count, (int)count);

    private static bool TryConcatenate(
        Utf8ConstructionSession storage,
        Utf8ConstructionBuffer<LoweredBranch> leftBranches,
        Utf8ConstructionBuffer<LoweredBranch> rightBranches,
        Utf8PreparationBudget budget,
        out Utf8ConstructionBuffer<LoweredBranch> combined)
    {
        if (leftBranches.Count == 0 || rightBranches.Count == 0)
        {
            combined = storage.Buffer<LoweredBranch>();
            return false;
        }

        if (!budget.TryReserveProduct(leftBranches.Count, rightBranches.Count, MaxExpandedBranches, out var product))
        {
            combined = storage.Buffer<LoweredBranch>();
            return false;
        }

        combined = storage.Buffer<LoweredBranch>();
        foreach (var left in leftBranches)
        {
            Utf8ConstructionScope.ChargeWork(1);
            foreach (var right in rightBranches)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (!TryCombineBranches(storage, left, right, budget, out var branch))
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
        left.Tokens.AddRange(right.Tokens.Span);
        foreach (var check in right.FixedChecks)
        {
            Utf8ConstructionScope.ChargeWork(1);
            left.FixedChecks.Add(new AsciiFixedLiteralCheck(leftLength + check.Offset, check.Literal));
        }

        return true;
    }

    private static bool TryCombineBranches(Utf8ConstructionSession storage, LoweredBranch left, LoweredBranch right, Utf8PreparationBudget budget, out LoweredBranch branch)
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

        var combinedTokens = storage.Buffer<AsciiSimplePatternToken>();
        combinedTokens.AddRange(left.Tokens.Span);
        combinedTokens.AddRange(right.Tokens.Span);

        var combinedChecks = storage.Buffer<AsciiFixedLiteralCheck>();
        combinedChecks.AddRange(left.FixedChecks.Span);
        foreach (var check in right.FixedChecks)
        {
            Utf8ConstructionScope.ChargeWork(1);
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
        Utf8ConstructionSession storage,
        Utf8ConstructionBuffer<LoweredBranch> repeatedBranches,
        int minCount,
        int maxCount,
        Utf8PreparationBudget budget,
        out Utf8ConstructionBuffer<LoweredBranch> branches)
    {
        branches = storage.Buffer<LoweredBranch>();
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

        var variants = storage.Buffer<LoweredBranch>();
        for (var count = maxCount; count >= minCount; count--)
        {
            Utf8ConstructionScope.ChargeWork(1);
            var expanded = SingleBranch(storage, EmptyBranch(storage));
            for (var i = 0; i < count; i++)
            {
                Utf8ConstructionScope.ChargeWork(1);
                if (!TryConcatenate(storage, expanded, repeatedBranches, budget, out expanded))
                {
                    return false;
                }
            }

            if (expanded.Count > MaxExpandedBranches - variants.Count)
            {
                return false;
            }
            variants.AddRange(expanded.Span);
        }

        branches = variants;
        return true;
    }

}
