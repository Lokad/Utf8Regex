using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed partial class Utf8LiteralCompiledEngineRuntime
{
    private const int PreparedSearcherLiteralFamilyCountThresholdBytes = 4096;
    internal const int CorrelatedTwoByteLiteralCountThresholdBytes = 128 * 1024;
    private readonly Utf8PreparedRegex _regexPlan;
    internal readonly bool _usesRightToLeft;
    private readonly Utf8SearchOperationPlan _search;
    internal readonly Utf8EmittedLiteralFamilyCounter? _emittedLiteralFamilyCounter;
    internal readonly byte[][]? _smallAsciiLiteralFamily;
    internal readonly PreparedByteSearch _smallAsciiLiteralFamilyFirstBytes;
    private readonly PreparedMultiLiteralCandidatePrefilter _leadingUtf8SegmentLiteralFamilyPrefilter;
    private readonly PreparedMultiLiteralAutomatonSearch _leadingUtf8SegmentLiteralFamilyAutomaton;
    internal readonly PreparedSmallAsciiLiteralFamilySearch? _smallAsciiLiteralFamilyPrimitive;
    private readonly PreparedShortAsciiLiteralFamilyCounter _shortAsciiLiteralFamilyCounter;
    private readonly bool _hasThreeByteLiteralFamily;
    private readonly CompositeTrailingLiteralSearch? _compositeTrailingLiteralSearch;

    private const int SmallAsciiLiteralFamilyPrimitiveProbeBytes = 16 * 1024;

    private Utf8LiteralCompiledEngineRuntime(ConstructionState state)
    {
        _regexPlan = state.RegexPlan;
        _usesRightToLeft = state.UsesRightToLeft;
        _search = state.Search;
        _emittedLiteralFamilyCounter = state.EmittedCounter;
        _smallAsciiLiteralFamily = state.SmallFamily;
        _smallAsciiLiteralFamilyFirstBytes = state.FirstBytes;
        _leadingUtf8SegmentLiteralFamilyPrefilter = state.LeadingPrefilter;
        _leadingUtf8SegmentLiteralFamilyAutomaton = state.LeadingAutomaton;
        _smallAsciiLiteralFamilyPrimitive = state.Primitive;
        _shortAsciiLiteralFamilyCounter = state.ShortCounter;
        _hasThreeByteLiteralFamily = state.HasThreeByteLiteralFamily;
        _compositeTrailingLiteralSearch = state.CompositeSearch;
    }

    internal bool TryGetSmallAsciiLiteralFamilyPrimitive(out PreparedSmallAsciiLiteralFamilySearch primitive)
    {
        if (_smallAsciiLiteralFamilyPrimitive is { } candidate)
        {
            primitive = candidate;
            return true;
        }

        primitive = default;
        return false;
    }

    public bool IsMatch(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        if (TryStartSingleLiteralInvocation(budget, out var invocation))
        {
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_regexPlan.SearchPlan, input, 0, input.Length,
                _usesRightToLeft, ref invocation, out _, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return found;
        }
        if (!_usesRightToLeft)
        {
            if (budget.IsInfinite && _compositeTrailingLiteralSearch is { } compositeSearch)
            {
                return compositeSearch.FindFirst(input) >= 0;
            }

            if (CanUseDirectExactLiteral(budget))
            {
                return FindFirstLiteralViaSearch(input, budget) >= 0;
            }

            if (!_regexPlan.SearchPlan.HasBoundaryRequirements &&
                !_regexPlan.SearchPlan.HasTrailingLiteralRequirement &&
                _regexPlan.ExecutionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals)
            {
                return IsMatchLiteralFamily(input, budget, rightToLeft: false);
            }

            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
                _regexPlan,
                input,
                budget);
            return cursor.MoveNext();
        }

        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal
                => FindLastLiteralViaSearch(input, budget) >= 0,
            NativeExecutionKind.AsciiLiteralIgnoreCase
                => FindLastIgnoreCaseLiteralViaSearch(input, budget) >= 0,
            NativeExecutionKind.ExactUtf8Literals
                => IsMatchLiteralFamily(input, budget, rightToLeft: true),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => IsMatchLiteralFamily(input, budget, rightToLeft: true),
            _ => throw UnexpectedExecutionKind(),
        };
    }

    public int Count(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        if (_regexPlan.LiteralUtf8 is { Length: 0 } &&
            !_regexPlan.SearchPlan.HasBoundaryRequirements && !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            var emptyCursor = new Utf8OperationMatchCursor(input, budget);
            var emptyCount = 0;
            while (emptyCursor.MoveNext()) emptyCount++;
            return emptyCount;
        }
        if (TryStartSingleLiteralInvocation(budget, out var invocation))
        {
            var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(_regexPlan.SearchPlan,
                input, _usesRightToLeft, ref invocation, out var count);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return count;
        }
        if (_regexPlan.SearchPlan.HasBoundaryRequirements ||
            _regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            if (budget.IsInfinite && _emittedLiteralFamilyCounter is not null)
            {
                return _emittedLiteralFamilyCounter.Count(input);
            }

            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
                _regexPlan,
                input,
                budget);
            var constrainedCount = 0;
            while (cursor.MoveNext())
            {
                constrainedCount++;
            }

            return constrainedCount;
        }

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

    public Utf8CaptureRange Match(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        if (TryStartSingleLiteralInvocation(budget, out var invocation))
        {
            var plan = _regexPlan.SearchPlan;
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(plan, input, 0, input.Length,
                _usesRightToLeft, ref invocation, out var selected, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return found ? Utf8SliceBufferWork.CreateMatchRange(selected.Index, (selected.Index) + (selected.Length), ref invocation) : default(Utf8CaptureRange);
        }
        if (!_usesRightToLeft)
        {
            if (budget.IsInfinite && _compositeTrailingLiteralSearch is { } compositeSearch)
            {
                return MatchCompositeTrailingLiteral(input, compositeSearch);
            }

            if (CanUseDirectExactLiteral(budget))
            {
                return _regexPlan.ExecutionKind == NativeExecutionKind.ExactAsciiLiteral
                    ? MatchExactAsciiLiteral(input, budget, rightToLeft: false)
                    : MatchExactUtf8Literal(input, budget, rightToLeft: false);
            }

            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
                _regexPlan,
                input,
                budget);
            if (!cursor.MoveNext())
            {
                return default(Utf8CaptureRange);
            }

            var match = cursor.CurrentValueMatch;

            return match;
        }

        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral
                => MatchExactAsciiLiteral(input, budget, rightToLeft: true),
            NativeExecutionKind.ExactUtf8Literal
                => MatchExactUtf8Literal(input, budget, rightToLeft: true),
            NativeExecutionKind.AsciiLiteralIgnoreCase
                => MatchAsciiLiteralIgnoreCase(input, budget, rightToLeft: true),
            NativeExecutionKind.ExactUtf8Literals
                => MatchLiteralFamily(input, budget, rightToLeft: true),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => MatchLiteralFamily(input, budget, rightToLeft: true),
            _ => throw UnexpectedExecutionKind(),
        };
    }

    private bool TryStartSingleLiteralInvocation(Utf8ExecutionDeadline budget, out Utf8SliceInvocation invocation)
    {
        invocation = default;
        if (budget.IsInfinite && Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null ||
            _regexPlan.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or
                NativeExecutionKind.AsciiLiteralIgnoreCase) || _regexPlan.LiteralUtf8 is not { Length: > 0 })
            return false;
        // Backend consumers borrow their caller's deadline. Public operations
        // that carry cancellation execute with their original token.
        invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        return true;
    }

    private bool CanUseDirectExactLiteral(Utf8ExecutionDeadline budget)
    {
        return !_usesRightToLeft &&
            budget.IsInfinite &&
            !_regexPlan.SearchPlan.HasBoundaryRequirements &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement &&
            _regexPlan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal;
    }

    private static Utf8CaptureRange MatchCompositeTrailingLiteral(
        ReadOnlySpan<byte> input, CompositeTrailingLiteralSearch search)
    {
        var indexInBytes = search.FindFirst(input);
        if (indexInBytes < 0)
        {
            return default(Utf8CaptureRange);
        }

        return new Utf8CaptureRange(indexInBytes, (indexInBytes) + (search.MatchLengthInBytes));
    }

    public bool TryMatchDirect(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        if (_smallAsciiLiteralFamilyPrimitive is { } primitive)
        {
            // ASCII needles cannot overlap a multibyte scalar in valid UTF-8.
            // The result uses byte offsets, even after a non-ASCII prefix.
            match = primitive.TryFindFirst(input, out var index, out var matchedLength)
                ? new Utf8CaptureRange(index, index + matchedLength)
                : default(Utf8CaptureRange);
            return true;
        }

        if (!SupportsDirectMatch)
        {
            match = default;
            return false;
        }

        if (_compositeTrailingLiteralSearch is { } compositeSearch)
        {
            match = MatchCompositeTrailingLiteral(input, compositeSearch);
            return true;
        }

        var budget = Utf8ExecutionDeadline.Infinite;
        match = _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => MatchExactAsciiLiteral(
                input,
                budget,
                _usesRightToLeft),
            NativeExecutionKind.ExactUtf8Literal => MatchExactUtf8Literal(input, budget, _usesRightToLeft),
            NativeExecutionKind.AsciiLiteralIgnoreCase => MatchAsciiLiteralIgnoreCase(input, budget, _usesRightToLeft),
            NativeExecutionKind.ExactUtf8Literals when _regexPlan.SearchPlan.Kind == Utf8SearchKind.ExactAsciiLiterals
                => MatchLiteralFamily(input, budget, _usesRightToLeft),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals => MatchLiteralFamily(input, budget, _usesRightToLeft),
            _ => default(Utf8CaptureRange),
        };

        return true;
    }

    private bool SupportsDirectMatch =>
        _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => true,
            NativeExecutionKind.ExactUtf8Literal => IsAllAscii(_regexPlan.LiteralUtf8),
            NativeExecutionKind.AsciiLiteralIgnoreCase => true,
            NativeExecutionKind.ExactUtf8Literals =>
                _regexPlan.SearchPlan.Kind == Utf8SearchKind.ExactAsciiLiterals &&
                !_regexPlan.SearchPlan.HasBoundaryRequirements &&
                !_regexPlan.SearchPlan.HasTrailingLiteralRequirement,
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals =>
                !_regexPlan.SearchPlan.HasBoundaryRequirements &&
                !_regexPlan.SearchPlan.HasTrailingLiteralRequirement,
            _ => false,
        };

    private static bool IsAllAscii(byte[]? bytes)
    {
        if (bytes is null)
        {
            return false;
        }

        return bytes.AsSpan().IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0;
    }






    private int CountExactLiteral(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        if (budget.IsInfinite && _regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            literal.Length == 3 && literal[0] is >= 0xE0 and < 0xF0)
        {
            return CountThreeByteScalar(input, literal);
        }

        if (budget.IsInfinite &&
            _regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            literal.Length >= 4 &&
            literal[0] is >= 0xC2 and < 0xE0 &&
            input.Length >= CorrelatedTwoByteLiteralCountThresholdBytes &&
            Vector256.IsHardwareAccelerated)
        {
            return CountExactUtf8LiteralCorrelatedTwoBytePrefix(input, literal);
        }

        if (budget.IsInfinite &&
            _regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            TryGetLeadingUtf8SegmentLength(literal, out _))
        {
            if (literal.Length > 16 && _regexPlan.SearchPlan.LiteralSearch is { HasExactAnchors: true } prepared)
            {
                var anchor = prepared.GetExactCountAnchor(input.Length);
                if (anchor > 0) return CountExactUtf8LiteralAnchored(input, literal, anchor, literal[anchor]);
                return prepared.Count(input);
            }
            // Reuse complete-sequence search instead of restarting a scalar
            // anchor and rechecking each prefix. The exact byte sequence
            // already identifies scalar boundaries in valid UTF-8.
            return PreparedSubstringSearch.CountExactSequence(input, literal);
        }

        if (budget.IsInfinite && _regexPlan.SearchPlan.LiteralSearch is { } literalSearch)
        {
            return literalSearch.Count(input);
        }

        if (budget.IsInfinite &&
            _regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            TryGetExactUtf8LiteralAnchor(literal, out var anchorIndex, out var anchorByte))
        {
            return CountExactUtf8LiteralAnchored(input, literal, anchorIndex, anchorByte);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - literal.Length)
        {
            budget.Step();
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

    private static int CountThreeByteScalar(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        var count = 0;
        var first = literal[0];
        var second = literal[1];
        var third = literal[2];
        for (var index = 0; index <= input.Length - 3;)
        {
            var lead = input[index];
            if (lead == first && input[index + 1] == second && input[index + 2] == third) count++;
            index += ScalarWidthFromLead(lead);
        }
        return count;
    }

    // Both walks start at a scalar boundary in caller-valid UTF-8. Exact
    // literal comparison verifies matches and the loop admits all byte reads.
    private static int ScalarWidthFromLead(byte lead) => lead < 0x80 ? 1 : lead < 0xE0 ? 2 : lead < 0xF0 ? 3 : 4;

    private static int CountExactUtf8LiteralCorrelatedTwoBytePrefix(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal)
    {
        var count = 0;
        var index = 0;
        var maxStart = input.Length - literal.Length;
        var first = Vector256.Create(literal[0]);
        var second = Vector256.Create(literal[1]);
        var fourth = Vector256.Create(literal[3]);

        if (Vector512.IsHardwareAccelerated)
        {
            var wideFirst = Vector512.Create(literal[0]);
            var wideSecond = Vector512.Create(literal[1]);
            var wideFourth = Vector512.Create(literal[3]);
            while (index <= maxStart - (Vector512<byte>.Count - 1))
            {
                var blockStart = index;
                var block = input.Slice(blockStart, Vector512<byte>.Count + 3);
                var matches = Vector512.BitwiseAnd(
                    Vector512.Equals(MemoryMarshal.Read<Vector512<byte>>(block), wideFirst),
                    Vector512.Equals(MemoryMarshal.Read<Vector512<byte>>(block[1..]), wideSecond));
                matches = Vector512.BitwiseAnd(matches,
                    Vector512.Equals(MemoryMarshal.Read<Vector512<byte>>(block[3..]), wideFourth));
                var candidateBits = matches.ExtractMostSignificantBits();
                while (candidateBits != 0)
                {
                    var candidate = blockStart + BitOperations.TrailingZeroCount(candidateBits);
                    if (candidate >= index && input.Slice(candidate, literal.Length).SequenceEqual(literal))
                    {
                        count++;
                        index = candidate + literal.Length;
                    }
                    candidateBits &= candidateBits - 1;
                }
                index = Math.Max(index, blockStart + Vector512<byte>.Count);
            }
        }

        while (index <= maxStart - (Vector256<byte>.Count - 1))
        {
            // Correlate the first scalar and the final byte of the next scalar.
            // A full compare is then needed only for the surviving start lanes.
            var blockStart = index;
            var block = input.Slice(blockStart, Vector256<byte>.Count + 3);
            var matches = Vector256.BitwiseAnd(
                Vector256.Equals(
                    MemoryMarshal.Read<Vector256<byte>>(block),
                    first),
                Vector256.Equals(
                    MemoryMarshal.Read<Vector256<byte>>(block[1..]),
                    second));
            matches = Vector256.BitwiseAnd(
                matches,
                Vector256.Equals(
                    MemoryMarshal.Read<Vector256<byte>>(block[3..]),
                    fourth));
            var candidateBits = matches.ExtractMostSignificantBits();

            while (candidateBits != 0)
            {
                var candidate = blockStart + BitOperations.TrailingZeroCount(candidateBits);
                if (candidate >= index && input.Slice(candidate, literal.Length).SequenceEqual(literal))
                {
                    count++;
                    index = candidate + literal.Length;
                }

                candidateBits &= candidateBits - 1;
            }

            // A successful match consumes starts, not the rest of this mask.
            // Long literals can carry the next eligible start beyond the block.
            index = Math.Max(index, blockStart + Vector256<byte>.Count);
        }

        while (index <= maxStart)
        {
            if (input[index] == literal[0] &&
                input[index + 1] == literal[1] &&
                input[index + 3] == literal[3] &&
                input.Slice(index, literal.Length).SequenceEqual(literal))
            {
                count++;
                index += literal.Length;
                continue;
            }

            index++;
        }

        return count;
    }

    internal static int CountExactUtf8LiteralAnchored(ReadOnlySpan<byte> input, ReadOnlySpan<byte> literal, int anchorIndex, byte anchorByte)
    {
        var count = 0;
        var closeMisses = 0;

        while (input.Length >= literal.Length)
        {
            var candidate = input.Slice(anchorIndex, input.Length - literal.Length + 1).IndexOf(anchorByte);
            if (candidate < 0) break;
            if (input.Slice(candidate, literal.Length).SequenceEqual(literal))
            {
                count++;
                closeMisses = 0;
                input = input[(candidate + literal.Length)..];
                continue;
            }

            input = input[(candidate + 1)..];
            // Nearby misses favor one vectorized sequence search over repeated
            // calls to find a byte and verify a complete candidate.
            if (candidate >= Vector256<byte>.Count) closeMisses = 0;
            else if (++closeMisses == 4) return count + PreparedSubstringSearch.CountExactSequence(input, literal);
        }

        return count;
    }

    internal static bool TryGetExactUtf8LiteralAnchor(ReadOnlySpan<byte> literal, out int anchorIndex, out byte anchorByte)
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

    internal static bool TryGetLeadingUtf8SegmentLength(ReadOnlySpan<byte> literal, out int segmentLength)
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

    private int CountAsciiLiteralIgnoreCase(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var literalSearch = _regexPlan.SearchPlan.LiteralSearch ?? throw UnexpectedExecutionKind();
        if (budget.IsInfinite)
        {
            return literalSearch.Count(input);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - literal.Length)
        {
            budget.Step();
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

    private int CountExactUtf8Literals(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        if (!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
            return Utf8BackendInstructionExecutor.CountLiteralFamily(_regexPlan.SearchPlan, _search, input, budget);
        if (budget.IsInfinite &&
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
                budget.Step();
                if (!primitive.TryFindNextNonOverlapping(input, ref startIndex, out _, out _))
                {
                    return primitiveCount;
                }

                primitiveCount++;
            }

            return primitiveCount;
        }

        if (budget.IsInfinite &&
            input.Length >= PreparedSearcherLiteralFamilyCountThresholdBytes &&
            _smallAsciiLiteralFamily is { Length: 2 } singleByteLiteralPair &&
            _smallAsciiLiteralFamilyFirstBytes.Count > 0 &&
            (singleByteLiteralPair[0].Length == 1 || singleByteLiteralPair[1].Length == 1))
        {
            return CountSmallAsciiLiteralFamily(
                input,
                singleByteLiteralPair,
                _smallAsciiLiteralFamilyFirstBytes,
                budget);
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
                budget.Step();
                if (!_regexPlan.SearchPlan.PreparedSearcher.TryFindNextNonOverlappingMatch(input, ref state, out _))
                {
                    return fastCount;
                }

                fastCount++;
            }
        }

        if (_smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes.Count > 0)
        {
            return CountSmallAsciiLiteralFamily(input, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, budget);
        }

        if (budget.IsInfinite &&
            _leadingUtf8SegmentLiteralFamilyPrefilter.HasValue)
        {
            return CountLeadingUtf8SegmentLiteralFamilyHybrid(input, _leadingUtf8SegmentLiteralFamilyPrefilter, _leadingUtf8SegmentLiteralFamilyAutomaton, 2048);
        }

        if (_hasThreeByteLiteralFamily)
            return CountThreeByteLeadingLiteralFamily(input, _regexPlan.SearchPlan.AlternateLiteralsUtf8!);

        if (_regexPlan.SearchPlan.HasPreparedSearcher &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            if (budget.IsInfinite && _emittedLiteralFamilyCounter is not null)
            {
                return _emittedLiteralFamilyCounter.Count(input);
            }

            return Utf8BackendInstructionExecutor.CountLiteralFamily(_regexPlan.SearchPlan, _search, input, budget);
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

    private static int CountThreeByteLeadingLiteralFamily(ReadOnlySpan<byte> input, byte[][] literals)
    {
        var count = 0;
        for (var index = 0; index <= input.Length - 3;)
        {
            var lead = input[index];
            var width = ScalarWidthFromLead(lead);
            if (width == 3)
            {
                var remaining = input[index..];
                foreach (var literal in literals)
                {
                    if (literal[0] != lead || !remaining.StartsWith(literal)) continue;
                    width = literal.Length;
                    count++;
                    break;
                }
            }
            index += width;
        }
        return count;
    }

    private static int CountLeadingUtf8SegmentLiteralFamilyHybrid(
        ReadOnlySpan<byte> input, PreparedMultiLiteralCandidatePrefilter prefilter, PreparedMultiLiteralAutomatonSearch automaton, int chunkBytes)
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

    private int CountAsciiLiteralIgnoreCaseLiterals(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        if (!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
            return Utf8BackendInstructionExecutor.CountLiteralFamily(_regexPlan.SearchPlan, _search, input, budget);
        if (_regexPlan.SearchPlan.HasPreparedSearcher &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            if (budget.IsInfinite &&
                !_regexPlan.SearchPlan.HasBoundaryRequirements &&
                _regexPlan.SearchPlan.AlternateIgnoreCaseLiteralSearch is { } ignoreCaseSearch &&
                ignoreCaseSearch.TryCountWithSelectiveCorrelatedPrefilter(input, out var correlatedCount))
            {
                return correlatedCount;
            }

            if (budget.IsInfinite && _emittedLiteralFamilyCounter is not null)
            {
                return _emittedLiteralFamilyCounter.Count(input);
            }

            return Utf8BackendInstructionExecutor.CountLiteralFamily(_regexPlan.SearchPlan, _search, input, budget);
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

    internal bool IsMatchLiteralFamily(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        if (!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
            return Utf8BackendInstructionExecutor.IsMatchLiteralFamily(_regexPlan.SearchPlan, _search, input, budget, rightToLeft);
        if (!rightToLeft &&
            budget.IsInfinite &&
            _smallAsciiLiteralFamilyPrimitive is { } primitive)
        {
            return primitive.TryFindFirst(input, out _, out _);
        }

        if (!rightToLeft &&
            budget.IsInfinite &&
            _smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes.Count > 0)
        {
            return TryFindNextSmallAsciiLiteralFamily(input, 0, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, out _, out _);
        }

        if (!rightToLeft && budget.IsInfinite && _emittedLiteralFamilyCounter is not null)
        {
            return _emittedLiteralFamilyCounter.IsMatch(input);
        }

        if (!rightToLeft && budget.IsInfinite && TryGetExactAutomatonDirectSearch(out var exactSearch))
        {
            return exactSearch.TryFindFirstMatchWithLength(input, out _, out _);
        }

        return Utf8BackendInstructionExecutor.IsMatchLiteralFamily(_regexPlan.SearchPlan, _search, input, budget, rightToLeft);
    }

    private Utf8CaptureRange MatchLiteralFamily(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        if (rightToLeft || !budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
            return Utf8BackendInstructionExecutor.MatchLiteralFamily(_regexPlan.SearchPlan, _search, input, budget, rightToLeft);
        if (_smallAsciiLiteralFamilyPrimitive is { } primitive)
        {
            return primitive.TryFindFirst(input, out var primitiveIndex, out var primitiveLength)
                ? new Utf8CaptureRange(primitiveIndex, (primitiveIndex) + (primitiveLength))
                : default(Utf8CaptureRange);
        }

        if (_smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes.Count > 0)
        {
            return TryFindNextSmallAsciiLiteralFamily(input, 0, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, out var directIndex, out var directLength)
                ? new Utf8CaptureRange(directIndex, (directIndex) + (directLength))
                : default(Utf8CaptureRange);
        }

        if (_emittedLiteralFamilyCounter is not null)
        {
            if (!_emittedLiteralFamilyCounter.TryMatch(input, out var index, out var matchedByteLength))
            {
                return default(Utf8CaptureRange);
            }

            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);

            return Utf8SliceBufferWork.CreateMatchRange(index, (index) + (matchedByteLength), ref invocation);
        }

        if (TryGetExactAutomatonDirectSearch(out var exactSearch))
        {
            return exactSearch.TryFindFirstMatchWithLength(input, out var index, out var matchedLength)
                ? new Utf8CaptureRange(index, (index) + (matchedLength))
                : default(Utf8CaptureRange);
        }

        // The caller has selected byte matching for this subject.
        budget.Step();
        return Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, 0, out var match)
            ? new Utf8CaptureRange(match.Index, match.Index + match.Length)
            : default;
    }

    private bool TryGetExactAutomatonDirectSearch(out PreparedLiteralSetSearch search)
    {
        if (_regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            _regexPlan.SearchPlan.MultiLiteralSearch.Kind == PreparedMultiLiteralKind.ExactAutomaton &&
            _regexPlan.SearchPlan.AlternateLiteralSearch is
            {
                Strategy: PreparedLiteralSetStrategy.UniqueAnchorByte,
            } exactSearch)
        {
            search = exactSearch;
            return true;
        }

        search = default;
        return false;
    }


    internal Utf8CaptureRange MatchExactAsciiLiteral(
        ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var index = rightToLeft
            ? FindLastLiteralViaSearch(input, budget)
            : FindFirstLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return default(Utf8CaptureRange);
        }
        return new Utf8CaptureRange(index, (index) + (literal.Length));
    }

    internal Utf8CaptureRange MatchExactUtf8Literal(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var index = rightToLeft
            ? FindLastLiteralViaSearch(input, budget)
            : FindFirstLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return default(Utf8CaptureRange);
        }

        return new Utf8CaptureRange(index, (index) + (literal.Length));
    }

    private Utf8CaptureRange MatchAsciiLiteralIgnoreCase(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var index = rightToLeft
            ? FindLastIgnoreCaseLiteralViaSearch(input, budget)
            : FindFirstIgnoreCaseLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return default(Utf8CaptureRange);
        }
        return new Utf8CaptureRange(index, (index) + (literal.Length));
    }

    internal int FindFirstLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        budget.Step();
        if (_regexPlan.SearchPlan.HasBoundaryRequirements || _regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            return Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, 0, out var match)
                ? match.Index
                : -1;
        }

        return Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, input);
    }

    private int FindLastLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        budget.Step();
        if (_regexPlan.SearchPlan.HasBoundaryRequirements || _regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            return Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out var match)
                ? match.Index
                : -1;
        }

        return Utf8SearchExecutor.FindLast(_regexPlan.SearchPlan, input);
    }

    private int FindFirstIgnoreCaseLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        budget.Step();
        if (_regexPlan.SearchPlan.HasBoundaryRequirements || _regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            return Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, 0, out var match)
                ? match.Index
                : -1;
        }

        return Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, input);
    }

    private int FindLastIgnoreCaseLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        budget.Step();
        if (_regexPlan.SearchPlan.HasBoundaryRequirements || _regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            return Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out var match)
                ? match.Index
                : -1;
        }

        return Utf8SearchExecutor.FindLast(_regexPlan.SearchPlan, input);
    }

    private int FindNextUtf8LiteralAlternationViaSearch(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionDeadline budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        if (budget.IsInfinite &&
            _smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes.Count > 0 &&
            TryFindNextSmallAsciiLiteralFamily(input, startIndex, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, out var directIndex, out matchedByteLength))
        {
            return directIndex;
        }

        budget.Step();
        if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, startIndex, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
    }

    private int FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionDeadline budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget.Step();
        if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, startIndex, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
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

    private static int CountSmallAsciiLiteralFamily(ReadOnlySpan<byte> input, byte[][] literals, PreparedByteSearch firstBytes, Utf8ExecutionDeadline budget)
    {
        var count = 0;
        var startIndex = 0;
        while (TryFindNextSmallAsciiLiteralFamily(input, startIndex, literals, firstBytes, out var index, out var matchedLength))
        {
            budget.Step();
            count++;
            startIndex = index + matchedLength;
        }

        return count;
    }

    internal static bool TryFindNextSmallAsciiLiteralFamily(ReadOnlySpan<byte> input, int startIndex, byte[][] literals, PreparedByteSearch firstBytes, out int index, out int matchedLength)
    {
        while ((uint)startIndex < (uint)input.Length)
        {
            var relativeIndex = firstBytes.IndexOf(input[startIndex..]);
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

    private sealed class CompositeTrailingLiteralSearch
    {
        private readonly PreparedSubstringSearch _search;

        public CompositeTrailingLiteralSearch(PreparedSubstringSearch search, int matchLengthInBytes)
        {
            _search = search;
            MatchLengthInBytes = matchLengthInBytes;
        }

        public int MatchLengthInBytes { get; }
        public int FindFirst(ReadOnlySpan<byte> input) => _search.IndexOf(input);

    }

    private InvalidOperationException UnexpectedExecutionKind()
    {
        return new InvalidOperationException($"Unexpected execution kind '{_regexPlan.ExecutionKind}' for the literal runtime.");
    }
}

internal readonly partial struct PreparedShortAsciiLiteralFamilyCounter
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
    private readonly PreparedByteSearch _primarySearch;

    private PreparedShortAsciiLiteralFamilyCounter(
        byte[] literal0, byte[] literal1, byte[] literal2, byte[] literal3, byte primary0, byte primary1, byte primary2, byte primary3, byte secondary0, byte secondary1, byte secondary2, byte secondary3, byte literalCount, int primaryOffset, int secondaryOffset, int shortestLength, PreparedByteSearch primarySearch)
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
        _primarySearch = primarySearch;
    }

    public bool HasValue => _shortestLength > 0;

    public int Count(ReadOnlySpan<byte> input)
    {
        var count = 0;
        var startIndex = 0;
        var maxStart = input.Length - _shortestLength;
        while (startIndex <= maxStart)
        {
            var anchorIndex = startIndex + _primaryOffset;
            var relative = _primarySearch.IndexOf(input[anchorIndex..]);
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

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool LiteralMatchesAt(ReadOnlySpan<byte> input, int index, ReadOnlySpan<byte> literal)
        => Utf8LiteralEquality.EqualsAt(input, index, literal, Utf8LiteralComparisonKind.FastShort);
}
