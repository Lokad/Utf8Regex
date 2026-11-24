using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8RegexPreparer
{
    internal static Utf8PreparedRegex Prepare(Utf8RegexAnalysis analyzedRegex, RegexOptions scalarOptions,
        bool enableProofTierUp = false)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var scalarProgram = Utf8SliceParser.TryParse(scalarOptions,
                analyzedRegex.SemanticRegex.RuntimeTree, enableProofTierUp);
            var anchoredLiteral = Utf8AnchoredLiteralClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
            var fallbackReason = analyzedRegex.FallbackReason;
            if (analyzedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex &&
                string.Equals(fallbackReason, "unsupported_loop", StringComparison.Ordinal) &&
                anchoredLiteral.HasValue && anchoredLiteral.IsExact)
                fallbackReason = null;

            var searchPlan = Utf8SearchPlan.Prepare(analyzedRegex.SearchFacts);
            var operationPlans = Utf8OperationMatchCursorPlans.Create(searchPlan);
            var result = Utf8PreparedRegex.Create(
                new Utf8PreparedSemanticRegex(analyzedRegex.SemanticRegex.Pattern, analyzedRegex.SemanticRegex.Options,
                    analyzedRegex.SemanticRegex.ExecutionPattern, analyzedRegex.SemanticRegex.ExecutionOptions,
                    analyzedRegex.SemanticRegex.RuntimeTree?.CaptureCatalog),
                analyzedRegex.Features,
                analyzedRegex.ExecutionPattern,
                analyzedRegex.ExecutionKind,
                scalarProgram,
                operationPlans,
                analyzedRegex.LiteralUtf8,
                fallbackReason,
                anchoredLiteral);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
