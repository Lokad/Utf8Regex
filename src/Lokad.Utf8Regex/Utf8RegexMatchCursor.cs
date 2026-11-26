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
    private Utf8SliceSlots _slots = Utf8SliceSlots.Empty;
    private Utf8SliceRunner.AttemptWorkspace _workspace = new();
    private Utf8NonBacktrackingSearchState? _nonBacktracking;
    private Utf8FundedStorage.Use? _programUse;
    private readonly TimeSpan _timeout;
    private int _position;
    private int _origin;
    private int _boundary;
    private int _active;
    private bool _firstCompactRegular;
    private Status _status;
    private enum Status : byte { Open, Ended, Faulted, Disposed }

    internal Utf8RegexMatchCursor(ReadOnlyMemory<byte> input, Utf8Regex regex, Utf8SliceProgram program,
        Utf8CaptureCatalog catalog, int start, Utf8SliceInvocation invocation)
    {
        _input = input; _regex = regex; _program = program; _catalog = catalog;
        _timeout = regex.MatchTimeout; _invocation = invocation;
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref _invocation);
            if ((uint)start > (uint)input.Length ||
                start < input.Length && !Utf8BytePosition.IsScalarBoundary(input.Span, start))
                throw new ArgumentOutOfRangeException("startOffsetInBytes");
            _position = _origin = start;
            _slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref _invocation);
            _firstCompactRegular = input.Length - start >= 64 && program.NonBacktrackingOrderedClosures.Length != 0;
            if (program.IsNonBacktracking && !_firstCompactRegular) PrepareNonBacktracking(program);
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

    /// <summary>Advances to immutable byte ranges without materializing captured text.</summary>
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
        if (_position < 0) { FinishExhausted(); return false; }
        var outcome = default(Utf8SliceSearchOutcome);
        var results = new Utf8InvocationResultLedger();
        var exhausted = false;
        try
        {
            var program = _program!;
            // Regular execution exposes only the last completed capture.
            var useCaptureSlots = program.IsNonBacktracking || program.HasSingleCapturePerGroup;
            var collectHistory = program.GroupCount != 0 && !useCaptureSlots;
            _invocation.AnchorOriginByte = _origin;
            Utf8RangeMatch completed = default;
            if (program.EmittedHistoryOffsets is { } offsets && (program.EmittedMatch ?? program.TieredProof) is { } proof &&
                _invocation.IsUnmetered && _invocation.CheckpointHook is null &&
                Utf8SliceRunner.TryGetFixedDecimalFollower(program, out var digits, out var first, out var last))
                outcome = FindEmittedHistory(proof, offsets, digits, first, last, ref results, out completed);
            else if (program.GroupCount == 0 && _regex!.TryFindNativeLiteralForResult(_input.Span, _position,
                    ref _invocation, out var found, out var start, out var end, forCoreCursor: true))
                outcome = new(found, start, end, Utf8ExecutionLimitKind.None);
            else if (program.IsNonBacktracking)
                outcome = FindNonBacktracking(program);
            else
            {
                var input = _input.Span;
                var fromByte = _position;
                // The proof meters candidate search on the cursor's invocation.
                // Its undecided case leaves the first unresolved candidate.
                if (program.GroupCount != 0 || program.Instructions[0].Kind != Utf8SliceInstructionKind.Lookbehind ||
                    _invocation.CheckpointHook is not null ||
                    !Utf8SliceRunner.TryFindGuardedRepeat(program, input, ref fromByte, ref _invocation, out outcome))
                    outcome = Utf8SliceRunner.TryFindFirst(program, input, fromByte, _slots.Starts, _slots.Ends,
                        collectHistory, ref _workspace, ref _invocation, in _regex!.SearchPlan,
                        requiredLiteralContinuation: _regex.RequiredLiteralContinuation,
                        requiredLiteralTerminalSuffix: _regex.RequiredLiteralTerminalSuffix,
                        requiredLiteralOptionalRun: _regex.RequiredLiteralOptionalRun);
            }
            if (outcome.Breach != Utf8ExecutionLimitKind.None) throw _invocation.Budget.CreateResourceFailure();
            if (!outcome.Found)
            {
                Checkpoint();
                exhausted = true;
                return false;
            }

            // Snapshot checks during capture shaping and before allocating each result array.
            if (!completed.Success)
            {
                completed = collectHistory
                    ? _workspace.Stacks.SnapshotHistory(_input, _catalog!.GroupNumbers, outcome.StartByte, outcome.EndByte,
                        ref results, ref _invocation)
                    : SnapshotSlots(outcome, ref results);
                program.ObserveCaptureProofWork(ref _invocation);
            }
            Utf8RetainedRangeMatch? receipt = null;
            if (retain)
            {
                results.ReserveSharedStorage(Utf8ConstructionBudget.ArrayBytes<int>(_catalog!.GroupNumbers.Length), ref _invocation);
                var allowance = resultAllowance ?? _invocation.Ledger.MemoryAllowance!;
                receipt = results.AllocateObject((Match: completed, Allowance: allowance),
                    static state => new Utf8RetainedRangeMatch(state.Match, state.Allowance), ref _invocation);
            }
            else results.Release(ref _invocation);
            // A caller allowance may signal interruption while receiving a refund.
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
                    try
                    {
                        results.Release(ref _invocation);
                        Checkpoint();
                    }
                    catch { allowance.Release(bytes); throw; }
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
            if (results.ReservedBytes != 0) results.Release(ref _invocation);
            // Close only after transient reservations/roots have finished.
            if (exhausted) FinishExhausted();
        }
    }

    private Utf8SliceSearchOutcome FindEmittedHistory(Utf8BoundedProgramEmitter.Match proof, int[] offsets,
        int digits, byte first, byte last, ref Utf8InvocationResultLedger results, out Utf8RangeMatch match)
    {
        Span<Utf8CaptureRange> captures = stackalloc Utf8CaptureRange[Utf8BoundedProgramEmitter.MaxHistoryCaptures];
        if (!Utf8SliceRunner.TryFindFixedDecimalMatch(_input.Span, _position, digits, first, last, proof,
                out var start, out var end, captures))
        {
            match = default;
            return new(false, -1, -1, Utf8ExecutionLimitKind.None);
        }

        var ranges = results.AllocateArray<Utf8CaptureRange>(offsets[^1], ref _invocation);
        ranges[0] = new(start, end);
        for (var slot = 1; slot < ranges.Length; slot++)
        {
            var capture = captures[slot - 1];
            ranges[slot] = new(start + capture.StartOffsetInBytes, start + capture.EndOffsetInBytes);
        }
        match = new(_input, ranges, offsets, _catalog!.GroupNumbers);
        return new(true, start, end, Utf8ExecutionLimitKind.None);
    }

    private Utf8SliceSearchOutcome FindNonBacktracking(Utf8SliceProgram program)
    {
        if (_nonBacktracking is null && (_firstCompactRegular || _input.Length - _position < 64))
        {
            _firstCompactRegular = false;
            var input = _input.Span;
            var selected = Utf8SliceRunner.FindOrderedRegularRange(program, input, _position, ref _invocation);
            return selected.Found && _slots.Count != 0
                ? Utf8SliceRunner.ReplayOrderedRegularRange(program, input, selected, _slots, ref _invocation)
                : selected;
        }

        if (program.NonBacktrackingOrderedForwardOnly && (_nonBacktracking is null || _nonBacktracking.IsWindow))
            return Utf8NonBacktrackingSearchState.FindInWindows(program, _input.Span, _position, _slots.Starts, _slots.Ends,
                ref _nonBacktracking, ref _boundary, ref _invocation);

        // After the first result, use viability unless only a bounded tail
        // remains. A preferred cyclic branch can otherwise rescan each suffix.
        if (_nonBacktracking is null) PrepareNonBacktracking(program);
        return _nonBacktracking!.FindFirst(program, ref _boundary, _slots.Starts, _slots.Ends, ref _invocation);
    }

    private void PrepareNonBacktracking(Utf8SliceProgram program)
    {
        var breach = Utf8NonBacktrackingSearchState.TryCreate(program, _input.Span, ref _invocation, out _nonBacktracking);
        if (breach != Utf8ExecutionLimitKind.None) throw _invocation.Budget.CreateResourceFailure();
        _boundary = _nonBacktracking!.ResolveByteOffset(_input.Span, _position);
    }

    private Utf8RangeMatch SnapshotSlots(Utf8SliceSearchOutcome outcome, ref Utf8InvocationResultLedger results)
    {
        var numbers = _catalog!.GroupNumbers;
        var starts = _slots.Starts;
        var ends = _slots.Ends;
        var count = 1;
        for (var slot = 0; slot < ends.Length; slot++)
        {
            if ((slot & 255) == 0) Checkpoint();
            if (ends[slot] >= 0) count++;
        }
        // One participating capture per ordinal already describes its history.
        var offsets = count == numbers.Length ? null
            : results.AllocateArray<int>(checked(numbers.Length + 1), ref _invocation);
        var captures = results.AllocateArray<Utf8CaptureRange>(count, ref _invocation);
        captures[0] = new(outcome.StartByte, outcome.EndByte);
        var written = 1;
        if (offsets is not null) offsets[1] = written;
        for (var slot = 1; slot < numbers.Length; slot++)
        {
            if ((slot & 255) == 0) Checkpoint();
            var end = ends[slot - 1];
            if (end >= 0) captures[written++] = new(starts[slot - 1], end);
            if (offsets is not null) offsets[slot + 1] = written;
        }
        return new(_input, captures, offsets, numbers);
    }

    private void Checkpoint()
    {
        if (_invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
            throw _invocation.Budget.CreateResourceFailure();
        _invocation.Budget.ThrowIfCancellationRequested();
    }

    private System.Text.RegularExpressions.RegexMatchTimeoutException TimeoutFailure()
        => Utf8Regex.CreateMatchTimeoutException(default, _regex?.Pattern ?? string.Empty, _timeout, _invocation.Budget.Limits);

    private void FinishExhausted()
    {
        var regex = _regex!;
        var funded = _invocation.Ledger.HasMemoryAllowance || _programUse is not null;
        Close(Status.Faulted);
        if (funded)
        {
            _invocation.Budget.ThrowIfCancellationRequested();
            try { _invocation.Budget.CheckDeadline(); }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw Utf8Regex.CreateMatchTimeoutException(default, regex.Pattern, _timeout, _invocation.Budget.Limits);
            }
        }
        _status = Status.Ended;
    }

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
        _input = default; _regex = null; _program = null; _catalog = null;
        if (_workspace.NeedsRelease) _workspace.Release(ref _invocation);
        _slots = Utf8SliceSlots.Empty; _nonBacktracking = null;
        _invocation.Ledger.Release(_invocation.Ledger.ReservedBytes);
        var use = _programUse; _programUse = null; use?.Dispose();
    }
}
