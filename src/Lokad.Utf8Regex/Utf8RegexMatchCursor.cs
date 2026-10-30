using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex;

/// <summary>Discovers Core scalar matches and captures over one borrowed memory subject.</summary>
/// <remarks>Keep subject bytes unchanged and their memory manager alive. Aliases share position/lifetime; concurrent or reentrant use is rejected. Dispose early abandonment; there is no finalizer. Work/result traffic and the original deadline remain cumulative across awaits. Exclusive scratch refunds on EOF, failure or disposal; previously published ranges stay valid.</remarks>
public sealed class Utf8RegexMatchCursor : IDisposable
{
    private ReadOnlyMemory<byte> _input;
    private Utf8Regex? _regex;
    private Utf8SliceProgram? _program;
    private Utf8CaptureCatalog? _catalog;
    private Utf8SliceInvocation _invocation;
    private Utf8SliceSlots _slots = new([], []);
    private Utf8SliceRunner.AttemptWorkspace _workspace = new();
    private Utf8NonBacktrackingSearchState? _nonBacktracking;
    private Utf8FundedStorage.Use? _programUse;
    private string _pattern;
    private readonly TimeSpan _timeout;
    private int _position;
    private int _origin;
    private int _boundary;
    private int _active;
    private Status _status;
    private enum Status : byte { Open, Ended, Faulted, Disposed }

    internal Utf8RegexMatchCursor(ReadOnlyMemory<byte> input, Utf8Regex regex, Utf8SliceProgram program,
        Utf8CaptureCatalog catalog, int start, Utf8SliceInvocation invocation)
    {
        _input = input; _regex = regex; _program = program; _catalog = catalog;
        _pattern = regex.Pattern; _timeout = regex.MatchTimeout; _invocation = invocation;
        _invocation.ShareStorageForCursor();
        try
        {
            _ = Utf8Validation.Validate(input.Span, ref _invocation);
            if ((uint)start > (uint)input.Length || start < input.Length && (input.Span[start] & 0xc0) == 0x80)
                throw new ArgumentOutOfRangeException("startOffsetInBytes");
            _position = _origin = start;
            _slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref _invocation);
            if (program.IsNonBacktracking)
            {
                var breach = Utf8NonBacktrackingSearchState.TryCreate(program, input.Span, ref _invocation, out _nonBacktracking);
                if (breach != Utf8ExecutionLimitKind.None) throw _invocation.Budget.CreateResourceFailure();
                _boundary = _nonBacktracking!.ResolveByteOffset(start);
            }
            Checkpoint();
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
            _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        { var failure = _invocation.Budget.CreateResourceFailure(); Close(Status.Faulted); throw failure; }
        catch (Utf8ExecutionDeadlineExpiredException)
        { var failure = TimeoutFailure(); Close(Status.Faulted); throw failure; }
        catch { Close(Status.Faulted); throw; }
    }

    // Attached only before the cursor escapes; failed owner setup keeps its own lease.
    internal void AttachProgramUse(Utf8FundedStorage.Use use) => _programUse = use;

    /// <summary>Advances to immutable byte ranges without captured text or a whole-subject map.</summary>
    /// <remarks>The optional token applies only to this synchronous advance; the original token/deadline and cumulative limits remain authoritative. A failed advance faults the cursor and releases exclusive scratch. Ordinary publication leaves continued result retention to the caller.</remarks>
    public bool MoveNext(out Utf8RangeMatch match, CancellationToken cancellationToken = default)
        => MoveNextCore(cancellationToken, false, null, out match, out _);

    /// <summary>Advances to ranges whose admission is retained until the returned receipt is disposed.</summary>
    /// <remarks>Requires shared working funding. Null result allowance transfers that existing loan unchanged. A separate result account reserves before the staging loan is refunded, admitting conservative overlap without a funding gap. Refusal is terminal. Copies already obtained from a receipt stay valid after it closes; the caller then owns their admission.</remarks>
    public bool MoveNextRetained([NotNullWhen(true)] out Utf8RetainedRangeMatch? match,
        CancellationToken cancellationToken = default, IUtf8MemoryAllowance? resultMemoryAllowance = null)
        => MoveNextCore(cancellationToken, true, resultMemoryAllowance, out _, out match);

