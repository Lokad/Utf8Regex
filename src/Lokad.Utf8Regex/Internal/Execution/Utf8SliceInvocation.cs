using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Invocation-owned state bundle for one owned-slice run: step/depth
/// budget with deadline and token, aggregate storage admission ledger, and an
/// optional deterministic checkpoint hook (test-only cancellation injection).
/// Hold by reference, never copy mid-operation.</summary>
internal struct Utf8SliceInvocation
{
    internal Utf8SliceInvocation(Utf8ExecutionLimits limits, TimeSpan timeout, CancellationToken token)
    {
        Budget = new Utf8ExecutionBudget(limits, timeout, token);
        Ledger = new Utf8StorageLedger(limits.MaxWorkingBytes);
        CheckpointHook = null;
        AnchorOriginByte = -1;
    }

    internal Utf8ExecutionBudget Budget;
    internal Utf8StorageLedger Ledger;
    internal Action<ulong>? CheckpointHook;
    // Contiguous-anchor origin in bytes (-1 selects the attempt start): set anew
    // by every scan driver, so nested lookbehind runs read the outer origin.
    internal int AnchorOriginByte;

    internal bool IsUnmetered => Budget.IsUnmetered && Ledger.IsUnlimited;
}
