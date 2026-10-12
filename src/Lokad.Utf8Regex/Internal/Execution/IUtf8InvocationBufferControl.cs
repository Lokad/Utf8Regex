namespace Lokad.Utf8Regex.Internal.Execution;

// PCRE2-INTEGRATION-POINT: shared working buffers borrow the existing profile
// authority. Implementations hold references to counters; they never fork them.
internal interface IUtf8InvocationBufferControl
{
    bool CanRetainWorkspace { get; }
    Utf8ExecutionLimitKind TryReserveWorking(ulong bytes);
    void ReleaseWorking(ulong bytes);
    Utf8ExecutionLimitKind TryChargeWorkspaceStep();
    Utf8ExecutionLimitKind TryChargeResultBytes(ulong bytes);
}

internal readonly ref struct Utf8InvocationBufferControl(ref Utf8SliceInvocation invocation) : IUtf8InvocationBufferControl
{
    private readonly ref Utf8SliceInvocation _invocation = ref invocation;
    public bool CanRetainWorkspace => _invocation.Ledger.IsUnlimited;

    public Utf8ExecutionLimitKind TryReserveWorking(ulong bytes)
        => _invocation.TryReserveWorking(bytes);

    public void ReleaseWorking(ulong bytes) => _invocation.Ledger.Release(bytes);
    // Core result producers already charge their major extents at the call
    // sites; shared buffer helpers must not charge them a second time.
    public Utf8ExecutionLimitKind TryChargeResultBytes(ulong bytes) => Utf8ExecutionLimitKind.None;
    public Utf8ExecutionLimitKind TryChargeWorkspaceStep()
    {
        var breach = _invocation.TryChargeInputStep();
        if (breach == Utf8ExecutionLimitKind.None) _invocation.Budget.ThrowIfCancellationRequested();
        return breach;
    }
}
