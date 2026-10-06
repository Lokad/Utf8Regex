using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

internal ref partial struct Utf8OperationMatchCursor
{
    internal static int DebugSizeInBytes => Unsafe.SizeOf<Utf8OperationMatchCursor>();

    internal ulong DebugInvocationSteps => _sliceInvocation.Budget.Steps;

    internal ulong DebugInvocationReservedBytes => _sliceInvocation.Ledger.ReservedBytes;

    internal bool DebugIsOwnedSlice => _mode == EnumeratorMode.OwnedSlice;

    internal Utf8StructuralSearchState DebugStructuralSearchState => _structuralSearchState;

    internal Utf8AsciiDeterministicScanState DebugDeterministicScanState => _deterministicScanState;

    internal (int Bytes, int Utf16) DebugConsumed => (_consumed, _consumedUtf16);

    internal static int DebugSimplePatternPlanSizeInBytes => Unsafe.SizeOf<AsciiSimplePatternPlan>();

    internal static int DebugStructuralLinearProgramSizeInBytes => Unsafe.SizeOf<Utf8StructuralLinearProgram>();

    internal static int DebugSearchPlanSizeInBytes => Unsafe.SizeOf<Utf8SearchPlan>();

    internal static int DebugSmallLiteralFamilySizeInBytes => Unsafe.SizeOf<PreparedSmallAsciiLiteralFamilySearch>();

    internal static int DebugSearchOperationPlanSizeInBytes => Unsafe.SizeOf<Utf8SearchOperationPlan>();

    internal static int DebugDeterministicScanStateSizeInBytes => Unsafe.SizeOf<Utf8AsciiDeterministicScanState>();

    private readonly EnumeratorMode _mode;
    private readonly Utf8OperationMatchCursorPlans _plans;
    private readonly PreparedSmallAsciiLiteralFamilySearch _smallAsciiLiteralFamilySearch;
    private readonly Utf8EmittedKernelMatcher? _emittedKernelMatcher;
    private readonly int[]? _alternateLiteralUtf16Lengths;
    private readonly bool _asciiInput;
    private readonly byte[]? _literal;
    private readonly ReadOnlySpan<byte> _input;
    private Utf8BoundaryMap? _boundaryMap;
    private ulong _boundaryMapReservedBytes;
    private bool _retainBoundaryMapReservation;
    private readonly Utf8ExecutionDeadline _budget;
    private readonly int _literalUtf16Length;
    private readonly int _totalUtf16Length;
    private readonly Utf8ProjectionPlan _projectionPlan;
    private readonly Utf8SearchOperationPlan _program;
    private Regex.ValueMatchEnumerator _fallbackEnumerator;
    private ReadOnlySpan<byte> _remaining;
    private int _consumed;
    private int _consumedUtf16;
    private PreparedMultiLiteralScanState _multiLiteralScanState;
    private Utf8AsciiDeterministicScanState _deterministicScanState;
    private Utf8ValueMatch _current;
    private int _lastLiteralFamilyBranchId;
    private int _baseByteOffset;
    private int _baseUtf16Offset;
    private Utf8SliceProgram? _sliceProgram;
    // Owned VM state and native projection share this cursor's invocation.
    // Literal discovery and projection use one state; other native families
    // still need scan-wide token/finite-policy routing and accounting.
    private Utf8SliceInvocation _sliceInvocation;
    private bool _nativeStructuralExhausted;
    private bool _preserveOffsetCurrent;
    private Utf8StructuralSearchState _structuralSearchState;
    private int _sliceUnit;
    private int _sliceOriginUnit;
    private string? _decodedUnits;
    private int _unitPosition;

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, byte[] literal, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget, bool asciiInput)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _asciiInput = asciiInput;
        _literal = literal;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _literalUtf16Length = literal.Length;
        // This nonempty-literal mode projects incrementally and never uses the
        // whole-subject UTF-16 length owned by the empty-literal mode.
        _totalUtf16Length = 0;
        _projectionPlan = default;
        _program = default;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = executionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => EnumeratorMode.ExactAsciiLiteral,
            NativeExecutionKind.AsciiLiteralIgnoreCase => EnumeratorMode.AsciiLiteralIgnoreCase,
            _ => EnumeratorMode.Exhausted,
        };
    }

    // Transfer invocation ownership exactly once when constructing a literal
    // cursor. Validation already performed by its caller keeps its charged work.
    internal Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan,
        bool asciiInput, bool rightToLeft, Utf8SliceInvocation invocation)
        : this(input, regexPlan, regexPlan.LiteralUtf8 ?? throw new InvalidOperationException("A literal cursor requires a literal."),
            regexPlan.ExecutionKind, invocation.Budget.Deadline, asciiInput)
    {
        _sliceInvocation = invocation;
        _literalUtf16Length = regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal
            ? Utf8Validation.Validate(_literal, ref _sliceInvocation).Utf16Length : _literalUtf16Length;
        _mode = (regexPlan.ExecutionKind, rightToLeft) switch
        {
            (NativeExecutionKind.ExactAsciiLiteral, false) => EnumeratorMode.ExactAsciiLiteral,
            (NativeExecutionKind.ExactUtf8Literal, false) => EnumeratorMode.ExactUtf8Literal,
            (NativeExecutionKind.AsciiLiteralIgnoreCase, false) => EnumeratorMode.AsciiLiteralIgnoreCase,
            (NativeExecutionKind.ExactAsciiLiteral, true) => EnumeratorMode.RtlExactAsciiLiteral,
            (NativeExecutionKind.ExactUtf8Literal, true) => EnumeratorMode.RtlExactUtf8Literal,
            (NativeExecutionKind.AsciiLiteralIgnoreCase, true) => EnumeratorMode.RtlAsciiLiteralIgnoreCase,
            _ => throw new InvalidOperationException("The plan is not a single literal."),
        };
    }

    // Right-to-left twin of the literal cursor above: the same literal, search
    // plan, and budget iterate matches from right to left instead. Only exact
    // ASCII literals (plain or folded) qualify; the divert guarantees a nonempty
    // literal, no boundary or trailing requirements, and no culture or Kelvin
    // fences, so the arms below stay branch-free.
    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, byte[] literal, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget, bool asciiInput, bool rightToLeft)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _asciiInput = asciiInput;
        _literal = literal;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _literalUtf16Length = executionKind == NativeExecutionKind.ExactUtf8Literal
            ? Utf8Validation.Validate(literal, ref _sliceInvocation).Utf16Length
            : literal.Length;
        _totalUtf16Length = 0;
        _projectionPlan = default;
        _program = default;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = (executionKind, rightToLeft) switch
        {
            (NativeExecutionKind.ExactAsciiLiteral, true) => EnumeratorMode.RtlExactAsciiLiteral,
            (NativeExecutionKind.ExactUtf8Literal, true) => EnumeratorMode.RtlExactUtf8Literal,
            (NativeExecutionKind.AsciiLiteralIgnoreCase, true) => EnumeratorMode.RtlAsciiLiteralIgnoreCase,
            _ => EnumeratorMode.Exhausted,
        };
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, string decoded, Regex regex, Utf8BoundaryMap? boundaryMap)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = null;
        _literal = null;
        _input = input;
        _boundaryMap = boundaryMap;
        _budget = Utf8ExecutionDeadline.Infinite;
        _literalUtf16Length = 0;
        _totalUtf16Length = boundaryMap?.Utf16Length ?? input.Length;
        _projectionPlan = boundaryMap is null
            ? default
            : new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16BoundaryMap);
        _program = default;
        _fallbackEnumerator = regex.EnumerateMatches(decoded);
        _remaining = default;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = boundaryMap is null
            ? EnumeratorMode.FallbackRegexAscii
            : EnumeratorMode.FallbackRegex;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Regex regex, string decoded, int startAt, Utf8BoundaryMap? boundaryMap)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = null;
        _literal = null;
        _input = input;
        _boundaryMap = boundaryMap;
        _budget = Utf8ExecutionDeadline.Infinite;
        _literalUtf16Length = 0;
        _totalUtf16Length = boundaryMap?.Utf16Length ?? input.Length;
        _projectionPlan = boundaryMap is null
            ? default
            : new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16BoundaryMap);
        _program = default;
        _fallbackEnumerator = regex.EnumerateMatches(decoded.AsSpan(), startAt);
        _remaining = default;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = boundaryMap is null
            ? EnumeratorMode.FallbackRegexAscii
            : EnumeratorMode.FallbackRegex;
    }

    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input,
        Utf8PreparedRegex regexPlan,
        bool useStructuralLinearProgram,
        Utf8ExecutionDeadline budget,
        bool asciiInput)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        ref readonly var structuralLinearProgram = ref regexPlan.StructuralLinearProgram;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = null;
        _literal = null;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _literalUtf16Length = 0;
        _asciiInput = asciiInput;
        _totalUtf16Length = input.Length;
        _projectionPlan = useStructuralLinearProgram ? default : searchPlan.ProjectionPlan;
        _program = useStructuralLinearProgram ? default : searchPlan.EnumerationOperation;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = useStructuralLinearProgram
            ? (structuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiOrderedLiteralWindow
                ? EnumeratorMode.AsciiOrderedLiteralWindow
                : structuralLinearProgram.DeterministicProgram.FixedWidthLength > 0
                ? EnumeratorMode.AsciiFixedTokenPattern
                : EnumeratorMode.AsciiDeterministicPattern)
            : EnumeratorMode.AsciiSimplePattern;
    }

    public Utf8ValueMatch CurrentValueMatch => ApplyBaseOffsets(_current);

    // Transfer the already validated caller invocation into a simple-pattern
    // cursor, retaining work counters, deadline, token and borrowed reservations.
    internal Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan,
        Utf8SliceInvocation invocation, bool asciiInput)
        : this(input, regexPlan, regexPlan.StructuralLinearProgram.DeterministicProgram.HasValue,
            invocation.Budget.Deadline, asciiInput)
    {
        _sliceInvocation = invocation;
    }

    public Utf8OperationMatch Current
        => new(CurrentValueMatch, branchId: 0, captureSlots: null);

    /// <summary>Gets the alternate index of the last literal match, or -1 when the last
    /// successful move did not come from a literal search. Singular literals always report 0.</summary>
    internal int LastLiteralFamilyBranchId =>
        _mode is EnumeratorMode.ExactAsciiLiteral or EnumeratorMode.ExactUtf8Literal or EnumeratorMode.ExactUtf8Literals or EnumeratorMode.AsciiLiteralIgnoreCase or EnumeratorMode.AsciiLiteralIgnoreCaseLiterals or EnumeratorMode.RtlExactAsciiLiteral or EnumeratorMode.RtlAsciiLiteralIgnoreCase or EnumeratorMode.RtlExactUtf8Literal
            ? _lastLiteralFamilyBranchId
            : -1;

    public Utf8OperationMatchCursor(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        TimeSpan timeout,
        CancellationToken token)
        : this(program, input, map, new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, timeout, token))
    {
    }

    internal Utf8OperationMatchCursor(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        Utf8BoundaryMap map, Utf8SliceInvocation invocation)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = null;
        _literal = null;
        _input = input;
        _boundaryMap = map;
        _budget = Utf8ExecutionDeadline.Infinite;
        _literalUtf16Length = 0;
        _totalUtf16Length = map.Utf16Length;
        _projectionPlan = new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16BoundaryMap);
        _program = default;
        _fallbackEnumerator = default;
        _remaining = default;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _lastLiteralFamilyBranchId = 0;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _sliceProgram = program;
        _sliceInvocation = invocation;
        _sliceInvocation.ShareStorageForCursor();
        _sliceUnit = 0;
        _sliceOriginUnit = -1;
        _mode = EnumeratorMode.OwnedSlice;
    }

    // Entry work and the map reservation move together into the cursor. Split
    // enumeration still needs its map for tail/result projection after EOF.
    internal static Utf8OperationMatchCursor CreateMapped(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        Utf8ValidationResult validation, scoped ref Utf8SliceInvocation invocation, bool retainMap,
        out Utf8BoundaryMap map)
    {
        invocation.ShareStorageForCursor();
        map = Utf8BoundaryMap.Create(input, validation, ref invocation, out var reservation);
        try
        {
            map = map.WithStorageLease(invocation.Ledger.CreateLease(reservation));
            var cursor = new Utf8OperationMatchCursor(program, input, map, invocation);
            cursor._boundaryMapReservedBytes = reservation;
            cursor._retainBoundaryMapReservation = retainMap;
            return cursor;
        }
        catch
        {
            if (map.StorageLease is { } lease) lease.AbortBeforePublication();
            else invocation.Ledger.Release(reservation);
            map = default;
            throw;
        }
    }

    internal static Utf8OperationMatchCursor CreateMappedFromUnit(Utf8SliceProgram program, ReadOnlySpan<byte> input,
        scoped ref Utf8BoundaryMap map, scoped ref Utf8SliceInvocation invocation, ulong reservation, int startUnit)
    {
        invocation.ShareStorageForCursor();
        try
        {
            map = map.WithStorageLease(invocation.Ledger.CreateLease(reservation));
            var cursor = new Utf8OperationMatchCursor(program, input, map, invocation);
            cursor._boundaryMapReservedBytes = reservation;
            cursor._sliceUnit = startUnit;
            cursor._sliceOriginUnit = startUnit;
            return cursor;
        }
        catch
        {
            if (map.StorageLease is { } lease) lease.AbortBeforePublication();
            else invocation.Ledger.Release(reservation);
            map = default;
            throw;
        }
    }

    internal void ReleaseOwnedBoundaryMap()
    {
        if (_retainBoundaryMapReservation) return;
        if (_boundaryMap?.StorageLease is null)
            _sliceInvocation.Ledger.Release(_boundaryMapReservedBytes);
        _boundaryMap = null;
        _boundaryMapReservedBytes = 0;
    }

    // From-unit slice enumeration starts the scan at the caller's start unit
    // on full input (anchors keep true left context, \G pins to the scan
    // start like BCL startat); the origin pin takes the scan start up front so
    // right-to-left scans descend from the caller bound (matches ending at or
    // below it, rightmost first) instead of resetting to the input end.
    internal Utf8OperationMatchCursor(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        TimeSpan timeout,
        CancellationToken token,
        int startUnit)
        : this(program, input, map, timeout, token)
    {
        _sliceUnit = startUnit;
        _sliceOriginUnit = startUnit;
    }

    private bool MoveNextOwnedSlice()
    {
        var slots = new Utf8SliceSlots([], []);
        try
        {
            // A new MoveNext and result publication are operation boundaries,
            // even when a short VM attempt never reaches its polling cadence.
            CheckOwnedSliceCursorBoundary();
            if (_sliceProgram is null || _boundaryMap is null) return false;
            var projectionMap = _boundaryMap.Value;
            var map = projectionMap;
            slots = Utf8SliceRunner.RentSlots(_sliceProgram.GroupCount, ref _sliceInvocation);
            // Right-to-left enumeration descends closing edges from the search
            // bound (BCL runtextstart): non-empty matches chain the bound to
            // their start while empty matches step one unit left, mirroring the
            // ascending advance above. The pin only moves on non-empty matches,
            // so an empty match ends a contiguous chain exactly like forward.
            if (_sliceProgram.RightToLeft)
            {
                if (_sliceOriginUnit < 0)
                {
                    _sliceUnit = map.Utf16Length;
                    _sliceOriginUnit = _sliceUnit;
                }

                if (_sliceOriginUnit >= 0 && _sliceOriginUnit <= map.Utf16Length)
                {
                    _sliceInvocation.AnchorOriginByte = map.Resolve(_sliceOriginUnit).ByteOffset;
                }

                while (_sliceUnit >= 0)
                {
                    var rtlOutcome = Utf8SliceRunner.TryMatchAtRightEdgeUnit(
                        _sliceProgram, _input, map, _sliceUnit, slots.Starts, slots.Ends, ref _sliceInvocation);
                    if (rtlOutcome.Breach != Utf8ExecutionLimitKind.None)
                    {
                        throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
                    }

                    if (!rtlOutcome.Found)
                    {
                        _sliceUnit--;
                        continue;
                    }

                    var projected = Utf8SliceRunner.ProjectMatch(projectionMap, rtlOutcome);
                    CheckOwnedSliceCursorBoundary();
                    _current = projected;
                    _sliceUnit = rtlOutcome.EndUnit > rtlOutcome.StartUnit ? rtlOutcome.StartUnit : rtlOutcome.EndUnit - 1;
                    if (rtlOutcome.EndUnit > rtlOutcome.StartUnit)
                    {
                        _sliceOriginUnit = rtlOutcome.StartUnit;
                    }

                    return true;
                }

                CheckOwnedSliceCursorBoundary();
                ReleaseOwnedBoundaryMap();
                _sliceProgram = null;
                return false;
            }
            // Contiguous matches chain BCL startat: a fresh scan pins \\G to its
            // start, a non-empty match advances the pin to its end, and an empty
            // match keeps the pin (later positions all miss it, ending the chain).
            if (_sliceOriginUnit < 0)
            {
                _sliceOriginUnit = _sliceUnit;
            }

            if (_sliceOriginUnit >= 0 && _sliceOriginUnit <= map.Utf16Length)
            {
                _sliceInvocation.AnchorOriginByte = map.Resolve(_sliceOriginUnit).ByteOffset;
            }

            while (_sliceUnit <= map.Utf16Length)
            {
                var outcome = Utf8SliceRunner.TryMatchFromUnit(
                    _sliceProgram, _input, map, _sliceUnit, slots.Starts, slots.Ends, ref _sliceInvocation);
                if (outcome.Breach != Utf8ExecutionLimitKind.None)
                {
                    throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
                }

                if (!outcome.Found)
                {
                    _sliceUnit++;
                    continue;
                }

                var projected = Utf8SliceRunner.ProjectMatch(projectionMap, outcome);
                CheckOwnedSliceCursorBoundary();
                _current = projected;
                _sliceUnit = outcome.EndUnit > outcome.StartUnit ? outcome.EndUnit : outcome.StartUnit + 1;
                if (outcome.EndUnit > outcome.StartUnit)
                {
                    _sliceOriginUnit = outcome.EndUnit;
                }

                return true;
            }

            CheckOwnedSliceCursorBoundary();
            ReleaseOwnedBoundaryMap();
            _sliceProgram = null;
            return false;
        }
        catch
        {
            ReleaseOwnedBoundaryMap();
            _sliceProgram = null;
            throw;
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref _sliceInvocation);
        }
    }

    // Anchored ASCII literal prefix enumeration: at most one match at offset 0,
    // byte-exact on every input. The constructor below is public (not a counted
    // private helper) and the move-next logic stays inline here to hold the
    // single-caller debt ceiling.
    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, byte[] literal, Utf8ExecutionDeadline budget)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = null;
        _literal = literal;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _literalUtf16Length = literal.Length;
        _totalUtf16Length = input.Length;
        _projectionPlan = default;
        _program = default;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _lastLiteralFamilyBranchId = 0;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _sliceProgram = null;
        _sliceUnit = 0;
        _sliceOriginUnit = 0;
        _emittedKernelMatcher = null;
        _mode = EnumeratorMode.AnchoredAsciiLiteralPrefix;
    }

    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input,
        Utf8PreparedRegex regexPlan,
        Utf8BoundaryMap boundaryMap,
        Utf8ExecutionDeadline budget)
    {
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = null;
        _asciiInput = false;
        _literal = null;
        _input = input;
        _boundaryMap = boundaryMap;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _literalUtf16Length = 0;
        _totalUtf16Length = input.Length;
        _projectionPlan = default;
        _program = default;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _lastLiteralFamilyBranchId = 0;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _sliceProgram = null;
        _sliceUnit = 0;
        _sliceOriginUnit = 0;
        _emittedKernelMatcher = null;
        _mode = EnumeratorMode.AsciiSimplePatternScalar;
    }

    // R05: astral unit enumeration walks decoded units (halves included) with unit
    // positions from the start unit; construction sites qualify up front via the
    // unit verifier. A nonzero floor keeps origin semantics for FromOffset calls:
    // unit shapes carry no left context, so matches at or after the floor are
    // identical to a scan from zero.
    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input,
        Utf8PreparedRegex regexPlan,
        Utf8BoundaryMap boundaryMap,
        string decodedUnits,
        Utf8ExecutionDeadline budget,
        int startUnit)
        : this(input, regexPlan, boundaryMap, budget)
    {
        _decodedUnits = decodedUnits;
        _unitPosition = startUnit;
        _mode = EnumeratorMode.AsciiSimplePatternUnit;
    }

    // Anchored ASCII literal suffix enumeration: at most one match at the input
    // end (or right before a single trailing line feed for the lax kind). The
    // kind-based constructor below is public (not a counted private helper) and
    // the move-next logic stays inline here to hold the single-caller debt ceiling.
    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input,
        byte[] literal,
        Utf8ExecutionDeadline budget,
        Utf8FallbackDirectFamilyKind familyKind)
        : this(input, literal, budget)
    {
        // The mode carries strictness so verification needs no extra fields.
        _mode = familyKind switch
        {
            Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffixStrictEnd => EnumeratorMode.AnchoredAsciiLiteralSuffixStrictEnd,
            Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralSuffix => EnumeratorMode.AnchoredAsciiLiteralSuffix,
            Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix => EnumeratorMode.AnchoredFoldedAsciiLiteralPrefix,
            Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix => EnumeratorMode.AnchoredFoldedAsciiLiteralSuffix,
            Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd => EnumeratorMode.AnchoredFoldedAsciiLiteralSuffixStrictEnd,
            _ => EnumeratorMode.Exhausted,
        };
    }

    public bool MoveNext()
    {
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

            _current = new Utf8ValueMatch(true, true, 0, literal.Length, 0, literal.Length);
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

            if (!Utf8AsciiAnchoredPrefixFamilyExecutor.FoldedSequenceEquals(_input[..foldedPrefixLiteral.Length], foldedPrefixLiteral))
            {
                return false;
            }

            _current = new Utf8ValueMatch(true, true, 0, foldedPrefixLiteral.Length, 0, foldedPrefixLiteral.Length);
            return true;
        }

        // Anchored suffix verification mirrors Utf8AsciiAnchoredSuffixFamilyExecutor
        // inline: the cursor keeps no plan reference, and the mode carries lax versus
        // strict end handling. Only the start index needs UTF-16 mapping on
        // non-ASCII input; ASCII literal lengths match in both encodings.
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

            var suffixStartUtf16 = suffixStart;
            if (_input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            {
                suffixStartUtf16 = Utf8Validation.Validate(_input[..suffixStart], ref _sliceInvocation).Utf16Length;
            }

            _current = new Utf8ValueMatch(true, true, suffixStartUtf16, suffixLiteral.Length, suffixStart, suffixLiteral.Length);
            return true;
        }

        // Folded anchored suffixes mirror the exact block with per-byte invariant
        // folding; the mode carries lax versus strict end handling, and only the
        // start index needs UTF-16 mapping on non-ASCII input.
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
            if (foldedSuffixStart < 0 || !Utf8AsciiAnchoredPrefixFamilyExecutor.FoldedSequenceEquals(_input.Slice(foldedSuffixStart, foldedSuffixLiteral.Length), foldedSuffixLiteral))
            {
                return false;
            }

            var foldedSuffixStartUtf16 = foldedSuffixStart;
            if (_input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            {
                foldedSuffixStartUtf16 = Utf8Validation.Validate(_input[..foldedSuffixStart], ref _sliceInvocation).Utf16Length;
            }

            _current = new Utf8ValueMatch(true, true, foldedSuffixStartUtf16, foldedSuffixLiteral.Length, foldedSuffixStart, foldedSuffixLiteral.Length);
            return true;
        }

        // Windows resume on the full subject so boundary guards retain their
        // left context. Every admitted window consumes a nonempty ASCII literal;
        // the factories reject non-ASCII subjects because gap counts are bytes.
        if (_mode == EnumeratorMode.AsciiOrderedLiteralWindow)
        {
            ref readonly var windowProgram = ref _plans.StructuralLinearProgram;
            // Search plans include all operation plans (currently several KiB).
            // A cursor borrows the immutable plan rather than copying it per hit.
            ref readonly var windowSearchPlan = ref _plans.SearchPlan;
            var breach = _sliceInvocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Native window cursor exceeded {breach}.");
            if (_nativeStructuralExhausted) { _sliceInvocation.Budget.ThrowIfCancellationRequested(); return false; }
            breach = AsciiOrderedLiteralWindowExecutor.TryFindNext(_input, windowProgram.OrderedLiteralWindowPlan,
                in windowSearchPlan, _consumed, ref _sliceInvocation, out var match, out var found);
            if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Native window cursor exceeded {breach}.");
            if (!found)
            {
                _nativeStructuralExhausted = true;
                return false;
            }

            var projected = Utf8ProjectionExecutor.ProjectMatch(new Utf8ProjectionPlan(Utf8ProjectionKind.ByteOnly), _input,
                0, 0, match.Index, match.Length, match.Length, ref _sliceInvocation, out _, out _);
            _current = projected;
            _consumed = match.Index + match.Length;
            return true;
        }

        if (_mode == EnumeratorMode.AsciiStructuralIdentifierFamily)
        {
            var breach = TryMoveNextStructuralFamily(out var found);
            if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Native structural cursor exceeded {breach}.");
            return found;
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
            EnumeratorMode.AsciiSimplePattern => MoveNextAsciiSimplePattern(),
            EnumeratorMode.AsciiSimplePatternScalar => MoveNextScalarSimplePattern(),
            EnumeratorMode.AsciiSimplePatternUnit => MoveNextUnitSimplePattern(),
            EnumeratorMode.AsciiFixedTokenPattern or EnumeratorMode.AsciiDeterministicPattern => MoveNextAsciiDeterministicPattern(),
            EnumeratorMode.SmallAsciiLiteralFamily => MoveNextSmallAsciiLiteralFamily(),
            EnumeratorMode.EmittedKernelPattern => MoveNextEmittedKernelPattern(),
            EnumeratorMode.EmptyLiteral => MoveNextEmptyLiteral(),
            EnumeratorMode.FallbackRegex => MoveNextFallback(),
            EnumeratorMode.FallbackRegexAscii => MoveNextFallbackAscii(),
            EnumeratorMode.OwnedSlice => MoveNextOwnedSlice(),
            _ => false,
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
        _consumedUtf16 = nextStart;
        _remaining = _input[nextStart..];
        return true;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, byte[] literal, int literalUtf16Length, Utf8ExecutionDeadline budget)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _literal = literal;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _literalUtf16Length = literalUtf16Length;
        _totalUtf16Length = input.Length;
        _projectionPlan = searchPlan.EnumerationOperation.Projection;
        _program = searchPlan.EnumerationOperation;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
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

    internal Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, Utf8SliceInvocation invocation)
        : this(input, regexPlan, useStructuralLinearProgram: true, invocation.Budget.Deadline, asciiInput: true)
    {
        _sliceInvocation = invocation;
        if (regexPlan.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily)
            _mode = EnumeratorMode.AsciiStructuralIdentifierFamily;
    }

    // The caller transfers the state already used for whole-subject validation.
    internal Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan,
        bool inputIsAscii, Utf8SliceInvocation invocation)
        : this(input, regexPlan, regexPlan.ExecutionKind, inputIsAscii, invocation.Budget.Deadline)
    {
        _sliceInvocation = invocation;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget)
        : this(input, regexPlan, executionKind, Utf8InputAnalyzer.IsAscii(input), budget)
    {
    }

    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input,
        Utf8PreparedRegex regexPlan,
        NativeExecutionKind executionKind,
        bool inputIsAscii,
        Utf8ExecutionDeadline budget)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _literal = null;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _literalUtf16Length = 0;
        _totalUtf16Length = input.Length;
        var useByteOnlyProjection = inputIsAscii &&
            executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals;
        _projectionPlan = useByteOnlyProjection
            ? new Utf8ProjectionPlan(Utf8ProjectionKind.ByteOnly)
            : searchPlan.EnumerationOperation.Projection;
        _program = useByteOnlyProjection
            ? searchPlan.EnumerationOperation.WithProjection(
                new Utf8ProjectionPlan(Utf8ProjectionKind.ByteOnly))
            : searchPlan.EnumerationOperation;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
            ? EnumeratorMode.AsciiLiteralIgnoreCaseLiterals
            : EnumeratorMode.ExactUtf8Literals;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8BoundaryMap? boundaryMap, Utf8ExecutionDeadline budget)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _alternateLiteralUtf16Lengths = null;
        _literal = Array.Empty<byte>();
        _input = input;
        _boundaryMap = boundaryMap;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _literalUtf16Length = 0;
        _totalUtf16Length = boundaryMap?.Utf16Length ?? input.Length;
        _projectionPlan = new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16BoundaryMap);
        _program = default;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = EnumeratorMode.EmptyLiteral;
    }

    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input,
        PreparedSmallAsciiLiteralFamilySearch smallAsciiLiteralFamilySearch,
        Utf8ExecutionDeadline budget)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = smallAsciiLiteralFamilySearch;
        _alternateLiteralUtf16Lengths = null;
        _literal = null;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _literalUtf16Length = 0;
        _totalUtf16Length = input.Length;
        _projectionPlan = new Utf8ProjectionPlan(Utf8ProjectionKind.ByteOnly);
        _program = default;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = EnumeratorMode.SmallAsciiLiteralFamily;
    }

    public Utf8OperationMatchCursor(
        ReadOnlySpan<byte> input,
        Utf8EmittedKernelMatcher emittedKernelMatcher,
        Utf8ExecutionDeadline budget)
    {
        this = default;
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _input = input;
        _budget = budget;
        _sliceInvocation = new(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        _emittedKernelMatcher = emittedKernelMatcher;
        _current = Utf8ValueMatch.NoMatch;
        _mode = EnumeratorMode.EmittedKernelPattern;
    }

    // R05: offset enumeration starts the scan at the floor unit over the full input so
    // line anchors keep their true left context; at floor zero this is the default state.
    public Utf8OperationMatchCursor WithStartFloor(int startByteOffset, int startUtf16Offset)
    {
        _preserveOffsetCurrent = true;
        _remaining = _input[startByteOffset..];
        _consumed = startByteOffset;
        _consumedUtf16 = startUtf16Offset;
        _deterministicScanState = new Utf8AsciiDeterministicScanState(startByteOffset, startByteOffset);
        _multiLiteralScanState = new PreparedMultiLiteralScanState(startByteOffset, startByteOffset, 0);
        _structuralSearchState = new Utf8StructuralSearchState(new PreparedSearchScanState(startByteOffset, default), default);
        _nativeStructuralExhausted = false;
        return this;
    }

    internal Utf8ExecutionLimitKind TryMoveNextStructuralFamily(out bool found)
    {
        found = false;
        var breach = _sliceInvocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        if (_nativeStructuralExhausted) return Utf8ExecutionLimitKind.None;
        // Candidate progress is tentative until its confirmation and projection
        // both succeed. A denied move retains the previous public result.
        var pending = _structuralSearchState;
        breach = Utf8AsciiStructuralIdentifierFamilyExecutor.TryFindNext(_plans.StructuralSearchPlan,
            _plans.StructuralVerifier, _input, _consumed, ref pending, ref _sliceInvocation, out var match, out var discovered);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (!discovered)
        {
            breach = _sliceInvocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            _sliceInvocation.Budget.ThrowIfCancellationRequested();
            _structuralSearchState = pending;
            _nativeStructuralExhausted = true;
            return Utf8ExecutionLimitKind.None;
        }
        breach = Utf8ProjectionExecutor.TryProjectMatch(new Utf8ProjectionPlan(Utf8ProjectionKind.ByteOnly), _input,
            0, 0, match.Index, match.Length, match.Length, ref _sliceInvocation, out var projected, out _, out _);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        _structuralSearchState = pending;
        _current = projected;
        _consumed = match.Index + Math.Max(1, match.Length);
        found = true;
        return Utf8ExecutionLimitKind.None;
    }

    public Utf8OperationMatchCursor WithBaseOffsets(int byteOffset, int utf16Offset)
    {
        _baseByteOffset = byteOffset;
        _baseUtf16Offset = utf16Offset;
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
        _current = Utf8ValueMatch.NoMatch;
        var literal = _literal;
        if (literal is not { Length: > 0 }) return Utf8ExecutionLimitKind.None;
        var reverse = _mode is EnumeratorMode.RtlExactAsciiLiteral or
            EnumeratorMode.RtlAsciiLiteralIgnoreCase or EnumeratorMode.RtlExactUtf8Literal;
        ref readonly var plan = ref _plans.SearchPlan;
        var breach = Utf8SearchExecutor.TryFindLiteral(plan, _input, reverse ? 0 : _consumed,
            reverse ? _remaining.Length : _input.Length, reverse, ref _sliceInvocation, out var index);
        if (breach != Utf8ExecutionLimitKind.None || index < 0) return breach;

        var projection = new Utf8ProjectionPlan(_asciiInput ? Utf8ProjectionKind.ByteOnly : Utf8ProjectionKind.Utf16Incremental);
        breach = Utf8ProjectionExecutor.TryProjectMatch(projection, _input,
            reverse ? 0 : _consumed, reverse ? 0 : _consumedUtf16, index,
            literal.Length, _literalUtf16Length, ref _sliceInvocation,
            out var match, out var nextBytes, out var nextUnits);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _current = match;
        if (reverse) _remaining = _input[..index];
        else
        {
            _consumed = nextBytes;
            _consumedUtf16 = nextUnits;
            _remaining = _input[_consumed..];
        }
        _lastLiteralFamilyBranchId = 0;
        found = true;
        return Utf8ExecutionLimitKind.None;
    }

    private bool MoveNextEmittedKernelPattern()
    {
        var matcher = _emittedKernelMatcher;
        if (matcher is null || _consumed > _input.Length)
        {
            return false;
        }

        var matchIndex = matcher.FindNext(_input, _consumed, out var matchedLength);
        if (matchIndex < 0)
        {
            return false;
        }

        _current = new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: matchIndex,
            lengthInUtf16: matchedLength,
            indexInBytes: matchIndex,
            lengthInBytes: matchedLength);
        _consumed = matchIndex + Math.Max(matchedLength, 1);
        return true;
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
        _current = Utf8ValueMatch.NoMatch;
        var pendingState = _multiLiteralScanState;
        ref readonly var searchPlan = ref _plans.SearchPlan;
        var breach = Utf8SearchStrategyExecutor.TryFindNextLiteralFamilyMatch(searchPlan, _program,
            _input, ref pendingState, ref _sliceInvocation, out var match, out var discovered);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (!discovered)
        {
            _multiLiteralScanState = pendingState;
            return Utf8ExecutionLimitKind.None;
        }
        var units = _alternateLiteralUtf16Lengths is { } lengths && (uint)match.LiteralId < (uint)lengths.Length
            ? lengths[match.LiteralId] : match.Length;
        breach = Utf8ProjectionExecutor.TryProjectMatch(_projectionPlan, _input,
            _consumed, _consumedUtf16, match.Index, match.Length, units,
            ref _sliceInvocation, out var projected, out var bytes, out var utf16);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _multiLiteralScanState = pendingState;
        _lastLiteralFamilyBranchId = match.LiteralId;
        _current = projected;
        _consumed = bytes;
        _consumedUtf16 = utf16;
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

        _current = new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: matchIndex,
            lengthInUtf16: matchLength,
            indexInBytes: matchIndex,
            lengthInBytes: matchLength);
        return true;
    }

    private bool MoveNextFallback()
    {
        if (!_fallbackEnumerator.MoveNext())
        {
            return false;
        }

        var valueMatch = _fallbackEnumerator.Current;
        var start = ResolveBoundary(valueMatch.Index);
        var end = ResolveBoundary(valueMatch.Index + valueMatch.Length);
        var isByteAligned = start.IsScalarBoundary && end.IsScalarBoundary;

        _current = new Utf8ValueMatch(
            success: true,
            isByteAligned: isByteAligned,
            indexInUtf16: valueMatch.Index,
            lengthInUtf16: valueMatch.Length,
            indexInBytes: start.ByteOffset,
            lengthInBytes: end.ByteOffset - start.ByteOffset);
        return true;
    }

    private bool MoveNextFallbackAscii()
    {
        if (!_fallbackEnumerator.MoveNext())
        {
            return false;
        }

        var valueMatch = _fallbackEnumerator.Current;
        _current = new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: valueMatch.Index,
            lengthInUtf16: valueMatch.Length,
            indexInBytes: valueMatch.Index,
            lengthInBytes: valueMatch.Length);
        return true;
    }

    private bool MoveNextAsciiSimplePattern()
    {
        var breach = TryMoveNextSimplePattern(out var found);
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Native simple cursor exceeded {breach}.");
        return found;
    }

    internal Utf8ExecutionLimitKind TryMoveNextSimplePattern(out bool found)
    {
        found = false;
        var breach = _sliceInvocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        if (_nativeStructuralExhausted) return Utf8ExecutionLimitKind.None;
        ref readonly var searchPlan = ref _plans.SearchPlan;
        ref readonly var simplePatternPlan = ref _plans.SimplePatternPlan;
        // Anchors see the complete subject, while discovery starts at the
        // caller's floor. No result or floor is published before projection.
        breach = Utf8ExecutionInterpreter.TryFindNextSimplePattern(_input, _plans.ExecutionProgram,
            searchPlan, simplePatternPlan, _consumed, ref _sliceInvocation, out var absolute, out var length);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (absolute < 0)
        {
            breach = _sliceInvocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            _sliceInvocation.Budget.ThrowIfCancellationRequested();
            _nativeStructuralExhausted = true;
            return Utf8ExecutionLimitKind.None;
        }
        breach = Utf8ProjectionExecutor.TryProjectMatch(new Utf8ProjectionPlan(_asciiInput
                ? Utf8ProjectionKind.ByteOnly : Utf8ProjectionKind.Utf16Incremental), _input,
            _consumed, _consumedUtf16, absolute, length, length, ref _sliceInvocation,
            out var projected, out var consumed, out var consumedUtf16);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        _current = projected;
        _consumed = consumed;
        _consumedUtf16 = consumedUtf16;
        _remaining = _input[_consumed..];
        found = true;
        return Utf8ExecutionLimitKind.None;
    }

    // Scalar simple-pattern enumeration: every scalar start steps through the shared
    // scalar verifier and projects through the boundary map. Construction sites qualify
    // up front (astral-free non-ASCII with scalar-verifiable tokens), so matches here
    // are always scalar-aligned; a projection miss throws instead of hiding later
    // matches. The move-next logic stays inline here to hold the single-caller debt
    // ceiling.
    private bool MoveNextScalarSimplePattern()
    {
        ref readonly var simplePatternPlan = ref _plans.SimplePatternPlan;
        if (_boundaryMap is null)
        {
            return false;
        }

        var map = _boundaryMap.Value;
        _budget.Step();
        var relative = Utf8ScalarSimplePatternVerifier.FindNextScalar(_remaining, in simplePatternPlan, _budget, 0, out var matchByteLength);
        if (relative < 0)
        {
            return false;
        }

        var matchByte = _consumed + relative;
        if (!map.TryGetUtf16OffsetForByteOffset(matchByte, out var startUnit) ||
            !map.TryGetUtf16OffsetForByteOffset(matchByte + matchByteLength, out var endUnit))
        {
            throw new InvalidOperationException("The scalar match is not aligned to UTF-8 scalar boundaries.");
        }

        _current = new Utf8ValueMatch(true, true, startUnit, endUnit - startUnit, matchByte, matchByteLength);
        _consumed = matchByte + matchByteLength;
        _consumedUtf16 = endUnit;
        _remaining = _input[_consumed..];
        return true;
    }

    // R05: astral unit enumeration steps every UTF-16 unit (halves included) through
    // the shared unit verifier and projects through the boundary map; half-involving
    // hits shape without byte ranges exactly like the BCL projector.
    public bool MoveNextUnitSimplePattern()
    {
        if (_decodedUnits is null || _boundaryMap is null)
        {
            return false;
        }

        var decoded = _decodedUnits;
        var map = _boundaryMap.Value;
        ref readonly var simplePatternPlan = ref _plans.SimplePatternPlan;
        _budget.Step();
        var unitStart = Utf8AstralUnitVerifier.FindNextUnit(decoded, in simplePatternPlan, _budget, _unitPosition, out var unitLength);
        if (unitStart < 0)
        {
            return false;
        }

        if (map.TryGetByteRange(unitStart, unitLength, out var unitByteIndex, out var unitByteLength))
        {
            _current = new Utf8ValueMatch(true, true, unitStart, unitLength, unitByteIndex, unitByteLength);
        }
        else
        {
            _current = new Utf8ValueMatch(true, false, unitStart, unitLength);
        }

        _unitPosition = unitStart + Math.Max(unitLength, 1);
        return true;
    }

    private bool MoveNextAsciiDeterministicPattern()
    {
        var breach = TryMoveNextDeterministic(out var found);
        if (breach != Utf8ExecutionLimitKind.None) throw new InvalidOperationException($"Native deterministic cursor exceeded {breach}.");
        return found;
    }

    internal Utf8ExecutionLimitKind TryMoveNextDeterministic(out bool found)
    {
        found = false;
        var breach = _sliceInvocation.TryChargeInputStep();
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        if (_nativeStructuralExhausted) return Utf8ExecutionLimitKind.None;
        ref readonly var structuralLinearProgram = ref _plans.StructuralLinearProgram;
        var pending = _deterministicScanState;
        breach = Utf8AsciiInstructionLinearExecutor.TryFindNextDeterministicMatch(structuralLinearProgram,
            _input, ref pending, ref _sliceInvocation, out var match, out var discovered);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        if (!discovered)
        {
            breach = _sliceInvocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) return breach;
            _sliceInvocation.Budget.ThrowIfCancellationRequested();
            _nativeStructuralExhausted = true;
            _deterministicScanState = pending;
            return Utf8ExecutionLimitKind.None;
        }
        breach = Utf8ProjectionExecutor.TryProjectMatch(new Utf8ProjectionPlan(_asciiInput
                ? Utf8ProjectionKind.ByteOnly : Utf8ProjectionKind.Utf16Incremental), _input,
            _consumed, _consumedUtf16, match.Index, match.Length, match.Length, ref _sliceInvocation,
            out var projected, out var consumed, out var consumedUtf16);
        if (breach != Utf8ExecutionLimitKind.None) return breach;
        _sliceInvocation.Budget.ThrowIfCancellationRequested();
        _deterministicScanState = pending;
        _current = projected;
        _consumed = consumed;
        _consumedUtf16 = consumedUtf16;
        found = true;
        return Utf8ExecutionLimitKind.None;
    }

    private bool MoveNextEmptyLiteral()
    {
        if (_consumedUtf16 > _totalUtf16Length)
        {
            return false;
        }

        var boundary = ResolveBoundary(_consumedUtf16);
        _current = new Utf8ValueMatch(
            success: true,
            isByteAligned: boundary.IsScalarBoundary,
            indexInUtf16: _consumedUtf16,
            lengthInUtf16: 0,
            indexInBytes: boundary.ByteOffset,
            lengthInBytes: 0);
        _consumedUtf16++;
        return true;
    }

    private Utf16Boundary ResolveBoundary(int utf16Offset)
    {
        if (_boundaryMap is { } map)
        {
            return map.Resolve(utf16Offset);
        }

        if (_totalUtf16Length == _input.Length)
        {
            return Utf16Boundary.ScalarBoundary(utf16Offset, utf16Offset);
        }

        throw new InvalidOperationException("UTF-16 projection was not prepared for this match enumeration.");
    }

    private Utf8ValueMatch ApplyBaseOffsets(Utf8ValueMatch match)
    {
        if (!match.Success || (_baseByteOffset | _baseUtf16Offset) == 0)
        {
            return match;
        }

        return new Utf8ValueMatch(
            success: true,
            isByteAligned: match.IsByteAligned,
            indexInUtf16: _baseUtf16Offset + match.IndexInUtf16,
            lengthInUtf16: match.LengthInUtf16,
            indexInBytes: match.IsByteAligned ? _baseByteOffset + match.IndexInBytes : match.IndexInBytes,
            lengthInBytes: match.LengthInBytes);
    }

    private enum EnumeratorMode : byte
    {
        Exhausted = 0,
        ExactAsciiLiteral = 1,
        ExactUtf8Literal = 2,
        ExactUtf8Literals = 3,
        AsciiLiteralIgnoreCase = 4,
        AsciiLiteralIgnoreCaseLiterals = 5,
        AsciiSimplePattern = 6,
        FallbackRegex = 7,
        AsciiFixedTokenPattern = 8,
        AsciiDeterministicPattern = 9,
        EmptyLiteral = 10,
        SmallAsciiLiteralFamily = 11,
        EmittedKernelPattern = 12,
        FallbackRegexAscii = 13,
        OwnedSlice = 14,
        AnchoredAsciiLiteralPrefix = 15,
        AnchoredAsciiLiteralSuffix = 16,
        AnchoredAsciiLiteralSuffixStrictEnd = 17,
        AnchoredFoldedAsciiLiteralPrefix = 18,
        AnchoredFoldedAsciiLiteralSuffix = 19,
        AnchoredFoldedAsciiLiteralSuffixStrictEnd = 20,
        AsciiSimplePatternScalar = 21,
        AsciiSimplePatternUnit = 25,
        RtlExactAsciiLiteral = 22,
        RtlAsciiLiteralIgnoreCase = 23,
        RtlExactUtf8Literal = 24,
        AsciiOrderedLiteralWindow = 26,
        AsciiStructuralIdentifierFamily = 27,
    }
}
