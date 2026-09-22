namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral execution budgets for owned runners. Zero disables each limit.
/// Units are work steps, frame depth, and accounted working bytes; the deadline and
/// cancellation token travel with the same invocation state (see Utf8ExecutionBudget).
/// </summary>
internal readonly record struct Utf8ExecutionLimits(
    ulong MaxWorkSteps,
    uint MaxDepth,
    ulong MaxWorkingBytes);
