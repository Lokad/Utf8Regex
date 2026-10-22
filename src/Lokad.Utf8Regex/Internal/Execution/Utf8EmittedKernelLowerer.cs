using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8EmittedKernelLowerer
{
    public static bool TryLower(
        Utf8PreparedRegex regexPlan,
        out Utf8EmittedKernelPlan kernelPlan)
    {
        return regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.AsciiStructuralIdentifierFamily
                => TryLower(regexPlan.StructuralIdentifierFamilyPlan, regexPlan.SearchPlan, out kernelPlan),
            _ => ReturnDefault(out kernelPlan),
        };
    }

    public static bool TryLower(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        out Utf8EmittedKernelPlan kernelPlan)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            budget.ChargeWork(1);
            if (Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetUpperWordIdentifierKernelSpec(familyPlan, budget.Deadline, out var anchorOffset, out var anchorClass))
            {
                var anchorBytes = anchorClass.GetPositiveMatchBytes();
                var anchorPrefixesByByte = budget.AllocateArray<byte[]>(256, false);
                foreach (var prefix in familyPlan.Prefixes)
                {
                    budget.ChargeWork(1);
                    anchorPrefixesByByte[prefix[anchorOffset]] = prefix;
                }

                kernelPlan = Utf8EmittedKernelPlan.CreateUpperWordIdentifierFamily(
                    new Utf8CompiledFindOptimization(anchorOffset, anchorBytes, anchorPrefixesByByte),
                    familyPlan.Prefixes,
                    CreateBlocks(budget, [Utf8EmittedKernelBlockKind.FindAnchorSet, Utf8EmittedKernelBlockKind.DispatchPrefixesAtAnchor, Utf8EmittedKernelBlockKind.ConsumeAsciiWhitespace, Utf8EmittedKernelBlockKind.RequireAsciiUpper, Utf8EmittedKernelBlockKind.ConsumeAsciiWordTail, Utf8EmittedKernelBlockKind.AcceptAndAdvance]));
                scope.Complete();
                return true;
            }

            if ((Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetSharedPrefixSuffixKernelSpec(familyPlan, searchPlan, out var bucket, out var requiredSuffixByte) &&
                 bucket.PrefixDiscriminator.HasValue) ||
                Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetSharedPrefixSuffixLiteralFamilyKernelSpec(familyPlan, searchPlan, out bucket, out requiredSuffixByte))
            {
                kernelPlan = Utf8EmittedKernelPlan.CreateSharedPrefixAsciiWhitespaceSuffix(
                    new Utf8CompiledFindOptimization(bucket.CommonPrefix, bucket.PrefixDiscriminator),
                    bucket.Literals,
                    requiredSuffixByte,
                    CreateBlocks(budget, [Utf8EmittedKernelBlockKind.FindCommonPrefix, Utf8EmittedKernelBlockKind.MatchSharedPrefixSuffix, Utf8EmittedKernelBlockKind.AcceptAndAdvance]));
                scope.Complete();
                return true;
            }

            kernelPlan = default;
            return false;
        }
        finally { budget.LeaveDepth(); }
    }

    private static Utf8EmittedKernelBlock[] CreateBlocks(Utf8ConstructionBudget budget, ReadOnlySpan<Utf8EmittedKernelBlockKind> kinds)
    {
        var blocks = budget.AllocateArray<Utf8EmittedKernelBlock>(kinds.Length, false);
        for (var index = 0; index < kinds.Length; index++)
        {
            budget.ChargeWork(1);
            blocks[index] = new(kinds[index]);
        }
        return blocks;
    }

    private static bool ReturnDefault(out Utf8EmittedKernelPlan kernelPlan)
    {
        kernelPlan = default;
        return false;
    }
}
