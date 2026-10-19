using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8RegexPreparer
{
    public static Utf8PreparedRegex Prepare(Utf8RegexAnalysis analyzedRegex)
        => Prepare(analyzedRegex, analyzedRegex.SemanticRegex.Options);

    internal static Utf8PreparedRegex Prepare(Utf8RegexAnalysis analyzedRegex, RegexOptions scalarOptions)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var scalarProgram = Utf8SliceParser.TryParse(scalarOptions,
                analyzedRegex.SemanticRegex.RuntimeTree);
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
            var searchPlan = Utf8SearchPlan.Prepare(analyzedRegex.SearchFacts);
            var deterministicGuards = Utf8DeterministicVerifierGuards.Create(tree, searchPlan);
            var structuralSearchPlan = searchPlan.StructuralSearchPlan
                .WithVerificationGuards(deterministicGuards);
            if (!structuralSearchPlan.HasValue &&
                analyzedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
                searchPlan.PreparedSearcher.HasValue)
            {
                structuralSearchPlan = Utf8StructuralSearchPlan.CreateStartPlan(searchPlan.PreparedSearcher);
            }
            var structuralVerifier = PrepareStructuralVerifier(analyzedRegex.StructuralIdentifierFamilyPlan);

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
                // A linear program alone does not prove the linear search route: that route
                // declines every anchored shape, while the interpreter enforces line edges
                // per candidate. Pure line-anchored shapes without backreferences therefore
                // keep the interpreter category; absolute anchors and backreference programs
                // keep the linear category and their current routing.
                var simplePlan = analyzedRegex.SimplePatternPlan;
                var hasLineAnchor = (simplePlan.IsStartAnchored && !simplePlan.IsAbsoluteStartAnchored) ||
                    (simplePlan.IsEndAnchored && !simplePlan.IsAbsoluteEndAnchored);
                var hasAbsoluteAnchor = (simplePlan.IsStartAnchored && simplePlan.IsAbsoluteStartAnchored) ||
                    (simplePlan.IsEndAnchored && simplePlan.IsAbsoluteEndAnchored);
                if (compiledPatternCategory == Utf8CompiledPatternCategory.None && structuralLinearProgram.HasValue &&
                    !(hasLineAnchor && !hasAbsoluteAnchor && !Utf8CompiledSearchAnalysisPolicy.HasBackreferenceInstructions(program)))
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

            var operationPlans = Utf8OperationMatchCursorPlans.Create(program, searchPlan, structuralLinearProgram,
                analyzedRegex.SimplePatternPlan, structuralSearchPlan, structuralVerifier.AsciiProgram);
            var result = Utf8PreparedRegex.Create(
                new Utf8PreparedSemanticRegex(analyzedRegex.SemanticRegex.Pattern, analyzedRegex.SemanticRegex.Options,
                    analyzedRegex.SemanticRegex.ExecutionPattern, analyzedRegex.SemanticRegex.ExecutionOptions,
                    analyzedRegex.SemanticRegex.RuntimeTree?.CaptureCatalog),
                analyzedRegex.Features,
                analyzedRegex.ExecutionPattern,
                analyzedRegex.ExecutionKind,
                executionBackend,
                compiledPatternCategory,
                tree,
                program,
                scalarProgram,
                operationPlans,
                structuralSearchPlan,
                deterministicGuards,
                structuralVerifier,
                analyzedRegex.StructuralIdentifierFamilyPlan,
                analyzedRegex.StructuralTokenWindowPlan,
                analyzedRegex.StructuralRepeatedSegmentPlan,
                analyzedRegex.StructuralQuotedRelationPlan,
                analyzedRegex.OrderedLiteralWindowPlan,
                analyzedRegex.LiteralUtf8,
                fallbackReason,
                fallbackDirectFamily);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }

    private static Utf8StructuralVerifierPlan PrepareStructuralVerifier(
        AsciiStructuralIdentifierFamilyPlan structuralFamilyPlan)
    {
        return structuralFamilyPlan.VerifierProgram.HasValue
            ? new Utf8StructuralVerifierPlan(
                Utf8StructuralVerifierKind.AsciiStructuralProgram,
                structuralFamilyPlan.VerifierProgram)
            : default;
    }

}
