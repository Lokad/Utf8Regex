using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Internal.FrontEnd;

internal static class Utf8FallbackRegexFamilyAnalyzer
{
    public static Utf8FallbackDirectFamilyPlan Classify(Utf8RegexAnalysis analyzedRegex)
    {
        var plan = Utf8FallbackFinalProfileClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
        if (plan.HasValue) return plan;
        plan = Utf8FallbackQueryClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
        if (plan.HasValue) return plan;
        plan = Utf8FallbackLineClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
        if (plan.HasValue) return plan;
        plan = Utf8FallbackAnchoredProfileClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
        if (plan.HasValue) return plan;
        plan = Utf8FallbackProfileClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
        if (plan.HasValue) return plan;
        plan = Utf8FallbackLiteralClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
        if (plan.HasValue) return plan;
        plan = Utf8FallbackClassClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
        return plan.HasValue ? plan : Utf8FallbackTokenClassifier.Classify(analyzedRegex.SemanticRegex.RuntimeTree);
    }

}
