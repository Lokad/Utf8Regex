namespace Lokad.Utf8Regex;

public ref struct Utf8ValueSplitEnumerator
{
    private readonly EnumeratorMode _mode;
    private readonly ReadOnlySpan<byte> _input;
    private readonly string? _decoded;
    private readonly Utf8SearchPlan _searchPlan;
    private readonly AsciiSimplePatternPlan _simplePatternPlan;
    private readonly Utf8StructuralLinearProgram _structuralLinearProgram;
    private readonly Utf8ExecutionProgram? _executionProgram;
    private readonly byte[]? _literal;
    private readonly NativeExecutionKind _executionKind;
    private readonly Utf8BoundaryMap? _boundaryMap;
    private readonly Utf8ExecutionBudget? _budget;
    private readonly Lokad.Utf8Regex.Internal.Utilities.PreparedSmallAsciiLiteralFamilySearch _smallAsciiLiteralFamilySearch;
    private readonly int _literalUtf16Length;
    private readonly int _totalUtf16Length;
    private int _segmentStartBytes;
    private int _segmentStartUtf16;
    private int _searchStartBytes;
    private int _remainingCount;
    private bool _completed;
    private Regex.ValueSplitEnumerator _fallbackEnumerator;
    private PreparedMultiLiteralScanState _multiLiteralScanState;
    private Utf8AsciiDeterministicScanState _deterministicScanState;

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input, string decoded, Regex regex, int count, Utf8BoundaryMap? boundaryMap = null)
    {
        _input = input;
        _decoded = decoded;
        _searchPlan = default;
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _literal = null;
        _executionKind = default;
        _boundaryMap = boundaryMap;
        _budget = null;
        _smallAsciiLiteralFamilySearch = default;
        _literalUtf16Length = 0;
        _totalUtf16Length = decoded.Length;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _searchStartBytes = 0;
        _remainingCount = 0;
        _completed = false;
        _fallbackEnumerator = regex.EnumerateSplits(decoded, count);
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        Current = default;
        _mode = EnumeratorMode.FallbackRegex;
    }

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, byte[] literal, NativeExecutionKind executionKind, int count, Utf8BoundaryMap? boundaryMap = null, Utf8ExecutionBudget? budget = null)
    {
        _input = input;
        _decoded = null;
        _searchPlan = searchPlan;
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _literal = literal;
        _executionKind = executionKind;
        _boundaryMap = boundaryMap;
        _budget = budget;
        _smallAsciiLiteralFamilySearch = default;
        _literalUtf16Length = executionKind == NativeExecutionKind.ExactUtf8Literal
            ? Utf8Validation.Validate(literal).Utf16Length
            : literal.Length;
        _totalUtf16Length = executionKind == NativeExecutionKind.ExactUtf8Literal
            ? (boundaryMap?.Utf16Length ?? Utf8Validation.Validate(input).Utf16Length)
            : input.Length;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _searchStartBytes = 0;
        _remainingCount = count;
        _completed = false;
        _fallbackEnumerator = default;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        Current = default;
        _mode = EnumeratorMode.NativeLiteral;
    }

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, int count, Utf8ExecutionBudget? budget = null)
    {
        this = new Utf8ValueSplitEnumerator(input, searchPlan, count, NativeExecutionKind.ExactUtf8Literals, budget);
    }

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, int count, NativeExecutionKind executionKind, Utf8ExecutionBudget? budget = null)
    {
        _input = input;
        _decoded = null;
        _searchPlan = searchPlan;
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _literal = null;
        _executionKind = executionKind;
        _boundaryMap = null;
        _budget = budget;
        _smallAsciiLiteralFamilySearch = default;
        _literalUtf16Length = 0;
        _totalUtf16Length = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
            ? input.Length
            : Utf8Validation.Validate(input).Utf16Length;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _searchStartBytes = 0;
        _remainingCount = count;
        _completed = false;
        _fallbackEnumerator = default;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        Current = default;
        _mode = executionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
            ? EnumeratorMode.NativeAsciiIgnoreCaseLiterals
            : EnumeratorMode.NativeUtf8Literals;
    }

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input, Utf8ExecutionProgram? executionProgram, AsciiSimplePatternPlan simplePatternPlan, int count, Utf8ExecutionBudget? budget = null)
    {
        _input = input;
        _decoded = null;
        _searchPlan = default;
        _simplePatternPlan = simplePatternPlan;
        _structuralLinearProgram = default;
        _executionProgram = executionProgram;
        _literal = null;
        _executionKind = NativeExecutionKind.AsciiSimplePattern;
        _boundaryMap = null;
        _budget = budget;
        _smallAsciiLiteralFamilySearch = default;
        _literalUtf16Length = 0;
        _totalUtf16Length = input.Length;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _searchStartBytes = 0;
        _remainingCount = count;
        _completed = false;
        _fallbackEnumerator = default;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        Current = default;
        _mode = EnumeratorMode.NativeSimplePattern;
    }

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input, Utf8SearchPlan searchPlan, Utf8ExecutionProgram? executionProgram, AsciiSimplePatternPlan simplePatternPlan, int count, Utf8ExecutionBudget? budget = null)
    {
        _input = input;
        _decoded = null;
        _searchPlan = searchPlan;
        _simplePatternPlan = simplePatternPlan;
        _structuralLinearProgram = default;
        _executionProgram = executionProgram;
        _literal = null;
        _executionKind = NativeExecutionKind.AsciiSimplePattern;
        _boundaryMap = null;
        _budget = budget;
        _smallAsciiLiteralFamilySearch = default;
        _literalUtf16Length = 0;
        _totalUtf16Length = input.Length;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _searchStartBytes = 0;
        _remainingCount = count;
        _completed = false;
        _fallbackEnumerator = default;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        Current = default;
        _mode = EnumeratorMode.NativeSimplePattern;
    }

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input, Utf8StructuralLinearProgram structuralLinearProgram, int count, Utf8ExecutionBudget? budget = null)
    {
        _input = input;
        _decoded = null;
        _searchPlan = default;
        _simplePatternPlan = default;
        _structuralLinearProgram = structuralLinearProgram;
        _executionProgram = null;
        _literal = null;
        _executionKind = NativeExecutionKind.AsciiSimplePattern;
        _boundaryMap = null;
        _budget = budget;
        _smallAsciiLiteralFamilySearch = default;
        _literalUtf16Length = 0;
        _totalUtf16Length = input.Length;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _searchStartBytes = 0;
        _remainingCount = count;
        _completed = false;
        _fallbackEnumerator = default;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        Current = default;
        _mode = structuralLinearProgram.DeterministicProgram.HasValue
            ? (structuralLinearProgram.DeterministicProgram.FixedWidthLength > 0
                ? EnumeratorMode.NativeFixedTokenPattern
                : EnumeratorMode.NativeDeterministicPattern)
            : EnumeratorMode.FallbackRegex;
    }

    internal Utf8ValueSplitEnumerator(ReadOnlySpan<byte> input, Lokad.Utf8Regex.Internal.Utilities.PreparedSmallAsciiLiteralFamilySearch smallAsciiLiteralFamilySearch, int count, Utf8ExecutionBudget? budget = null)
    {
        _input = input;
        _decoded = null;
        _searchPlan = default;
        _simplePatternPlan = default;
        _structuralLinearProgram = default;
        _executionProgram = null;
        _literal = null;
        _executionKind = NativeExecutionKind.ExactUtf8Literals;
        _boundaryMap = null;
        _budget = budget;
        _smallAsciiLiteralFamilySearch = smallAsciiLiteralFamilySearch;
        _literalUtf16Length = 0;
        _totalUtf16Length = input.Length;
        _segmentStartBytes = 0;
        _segmentStartUtf16 = 0;
        _searchStartBytes = 0;
        _remainingCount = count;
        _completed = false;
        _fallbackEnumerator = default;
        _multiLiteralScanState = default;
        _deterministicScanState = default;
        Current = default;
        _mode = EnumeratorMode.NativeSmallAsciiLiteralFamily;
    }

    public Utf8ValueSplit Current { get; private set; }

    public Utf8ValueSplitEnumerator GetEnumerator() => this;

    public bool MoveNext()
    {
        return _mode switch
        {
            EnumeratorMode.NativeLiteral => MoveNextNativeLiteral(),
            EnumeratorMode.NativeUtf8Literals => MoveNextNativeUtf8Literals(),
            EnumeratorMode.NativeAsciiIgnoreCaseLiterals => MoveNextNativeAsciiIgnoreCaseLiterals(),
            EnumeratorMode.NativeSmallAsciiLiteralFamily => MoveNextNativeSmallAsciiLiteralFamily(),
            EnumeratorMode.NativeSimplePattern => MoveNextNativeSimplePattern(),
            EnumeratorMode.NativeFixedTokenPattern => MoveNextNativeFixedTokenPattern(),
            EnumeratorMode.NativeDeterministicPattern => MoveNextNativeDeterministicPattern(),
            _ => MoveNextFallback(),
        };
    }

    private bool MoveNextFallback()
    {
        if (!_fallbackEnumerator.MoveNext())
        {
            return false;
        }

        var range = _fallbackEnumerator.Current;
        var start = range.Start.Value;
        var end = range.End.Value;
        Current = new Utf8ValueSplit(_input, _decoded, start, end - start, _boundaryMap);
        return true;
    }

    private bool MoveNextNativeLiteral()
    {
        var literal = _literal;
        if (_completed || _remainingCount <= 0 || literal is null || literal.Length == 0)
        {
            return false;
        }

        if (_remainingCount == 1)
        {
            return EmitTail();
        }

        _budget?.Step(_input);
        var matchIndex = Utf8SearchExecutor.FindNext(_searchPlan, _input, _searchStartBytes);
        if (matchIndex < 0)
        {
            return EmitTail();
        }

        if (_executionKind == NativeExecutionKind.ExactUtf8Literal)
        {
            var relativeUtf16Length = matchIndex == _segmentStartBytes
                ? 0
                : Utf8Validation.Validate(_input[_segmentStartBytes..matchIndex]).Utf16Length;
            var startUtf16 = _segmentStartUtf16;
            var matchUtf16 = startUtf16 + relativeUtf16Length;
            var relativeByteLength = matchIndex - _segmentStartBytes;
            Current = new Utf8ValueSplit(
                _input,
                decoded: null,
                indexInUtf16: startUtf16,
                lengthInUtf16: relativeUtf16Length,
                indexInBytes: _segmentStartBytes,
                lengthInBytes: relativeByteLength);

            _segmentStartBytes = matchIndex + literal.Length;
            _searchStartBytes = _segmentStartBytes;
            _segmentStartUtf16 = matchUtf16 + _literalUtf16Length;
            _remainingCount--;
            return true;
        }

        var asciiSegmentLength = matchIndex - _segmentStartBytes;
        Current = new Utf8ValueSplit(
            _input,
            decoded: null,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: asciiSegmentLength,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: asciiSegmentLength);
        _segmentStartBytes = matchIndex + literal.Length;
        _searchStartBytes = _segmentStartBytes;
        _segmentStartUtf16 = _segmentStartBytes;
        _remainingCount--;
        return true;
    }

    private bool MoveNextNativeSimplePattern()
    {
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_remainingCount == 1)
        {
            return EmitTail();
        }

        var matchIndex = Utf8ExecutionInterpreter.FindNextSimplePattern(_input, _executionProgram, _searchPlan, _simplePatternPlan, _searchStartBytes, captures: null, _budget, out var matchLength);
        if (matchIndex < 0)
        {
            return EmitTail();
        }

        var segmentLength = matchIndex - _segmentStartBytes;
        Current = new Utf8ValueSplit(
            _input,
            decoded: null,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: segmentLength,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: segmentLength);
        _segmentStartBytes = matchIndex + matchLength;
        _segmentStartUtf16 = _segmentStartBytes;
        _searchStartBytes = _segmentStartBytes;
        _remainingCount--;
        return true;
    }

    private bool MoveNextNativeUtf8Literals()
    {
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_remainingCount == 1)
        {
            return EmitTail();
        }

        if (_searchPlan.HasBoundaryRequirements || _searchPlan.HasTrailingLiteralRequirement)
        {
            _budget?.Step(_input);
            if (!Utf8SearchExecutor.TryFindNextMatch(_searchPlan, _input, _searchStartBytes, out var fallbackMatch))
            {
                return EmitTail();
            }

            var fallbackMatchIndex = fallbackMatch.Index;
            var fallbackMatchByteLength = fallbackMatch.Length;
            var fallbackRelativeUtf16Length = fallbackMatchIndex == _segmentStartBytes
                ? 0
                : Utf8Validation.Validate(_input[_segmentStartBytes..fallbackMatchIndex]).Utf16Length;
            Current = new Utf8ValueSplit(
                _input,
                decoded: null,
                indexInUtf16: _segmentStartUtf16,
                lengthInUtf16: fallbackRelativeUtf16Length,
                indexInBytes: _segmentStartBytes,
                lengthInBytes: fallbackMatchIndex - _segmentStartBytes);

            _segmentStartBytes = fallbackMatchIndex + fallbackMatchByteLength;
            _searchStartBytes = _segmentStartBytes;
            var fallbackMatchedUtf16Length = _searchPlan.AlternateLiteralUtf16Lengths is { Length: > 0 } fallbackUtf16Lengths &&
                (uint)fallbackMatch.LiteralId < (uint)fallbackUtf16Lengths.Length
                ? fallbackUtf16Lengths[fallbackMatch.LiteralId]
                : Utf8Validation.Validate(_input.Slice(fallbackMatchIndex, fallbackMatchByteLength)).Utf16Length;
            _segmentStartUtf16 += fallbackRelativeUtf16Length + fallbackMatchedUtf16Length;
            _remainingCount--;
            return true;
        }

        if (!Utf8SearchStrategyExecutor.TryFindNextLiteralFamilyMatch(_searchPlan, _input, ref _multiLiteralScanState, _budget, out var match))
        {
            return EmitTail();
        }

        var matchIndex = match.Index;
        var matchByteLength = match.Length;

        var relativeUtf16Length = matchIndex == _segmentStartBytes
            ? 0
            : Utf8Validation.Validate(_input[_segmentStartBytes..matchIndex]).Utf16Length;
        Current = new Utf8ValueSplit(
            _input,
            decoded: null,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: relativeUtf16Length,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: matchIndex - _segmentStartBytes);

        _segmentStartBytes = matchIndex + matchByteLength;
        _searchStartBytes = _segmentStartBytes;
        var matchedUtf16Length = _searchPlan.AlternateLiteralUtf16Lengths is { Length: > 0 } utf16Lengths &&
            (uint)match.LiteralId < (uint)utf16Lengths.Length
            ? utf16Lengths[match.LiteralId]
            : Utf8Validation.Validate(_input.Slice(matchIndex, matchByteLength)).Utf16Length;
        _segmentStartUtf16 += relativeUtf16Length + matchedUtf16Length;
        _remainingCount--;
        return true;
    }

    private bool MoveNextNativeFixedTokenPattern()
    {
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_remainingCount == 1)
        {
            return EmitTail();
        }

        _deterministicScanState.NextStartIndex = _searchStartBytes;
        _deterministicScanState.SearchFrom = Math.Max(_deterministicScanState.SearchFrom, _searchStartBytes + _structuralLinearProgram.DeterministicProgram.SearchLiteralOffset);
        if (!Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicRawMatch(
                _structuralLinearProgram,
                _input,
                ref _deterministicScanState,
                _budget,
                out var match))
        {
            return EmitTail();
        }

        var matchIndex = match.Index;
        var matchLength = match.Length;

        var segmentLength = matchIndex - _segmentStartBytes;
        Current = new Utf8ValueSplit(
            _input,
            decoded: null,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: segmentLength,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: segmentLength);
        _segmentStartBytes = matchIndex + matchLength;
        _segmentStartUtf16 = _segmentStartBytes;
        _searchStartBytes = _segmentStartBytes;
        _remainingCount--;
        return true;
    }

    private bool MoveNextNativeDeterministicPattern()
    {
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_remainingCount == 1)
        {
            return EmitTail();
        }

        _deterministicScanState.NextStartIndex = _searchStartBytes;
        _deterministicScanState.SearchFrom = Math.Max(_deterministicScanState.SearchFrom, _searchStartBytes + _structuralLinearProgram.DeterministicProgram.SearchLiteralOffset);
        if (!Utf8AsciiInstructionLinearExecutor.TryFindNextNonOverlappingDeterministicRawMatch(
                _structuralLinearProgram,
                _input,
                ref _deterministicScanState,
                _budget,
                out var match))
        {
            return EmitTail();
        }

        var matchIndex = match.Index;
        var matchLength = match.Length;

        var segmentLength = matchIndex - _segmentStartBytes;
        Current = new Utf8ValueSplit(
            _input,
            decoded: null,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: segmentLength,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: segmentLength);
        _segmentStartBytes = matchIndex + matchLength;
        _segmentStartUtf16 = _segmentStartBytes;
        _searchStartBytes = _segmentStartBytes;
        _remainingCount--;
        return true;
    }

    private bool MoveNextNativeAsciiIgnoreCaseLiterals()
    {
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_remainingCount == 1)
        {
            return EmitTail();
        }

        if (_searchPlan.HasBoundaryRequirements || _searchPlan.HasTrailingLiteralRequirement)
        {
            _budget?.Step(_input);
            if (!Utf8SearchExecutor.TryFindNextMatch(_searchPlan, _input, _searchStartBytes, out var fallbackMatch))
            {
                return EmitTail();
            }

            var fallbackMatchIndex = fallbackMatch.Index;
            var fallbackMatchByteLength = fallbackMatch.Length;
            var fallbackSegmentLength = fallbackMatchIndex - _segmentStartBytes;
            Current = new Utf8ValueSplit(
                _input,
                decoded: null,
                indexInUtf16: _segmentStartUtf16,
                lengthInUtf16: fallbackSegmentLength,
                indexInBytes: _segmentStartBytes,
                lengthInBytes: fallbackSegmentLength);

            _segmentStartBytes = fallbackMatchIndex + fallbackMatchByteLength;
            _searchStartBytes = _segmentStartBytes;
            _segmentStartUtf16 = _segmentStartBytes;
            _remainingCount--;
            return true;
        }

        if (!Utf8SearchStrategyExecutor.TryFindNextLiteralFamilyMatch(_searchPlan, _input, ref _multiLiteralScanState, _budget, out var match))
        {
            return EmitTail();
        }

        var matchIndex = match.Index;
        var matchByteLength = match.Length;

        var segmentLength = matchIndex - _segmentStartBytes;
        Current = new Utf8ValueSplit(
            _input,
            decoded: null,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: segmentLength,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: segmentLength);

        _segmentStartBytes = matchIndex + matchByteLength;
        _searchStartBytes = _segmentStartBytes;
        _segmentStartUtf16 = _segmentStartBytes;
        _remainingCount--;
        return true;
    }

    private bool MoveNextNativeSmallAsciiLiteralFamily()
    {
        if (_completed || _remainingCount <= 0)
        {
            return false;
        }

        if (_remainingCount == 1)
        {
            return EmitTail();
        }

        var startIndex = _searchStartBytes;
        if (!_smallAsciiLiteralFamilySearch.TryFindNextNonOverlapping(_input, ref startIndex, out var matchIndex, out var matchByteLength))
        {
            return EmitTail();
        }

        var segmentLength = matchIndex - _segmentStartBytes;
        Current = new Utf8ValueSplit(
            _input,
            decoded: null,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: segmentLength,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: segmentLength);

        _segmentStartBytes = matchIndex + matchByteLength;
        _searchStartBytes = _segmentStartBytes;
        _segmentStartUtf16 = _segmentStartBytes;
        _remainingCount--;
        return true;
    }

    private bool EmitTail()
    {
        Current = new Utf8ValueSplit(
            _input,
            _decoded,
            indexInUtf16: _segmentStartUtf16,
            lengthInUtf16: _totalUtf16Length - _segmentStartUtf16,
            indexInBytes: _segmentStartBytes,
            lengthInBytes: _input.Length - _segmentStartBytes);
        _completed = true;
        return true;
    }

    private enum EnumeratorMode : byte
    {
        FallbackRegex = 0,
        NativeLiteral = 1,
        NativeSimplePattern = 2,
        NativeUtf8Literals = 3,
        NativeAsciiIgnoreCaseLiterals = 4,
        NativeSmallAsciiLiteralFamily = 5,
        NativeFixedTokenPattern = 6,
        NativeDeterministicPattern = 7,
    }
}
