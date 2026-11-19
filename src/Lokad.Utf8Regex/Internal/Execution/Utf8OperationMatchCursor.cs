using System.Diagnostics.CodeAnalysis;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

internal ref partial struct Utf8OperationMatchCursor
{
    internal static int DebugSizeInBytes => Unsafe.SizeOf<Utf8OperationMatchCursor>();

    [UnscopedRef]
    internal Utf8InvocationBufferControl WorkingBufferControl => new(ref _sliceInvocation);

    internal void CheckCancellationAtEntry() => _sliceInvocation.Budget.ThrowIfCancellationRequested();

    internal ulong DebugInvocationSteps => _sliceInvocation.Budget.Steps;

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

    internal static int DebugSearchPlanSizeInBytes => Unsafe.SizeOf<Utf8SearchPlan>();

    internal static int DebugSmallLiteralFamilySizeInBytes => Unsafe.SizeOf<PreparedSmallAsciiLiteralFamilySearch>();

    internal static int DebugSearchOperationPlanSizeInBytes => Unsafe.SizeOf<Utf8SearchOperationPlan>();

    private readonly EnumeratorMode _mode;
    private readonly Utf8OperationMatchCursorPlans _plans;
    private readonly PreparedSmallAsciiLiteralFamilySearch _smallAsciiLiteralFamilySearch;
    private readonly byte[]? _literal;
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8ExecutionDeadline _budget;
    private readonly Utf8SearchOperationPlan _program;
    private ReadOnlySpan<byte> _remaining;
    private int _consumed;
    private PreparedMultiLiteralScanState _multiLiteralScanState;
    private Utf8CaptureRange _current;
    private int _baseByteOffset;
    private Utf8SliceProgram? _sliceProgram;
    private Utf8NonBacktrackingSearchState? _nonBacktrackingState;
    private int _nonBacktrackingBoundary;
    // Native discovery/publication and the owned VM retain one invocation.
    private Utf8SliceInvocation _sliceInvocation;
    private bool _preserveOffsetCurrent;
    private bool _anchoredExhausted;
    private int _slicePositionByte;
    private int _sliceOriginByte;

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, byte[] literal, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _literal = literal;
        _input = input;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _program = default;
        _remaining = input;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _baseByteOffset = 0;
        _mode = executionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => EnumeratorMode.ExactAsciiLiteral,
            NativeExecutionKind.AsciiLiteralIgnoreCase => EnumeratorMode.AsciiLiteralIgnoreCase,
            _ => EnumeratorMode.Exhausted
        };
    }

    // Transfer invocation ownership exactly once when constructing a literal
    // cursor. Entry admission and subsequent search share the same charged work.
    internal Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, bool rightToLeft, Utf8SliceInvocation invocation)
        : this(input, regexPlan, regexPlan.LiteralUtf8 ?? throw new InvalidOperationException("A literal cursor requires a literal."), regexPlan.ExecutionKind, invocation.Budget.Deadline)
    {
        _sliceInvocation = invocation;
        _mode = (regexPlan.ExecutionKind, rightToLeft) switch
        {
            (NativeExecutionKind.ExactAsciiLiteral, false) => EnumeratorMode.ExactAsciiLiteral,
            (NativeExecutionKind.ExactUtf8Literal, false) => EnumeratorMode.ExactUtf8Literal,
            (NativeExecutionKind.AsciiLiteralIgnoreCase, false) => EnumeratorMode.AsciiLiteralIgnoreCase,
            (NativeExecutionKind.ExactAsciiLiteral, true) => EnumeratorMode.RtlExactAsciiLiteral,
            (NativeExecutionKind.ExactUtf8Literal, true) => EnumeratorMode.RtlExactUtf8Literal,
            (NativeExecutionKind.AsciiLiteralIgnoreCase, true) => EnumeratorMode.RtlAsciiLiteralIgnoreCase,
            _ => throw new InvalidOperationException("The plan is not a single literal.")
        };
    }

    // Right-to-left twin of the literal cursor above: the same literal, search
    // plan, and budget iterate matches from right to left instead. Only exact
    // ASCII literals (plain or folded) qualify; the divert guarantees a nonempty
    // literal, no boundary or trailing requirements, and no culture or Kelvin
    // fences, so the arms below stay branch-free.
    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, byte[] literal, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _literal = literal;
        _input = input;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _program = default;
        _remaining = input;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _baseByteOffset = 0;
        _mode = (executionKind, rightToLeft) switch
        {
            (NativeExecutionKind.ExactAsciiLiteral, true) => EnumeratorMode.RtlExactAsciiLiteral,
            (NativeExecutionKind.ExactUtf8Literal, true) => EnumeratorMode.RtlExactUtf8Literal,
            (NativeExecutionKind.AsciiLiteralIgnoreCase, true) => EnumeratorMode.RtlAsciiLiteralIgnoreCase,
            _ => EnumeratorMode.Exhausted
        };
    }

    public Utf8CaptureRange CurrentValueMatch => ApplyBaseOffsets(_current);

    internal Utf8OperationMatchCursor(Utf8SliceProgram program, ReadOnlySpan<byte> input, Utf8SliceInvocation invocation)
        : this(program, input, invocation, program.RightToLeft ? input.Length : 0)
    {
    }

    internal Utf8OperationMatchCursor(Utf8SliceProgram program, ReadOnlySpan<byte> input, Utf8SliceInvocation invocation, int startByte)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _literal = null;
        _input = input;
        _budget = Utf8ExecutionDeadline.Infinite;
        _program = default;
        _remaining = default;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _baseByteOffset = 0;
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
                _nonBacktrackingBoundary = state.ResolveByteOffset(startByte);
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

    // Byte starts retain the original subject for assertions and scalar progress.
    internal Utf8OperationMatchCursor(
        Utf8SliceProgram program, ReadOnlySpan<byte> input, TimeSpan timeout, CancellationToken token, int startByte)
        : this(program, input, new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, timeout, token), startByte)
    {
    }

    private bool MoveNextOwnedSlice() => MoveNextOwnedSliceBytes(out _);

    private bool MoveNextOwnedSliceBytes(out Utf8CaptureRange range)
    {
        range = default;
        var slots = new Utf8SliceSlots([], []);
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

    // Anchored ASCII literal prefix enumeration: at most one match at offset 0,
    // byte-exact on every input.
    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, byte[] literal, Utf8ExecutionDeadline budget)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _literal = literal;
        _input = input;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _program = default;
        _remaining = input;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _baseByteOffset = 0;
        _sliceProgram = null;
        _slicePositionByte = 0;
        _sliceOriginByte = 0;
        _mode = EnumeratorMode.AnchoredAsciiLiteralPrefix;
    }

    // Anchored ASCII literal suffix enumeration: at most one match at the input
    // end (or right before a single trailing line feed for the lax kind).
    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input, byte[] literal, Utf8ExecutionDeadline budget, Utf8AnchoredLiteralKind familyKind)
        : this(input, literal, budget)
    {
        // The mode carries strictness so verification needs no extra fields.
        _mode = familyKind switch
        {
            Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffixStrictEnd => EnumeratorMode.AnchoredAsciiLiteralSuffixStrictEnd,
            Utf8AnchoredLiteralKind.AnchoredAsciiLiteralSuffix => EnumeratorMode.AnchoredAsciiLiteralSuffix,
            Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix => EnumeratorMode.AnchoredFoldedAsciiLiteralPrefix,
            Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix => EnumeratorMode.AnchoredFoldedAsciiLiteralSuffix,
            Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd => EnumeratorMode.AnchoredFoldedAsciiLiteralSuffixStrictEnd,
            _ => EnumeratorMode.Exhausted
        };
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

            if (!Utf8AnchoredLiteralPrefixExecutor.FoldedSequenceEquals(_input[..foldedPrefixLiteral.Length], foldedPrefixLiteral))
            {
                return false;
            }

            _current = new Utf8CaptureRange(0, 0 + (foldedPrefixLiteral.Length));
            return true;
        }

        // Anchored suffix verification mirrors Utf8AnchoredLiteralSuffixExecutor
        // inline: the cursor keeps no plan reference, and the mode carries lax versus
        // strict end handling. Results retain the original byte endpoints.
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
            if (foldedSuffixStart < 0 || !Utf8AnchoredLiteralPrefixExecutor.FoldedSequenceEquals(_input.Slice(foldedSuffixStart, foldedSuffixLiteral.Length), foldedSuffixLiteral))
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

    internal bool TryMoveNextSmallAsciiLiteralFamilyCoordinates(out int matchIndex, out int matchLength)
    {
        if (_mode != EnumeratorMode.SmallAsciiLiteralFamily)
        {
            matchIndex = -1;
            matchLength = 0;
            return false;
        }

        var nextStart = _consumed;
        if (!_smallAsciiLiteralFamilySearch.TryFindNextNonOverlapping(
                _input,
                ref nextStart,
                out matchIndex,
                out matchLength))
        {
            return false;
        }

        _consumed = nextStart;
        _remaining = _input[nextStart..];
        return true;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, byte[] literal, Utf8ExecutionDeadline budget)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _literal = literal;
        _input = input;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _program = searchPlan.SearchOperation;
        _remaining = input;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _mode = EnumeratorMode.ExactUtf8Literal;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, Utf8ExecutionDeadline budget)
    {
        this = new Utf8OperationMatchCursor(input, regexPlan, NativeExecutionKind.ExactUtf8Literals, budget);
    }

    internal void CheckOwnedSliceCursorBoundary()
    {
        var breach = _sliceInvocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Owned slice cursor exceeded {breach}.");
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
    }

    // Literal-family cursors keep one original invocation.
    internal Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, Utf8SliceInvocation invocation)
        : this(input, regexPlan, regexPlan.ExecutionKind, invocation.Budget.Deadline)
    {
        _sliceInvocation = invocation;
    }

    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _literal = null;
        _input = input;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _program = searchPlan.SearchOperation;
        _remaining = input;
        _consumed = 0;
        _multiLiteralScanState = default;
        _current = default(Utf8CaptureRange);
        _baseByteOffset = 0;
        _mode = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
            ? EnumeratorMode.AsciiLiteralIgnoreCaseLiterals
            : EnumeratorMode.ExactUtf8Literals;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
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
        _baseByteOffset = 0;
        _mode = EnumeratorMode.EmptyLiteral;
    }

    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input, PreparedSmallAsciiLiteralFamilySearch smallAsciiLiteralFamilySearch, Utf8ExecutionDeadline budget)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
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
        _baseByteOffset = 0;
        _mode = EnumeratorMode.SmallAsciiLiteralFamily;
    }

    // R05: offset enumeration starts the scan at the floor unit over the full input so
    // line anchors keep their true left context; at floor zero this is the default state.
    public Utf8OperationMatchCursor WithStartFloor(int startByteOffset)
    {
        if (_sliceProgram is not null)
        {
            if ((uint)startByteOffset > (uint)_input.Length ||
                startByteOffset < _input.Length && (_input[startByteOffset] & 0xC0) == 0x80)
                throw new ArgumentOutOfRangeException(nameof(startByteOffset));
            _slicePositionByte = startByteOffset;
            _sliceOriginByte = startByteOffset;
            if (_nonBacktrackingState is { } batch)
                _nonBacktrackingBoundary = batch.ResolveByteOffset(startByteOffset);
        }
        _preserveOffsetCurrent = true;
        _remaining = _input[startByteOffset..];
        _consumed = startByteOffset;
        _multiLiteralScanState = new PreparedMultiLiteralScanState(startByteOffset, startByteOffset, 0);
        _anchoredExhausted = false;
        return this;
    }

    public Utf8OperationMatchCursor WithBaseOffset(int byteOffset)
    {
        _baseByteOffset = byteOffset;
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
        ref readonly var plan = ref _plans.SearchPlan;
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
        ref readonly var searchPlan = ref _plans.SearchPlan;
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
        if (!TryMoveNextSmallAsciiLiteralFamilyCoordinates(out var matchIndex, out var matchLength))
        {
            return false;
        }

        _current = new Utf8CaptureRange(matchIndex, (matchIndex) + (matchLength));
        return true;
    }

    private bool MoveNextEmptyLiteral()
    {
        var breach = _sliceInvocation.TryChargeInputStep();
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



    private Utf8CaptureRange ApplyBaseOffsets(Utf8CaptureRange match)
    {
        if (!match.Success || _baseByteOffset == 0)
        {
            return match;
        }

        return new Utf8CaptureRange(_baseByteOffset + match.StartOffsetInBytes,
            _baseByteOffset + match.EndOffsetInBytes);
    }

    private enum EnumeratorMode : byte
    {
        Exhausted = 0, ExactAsciiLiteral = 1, ExactUtf8Literal = 2, ExactUtf8Literals = 3, AsciiLiteralIgnoreCase = 4, AsciiLiteralIgnoreCaseLiterals = 5, EmptyLiteral = 10, SmallAsciiLiteralFamily = 11, OwnedSlice = 14, AnchoredAsciiLiteralPrefix = 15, AnchoredAsciiLiteralSuffix = 16, AnchoredAsciiLiteralSuffixStrictEnd = 17, AnchoredFoldedAsciiLiteralPrefix = 18, AnchoredFoldedAsciiLiteralSuffix = 19, AnchoredFoldedAsciiLiteralSuffixStrictEnd = 20, RtlExactAsciiLiteral = 22, RtlAsciiLiteralIgnoreCase = 23, RtlExactUtf8Literal = 24
    }
}
