using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Utilities;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed class Utf8LiteralCompiledEngineRuntime : Utf8CompiledEngineRuntime
{
    private const int PreparedSearcherLiteralFamilyCountThresholdBytes = 4096;
    private readonly Utf8RegexPlan _regexPlan;
    private readonly bool _usesRightToLeft;
    private readonly Utf8CompiledExecutionBackend _backend;
    private readonly Utf8BackendInstructionProgram _countProgram;
    private readonly Utf8BackendInstructionProgram _firstMatchProgram;
    private readonly Utf8BackendInstructionProgram _enumerationProgram;
    private readonly Utf8EmittedLiteralFamilyCounter? _emittedLiteralFamilyCounter;
    private readonly byte[][]? _smallAsciiLiteralFamily;
    private readonly SearchValues<byte>? _smallAsciiLiteralFamilyFirstBytes;
    private readonly PreparedMultiLiteralCandidatePrefilter _leadingUtf8SegmentLiteralFamilyPrefilter;
    private readonly PreparedMultiLiteralAutomatonSearch _leadingUtf8SegmentLiteralFamilyAutomaton;
    private readonly PreparedSmallAsciiLiteralFamilySearch? _smallAsciiLiteralFamilyPrimitive;
    private readonly PreparedShortAsciiLiteralFamilyCounter _shortAsciiLiteralFamilyCounter;
    private readonly PreparedBmpThreeByteLiteralFamilySearch _bmpThreeByteLiteralFamilySearch;

    private const int SmallAsciiLiteralFamilyPrimitiveProbeBytes = 16 * 1024;

    public Utf8LiteralCompiledEngineRuntime(Utf8CompiledEngine compiledEngine, Utf8RegexPlan regexPlan, bool usesRightToLeft)
    {
        _regexPlan = regexPlan;
        _usesRightToLeft = usesRightToLeft;
        _backend = compiledEngine.Backend;
        _countProgram = _regexPlan.SearchPlan.CountProgram;
        _firstMatchProgram = _regexPlan.SearchPlan.FirstMatchProgram;
        _enumerationProgram = _regexPlan.SearchPlan.EnumerationProgram;
        _emittedLiteralFamilyCounter = _backend == Utf8CompiledExecutionBackend.EmittedInstruction &&
                                       Utf8EmittedLiteralFamilyCounter.TryCreate(_regexPlan.SearchPlan, _countProgram, _firstMatchProgram, out var emitted)
            ? emitted
            : null;
        if (TryCreateSmallAsciiLiteralFamily(regexPlan, usesRightToLeft, out var smallAsciiLiteralFamily, out var firstBytes))
        {
            _smallAsciiLiteralFamily = smallAsciiLiteralFamily;
            _smallAsciiLiteralFamilyFirstBytes = SearchValues.Create(firstBytes);
        }

        if (TryCreateLeadingUtf8SegmentLiteralFamily(regexPlan, usesRightToLeft, out var leadingUtf8SegmentLiteralFamilyPrefilter, out var leadingUtf8SegmentLiteralFamilyAutomaton))
        {
            _leadingUtf8SegmentLiteralFamilyPrefilter = leadingUtf8SegmentLiteralFamilyPrefilter;
            _leadingUtf8SegmentLiteralFamilyAutomaton = leadingUtf8SegmentLiteralFamilyAutomaton;
        }

        if (TryCreateBmpThreeByteLiteralFamilySearch(regexPlan, usesRightToLeft, out var bmpThreeByteLiteralFamilySearch))
        {
            _bmpThreeByteLiteralFamilySearch = bmpThreeByteLiteralFamilySearch;
        }

        _smallAsciiLiteralFamilyPrimitive = TryCreateSmallAsciiLiteralFamilyPrimitive(regexPlan, usesRightToLeft, out var primitive)
            ? primitive
            : null;
        if (TryCreateShortAsciiLiteralFamilyCounter(regexPlan, usesRightToLeft, out var shortAsciiLiteralFamilyCounter))
        {
            _shortAsciiLiteralFamilyCounter = shortAsciiLiteralFamilyCounter;
        }
    }

    public override bool SupportsWellFormedOnlyCount => true;
    public override bool SupportsThrowIfInvalidOnlyCount => true;
    public override bool SkipRequiredPrefilterForCount => true;
    public override bool SupportsAsciiWellFormedOnlyMatch => _smallAsciiLiteralFamilyPrimitive is not null || SupportsAsciiDirectMatch;
    public override bool SupportsWellFormedOnlyMatch => false;
    public override bool WellFormedOnlyMatchMissIsDefinitive => _smallAsciiLiteralFamilyPrimitive is not null;

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        var rightToLeft = _usesRightToLeft;
        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal
                => rightToLeft
                    ? FindLastLiteralViaSearch(input, budget) >= 0
                    : FindFirstLiteralViaSearch(input, budget) >= 0,
            NativeExecutionKind.AsciiLiteralIgnoreCase
                => rightToLeft
                    ? FindLastIgnoreCaseLiteralViaSearch(input, budget) >= 0
                    : FindFirstIgnoreCaseLiteralViaSearch(input, budget) >= 0,
            NativeExecutionKind.ExactUtf8Literals
                => IsMatchLiteralFamily(input, budget, rightToLeft),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => IsMatchLiteralFamily(input, budget, rightToLeft),
            _ => throw UnexpectedExecutionKind(),
        };
    }

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal
                => CountExactLiteral(input, budget),
            NativeExecutionKind.AsciiLiteralIgnoreCase
                => CountAsciiLiteralIgnoreCase(input, budget),
            NativeExecutionKind.ExactUtf8Literals
                => CountExactUtf8Literals(input, budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => CountAsciiLiteralIgnoreCaseLiterals(input, budget),
            _ => throw UnexpectedExecutionKind(),
        };
    }

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        var rightToLeft = _usesRightToLeft;
        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral
                => MatchExactAsciiLiteral(input, budget, rightToLeft),
            NativeExecutionKind.ExactUtf8Literal
                => MatchExactUtf8Literal(input, budget, rightToLeft),
            NativeExecutionKind.AsciiLiteralIgnoreCase
                => MatchAsciiLiteralIgnoreCase(input, budget, rightToLeft),
            NativeExecutionKind.ExactUtf8Literals
                => MatchLiteralFamily(input, budget, rightToLeft),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => MatchLiteralFamily(input, budget, rightToLeft),
            _ => throw UnexpectedExecutionKind(),
        };
    }

    public override bool TryMatchWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public override bool TryMatchAsciiWellFormedOnly(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (_smallAsciiLiteralFamilyPrimitive is { } primitive &&
            primitive.TryFindFirst(input, out var index, out var matchedLength))
        {
            match = new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
            return true;
        }

        if (TryMatchAsciiDirect(input, budget: null, out match))
        {
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public override bool TryMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out Utf8ValueMatch match)
    {
        if (budget is null &&
            _smallAsciiLiteralFamilyPrimitive is { } primitive &&
            input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0 &&
            primitive.TryFindFirst(input, out var index, out var matchedLength))
        {
            match = new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
            return true;
        }

        if (budget is null &&
            input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0 &&
            TryMatchAsciiDirect(input, budget, out match))
        {
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public override bool TryDebugMatchAsciiLiteralFamilyRaw(ReadOnlySpan<byte> input, out int index, out int matchedByteLength)
    {
        if (!CanProjectLiteralFamilyAsAscii(input))
        {
            index = -1;
            matchedByteLength = 0;
            return false;
        }

        if (!_usesRightToLeft &&
            _smallAsciiLiteralFamilyPrimitive is { } primitive &&
            primitive.TryFindFirst(input, out index, out matchedByteLength))
        {
            return true;
        }

        if (!_usesRightToLeft &&
            _smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes is not null &&
            TryFindNextSmallAsciiLiteralFamily(input, 0, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, out index, out matchedByteLength))
        {
            return true;
        }

        if (!_usesRightToLeft &&
            _emittedLiteralFamilyCounter is not null &&
            _emittedLiteralFamilyCounter.TryMatch(input, out index, out matchedByteLength))
        {
            return true;
        }

        PreparedSearchMatch preparedMatch;
        if (_usesRightToLeft)
        {
            if (!Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out preparedMatch))
            {
                index = -1;
                matchedByteLength = 0;
                return false;
            }
        }
        else if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, 0, out preparedMatch))
        {
            index = -1;
            matchedByteLength = 0;
            return false;
        }

        index = preparedMatch.Index;
        matchedByteLength = preparedMatch.Length;
        return index >= 0;
    }

    private bool SupportsAsciiDirectMatch =>
        _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => true,
            NativeExecutionKind.ExactUtf8Literal => IsAllAscii(_regexPlan.LiteralUtf8),
            NativeExecutionKind.AsciiLiteralIgnoreCase => true,
            NativeExecutionKind.ExactUtf8Literals => _regexPlan.SearchPlan.Kind == Utf8SearchKind.ExactAsciiLiterals,
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals => true,
            _ => false,
        };

    private bool TryMatchAsciiDirect(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out Utf8ValueMatch match)
    {
        match = _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => MatchExactAsciiLiteral(input, budget, _usesRightToLeft),
            NativeExecutionKind.ExactUtf8Literal when IsAllAscii(_regexPlan.LiteralUtf8)
                => MatchAsciiAlignedExactUtf8Literal(input, budget),
            NativeExecutionKind.AsciiLiteralIgnoreCase => MatchAsciiLiteralIgnoreCase(input, budget, _usesRightToLeft),
            NativeExecutionKind.ExactUtf8Literals when _regexPlan.SearchPlan.Kind == Utf8SearchKind.ExactAsciiLiterals
                => MatchLiteralFamily(input, budget, _usesRightToLeft),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals => MatchLiteralFamily(input, budget, _usesRightToLeft),
            _ => Utf8ValueMatch.NoMatch,
        };

        return true;
    }

    private static bool IsAllAscii(byte[]? bytes)
    {
        if (bytes is null)
        {
            return false;
        }

        return bytes.AsSpan().IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0;
    }

    public override bool TryDebugCountExactUtf8LiteralValidatedThreeByte(ReadOnlySpan<byte> input, out int count)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (_regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            literal is { Length: > 0 } &&
            CanUseFusedValidatedBmpThreeByteLiteralCount(literal))
        {
            count = CountExactUtf8LiteralValidatedThreeByte(input, literal);
            return true;
        }

        count = 0;
        return false;
    }

    public override bool TryDebugCountExactUtf8LiteralLeadingScalarAnchored(ReadOnlySpan<byte> input, out int count)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (_regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            literal is { Length: > 0 } &&
            TryGetLeadingUtf8SegmentLength(literal, out var leadingSegmentLength))
        {
            count = CountExactUtf8LiteralLeadingScalarAnchored(input, literal, leadingSegmentLength);
            return true;
        }

        count = 0;
        return false;
    }

    public override bool TryDebugCountExactUtf8LiteralPreparedSearch(ReadOnlySpan<byte> input, out int count)
    {
        if (_regexPlan.SearchPlan.LiteralSearch is { } literalSearch)
        {
            count = literalSearch.CountWithMetrics(input, out _, out _);
            return true;
        }

        count = 0;
        return false;
    }

    public override bool TryDebugCountExactUtf8LiteralAnchored(ReadOnlySpan<byte> input, out int count)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (_regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            literal is { Length: > 0 } &&
            TryGetExactUtf8LiteralAnchor(literal, out var anchorIndex, out var anchorByte))
        {
            count = CountExactUtf8LiteralAnchored(input, literal, anchorIndex, anchorByte);
            return true;
        }

        count = 0;
        return false;
    }

    public override Utf8ValueMatchEnumerator CreateMatchEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8;
        var analysis = literal is { Length: 0 } ? Utf8InputAnalyzer.Analyze(input) : default;
        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: 0 }
                => new Utf8ValueMatchEnumerator(input, analysis.BoundaryMap, budget),
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => new Utf8ValueMatchEnumerator(input, _regexPlan.SearchPlan, literal, _regexPlan.ExecutionKind, budget),
            NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }
                => new Utf8ValueMatchEnumerator(input, _regexPlan.SearchPlan, literal, GetLiteralUtf16Length(literal), budget),
            NativeExecutionKind.ExactUtf8Literals
                => new Utf8ValueMatchEnumerator(input, _regexPlan.SearchPlan, budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8ValueMatchEnumerator(input, _regexPlan.SearchPlan, _regexPlan.ExecutionKind, budget),
            _ => throw UnexpectedExecutionKind(),
        };
    }

    public override Utf8ValueSplitEnumerator CreateSplitEnumerator(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (_regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            validation.IsAscii &&
            _smallAsciiLiteralFamilyPrimitive is { } primitive &&
            ShouldPreferSmallAsciiLiteralFamilyPrimitive(input, primitive))
        {
            return new Utf8ValueSplitEnumerator(input, primitive, count, budget);
        }

        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => new Utf8ValueSplitEnumerator(input, _regexPlan.SearchPlan, literal, _regexPlan.ExecutionKind, count, budget: budget),
            NativeExecutionKind.ExactUtf8Literals
                => new Utf8ValueSplitEnumerator(input, _regexPlan.SearchPlan, count, budget: budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => new Utf8ValueSplitEnumerator(input, _regexPlan.SearchPlan, count, _regexPlan.ExecutionKind, budget),
            _ => throw UnexpectedExecutionKind(),
        };
    }

    public override byte[] ReplaceExactLiteral(ReadOnlySpan<byte> input, byte[] replacementBytes, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8;
        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    literal.Length,
                    budget),
            NativeExecutionKind.ExactUtf8Literals
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                    {
                        matchIndex = FindNextUtf8LiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                        return matchIndex >= 0;
                    },
                    budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                    {
                        matchIndex = FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                        return matchIndex >= 0;
                    },
                    budget),
            _ => throw UnexpectedExecutionKind(),
        };
    }

    private int CountExactLiteral(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        if (budget is null &&
            _regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            CanUseFusedValidatedBmpThreeByteLiteralCount(literal))
        {
            return CountExactUtf8LiteralValidatedThreeByte(input, literal);
        }

        if (budget is null &&
            _regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            TryGetLeadingUtf8SegmentLength(literal, out var leadingSegmentLength))
        {
            return CountExactUtf8LiteralLeadingScalarAnchored(input, literal, leadingSegmentLength);
        }

        if (budget is null && _regexPlan.SearchPlan.LiteralSearch is { } literalSearch)
        {
            return literalSearch.CountWithMetrics(input, out _, out _);
        }

        if (budget is null &&
            _regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            TryGetExactUtf8LiteralAnchor(literal, out var anchorIndex, out var anchorByte))
        {
            return CountExactUtf8LiteralAnchored(input, literal, anchorIndex, anchorByte);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - literal.Length)
        {
            budget?.Step(input[index..]);
            var found = input[index..].IndexOf(literal);
            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + literal.Length;
        }

        return count;
    }

    private static bool CanUseFusedValidatedBmpThreeByteLiteralCount(ReadOnlySpan<byte> literal)
    {
        if (literal.Length == 0 || literal.Length % 3 != 0)
        {
            return false;
        }

        for (var i = 0; i < literal.Length; i += 3)
        {
            var b0 = literal[i];
            if (b0 < 0xE0 || b0 >= 0xF0)
            {
                return false;
            }
        }

        return true;
    }

    private static int CountExactUtf8LiteralValidatedThreeByte(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        var count = 0;
        var index = 0;
        var maxStart = input.Length - literal.Length;
        var literalFirst = literal[0];

        while (index < input.Length)
        {
            var scalarStart = index;
            var b0 = input[index];
            if (b0 < 0x80)
            {
                index++;
                continue;
            }

            if (b0 < 0xC2)
            {
                break;
            }

            if (b0 < 0xE0)
            {
                if (index + 1 >= input.Length || !IsContinuationByte(input[index + 1]))
                {
                    break;
                }

                index += 2;
                continue;
            }

            if (b0 < 0xF0)
            {
                if (index + 2 >= input.Length)
                {
                    break;
                }

                var b1 = input[index + 1];
                var b2 = input[index + 2];
                var validSecond =
                    b0 == 0xE0 ? b1 is >= 0xA0 and <= 0xBF :
                    b0 == 0xED ? b1 is >= 0x80 and <= 0x9F :
                    IsContinuationByte(b1);

                if (!validSecond || !IsContinuationByte(b2))
                {
                    break;
                }

                if (scalarStart <= maxStart &&
                    b0 == literalFirst &&
                    input.Slice(scalarStart, literal.Length).SequenceEqual(literal))
                {
                    count++;
                    index = scalarStart + literal.Length;
                    continue;
                }

                index += 3;
                continue;
            }

            if (b0 < 0xF5)
            {
                if (index + 3 >= input.Length)
                {
                    break;
                }

                var b1 = input[index + 1];
                var b2 = input[index + 2];
                var b3 = input[index + 3];
                var validSecond =
                    b0 == 0xF0 ? b1 is >= 0x90 and <= 0xBF :
                    b0 == 0xF4 ? b1 is >= 0x80 and <= 0x8F :
                    IsContinuationByte(b1);

                if (!validSecond || !IsContinuationByte(b2) || !IsContinuationByte(b3))
                {
                    break;
                }

                index += 4;
                continue;
            }

            break;
        }

        if (index < input.Length)
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
        }

        return count;
    }

    private static bool IsContinuationByte(byte value) => (value & 0xC0) == 0x80;

    private static int CountExactUtf8LiteralAnchored(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal, int anchorIndex, byte anchorByte)
    {
        var count = 0;
        var searchIndex = anchorIndex;
        var maxStart = input.Length - literal.Length;

        while (searchIndex < input.Length)
        {
            var relative = input[searchIndex..].IndexOf(anchorByte);
            if (relative < 0)
            {
                return count;
            }

            var anchorPosition = searchIndex + relative;
            var candidate = anchorPosition - anchorIndex;
            searchIndex = anchorPosition + 1;

            if ((uint)candidate > (uint)maxStart)
            {
                continue;
            }

            if (!input.Slice(candidate, literal.Length).SequenceEqual(literal))
            {
                continue;
            }

            count++;
            searchIndex = candidate + literal.Length + anchorIndex;
        }

        return count;
    }

    private static int CountExactUtf8LiteralLeadingScalarAnchored(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal, int scalarLength)
    {
        var count = 0;
        var searchIndex = 0;
        var maxStart = input.Length - literal.Length;
        var anchor = literal[..scalarLength];

        while (searchIndex <= maxStart)
        {
            var relative = input[searchIndex..].IndexOf(anchor);
            if (relative < 0)
            {
                return count;
            }

            var candidate = searchIndex + relative;
            if (input.Slice(candidate, literal.Length).SequenceEqual(literal))
            {
                count++;
                searchIndex = candidate + literal.Length;
                continue;
            }

            searchIndex = candidate + 1;
        }

        return count;
    }

    private static bool TryGetExactUtf8LiteralAnchor(ReadOnlySpan<byte> literal, out int anchorIndex, out byte anchorByte)
    {
        anchorIndex = -1;
        anchorByte = 0;

        if (literal.Length < 4)
        {
            return false;
        }

        var bestScore = int.MaxValue;
        for (var i = 1; i < literal.Length - 1; i++)
        {
            var value = literal[i];
            var frequency = 0;
            for (var j = 0; j < literal.Length; j++)
            {
                if (literal[j] == value)
                {
                    frequency++;
                }
            }

            if (frequency >= bestScore)
            {
                continue;
            }

            bestScore = frequency;
            anchorIndex = i;
            anchorByte = value;
            if (frequency == 1)
            {
                break;
            }
        }

        return anchorIndex > 0 && anchorByte < 0x80;
    }

    private static bool TryGetLeadingUtf8SegmentLength(ReadOnlySpan<byte> literal, out int segmentLength)
    {
        segmentLength = 0;

        if (literal.Length <= 3)
        {
            return false;
        }

        var starter = literal[0];
        if (starter is < 0xE0 or >= 0xF0)
        {
            return false;
        }

        if (literal.Length >= 6 &&
            literal[3] is >= 0xE0 and < 0xF0)
        {
            segmentLength = 6;
            return true;
        }

        segmentLength = 3;
        return true;
    }

    private static bool HasLeadingUtf8ScalarLength(ReadOnlySpan<byte> literal, int scalarLength)
    {
        if (literal.Length <= scalarLength)
        {
            return false;
        }

        var starter = literal[0];
        return scalarLength switch
        {
            2 => starter is >= 0xC2 and < 0xE0,
            3 => starter is >= 0xE0 and < 0xF0,
            4 => starter is >= 0xF0 and < 0xF5,
            _ => false,
        };
    }

    private int CountAsciiLiteralIgnoreCase(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var literalSearch = _regexPlan.SearchPlan.LiteralSearch ?? throw UnexpectedExecutionKind();
        if (budget is null)
        {
            var preferredCompareIndex = literalSearch.GetIgnoreCasePreferredCompareIndex();
            if (preferredCompareIndex >= 0)
            {
                return literalSearch.CountIgnoreCaseWithPreferredCompareIndex(input, preferredCompareIndex, out _, out _);
            }

            return literalSearch.CountIgnoreCaseWithTier(input, literalSearch.IgnoreCaseTier, out _, out _);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - literal.Length)
        {
            budget?.Step(input[index..]);
            var found = literalSearch.IndexOf(input[index..]);
            if (found < 0)
            {
                return count;
            }

            count++;
            index += found + literal.Length;
        }

        return count;
    }

    private int CountExactUtf8Literals(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        if (budget is null &&
            _shortAsciiLiteralFamilyCounter.HasValue)
        {
            return _shortAsciiLiteralFamilyCounter.Count(input);
        }

        if (_smallAsciiLiteralFamilyPrimitive is { } primitive &&
            ShouldPreferSmallAsciiLiteralFamilyPrimitive(input, primitive))
        {
            var primitiveCount = 0;
            var startIndex = 0;
            while (startIndex <= input.Length - primitive.ShortestLength)
            {
                budget?.Step(input[startIndex..]);
                if (!primitive.TryFindNextNonOverlapping(input, ref startIndex, out _, out _))
                {
                    return primitiveCount;
                }

                primitiveCount++;
            }

            return primitiveCount;
        }

        if (input.Length >= PreparedSearcherLiteralFamilyCountThresholdBytes &&
            _regexPlan.SearchPlan.PreparedSearcher.HasValue &&
            !_regexPlan.SearchPlan.HasBoundaryRequirements &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement &&
            _regexPlan.SearchPlan.MultiLiteralSearch.Kind != PreparedMultiLiteralKind.ExactAutomaton)
        {
            var fastCount = 0;
            var state = new PreparedMultiLiteralScanState(0, 0, 0);
            while (true)
            {
                budget?.Step(input[state.NextStart..]);
                if (!_regexPlan.SearchPlan.PreparedSearcher.TryFindNextNonOverlappingMatch(input, ref state, out _))
                {
                    return fastCount;
                }

                fastCount++;
            }
        }

        if (_smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes is not null)
        {
            return CountSmallAsciiLiteralFamily(input, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, budget);
        }

        if (budget is null &&
            _leadingUtf8SegmentLiteralFamilyPrefilter.HasValue)
        {
            return CountLeadingUtf8SegmentLiteralFamilyHybrid(input, _leadingUtf8SegmentLiteralFamilyPrefilter, _leadingUtf8SegmentLiteralFamilyAutomaton, 2048);
        }

        if (budget is null &&
            _bmpThreeByteLiteralFamilySearch.HasValue)
        {
            return CountExactUtf8LiteralFamilyValidated(input, _bmpThreeByteLiteralFamilySearch);
        }

        if (_regexPlan.SearchPlan.NativeSearch.HasPreparedSearcher &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            if (budget is null && _emittedLiteralFamilyCounter is not null)
            {
                return _emittedLiteralFamilyCounter.Count(input);
            }

            return Utf8BackendInstructionExecutor.CountLiteralFamily(_regexPlan.SearchPlan, _countProgram, input, budget);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length)
        {
            var found = FindNextUtf8LiteralAlternationViaSearch(input, index, budget, out var matchedByteLength);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + matchedByteLength;
        }

        return count;
    }

    private static bool TryCreateLeadingUtf8SegmentLiteralFamily(
        Utf8RegexPlan regexPlan,
        bool usesRightToLeft,
        out PreparedMultiLiteralCandidatePrefilter prefilter,
        out PreparedMultiLiteralAutomatonSearch automaton)
    {
        prefilter = default;
        automaton = default;

        if (usesRightToLeft ||
            regexPlan.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            regexPlan.SearchPlan.HasBoundaryRequirements ||
            regexPlan.SearchPlan.HasTrailingLiteralRequirement ||
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 1 } literals)
        {
            return false;
        }

        for (var i = 0; i < literals.Length; i++)
        {
            if (!TryGetLeadingUtf8SegmentLength(literals[i], out _))
            {
                return false;
            }
        }

        prefilter = PreparedMultiLiteralCandidatePrefilter.CreateLeadingUtf8Segment(literals);
        if (!prefilter.HasValue)
        {
            return false;
        }

        automaton = PreparedMultiLiteralAutomatonSearch.Create(literals);
        return true;
    }

    private static int CountLeadingUtf8SegmentLiteralFamilyHybrid(
        ReadOnlySpan<byte> input,
        PreparedMultiLiteralCandidatePrefilter prefilter,
        PreparedMultiLiteralAutomatonSearch automaton,
        int chunkBytes)
    {
        var count = 0;
        var position = 0;
        var shortestLength = prefilter.ShortestLength;
        var maxLiteralLength = prefilter.LongestLength;

        while (position <= input.Length - shortestLength)
        {
            var chunkEnd = Math.Min(input.Length, position + chunkBytes);
            var probeEnd = Math.Min(input.Length, chunkEnd + maxLiteralLength - 1);
            var probeWindow = input[position..probeEnd];
            var probeState = new PreparedMultiLiteralScanState(0, 0, 0);
            if (!prefilter.TryFindNextCandidate(probeWindow, ref probeState, out var candidateIndex) ||
                candidateIndex >= chunkEnd - position)
            {
                position = chunkEnd;
                continue;
            }

            var automatonState = new PreparedMultiLiteralScanState(0, 0, 0);
            var nextPosition = chunkEnd;
            while (automaton.TryFindNextNonOverlappingMatch(probeWindow, ref automatonState, out var matchIndex, out _, out _))
            {
                if (matchIndex >= chunkEnd - position)
                {
                    nextPosition = position + matchIndex;
                    break;
                }

                count++;
                nextPosition = Math.Max(chunkEnd, position + automatonState.NextStart);
            }

            position = nextPosition;
        }

        return count;
    }

    private static bool CanUseFusedValidatedBmpThreeByteLiteralFamilyCount(byte[][] literals)
    {
        for (var i = 0; i < literals.Length; i++)
        {
            if (!CanUseFusedValidatedBmpThreeByteLiteralCount(literals[i]))
            {
                return false;
            }
        }

        return literals.Length > 0;
    }

    private static bool TryCreateBmpThreeByteLiteralFamilySearch(
        Utf8RegexPlan regexPlan,
        bool usesRightToLeft,
        out PreparedBmpThreeByteLiteralFamilySearch search)
    {
        search = default;

        return !usesRightToLeft &&
            regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            regexPlan.SearchPlan.HasBoundaryRequirements == false &&
            regexPlan.SearchPlan.HasTrailingLiteralRequirement == false &&
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is { Length: > 0 } literals &&
            CanUseFusedValidatedBmpThreeByteLiteralFamilyCount(literals) &&
            PreparedBmpThreeByteLiteralFamilySearch.TryCreate(literals, out search);
    }

    private static int CountExactUtf8LiteralFamilyValidated(ReadOnlySpan<byte> input, PreparedBmpThreeByteLiteralFamilySearch search)
    {
        var count = 0;
        var index = 0;

        while (index < input.Length)
        {
            var scalarStart = index;
            var b0 = input[index];
            if (b0 < 0x80)
            {
                index++;
                continue;
            }

            if (b0 < 0xC2)
            {
                break;
            }

            if (b0 < 0xE0)
            {
                if (index + 1 >= input.Length || !IsContinuationByte(input[index + 1]))
                {
                    break;
                }

                index += 2;
                continue;
            }

            if (b0 < 0xF0)
            {
                if (index + 2 >= input.Length)
                {
                    break;
                }

                var b1 = input[index + 1];
                var b2 = input[index + 2];
                var validSecond =
                    b0 == 0xE0 ? b1 is >= 0xA0 and <= 0xBF :
                    b0 == 0xED ? b1 is >= 0x80 and <= 0x9F :
                    IsContinuationByte(b1);

                if (!validSecond || !IsContinuationByte(b2))
                {
                    break;
                }

                var matchedLength = search.TryGetMatchedLength(input, scalarStart, b0);
                if (matchedLength > 0)
                {
                    count++;
                    index = scalarStart + matchedLength;
                    continue;
                }

                index += 3;
                continue;
            }

            if (b0 < 0xF5)
            {
                if (index + 3 >= input.Length)
                {
                    break;
                }

                var b1 = input[index + 1];
                var b2 = input[index + 2];
                var b3 = input[index + 3];
                var validSecond =
                    b0 == 0xF0 ? b1 is >= 0x90 and <= 0xBF :
                    b0 == 0xF4 ? b1 is >= 0x80 and <= 0x8F :
                    IsContinuationByte(b1);

                if (!validSecond || !IsContinuationByte(b2) || !IsContinuationByte(b3))
                {
                    break;
                }

                index += 4;
                continue;
            }

            break;
        }

        if (index < input.Length)
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
        }

        return count;
    }

    private int CountAsciiLiteralIgnoreCaseLiterals(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.SearchPlan.NativeSearch.HasPreparedSearcher &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            if (budget is null && _emittedLiteralFamilyCounter is not null)
            {
                return _emittedLiteralFamilyCounter.Count(input);
            }

            return Utf8BackendInstructionExecutor.CountLiteralFamily(_regexPlan.SearchPlan, _countProgram, input, budget);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length)
        {
            var found = FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(input, index, budget, out var matchedByteLength);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + matchedByteLength;
        }

        return count;
    }

    private bool IsMatchLiteralFamily(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, bool rightToLeft)
    {
        if (!rightToLeft &&
            budget is null &&
            _smallAsciiLiteralFamilyPrimitive is { } primitive)
        {
            return primitive.TryFindFirst(input, out _, out _);
        }

        if (!rightToLeft &&
            budget is null &&
            _smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes is not null)
        {
            return TryFindNextSmallAsciiLiteralFamily(input, 0, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, out _, out _);
        }

        if (!rightToLeft && budget is null && _emittedLiteralFamilyCounter is not null)
        {
            return _emittedLiteralFamilyCounter.IsMatch(input);
        }

        return Utf8BackendInstructionExecutor.IsMatchLiteralFamily(_regexPlan.SearchPlan, _firstMatchProgram, input, budget, rightToLeft);
    }

    private Utf8ValueMatch MatchLiteralFamily(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, bool rightToLeft)
    {
        if (!rightToLeft &&
            budget is null &&
            _smallAsciiLiteralFamilyPrimitive is { } primitive &&
            primitive.TryFindFirst(input, out var primitiveIndex, out var primitiveLength))
        {
            return new Utf8ValueMatch(true, true, primitiveIndex, primitiveLength, primitiveIndex, primitiveLength);
        }

        if (!rightToLeft &&
            budget is null &&
            _smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes is not null &&
            TryFindNextSmallAsciiLiteralFamily(input, 0, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, out var directIndex, out var directLength))
        {
            return new Utf8ValueMatch(true, true, directIndex, directLength, directIndex, directLength);
        }

        if (!rightToLeft &&
            budget is null &&
            _emittedLiteralFamilyCounter is not null &&
            _emittedLiteralFamilyCounter.TryMatch(input, out var index, out var matchedByteLength))
        {
            var matchedUtf16Length = Utf8Validation.Validate(input.Slice(index, matchedByteLength)).Utf16Length;
            return Utf8BackendInstructionExecutor.ProjectMatch(
                _firstMatchProgram,
                input,
                0,
                0,
                index,
                matchedByteLength,
                matchedUtf16Length,
                out _,
                out _);
        }

        if (CanProjectLiteralFamilyAsAscii(input))
        {
            if (TryFindLiteralFamilyMatch(input, budget, rightToLeft, out var directMatch))
            {
                return directMatch;
            }

            return Utf8ValueMatch.NoMatch;
        }

        return Utf8BackendInstructionExecutor.MatchLiteralFamily(_regexPlan.SearchPlan, _firstMatchProgram, input, _regexPlan.SearchPlan.AlternateLiteralUtf16Lengths, budget, rightToLeft);
    }


    private Utf8ValueMatch MatchExactAsciiLiteral(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, bool rightToLeft)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var index = rightToLeft
            ? FindLastLiteralViaSearch(input, budget)
            : FindFirstLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(true, true, index, literal.Length, index, literal.Length);
    }

    private Utf8ValueMatch MatchExactUtf8Literal(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, bool rightToLeft)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var index = rightToLeft
            ? FindLastLiteralViaSearch(input, budget)
            : FindFirstLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(
            true,
            true,
            GetUtf16LengthOfPrefix(input, index),
            GetLiteralUtf16Length(literal),
            index,
            literal.Length);
    }

    private Utf8ValueMatch MatchAsciiAlignedExactUtf8Literal(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var index = _usesRightToLeft
            ? FindLastLiteralViaSearch(input, budget)
            : FindFirstLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(true, true, index, literal.Length, index, literal.Length);
    }

    private Utf8ValueMatch MatchAsciiLiteralIgnoreCase(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, bool rightToLeft)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var index = rightToLeft
            ? FindLastIgnoreCaseLiteralViaSearch(input, budget)
            : FindFirstIgnoreCaseLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(true, true, index, literal.Length, index, literal.Length);
    }

    private bool CanProjectLiteralFamilyAsAscii(ReadOnlySpan<byte> input)
    {
        if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
        {
            return false;
        }

        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactUtf8Literals => _regexPlan.SearchPlan.Kind == Utf8SearchKind.ExactAsciiLiterals,
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals => true,
            _ => false,
        };
    }

    private bool TryFindLiteralFamilyMatch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, bool rightToLeft, out Utf8ValueMatch match)
    {
        PreparedSearchMatch preparedMatch;
        if (rightToLeft)
        {
            budget?.Step(input);
            if (!Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out preparedMatch))
            {
                match = Utf8ValueMatch.NoMatch;
                return false;
            }
        }
        else
        {
            budget?.Step(input);
            if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, 0, out preparedMatch))
            {
                match = Utf8ValueMatch.NoMatch;
                return false;
            }
        }

        match = new Utf8ValueMatch(true, true, preparedMatch.Index, preparedMatch.Length, preparedMatch.Index, preparedMatch.Length);
        return true;
    }

    private int FindFirstLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        if (_regexPlan.SearchPlan.HasBoundaryRequirements || _regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            return Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, 0, out var match)
                ? match.Index
                : -1;
        }

        return Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, input);
    }

    private int FindLastLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        if (_regexPlan.SearchPlan.HasBoundaryRequirements || _regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            return Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out var match)
                ? match.Index
                : -1;
        }

        return Utf8SearchExecutor.FindLast(_regexPlan.SearchPlan, input);
    }

    private int FindFirstIgnoreCaseLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        if (_regexPlan.SearchPlan.HasBoundaryRequirements || _regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            return Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, 0, out var match)
                ? match.Index
                : -1;
        }

        return Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, input);
    }

    private int FindLastIgnoreCaseLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        if (_regexPlan.SearchPlan.HasBoundaryRequirements || _regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            return Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out var match)
                ? match.Index
                : -1;
        }

        return Utf8SearchExecutor.FindLast(_regexPlan.SearchPlan, input);
    }

    private int FindNextUtf8LiteralAlternationViaSearch(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        if (budget is null &&
            _smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes is not null &&
            TryFindNextSmallAsciiLiteralFamily(input, startIndex, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, out var directIndex, out matchedByteLength))
        {
            return directIndex;
        }

        budget?.Step(input);
        if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, startIndex, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
    }

    private int FindLastUtf8LiteralAlternationViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget?.Step(input);
        if (!Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
    }

    private int FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget?.Step(input);
        if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, startIndex, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
    }

    private int FindLastAsciiIgnoreCaseLiteralAlternationViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget?.Step(input);
        if (!Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
    }

    private static int GetLiteralUtf16Length(byte[] literal)
    {
        return Utf8Validation.Validate(literal).Utf16Length;
    }

    private static int GetUtf16LengthOfPrefix(ReadOnlySpan<byte> input, int byteCount)
    {
        return byteCount == 0 ? 0 : Utf8Validation.Validate(input[..byteCount]).Utf16Length;
    }

    private static bool TryCreateSmallAsciiLiteralFamily(Utf8RegexPlan regexPlan, bool usesRightToLeft, out byte[][]? literals, out byte[] firstBytes)
    {
        literals = null;
        firstBytes = [];

        if (usesRightToLeft ||
            regexPlan.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            regexPlan.SearchPlan.HasBoundaryRequirements ||
            regexPlan.SearchPlan.HasTrailingLiteralRequirement ||
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is not { Length: >= 2 and <= 8 } candidates)
        {
            return false;
        }

        Span<bool> seen = stackalloc bool[256];
        var orderedLiterals = new byte[candidates.Length][];
        var orderedFirstBytes = new byte[candidates.Length];
        for (var i = 0; i < candidates.Length; i++)
        {
            var literal = candidates[i];
            if (literal.Length == 0 || !IsAscii(literal))
            {
                return false;
            }

            var first = literal[0];
            if (seen[first])
            {
                return false;
            }

            seen[first] = true;
            orderedLiterals[i] = literal;
            orderedFirstBytes[i] = first;
        }

        Array.Sort(orderedFirstBytes, orderedLiterals, Comparer<byte>.Default);
        literals = orderedLiterals;
        firstBytes = orderedFirstBytes;
        return true;
    }

    private static bool TryCreateSmallAsciiLiteralFamilyPrimitive(Utf8RegexPlan regexPlan, bool usesRightToLeft, out PreparedSmallAsciiLiteralFamilySearch primitive)
    {
        primitive = default;

        return !usesRightToLeft &&
            regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            !regexPlan.SearchPlan.HasBoundaryRequirements &&
            !regexPlan.SearchPlan.HasTrailingLiteralRequirement &&
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is { Length: >= 3 and <= 6 } literals &&
            literals.Min(static literal => literal.Length) >= 5 &&
            PreparedSmallAsciiLiteralFamilySearch.TryCreate(literals, out primitive);
    }

    private static bool TryCreateShortAsciiLiteralFamilyCounter(
        Utf8RegexPlan regexPlan,
        bool usesRightToLeft,
        out PreparedShortAsciiLiteralFamilyCounter counter)
    {
        counter = default;

        return !usesRightToLeft &&
            regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            !regexPlan.SearchPlan.HasBoundaryRequirements &&
            !regexPlan.SearchPlan.HasTrailingLiteralRequirement &&
            regexPlan.SearchPlan.AlternateLiteralsUtf8 is { Length: >= 2 and <= 4 } literals &&
            PreparedShortAsciiLiteralFamilyCounter.TryCreate(literals, out counter);
    }

    private static bool ShouldPreferSmallAsciiLiteralFamilyPrimitive(ReadOnlySpan<byte> input, PreparedSmallAsciiLiteralFamilySearch primitive)
    {
        var probeLength = Math.Min(input.Length, SmallAsciiLiteralFamilyPrimitiveProbeBytes);
        if (probeLength < primitive.ShortestLength)
        {
            return false;
        }

        var probe = input[..probeLength];
        var startIndex = 0;
        return primitive.TryFindNextNonOverlapping(probe, ref startIndex, out _, out _);
    }

    private static int CountSmallAsciiLiteralFamily(ReadOnlySpan<byte> input, byte[][] literals, SearchValues<byte> firstBytes, Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindNextSmallAsciiLiteralFamily(input, startIndex, literals, firstBytes, out var index, out var matchedLength))
        {
            budget?.Step(input[startIndex..]);
            count++;
            startIndex = index + matchedLength;
        }

        return count;
    }

    private static bool TryFindNextSmallAsciiLiteralFamily(ReadOnlySpan<byte> input, int startIndex, byte[][] literals, SearchValues<byte> firstBytes, out int index, out int matchedLength)
    {
        while ((uint)startIndex < (uint)input.Length)
        {
            var relativeIndex = input[startIndex..].IndexOfAny(firstBytes);
            if (relativeIndex < 0)
            {
                break;
            }

            index = startIndex + relativeIndex;
            var first = input[index];
            for (var i = 0; i < literals.Length; i++)
            {
                var literal = literals[i];
                if (literal[0] != first || literal.Length > input.Length - index)
                {
                    continue;
                }

                if (input.Slice(index + 1, literal.Length - 1).SequenceEqual(literal.AsSpan(1)))
                {
                    matchedLength = literal.Length;
                    return true;
                }
            }

            startIndex = index + 1;
        }

        index = -1;
        matchedLength = 0;
        return false;
    }

    private static bool IsAscii(ReadOnlySpan<byte> value)
    {
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] >= 0x80)
            {
                return false;
            }
        }

        return true;
    }

    private InvalidOperationException UnexpectedExecutionKind()
    {
        return new InvalidOperationException($"Unexpected execution kind '{_regexPlan.ExecutionKind}' for compiled engine '{_regexPlan.CompiledEngine.Kind}'.");
    }
}

