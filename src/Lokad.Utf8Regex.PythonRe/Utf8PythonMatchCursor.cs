using System.Threading;
using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Discovers Python-compatible scalar matches incrementally over one UTF-8 memory subject.</summary>
/// <remarks>
/// Keep the subject unchanged and its memory manager alive while reading any borrowed result bytes.
/// Dispose the cursor when abandoning discovery early. There is no finalizer.
/// Sequential advances may cross host calls and asynchronous waits; the original deadline includes those waits.
/// Concurrent or reentrant access is rejected. Aliases reference the same position and lifetime.
/// Returned ranges own immutable metadata and retain their borrowed subject; they remain usable after later advances or disposal.
/// Work and modeled result capacity remain cumulative through moves and snapshot publication. Working release never refunds result traffic; caller-retained live memory has separate scope.
/// Exclusive VM scratch is reused and stays charged while positioned, including during projection and host waits. EOF, failure and disposal release it synchronously. Scratch and publication overlap can require a higher working cap.
/// Captures expose byte ranges only; callers derive character positions from their text when needed.
/// </remarks>
public sealed class Utf8PythonMatchCursor : IDisposable
{
    private ReadOnlyMemory<byte> _input;
    private PythonReScalarMatchState _discovery;
    private int _active;
    private Utf8FundedStorage.Use? _programUse;
    private Status _status;
    private readonly Utf8PythonMatchMode _mode;

    private enum Status : byte { Open, Matched, Ended, Faulted, Disposed }

