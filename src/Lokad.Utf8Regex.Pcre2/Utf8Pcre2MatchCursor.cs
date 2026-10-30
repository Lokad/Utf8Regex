using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Discovers PCRE2 matches incrementally over one borrowed UTF-8 memory subject.</summary>
/// <remarks>Keep subject bytes unchanged and their memory manager alive. Aliases share position/lifetime; concurrent or reentrant access is rejected. Sequential advances may cross awaits under one original deadline and cumulative work/result policy. Dispose early abandonment; there is no finalizer. EOF, failure and Dispose refund exclusive scratch and program use. Returned ranges survive later moves and close.</remarks>
public sealed class Utf8Pcre2MatchCursor : IDisposable
{
    private Utf8ValidatedInput.Memory _input;
    private Pcre2CompiledProgram? _program;
    private readonly Pcre2MatchOptions _options;
    private Pcre2ResourceBudget _budget;
    private Pcre2GlobalProgress _progress;
    private Utf8FundedStorage.Use? _programUse;
    private int _active;
    private Pcre2CursorStatus _status;
    private enum Pcre2CursorStatus : byte { Open, Ended, Faulted, Disposed }

    internal Utf8Pcre2MatchCursor(ReadOnlyMemory<byte> input, Pcre2CompiledProgram program, int start,
        Pcre2MatchOptions options, Utf8SliceInvocation invocation)
    {
        _program = program; _options = options;
        _budget = new(program.Request.DefaultLimits, invocation, false);
        _budget.WorkingInvocation.ShareStorageForCursor();
        try
        {
            Pcre2SliceDriver.ThrowIfSliceBreached(Utf8ValidatedInput.Memory.TryCreate(input, ref _budget.WorkingInvocation, out _input));
            var subject = _input.Borrow();
            _progress = new(subject.GetBytePosition(start, "startOffsetInBytes"));
            Checkpoint();
        }
        catch (Utf8ExecutionDeadlineExpiredException) { Close(Pcre2CursorStatus.Faulted); throw TimeoutFailure(); }
        catch { Close(Pcre2CursorStatus.Faulted); throw; }
    }

    internal void AttachProgramUse(Utf8FundedStorage.Use use) => _programUse = use;

    /// <summary>Advances to immutable last-capture byte ranges, without captured text or UTF-16 projection.</summary>
    /// <remarks>The optional token applies only to this synchronous call. The original token/deadline remains authoritative. Failure faults the cursor and refunds exclusive loans. Ordinary publication leaves continued result admission to the caller.</remarks>
    public bool MoveNext(out Utf8Pcre2RangeMatch match, CancellationToken cancellationToken = default)
        => MoveNextCore(cancellationToken, false, null, out match, out _);

    /// <summary>Advances to ranges whose live admission remains reserved until the returned receipt closes.</summary>
    /// <remarks>Requires shared working admission. Null result allowance transfers the existing loan unchanged. A separate result account reserves before working staging refunds, admitting overlap without a funding gap. Refusal is terminal. Copies kept after receipt disposal stay valid, with their admission then the caller's responsibility.</remarks>
    public bool MoveNextRetained([NotNullWhen(true)] out Utf8Pcre2RetainedRangeMatch? match,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? resultMemoryAllowance = null)
        => MoveNextCore(cancellationToken, true, resultMemoryAllowance, out _, out match);