internal readonly struct PreparedBmpThreeByteLiteralFamilySearch
{
    private PreparedBmpThreeByteLiteralFamilySearch(byte[][] literals, byte[][] firstScalars, byte[][] secondScalars, byte[][] thirdScalars, int shortestLength)
    {
        Literals = literals;
        FirstScalars = firstScalars;
        SecondScalars = secondScalars;
        ThirdScalars = thirdScalars;
        ShortestLength = shortestLength;
    }

    public byte[][] Literals { get; }

    public byte[][] FirstScalars { get; }

    public byte[][] SecondScalars { get; }

    public byte[][] ThirdScalars { get; }

    public int ShortestLength { get; }

    public bool HasValue => Literals is { Length: > 0 };

    public static bool TryCreate(byte[][] literals, out PreparedBmpThreeByteLiteralFamilySearch search)
    {
        search = default;
        if (literals.Length == 0)
        {
            return false;
        }

        var shortestLength = int.MaxValue;
        var firstScalars = new List<byte[]>();
        var secondScalars = new List<byte[]>();
        var thirdScalars = new List<byte[]>();

        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            shortestLength = Math.Min(shortestLength, literal.Length);
        }

        var useSecondScalarGuard = shortestLength >= 6;
        var useThirdScalarGuard = shortestLength >= 9;

        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            if (!TryAddUniqueScalar(firstScalars, literal, 0))
            {
                return false;
            }

            if (useSecondScalarGuard && !TryAddUniqueScalar(secondScalars, literal, 3))
            {
                return false;
            }

            if (useThirdScalarGuard && !TryAddUniqueScalar(thirdScalars, literal, 6))
            {
                return false;
            }
        }

        search = new PreparedBmpThreeByteLiteralFamilySearch(
            literals,
            [.. firstScalars],
            [.. secondScalars],
            [.. thirdScalars],
            shortestLength);
        return true;
    }

    public int TryGetMatchedLength(ReadOnlySpan<byte> input, int index, byte firstByte)
    {
        if ((uint)index >= (uint)input.Length || ShortestLength > input.Length - index)
        {
            return 0;
        }

        if (!MatchesAnyScalar(input, index, firstByte, FirstScalars))
        {
            return 0;
        }

        if (SecondScalars.Length > 0 && !MatchesAnyScalar(input, index + 3, input[index + 3], SecondScalars))
        {
            return 0;
        }

        if (ThirdScalars.Length > 0 && !MatchesAnyScalar(input, index + 6, input[index + 6], ThirdScalars))
        {
            return 0;
        }

        for (var i = 0; i < Literals.Length; i++)
        {
            var literal = Literals[i];
            if (literal[0] != firstByte || literal.Length > input.Length - index)
            {
                continue;
            }

            if (input.Slice(index, literal.Length).SequenceEqual(literal))
            {
                return literal.Length;
            }
        }

        return 0;
    }

    private static bool TryAddUniqueScalar(List<byte[]> scalars, byte[] literal, int offset)
    {
        if (literal.Length < offset + 3)
        {
            return false;
        }

        var scalar = literal.AsSpan(offset, 3);
        for (var i = 0; i < scalars.Count; i++)
        {
            if (scalars[i].AsSpan().SequenceEqual(scalar))
            {
                return true;
            }
        }

        scalars.Add(scalar.ToArray());
        return true;
    }

    private static bool MatchesAnyScalar(ReadOnlySpan<byte> input, int index, byte firstByte, byte[][] scalars)
    {
        if ((uint)index > (uint)(input.Length - 3))
        {
            return false;
        }

        for (var i = 0; i < scalars.Length; i++)
        {
            var scalar = scalars[i];
            if (scalar[0] != firstByte)
            {
                continue;
            }

            if (input[index + 1] == scalar[1] && input[index + 2] == scalar[2])
            {
                return true;
            }
        }

        return false;
    }
}