    internal Utf8PythonMatchCursor(ReadOnlyMemory<byte> input, Utf8SliceProgram program,
        int startOffsetInBytes, TimeSpan timeout,
        object pattern, Utf8SliceInvocation invocation, Utf8Regex? candidateRegex, Utf8FundedStorage.Use? programUse = null,
        Utf8PythonMatchMode mode = Utf8PythonMatchMode.Search)
    {
        _input = input;
        _programUse = programUse;
        _mode = mode;
        PythonReScalarMatchState.Initialize(out _discovery, input.Span, program,
            startOffsetInBytes, timeout, pattern, invocation, candidateRegex, prepareSearch: mode == Utf8PythonMatchMode.Search);
        try { _discovery.Checkpoint(); }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            _discovery.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            var failure = _discovery.WorkingInvocation.Budget.CreateResourceFailure();
            Close(Status.Faulted);
            throw failure;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            var failure = _discovery.TimeoutFailure(_input.Span);
            Close(Status.Faulted);
            throw failure;
        }
        catch { Close(Status.Faulted); throw; }
    }

    /// <summary>Advances to byte ranges under a token scoped to this synchronous advance.</summary>
    /// <remarks>The creation token, absolute deadline and cumulative budgets remain authoritative. Range metadata survives advances/disposal; subject bytes remain borrowed. Use the returned range match for this advance.</remarks>
    public bool MoveNext(out Utf8PythonRangeMatch match, CancellationToken cancellationToken = default)
        => MoveNextCore(cancellationToken, out match, out _);

    /// <summary>Advances to ranges whose admitted result charge stays reserved until the caller disposes the returned receipt.</summary>
    /// <remarks>Requires a cursor with a caller-owned working allowance. No release/reacquisition occurs at publication. Creation/advance tokens, deadline and cumulative limits still apply;  Dispose the receipt after retained users finish or take over admission for their aliases. The receipt can outlive this cursor and expression.</remarks>
    /// <exception cref="InvalidOperationException">The cursor has no caller-owned working allowance, previously failed, or is accessed concurrently.</exception>
    public bool MoveNextRetained([NotNullWhen(true)] out Utf8PythonRetainedRangeMatch? match,
        CancellationToken cancellationToken = default)
    {
        var advanced = MoveNextCore(cancellationToken, out _, out var retained, retain: true);
        match = retained!;
        return advanced;
    }

    private bool MoveNextCore(CancellationToken cancellationToken, out Utf8PythonRangeMatch match,
        out Utf8PythonRetainedRangeMatch? retained, bool retain = false)
    {
        match = default;
        retained = null;
        Enter();
        try
        {
            if (!CanAdvance()) return false;
            if (retain && !_discovery.WorkingInvocation.Ledger.HasMemoryAllowance)
                throw new InvalidOperationException("Retained ranges require a cursor with a caller-owned working allowance.");
            if (!cancellationToken.CanBeCanceled) return Advance(out match, out retained, retain);
            var original = _discovery.WorkingInvocation.Budget.ExchangeCancellationToken(cancellationToken);
            CancellationTokenSource? linked = null;
            try
            {
                if (original.CanBeCanceled && original != cancellationToken)
                {
                    linked = CancellationTokenSource.CreateLinkedTokenSource(original, cancellationToken);
                    _discovery.WorkingInvocation.Budget.ExchangeCancellationToken(linked.Token);
                }
                return Advance(out match, out retained, retain);
            }
            catch (OperationCanceledException)
            {
                // Linked tokens are implementation details, never caller-facing
                // interruption authority. Keep original-token precedence.
                original.ThrowIfCancellationRequested();
                cancellationToken.ThrowIfCancellationRequested();
                throw;
            }
            finally
            {
                // Terminal advances have already released the invocation.
                if (_status is Status.Open or Status.Matched)
                    _discovery.WorkingInvocation.Budget.ExchangeCancellationToken(original);
                linked?.Dispose();
            }
        }
        finally { Exit(); }
    }

    private bool CanAdvance()
    {
        ThrowIfDisposed();
        if (_status == Status.Faulted)
            throw new InvalidOperationException("The cursor cannot advance after a failed operation.");
        return _status != Status.Ended;
    }

    private bool Advance(out Utf8PythonRangeMatch match, out Utf8PythonRetainedRangeMatch? retained, bool retain)
    {
        match = default;
        retained = null;
        try
        {
            if (_status == Status.Matched)
            {
                _discovery.Checkpoint();
                Close(Status.Ended);
                return false;
            }
            var input = _input.Span;
            var found = _mode == Utf8PythonMatchMode.Search
                ? _discovery.MoveNext(input)
                : _discovery.MatchAtCurrentPosition(input, requireFullInput: _mode == Utf8PythonMatchMode.FullMatch);
            if (!found)
            {
                Close(Status.Ended);
                return false;
            }
            var marker = _discovery.ResultReservation;
            var completed = _discovery.SnapshotRanges(_input);
            var receipt = retain ? _discovery.RetainRanges(completed) : null;
            if (receipt is null) _discovery.ReleaseResultsSince(marker);
            _discovery.Checkpoint();
            if (receipt is not null)
            {
                receipt.SetTransferredBytes(_discovery.TransferResultsSince(marker));
                retained = receipt;
            }
            else match = completed;
            _status = _mode == Utf8PythonMatchMode.Search ? Status.Open : Status.Matched;
            return true;
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            _discovery.WorkingInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            var failure = _discovery.WorkingInvocation.Budget.CreateResourceFailure();
            Close(Status.Faulted);
            throw failure;
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            var failure = _discovery.TimeoutFailure(_input.Span);
            Close(Status.Faulted);
            throw failure;
        }
        catch { Close(Status.Faulted); throw; }
    }

    /// <summary>Releases cursor scratch and subject references; previously returned snapshots remain valid.</summary>
    /// <remarks>Idempotent. Every alias observes the disposed state.</remarks>
    /// <exception cref="InvalidOperationException">The cursor is being accessed concurrently.</exception>
    public void Dispose()
    {
        Enter();
        try { Close(Status.Disposed); }
        finally { Exit(); }
    }

    private void Enter()
    {
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            throw new InvalidOperationException("Concurrent or reentrant cursor access is unsupported.");
    }

    private void Exit() => Volatile.Write(ref _active, 0);

    private void ThrowIfDisposed()
    {
        if (_status == Status.Disposed) throw new ObjectDisposedException(nameof(Utf8PythonMatchCursor));
    }

    private void Close(Status status)
    {
        if (_status is Status.Open or Status.Matched)
        {
            _discovery.Dispose();
        }
        var programUse = _programUse;
        _programUse = null;
        programUse?.Dispose();
        if (status == Status.Ended && (_discovery.WorkingInvocation.Ledger.HasMemoryAllowance || programUse is not null))
        {
            // Keep the original timeout diagnostic state until the advance has checked the refund.
            _status = Status.Faulted;
            ref var budget = ref _discovery.WorkingInvocation.Budget;
            budget.ThrowIfCancellationRequested();
            budget.CheckDeadline();
            budget.ThrowIfCancellationRequested();
        }
        _discovery = default;
        _input = default;
        _status = status;
    }

    internal ref Utf8SliceInvocation WorkingInvocation => ref _discovery.WorkingInvocation;
}
