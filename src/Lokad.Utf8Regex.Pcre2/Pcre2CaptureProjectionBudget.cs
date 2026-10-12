using System.Runtime.CompilerServices;
using Lokad.Utf8Regex.Internal.Execution;

namespace Lokad.Utf8Regex.Pcre2;

// Stack-only borrowed authority for the two existing PCRE2 execution owners.
// The projector never copies their counters or creates a second invocation.
// Copies of this readonly view still reference the same mutable owners.
internal readonly ref struct Pcre2CaptureProjectionBudget : IUtf8InvocationBufferControl
{
    private readonly ref Pcre2ResourceBudget _vm;
    private readonly ref Utf8SliceInvocation _slice;
    private readonly bool _isSlice;

    internal Pcre2CaptureProjectionBudget(ref Pcre2ResourceBudget budget)
    {
        _vm = ref budget;
        _slice = ref Unsafe.NullRef<Utf8SliceInvocation>();
        _isSlice = false;
    }

    internal Pcre2CaptureProjectionBudget(ref Utf8SliceInvocation invocation)
    {
        _vm = ref Unsafe.NullRef<Pcre2ResourceBudget>();
        _slice = ref invocation;
        _isSlice = true;
    }

    internal bool IsUnmetered => _isSlice
        ? _slice.IsUnmetered && _slice.CheckpointHook is null
        : _vm.IsUnmetered;

    public bool CanRetainWorkspace => _isSlice ? _slice.Ledger.IsUnlimited : _vm.CanRetainWorkspace;

    internal ref Utf8SliceInvocation WorkingInvocation => ref (_isSlice ? ref _slice : ref _vm.WorkingInvocation);

    // The byte output and range staging are still live at this call. Their
    // owner admits the final string and retains spent discovery/shaping work.
    internal string DecodeReplacementText(ReadOnlySpan<byte> bytes)
    {
        ref var invocation = ref (_isSlice ? ref _slice : ref _vm.WorkingInvocation);
        try
        {
            Pcre2ReplacementPreparationControl.Check();
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            Utf8SliceBufferWork.ValidateUtf8(bytes, ref invocation);
            return Utf8SliceBufferWork.DecodeUtf8(bytes, ref invocation);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw new Pcre2MatchException("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);
        }
        catch (InvalidOperationException) when (invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(invocation.Budget.LimitBreach);
            throw;
        }
    }

    internal void ReserveWorking(ulong bytes)
    {
        if (!_isSlice) { _vm.ReserveWorking(bytes); return; }
        Pcre2SliceDriver.ThrowIfSliceBreached(_slice.TryReserveWorking(bytes));
    }

    public Utf8ExecutionLimitKind TryChargeResultBytes(ulong bytes)
    {
        Pcre2SliceDriver.ThrowIfSliceBreached(WorkingInvocation.Budget.TryChargeResultBytes(bytes));
        return Utf8ExecutionLimitKind.None;
    }

    public void ReleaseWorking(ulong bytes)
    {
        if (_isSlice) _slice.Ledger.Release(bytes);
        else _vm.ReleaseWorking(bytes);
    }

    internal void ChargeWorkspaceStep()
    {
        if (!_isSlice) { _vm.ChargeWorkspaceStep(); return; }
        if (IsUnmetered) return;
        Pcre2SliceDriver.ThrowIfSliceBreached(_slice.TryChargeInputStep());
    }

    public Utf8ExecutionLimitKind TryReserveWorking(ulong bytes)
    {
        ReserveWorking(bytes);
        return Utf8ExecutionLimitKind.None;
    }

    public Utf8ExecutionLimitKind TryChargeWorkspaceStep()
    {
        Pcre2ReplacementPreparationControl.Check();
        ChargeWorkspaceStep();
        return Utf8ExecutionLimitKind.None;
    }
}
