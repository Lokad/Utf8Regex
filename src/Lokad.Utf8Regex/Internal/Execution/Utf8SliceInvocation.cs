using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Invocation-owned state bundle for one owned-slice run: step/depth
/// budget with deadline and token, aggregate storage admission ledger, and an
/// optional deterministic checkpoint hook (test-only cancellation injection).
/// Hold by reference, never copy mid-operation.</summary>
internal struct Utf8SliceInvocation
{
    internal Utf8SliceInvocation(Utf8ExecutionLimits limits, TimeSpan timeout, CancellationToken token)
        : this(limits, Utf8ExecutionDeadline.Start(timeout), token)
    {
    }

    internal Utf8SliceInvocation(Utf8ExecutionLimits limits, Utf8ExecutionDeadline deadline, CancellationToken token)
    {
        Budget = new Utf8ExecutionBudget(limits, deadline, token);
        Ledger = new Utf8StorageLedger(limits.MaxWorkingBytes);
        CheckpointHook = Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook;
        AnchorOriginByte = -1;
    }

    internal Utf8ExecutionBudget Budget;
    internal Utf8StorageLedger Ledger;
    internal Action<ulong>? CheckpointHook;
    // Contiguous-anchor origin in bytes (-1 selects the attempt start): set anew
    // by every scan driver, so nested lookbehind runs read the outer origin.
    internal int AnchorOriginByte;

    internal bool IsUnmetered => Budget.IsUnmetered && Ledger.IsUnlimited;

    internal Utf8ExecutionLimitKind TryChargeStep()
    {
        var breach = Budget.TryChargeStep();
        if (breach == Utf8ExecutionLimitKind.None && CheckpointHook is not null && (Budget.Steps & 63) == 0)
        {
            CheckpointHook(Budget.Steps);
        }
        return breach;
    }

    internal Utf8ExecutionLimitKind TryChargeInputStep()
    {
        var breach = TryChargeStep();
        Budget.CheckDeadline();
        return breach;
    }
}