    private bool MoveNextCore(CancellationToken token, bool retain, IUtf8MemoryAllowance? resultAllowance,
        out Utf8Pcre2RangeMatch match, out Utf8Pcre2RetainedRangeMatch? retained)
    {
        match = default; retained = null;
        Enter();
        try
        {
            if (_status == Pcre2CursorStatus.Disposed) throw new ObjectDisposedException(nameof(Utf8Pcre2MatchCursor));
            if (_status == Pcre2CursorStatus.Faulted) throw new InvalidOperationException("The cursor cannot advance after a failure.");
            if (_status == Pcre2CursorStatus.Ended) return false;
            if (retain && !_budget.WorkingInvocation.Ledger.HasMemoryAllowance)
                throw new InvalidOperationException("Retained ranges require caller-owned working admission.");
            var original = token.CanBeCanceled ? _budget.WorkingInvocation.Budget.ExchangeCancellationToken(token) : default;
            CancellationTokenSource? linked = null;
            try
            {
                if (token.CanBeCanceled && original.CanBeCanceled && original != token)
                {
                    linked = CancellationTokenSource.CreateLinkedTokenSource(original, token);
                    _budget.WorkingInvocation.Budget.ExchangeCancellationToken(linked.Token);
                }
                return Advance(retain, resultAllowance, out match, out retained);
            }
            catch (OperationCanceledException)
            { original.ThrowIfCancellationRequested(); token.ThrowIfCancellationRequested(); throw; }
            catch (InvalidOperationException) when (_budget.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
            { Pcre2SliceDriver.ThrowIfSliceBreached(_budget.WorkingInvocation.Budget.LimitBreach); throw; }
            catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
            finally
            {
                if (token.CanBeCanceled) _budget.WorkingInvocation.Budget.ExchangeCancellationToken(original);
                linked?.Dispose();
            }
        }
        catch { if (_status == Pcre2CursorStatus.Open) Close(Pcre2CursorStatus.Faulted); throw; }
        finally { Exit(); }
    }

    private bool Advance(bool retain, IUtf8MemoryAllowance? resultAllowance,
        out Utf8Pcre2RangeMatch match, out Utf8Pcre2RetainedRangeMatch? retained)
    {
        match = default; retained = null;
        Checkpoint();
        var subject = _input.Borrow();
        while (!_progress.Finished)
        {
            var results = new Utf8InvocationResultLedger();
            ulong captureBytes = 0;
            try
            {
                _progress.GetAttempt(_options, out var restart, out var firstMatchingPosition, out var options);
                var outcome = Pcre2Runner.MatchByteRanges(_program!, ref subject, restart, firstMatchingPosition, options, ref _budget);
                var groups = outcome.CaptureResult.ByteGroups;
                if (outcome.Success) captureBytes = Utf8ConstructionBudget.ArrayBytes<Utf8Pcre2CaptureRange>(groups.Length);
                Checkpoint();
                if (!outcome.Success) { _progress.AfterMiss(ref subject, _program!.Request, _options); continue; }
                _progress.Accept(outcome.StartOffsetInBytes, outcome.EndOffsetInBytes,
                    outcome.ConsumedStartOffsetInBytes, outcome.ConsumedEndOffsetInBytes);
                var completed = new Utf8Pcre2RangeMatch(_input.Bytes, groups, _program!.NameEntries, outcome.Mark);
                Utf8Pcre2RetainedRangeMatch? receipt = null;
                var allowance = resultAllowance ?? _budget.WorkingInvocation.Ledger.MemoryAllowance;
                if (retain)
                {
                    ReserveMetadata(outcome.Mark, ref results);
                    receipt = results.AllocateObject((Match: completed, Allowance: allowance!),
                        static state => new Utf8Pcre2RetainedRangeMatch(state.Match, state.Allowance), ref _budget.WorkingInvocation);
                }
                Checkpoint();
                if (receipt is not null)
                {
                    var bytes = checked(captureBytes + results.ReservedBytes);
                    if (ReferenceEquals(allowance, _budget.WorkingInvocation.Ledger.MemoryAllowance))
                    {
                        _budget.WorkingInvocation.Ledger.TransferToCaller(captureBytes); captureBytes = 0;
                        _ = results.TransferSince(0, ref _budget.WorkingInvocation);
                    }
                    else
                    {
                        if (!allowance!.TryReserve(bytes)) throw new Utf8MemoryAdmissionException(bytes);
                        try { Checkpoint(); } catch { allowance.Release(bytes); throw; }
                        _budget.ReleaseWorking(captureBytes); captureBytes = 0;
                        results.Release(ref _budget.WorkingInvocation);
                    }
                    receipt.SetTransferredBytes(bytes); retained = receipt;
                }
                else match = completed;
                return true;
            }
            finally
            {
                _budget.ReleaseWorking(captureBytes);
                results.Release(ref _budget.WorkingInvocation);
            }
        }
        Close(Pcre2CursorStatus.Ended); return false;
    }

    private void ReserveMetadata(string? mark, ref Utf8InvocationResultLedger results)
    {
        results.ReserveSharedStorage(Utf8ConstructionBudget.ArrayBytes<Pcre2NameEntry>(_program!.NameEntries.Length), ref _budget.WorkingInvocation);
        for (var index = 0; index < _program.NameEntries.Length; index++)
        {
            if ((index & 255) == 0) Checkpoint();
            results.ReserveSharedStorage(Utf8ConstructionBudget.StringBytes(_program.NameEntries[index].Name.Length), ref _budget.WorkingInvocation);
        }
        if (mark is not null) results.ReserveSharedStorage(Utf8ConstructionBudget.StringBytes(mark.Length), ref _budget.WorkingInvocation);
    }

    private void Checkpoint()
    {
        Pcre2SliceDriver.ThrowIfSliceBreached(_budget.WorkingInvocation.TryChargeInputStep());
        Pcre2SliceDriver.CheckInvocation(ref _budget.WorkingInvocation);
        _budget.WorkingInvocation.Budget.ThrowIfCancellationRequested();
    }
    private static Pcre2MatchException TimeoutFailure() => new("The PCRE2 match deadline expired.", Pcre2ErrorKind.Timeout);

    /// <summary>Closes every alias and refunds exclusive loans/program use. Previously returned ranges remain valid.</summary>
    public void Dispose()
    {
        Enter(); try { Close(Pcre2CursorStatus.Disposed); } finally { Exit(); }
    }
    private void Enter()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            throw new InvalidOperationException("Concurrent or reentrant cursor access is unsupported.");
    }
    private void Exit() => Volatile.Write(ref _active, 0);
    private void Close(Pcre2CursorStatus status)
    {
        _status = status; _input = default; _program = null; _progress = default;
        _budget.WorkingInvocation.Ledger.Release(_budget.WorkingInvocation.Ledger.ReservedBytes);
        var use = _programUse; _programUse = null; use?.Dispose();
    }
}
