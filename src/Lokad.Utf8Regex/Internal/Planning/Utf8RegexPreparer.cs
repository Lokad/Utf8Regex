using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Planning;

internal static class Utf8RegexPreparer
{
    internal static Utf8PreparedRegex Prepare(in Utf8RegexAnalysis analyzedRegex, RegexOptions scalarOptions,
        Utf8BoundedProgramEmitter.Compilation proofCompilation = default)
    {
        using var scope = Utf8ConstructionScope.Enter();
        var budget = scope.Budget;
        budget.EnterDepth();
        try
        {
            var scalarProgram = Utf8SliceParser.TryParse(scalarOptions,
                analyzedRegex.SemanticRegex.RuntimeTree, proofCompilation);
            var anchoredLiteral = Utf8AnchoredLiteralClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
            var searchPlan = Utf8SearchPlan.Prepare(analyzedRegex.SearchFacts, scalarProgram);
            var result = Utf8PreparedRegex.Create(
                new Utf8PreparedSemanticRegex(analyzedRegex.SemanticRegex.Pattern, analyzedRegex.SemanticRegex.Options,
                    analyzedRegex.SemanticRegex.ExecutionPatternOffset, analyzedRegex.SemanticRegex.ExecutionOptions,
                    analyzedRegex.SemanticRegex.RuntimeTree?.CaptureCatalog),
                analyzedRegex.Features,
                analyzedRegex.ExecutionKind,
                scalarProgram,
                searchPlan,
                analyzedRegex.LiteralUtf8,
                anchoredLiteral,
                budget);
            scope.Complete();
            return result;
        }
        finally { budget.LeaveDepth(); }
    }
}
