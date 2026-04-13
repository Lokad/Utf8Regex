using Lokad.Utf8Regex.Internal.Diagnostics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchGuidedFallbackCompiledPolicy
{
    private const int DirectFallbackVerifierThreshold = 8;
    private const int DirectFallbackInvocationThreshold = 32;

    public static bool ShouldBypassIsMatch(Utf8RegexPlan regexPlan)
    {
        return string.Equals(regexPlan.FallbackReason, "unsupported_conditional", StringComparison.Ordinal);
    }

    public static bool CanUseEmittedBackend(Utf8EmittedSearchGuidedFallback? emittedBackend, Utf8ExecutionBudget? budget)
    {
        return budget is null && emittedBackend is not null;
    }

    public static bool ShouldDemoteToFallbackCount(int verifierCount)
    {
        return verifierCount >= DirectFallbackVerifierThreshold;
    }

    public static bool ShouldDemoteToFallbackCountByInvocations()
    {
        return (Utf8SearchDiagnosticsSession.Current?.VerifierInvocations ?? 0) >= DirectFallbackInvocationThreshold;
    }
}
