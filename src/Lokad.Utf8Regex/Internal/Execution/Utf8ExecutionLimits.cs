namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>
/// Flavor-neutral execution budgets for owned runners. Zero disables each limit.
/// Units are frame depth, accounted working bytes and cumulative
/// modeled result/staging capacity; the deadline and
/// cancellation token travel with the same invocation state (see Utf8ExecutionBudget).
/// </summary>
internal readonly record struct Utf8ExecutionLimits(
    uint MaxDepth,
    ulong MaxWorkingBytes,
    ulong MaxResultBytes = 0)
{
    internal Utf8ExecutionLimits(Utf8ResourcePolicy validatedPolicy)
        : this(validatedPolicy.MaxDepth ?? 0,
            validatedPolicy.MaxWorkingBytes ?? 0, validatedPolicy.MaxResultBytes ?? 0)
    { }

    // Public admission rejects explicit zero budgets, so zero round-trips as null.
    internal Utf8ResourcePolicy ToResourcePolicy() => new()
    {
        MaxDepth = MaxDepth == 0 ? null : MaxDepth,
        MaxWorkingBytes = MaxWorkingBytes == 0 ? null : MaxWorkingBytes,
        MaxResultBytes = MaxResultBytes == 0 ? null : MaxResultBytes
    };

    // Explicit no-cap value. Public finite policies supply their own limits;
    // using this member does not inherit limits from an enclosing invocation.
    internal static readonly Utf8ExecutionLimits Unlimited = new(0, 0);
}
