using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Lokad.Utf8Regex.Internal.Execution;

internal ref struct Utf8OperationMatchCursor
{
    internal static int DebugSizeInBytes => Unsafe.SizeOf<Utf8OperationMatchCursor>();

    internal static int DebugSimplePatternPlanSizeInBytes => Unsafe.SizeOf<AsciiSimplePatternPlan>();

    internal static int DebugStructuralLinearProgramSizeInBytes => Unsafe.SizeOf<Utf8StructuralLinearProgram>();

    internal static int DebugSearchPlanSizeInBytes => Unsafe.SizeOf<Utf8SearchPlan>();

    internal static int DebugSmallLiteralFamilySizeInBytes => Unsafe.SizeOf<PreparedSmallAsciiLiteralFamilySearch>();

    internal static int DebugSearchOperationPlanSizeInBytes => Unsafe.SizeOf<Utf8SearchOperationPlan>();

    internal static int DebugDeterministicScanStateSizeInBytes => Unsafe.SizeOf<Utf8AsciiDeterministicScanState>();

    private readonly EnumeratorMode _mode;
    private readonly Utf8OperationMatchCursorPlans _plans;
    private readonly PreparedSmallAsciiLiteralFamilySearch _smallAsciiLiteralFamilySearch;
    private readonly PreparedSubstringSearch? _literalSearch;
    private readonly Utf8EmittedKernelMatcher? _emittedKernelMatcher;
    private readonly int[]? _alternateLiteralUtf16Lengths;
    private readonly bool _hasBoundaryRequirements;
    private readonly bool _hasTrailingLiteralRequirement;
    private readonly bool _asciiInput;
    private readonly byte[]? _literal;
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8BoundaryMap? _boundaryMap;
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
    private int _asciiFixedTokenCurrentIndex;
    private int _asciiFixedTokenMatchLength;
    private int _baseByteOffset;
    private int _baseUtf16Offset;
    private Utf8SliceProgram? _sliceProgram;
    private Utf8SliceInvocation _sliceInvocation;
    private int _sliceUnit;
    private int _sliceOriginUnit;

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, byte[] literal, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget, bool asciiInput)
    {
        ref readonly var searchPlan = ref regexPlan.SearchPlan;
        _plans = regexPlan.OperationMatchCursorPlans;
        _smallAsciiLiteralFamilySearch = default;
        _literalSearch = searchPlan.LiteralSearch;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _hasBoundaryRequirements = searchPlan.HasBoundaryRequirements;
        _hasTrailingLiteralRequirement = searchPlan.HasTrailingLiteralRequirement;
        _asciiInput = asciiInput;
        _literal = literal;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = executionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => EnumeratorMode.ExactAsciiLiteral,
            NativeExecutionKind.AsciiLiteralIgnoreCase => EnumeratorMode.AsciiLiteralIgnoreCase,
            _ => EnumeratorMode.Exhausted,
        };
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, string decoded, Regex regex, Utf8BoundaryMap? boundaryMap)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = null;
        _hasBoundaryRequirements = false;
        _hasTrailingLiteralRequirement = false;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
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
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = null;
        _hasBoundaryRequirements = false;
        _hasTrailingLiteralRequirement = false;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
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
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = null;
        _hasBoundaryRequirements = false;
        _hasTrailingLiteralRequirement = false;
        _literal = null;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = useStructuralLinearProgram &&
            structuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern
                ? structuralLinearProgram.DeterministicProgram.FixedWidthLength
                : 0;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = useStructuralLinearProgram
            ? (structuralLinearProgram.DeterministicProgram.FixedWidthLength > 0
                ? EnumeratorMode.AsciiFixedTokenPattern
                : EnumeratorMode.AsciiDeterministicPattern)
            : EnumeratorMode.AsciiSimplePattern;
    }

    public Utf8ValueMatch CurrentValueMatch
        => _mode == EnumeratorMode.AsciiFixedTokenPattern && _asciiInput && _asciiFixedTokenCurrentIndex >= 0
            ? new Utf8ValueMatch(
                success: true,
                isByteAligned: true,
                indexInUtf16: _baseUtf16Offset + _asciiFixedTokenCurrentIndex,
                lengthInUtf16: _asciiFixedTokenMatchLength,
                indexInBytes: _baseByteOffset + _asciiFixedTokenCurrentIndex,
                lengthInBytes: _asciiFixedTokenMatchLength)
            : ApplyBaseOffsets(_current);

    public Utf8OperationMatch Current
        => new(CurrentValueMatch, branchId: 0, captureSlots: null);

    /// <summary>Gets the alternate index of the last literal match, or -1 when the last
    /// successful move did not come from a literal search. Singular literals always report 0.</summary>
    internal int LastLiteralFamilyBranchId =>
        _mode is EnumeratorMode.ExactAsciiLiteral or EnumeratorMode.ExactUtf8Literal or EnumeratorMode.ExactUtf8Literals or EnumeratorMode.AsciiLiteralIgnoreCase or EnumeratorMode.AsciiLiteralIgnoreCaseLiterals
            ? _lastLiteralFamilyBranchId
            : -1;

    public Utf8OperationMatchCursor(
        Utf8SliceProgram program,
        ReadOnlySpan<byte> input,
        Utf8BoundaryMap map,
        TimeSpan timeout,
        CancellationToken token)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = null;
        _hasBoundaryRequirements = false;
        _hasTrailingLiteralRequirement = false;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _sliceProgram = program;
        _sliceInvocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, timeout, token);
        _sliceUnit = 0;
        _sliceOriginUnit = -1;
        _mode = EnumeratorMode.OwnedSlice;
    }

    private bool MoveNextOwnedSlice()
    {
        if (_sliceProgram is null || _boundaryMap is null)
        {
            return false;
        }

        var map = _boundaryMap.Value;
        var slots = Utf8SliceRunner.RentSlots(_sliceProgram.GroupCount, ref _sliceInvocation);
        try
        {
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

                    _current = Utf8SliceRunner.ProjectMatch(map, rtlOutcome);
                    _sliceUnit = rtlOutcome.EndUnit > rtlOutcome.StartUnit ? rtlOutcome.StartUnit : rtlOutcome.EndUnit - 1;
                    if (rtlOutcome.EndUnit > rtlOutcome.StartUnit)
                    {
                        _sliceOriginUnit = rtlOutcome.StartUnit;
                    }

                    return true;
                }

                return false;
            }
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref _sliceInvocation);
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

                _current = Utf8SliceRunner.ProjectMatch(map, outcome);
                _sliceUnit = outcome.EndUnit > outcome.StartUnit ? outcome.EndUnit : outcome.StartUnit + 1;
                if (outcome.EndUnit > outcome.StartUnit)
                {
                    _sliceOriginUnit = outcome.EndUnit;
                }

                return true;
            }

            return false;
    }

    // Anchored ASCII literal prefix enumeration: at most one match at offset 0,
    // byte-exact on every input. The constructor below is public (not a counted
    // private helper) and the move-next logic stays inline here to hold the
    // single-caller debt ceiling.
    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, byte[] literal, Utf8ExecutionDeadline budget)
    {
        _plans = Utf8OperationMatchCursorPlans.Empty;
        _smallAsciiLiteralFamilySearch = default;
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = null;
        _hasBoundaryRequirements = false;
        _hasTrailingLiteralRequirement = false;
        _literal = literal;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _sliceProgram = null;
        _sliceInvocation = default;
        _sliceUnit = 0;
        _sliceOriginUnit = 0;
        _emittedKernelMatcher = null;
        _mode = EnumeratorMode.AnchoredAsciiLiteralPrefix;
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
                suffixStartUtf16 = Utf8Validation.Validate(_input[..suffixStart]).Utf16Length;
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
                foldedSuffixStartUtf16 = Utf8Validation.Validate(_input[..foldedSuffixStart]).Utf16Length;
            }

            _current = new Utf8ValueMatch(true, true, foldedSuffixStartUtf16, foldedSuffixLiteral.Length, foldedSuffixStart, foldedSuffixLiteral.Length);
            return true;
        }

        return _mode switch
        {
            EnumeratorMode.ExactAsciiLiteral => MoveNextExactLiteral(),
            EnumeratorMode.ExactUtf8Literal => MoveNextExactUtf8Literal(),
            EnumeratorMode.ExactUtf8Literals => MoveNextExactUtf8Literals(),
            EnumeratorMode.AsciiLiteralIgnoreCase => MoveNextIgnoreCaseLiteral(),
            EnumeratorMode.AsciiLiteralIgnoreCaseLiterals => MoveNextAsciiIgnoreCaseLiterals(),
            EnumeratorMode.AsciiSimplePattern => MoveNextAsciiSimplePattern(),
            EnumeratorMode.AsciiFixedTokenPattern => MoveNextAsciiFixedTokenPattern(),
            EnumeratorMode.AsciiDeterministicPattern => MoveNextAsciiDeterministicPattern(),
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
        _literalSearch = searchPlan.LiteralSearch;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _hasBoundaryRequirements = searchPlan.HasBoundaryRequirements;
        _hasTrailingLiteralRequirement = searchPlan.HasTrailingLiteralRequirement;
        _literal = literal;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
        _mode = EnumeratorMode.ExactUtf8Literal;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8PreparedRegex regexPlan, Utf8ExecutionDeadline budget)
    {
        this = new Utf8OperationMatchCursor(input, regexPlan, NativeExecutionKind.ExactUtf8Literals, budget);
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
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _hasBoundaryRequirements = searchPlan.HasBoundaryRequirements;
        _hasTrailingLiteralRequirement = searchPlan.HasTrailingLiteralRequirement;
        _literal = null;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
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
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = null;
        _hasBoundaryRequirements = false;
        _hasTrailingLiteralRequirement = false;
        _literal = Array.Empty<byte>();
        _input = input;
        _boundaryMap = boundaryMap;
        _budget = budget;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
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
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = null;
        _hasBoundaryRequirements = false;
        _hasTrailingLiteralRequirement = false;
        _literal = null;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
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
        _emittedKernelMatcher = emittedKernelMatcher;
        _current = Utf8ValueMatch.NoMatch;
        _asciiFixedTokenCurrentIndex = -1;
        _mode = EnumeratorMode.EmittedKernelPattern;
    }

    public Utf8OperationMatchCursor WithBaseOffsets(int byteOffset, int utf16Offset)
    {
        _baseByteOffset = byteOffset;
        _baseUtf16Offset = utf16Offset;
        return this;
    }

    private bool MoveNextExactLiteral()
    {
        var literal = _literal;
        if (literal is null || literal.Length == 0)
        {
            return false;
        }

        _budget.Step();
        ref readonly var searchPlan = ref _plans.SearchPlan;
        var index = Utf8SearchExecutor.FindFirst(in searchPlan, _remaining);
        if (index < 0)
        {
            return false;
        }

        if (!_asciiInput)
        {
            // R05: ASCII literals on non-ASCII input project incrementally (the
            // literal length is the UTF-16 length; only the gap needs decoding),
            // mirroring the UTF-8 literal mode instead of declining to BCL.
            _current = Utf8ProjectionExecutor.ProjectMatch(
                new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16Incremental),
                _input,
                _consumed,
                _consumedUtf16,
                _consumed + index,
                literal.Length,
                literal.Length,
                out _consumed,
                out _consumedUtf16);
            _remaining = _input[_consumed..];
            _lastLiteralFamilyBranchId = 0;
            return true;
        }

        _current = new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: _consumed + index,
            lengthInUtf16: literal.Length,
            indexInBytes: _consumed + index,
            lengthInBytes: literal.Length);

        var advance = index + literal.Length;
        _remaining = _remaining[advance..];
        _consumed += advance;
        _lastLiteralFamilyBranchId = 0;
        return true;
    }

    private bool MoveNextIgnoreCaseLiteral()
    {
        var literal = _literal;
        if (literal is null || literal.Length == 0)
        {
            return false;
        }

        _budget.Step();
        ref readonly var searchPlan = ref _plans.SearchPlan;
        var index = Utf8SearchExecutor.FindFirst(in searchPlan, _remaining);
        if (index < 0)
        {
            return false;
        }

        if (!_asciiInput)
        {
            // R05: folded ASCII literals project incrementally on non-ASCII input
            // (every fold variant is ASCII, so the literal length is the UTF-16
            // length), mirroring the exact-literal mode. Kelvin and culture
            // divergences stay fenced upstream of the cursor.
            _current = Utf8ProjectionExecutor.ProjectMatch(
                new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16Incremental),
                _input,
                _consumed,
                _consumedUtf16,
                _consumed + index,
                literal.Length,
                literal.Length,
                out _consumed,
                out _consumedUtf16);
            _remaining = _input[_consumed..];
            _lastLiteralFamilyBranchId = 0;
            return true;
        }

        _current = new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: _consumed + index,
            lengthInUtf16: literal.Length,
            indexInBytes: _consumed + index,
            lengthInBytes: literal.Length);

        var advance = index + literal.Length;
        _remaining = _remaining[advance..];
        _consumed += advance;
        _lastLiteralFamilyBranchId = 0;
        return true;
    }

    private bool MoveNextExactUtf8Literal()
    {
        var literal = _literal;
        if (literal is null || literal.Length == 0)
        {
            return false;
        }

        _budget.Step();
        ref readonly var searchPlan = ref _plans.SearchPlan;
        var index = _literalSearch is { } literalSearch &&
            !_hasBoundaryRequirements &&
            !_hasTrailingLiteralRequirement
            ? literalSearch.IndexOf(_remaining)
            : Utf8SearchExecutor.FindFirst(in searchPlan, _remaining);
        if (index < 0)
        {
            return false;
        }

        _current = Utf8ProjectionExecutor.ProjectMatch(
            _projectionPlan,
            _input,
            _consumed,
            _consumedUtf16,
            _consumed + index,
            literal.Length,
            _literalUtf16Length,
            out _consumed,
            out _consumedUtf16);
        _remaining = _input[_consumed..];
        _lastLiteralFamilyBranchId = 0;
        return true;
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
        ref readonly var searchPlan = ref _plans.SearchPlan;
        if (!Utf8SearchStrategyExecutor.TryFindNextLiteralFamilyMatch(
                in searchPlan,
                in _program,
                _input,
                ref _multiLiteralScanState,
                _budget,
                out var match))
        {
            return false;
        }

        _lastLiteralFamilyBranchId = match.LiteralId;
        _current = Utf8ProjectionExecutor.ProjectLiteralFamilyMatch(
            _projectionPlan,
            _input,
            _alternateLiteralUtf16Lengths,
            _consumed,
            _consumedUtf16,
            match,
            out _consumed,
            out _consumedUtf16);
        _remaining = _input[_consumed..];
        return true;
    }

    private bool MoveNextAsciiIgnoreCaseLiterals()
    {
        ref readonly var searchPlan = ref _plans.SearchPlan;
        if (!Utf8SearchStrategyExecutor.TryFindNextLiteralFamilyMatch(
                in searchPlan,
                in _program,
                _input,
                ref _multiLiteralScanState,
                _budget,
                out var match))
        {
            return false;
        }
        _lastLiteralFamilyBranchId = match.LiteralId;
        _current = Utf8ProjectionExecutor.ProjectMatch(
            _projectionPlan,
            _input,
            _consumed,
            _consumedUtf16,
            match.Index,
            match.Length,
            match.Length,
            out _consumed,
            out _consumedUtf16);
        _remaining = _input[_consumed..];
        return true;
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
        ref readonly var searchPlan = ref _plans.SearchPlan;
        ref readonly var simplePatternPlan = ref _plans.SimplePatternPlan;
        var relative = Utf8ExecutionInterpreter.FindNextSimplePattern(
            _remaining,
            _plans.ExecutionProgram,
            in searchPlan,
            in simplePatternPlan,
            0,
            captures: null,
            _budget,
            out var matchLength);
        if (relative < 0)
        {
            return false;
        }

        if (!_asciiInput)
        {
            // R05: byte-safe programs match ASCII-only spans, so the match UTF-16
            // length equals the byte length; only the gap needs decoding.
            _current = Utf8ProjectionExecutor.ProjectMatch(
                new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16Incremental),
                _input,
                _consumed,
                _consumedUtf16,
                _consumed + relative,
                matchLength,
                matchLength,
                out _consumed,
                out _consumedUtf16);
            _remaining = _input[_consumed..];
            return true;
        }

        _current = new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: _consumed + relative,
            lengthInUtf16: matchLength,
            indexInBytes: _consumed + relative,
            lengthInBytes: matchLength);

        var advance = relative + Math.Max(matchLength, 1);
        _remaining = _remaining[advance..];
        _consumed += advance;
        return true;
    }

    private bool MoveNextAsciiFixedTokenPattern()
    {
        ref readonly var structuralLinearProgram = ref _plans.StructuralLinearProgram;
        if (!Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicFixedWidthMatch(
                in structuralLinearProgram,
                _input,
                ref _deterministicScanState,
                _budget,
                out var matchIndex))
        {
            _asciiFixedTokenCurrentIndex = -1;
            return false;
        }

        if (!_asciiInput)
        {
            // R05: fixed-width byte-safe matches are ASCII-only; decode only the gap
            // and project the hit, keeping byte/UTF-16 cursors for the next gap.
            var utf16Index = _consumedUtf16 + System.Text.Encoding.UTF8.GetCharCount(_input[_consumed..matchIndex]);
            _consumed = matchIndex + _asciiFixedTokenMatchLength;
            _consumedUtf16 = utf16Index + _asciiFixedTokenMatchLength;
            _current = new Utf8ValueMatch(
                success: true,
                isByteAligned: true,
                indexInUtf16: utf16Index,
                lengthInUtf16: _asciiFixedTokenMatchLength,
                indexInBytes: matchIndex,
                lengthInBytes: _asciiFixedTokenMatchLength);
            _asciiFixedTokenCurrentIndex = matchIndex;
            return true;
        }

        _asciiFixedTokenCurrentIndex = matchIndex;
        return true;
    }

    private bool MoveNextAsciiDeterministicPattern()
    {
        ref readonly var structuralLinearProgram = ref _plans.StructuralLinearProgram;
        if (!Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicRawMatch(
                in structuralLinearProgram,
                _input,
                ref _deterministicScanState,
                _budget,
                out var match))
        {
            return false;
        }

        if (!_asciiInput)
        {
            // R05: deterministic byte-safe matches are ASCII-only (see the simple
            // mode above); project incrementally off the scan position.
            _current = Utf8ProjectionExecutor.ProjectMatch(
                new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16Incremental),
                _input,
                _consumed,
                _consumedUtf16,
                match.Index,
                match.Length,
                match.Length,
                out _consumed,
                out _consumedUtf16);
            return true;
        }

        _current = new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: match.Index,
            lengthInUtf16: match.Length,
            indexInBytes: match.Index,
            lengthInBytes: match.Length);
        return true;
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
    }
}
