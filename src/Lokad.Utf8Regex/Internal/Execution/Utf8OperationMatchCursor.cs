using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Buffers;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

internal ref partial struct Utf8OperationMatchCursor
{
    [UnscopedRef]
    internal Utf8InvocationBufferControl WorkingBufferControl => new(ref _sliceInvocation);

    internal void ChargeResultBytes(ulong bytes) => Utf8SliceBufferWork.ChargeResultBytes(bytes, ref _sliceInvocation);

    internal void CheckCancellationAtEntry() => _sliceInvocation.Budget.ThrowIfCancellationRequested();

    internal ulong DiagnosticCheckpointCount => _sliceInvocation.Budget.DiagnosticCheckpoints;

    internal bool HasResourceLimitBreach => _sliceInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None;

    internal Utf8ResourceLimitExceededException CreateExecutionResourceFailure()
        => _sliceInvocation.Budget.CreateResourceFailure();

    internal System.Text.RegularExpressions.RegexMatchTimeoutException CreateMatchTimeoutException(ReadOnlySpan<byte> input, string pattern, TimeSpan timeout)
        => Utf8Regex.CreateMatchTimeoutException(_sliceInvocation.Ledger.HasMemoryAllowance ? default : input,
            pattern, timeout, _sliceInvocation.Budget.Limits);


    internal ulong DebugInvocationReservedBytes => _sliceInvocation.Ledger.ReservedBytes;

    internal bool DebugIsOwnedSlice => _mode == EnumeratorMode.OwnedSlice;

    internal bool IsRightToLeft => _sliceProgram is { RightToLeft: true } ||
        _mode is EnumeratorMode.RtlExactAsciiLiteral or EnumeratorMode.RtlExactUtf8Literal or EnumeratorMode.RtlAsciiLiteralIgnoreCase;
    internal int DebugConsumed => _consumed;

    private bool _hasAnchoredLiteralInvocation;
    private readonly EnumeratorMode _mode;
    private static readonly Utf8SearchPlan s_emptySearchPlan = default;
    private readonly ref readonly Utf8SearchPlan _searchPlan;
    private readonly PreparedSmallAsciiLiteralFamilySearch _smallAsciiLiteralFamilySearch;
    private readonly byte[]? _literal;
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8ExecutionDeadline _budget;
    private readonly Utf8SearchOperationPlan _program;
    private ReadOnlySpan<byte> _remaining;
    private int _consumed;
    private PreparedMultiLiteralScanState _multiLiteralScanState;
    private Utf8CaptureRange _current;
    private Utf8SliceProgram? _sliceProgram;
    private Utf8NonBacktrackingSearchState? _nonBacktrackingState;
    private int _nonBacktrackingBoundary;
    // Native discovery/publication and the owned VM retain one invocation.
    private Utf8SliceInvocation _sliceInvocation;
    private bool _preserveOffsetCurrent;
    private bool _anchoredExhausted;
    private int _slicePositionByte;
    private int _sliceOriginByte;

    internal Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, Utf8ExecutionDeadline deadline, bool rightToLeft)
        : this(input, regexPlan, rightToLeft, new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, deadline, CancellationToken.None)) { }

    // Keep the original invocation when the entry point already admitted it.
    internal Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, bool rightToLeft, Utf8SliceInvocation invocation)
        : this(input, regexPlan.LiteralUtf8 ?? throw new InvalidOperationException("A literal cursor requires a literal."),
            in regexPlan.SearchPlan, (regexPlan.ExecutionKind, rightToLeft) switch
            {
                (NativeExecutionKind.ExactAsciiLiteral, false) => EnumeratorMode.ExactAsciiLiteral,
                (NativeExecutionKind.ExactUtf8Literal, false) => EnumeratorMode.ExactUtf8Literal,
                (NativeExecutionKind.AsciiLiteralIgnoreCase, false) => EnumeratorMode.AsciiLiteralIgnoreCase,
                (NativeExecutionKind.ExactAsciiLiteral, true) => EnumeratorMode.RtlExactAsciiLiteral,
                (NativeExecutionKind.ExactUtf8Literal, true) => EnumeratorMode.RtlExactUtf8Literal,
                (NativeExecutionKind.AsciiLiteralIgnoreCase, true) => EnumeratorMode.RtlAsciiLiteralIgnoreCase,
                _ => throw new InvalidOperationException("The plan is not a single literal.")
            }, invocation) { }

    private Utf8OperationMatchCursor(ReadOnlySpan<byte> input, byte[] literal, in Utf8SearchPlan searchPlan,
        EnumeratorMode mode, Utf8SliceInvocation invocation)
    {
        _searchPlan = ref searchPlan;
        _literal = literal;
        _input = input;
        _budget = invocation.Budget.Deadline;
        _sliceInvocation = invocation;
        _remaining = input;
        _mode = mode;
    }

    public Utf8CaptureRange CurrentValueMatch => _current;

    internal Utf8OperationMatchCursor(Utf8SliceProgram program, ReadOnlySpan<byte> input, Utf8SliceInvocation invocation)
        : this(program, input, invocation, program.RightToLeft ? input.Length : 0)
    {
    }

    internal Utf8OperationMatchCursor(Utf8SliceProgram program, ReadOnlySpan<byte> input, Utf8SliceInvocation invocation, int startByte)
    {
        _searchPlan = ref s_emptySearchPlan;
        _smallAsciiLiteralFamilySearch = default;
        _literal = null;
        _input = input;
        _budget = Utf8ExecutionDeadline.Infinite;
        _program = default;
        _remaining = default;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _sliceProgram = program;
        _sliceInvocation = invocation;
        _sliceInvocation.ShareStorageForCursor();
        if (program.IsNonBacktracking)
        {
            var breach = Utf8NonBacktrackingSearchState.TryCreate(program, input, ref _sliceInvocation, out var state);
            if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Owned cursor preparation exceeded {breach}.");
            if (state is null) throw new InvalidOperationException("NonBacktracking preparation returned no state.");
            try
            {
                _nonBacktrackingBoundary = state.ResolveByteOffset(input, startByte);
            }
            catch { state.Release(ref _sliceInvocation); throw; }
            _nonBacktrackingState = state;
        }
        _slicePositionByte = startByte;
        _sliceOriginByte = _slicePositionByte;
        _mode = EnumeratorMode.OwnedSlice;
    }

    internal void ReleaseOwnedSearchState()
    {
        _nonBacktrackingState = null;
    }

    private bool MoveNextOwnedSlice() => MoveNextOwnedSliceBytes(out _);

    private bool MoveNextOwnedSliceBytes(out Utf8CaptureRange range)
    {
        range = default;
        var slots = Utf8SliceSlots.Empty;
        try
        {
            // A new MoveNext and result publication are operation boundaries,
            // even when a short VM attempt never reaches its polling cadence.
            CheckOwnedSliceCursorBoundary();
            if (_sliceProgram is null) return false;
            // This cursor returns whole-match extents. Only capture-dependent
            // programs need the VM's capture registers during discovery.
            var slotCount = _sliceProgram.ReadsCaptures ? _sliceProgram.GroupCount : 0;
            slots = Utf8SliceRunner.RentSlots(slotCount, ref _sliceInvocation);
            _sliceInvocation.AnchorOriginByte = _sliceOriginByte;
            var outcome = _nonBacktrackingState is { } batch
                ? batch.FindFirst(_sliceProgram, ref _nonBacktrackingBoundary, slots.Starts, slots.Ends, ref _sliceInvocation)
                : Utf8SliceRunner.TryFindFirst(_sliceProgram, _input, _slicePositionByte, slots.Starts, slots.Ends, false, ref _sliceInvocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
            if (outcome.Found)
            {
                CheckOwnedSliceCursorBoundary();
                range = new Utf8CaptureRange(outcome.StartByte, outcome.EndByte);
                _current = range;
                var edge = _sliceProgram.RightToLeft ? outcome.StartByte : outcome.EndByte;
                if (outcome.EndByte > outcome.StartByte)
                {
                    _slicePositionByte = edge;
                    _sliceOriginByte = edge;
                }
                else _slicePositionByte = Utf8SliceRunner.AdvancePastScalar(_input, edge, _sliceProgram.RightToLeft);
                return true;
            }

            CheckOwnedSliceCursorBoundary();
            ReleaseOwnedSearchState();
            _sliceProgram = null;
            return false;
        }
        catch
        {
            ReleaseOwnedSearchState();
            _sliceProgram = null;
            throw;
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref _sliceInvocation);
        }
    }

    public bool MoveNext()
    {
        _budget.CheckNow();
        if (_hasAnchoredLiteralInvocation)
        {
            var breach = TryMoveNextAnchoredLiteral(out var found);
            if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Anchored literal cursor exceeded {breach}.");
            return found;
        }
        if (_mode == EnumeratorMode.AnchoredAsciiLiteralPrefix)
        {
            if (_consumed != 0)
            {
                return false;
            }

            _consumed = 1;
            _budget.Step();
            var literal = _literal;
            if (literal is null || literal.Length == 0 || literal.Length > _input.Length)
            {
                return false;
            }

            if (!_input[..literal.Length].SequenceEqual(literal))
            {
                return false;
            }

            _current = new Utf8CaptureRange(0, 0 + (literal.Length));
            return true;
        }

        // Folded anchored prefixes match the same single anchored hit with per-byte
        // invariant folding; Kelvin and Turkic divergences stay fenced upstream of
        // every cursor construction site.
        if (_mode == EnumeratorMode.AnchoredFoldedAsciiLiteralPrefix)
        {
            if (_consumed != 0)
            {
                return false;
            }

            _consumed = 1;
            _budget.Step();
            var foldedPrefixLiteral = _literal;
            if (foldedPrefixLiteral is null || foldedPrefixLiteral.Length == 0 || foldedPrefixLiteral.Length > _input.Length)
            {
                return false;
            }

            if (!FoldedAnchoredLiteralEquals(_input[..foldedPrefixLiteral.Length], foldedPrefixLiteral))
            {
                return false;
            }

            _current = new Utf8CaptureRange(0, 0 + (foldedPrefixLiteral.Length));
            return true;
        }

        // The mode carries lax versus strict end handling. Results retain the
        // original byte endpoints.
        if (_mode is EnumeratorMode.AnchoredAsciiLiteralSuffix or EnumeratorMode.AnchoredAsciiLiteralSuffixStrictEnd)
        {
            if (_consumed != 0)
            {
                return false;
            }

            _consumed = 1;
            _budget.Step();
            var suffixLiteral = _literal;
            if (suffixLiteral is null || suffixLiteral.Length == 0 || suffixLiteral.Length > _input.Length)
            {
                return false;
            }

            var suffixEnd = _input.Length;
            if (_mode == EnumeratorMode.AnchoredAsciiLiteralSuffix &&
                suffixEnd > suffixLiteral.Length &&
                _input[suffixEnd - 1] == (byte)'\n')
            {
                suffixEnd--;
            }

            var suffixStart = suffixEnd - suffixLiteral.Length;
            if (suffixStart < 0 || !_input.Slice(suffixStart, suffixLiteral.Length).SequenceEqual(suffixLiteral))
            {
                return false;
            }


            _current = new Utf8CaptureRange(suffixStart, (suffixStart) + (suffixLiteral.Length));
            return true;
        }

        // Folded anchored suffixes mirror the exact block with per-byte invariant
        // folding; the mode carries lax versus strict end handling.
        if (_mode is EnumeratorMode.AnchoredFoldedAsciiLiteralSuffix or EnumeratorMode.AnchoredFoldedAsciiLiteralSuffixStrictEnd)
        {
            if (_consumed != 0)
            {
                return false;
            }

            _consumed = 1;
            _budget.Step();
            var foldedSuffixLiteral = _literal;
            if (foldedSuffixLiteral is null || foldedSuffixLiteral.Length == 0 || foldedSuffixLiteral.Length > _input.Length)
            {
                return false;
            }

            var foldedSuffixEnd = _input.Length;
            if (_mode == EnumeratorMode.AnchoredFoldedAsciiLiteralSuffix &&
                foldedSuffixEnd > foldedSuffixLiteral.Length &&
                _input[foldedSuffixEnd - 1] == (byte)'\n')
            {
                foldedSuffixEnd--;
            }

            var foldedSuffixStart = foldedSuffixEnd - foldedSuffixLiteral.Length;
            if (foldedSuffixStart < 0 || !FoldedAnchoredLiteralEquals(_input.Slice(foldedSuffixStart, foldedSuffixLiteral.Length), foldedSuffixLiteral))
            {
                return false;
            }


            _current = new Utf8CaptureRange(foldedSuffixStart, (foldedSuffixStart) + (foldedSuffixLiteral.Length));
            return true;
        }

        return _mode switch
        {
            EnumeratorMode.ExactAsciiLiteral => MoveNextLiteral(),
            EnumeratorMode.ExactUtf8Literal => MoveNextLiteral(),
            EnumeratorMode.ExactUtf8Literals => MoveNextExactUtf8Literals(),
            EnumeratorMode.AsciiLiteralIgnoreCase => MoveNextLiteral(),
            EnumeratorMode.AsciiLiteralIgnoreCaseLiterals => MoveNextAsciiIgnoreCaseLiterals(),
            EnumeratorMode.RtlExactAsciiLiteral => MoveNextLiteral(),
            EnumeratorMode.RtlAsciiLiteralIgnoreCase => MoveNextLiteral(),
            EnumeratorMode.RtlExactUtf8Literal => MoveNextLiteral(),
            EnumeratorMode.SmallAsciiLiteralFamily => MoveNextSmallAsciiLiteralFamily(),
            EnumeratorMode.EmptyLiteral => MoveNextEmptyLiteral(),
            EnumeratorMode.OwnedSlice => MoveNextOwnedSlice(),
            _ => false
        };
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, Utf8ExecutionDeadline budget)
    {
        this = new Utf8OperationMatchCursor(input, regexPlan, NativeExecutionKind.ExactUtf8Literals, budget);
    }

    internal void CheckOwnedSliceCursorBoundary()
    {
        var breach = _sliceInvocation.CheckInput();
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Owned slice cursor exceeded {breach}.");
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
    }

    // Literal-family cursors keep one original invocation.
    internal Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, Utf8SliceInvocation invocation)
        : this(input, regexPlan, regexPlan.ExecutionKind, invocation) { }

    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget)
        : this(input, regexPlan, executionKind, new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None)) { }

    private Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, NativeExecutionKind executionKind, Utf8SliceInvocation invocation)
    {
        _searchPlan = ref regexPlan.SearchPlan;
        _smallAsciiLiteralFamilySearch = default;
        _literal = null;
        _input = input;
        _budget = invocation.Budget.Deadline;
        _sliceInvocation = invocation;
        _program = _searchPlan.SearchOperation;
        _remaining = input;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _mode = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
            ? EnumeratorMode.AsciiLiteralIgnoreCaseLiterals
            : EnumeratorMode.ExactUtf8Literals;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        _searchPlan = ref s_emptySearchPlan;
        _smallAsciiLiteralFamilySearch = default;
        _literal = Array.Empty<byte>();
        _input = input;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _program = default;
        _remaining = input;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _mode = EnumeratorMode.EmptyLiteral;
    }

    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input, PreparedSmallAsciiLiteralFamilySearch smallAsciiLiteralFamilySearch, Utf8ExecutionDeadline budget)
    {
        _searchPlan = ref s_emptySearchPlan;
        _smallAsciiLiteralFamilySearch = smallAsciiLiteralFamilySearch;
        _literal = null;
        _input = input;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _program = default;
        _remaining = input;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _mode = EnumeratorMode.SmallAsciiLiteralFamily;
    }

    // The entry point supplies a checked floor. Anchors use the full subject.
    internal Utf8OperationMatchCursor WithLiteralStartFloor(int startByteOffset)
    {
        _preserveOffsetCurrent = true;
        _remaining = _input[startByteOffset..];
        _consumed = startByteOffset;
        _multiLiteralScanState = new PreparedMultiLiteralScanState(startByteOffset, startByteOffset, 0);
        _anchoredExhausted = false;
        return this;
    }

    private bool MoveNextLiteral()
    {
        var previous = _current;
        try
        {
            var breach = TryMoveNextLiteral(out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return found;
        }
        catch { if (_preserveOffsetCurrent) _current = previous; throw; }
    }

    internal Utf8ExecutionLimitKind TryMoveNextLiteral(out bool found)
    {
        found = false;
        _current = default(Utf8CaptureRange);
        var literal = _literal;
        if (literal is not { Length: > 0 }) return Utf8ExecutionLimitKind.None;
        var reverse = _mode is EnumeratorMode.RtlExactAsciiLiteral or
            EnumeratorMode.RtlAsciiLiteralIgnoreCase or EnumeratorMode.RtlExactUtf8Literal;
        ref readonly var plan = ref _searchPlan;
        var breach = Utf8SearchExecutor.TryFindLiteral(plan, _input, reverse ? 0 : _consumed,
            reverse ? _remaining.Length : _input.Length, reverse, ref _sliceInvocation, out var index);
        if (breach != Utf8ExecutionLimitKind.None || index < 0) return breach;
        var nextBytes = (index) + (literal.Length);
        breach = Utf8SliceBufferWork.TryCreateMatchRange(index, (index) + (literal.Length), ref _sliceInvocation, out var match);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _current = match;
        if (reverse) _remaining = _input[..index];
        else
        {
            _consumed = nextBytes;
            _remaining = _input[_consumed..];
        }
        found = true;
        return Utf8ExecutionLimitKind.None;
    }

    private bool MoveNextExactUtf8Literals()
    {
        var previous = _current;
        try
        {
            var breach = TryMoveNextLiteralFamily(out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal-family execution exceeded {breach}.");
            return found;
        }
        catch { if (_preserveOffsetCurrent) _current = previous; throw; }
    }

    private bool MoveNextAsciiIgnoreCaseLiterals()
        => MoveNextExactUtf8Literals();

    internal Utf8ExecutionLimitKind TryMoveNextLiteralFamily(out bool found)
    {
        found = false;
        _current = default(Utf8CaptureRange);
        var pendingState = _multiLiteralScanState;
        ref readonly var searchPlan = ref _searchPlan;
        var breach = Utf8BackendInstructionExecutor.TryFindNextLiteralFamilyMatch(searchPlan, _program,
            _input, ref pendingState, ref _sliceInvocation, out var match, out var discovered);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (!discovered)
        {
            _multiLiteralScanState = pendingState;
            return Utf8ExecutionLimitKind.None;
        }
        var bytes = (match.Index) + (match.Length);
        breach = Utf8SliceBufferWork.TryCreateMatchRange(match.Index, (match.Index) + (match.Length), ref _sliceInvocation, out var projected);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _multiLiteralScanState = pendingState;
        _current = projected;
        _consumed = bytes;
        _remaining = _input[_consumed..];
        found = true;
        return Utf8ExecutionLimitKind.None;
    }

    private bool MoveNextSmallAsciiLiteralFamily()
    {
        var nextStart = _consumed;
        if (!_smallAsciiLiteralFamilySearch.TryFindNextNonOverlapping(
            _input, ref nextStart, out var matchIndex, out var matchLength))
        {
            return false;
        }

        _consumed = nextStart;
        _remaining = _input[nextStart..];
        _current = new Utf8CaptureRange(matchIndex, matchIndex + matchLength);
        return true;
    }

    private bool MoveNextEmptyLiteral()
    {
        var breach = _sliceInvocation.CheckInput();
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException($"Empty scalar cursor exceeded {breach}.");
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        if (_consumed < 0)
        {
            _current = default(Utf8CaptureRange);
            return false;
        }

        _current = new Utf8CaptureRange(_consumed, (_consumed) + 0);
        if (_consumed == _input.Length)
            _consumed = -1;
        else
        {
            if (Rune.DecodeFromUtf8(_input[_consumed..], out var scalar, out var width) != OperationStatus.Done)
                throw Utf8Validation.CreateInvalidUtf8Exception(_consumed);
            _consumed += width;
        }
        return true;
    }

    private enum EnumeratorMode : byte
    {
        Exhausted = 0, ExactAsciiLiteral = 1, ExactUtf8Literal = 2, ExactUtf8Literals = 3, AsciiLiteralIgnoreCase = 4, AsciiLiteralIgnoreCaseLiterals = 5, EmptyLiteral = 10, SmallAsciiLiteralFamily = 11, OwnedSlice = 14, AnchoredAsciiLiteralPrefix = 15, AnchoredAsciiLiteralSuffix = 16, AnchoredAsciiLiteralSuffixStrictEnd = 17, AnchoredFoldedAsciiLiteralPrefix = 18, AnchoredFoldedAsciiLiteralSuffix = 19, AnchoredFoldedAsciiLiteralSuffixStrictEnd = 20, RtlExactAsciiLiteral = 22, RtlAsciiLiteralIgnoreCase = 23, RtlExactUtf8Literal = 24
    }
}
