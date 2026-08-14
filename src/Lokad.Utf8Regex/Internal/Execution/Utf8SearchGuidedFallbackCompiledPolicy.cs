using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchGuidedFallbackCompiledPolicy
{
    private const int DirectFallbackVerifierThreshold = 8;
    private const int DirectFallbackInvocationThreshold = 32;

    public static bool ShouldBypassIsMatch(Utf8PreparedRegex regexPlan)
    {
        return string.Equals(regexPlan.FallbackReason, "unsupported_conditional", StringComparison.Ordinal);
    }

    public static bool CanUseEmittedBackend(Utf8EmittedSearchGuidedFallback? emittedBackend, Utf8ExecutionDeadline budget)
    {
        return budget.IsInfinite && emittedBackend is not null;
    }

    public static bool ShouldDemoteToFallbackCount(int verifierCount)
    {
        return verifierCount >= DirectFallbackVerifierThreshold;
    }

    public static bool ShouldDemoteToFallbackCountByInvocations(int verifierInvocations)
    {
        return verifierInvocations >= DirectFallbackInvocationThreshold;
    }
}
