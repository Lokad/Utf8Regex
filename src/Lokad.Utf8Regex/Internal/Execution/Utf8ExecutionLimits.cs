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

    // Explicit no-cap value. Public finite policies supply their own limits;
    // using this member does not inherit limits from an enclosing invocation.
    internal static readonly Utf8ExecutionLimits Unlimited = new(0, 0, 0);
}
