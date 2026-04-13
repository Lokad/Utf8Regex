using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FrontEndLowerer
{
    public static Utf8RegexPlan Lower(Utf8AnalyzedRegex analyzedRegex)
    {
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
        var structuralVerifier = Utf8StructuralVerifierPlan.Create(analyzedRegex.StructuralIdentifierFamilyPlan);
        if (!structuralVerifier.HasValue &&
            analyzedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            program is not null &&
            Utf8DeterministicAnchorSearch.CanUseForByteSafeLinear(tree, deterministicAnchor, structuralSearchPlan))
        {
            structuralVerifier = Utf8StructuralVerifierPlan.CreateByteSafe(tree, program, deterministicGuards);
        }
        var fallbackVerifier = Utf8FallbackVerifierPlan.Create(
            analyzedRegex.SemanticRegex.Pattern,
            analyzedRegex.SemanticRegex.Options,
            structuralSearchPlan);

        if (Utf8NativeTreeLowerer.TryLower(analyzedRegex, tree, program, deterministicAnchor, deterministicGuards, fallbackVerifier, out var regexPlan))
        {
            if (structuralSearchPlan.HasValue)
            {
                regexPlan = new Utf8RegexPlan(
                    new Utf8ExecutionPlan(
                        regexPlan.ExecutionPlan.ExecutionPattern,
                        regexPlan.ExecutionPlan.NativeKind,
                        regexPlan.ExecutionPlan.Tree,
                        regexPlan.ExecutionPlan.Program,
                        regexPlan.ExecutionPlan.DeterministicAnchor,
                        regexPlan.ExecutionPlan.DeterministicGuards,
                        regexPlan.ExecutionPlan.FallbackVerifier,
                        structuralVerifier,
                        regexPlan.ExecutionPlan.SimplePatternPlan,
                        regexPlan.ExecutionPlan.StructuralIdentifierFamilyPlan,
                        regexPlan.ExecutionPlan.StructuralTokenWindowPlan,
                        regexPlan.ExecutionPlan.StructuralRepeatedSegmentPlan,
                        regexPlan.ExecutionPlan.StructuralQuotedRelationPlan,
                        regexPlan.ExecutionPlan.OrderedLiteralWindowPlan,
                        regexPlan.ExecutionPlan.LiteralUtf8,
                        regexPlan.ExecutionPlan.FallbackReason,
                        regexPlan.ExecutionPlan.FallbackDirectFamily),
                    regexPlan.SearchPlan,
                    structuralSearchPlan);
            }
            return regexPlan;
        }

        return new Utf8RegexPlan(
            new Utf8ExecutionPlan(
                analyzedRegex.ExecutionPattern,
                analyzedRegex.ExecutionKind,
                tree,
                program,
                deterministicAnchor,
                deterministicGuards,
                fallbackVerifier,
                structuralVerifier,
                analyzedRegex.SimplePatternPlan,
                analyzedRegex.StructuralIdentifierFamilyPlan,
                analyzedRegex.StructuralTokenWindowPlan,
                analyzedRegex.StructuralRepeatedSegmentPlan,
                analyzedRegex.StructuralQuotedRelationPlan,
                analyzedRegex.OrderedLiteralWindowPlan,
                analyzedRegex.LiteralUtf8,
                analyzedRegex.FallbackReason,
                analyzedRegex.FallbackDirectFamily),
            searchPlan,
            structuralSearchPlan);
    }
}

