using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex.Internal.Execution;

internal ref struct Utf8OperationMatchCursor
{
    private readonly EnumeratorMode _mode;
    private readonly AsciiSimplePatternPlan _simplePatternPlan;
    private readonly Utf8StructuralLinearProgram _structuralLinearProgram;
    private readonly Utf8ExecutionProgram? _executionProgram;
    private readonly Utf8SearchPlan _searchPlan;
    private readonly PreparedSmallAsciiLiteralFamilySearch _smallAsciiLiteralFamilySearch;
    private readonly PreparedSubstringSearch? _literalSearch;
    private readonly Utf8EmittedKernelMatcher? _emittedKernelMatcher;
    private readonly int[]? _alternateLiteralUtf16Lengths;
    private readonly bool _hasBoundaryRequirements;
    private readonly bool _hasTrailingLiteralRequirement;
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
    private int _asciiFixedTokenCurrentIndex;
    private int _asciiFixedTokenMatchLength;
    private int _baseByteOffset;
    private int _baseUtf16Offset;

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, byte[] literal, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = searchPlan;
        _smallAsciiLiteralFamilySearch = default;
        _literalSearch = searchPlan.LiteralSearch;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _hasBoundaryRequirements = searchPlan.HasBoundaryRequirements;
        _hasTrailingLiteralRequirement = searchPlan.HasTrailingLiteralRequirement;
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

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, string decoded, Regex regex, Utf8BoundaryMap boundaryMap)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = default;
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
        _totalUtf16Length = boundaryMap.Utf16Length;
        _projectionPlan = new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16BoundaryMap);
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
        _mode = EnumeratorMode.FallbackRegex;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Regex regex, string decoded, int startAt, Utf8BoundaryMap boundaryMap)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = default;
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
        _totalUtf16Length = boundaryMap.Utf16Length;
        _projectionPlan = new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16BoundaryMap);
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
        _mode = EnumeratorMode.FallbackRegex;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8ExecutionProgram? executionProgram, AsciiSimplePatternPlan simplePatternPlan, Utf8ExecutionDeadline budget)
    {
        _simplePatternPlan = simplePatternPlan;
        _structuralLinearProgram = default;
        _executionProgram = executionProgram;
        _searchPlan = default;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = EnumeratorMode.AsciiSimplePattern;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8ExecutionProgram? executionProgram, Utf8SearchPlan searchPlan, AsciiSimplePatternPlan simplePatternPlan, Utf8ExecutionDeadline budget)
    {
        _simplePatternPlan = simplePatternPlan;
        _structuralLinearProgram = default;
        _executionProgram = executionProgram;
        _searchPlan = searchPlan;
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
        _totalUtf16Length = input.Length;
        _projectionPlan = searchPlan.ProjectionPlan;
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
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = EnumeratorMode.AsciiSimplePattern;
    }

    public Utf8OperationMatch Current
        => new(
            _mode == EnumeratorMode.AsciiFixedTokenPattern && _asciiFixedTokenCurrentIndex >= 0
            ? new Utf8ValueMatch(
                success: true,
                isByteAligned: true,
                indexInUtf16: _baseUtf16Offset + _asciiFixedTokenCurrentIndex,
                lengthInUtf16: _asciiFixedTokenMatchLength,
                indexInBytes: _baseByteOffset + _asciiFixedTokenCurrentIndex,
                lengthInBytes: _asciiFixedTokenMatchLength)
            : ApplyBaseOffsets(_current),
            branchId: 0,
            captureSlots: null);

    public bool MoveNext()
    {
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

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, byte[] literal, int literalUtf16Length, Utf8ExecutionDeadline budget)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = searchPlan;
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

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, Utf8ExecutionDeadline budget)
    {
        this = new Utf8OperationMatchCursor(input, searchPlan, NativeExecutionKind.ExactUtf8Literals, budget);
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, NativeExecutionKind executionKind, Utf8ExecutionDeadline budget)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = searchPlan;
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
        _projectionPlan = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
            ? new Utf8ProjectionPlan(Utf8ProjectionKind.ByteOnly)
            : searchPlan.EnumerationOperation.Projection;
        _program = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
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

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8StructuralLinearProgram structuralLinearProgram, Utf8ExecutionDeadline budget)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = structuralLinearProgram;
        _executionProgram = null;
        _searchPlan = default;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = structuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern
            ? structuralLinearProgram.DeterministicProgram.FixedWidthLength
            : 0;
        _baseByteOffset = 0;
        _baseUtf16Offset = 0;
        _mode = structuralLinearProgram.DeterministicProgram.HasValue
            ? (structuralLinearProgram.DeterministicProgram.FixedWidthLength > 0
                ? EnumeratorMode.AsciiFixedTokenPattern
                : EnumeratorMode.AsciiDeterministicPattern)
            : EnumeratorMode.Exhausted;
    }

    public Utf8OperationMatchCursor(ReadOnlySpan<byte> input, Utf8BoundaryMap? boundaryMap, Utf8ExecutionDeadline budget)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = default;
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
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = default;
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
        var index = Utf8SearchExecutor.FindFirst(in _searchPlan, _remaining);
        if (index < 0)
        {
            return false;
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
        var index = Utf8SearchExecutor.FindFirst(in _searchPlan, _remaining);
        if (index < 0)
        {
            return false;
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
        var index = _literalSearch is { } literalSearch &&
            !_hasBoundaryRequirements &&
            !_hasTrailingLiteralRequirement
            ? literalSearch.IndexOf(_remaining)
            : Utf8SearchExecutor.FindFirst(in _searchPlan, _remaining);
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
        if (!Utf8SearchStrategyExecutor.TryFindNextLiteralFamilyMatch(
                in _searchPlan,
                in _program,
                _input,
                ref _multiLiteralScanState,
                _budget,
                out var match))
        {
            return false;
        }

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
        if (!Utf8SearchStrategyExecutor.TryFindNextLiteralFamilyMatch(
                in _searchPlan,
                in _program,
                _input,
                ref _multiLiteralScanState,
                _budget,
                out var match))
        {
            return false;
        }

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

    private bool MoveNextAsciiSimplePattern()
    {
        var relative = Utf8ExecutionInterpreter.FindNextSimplePattern(_remaining, _executionProgram, _searchPlan, _simplePatternPlan, 0, captures: null, _budget, out var matchLength);
        if (relative < 0)
        {
            return false;
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
        if (!Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicFixedWidthMatch(
                _structuralLinearProgram,
                _input,
                ref _deterministicScanState,
                _budget,
                out var matchIndex))
        {
            _asciiFixedTokenCurrentIndex = -1;
            return false;
        }

        _asciiFixedTokenCurrentIndex = matchIndex;
        return true;
    }

    private bool MoveNextAsciiDeterministicPattern()
    {
        if (!Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicRawMatch(
                _structuralLinearProgram,
                _input,
                ref _deterministicScanState,
                _budget,
                out var match))
        {
            return false;
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
    }
}
