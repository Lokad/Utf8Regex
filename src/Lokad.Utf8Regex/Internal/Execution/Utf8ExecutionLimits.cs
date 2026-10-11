namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral execution budgets for owned runners. Zero disables each limit.
/// Units are work steps, frame depth, accounted working bytes and cumulative
/// modeled result/staging capacity; the deadline and
/// cancellation token travel with the same invocation state (see Utf8ExecutionBudget).
/// </summary>
internal readonly record struct Utf8ExecutionLimits(
    ulong MaxWorkSteps,
    uint MaxDepth,
    ulong MaxWorkingBytes,
    ulong MaxResultBytes)
{
    internal Utf8ExecutionLimits(ulong maxWorkSteps, uint maxDepth, ulong maxWorkingBytes)
        : this(maxWorkSteps, maxDepth, maxWorkingBytes, 0)
    { }

    // R02b budgeted-invocation placeholder: profiles whose policy
    // constructors still reject finite budgets build every slice invocation
    // from this unbounded value. Profiles with real request limits build
    // their own values instead; no caller may assume this member means
    // anything other than no-cap.
    internal static readonly Utf8ExecutionLimits Unlimited = new(0, 0, 0);
}
