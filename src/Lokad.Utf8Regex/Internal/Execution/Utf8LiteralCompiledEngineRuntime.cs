using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Search;
using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Text.Unicode;

namespace Lokad.Utf8Regex.Internal.Execution;

internal sealed partial class Utf8LiteralCompiledEngineRuntime : Utf8CompiledEngineRuntime,
    IUtf8AsciiWellFormedMatchRuntime,
    IUtf8WellFormedMatchRuntime,
    IUtf8UnvalidatedMatchRuntime
{
    private const int PreparedSearcherLiteralFamilyCountThresholdBytes = 4096;
    private const int CorrelatedTwoByteLiteralCountThresholdBytes = 128 * 1024;
    private readonly Utf8CompiledEngine _compiledEngine;
    private readonly Utf8PreparedRegex _regexPlan;
    private readonly bool _usesRightToLeft;
    private readonly Utf8CompiledExecutionBackend _backend;
    private readonly Utf8SearchOperationPlan _countProgram;
    private readonly Utf8SearchOperationPlan _firstMatchProgram;
    private readonly Utf8EmittedLiteralFamilyCounter? _emittedLiteralFamilyCounter;
    private readonly byte[][]? _smallAsciiLiteralFamily;
    private readonly PreparedByteSearch _smallAsciiLiteralFamilyFirstBytes;
    private readonly PreparedMultiLiteralCandidatePrefilter _leadingUtf8SegmentLiteralFamilyPrefilter;
    private readonly PreparedMultiLiteralAutomatonSearch _leadingUtf8SegmentLiteralFamilyAutomaton;
    private readonly PreparedSmallAsciiLiteralFamilySearch? _smallAsciiLiteralFamilyPrimitive;
    private readonly PreparedShortAsciiLiteralFamilyCounter _shortAsciiLiteralFamilyCounter;
    private readonly PreparedBmpThreeByteLiteralFamilySearch _bmpThreeByteLiteralFamilySearch;
    private readonly CompositeTrailingLiteralSearch? _compositeTrailingLiteralSearch;

    private const int SmallAsciiLiteralFamilyPrimitiveProbeBytes = 16 * 1024;

    private Utf8LiteralCompiledEngineRuntime(ConstructionState state)
    {
        _compiledEngine = state.CompiledEngine;
        _regexPlan = state.RegexPlan;
        _usesRightToLeft = state.UsesRightToLeft;
        _backend = state.Backend;
        _countProgram = state.CountProgram;
        _firstMatchProgram = state.FirstMatchProgram;
        _emittedLiteralFamilyCounter = state.EmittedCounter;
        _smallAsciiLiteralFamily = state.SmallFamily;
        _smallAsciiLiteralFamilyFirstBytes = state.FirstBytes;
        _leadingUtf8SegmentLiteralFamilyPrefilter = state.LeadingPrefilter;
        _leadingUtf8SegmentLiteralFamilyAutomaton = state.LeadingAutomaton;
        _smallAsciiLiteralFamilyPrimitive = state.Primitive;
        _shortAsciiLiteralFamilyCounter = state.ShortCounter;
        _bmpThreeByteLiteralFamilySearch = state.BmpSearch;
        _compositeTrailingLiteralSearch = state.CompositeSearch;
    }

    public override Utf8CompiledRuntimeCapabilities Capabilities => new(
        SupportsAsciiWellFormedOnlyMatch: _smallAsciiLiteralFamilyPrimitive is not null || SupportsAsciiDirectMatch,
        SupportsWellFormedOnlyMatch: CanUseDirectExactSingleScalar(Utf8ExecutionDeadline.Infinite),
        WellFormedOnlyMatchMissIsDefinitive: _smallAsciiLiteralFamilyPrimitive is not null || CanUseDirectExactSingleScalar(Utf8ExecutionDeadline.Infinite),
        SupportsWellFormedOnlyCount: true,
        SupportsThrowIfInvalidOnlyCount: true,
        PreferValidateOnlyCount: false,
        SkipRequiredPrefilterForMatch: false,
        SkipRequiredPrefilterForCount: true,
        UsesEmittedWholeMatcher: false,
        UsesEmittedKernelMatcher: false);

    internal bool TryGetSmallAsciiLiteralFamilyPrimitive(
        ReadOnlySpan<byte> input,
        out PreparedSmallAsciiLiteralFamilySearch primitive)
    {
        if (_smallAsciiLiteralFamilyPrimitive is { } candidate && Utf8InputAnalyzer.IsAscii(input))
        {
            primitive = candidate;
            return true;
        }

        primitive = default;
        return false;
    }

    public override bool IsMatch(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

            if (CanUseDirectExactSingleScalar(budget))
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
                validation,
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

    public override int Count(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (_regexPlan.LiteralUtf8 is { Length: 0 } &&
            !_regexPlan.SearchPlan.HasBoundaryRequirements && !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            var emptyCursor = new Utf8OperationMatchCursor(input, GetProbeValidation(input, validation), budget);
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
                validation,
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

    public override Utf8ValueMatch Match(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (TryStartSingleLiteralInvocation(budget, out var invocation))
        {
            var plan = _regexPlan.SearchPlan;
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(plan, input, 0, input.Length,
                _usesRightToLeft, ref invocation, out var selected, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return found ? Utf8ProjectionExecutor.ProjectLiteralFamilyMatch(plan.ProjectionPlan, input,
                null, 0, 0, selected, ref invocation, out _, out _) : Utf8ValueMatch.NoMatch;
        }
        if (!_usesRightToLeft)
        {
            if (budget.IsInfinite && _compositeTrailingLiteralSearch is { } compositeSearch)
            {
                return MatchCompositeTrailingLiteral(input, compositeSearch, validation.IsAscii);
            }

            if (CanUseDirectExactSingleScalar(budget))
            {
                return _regexPlan.ExecutionKind == NativeExecutionKind.ExactAsciiLiteral
                    ? MatchExactAsciiLiteral(input, budget, rightToLeft: false)
                    : MatchExactUtf8Literal(input, budget, rightToLeft: false);
            }

            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
                _regexPlan,
                input,
                validation,
                budget);
            if (!cursor.MoveNext())
            {
                return Utf8ValueMatch.NoMatch;
            }

            var match = cursor.Current.ToValueMatch();
            // The ASCII literal cursors above project byte offsets as UTF-16
            // coordinates (this override runs without validation by design, so the
            // factory ASCII-only gate cannot see it): remap the index through the
            // UTF-16 prefix length on non-ASCII input. ASCII literal lengths match
            // in both encodings, and ASCII bytes sit on scalar boundaries, so only
            // the index needs mapping.
            if (_regexPlan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.AsciiLiteralIgnoreCase &&
                _regexPlan.LiteralUtf8 is { Length: > 0 } &&
                match.Success &&
                !Utf8InputAnalyzer.IsAscii(input))
            {
                match = new Utf8ValueMatch(
                    true,
                    match.IsByteAligned,
                    GetUtf16LengthOfPrefix(input, match.IndexInBytes),
                    match.LengthInUtf16,
                    match.IndexInBytes,
                    match.LengthInBytes);
            }

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
        // Diagnostic/backend consumers borrow their caller's original deadline;
        // the public front door also retains validation and its operation token.
        invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
        return true;
    }

    private bool CanUseDirectExactSingleScalar(Utf8ExecutionDeadline budget)
    {
        return !_usesRightToLeft &&
            budget.IsInfinite &&
            !_regexPlan.SearchPlan.HasBoundaryRequirements &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement &&
            _regexPlan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal;
    }

    private static Utf8ValueMatch MatchCompositeTrailingLiteral(
        ReadOnlySpan<byte> input,
        CompositeTrailingLiteralSearch search,
        bool inputIsAscii)
    {
        var indexInBytes = search.FindFirst(input);
        if (indexInBytes < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(
            true,
            true,
            inputIsAscii ? indexInBytes : GetUtf16LengthOfPrefix(input, indexInBytes),
            search.MatchLengthInUtf16,
            indexInBytes,
            search.MatchLengthInBytes);
    }

    public bool TryMatchAsciiWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (_smallAsciiLiteralFamilyPrimitive is { } primitive)
        {
            match = primitive.TryFindFirst(input, out var index, out var matchedLength)
                ? new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength)
                : Utf8ValueMatch.NoMatch;
            return true;
        }

        if (TryMatchAsciiDirect(input, budget: Utf8ExecutionDeadline.Infinite, out match))
        {
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    public bool TryMatchWellFormed(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (!CanUseDirectExactSingleScalar(Utf8ExecutionDeadline.Infinite))
        {
            match = Utf8ValueMatch.NoMatch;
            return false;
        }

        match = _regexPlan.ExecutionKind == NativeExecutionKind.ExactAsciiLiteral
            ? MatchExactAsciiLiteral(input, Utf8ExecutionDeadline.Infinite, rightToLeft: false)
            : MatchExactUtf8Literal(input, Utf8ExecutionDeadline.Infinite, rightToLeft: false);
        return true;
    }

    public bool ExecuteMatchWithoutValidation(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8ValueMatch match)
    {
        if (!budget.IsInfinite ||
            (_smallAsciiLiteralFamilyPrimitive is null && !SupportsAsciiDirectMatch))
        {
            match = Utf8ValueMatch.NoMatch;
            return false;
        }

        if (_smallAsciiLiteralFamilyPrimitive is { } primitive)
        {
            // The public API must reject malformed bytes even after an early
            // match. Utf8.IsValid proves that contract more cheaply than an
            // ASCII-only scan on large subjects. A non-ASCII prefix still
            // declines this shortcut because byte and UTF-16 indices diverge.
            if (!Utf8.IsValid(input))
            {
                match = Utf8ValueMatch.NoMatch;
                return false;
            }

            if (!primitive.TryFindFirst(input, out var index, out var matchedLength))
            {
                match = Utf8ValueMatch.NoMatch;
                return true;
            }

            if (!Utf8InputAnalyzer.IsAscii(input[..index]))
            {
                match = Utf8ValueMatch.NoMatch;
                return false;
            }

            match = new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
            return true;
        }

        if (!Utf8InputAnalyzer.IsAscii(input))
        {
            match = Utf8ValueMatch.NoMatch;
            return false;
        }

        return TryMatchAsciiDirect(input, budget, out match);
    }

    internal bool TryInspectMatchAsciiLiteralFamily(ReadOnlySpan<byte> input, out int index, out int matchedByteLength)
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
            _smallAsciiLiteralFamilyFirstBytes.Count > 0 &&
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

    internal bool TryInspectIsMatchLiteralFamily(ReadOnlySpan<byte> input, out bool isMatch)
    {
        if (_usesRightToLeft ||
            _regexPlan.ExecutionKind is not (NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals))
        {
            isMatch = false;
            return false;
        }

        isMatch = IsMatchLiteralFamily(input, Utf8ExecutionDeadline.Infinite, rightToLeft: false);
        return true;
    }

    internal bool TryInspectIsMatchExactLiteral(ReadOnlySpan<byte> input, out bool isMatch)
    {
        if (_usesRightToLeft ||
            _regexPlan.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal))
        {
            isMatch = false;
            return false;
        }

        isMatch = FindFirstLiteralViaSearch(input, Utf8ExecutionDeadline.Infinite) >= 0;
        return true;
    }

    internal bool TryInspectMatchExactLiteral(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (_usesRightToLeft)
        {
            match = Utf8ValueMatch.NoMatch;
            return false;
        }

        match = _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => MatchExactAsciiLiteral(
                input,
                Utf8ExecutionDeadline.Infinite,
                rightToLeft: false),
            NativeExecutionKind.ExactUtf8Literal => MatchExactUtf8Literal(input, Utf8ExecutionDeadline.Infinite, rightToLeft: false),
            _ => Utf8ValueMatch.NoMatch,
        };
        return _regexPlan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal;
    }

    private bool SupportsAsciiDirectMatch =>
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

    private bool TryMatchAsciiDirect(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out Utf8ValueMatch match)
    {
        if (budget.IsInfinite && _compositeTrailingLiteralSearch is { } compositeSearch)
        {
            match = MatchCompositeTrailingLiteral(input, compositeSearch, inputIsAscii: true);
            return true;
        }

        match = _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral => MatchExactAsciiLiteral(
                input,
                budget,
                _usesRightToLeft),
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

    internal bool TryInspectCountValidatedThreeByte(ReadOnlySpan<byte> input, out int count)
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

    internal bool TryInspectCountLeadingScalarAnchored(ReadOnlySpan<byte> input, out int count)
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

    internal bool TryInspectCountPreparedSearch(ReadOnlySpan<byte> input, out int count)
    {
        if (_regexPlan.SearchPlan.LiteralSearch is { } literalSearch)
        {
            count = literalSearch.CountWithMetrics(input, out _, out _);
            return true;
        }

        count = 0;
        return false;
    }

    internal bool TryInspectCountAnchored(ReadOnlySpan<byte> input, out int count)
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

    internal bool TryInspectSelectedCountKernelMetrics(
        ReadOnlySpan<byte> input,
        out Utf8SelectedCountKernelMetrics metrics)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (_regexPlan.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal) ||
            literal is not { Length: > 0 })
        {
            metrics = default;
            return false;
        }

        if (_regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            CanUseFusedValidatedBmpThreeByteLiteralCount(literal))
        {
            metrics = InspectValidatedThreeByteMetrics(input, literal);
            return true;
        }

        if (_regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            literal.Length >= 4 &&
            literal[0] is >= 0xC2 and < 0xE0 &&
            input.Length >= CorrelatedTwoByteLiteralCountThresholdBytes &&
            Vector256.IsHardwareAccelerated)
        {
            metrics = InspectCorrelatedTwoBytePrefixMetrics(input, literal);
            return true;
        }

        if (_regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            TryGetLeadingUtf8SegmentLength(literal, out var leadingSegmentLength))
        {
            metrics = InspectLeadingScalarMetrics(input, literal, leadingSegmentLength);
            return true;
        }

        if (_regexPlan.SearchPlan.Kind == Utf8SearchKind.ExactAsciiLiteral &&
            literal.Length is >= 5 and <= 16)
        {
            metrics = InspectFullSequenceSearchMetrics(input, literal, "SpanSequenceSearch");
            return true;
        }

        if (_regexPlan.SearchPlan.LiteralSearch.HasValue)
        {
            metrics = InspectFullSequenceSearchMetrics(input, literal, "PreparedLiteralSearch");
            return true;
        }

        if (_regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            TryGetExactUtf8LiteralAnchor(literal, out var anchorIndex, out var anchorByte))
        {
            metrics = InspectAnchoredMetrics(input, literal, anchorIndex, anchorByte);
            return true;
        }

        metrics = InspectFullSequenceSearchMetrics(input, literal, "SequenceSearchFallback");
        return true;

        static Utf8SelectedCountKernelMetrics InspectLeadingScalarMetrics(
            ReadOnlySpan<byte> input,
            ReadOnlySpan<byte> literal,
            int scalarLength)
        {
            var candidates = 0;
            var matches = 0;
            var searchIndex = 0;
            var maxStart = input.Length - literal.Length;
            var anchor = literal[..scalarLength];
            while (searchIndex <= maxStart)
            {
                var relative = input[searchIndex..].IndexOf(anchor);
                if (relative < 0)
                {
                    break;
                }

                candidates++;
                var candidate = searchIndex + relative;
                if (input.Slice(candidate, literal.Length).SequenceEqual(literal))
                {
                    matches++;
                    searchIndex = candidate + literal.Length;
                }
                else
                {
                    searchIndex = candidate + 1;
                }
            }

            return new Utf8SelectedCountKernelMetrics(
                "LeadingUtf8Scalar",
                "leading-scalar sequence hits",
                candidates,
                candidates,
                matches,
                IncludesUtf8Validation: false);
        }
    }

    private static Utf8SelectedCountKernelMetrics InspectValidatedThreeByteMetrics(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal)
    {
        var candidates = 0;
        var matches = 0;
        var index = 0;
        var maxStart = input.Length - literal.Length;
        while (index < input.Length)
        {
            if (Rune.DecodeFromUtf8(input[index..], out _, out var consumed) != OperationStatus.Done)
            {
                throw new InvalidOperationException("Selected Count metric replay received malformed UTF-8.");
            }

            if (index <= maxStart && input[index] == literal[0])
            {
                candidates++;
                if (input.Slice(index, literal.Length).SequenceEqual(literal))
                {
                    matches++;
                    index += literal.Length;
                    continue;
                }
            }

            index += consumed;
        }

        return new Utf8SelectedCountKernelMetrics(
            "FusedValidatedBmpThreeByte",
            "scalar starts matching the literal lead byte",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: true);
    }

    private static Utf8SelectedCountKernelMetrics InspectCorrelatedTwoBytePrefixMetrics(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal)
    {
        var candidates = 0;
        var matches = 0;
        var index = 0;
        var maxStart = input.Length - literal.Length;
        var first = Vector256.Create(literal[0]);
        var second = Vector256.Create(literal[1]);
        var fourth = Vector256.Create(literal[3]);
        ref var inputRef = ref MemoryMarshal.GetReference(input);

        while (index <= maxStart - (Vector256<byte>.Count - 1))
        {
            var correlated = Vector256.BitwiseAnd(
                Vector256.Equals(Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, index)), first),
                Vector256.Equals(Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, index + 1)), second));
            correlated = Vector256.BitwiseAnd(
                correlated,
                Vector256.Equals(Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, index + 3)), fourth));
            var candidateBits = correlated.ExtractMostSignificantBits();
            var matched = false;
            while (candidateBits != 0)
            {
                candidates++;
                var candidate = index + BitOperations.TrailingZeroCount(candidateBits);
                if (input.Slice(candidate, literal.Length).SequenceEqual(literal))
                {
                    matches++;
                    index = candidate + literal.Length;
                    matched = true;
                    break;
                }

                candidateBits &= candidateBits - 1;
            }

            if (!matched)
            {
                index += Vector256<byte>.Count;
            }
        }

        while (index <= maxStart)
        {
            if (input[index] == literal[0] &&
                input[index + 1] == literal[1] &&
                input[index + 3] == literal[3])
            {
                candidates++;
                if (input.Slice(index, literal.Length).SequenceEqual(literal))
                {
                    matches++;
                    index += literal.Length;
                    continue;
                }
            }

            index++;
        }

        return new Utf8SelectedCountKernelMetrics(
            "CorrelatedTwoBytePrefix",
            "starts matching literal bytes 0, 1, and 3",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: false);
    }

    private static Utf8SelectedCountKernelMetrics InspectAnchoredMetrics(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal,
        int anchorIndex,
        byte anchorByte)
    {
        var candidates = 0;
        var matches = 0;
        var searchIndex = anchorIndex;
        var maxStart = input.Length - literal.Length;
        while (searchIndex < input.Length)
        {
            var relative = input[searchIndex..].IndexOf(anchorByte);
            if (relative < 0)
            {
                break;
            }

            var anchorPosition = searchIndex + relative;
            var candidate = anchorPosition - anchorIndex;
            searchIndex = anchorPosition + 1;
            if ((uint)candidate > (uint)maxStart)
            {
                continue;
            }

            candidates++;
            if (input.Slice(candidate, literal.Length).SequenceEqual(literal))
            {
                matches++;
                searchIndex = candidate + literal.Length + anchorIndex;
            }
        }

        return new Utf8SelectedCountKernelMetrics(
            "RareByteAnchored",
            $"byte 0x{anchorByte:X2} hits at literal offset {anchorIndex}",
            candidates,
            candidates,
            matches,
            IncludesUtf8Validation: false);
    }

    private static Utf8SelectedCountKernelMetrics InspectFullSequenceSearchMetrics(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal,
        string route)
    {
        var matches = 0;
        var index = 0;
        while (index <= input.Length - literal.Length)
        {
            var relative = input[index..].IndexOf(literal);
            if (relative < 0)
            {
                break;
            }

            matches++;
            index += relative + literal.Length;
        }

        return new Utf8SelectedCountKernelMetrics(
            route,
            "internal search candidates unavailable",
            Candidates: null,
            FullVerifications: null,
            matches,
            IncludesUtf8Validation: false);
    }

    private int CountExactLiteral(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        static int CountViaSpanSearch(ReadOnlySpan<byte> haystack, ReadOnlySpan<byte> needle)
        {
            var count = 0;
            var index = 0;
            while (index <= haystack.Length - needle.Length)
            {
                var found = haystack[index..].IndexOf(needle);
                if (found < 0)
                {
                    break;
                }

                count++;
                index += found + needle.Length;
            }

            return count;
        }

        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        if (budget.IsInfinite &&
            _regexPlan.ExecutionKind == NativeExecutionKind.ExactUtf8Literal &&
            CanUseFusedValidatedBmpThreeByteLiteralCount(literal))
        {
            return CountExactUtf8LiteralValidatedThreeByte(input, literal);
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
            TryGetLeadingUtf8SegmentLength(literal, out var leadingSegmentLength))
        {
            return CountExactUtf8LiteralLeadingScalarAnchored(input, literal, leadingSegmentLength);
        }

        if (budget.IsInfinite &&
            _regexPlan.SearchPlan.Kind == Utf8SearchKind.ExactAsciiLiteral &&
            literal.Length is >= 5 and <= 16)
        {
            // For five- to sixteen-byte exact literals, the runtime sequence search keeps one
            // vectorized scan in flight instead of restarting an anchor-byte
            // scan after every match. Short literals retain their specialized
            // prepared tiers, and longer literals keep their existing policy.
            return CountViaSpanSearch(input, literal);
        }

        if (budget.IsInfinite && _regexPlan.SearchPlan.LiteralSearch is { } literalSearch)
        {
            return literalSearch.CountWithMetrics(input, out _, out _);
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

    private static int CountExactUtf8LiteralCorrelatedTwoBytePrefix(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> literal)
    {
        var count = 0;
        var index = 0;
        var maxStart = input.Length - literal.Length;
        var first = Vector256.Create(literal[0]);
        var second = Vector256.Create(literal[1]);
        var fourth = Vector256.Create(literal[3]);
        ref var inputRef = ref MemoryMarshal.GetReference(input);

        while (index <= maxStart - (Vector256<byte>.Count - 1))
        {
            // Correlate the first scalar and the final byte of the next scalar.
            // A full compare is then needed only for the surviving start lanes.
            var matches = Vector256.BitwiseAnd(
                Vector256.Equals(
                    Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, index)),
                    first),
                Vector256.Equals(
                    Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, index + 1)),
                    second));
            matches = Vector256.BitwiseAnd(
                matches,
                Vector256.Equals(
                    Vector256.LoadUnsafe(ref Unsafe.Add(ref inputRef, index + 3)),
                    fourth));
            var candidateBits = matches.ExtractMostSignificantBits();
            var matched = false;

            while (candidateBits != 0)
            {
                var candidate = index + BitOperations.TrailingZeroCount(candidateBits);
                if (input.Slice(candidate, literal.Length).SequenceEqual(literal))
                {
                    count++;
                    index = candidate + literal.Length;
                    matched = true;
                    break;
                }

                candidateBits &= candidateBits - 1;
            }

            if (!matched)
            {
                index += Vector256<byte>.Count;
            }
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

    private int CountAsciiLiteralIgnoreCase(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var literalSearch = _regexPlan.SearchPlan.LiteralSearch ?? throw UnexpectedExecutionKind();
        if (budget.IsInfinite)
        {
            return literalSearch.CountIgnoreCaseWithTier(input, literalSearch.IgnoreCaseTier, out _, out _);
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
            return Utf8BackendInstructionExecutor.CountLiteralFamily(_regexPlan.SearchPlan, _countProgram, input, budget);
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

        if (budget.IsInfinite &&
            _bmpThreeByteLiteralFamilySearch.HasValue)
        {
            return CountExactUtf8LiteralFamilyValidated(input, _bmpThreeByteLiteralFamilySearch);
        }

        if (_regexPlan.SearchPlan.HasPreparedSearcher &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            if (budget.IsInfinite && _emittedLiteralFamilyCounter is not null)
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

    private int CountAsciiLiteralIgnoreCaseLiterals(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        if (!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
            return Utf8BackendInstructionExecutor.CountLiteralFamily(_regexPlan.SearchPlan, _countProgram, input, budget);
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

    private bool IsMatchLiteralFamily(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        if (!budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
            return Utf8BackendInstructionExecutor.IsMatchLiteralFamily(_regexPlan.SearchPlan, _firstMatchProgram, input, budget, rightToLeft);
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

        return Utf8BackendInstructionExecutor.IsMatchLiteralFamily(_regexPlan.SearchPlan, _firstMatchProgram, input, budget, rightToLeft);
    }

    private Utf8ValueMatch MatchLiteralFamily(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        if (rightToLeft || !budget.IsInfinite || Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
            return Utf8BackendInstructionExecutor.MatchLiteralFamily(_regexPlan.SearchPlan, _firstMatchProgram, input,
                _regexPlan.SearchPlan.AlternateLiteralUtf16Lengths, budget, rightToLeft);
        if (!rightToLeft &&
            budget.IsInfinite &&
            _smallAsciiLiteralFamilyPrimitive is { } primitive)
        {
            return primitive.TryFindFirst(input, out var primitiveIndex, out var primitiveLength)
                ? new Utf8ValueMatch(true, true, primitiveIndex, primitiveLength, primitiveIndex, primitiveLength)
                : Utf8ValueMatch.NoMatch;
        }

        if (!rightToLeft &&
            budget.IsInfinite &&
            _smallAsciiLiteralFamily is { } smallAsciiLiteralFamily &&
            _smallAsciiLiteralFamilyFirstBytes.Count > 0)
        {
            return TryFindNextSmallAsciiLiteralFamily(input, 0, smallAsciiLiteralFamily, _smallAsciiLiteralFamilyFirstBytes, out var directIndex, out var directLength)
                ? new Utf8ValueMatch(true, true, directIndex, directLength, directIndex, directLength)
                : Utf8ValueMatch.NoMatch;
        }

        if (!rightToLeft &&
            budget.IsInfinite &&
            _emittedLiteralFamilyCounter is not null)
        {
            if (!_emittedLiteralFamilyCounter.TryMatch(input, out var index, out var matchedByteLength))
            {
                return Utf8ValueMatch.NoMatch;
            }

            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, budget, CancellationToken.None);
            var matchedUtf16Length = Utf8Validation.Validate(input.Slice(index, matchedByteLength), ref invocation).Utf16Length;
            return Utf8BackendInstructionExecutor.ProjectMatch(
                _firstMatchProgram,
                input,
                0,
                0,
                index,
                matchedByteLength,
                matchedUtf16Length,
                ref invocation,
                out _,
                out _);
        }

        if (!rightToLeft && budget.IsInfinite && TryGetExactAutomatonDirectSearch(out var exactSearch))
        {
            return exactSearch.TryFindFirstMatchWithLength(input, out var index, out var matchedLength)
                ? new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength)
                : Utf8ValueMatch.NoMatch;
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


    private Utf8ValueMatch MatchExactAsciiLiteral(
        ReadOnlySpan<byte> input,
        Utf8ExecutionDeadline budget,
        bool rightToLeft)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var index = rightToLeft
            ? FindLastLiteralViaSearch(input, budget)
            : FindFirstLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        var indexInUtf16 = Utf8InputAnalyzer.IsAscii(input) ? index : GetUtf16LengthOfPrefix(input, index);
        return new Utf8ValueMatch(true, true, indexInUtf16, literal.Length, index, literal.Length);
    }

    private Utf8ValueMatch MatchExactUtf8Literal(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
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

    private Utf8ValueMatch MatchAsciiAlignedExactUtf8Literal(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
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

    private Utf8ValueMatch MatchAsciiLiteralIgnoreCase(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft)
    {
        var literal = _regexPlan.LiteralUtf8 ?? throw UnexpectedExecutionKind();
        var index = rightToLeft
            ? FindLastIgnoreCaseLiteralViaSearch(input, budget)
            : FindFirstIgnoreCaseLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        var indexInUtf16 = Utf8InputAnalyzer.IsAscii(input) ? index : GetUtf16LengthOfPrefix(input, index);
        return new Utf8ValueMatch(true, true, indexInUtf16, literal.Length, index, literal.Length);
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

    private bool TryFindLiteralFamilyMatch(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, bool rightToLeft, out Utf8ValueMatch match)
    {
        PreparedSearchMatch preparedMatch;
        if (rightToLeft)
        {
            budget.Step();
            if (!Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out preparedMatch))
            {
                match = Utf8ValueMatch.NoMatch;
                return false;
            }
        }
        else
        {
            budget.Step();
            if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, 0, out preparedMatch))
            {
                match = Utf8ValueMatch.NoMatch;
                return false;
            }
        }

        match = new Utf8ValueMatch(true, true, preparedMatch.Index, preparedMatch.Length, preparedMatch.Index, preparedMatch.Length);
        return true;
    }

    private int FindFirstLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
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

    private int FindLastUtf8LiteralAlternationViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget.Step();
        if (!Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out var match))
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

    private int FindLastAsciiIgnoreCaseLiteralAlternationViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget.Step();
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

    private static bool TryFindNextSmallAsciiLiteralFamily(ReadOnlySpan<byte> input, int startIndex, byte[][] literals, PreparedByteSearch firstBytes, out int index, out int matchedLength)
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

    private sealed class CompositeTrailingLiteralSearch(
        PreparedSubstringSearch search,
        int matchLengthInBytes,
        int matchLengthInUtf16)
    {
        public int MatchLengthInBytes { get; } = matchLengthInBytes;

        public int MatchLengthInUtf16 { get; } = matchLengthInUtf16;

        public int FindFirst(ReadOnlySpan<byte> input) => search.IndexOf(input);
    }

    private InvalidOperationException UnexpectedExecutionKind()
    {
        return new InvalidOperationException($"Unexpected execution kind '{_regexPlan.ExecutionKind}' for compiled engine '{_compiledEngine.Kind}'.");
    }
}

internal readonly partial struct PreparedBmpThreeByteLiteralFamilySearch
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
        PreparedByteSearch primarySearch)
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
