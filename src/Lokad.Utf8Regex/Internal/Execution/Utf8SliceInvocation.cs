using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Invocation-owned state bundle for one owned-slice run: step/depth
/// budget with deadline and token, aggregate storage admission ledger, and an
/// optional deterministic checkpoint hook (test-only cancellation injection).
/// Metered work counters have one shared owner, including after transfer into
/// a copied value cursor. Positions and anchor origins stay cursor-local.
/// Storage ledgers must still be held by reference: their leases have not yet
/// acquired shared copy/abandon ownership.</summary>
internal struct Utf8SliceInvocation
{
    private Utf8ExecutionBudget _inlineBudget;
    private readonly SharedWorkBudget? _sharedWork;

    internal Utf8SliceInvocation(Utf8ExecutionLimits limits, TimeSpan timeout, CancellationToken token)
        : this(limits, Utf8ExecutionDeadline.Start(timeout), token)
    {
    }

    internal Utf8SliceInvocation(Utf8ExecutionLimits limits, Utf8ExecutionDeadline deadline, CancellationToken token)
    {
        _inlineBudget = new Utf8ExecutionBudget(limits, deadline, token);
        Ledger = new Utf8StorageLedger(limits.MaxWorkingBytes);
        CheckpointHook = Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook;
        AnchorOriginByte = -1;
        // Create the owner at invocation entry, before validation/preparation.
        // Sharing only when the cursor is constructed would lose later entry
        // checks and would fork a scope passed by value to a nested operation.
        // Infinite, None-token calls with no hook keep their allocation-free
        // inline path. The fixed accounting metadata is not storage payload;
        // complete object-overhead admission remains a separate policy gate.
        _sharedWork = !_inlineBudget.IsUnmetered || CheckpointHook is not null
            ? new SharedWorkBudget(_inlineBudget)
            : null;
    }

    [UnscopedRef]
    internal ref Utf8ExecutionBudget Budget
    {
        get
        {
            if (_sharedWork is not null) return ref _sharedWork.Budget;
            return ref _inlineBudget;
        }
    }

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

    // One synchronous invocation owns these mutable counters. Separate calls
    // construct separate owners; this is not a caller-owned/concurrent meter.
    private sealed class SharedWorkBudget(Utf8ExecutionBudget budget)
    {
        internal Utf8ExecutionBudget Budget = budget;
    }
}