    private bool MoveNextCore(CancellationToken token, bool retain, IUtf8MemoryAllowance? resultAllowance,
        out Utf8RangeMatch match, out Utf8RetainedRangeMatch? retained)
    {
        match = default; retained = null;
        Enter();
        try
        {
            if (_status == Status.Disposed) throw new ObjectDisposedException(nameof(Utf8RegexMatchCursor));
            if (_status == Status.Faulted) throw new InvalidOperationException("The cursor cannot advance after a failure.");
            if (_status == Status.Ended) return false;
            if (retain && !_invocation.Ledger.HasMemoryAllowance)
                throw new InvalidOperationException("Retained ranges require caller-owned working admission.");
            var original = token.CanBeCanceled ? _invocation.Budget.ExchangeCancellationToken(token) : default;
            CancellationTokenSource? linked = null;
            try
            {
                if (token.CanBeCanceled && original.CanBeCanceled && original != token)
                {
                    linked = CancellationTokenSource.CreateLinkedTokenSource(original, token);
                    _invocation.Budget.ExchangeCancellationToken(linked.Token);
                }
                return Advance(retain, resultAllowance, out match, out retained);
            }
            catch (OperationCanceledException)
            { original.ThrowIfCancellationRequested(); token.ThrowIfCancellationRequested(); throw; }
            catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
                _invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
            { throw _invocation.Budget.CreateResourceFailure(); }
            catch (Utf8ExecutionDeadlineExpiredException) { throw TimeoutFailure(); }
            finally
            {
                if (token.CanBeCanceled) _invocation.Budget.ExchangeCancellationToken(original);
                linked?.Dispose();
            }
        }
        catch { if (_status == Status.Open) Close(Status.Faulted); throw; }
        finally { Exit(); }
    }