internal readonly struct PreparedShortAsciiLiteralFamilyCounter
{
    private readonly byte[] _literal0;
    private readonly byte[] _literal1;
    private readonly byte[] _literal2;
    private readonly byte[] _literal3;
    private readonly byte _primary0;
    private readonly byte _primary1;
    private readonly byte _primary2;
    private readonly byte _primary3;
    private readonly byte _secondary0;
    private readonly byte _secondary1;
    private readonly byte _secondary2;
    private readonly byte _secondary3;
    private readonly byte _literalCount;
    private readonly int _primaryOffset;
    private readonly int _secondaryOffset;
    private readonly int _shortestLength;
    private readonly SearchValues<byte> _primaryValues;

    private PreparedShortAsciiLiteralFamilyCounter(
        byte[] literal0,
        byte[] literal1,
        byte[] literal2,
        byte[] literal3,
        byte primary0,
        byte primary1,
        byte primary2,
        byte primary3,
        byte secondary0,
        byte secondary1,
        byte secondary2,
        byte secondary3,
        byte literalCount,
        int primaryOffset,
        int secondaryOffset,
        int shortestLength,
        SearchValues<byte> primaryValues)
    {
        _literal0 = literal0;
        _literal1 = literal1;
        _literal2 = literal2;
        _literal3 = literal3;
        _primary0 = primary0;
        _primary1 = primary1;
        _primary2 = primary2;
        _primary3 = primary3;
        _secondary0 = secondary0;
        _secondary1 = secondary1;
        _secondary2 = secondary2;
        _secondary3 = secondary3;
        _literalCount = literalCount;
        _primaryOffset = primaryOffset;
        _secondaryOffset = secondaryOffset;
        _shortestLength = shortestLength;
        _primaryValues = primaryValues;
    }

    public bool HasValue => _shortestLength > 0;

    public static bool TryCreate(byte[][] literals, out PreparedShortAsciiLiteralFamilyCounter counter)
    {
        counter = default;
        if (literals is not { Length: >= 2 and <= 4 })
        {
            return false;
        }

        var shortestLength = int.MaxValue;
        var longestLength = 0;
        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            if (literal.Length is < 4 or > 8 || !Utf8InputAnalyzer.IsAscii(literal))
            {
                return false;
            }

            shortestLength = Math.Min(shortestLength, literal.Length);
            longestLength = Math.Max(longestLength, literal.Length);
        }

        if (!TryChooseBestUniquePair(literals, shortestLength, out var primaryOffset, out var secondaryOffset))
        {
            return false;
        }

        var primaryValues = new byte[literals.Length];
        for (var i = 0; i < literals.Length; i++)
        {
            primaryValues[i] = literals[i][primaryOffset];
        }

        counter = new PreparedShortAsciiLiteralFamilyCounter(
            literals[0],
            literals.Length > 1 ? literals[1] : [],
            literals.Length > 2 ? literals[2] : [],
            literals.Length > 3 ? literals[3] : [],
            literals[0][primaryOffset],
            literals.Length > 1 ? literals[1][primaryOffset] : default,
            literals.Length > 2 ? literals[2][primaryOffset] : default,
            literals.Length > 3 ? literals[3][primaryOffset] : default,
            literals[0][secondaryOffset],
            literals.Length > 1 ? literals[1][secondaryOffset] : default,
            literals.Length > 2 ? literals[2][secondaryOffset] : default,
            literals.Length > 3 ? literals[3][secondaryOffset] : default,
            (byte)literals.Length,
            primaryOffset,
            secondaryOffset,
            shortestLength,
            SearchValues.Create(primaryValues));
        return true;
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var startIndex = 0;
        var maxStart = input.Length - _shortestLength;
        while (startIndex <= maxStart)
        {
            var anchorIndex = startIndex + _primaryOffset;
            var relative = input[anchorIndex..].IndexOfAny(_primaryValues);
            if (relative < 0)
            {
                return count;
            }

            var candidate = anchorIndex + relative - _primaryOffset;
            if (candidate > maxStart)
            {
                return count;
            }

            if (TryMatchAt(input, candidate, out var matchedLength))
            {
                count++;
                startIndex = candidate + matchedLength;
            }
            else
            {
                startIndex = candidate + 1;
            }
        }

        return count;
    }

    private bool TryMatchAt(ReadOnlySpan<byte> input, int index, out int matchedLength)
    {
        matchedLength = 0;
        var primary = input[index + _primaryOffset];
        var secondary = input[index + _secondaryOffset];
        if (primary == _primary0 &&
            secondary == _secondary0 &&
            LiteralMatchesAt(input, index, _literal0))
        {
            matchedLength = _literal0.Length;
            return true;
        }

        if (_literalCount >= 2 &&
            primary == _primary1 &&
            secondary == _secondary1 &&
            LiteralMatchesAt(input, index, _literal1))
        {
            matchedLength = _literal1.Length;
            return true;
        }

        if (_literalCount >= 3 &&
            primary == _primary2 &&
            secondary == _secondary2 &&
            LiteralMatchesAt(input, index, _literal2))
        {
            matchedLength = _literal2.Length;
            return true;
        }

        if (_literalCount >= 4 &&
            primary == _primary3 &&
            secondary == _secondary3 &&
            LiteralMatchesAt(input, index, _literal3))
        {
            matchedLength = _literal3.Length;
            return true;
        }

        return false;
    }

    private static bool TryChooseBestUniquePair(byte[][] literals, int shortestLength, out int primaryOffset, out int secondaryOffset)
    {
        primaryOffset = -1;
        secondaryOffset = -1;
        var bestScore = int.MaxValue;
        for (var firstOffset = 0; firstOffset < shortestLength; firstOffset++)
        {
            if (!TryGetDistinctValues(literals, firstOffset, out var firstValues))
            {
                continue;
            }

            for (var secondOffset = 0; secondOffset < shortestLength; secondOffset++)
            {
                if (secondOffset == firstOffset ||
                    !HasUniquePairs(literals, firstOffset, secondOffset) ||
                    !TryGetDistinctValues(literals, secondOffset, out var secondValues))
                {
                    continue;
                }

                var score = ScoreValues(firstValues) * 4 + ScoreValues(secondValues);
                if (score < bestScore ||
                    (score == bestScore && (primaryOffset < 0 || firstOffset < primaryOffset ||
                                            (firstOffset == primaryOffset && secondOffset < secondaryOffset))))
                {
                    bestScore = score;
                    primaryOffset = firstOffset;
                    secondaryOffset = secondOffset;
                }
            }
        }

        return primaryOffset >= 0;
    }

    private static bool TryGetDistinctValues(byte[][] literals, int offset, out byte[] values)
    {
        values = [];
        Span<bool> seen = stackalloc bool[256];
        Span<byte> buffer = stackalloc byte[8];
        var count = 0;
        foreach (var literal in literals)
        {
            var value = literal[offset];
            if (seen[value])
            {
                continue;
            }

            seen[value] = true;
            if (count >= buffer.Length)
            {
                return false;
            }

            buffer[count++] = value;
        }

        if (count is <= 1 or > 6)
        {
            return false;
        }

        values = buffer[..count].ToArray();
        return true;
    }

    private static bool HasUniquePairs(byte[][] literals, int firstOffset, int secondOffset)
    {
        for (var i = 0; i < literals.Length; i++)
        {
            for (var j = i + 1; j < literals.Length; j++)
            {
                if (literals[i][firstOffset] == literals[j][firstOffset] &&
                    literals[i][secondOffset] == literals[j][secondOffset])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int ScoreValues(byte[] values)
    {
        var commonness = 0;
        for (var i = 0; i < values.Length; i++)
        {
            commonness += PreparedMultiLiteralRareBytePrefilter.GetAsciiFrequencyRank(values[i]);
        }

        return commonness * 8 + values.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LiteralMatchesAt(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal)
    {
        return (uint)index <= (uint)(input.Length - literal.Length) &&
            FastLiteralEquals(input.Slice(index, literal.Length), literal);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool FastLiteralEquals(ReadOnlySpan<byte> candidate, ReadOnlySpan<byte> literal)
    {
        ref var candidateRef = ref MemoryMarshal.GetReference(candidate);
        ref var literalRef = ref MemoryMarshal.GetReference(literal);
        switch (literal.Length)
        {
            case 5:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) == Unsafe.ReadUnaligned<uint>(ref literalRef) &&
                       Unsafe.Add(ref candidateRef, 4) == Unsafe.Add(ref literalRef, 4);
            case 6:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) == Unsafe.ReadUnaligned<uint>(ref literalRef) &&
                       Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref candidateRef, 4)) ==
                       Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref literalRef, 4));
            case 7:
                return Unsafe.ReadUnaligned<uint>(ref candidateRef) == Unsafe.ReadUnaligned<uint>(ref literalRef) &&
                       Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref candidateRef, 4)) ==
                       Unsafe.ReadUnaligned<ushort>(ref Unsafe.Add(ref literalRef, 4)) &&
                       Unsafe.Add(ref candidateRef, 6) == Unsafe.Add(ref literalRef, 6);
            case 8:
                return Unsafe.ReadUnaligned<ulong>(ref candidateRef) == Unsafe.ReadUnaligned<ulong>(ref literalRef);
            default:
                return candidate.SequenceEqual(literal);
        }
    }
}
