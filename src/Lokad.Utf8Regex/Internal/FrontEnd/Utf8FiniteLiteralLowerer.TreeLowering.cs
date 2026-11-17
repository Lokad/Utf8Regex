using Lokad.Utf8Regex.Internal.Execution;
namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static partial class Utf8FiniteLiteralLowerer
{
    private static bool TryLowerBranches(Utf8ConstructionSession storage, Utf8TreeNode node,
        Utf8PreparationBudget budget, int depth, out Utf8ConstructionBuffer<LoweredBranch> branches)
        => BranchLowerer.Create(budget, storage).TryLower(node, depth, out branches);

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
        if (!TryReserveBranchCells(budget, right.Tokens.Count)) return false;
        left.Tokens.AddRange(right.Tokens.Span);
        return true;
    }

    private static bool TryCombineBranches(Utf8ConstructionSession storage, LoweredBranch left, LoweredBranch right, Utf8PreparationBudget budget, out LoweredBranch branch)
    {
        branch = default;
        if (!TryReserveBranchCells(budget, (long)left.Tokens.Count + right.Tokens.Count)) return false;
        var tokens = storage.Buffer<AsciiPatternToken>();
        tokens.AddRange(left.Tokens.Span);
        tokens.AddRange(right.Tokens.Span);
        branch = new(tokens);
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