    private bool Advance(bool retain, IUtf8MemoryAllowance? resultAllowance,
        out Utf8RangeMatch match, out Utf8RetainedRangeMatch? retained)
    {
        match = default; retained = null;
        Checkpoint();
        if (_position < 0) { Close(Status.Ended); return false; }
        var outcome = default(Utf8SliceSearchOutcome);
        var results = new Utf8InvocationResultLedger();
        var exhausted = false;
        try
        {
            var program = _program!;
            _invocation.AnchorOriginByte = _origin;
            if (program.GroupCount == 0 && _regex!.TryFindNativeLiteralForResult(_input.Span, _position,
                    ref _invocation, out var found, out var start, out var end))
                outcome = new(found, start, end, null, null, Utf8ExecutionLimitKind.None);
            else outcome = _nonBacktracking is { } batch
                ? batch.FindFirst(program, ref _boundary, _slots.Starts, _slots.Ends, true, ref _invocation)
                : Utf8SliceRunner.TryFindFirst(program, _input.Span, _position, _slots.Starts, _slots.Ends,
                    true, ref _workspace, ref _invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None) throw _invocation.Budget.CreateResourceFailure();
            Checkpoint();
            if (!outcome.Found) { exhausted = true; return false; }

            var completed = Snapshot(outcome, ref results);
            Utf8RetainedRangeMatch? receipt = null;
            if (retain)
            {
                ReserveNames(ref results);
                var allowance = resultAllowance ?? _invocation.Ledger.MemoryAllowance!;
                receipt = results.AllocateObject((Match: completed, Allowance: allowance),
                    static state => new Utf8RetainedRangeMatch(state.Match, state.Allowance), ref _invocation);
            }
            Checkpoint();
            if (receipt is not null)
            {
                var allowance = resultAllowance ?? _invocation.Ledger.MemoryAllowance!;
                var bytes = results.ReservedBytes;
                if (ReferenceEquals(allowance, _invocation.Ledger.MemoryAllowance))
                    receipt.SetTransferredBytes(results.TransferSince(0, ref _invocation));
                else
                {
                    if (!allowance.TryReserve(bytes)) throw new Utf8MemoryAdmissionException(bytes);
                    try { Checkpoint(); }
                    catch { allowance.Release(bytes); throw; }
                    results.Release(ref _invocation);
                    receipt.SetTransferredBytes(bytes);
                }
                retained = receipt;
            }
            else match = completed;
            var edge = program.RightToLeft ? outcome.StartByte : outcome.EndByte;
            if (outcome.StartByte == outcome.EndByte)
                _position = Utf8SliceRunner.AdvancePastScalar(_input.Span, edge, program.RightToLeft);
            else _position = _origin = edge;
            return true;
        }
        finally
        {
            results.Release(ref _invocation);
            Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref _invocation);
            // Close only after transient reservations/roots have finished.
            if (exhausted) Close(Status.Ended);
        }
    }

    private Utf8RangeMatch Snapshot(Utf8SliceSearchOutcome outcome, ref Utf8InvocationResultLedger results)
    {
        var catalog = _catalog!;
        var count = 1;
        if (outcome.CaptureStarts is { } history)
            for (var slot = 0; slot < history.Length; slot++)
            { if ((slot & 255) == 0) Checkpoint(); count = checked(count + history[slot].Length); }
        var offsets = results.AllocateArray<int>(checked(catalog.GroupNumbers.Length + 1), ref _invocation);
        var captures = results.AllocateArray<Utf8CaptureRange>(count, ref _invocation);
        captures[0] = new(outcome.StartByte, outcome.EndByte);
        var written = 1;
        offsets[1] = written;
        for (var slot = 1; slot < catalog.GroupNumbers.Length; slot++)
        {
            if ((slot & 255) == 0) Checkpoint();
            if (outcome.CaptureStarts is { } starts && outcome.CaptureLengths is { } lengths)
                for (var capture = 0; capture < starts[slot - 1].Length; capture++)
                {
                    if ((written & 255) == 0) Checkpoint();
                    var start = starts[slot - 1][capture];
                    captures[written++] = new(start, checked(start + lengths[slot - 1][capture]));
                }
            offsets[slot + 1] = written;
        }
        return new(_input, captures, offsets, catalog.GroupNumbers, catalog.GroupNames);
    }

    private void ReserveNames(ref Utf8InvocationResultLedger results)
    {
        results.ReserveSharedStorage(Utf8ConstructionBudget.ArrayBytes<int>(_catalog!.GroupNumbers.Length), ref _invocation);
        results.ReserveSharedStorage(Utf8ConstructionBudget.ArrayBytes<string>(_catalog.GroupNames.Length), ref _invocation);
        for (var slot = 0; slot < _catalog.GroupNames.Length; slot++)
        {
            if ((slot & 255) == 0) Checkpoint();
            results.ReserveSharedStorage(Utf8ConstructionBudget.StringBytes(_catalog.GroupNames[slot].Length), ref _invocation);
        }
    }

    private void Checkpoint()
    {
        if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
            throw _invocation.Budget.CreateResourceFailure();
        _invocation.Budget.ThrowIfCancellationRequested();
    }

    private System.Text.RegularExpressions.RegexMatchTimeoutException TimeoutFailure()
        => Utf8Regex.CreateMatchTimeoutException(default, _pattern, _timeout, _invocation.Budget.Limits);

    /// <summary>Closes every alias and refunds exclusive storage synchronously; previously returned ranges remain valid.</summary>
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

    private void Close(Status status)
    {
        // This class exclusively owns the invocation's remaining loans. No
        // copied span cursor or result receipt retains them after transfer.
        _status = status;
        _input = default; _regex = null; _program = null; _catalog = null; _pattern = string.Empty;
        _slots = new([], []); _workspace = new(); _nonBacktracking = null;
        _invocation.Ledger.Release(_invocation.Ledger.ReservedBytes);
        var use = _programUse; _programUse = null; use?.Dispose();
    }
}
