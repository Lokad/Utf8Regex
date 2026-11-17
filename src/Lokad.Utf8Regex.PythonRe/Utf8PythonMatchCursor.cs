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
    private PythonReNameEntry[] _names;
    private PythonReScalarMatchState _discovery;
    private int _active;
    private Utf8FundedStorage.Use? _programUse;
    private Status _status;

    private enum Status : byte { Open, Ended, Faulted, Disposed }

    internal Utf8PythonMatchCursor(ReadOnlyMemory<byte> input, Utf8SliceProgram program,
        PythonReNameEntry[] names, int startOffsetInBytes, TimeSpan timeout,
        object pattern, Utf8SliceInvocation invocation, Utf8Regex? candidateRegex, Utf8FundedStorage.Use? programUse = null)
    {
        _input = input;
        _programUse = programUse;
        _names = names;
        _discovery = new PythonReScalarMatchState(input.Span, program,
            startOffsetInBytes, timeout, pattern, invocation, candidateRegex);
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
            // Hold one shared invocation even if Close resets the discovery
            // state. Counters, original deadline and outstanding loans stay put.
            _discovery.WorkingInvocation.ShareStorageForCursor();
            var invocation = _discovery.WorkingInvocation;
            var original = invocation.Budget.ExchangeCancellationToken(cancellationToken);
            CancellationTokenSource? linked = null;
            try
            {
                if (original.CanBeCanceled && original != cancellationToken)
                {
                    linked = CancellationTokenSource.CreateLinkedTokenSource(original, cancellationToken);
                    invocation.Budget.ExchangeCancellationToken(linked.Token);
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
                invocation.Budget.ExchangeCancellationToken(original);
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
            var input = _input.Span;
            if (!_discovery.MoveNext(input))
            {
                Close(Status.Ended);
                return false;
            }
            var marker = _discovery.ResultReservation;
            {
                var completed = _discovery.SnapshotRanges(_input, _names);
                var receipt = retain ? _discovery.RetainRanges(completed) : null;
                _discovery.Checkpoint();
                if (receipt is not null)
                {
                    receipt.SetTransferredBytes(_discovery.TransferResultsSince(marker));
                    retained = receipt;
                }
                else
                {
                    _discovery.ReleaseResultsSince(marker);
                    match = completed;
                }
                _status = Status.Open;
            }
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
        if (_status == Status.Open)
        {
            _discovery.Dispose();
        }
        var programUse = _programUse;
        _programUse = null;
        _discovery = default;
        _input = default;
        _names = [];
        _status = status;
        programUse?.Dispose();
    }

    internal ref Utf8SliceInvocation WorkingInvocation => ref _discovery.WorkingInvocation;
}
