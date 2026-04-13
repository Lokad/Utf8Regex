using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Utilities;
using RuntimeFrontEnd = Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

namespace Lokad.Utf8Regex;

public ref struct Utf8ValueMatchEnumerator
{
    private readonly EnumeratorMode _mode;
    private readonly AsciiSimplePatternPlan _simplePatternPlan;
    private readonly Utf8StructuralLinearProgram _structuralLinearProgram;
    private readonly Utf8ExecutionProgram? _executionProgram;
    private readonly Utf8SearchPlan _searchPlan;
    private readonly PreparedSearcher _preparedSearcher;
    private readonly PreparedMultiLiteralSearch _multiLiteralSearch;
    private readonly PreparedSubstringSearch? _literalSearch;
    private readonly int[]? _alternateLiteralUtf16Lengths;
    private readonly bool _hasBoundaryRequirements;
    private readonly bool _hasTrailingLiteralRequirement;
    private readonly byte[]? _literal;
    private readonly ReadOnlySpan<byte> _input;
    private readonly Utf8BoundaryMap? _boundaryMap;
    private readonly Utf8ExecutionBudget? _budget;
    private readonly int _literalUtf16Length;
    private readonly int _totalUtf16Length;
    private readonly Utf8ProjectionPlan _projectionPlan;
    private readonly Utf8BackendInstructionProgram _program;
    private Regex.ValueMatchEnumerator _fallbackEnumerator;
    private ReadOnlySpan<byte> _remaining;
    private int _consumed;
    private int _consumedUtf16;
    private PreparedMultiLiteralScanState _multiLiteralScanState;
    private Utf8AsciiDeterministicScanState _deterministicScanState;
    private Utf8ValueMatch _current;
    private int _asciiFixedTokenCurrentIndex;
    private int _asciiFixedTokenMatchLength;

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, byte[] literal, NativeExecutionKind executionKind, Utf8ExecutionBudget? budget = null)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = searchPlan;
        _preparedSearcher = default;
        _multiLiteralSearch = default;
        _literalSearch = searchPlan.LiteralSearch;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _hasBoundaryRequirements = searchPlan.HasBoundaryRequirements;
        _hasTrailingLiteralRequirement = searchPlan.HasTrailingLiteralRequirement;
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
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
        _mode = executionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => EnumeratorMode.ExactAsciiLiteral,
            NativeExecutionKind.AsciiLiteralIgnoreCase => EnumeratorMode.AsciiLiteralIgnoreCase,
            _ => EnumeratorMode.Exhausted,
        };
    }

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, string decoded, Regex regex, Utf8BoundaryMap? boundaryMap = null)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = default;
        _preparedSearcher = default;
        _multiLiteralSearch = default;
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = null;
        _hasBoundaryRequirements = false;
        _hasTrailingLiteralRequirement = false;
        _literal = null;
        _input = input;
        _boundaryMap = boundaryMap;
        _budget = null;
        _literalUtf16Length = 0;
        _totalUtf16Length = boundaryMap?.Utf16Length ?? decoded.Length;
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
        _mode = EnumeratorMode.FallbackRegex;
    }

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, Regex regex, string decoded, int startAt, Utf8BoundaryMap? boundaryMap = null)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = default;
        _preparedSearcher = default;
        _multiLiteralSearch = default;
        _literalSearch = null;
        _alternateLiteralUtf16Lengths = null;
        _hasBoundaryRequirements = false;
        _hasTrailingLiteralRequirement = false;
        _literal = null;
        _input = input;
        _boundaryMap = boundaryMap;
        _budget = null;
        _literalUtf16Length = 0;
        _totalUtf16Length = boundaryMap?.Utf16Length ?? decoded.Length;
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
        _mode = EnumeratorMode.FallbackRegex;
    }

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8ExecutionProgram? executionProgram, AsciiSimplePatternPlan simplePatternPlan, Utf8ExecutionBudget? budget = null)
    {
        _simplePatternPlan = simplePatternPlan;
        _structuralLinearProgram = default;
        _executionProgram = executionProgram;
        _searchPlan = default;
        _preparedSearcher = default;
        _multiLiteralSearch = default;
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
        _mode = EnumeratorMode.AsciiSimplePattern;
    }

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8ExecutionProgram? executionProgram, Utf8SearchPlan searchPlan, AsciiSimplePatternPlan simplePatternPlan, Utf8ExecutionBudget? budget = null)
    {
        _simplePatternPlan = simplePatternPlan;
        _structuralLinearProgram = default;
        _executionProgram = executionProgram;
        _searchPlan = searchPlan;
        _preparedSearcher = default;
        _multiLiteralSearch = default;
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
        _program = searchPlan.EnumerationProgram;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
        _mode = EnumeratorMode.AsciiSimplePattern;
    }

    public Utf8ValueMatch Current
        => _mode == EnumeratorMode.AsciiFixedTokenPattern && _asciiFixedTokenCurrentIndex >= 0
            ? new Utf8ValueMatch(
                success: true,
                isByteAligned: true,
                indexInUtf16: _asciiFixedTokenCurrentIndex,
                lengthInUtf16: _asciiFixedTokenMatchLength,
                indexInBytes: _asciiFixedTokenCurrentIndex,
                lengthInBytes: _asciiFixedTokenMatchLength)
            : _current;

    public Utf8ValueMatchEnumerator GetEnumerator() => this;

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
            EnumeratorMode.EmptyLiteral => MoveNextEmptyLiteral(),
            EnumeratorMode.FallbackRegex => MoveNextFallback(),
            _ => false,
        };
    }

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, byte[] literal, int literalUtf16Length, Utf8ExecutionBudget? budget = null)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = searchPlan;
        _preparedSearcher = default;
        _multiLiteralSearch = default;
        _literalSearch = searchPlan.LiteralSearch;
        _alternateLiteralUtf16Lengths = searchPlan.AlternateLiteralUtf16Lengths;
        _hasBoundaryRequirements = searchPlan.HasBoundaryRequirements;
        _hasTrailingLiteralRequirement = searchPlan.HasTrailingLiteralRequirement;
        _literal = literal;
        _input = input;
        _boundaryMap = null;
        _budget = budget;
        _literalUtf16Length = literalUtf16Length;
        _totalUtf16Length = literalUtf16Length == literal.Length ? input.Length : Utf8Validation.Validate(input).Utf16Length;
        _projectionPlan = searchPlan.EnumerationPipeline.Projection;
        _program = searchPlan.EnumerationProgram;
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

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, Utf8ExecutionBudget? budget = null)
    {
        this = new Utf8ValueMatchEnumerator(input, searchPlan, NativeExecutionKind.ExactUtf8Literals, budget);
    }

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, NativeExecutionKind executionKind, Utf8ExecutionBudget? budget = null)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = searchPlan;
        _preparedSearcher = searchPlan.PreparedSearcher;
        _multiLiteralSearch = searchPlan.MultiLiteralSearch;
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
            : searchPlan.EnumerationPipeline.Projection;
        _program = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
            ? Utf8BackendInstructionProgramBuilder.Create(
                new Utf8ExecutablePipelinePlan(
                    searchPlan.EnumerationPipeline.Strategy,
                    searchPlan.EnumerationPipeline.Confirmation,
                    new Utf8ProjectionPlan(Utf8ProjectionKind.ByteOnly)))
            : searchPlan.EnumerationProgram;
        _fallbackEnumerator = default;
        _remaining = input;
        _consumed = 0;
        _consumedUtf16 = 0;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        _current = Utf8ValueMatch.NoMatch;
        _asciiFixedTokenCurrentIndex = -1;
        _asciiFixedTokenMatchLength = 0;
        _mode = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
            ? EnumeratorMode.AsciiLiteralIgnoreCaseLiterals
            : EnumeratorMode.ExactUtf8Literals;
    }

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8StructuralLinearProgram structuralLinearProgram, Utf8ExecutionBudget? budget = null)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = structuralLinearProgram;
        _executionProgram = null;
        _searchPlan = default;
        _preparedSearcher = default;
        _multiLiteralSearch = default;
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
        _mode = structuralLinearProgram.DeterministicProgram.HasValue
            ? (structuralLinearProgram.DeterministicProgram.FixedWidthLength > 0
                ? EnumeratorMode.AsciiFixedTokenPattern
                : EnumeratorMode.AsciiDeterministicPattern)
            : EnumeratorMode.Exhausted;
    }

    internal Utf8ValueMatchEnumerator(ReadOnlySpan<byte> input, Utf8BoundaryMap? boundaryMap, Utf8ExecutionBudget? budget = null)
    {
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _searchPlan = default;
        _preparedSearcher = default;
        _multiLiteralSearch = default;
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
        _mode = EnumeratorMode.EmptyLiteral;
    }

    private bool MoveNextExactLiteral()
    {
        var literal = _literal;
        if (literal is null || literal.Length == 0)
        {
            return false;
        }

        _budget?.Step(_remaining);
        var index = Utf8SearchExecutor.FindFirst(_searchPlan, _remaining);
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

        _budget?.Step(_remaining);
        var index = Utf8SearchExecutor.FindFirst(_searchPlan, _remaining);
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

        _budget?.Step(_remaining);
        var index = _literalSearch is { } literalSearch &&
            !_hasBoundaryRequirements &&
            !_hasTrailingLiteralRequirement
            ? literalSearch.IndexOf(_remaining)
            : Utf8SearchExecutor.FindFirst(_searchPlan, _remaining);
        if (index < 0)
        {
            return false;
        }

        _current = Utf8ProjectionExecutor.ProjectMatch(
            _program,
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

    private bool MoveNextExactUtf8Literals()
    {
        if (!Utf8SearchStrategyExecutor.TryFindNextLiteralFamilyMatch(_searchPlan, _input, ref _multiLiteralScanState, _budget, out var match))
        {
            return false;
        }

        _current = Utf8ProjectionExecutor.ProjectLiteralFamilyMatch(
            _program,
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
        if (!Utf8SearchStrategyExecutor.TryFindNextLiteralFamilyMatch(_searchPlan, _input, ref _multiLiteralScanState, _budget, out var match))
        {
            return false;
        }

        _current = Utf8ProjectionExecutor.ProjectMatch(
            _program,
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
        return _boundaryMap?.Resolve(utf16Offset) ?? Utf8Utf16BoundaryResolver.ResolveBoundary(_input, utf16Offset);
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
    }
}
