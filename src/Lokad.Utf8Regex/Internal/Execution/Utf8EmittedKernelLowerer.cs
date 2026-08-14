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
            NativeExecutionKind.AsciiOrderedLiteralWindow
                => TryLower(regexPlan.StructuralLinearProgram.OrderedLiteralWindowPlan, out kernelPlan),
            _ => ReturnDefault(out kernelPlan),
        };
    }

    public static bool TryLower(
        in AsciiStructuralIdentifierFamilyPlan familyPlan,
        Utf8SearchPlan searchPlan,
        out Utf8EmittedKernelPlan kernelPlan)
    {
        if (Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetUpperWordIdentifierKernelSpec(familyPlan, out var anchorOffset, out var anchorBytes))
        {
            var anchorPrefixesByByte = new byte[256][];
            foreach (var prefix in familyPlan.Prefixes)
            {
                anchorPrefixesByByte[prefix[anchorOffset]] = prefix;
            }

            kernelPlan = Utf8EmittedKernelPlan.CreateUpperWordIdentifierFamily(
                new Utf8CompiledFindOptimization(anchorOffset, anchorBytes, anchorPrefixesByByte),
                familyPlan.Prefixes,
                [
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.FindAnchorSet),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.DispatchPrefixesAtAnchor),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.ConsumeAsciiWhitespace),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.RequireAsciiUpper),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.ConsumeAsciiWordTail),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.AcceptAndAdvance),
                ]);
            return true;
        }

        if ((Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetSharedPrefixSuffixKernelSpec(familyPlan, searchPlan, out var bucket, out var requiredSuffixByte) &&
             bucket.PrefixDiscriminator.HasValue &&
             bucket.PrefixDiscriminator.HasValue) ||
            Utf8AsciiStructuralIdentifierFamilyExecutor.TryGetSharedPrefixSuffixLiteralFamilyKernelSpec(familyPlan, searchPlan, out bucket, out requiredSuffixByte))
        {
            kernelPlan = Utf8EmittedKernelPlan.CreateSharedPrefixAsciiWhitespaceSuffix(
                new Utf8CompiledFindOptimization(bucket.CommonPrefix, bucket.PrefixDiscriminator),
                bucket.Literals,
                requiredSuffixByte,
                [
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.FindCommonPrefix),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.MatchSharedPrefixSuffix),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.AcceptAndAdvance),
                ]);
            return true;
        }

        kernelPlan = default;
        return false;
    }

    public static bool TryLower(
        AsciiOrderedLiteralWindowPlan plan,
        out Utf8EmittedKernelPlan kernelPlan)
    {
        if (!plan.IsLiteralFamily &&
            !plan.YieldLeadingLiteralOnly &&
            plan.GapLeadingSeparatorMinCount > 0 &&
            plan.LeadingLiteralLeadingBoundary is Utf8BoundaryRequirement.None or Utf8BoundaryRequirement.Boundary &&
            plan.LeadingLiteralTrailingBoundary is Utf8BoundaryRequirement.None or Utf8BoundaryRequirement.Boundary &&
            plan.TrailingLiteralLeadingBoundary is Utf8BoundaryRequirement.None or Utf8BoundaryRequirement.Boundary &&
            plan.TrailingLiteralTrailingBoundary is Utf8BoundaryRequirement.None or Utf8BoundaryRequirement.Boundary)
        {
            kernelPlan = Utf8EmittedKernelPlan.CreateOrderedAsciiWhitespaceLiteralWindow(
                new Utf8CompiledFindOptimization(plan.TrailingLiteralUtf8, default),
                [plan.LeadingLiteralUtf8],
                plan.GapLeadingSeparatorMinCount,
                plan.MaxGap,
                plan.GapSameLine,
                plan.LeadingLiteralLeadingBoundary,
                plan.LeadingLiteralTrailingBoundary,
                plan.TrailingLiteralLeadingBoundary,
                plan.TrailingLiteralTrailingBoundary,
                [
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.FindTrailingLiteral),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.ConsumeReverseAsciiWhitespace),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.MatchLeadingLiteralBeforeSeparator),
                    new Utf8EmittedKernelBlock(Utf8EmittedKernelBlockKind.AcceptAndAdvance),
                ]);
            return true;
        }

        kernelPlan = default;
        return false;
    }

    private static bool ReturnDefault(out Utf8EmittedKernelPlan kernelPlan)
    {
        kernelPlan = default;
        return false;
    }
}
