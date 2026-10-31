using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

/// <summary>Invocation-owned state bundle for one owned-slice run: step/depth
/// budget with deadline and token, aggregate storage admission ledger, and an
/// optional deterministic checkpoint hook (test-only cancellation injection).
/// Metered work counters have one shared owner, including after transfer into
/// a copied value cursor. Positions and anchor origins stay cursor-local.
/// Controlled scopes also share one storage ledger. Forkable cursors promote
/// even unlimited scopes before publishing invocation-long payload reservations.</summary>
internal struct Utf8SliceInvocation
{
    private Utf8ExecutionBudget _inlineBudget;
    private Utf8StorageLedger _inlineLedger;
    private SharedInvocationState? _sharedWork;

    internal Utf8SliceInvocation(Utf8ExecutionLimits limits, TimeSpan timeout, CancellationToken token)
        : this(limits, Utf8ExecutionDeadline.Start(timeout), token)
    {
    }

    internal Utf8SliceInvocation(Utf8ExecutionLimits limits, TimeSpan timeout, CancellationToken token,
        IUtf8MemoryAllowance memoryAllowance)
        : this(limits, Utf8ExecutionDeadline.Start(timeout), token, memoryAllowance)
    {
    }

    internal Utf8SliceInvocation(Utf8ExecutionLimits limits, Utf8ExecutionDeadline deadline, CancellationToken token)
        : this(limits, deadline, token, null)
    {
    }

    private Utf8SliceInvocation(Utf8ExecutionLimits limits, Utf8ExecutionDeadline deadline, CancellationToken token,
        IUtf8MemoryAllowance? memoryAllowance)
    {
        _inlineBudget = new Utf8ExecutionBudget(limits, deadline, token);
        _inlineLedger = new Utf8StorageLedger(limits.MaxWorkingBytes, memoryAllowance);
        CheckpointHook = Lokad.Utf8Regex.Internal.Diagnostics.Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook;
        AnchorOriginByte = -1;
        // Create the owner at invocation entry, before validation/preparation.
        // Sharing only when the cursor is constructed would lose later entry
        // checks and would fork a scope passed by value to a nested operation.
        // Infinite, None-token calls with no hook keep their allocation-free
        // inline path. The fixed accounting metadata is not storage payload;
        // complete object-overhead admission remains a separate policy gate.
        _sharedWork = !_inlineBudget.IsUnmetered || memoryAllowance is not null || CheckpointHook is not null
            ? new SharedInvocationState(_inlineBudget, _inlineLedger)
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

    [UnscopedRef]
    internal ref Utf8StorageLedger Ledger
    {
        get
        {
            if (_sharedWork is not null) return ref _sharedWork.Ledger;
            return ref _inlineLedger;
        }
    }

    internal void ShareStorageForCursor()
    {
        _sharedWork ??= new SharedInvocationState(_inlineBudget, _inlineLedger);
    }
    internal Action<ulong>? CheckpointHook;
    // Contiguous-anchor origin in bytes (-1 selects the attempt start): set anew
    // by every scan driver, so nested lookbehind runs read the outer origin.
    internal int AnchorOriginByte;

    internal bool IsUnmetered => Budget.IsUnmetered && Ledger.IsUnlimited;

    // A funded synchronous preparation borrows the same numeric/host ledger as
    // execution. Its construction scope owns/refunds its temporary loans; no
    // second account or release/reacquire transfer is needed between phases.
    internal IUtf8MemoryAllowance BorrowWorkingMemoryAllowance()
        => _sharedWork ?? throw new InvalidOperationException("Working admission requires a shared invocation.");

    // One checked admission for major capacity, including old/new overlap.
    // A denied request updates the existing peak/first-failure authority but
    // never changes outstanding bytes or acquires the requested payload.
    internal Utf8ExecutionLimitKind TryReserveWorking(ulong bytes)
    {
        Budget.ThrowIfCancellationRequested();
        Budget.CheckDeadline();
        ref var budget = ref Budget;
        if (budget.LimitBreach != Utf8ExecutionLimitKind.None) return budget.LimitBreach;
        ref var ledger = ref Ledger;
        var held = ledger.ReservedBytes;
        var requested = bytes > ulong.MaxValue - held ? ulong.MaxValue : held + bytes;
        var breach = budget.TryNoteWorkingBytes(requested);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        return ledger.TryReserve(bytes) ? Utf8ExecutionLimitKind.None : budget.DenyWorkingBytes(requested);
    }

    internal Utf8ExecutionLimitKind TryChargeStep()
    {
        // Borrow only until invoking the hook; later checks select the current owner again.
        ref var budget = ref Budget;
        var breach = budget.TryChargeStep();
        if (breach == Utf8ExecutionLimitKind.None && CheckpointHook is not null && (budget.Steps & 63) == 0)
        {
            CheckpointHook(budget.Steps);
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
    private sealed class SharedInvocationState(Utf8ExecutionBudget budget, Utf8StorageLedger ledger) : IUtf8MemoryAllowance
    {
        internal Utf8ExecutionBudget Budget = budget;
        internal Utf8StorageLedger Ledger = ledger;

        public bool TryReserve(ulong bytes)
        {
            // This temporary view shares counters; it does not copy their state.
            var invocation = new Utf8SliceInvocation { _sharedWork = this };
            if (invocation.TryReserveWorking(bytes) != Utf8ExecutionLimitKind.None)
                throw Budget.CreateResourceFailure();
            return true;
        }

        public void Release(ulong bytes) => Ledger.Release(bytes);
    }
}
