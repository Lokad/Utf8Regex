using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8RegexPreparer
{
    public static Utf8PreparedRegex Prepare(Utf8RegexAnalysis analyzedRegex)
    {
        var fallbackDirectFamily = Utf8FallbackRegexFamilyAnalyzer.Classify(analyzedRegex);
        var fallbackReason = analyzedRegex.FallbackReason;
        if (analyzedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            string.Equals(fallbackReason, "unsupported_loop", StringComparison.Ordinal) &&
            fallbackDirectFamily.HasValue &&
            fallbackDirectFamily.SupportsNativeFallbackRoute)
        {
            fallbackReason = null;
        }

        var tree = Utf8ExecutionTreeLowerer.Lower(analyzedRegex.SemanticRegex);
        var program = Utf8ExecutionProgramLowerer.Lower(tree);
        var searchPlan = new Utf8SearchPlan(
            analyzedRegex.SearchInfo.Kind,
            analyzedRegex.SearchInfo.LiteralUtf8,
            analyzedRegex.SearchInfo.AlternateLiteralsUtf8,
            analyzedRegex.SearchInfo.CanGuideFallbackStarts,
            analyzedRegex.SearchInfo.RequiredPrefilterLiteralUtf8,
            analyzedRegex.SearchInfo.RequiredPrefilterAlternateLiteralsUtf8,
            analyzedRegex.SearchInfo.SecondaryRequiredPrefilterQuotedAsciiSet,
            analyzedRegex.SearchInfo.SecondaryRequiredPrefilterQuotedAsciiLength,
            analyzedRegex.SearchInfo.FixedDistanceSets,
            analyzedRegex.SearchInfo.TrailingLiteralUtf8,
            analyzedRegex.SearchInfo.OrderedWindowLeadingLiteralsUtf8,
            analyzedRegex.SearchInfo.OrderedWindowTrailingLiteralUtf8,
            analyzedRegex.SearchInfo.RequiredWindowPrefilters,
            analyzedRegex.SearchInfo.OrderedWindowMaxGap,
            analyzedRegex.SearchInfo.OrderedWindowSameLine,
            analyzedRegex.SearchInfo.FallbackStartTransform,
            analyzedRegex.SearchInfo.Distance,
            analyzedRegex.SearchInfo.MinRequiredLength,
            analyzedRegex.SearchInfo.ExactRequiredLength,
            analyzedRegex.SearchInfo.MaxPossibleLength,
            analyzedRegex.SearchInfo.LeadingBoundary,
            analyzedRegex.SearchInfo.TrailingBoundary);
        var deterministicAnchor = Utf8DeterministicAnchorSearch.Create(tree, searchPlan);
        var deterministicGuards = Utf8DeterministicVerifierGuards.Create(tree, searchPlan);
        var structuralSearchPlan = searchPlan.StructuralSearchPlan
            .WithPrefixGuards(deterministicGuards.PrefixGuards)
            .WithFixedLiteral(deterministicGuards.FixedLiteralUtf8, deterministicGuards.FixedLiteralOffset)
            .WithFixedSets(deterministicGuards.FixedDistanceSets)
            .WithMinLength(deterministicGuards.MinRequiredLength);
        if (!structuralSearchPlan.HasValue &&
            analyzedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
            searchPlan.PreparedSearcher.HasValue)
        {
            structuralSearchPlan = Utf8StructuralSearchPlan.CreateStartPlan(searchPlan.PreparedSearcher);
        }
        var structuralVerifier = PrepareStructuralVerifier(analyzedRegex.StructuralIdentifierFamilyPlan);
        if (!structuralVerifier.HasValue &&
            analyzedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            program is not null &&
            Utf8DeterministicAnchorSearch.CanUseForByteSafeLinear(tree, deterministicAnchor, structuralSearchPlan))
        {
            structuralVerifier = PrepareByteSafeStructuralVerifier(tree, program, deterministicGuards);
        }
        var fallbackVerifier = PrepareFallbackVerifier(
            analyzedRegex.SemanticRegex.Pattern,
            analyzedRegex.SemanticRegex.Options,
            structuralSearchPlan);

        var structuralLinearProgram = Utf8StructuralLinearProgram.Create(
            analyzedRegex.ExecutionKind,
            analyzedRegex.SimplePatternPlan,
            analyzedRegex.StructuralIdentifierFamilyPlan,
            analyzedRegex.StructuralTokenWindowPlan,
            analyzedRegex.StructuralRepeatedSegmentPlan,
            analyzedRegex.StructuralQuotedRelationPlan,
            analyzedRegex.OrderedLiteralWindowPlan,
            structuralVerifier,
            searchPlan,
            structuralSearchPlan);
        var compiledPatternCategory = Utf8CompiledPatternCategories.GetNativeCategory(analyzedRegex.ExecutionKind);
        if (compiledPatternCategory == Utf8CompiledPatternCategory.None &&
            analyzedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern)
        {
            compiledPatternCategory = analyzedRegex.SimplePatternPlan.CompiledPatternCategory;
            if (compiledPatternCategory == Utf8CompiledPatternCategory.None && structuralLinearProgram.HasValue)
            {
                compiledPatternCategory = Utf8CompiledPatternCategory.DeterministicLinear;
            }
        }

        var executionBackend = analyzedRegex.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.AsciiLiteralIgnoreCase or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.ExactUtf8Literals or
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals => Utf8ExecutionBackend.NativeLiteral,
            NativeExecutionKind.FallbackRegex => Utf8ExecutionBackend.FallbackRegex,
            _ => Utf8ExecutionBackend.NativeSimplePattern,
        };

        return new Utf8PreparedRegex(
            analyzedRegex.SemanticRegex,
            analyzedRegex.Features,
            analyzedRegex.SearchInfo,
            analyzedRegex.ExecutionPattern,
            analyzedRegex.ExecutionKind,
            executionBackend,
            compiledPatternCategory,
            tree,
            program,
            searchPlan,
            structuralSearchPlan,
            deterministicAnchor,
            deterministicGuards,
            fallbackVerifier,
            structuralVerifier,
            structuralLinearProgram,
            analyzedRegex.SimplePatternPlan,
            analyzedRegex.StructuralIdentifierFamilyPlan,
            analyzedRegex.StructuralTokenWindowPlan,
            analyzedRegex.StructuralRepeatedSegmentPlan,
            analyzedRegex.StructuralQuotedRelationPlan,
            analyzedRegex.OrderedLiteralWindowPlan,
            analyzedRegex.LiteralUtf8,
            fallbackReason,
            fallbackDirectFamily);
    }

    private static Utf8StructuralVerifierPlan PrepareStructuralVerifier(
        AsciiStructuralIdentifierFamilyPlan structuralFamilyPlan)
    {
        return structuralFamilyPlan.VerifierProgram.HasValue
            ? new Utf8StructuralVerifierPlan(
                Utf8StructuralVerifierKind.AsciiStructuralProgram,
                structuralFamilyPlan.VerifierProgram,
                default,
                default,
                null,
                default)
            : default;
    }

    internal static Utf8StructuralVerifierPlan PrepareByteSafeStructuralVerifier(
        Utf8ExecutionTree? tree,
        Utf8ExecutionProgram program,
        Utf8DeterministicVerifierGuards guards)
    {
        var linearOutcome = Utf8ByteSafeLinearVerifierProgram.Compile(tree);
        var lazyDfaOutcome = Utf8ByteSafeLazyDfaVerifierProgram.Compile(linearOutcome.Program);
        return new Utf8StructuralVerifierPlan(
            lazyDfaOutcome.Succeeded
                ? Utf8StructuralVerifierKind.ByteSafeLazyDfaProgram
                : Utf8StructuralVerifierKind.ByteSafeLinearProgram,
            default,
            linearOutcome,
            lazyDfaOutcome,
            program,
            guards);
    }

    internal static Utf8FallbackVerifierPlan PrepareFallbackVerifier(
        string pattern,
        RegexOptions options,
        Utf8StructuralSearchPlan structuralSearchPlan)
    {
        var canUseAnchoredRegex = CanUseAnchoredRegex(pattern, options);
        var mode = structuralSearchPlan.ProducesBoundedCandidates && canUseAnchoredRegex
            ? Utf8FallbackVerifierMode.AnchoredSliceRegex
            : canUseAnchoredRegex
                ? Utf8FallbackVerifierMode.AnchoredRegex
                : Utf8FallbackVerifierMode.DirectRegex;
        return new Utf8FallbackVerifierPlan(
            mode,
            structuralSearchPlan.RequiresCandidateEndCoverage,
            structuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Window);
    }

    private static bool CanUseAnchoredRegex(string pattern, RegexOptions options)
    {
        if ((options & (RegexOptions.RightToLeft | RegexOptions.NonBacktracking)) != 0)
        {
            return false;
        }

        // Wrapping the pattern changes semantics for leading global inline constructs.
        return !pattern.StartsWith("(?", StringComparison.Ordinal);
    }
}
