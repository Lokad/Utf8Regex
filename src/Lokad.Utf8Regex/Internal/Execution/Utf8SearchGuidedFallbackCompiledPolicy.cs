namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8SearchGuidedFallbackCompiledPolicy
{
    public static bool CanUseEmittedBackend(Utf8EmittedSearchGuidedFallback? emittedBackend, Utf8ExecutionDeadline budget)
        => budget.IsInfinite && emittedBackend is not null;
}
