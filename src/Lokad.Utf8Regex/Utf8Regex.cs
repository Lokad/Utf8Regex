using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Search;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;
using Lokad.Utf8Regex.Internal.Replacement;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Unicode;

namespace Lokad.Utf8Regex;

internal enum Utf8PrioritizedBooleanFamilyKind : byte
{
    None = 0,
    LeadingAnyRunTrailingAsciiLiteral = 1,
    AnchoredBoundedDate = 2,
    CompiledAnchoredOptionalField = 3,
    AsciiBoundedDateToken = 4,
    AnchoredAsciiLeadingDigitsTail = 5,
    CompiledRepeatedDigitGroup = 6,
    AnchoredAsciiSignedDecimalWhole = 7,
    AnchoredAsciiOptionalFieldWhole = 8,
    AsciiUriToken = 9,
    AsciiIpv4Token = 10,
}

/// <summary>Matches Unicode scalars directly over well-formed UTF-8 input.</summary>
/// <remarks>Byte ranges are primary; UTF-16 coordinates are derived interoperability values.</remarks>
public sealed partial class Utf8Regex
{
    private static TimeSpan s_defaultMatchTimeout = Regex.InfiniteMatchTimeout;

    private readonly Utf8RegexProgram _program;
    private readonly Utf8PrioritizedBooleanFamilyKind _prioritizedBooleanFamilyKind;
    private readonly bool _prioritizePlainAsciiLiteralCount;
    private readonly int _fusedAsciiLiteralCountAnchorOffset;
    private readonly bool _prioritizeBoundaryLiteralFamilyCount;
    private readonly Utf8EmittedLiteralFamilyCounter? _fusedAsciiBoundaryLiteralFamilyCounter;
    private readonly bool _prioritizeOrderedLiteralWindowCount;
    private readonly bool _requiresKelvinSignFallback;
    private readonly Utf8InvariantCyrillicLiteralCountStrategy? _invariantCyrillicLiteralCountStrategy;
    private readonly Utf8PackedNibbleLiteralFamilyCountStrategy? _packedNibbleLiteralFamilyCountStrategy;
    private readonly Utf8LiteralFamilyCaptureMap? _literalFamilyCaptureMap;
    private readonly bool _hasCaptures;
    private readonly Utf8GroupedLiteralSpanTable? _groupedLiteralSpanTable;
    private readonly byte[]? _caretAnchoredGroupedLiteralUtf8;
    private readonly Utf8AlternationBranchSpanMap? _alternationBranchSpanMap;
    private readonly Utf8SliceProgram? _sliceProgram;
    private readonly Utf8SliceProgram? _ownedSliceProgram;
    private readonly Utf8ReplacementPlanCache _replacementCache = new();

    /// <summary>Compiles a culture-invariant expression with <see cref="DefaultMatchTimeout"/>.</summary>
    public Utf8Regex(string pattern)
        : this(pattern, RegexOptions.CultureInvariant, DefaultMatchTimeout)
    {
    }

    /// <summary>Compiles an expression with the specified .NET regex options and <see cref="DefaultMatchTimeout"/>.</summary>
    public Utf8Regex(string pattern, RegexOptions options)
        : this(pattern, options, DefaultMatchTimeout)
    {
    }

    /// <summary>Compiles an expression with the specified options, match timeout, and invocation resource policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A policy budget is zero; use null for unlimited.</exception>
    /// <exception cref="NotSupportedException">A work, depth, working-bytes, or result-bytes budget is set. This profile enforces only match timeouts.</exception>
    public Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy)
        : this(pattern, options, matchTimeout, resourcePolicy, checkPolicy: true)
    {
    }

    /// <summary>Compiles an expression with the specified .NET regex options and match timeout.</summary>
    public Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout)
        : this(pattern, options, matchTimeout, Utf8ResourcePolicy.Unlimited, checkPolicy: false)
    {
    }

    /// <summary>Shared construction worker. Validates the policy before any parsing or preparation when requested.</summary>
    private Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy, bool checkPolicy)
    {
        if (checkPolicy)
        {
            ValidateCorePolicy(resourcePolicy, nameof(resourcePolicy));
        }
        using var construction = Utf8ConstructionScope.Enter();

        Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout));
        ValidateOptions(options);
        Pattern = pattern;
        Options = options;
        MatchTimeout = matchTimeout;

        _program = Utf8RegexProgram.Compile(pattern, options, matchTimeout);
        var preparedRegex = _program.PreparedRegex;
        _prioritizedBooleanFamilyKind = SelectPrioritizedBooleanFamily(preparedRegex, options);
        _prioritizePlainAsciiLiteralCount = CanPrioritizePlainAsciiLiteralCount(pattern, preparedRegex, options);
        _fusedAsciiLiteralCountAnchorOffset =
            _prioritizePlainAsciiLiteralCount &&
            matchTimeout == Regex.InfiniteMatchTimeout &&
            preparedRegex.LiteralUtf8 is { } fusedAsciiLiteral
                ? Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset(fusedAsciiLiteral)
                : -1;
        _prioritizeBoundaryLiteralFamilyCount =
            (options & RegexOptions.RightToLeft) == 0 &&
            (Utf8EmittedLiteralFamilyCounter.CanCreateBoundaryOnlyStructuralFamily(preparedRegex) ||
             preparedRegex.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
             preparedRegex.SearchPlan.HasBoundaryRequirements &&
             Utf8EmittedLiteralFamilyCounter.CanCreate(
                 preparedRegex.SearchPlan,
                 preparedRegex.SearchPlan.CountOperation,
                 preparedRegex.SearchPlan.FirstMatchOperation));
        if (matchTimeout == Regex.InfiniteMatchTimeout &&
            _program.CompiledEngineRuntime is Utf8StructuralLinearAutomatonCompiledEngineRuntime structuralRuntime &&
            structuralRuntime.EmittedBoundaryLiteralFamily is { SupportsAsciiValidatedCount: true } boundaryLiteralFamilyCounter)
        {
            _fusedAsciiBoundaryLiteralFamilyCounter = boundaryLiteralFamilyCounter;
        }
        _prioritizeOrderedLiteralWindowCount =
            preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow;
        _requiresKelvinSignFallback = RequiresKelvinSignFallback(_program.PreparedRegex);
        _invariantCyrillicLiteralCountStrategy =
            Utf8InvariantCyrillicLiteralCountStrategy.TryCreate(
                pattern,
                options,
                matchTimeout,
                out var invariantCyrillicLiteralCountStrategy)
                    ? invariantCyrillicLiteralCountStrategy
                    : null;
        _packedNibbleLiteralFamilyCountStrategy =
            Utf8PackedNibbleLiteralFamilyCountStrategy.TryCreate(
                preparedRegex,
                options,
                matchTimeout,
                out var packedNibbleLiteralFamilyCountStrategy)
                    ? packedNibbleLiteralFamilyCountStrategy
                    : null;
        _literalFamilyCaptureMap = Utf8LiteralFamilyCaptureMap.TryCreate(preparedRegex.ExecutionTree);
        _hasCaptures = HasOwnedCaptures(preparedRegex.ExecutionTree);
        _groupedLiteralSpanTable = UsesRightToLeft() ? null : Utf8GroupedLiteralSpanTable.TryCreate(preparedRegex.ExecutionTree);
        _caretAnchoredGroupedLiteralUtf8 = TryGetCaretAnchoredGroupedLiteral(preparedRegex.ExecutionTree, options, _groupedLiteralSpanTable, out var caretLiteral) ? caretLiteral : null;
        _alternationBranchSpanMap = UsesRightToLeft() ? null : Utf8AlternationBranchSpanMap.TryCreate(preparedRegex.ExecutionTree);
        // R02a: fallback-shaped literal/concat/alt/repeat/capture patterns run the owned
        // scalar slice. Instruction-capacity exhaustion fails in owned admission;
        // pattern nesting and resource failure do not select a BCL engine.
        // NonBacktracking regular graphs lower to ordered scalar NFA states,
        // including loops and shared captures. They do not use the backtracker.
        var ownedSliceProgram = preparedRegex.ScalarProgram;
        _sliceProgram = preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex ? ownedSliceProgram : null;
        _ownedSliceProgram = ownedSliceProgram;
        // Syntax failures already have owned diagnostics. This residual
        // metadata construction for an absent owned tree is migration debt;
        // class payload size no longer selects it.
        if (_sliceProgram is null && preparedRegex.SemanticRegex.RuntimeTree is null)
        {
            _ = _program.GroupNumbers.Value;
        }

        // The front end rejects parsed nonregular constructs itself. Only
        // declined parsing/unsupported lowering still reach
        // this transitional validation engine.
        if ((options & RegexOptions.NonBacktracking) != 0 && ownedSliceProgram is null)
        {
            _ = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex;
        }

        // Unserved conditional representations retain transitional validation.
        // This heuristic gate remains removal debt; parsed native conditionals
        // already have an owned scalar program and do not construct this engine.
        if (ownedSliceProgram is null && pattern.Contains("(?(", StringComparison.Ordinal))
        {
            _ = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex;
        }

        static bool CanPrioritizePlainAsciiLiteralCount(
            string pattern,
            Utf8PreparedRegex preparedRegex,
            RegexOptions options)
        {
            if (preparedRegex.ExecutionKind != NativeExecutionKind.ExactAsciiLiteral ||
                (options & RegexOptions.RightToLeft) != 0 ||
                preparedRegex.LiteralUtf8 is not { } literal ||
                pattern.Length != literal.Length)
            {
                return false;
            }

            for (var i = 0; i < literal.Length; i++)
            {
                if (pattern[i] != literal[i])
                {
                    return false;
                }
            }

            return true;
        }

        static Utf8PrioritizedBooleanFamilyKind SelectPrioritizedBooleanFamily(
            Utf8PreparedRegex preparedRegex,
            RegexOptions options)
        {
            if (preparedRegex.FallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral)
            {
                return Utf8PrioritizedBooleanFamilyKind.LeadingAnyRunTrailingAsciiLiteral;
            }

            if (preparedRegex.SimplePatternPlan.AnchoredBoundedDatePlan.HasValue)
            {
                return Utf8PrioritizedBooleanFamilyKind.AnchoredBoundedDate;
            }

            if ((options & RegexOptions.Compiled) != 0)
            {
                if (preparedRegex.SimplePatternPlan.AnchoredOptionalFieldPlan.HasValue)
                {
                    return Utf8PrioritizedBooleanFamilyKind.CompiledAnchoredOptionalField;
                }

                if (preparedRegex.SimplePatternPlan.RepeatedDigitGroupPlan.HasValue)
                {
                    return Utf8PrioritizedBooleanFamilyKind.CompiledRepeatedDigitGroup;
                }
            }

            if (preparedRegex.SimplePatternPlan.AnchoredOptionalFieldPlan.CanUseShortAsciiWholeMatcher)
            {
                return Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiOptionalFieldWhole;
            }

            return preparedRegex.FallbackDirectFamily.Kind switch
            {
                Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole =>
                    Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiSignedDecimalWhole,
                Utf8FallbackDirectFamilyKind.Utf8UriToken =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiUriToken,
                Utf8FallbackDirectFamilyKind.AsciiIpv4Token =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiIpv4Token,
                Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiIpv4Token,
                Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiBoundedDateToken,
                Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail =>
                    Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiLeadingDigitsTail,
                _ => Utf8PrioritizedBooleanFamilyKind.None,
            };
        }

        ResourcePolicy = resourcePolicy;
        construction.Complete();
    }

    private static void ValidateCorePolicy(Utf8ResourcePolicy resourcePolicy, string paramName)
    {
        resourcePolicy.Validate(paramName);
        if (resourcePolicy.MaxWorkSteps.HasValue || resourcePolicy.MaxDepth.HasValue || resourcePolicy.MaxWorkingBytes.HasValue || resourcePolicy.MaxResultBytes.HasValue)
        {
            throw new NotSupportedException("This profile enforces only match timeouts; work, depth, and memory budgets are not supported here.");
        }
    }

    /// <summary>Gets or sets the timeout used by constructors and cached operations that do not specify one.</summary>
    /// <remarks>The initial value is <see cref="Regex.InfiniteMatchTimeout"/>.</remarks>
    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set
        {
            s_defaultMatchTimeout = Utf8MatchTimeout.Validate(value, nameof(value));
        }
    }

    internal static TimeSpan DefaultTimeout => DefaultMatchTimeout;

    /// <summary>Gets the regular-expression pattern.</summary>
    public string Pattern { get; }

    /// <summary>Gets the .NET regular-expression options used during compilation.</summary>
    public RegexOptions Options { get; }

    /// <summary>Gets the maximum duration of an individual matching operation.</summary>
    public TimeSpan MatchTimeout { get; }

    /// <summary>Gets the invocation resource policy.</summary>
    public Utf8ResourcePolicy ResourcePolicy { get; }

    private Utf8PreparedRegex _preparedRegex => _program.PreparedRegex;

    private Utf8CompiledEngine _compiledEngine => _program.CompiledEngine;

    private Utf8VerifierRuntime _verifierRuntime => _program.VerifierRuntime;

    private Utf8CompiledEngineRuntime _compiledEngineRuntime => _program.CompiledEngineRuntime;

    private Utf8EmittedKernelMatcher? _directStructuralFamilyKernelMatcher =>
        _program.DirectStructuralFamilyKernelMatcher;

    private int[] _groupNumbers => _sliceProgram is not null ? _sliceProgram.GroupNumbers : _program.GroupNumbers.Value;

    private string[] _groupNames => _sliceProgram is not null ? _sliceProgram.GroupNames : _program.GroupNames.Value;

    private bool _allowsTrailingNewlineBeforeEnd =>
        _preparedRegex.SimplePatternPlan.AllowsTrailingNewlineBeforeEnd;

    private AsciiSimplePatternAnchoredHeadTailRunPlan _anchoredHeadTailRunPlan =>
        _preparedRegex.SimplePatternPlan.AnchoredHeadTailRunPlan;

    private AsciiSimplePatternAnchoredValidatorPlan _anchoredValidatorPlan =>
        _preparedRegex.SimplePatternPlan.AnchoredValidatorPlan;

    private AsciiSimplePatternAnchoredBoundedDatePlan _anchoredBoundedDatePlan =>
        _preparedRegex.SimplePatternPlan.AnchoredBoundedDatePlan;

    private AsciiSimplePatternAnchoredOptionalFieldPlan _anchoredOptionalFieldPlan =>
        _preparedRegex.SimplePatternPlan.AnchoredOptionalFieldPlan;

    private ref readonly Utf8FallbackDirectFamilyPlan _fallbackDirectFamily =>
        ref _preparedRegex.FallbackDirectFamily;

    private bool _hasDirectAnchoredHeadTailWithoutValidation =>
        _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
        _anchoredHeadTailRunPlan.HasValue;

    private bool _hasDirectAnchoredValidatorWithoutValidation =>
        _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
        _anchoredValidatorPlan.HasValue;

    private bool _hasDirectFallbackTokenFamilyWithoutValidation =>
        Utf8FallbackDirectFamilyCategories.IsTokenFindFamily(_fallbackDirectFamily.Kind);

    private bool _hasDirectFallbackAsciiFamilyWithoutValidation =>
        (_fallbackDirectFamily.SupportsAsciiDefinitiveIsMatch ||
         _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole) &&
        !_hasDirectFallbackTokenFamilyWithoutValidation;

    private NativeExecutionKind ExecutionKind => _preparedRegex.ExecutionKind;

    private Utf8PreparedRegex PreparedRegex => _preparedRegex;

    internal Utf8ByteOffsetExecution ByteOffsetExecution => new(this); // PCRE2-INTEGRATION-POINT

    internal Utf8RegexInspection Inspection => new(this);

    private Utf8SearchPlan SearchPlan => _preparedRegex.SearchPlan;

    private Utf8StructuralSearchPlan StructuralSearchPlan => _preparedRegex.StructuralSearchPlan;

    private Utf8SearchPortfolioKind SearchPortfolioKind => _preparedRegex.SearchPlan.PortfolioKind;

    private Utf8CompiledEngineKind CompiledEngineKind => _compiledEngine.Kind;

    private Utf8CompiledExecutionBackend CompiledExecutionBackend => _compiledEngine.Backend;

    private Utf8StructuralLinearProgramKind StructuralLinearProgramKind => _preparedRegex.StructuralLinearProgram.Kind;

    private Utf8StructuralVerifierPlan StructuralVerifierPlan => _verifierRuntime.StructuralVerifierPlan;

    private AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan => _preparedRegex.StructuralIdentifierFamilyPlan;

    private Utf8AsciiStructuralIdentifierFamilyExecutor.SharedPrefixSuffixKernelDiagnostics
        DebugStructuralSharedPrefixSuffixKernelDiagnostics =>
            _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily
                ? Utf8AsciiStructuralIdentifierFamilyExecutor.GetSharedPrefixSuffixKernelDiagnostics(
                    _preparedRegex.StructuralIdentifierFamilyPlan,
                    _preparedRegex.SearchPlan)
                : default;

    private AsciiSimplePatternPlan SimplePatternPlan => _preparedRegex.SimplePatternPlan;

    private string? FallbackReason => _preparedRegex.FallbackReason;

    private static void ValidateOptions(RegexOptions options)
    {
        // R00: owned validation; no BCL regex construction. Matches BCL ValidateOptions semantics.
        Utf8RegexSyntax.ValidateOptions(options);
    }

    private static Utf8ValueMatch CreateValueMatch(ReadOnlySpan<byte> input, Utf8BoundaryMap boundaryMap, Match match)
    {
        if (!match.Success)
        {
            return Utf8ValueMatch.NoMatch;
        }

        var start = boundaryMap.Resolve(match.Index);
        var end = boundaryMap.Resolve(match.Index + match.Length);
        var isByteAligned = start.IsScalarBoundary && end.IsScalarBoundary;
        return new Utf8ValueMatch(
            success: true,
            isByteAligned,
            indexInUtf16: match.Index,
            lengthInUtf16: match.Length,
            indexInBytes: isByteAligned ? start.ByteOffset : 0,
            lengthInBytes: isByteAligned ? end.ByteOffset - start.ByteOffset : 0);
    }

    /// <summary>Returns the number assigned to a group name, or <c>-1</c> when the name is undefined.</summary>
    public int GroupNumberFromName(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        if (_sliceProgram?.CaptureCatalog is { } catalog) return catalog.GroupNumberFromName(name);

        return _program.CaptureCatalog?.GroupNumberFromName(name)
            ?? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GroupNumberFromName(name);
    }

    /// <summary>Returns the name assigned to a group number, or an empty string when the number is undefined.</summary>
    public string GroupNameFromNumber(int i)
    {
        if (_sliceProgram is not null)
        {
            var ordinal = _sliceProgram.CaptureCatalog?.OrdinalFromNumber(i) ?? i;
            var names = _sliceProgram.GroupNames;
            return (uint)ordinal < (uint)names.Length ? names[ordinal] : string.Empty;
        }

        return _program.CaptureCatalog?.GroupNameFromNumber(i)
            ?? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GroupNameFromNumber(i);
    }

    /// <summary>Returns the group names defined by the expression, including group zero.</summary>
    public string[] GetGroupNames()
    {
        if (_sliceProgram is not null)
        {
            return _sliceProgram.GroupNames.AsSpan().ToArray();
        }

        return _program.CaptureCatalog?.GroupNames.AsSpan().ToArray() ?? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GetGroupNames();
    }

    /// <summary>Returns the group numbers defined by the expression, including group zero.</summary>
    public int[] GetGroupNumbers()
    {
        if (_sliceProgram is not null)
        {
            return _sliceProgram.GroupNumbers.AsSpan().ToArray();
        }

        return _program.CaptureCatalog?.GroupNumbers.AsSpan().ToArray() ?? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GetGroupNumbers();
    }

    /// <summary>Determines whether the well-formed UTF-8 input contains a match.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input)
    {
        if (_prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiSignedDecimalWhole)
        {
            if (Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input, out _))
            {
                return true;
            }

            if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0)
            {
                return false;
            }
        }

        if (_prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AsciiBoundedDateToken &&
            input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0)
        {
            return Utf8AsciiBoundedDateTokenExecutor.TryFindAsciiBoundedDateToken(
                input,
                0,
                _fallbackDirectFamily,
                out _,
                out _);
        }

        if (_prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiOptionalFieldWhole)
        {
            var optionalFieldMatched = Utf8AsciiAnchoredOptionalFieldExecutor.TryMatchWhole(
                input,
                _anchoredOptionalFieldPlan,
                _allowsTrailingNewlineBeforeEnd,
                out _,
                out var optionalFieldNeedsValidation);
            if (!optionalFieldNeedsValidation)
            {
                return optionalFieldMatched;
            }
        }

        try
        {
            return IsMatchCore(input, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    // Preserve qualified native lanes; the shared owned VM traverses the original
    // UTF-8 subject. Coordinate conversion belongs to result interoperability.
    private Utf8SliceProgram? GetOwnedSliceProgram(ReadOnlySpan<byte> input, CancellationToken token)
    {
        // Native loops do not yet carry this token through every checkpoint.
        // Keep their no-token selection; live tokens use the owned scalar VM.
        if (token.CanBeCanceled && _ownedSliceProgram is not null) return _ownedSliceProgram;
        return GetOwnedSliceProgram(input);
    }

    private Utf8SliceProgram? GetOwnedSliceProgram(ReadOnlySpan<byte> input)
    {
        if (_sliceProgram is not null) return _sliceProgram;
        if (_ownedSliceProgram is not null &&
            (_ownedSliceProgram.CanMatchEmpty && _preparedRegex.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral ||
             _ownedSliceProgram.UsesInterpretedLazyLoopSemantics ||
             _ownedSliceProgram.HasSharedCaptureDeclarations ||
             _ownedSliceProgram.HasExplicitCaptureNumbers ||
             _ownedSliceProgram.HasBalancing ||
             _ownedSliceProgram.HasContiguousAnchor ||
             // A shorter structural prefix can fail its suffix where a longer
             // branch at the same start succeeds. The owned VM retries it.
             _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
                _preparedRegex.StructuralIdentifierFamilyPlan.HasOverlappingPrefixes ||
             // Absolute anchored simple plans without a dedicated native matcher
             // otherwise force BCL verification, even on ASCII misses. Serve the
             // cached owned program; retain qualified direct/native and line paths.
             _compiledEngine.Kind == Utf8CompiledEngineKind.SimplePatternInterpreter &&
                !_hasDirectAnchoredHeadTailWithoutValidation &&
                !_hasDirectFallbackAsciiFamilyWithoutValidation &&
                !_hasDirectFallbackTokenFamilyWithoutValidation &&
                _caretAnchoredGroupedLiteralUtf8 is null &&
                ShouldUseFallbackForAnchoredSimplePattern() && !CanServeLineAnchoredOwned(input) ||
             _ownedSliceProgram.IgnoreCase &&
                (_preparedRegex.ExecutionKind is NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals ||
                 ShouldUseKelvinSignFallback(input)) ||
             _hasCaptures && _ownedSliceProgram.HasUnicodePatternAtoms ||
             // Exact byte literals already preserve scalar endpoints. Native
             // boundary/class predicates need scalar qualification before they
             // can serve mixed subjects without the shared owned VM.
             !Utf8InputAnalyzer.IsAscii(input) &&
                (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or
                    NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals) ||
                 _preparedRegex.SearchPlan.HasBoundaryRequirements)))
            return _ownedSliceProgram;
        return null;
    }

    private bool OwnedSliceIsMatch(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
        => OwnedSliceIsMatchFromByte(input, program, program.RightToLeft ? input.Length : 0, token);

    private Utf8ValueMatch OwnedSliceMatch(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
        => OwnedSliceMatchFromByte(input, program, program.RightToLeft ? input.Length : 0, token);

    private Utf8MatchContext OwnedSliceMatchDetailed(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
        => OwnedSliceMatchDetailedFromByte(input, program, program.RightToLeft ? input.Length : 0, token);

    private Utf8MatchContext OwnedSliceMatchDetailedFromByte(ReadOnlySpan<byte> input,
        Utf8SliceProgram program, int fromByte, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        var validation = Utf8Validation.Validate(input, ref invocation);
        invocation.AnchorOriginByte = fromByte;
        var slots = new Utf8SliceSlots([], []);
        var outcome = default(Utf8SliceSearchOutcome);
        var projection = default(Utf8SliceCaptureProjection);
        ulong mapReservation = 0;
        try
        {
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            outcome = Utf8SliceRunner.TryFindFirst(program, input, fromByte, slots.Starts, slots.Ends, true, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
            invocation.Budget.ThrowIfCancellationRequested();
            if (!outcome.Found)
                return Utf8MatchContext.Unsuccessful(input, null, Utf8BoundaryMap.Create([]), _groupNames);
            // The current context stores derived UTF-16 capture coordinates.
            // Its admitted map is result interoperability, never discovery state.
            var map = Utf8BoundaryMap.Create(input, validation, ref invocation, out mapReservation);
            projection = Utf8SliceCaptureProjection.Create(outcome, map, ref invocation);
            invocation.Budget.ThrowIfCancellationRequested();
            return projection.CreateContext(input, null, map, program);
        }
        finally
        {
            // Result arrays/maps transfer to the caller. Common retained-result
            // policy is still open; release this invocation's working reservation.
            projection.Release(ref invocation);
            invocation.Ledger.Release(mapReservation);
            Utf8SliceRunner.ReleaseHistory(outcome.CaptureStarts, outcome.CaptureLengths, ref invocation);
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    private int OwnedSliceCount(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        return OwnedSliceCountFromByte(input, program, program.RightToLeft ? input.Length : 0, token);
    }

    private bool IsMatchCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (TryStartNativeStructuralInvocation(input, cancellationToken, false, out var structuralInvocation, out var structuralValidation))
        {
            // Mixed subjects and overlapping prefixes use shared scalar
            // verification under the same invocation.
            if (!structuralValidation.IsAscii || _preparedRegex.StructuralIdentifierFamilyPlan.HasOverlappingPrefixes ||
                _preparedRegex.SearchPlan.HasAlternateLiteralPrefixOverlap ||
                _preparedRegex.SearchPlan.HasAlternateLiteralProperStartOverlap)
                return OwnedSliceIsMatchFromByte(input, _ownedSliceProgram ?? throw new InvalidOperationException("A structural scalar program is required."),
                    0, ref structuralInvocation);
            var state = new Utf8StructuralSearchState(default, default);
            var breach = _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow
                ? AsciiOrderedLiteralWindowExecutor.TryFindNext(input, _preparedRegex.StructuralLinearProgram.OrderedLiteralWindowPlan,
                    _preparedRegex.SearchPlan, 0, ref structuralInvocation, out _, out var found)
                : Utf8AsciiStructuralIdentifierFamilyExecutor.TryFindNext(_preparedRegex.StructuralSearchPlan,
                    _preparedRegex.StructuralVerifier.AsciiProgram, input, 0, ref state, ref structuralInvocation, out _, out found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native structural execution exceeded {breach}.");
            return found;
        }
        if (TryStartNativeLiteralInvocation(input, cancellationToken, out var invocation))
        {
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan, input, 0, input.Length,
                UsesRightToLeft(), ref invocation, out _, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return found;
        }
        if (GetOwnedSliceProgram(input, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceIsMatch(input, ownedProgram, cancellationToken);
        }

        if (TryIsMatchPrioritizedBooleanFamilyWithoutValidation(input, out var prioritizedIsMatch))
        {
            return prioritizedIsMatch;
        }

        if (ShouldUseKelvinSignFallback(input))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
                Utf8Validation.DecodeStrict(input));
        }

        if (ShouldUseRightToLeftScalarFallback())
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
                Utf8Validation.DecodeStrict(input));
        }

        if (TryIsMatchDirectWithoutValidation(input, out var directIsMatch))
        {
            return directIsMatch;
        }

        if (TryMatchDirectWithoutValidation(input, out var directMatch))
        {
            return directMatch.Success;
        }

        // R05: caret-anchored grouped literals decide every group-agnostic lane
        // by byte-prefix compare: the absolute start anchor pins the only candidate
        // at offset 0, so a miss there rules out every substring match and both
        // directions answer with no BCL verification (group shaping reuses the span
        // table). Kelvin stays fenced like the direct arms above.
        if (_caretAnchoredGroupedLiteralUtf8 is { Length: > 0 } caretIsMatchPrefix &&
            !ShouldUseKelvinSignFallback(input))
        {
            return input.Length >= caretIsMatchPrefix.Length &&
                input.Slice(0, caretIsMatchPrefix.Length).SequenceEqual(caretIsMatchPrefix);
        }

        if (ShouldDecodeWholeSubjectForFallbackValueOperation())
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
                Utf8Validation.DecodeStrict(input));
        }

        if (CanUseWellFormedOnlyValidation())
        {
            if (!TryUseAsciiInputValidationShortcut(input))
            {
                Utf8Validation.ThrowIfInvalidOnly(input);
            }

            if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
            {
                return false;
            }

            var fastBudget = CreateExecutionBudget();
            return IsMatchViaCompiledEngine(input, default, fastBudget);
        }

        var validation = Utf8Validation.Validate(input);
        if (ShouldFallbackForTrailingNewlineAnchoredValidator(input, validation))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
        }

        if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
        {
            return false;
        }

        var budget = CreateExecutionBudget();

        return IsMatchViaCompiledEngine(input, validation, budget);
    }

    /// <summary>Determines whether the well-formed UTF-8 input contains a match, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public bool IsMatch(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return IsMatchCore(input, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Determines whether a match starts at or after the specified UTF-16 code-unit offset.</summary>
    public bool IsMatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        try
        {
            return IsMatchFromUtf16OffsetCore(input, utf16Offset, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Determines whether a match starts at or after the specified UTF-16 code-unit offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public bool IsMatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return IsMatchFromUtf16OffsetCore(input, utf16Offset, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private bool IsMatchFromUtf16OffsetCore(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var start = subject.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            return start.Value == 0 &&
                Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out _);
        }

        // Anchored suffixes match at most once at a fixed end position: a start past
        // it misses definitively, otherwise the direct hit itself decides.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixDirect) &&
                suffixDirect.Success &&
                start.Value <= suffixDirect.IndexInUtf16;
        }

        // Admitted UTF-16 starts use the cached owned scalar program for ASCII
        // and Unicode atoms alike. Ordinary byte-oriented discovery retains
        // its native choices through GetOwnedSliceProgram.
        if (_ownedSliceProgram is { } ownedProgram)
        {
            return OwnedSliceIsMatchFromByte(input, ownedProgram, subject.BoundaryMap.Resolve(start.Value).ByteOffset, cancellationToken);
        }


                // R05: line-anchored ASCII discovery from the start unit runs owned through the
        // shared interpreter; misses are definitive without BCL verification.
        if (CanServeLineAnchoredOwned(input) && Utf8InputAnalyzer.IsAscii(input))
        {
            var offsetBudget = CreateExecutionBudget();
            return FindNextSimplePatternViaInterpreter(input, start.Value, offsetBudget, out _) >= 0;
        }

        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
            subject.GetDecodedString().AsSpan(),
            start.Value);
    }

    private bool OwnedSliceIsMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        _ = Utf8Validation.Validate(input, ref invocation);
        return OwnedSliceIsMatchFromByte(input, program, startByte, ref invocation);
    }

    private bool OwnedSliceIsMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, ref Utf8SliceInvocation invocation)
    {
        invocation.AnchorOriginByte = startByte;
        var slots = new Utf8SliceSlots([], []);
        try
        {
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, startByte, slots.Starts, slots.Ends, false, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
            invocation.Budget.ThrowIfCancellationRequested();
            return outcome.Found;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    /// <summary>Counts non-overlapping matches in the well-formed UTF-8 input.</summary>
    public int Count(ReadOnlySpan<byte> input)
    {
        try
        {
            return CountCore(input, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Counts non-overlapping matches in the well-formed UTF-8 input, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public int Count(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return CountCore(input, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private int CountCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (TryStartNativeStructuralInvocation(input, cancellationToken, false, out var structuralInvocation, out var structuralValidation))
        {
            if (!structuralValidation.IsAscii || _preparedRegex.StructuralIdentifierFamilyPlan.HasOverlappingPrefixes ||
                _preparedRegex.SearchPlan.HasAlternateLiteralPrefixOverlap ||
                _preparedRegex.SearchPlan.HasAlternateLiteralProperStartOverlap)
                return OwnedSliceCountFromByte(input, _ownedSliceProgram ?? throw new InvalidOperationException("A structural scalar program is required."),
                    0, ref structuralInvocation);
            var breach = _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow
                ? AsciiOrderedLiteralWindowExecutor.TryCount(input, _preparedRegex.StructuralLinearProgram.OrderedLiteralWindowPlan,
                    _preparedRegex.SearchPlan, ref structuralInvocation, out var count)
                : Utf8AsciiStructuralIdentifierFamilyExecutor.TryCount(_preparedRegex.StructuralSearchPlan,
                    _preparedRegex.StructuralVerifier.AsciiProgram, input, ref structuralInvocation, out count);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native structural execution exceeded {breach}.");
            return count;
        }
        if (TryStartNativeLiteralInvocation(input, cancellationToken, out var invocation))
        {
            var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(_preparedRegex.SearchPlan,
                input, UsesRightToLeft(), ref invocation, out var count);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return count;
        }
        // Retain the qualified Unicode literal Count kernel. Other operations
        // can now use the newly admitted owned program for the same pattern.
        if (!cancellationToken.CanBeCanceled && MatchTimeout == Regex.InfiniteMatchTimeout &&
            _invariantCyrillicLiteralCountStrategy is { } invariantCyrillicLiteralCountStrategy)
        {
            return invariantCyrillicLiteralCountStrategy.Count(input);
        }

        // This qualified kernel consumes scalar whitespace around an ASCII-only
        // repeated run and literal. Keep its unlimited, no-token native lane;
        // metered and cancellation-aware calls retain the shared scalar VM.
        var boundedSuffixLiteralPlan = _preparedRegex.SimplePatternPlan.BoundedSuffixLiteralPlan;
        if (!cancellationToken.CanBeCanceled && MatchTimeout == Regex.InfiniteMatchTimeout &&
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null &&
            boundedSuffixLiteralPlan.SupportsValidatedUtf8Input && !Utf8InputAnalyzer.IsAscii(input))
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return Utf8AsciiBoundedSuffixLiteralExecutor.Count(input, boundedSuffixLiteralPlan, CreateExecutionBudget());
        }

        if (GetOwnedSliceProgram(input, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceCount(input, ownedProgram, cancellationToken);
        }

        if (_fusedAsciiLiteralCountAnchorOffset >= 0 &&
            _preparedRegex.LiteralUtf8 is { } fusedAsciiLiteral &&
            Utf8AsciiExactLiteralCountExecutor.TryCountAndValidateAscii(
                input,
                fusedAsciiLiteral,
                _fusedAsciiLiteralCountAnchorOffset,
                out var fusedAsciiLiteralCount))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.CompiledFusedAsciiLiteralCount);
            return fusedAsciiLiteralCount;
        }

        if (_fusedAsciiBoundaryLiteralFamilyCounter is { } fusedAsciiBoundaryLiteralFamilyCounter &&
            fusedAsciiBoundaryLiteralFamilyCounter.TryCountAndValidateAscii(input, out var fusedBoundaryLiteralFamilyCount))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.CompiledFusedAsciiLiteralFamilyCount);
            return fusedBoundaryLiteralFamilyCount;
        }

        if (_packedNibbleLiteralFamilyCountStrategy is { } packedNibbleLiteralFamilyCountStrategy)
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return packedNibbleLiteralFamilyCountStrategy.Count(input);
        }

        if (_prioritizeOrderedLiteralWindowCount)
        {
            var orderedWindowPlan = _program.PreparedRegex.StructuralLinearProgram.OrderedLiteralWindowPlan;
            if (orderedWindowPlan.TrailingLiteralUtf8.Length >= 2 &&
                AsciiOrderedLiteralWindowExecutor.CanUseSeparatorOnlySingleLiteralFastPath(orderedWindowPlan))
            {
                var orderedWindowBudget = CreateExecutionBudget();
                if (AsciiOrderedLiteralWindowExecutor.TryCountSeparatorOnlySingleLiteralAscii(
                        input,
                        orderedWindowPlan,
                        orderedWindowBudget,
                        out var orderedWindowCount))
                {
                    Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeOrderedLiteralWindow);
                    return orderedWindowCount;
                }

                if (_ownedSliceProgram is { } windowProgram)
                    return OwnedSliceCountFromByte(input, windowProgram, 0, CancellationToken.None);
                // Unserved scalar representations remain transitional debt.
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                    Utf8Validation.DecodeStrict(input));
            }
        }

        if (_prioritizePlainAsciiLiteralCount ||
            _prioritizeBoundaryLiteralFamilyCount ||
            _prioritizeOrderedLiteralWindowCount)
        {
            if (!TryUseAsciiInputValidationShortcut(input))
            {
                Utf8Validation.ThrowIfInvalidOnly(input);
            }

            return CountViaCompiledEngine(input, default, CreateExecutionBudget());
        }

        if (CanValidateKelvinSignDuringNativeCount())
        {
            var kelvinValidation = Utf8InputAnalyzer.ValidateOnly(input, out var containsKelvinSign);
            if (containsKelvinSign && !ScalarVerifierServesInput(input))
            {
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                    Utf8Validation.DecodeStrict(input));
            }

            if (!ShouldSkipRequiredPrefilterForCount() && RejectsByRequiredPrefilter(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
                return 0;
            }

            return CountViaCompiledEngine(input, kelvinValidation, CreateExecutionBudget());
        }

        if (ShouldUseKelvinSignFallback(input))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                Utf8Validation.DecodeStrict(input));
        }

        if (ShouldUseRightToLeftScalarFallback())
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                Utf8Validation.DecodeStrict(input));
        }

        // R05: caret-anchored grouped literals count owned by prefix compare (at
        // most one match at offset 0); folding and RTL use the routes above.
        if (_caretAnchoredGroupedLiteralUtf8 is { Length: > 0 } caretCountPrefix)
        {
            return input.Length >= caretCountPrefix.Length &&
                input.Slice(0, caretCountPrefix.Length).SequenceEqual(caretCountPrefix) ? 1 : 0;
        }

        if (ShouldDecodeWholeSubjectForFallbackValueOperation())
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                Utf8Validation.DecodeStrict(input));
        }

        if (_fallbackDirectFamily.Kind is
            Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount or
            Utf8FallbackDirectFamilyKind.Utf8UriToken)
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return CountViaCompiledEngine(input, default, CreateExecutionBudget());
        }

        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount ||
            Utf8FallbackDirectFamilyCategories.IsTokenCountFamily(_fallbackDirectFamily.Kind))
        {
            if (!Utf8InputAnalyzer.IsAscii(input))
            {
                return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                    Utf8Validation.DecodeStrict(input));
            }

            var asciiValidation = new Utf8ValidationResult(
                input.Length,
                input.Length,
                isAscii: true,
                containsSupplementaryScalars: false);
            return CountViaCompiledEngine(input, asciiValidation, CreateExecutionBudget());
        }

        if (CanUseFusedCompiledAsciiLiteralFamilyCount())
        {
            if (!TryUseAsciiInputValidationShortcut(input))
            {
                Utf8Validation.ThrowIfInvalidOnly(input);
            }

            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.CompiledFusedAsciiLiteralFamilyCount);
            var fastBudget = CreateExecutionBudget();
            return CountViaCompiledEngine(input, default, fastBudget);
        }

        if (CanUseFusedCompiledUtf8LiteralCount() || CanUseFusedCompiledUtf8LiteralFamilyCount())
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(
                CanUseFusedCompiledUtf8LiteralCount()
                    ? Utf8ExecutionRoute.CompiledFusedUtf8LiteralCount
                    : Utf8ExecutionRoute.CompiledFusedUtf8LiteralFamilyCount);
            var fastBudget = CreateExecutionBudget();
            return CountViaCompiledEngine(input, default, fastBudget);
        }

        if (_compiledEngineRuntime.SupportsWellFormedOnlyCount)
        {
            Utf8ValidationResult countValidation;
            if (_compiledEngineRuntime.PreferValidateOnlyCount)
            {
                countValidation = Utf8InputAnalyzer.ValidateOnly(input);
            }
            else if (_compiledEngineRuntime.SupportsThrowIfInvalidOnlyCount)
            {
                if (!TryUseAsciiInputValidationShortcut(input))
                {
                    Utf8Validation.ThrowIfInvalidOnly(input);
                }

                countValidation = default;
            }
            else
            {
                countValidation = GetWellFormedOnlyValidation(input);
            }

            if (!ShouldSkipRequiredPrefilterForCount() && RejectsByRequiredPrefilter(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
                return 0;
            }

            var fastBudget = CreateExecutionBudget();
            return CountViaCompiledEngine(input, countValidation, fastBudget);
        }

        if (CanUseWellFormedOnlyValidation())
        {
            if (!TryUseAsciiInputValidationShortcut(input))
            {
                Utf8Validation.ThrowIfInvalidOnly(input);
            }

            if (!ShouldSkipRequiredPrefilterForCount() && RejectsByRequiredPrefilter(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
                return 0;
            }

            var fastBudget = CreateExecutionBudget();
            if (fastBudget.IsInfinite &&
                _directStructuralFamilyKernelMatcher is not null)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.NativeStructuralFamilyEmitSharedPrefixSuffix);
                return _directStructuralFamilyKernelMatcher.Count(input);
            }

            return CountViaCompiledEngine(input, default, fastBudget);
        }

        var validation = Utf8Validation.Validate(input);
        if (ShouldFallbackForTrailingNewlineAnchoredValidator(input, validation))
        {
            if (_ownedSliceProgram is { } program)
                return OwnedSliceCountFromByte(input, program, 0, cancellationToken);
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
        }

        if (RejectsByRequiredPrefilter(input))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
            return 0;
        }

        var budget = CreateExecutionBudget();

        return CountViaCompiledEngine(input, validation, budget);
    }

    /// <summary>Counts non-overlapping matches starting at the specified UTF-16 code-unit offset.</summary>
    public int CountFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        try
        {
            return CountFromUtf16OffsetCore(input, utf16Offset, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Counts non-overlapping matches starting at the specified UTF-16 code-unit offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public int CountFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return CountFromUtf16OffsetCore(input, utf16Offset, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private int CountFromUtf16OffsetCore(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var start = subject.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        // At most one anchored suffix match exists past the start: count it directly.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixCount) &&
                suffixCount.Success &&
                start.Value <= suffixCount.IndexInUtf16
                ? 1
                : 0;
        }

        if (_ownedSliceProgram is { } ownedProgram)
        {
            return OwnedSliceCountFromByte(input, ownedProgram, subject.BoundaryMap.Resolve(start.Value).ByteOffset, cancellationToken);
        }


                // R05: line-anchored ASCII counting from the start unit runs the shared
        // interpreter loop; anchors still see the whole input.
        if (CanServeLineAnchoredOwned(input) && Utf8InputAnalyzer.IsAscii(input))
        {
            var offsetBudget = CreateExecutionBudget();
            return Utf8ExecutionInterpreter.CountSimplePatternFrom(
                input,
                _preparedRegex.ExecutionProgram,
                _preparedRegex.SearchPlan,
                _preparedRegex.SimplePatternPlan,
                start.Value,
                offsetBudget);
        }

        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
            subject.GetDecodedString().AsSpan(),
            start.Value);
    }

    /// <summary>Owned scalar count from an admitted byte boundary.</summary>
    private int OwnedSliceCountFromByte(
        ReadOnlySpan<byte> input,
        Utf8SliceProgram program,
        int startByte,
        CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        _ = Utf8Validation.Validate(input, ref invocation);
        return OwnedSliceCountFromByte(input, program, startByte, ref invocation);
    }

    private int OwnedSliceCountFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, ref Utf8SliceInvocation invocation)
    {
        var slots = new Utf8SliceSlots([], []);
        try
        {
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var outcome = Utf8SliceRunner.CountFromBytes(program, input, startByte, slots.Starts, slots.Ends, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
            }

            return outcome.Count;
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    /// <summary>Finds the first match and returns its UTF-16 and, when scalar-aligned, UTF-8 byte coordinates.</summary>
    public Utf8ValueMatch Match(ReadOnlySpan<byte> input)
    {
        try
        {
            return MatchCore(input, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Finds the first match and returns its UTF-16 and, when scalar-aligned, UTF-8 byte coordinates, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueMatch Match(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return MatchCore(input, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private Utf8ValueMatch MatchCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (TryStartNativeStructuralInvocation(input, cancellationToken, false, out var structuralInvocation, out var structuralValidation))
        {
            if (!structuralValidation.IsAscii || _preparedRegex.StructuralIdentifierFamilyPlan.HasOverlappingPrefixes ||
                _preparedRegex.SearchPlan.HasAlternateLiteralPrefixOverlap ||
                _preparedRegex.SearchPlan.HasAlternateLiteralProperStartOverlap)
                return OwnedSliceMatchFromByte(input, _ownedSliceProgram ?? throw new InvalidOperationException("A structural scalar program is required."),
                    0, ref structuralInvocation);
            var state = new Utf8StructuralSearchState(default, default);
            var breach = _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow
                ? AsciiOrderedLiteralWindowExecutor.TryFindNext(input, _preparedRegex.StructuralLinearProgram.OrderedLiteralWindowPlan,
                    _preparedRegex.SearchPlan, 0, ref structuralInvocation, out var match, out var found)
                : Utf8AsciiStructuralIdentifierFamilyExecutor.TryFindNext(_preparedRegex.StructuralSearchPlan,
                    _preparedRegex.StructuralVerifier.AsciiProgram, input, 0, ref state, ref structuralInvocation, out match, out found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native structural execution exceeded {breach}.");
            return found ? Utf8ProjectionExecutor.ProjectMatch(new Utf8ProjectionPlan(Utf8ProjectionKind.ByteOnly), input,
                0, 0, match.Index, match.Length, match.Length, ref structuralInvocation, out _, out _) : Utf8ValueMatch.NoMatch;
        }
        if (TryStartNativeLiteralInvocation(input, cancellationToken, out var invocation))
        {
            var plan = _preparedRegex.SearchPlan;
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(plan, input, 0, input.Length,
                UsesRightToLeft(), ref invocation, out var selected, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return found ? Utf8ProjectionExecutor.ProjectLiteralFamilyMatch(plan.ProjectionPlan, input,
                plan.AlternateLiteralUtf16Lengths, 0, 0, selected, ref invocation, out _, out _) : Utf8ValueMatch.NoMatch;
        }
        if (GetOwnedSliceProgram(input, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceMatch(input, ownedProgram, cancellationToken);
        }

        if (ShouldUseKelvinSignFallback(input))
        {
            return MatchScalarProgram(input);
        }

        if (ShouldUseRightToLeftScalarFallback())
        {
            return MatchScalarProgram(input);
        }

        if (TryMatchDirectWithoutValidation(input, out var directMatch))
        {
            return directMatch;
        }

        // R05: caret-anchored grouped literals match owned by prefix compare (the
        // absolute start anchor pins the only candidate at offset 0; ASCII bytes
        // and units coincide, so the value hit needs no BCL verification and
        // Detailed shapes from it through the span table). Kelvin stays fenced.
        if (_caretAnchoredGroupedLiteralUtf8 is { Length: > 0 } caretMatchPrefix &&
            !ShouldUseKelvinSignFallback(input))
        {
            return input.Length >= caretMatchPrefix.Length &&
                input.Slice(0, caretMatchPrefix.Length).SequenceEqual(caretMatchPrefix)
                ? new Utf8ValueMatch(true, true, 0, caretMatchPrefix.Length, 0, caretMatchPrefix.Length)
                : Utf8ValueMatch.NoMatch;
        }

        var canUseAsciiWellFormedOnlyMatch =
            _compiledEngineRuntime.SupportsAsciiWellFormedOnlyMatch &&
            TryUseAsciiInputValidationShortcut(input);

        // An ASCII whole-match is incomplete for non-ASCII input unless the
        // simple-pattern plan is UTF-8 byte-safe (PLAN.md C01): a miss must not
        // shortcut Unicode verification. Those inputs fall through to full
        // validation and the compiled engine, which selects the semantic fallback.
        // This stays inline: single-caller private helpers count as source debt.
        var requiresUnicodeVerification =
            _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            !_preparedRegex.SimplePatternPlan.IsUtf8ByteSafe &&
            !Utf8InputAnalyzer.IsAscii(input);

        var canUseWellFormedOnlyMatch =
            _compiledEngineRuntime.SupportsWellFormedOnlyMatch &&
            !requiresUnicodeVerification;

        if (canUseAsciiWellFormedOnlyMatch || canUseWellFormedOnlyMatch)
        {
            if (!canUseAsciiWellFormedOnlyMatch)
            {
                _ = GetWellFormedOnlyValidation(input);
            }

            if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
            {
                return Utf8ValueMatch.NoMatch;
            }

            var matched = canUseAsciiWellFormedOnlyMatch
                ? _compiledEngineRuntime.TryMatchAsciiWellFormedOnly(input, out var wellFormedMatch)
                : _compiledEngineRuntime.TryMatchWellFormedOnly(input, out wellFormedMatch);

            if (matched)
            {
                return wellFormedMatch;
            }

            // A whole-match miss is definitive only when a miss at offset 0 rules
            // out every substring match. Line-anchored (`(?m:^)`, `(?m:$)`) plans can
            // still match inside the input, so they fall through to the engine below.
            if (_compiledEngineRuntime.WellFormedOnlyMatchMissIsDefinitive &&
                !(_preparedRegex.SimplePatternPlan.IsStartAnchored &&
                    !_preparedRegex.SimplePatternPlan.IsAbsoluteStartAnchored) &&
                !(_preparedRegex.SimplePatternPlan.IsEndAnchored &&
                    !_preparedRegex.SimplePatternPlan.IsAbsoluteEndAnchored))
            {
                return Utf8ValueMatch.NoMatch;
            }
        }

        var validation = Utf8Validation.Validate(input);
        if (ShouldFallbackForTrailingNewlineAnchoredValidator(input, validation))
        {
            return MatchFallback(input);
        }

        if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
        {
            return Utf8ValueMatch.NoMatch;
        }

        var budget = CreateExecutionBudget();
        return MatchViaCompiledEngine(input, validation, budget);
    }

    /// <summary>Finds the first match at or after the specified UTF-16 code-unit offset.</summary>
    public Utf8ValueMatch MatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        try
        {
            return MatchFromUtf16OffsetCore(input, utf16Offset, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Finds the first match at or after the specified UTF-16 code-unit offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueMatch MatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return MatchFromUtf16OffsetCore(input, utf16Offset, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }


    private Utf8ValueMatch MatchFromUtf16OffsetCore(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var start = subject.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (start.Value == 0 &&
                Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredOffsetMatchLength))
            {
                return new Utf8ValueMatch(true, true, 0, anchoredOffsetMatchLength, 0, anchoredOffsetMatchLength);
            }

            return Utf8ValueMatch.NoMatch;
        }

        // Folded anchored prefixes serve the same single anchored hit with per-byte
        // invariant folding; nonzero starts and Kelvin/Turkic divergence fall through.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            if (!ShouldUseKelvinSignFallback(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
                if (start.Value == 0 &&
                    Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var foldedOffsetMatchLength))
                {
                    return new Utf8ValueMatch(true, true, 0, foldedOffsetMatchLength, 0, foldedOffsetMatchLength);
                }

                return Utf8ValueMatch.NoMatch;
            }
        }

        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixDirect) &&
                suffixDirect.Success &&
                start.Value <= suffixDirect.IndexInUtf16)
            {
                return suffixDirect;
            }

            return Utf8ValueMatch.NoMatch;
        }

        // Folded anchored suffixes serve the same single tail hit with per-byte invariant
        // folding through the suffix kernel; non-native folds use scalar execution.
        if ((_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            if (!ShouldUseKelvinSignFallback(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var foldedSuffixDirect) &&
                    foldedSuffixDirect.Success &&
                    start.Value <= foldedSuffixDirect.IndexInUtf16)
                {
                    return foldedSuffixDirect;
                }

                return Utf8ValueMatch.NoMatch;
            }
        }

        if (_ownedSliceProgram is { } ownedProgram)
        {
            return OwnedSliceMatchFromByte(input, ownedProgram, subject.BoundaryMap.Resolve(start.Value).ByteOffset, cancellationToken);
        }


                // R05: line-anchored ASCII first-match from the start unit projects owned through
        // the shared interpreter with whole-input anchor checks.
        if (CanServeLineAnchoredOwned(input) && Utf8InputAnalyzer.IsAscii(input))
        {
            var offsetBudget = CreateExecutionBudget();
            var offsetFound = FindNextSimplePatternViaInterpreter(input, start.Value, offsetBudget, out var offsetLength);
            if (offsetFound < 0)
            {
                return Utf8ValueMatch.NoMatch;
            }

            return new Utf8ValueMatch(true, true, offsetFound, offsetLength, offsetFound, offsetLength);
        }

        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(
            subject.GetDecodedString(),
            start.Value);
        return CreateValueMatch(input, subject.BoundaryMap, match);
    }

    private Utf8ValueMatch OwnedSliceMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
        _ = Utf8Validation.Validate(input, ref invocation);
        return OwnedSliceMatchFromByte(input, program, startByte, ref invocation);
    }

    private Utf8ValueMatch OwnedSliceMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, ref Utf8SliceInvocation invocation)
    {
        invocation.AnchorOriginByte = startByte;
        var slots = new Utf8SliceSlots([], []);
        try
        {
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, startByte, slots.Starts, slots.Ends, false, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Owned slice execution breached an unlimited invocation budget.");
            invocation.Budget.ThrowIfCancellationRequested();
            if (!outcome.Found) return Utf8ValueMatch.NoMatch;
            var length = outcome.EndByte - outcome.StartByte;
            var utf16Length = Utf8Validation.Validate(input.Slice(outcome.StartByte, length), ref invocation).Utf16Length;
            return Utf8ProjectionExecutor.ProjectMatch(new Utf8ProjectionPlan(Utf8ProjectionKind.Utf16Incremental),
                input, 0, 0, outcome.StartByte, length, utf16Length, ref invocation, out _, out _);
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    /// <summary>Finds the first match and exposes its groups and captures over the original UTF-8 input.</summary>
    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input)
    {
        try
        {
            return MatchDetailedCore(input, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private Utf8MatchContext MatchDetailedCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Native simple discovery may ignore captures; detailed projection needs
        // the scalar VM instead of the removed fixed unit-span branch maps.
        var detailedProgram = GetOwnedSliceProgram(input, cancellationToken) ??
            (_hasCaptures && _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern ? _ownedSliceProgram : null);
        if (detailedProgram is { } ownedProgram)
        {
            return OwnedSliceMatchDetailed(input, ownedProgram, cancellationToken);
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        var decoded = analysis.GetDecodedString();
        // Anchored ASCII literal prefixes shape directly from the prefix compare:
        // byte-exact on every input, so detailed matches never construct the BCL engine.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            _groupNumbers.Length <= 1)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredDetailedLength))
            {
                return new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, 0, anchoredDetailedLength);
            }

            return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
        }

        // Anchored suffixes shape the same way from the direct hit, which already
        // carries UTF-16 coordinates without BCL construction.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            _groupNumbers.Length <= 1)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixDetailed) &&
                suffixDetailed.Success)
            {
                return new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, suffixDetailed.IndexInUtf16, suffixDetailed.LengthInUtf16);
            }

            return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
        }

        if (TryGetOwnedDetailedGroupCount(out var ownedGroupCount))
        {
            // R03: discovery runs through the shared value path and shaping projects owned
            // spans, so qualifying literal shapes never execute the BCL engine; semantic declines
            // inside MatchCore keep their existing BCL routes.
            Utf8ValueMatch first;
            try
            {
                first = MatchCore(input, cancellationToken);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!first.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            return ownedGroupCount > 1
                ? new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, first.IndexInUtf16, first.LengthInUtf16, ownedGroupCount)
                : new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, first.IndexInUtf16, first.LengthInUtf16);
        }

        if (_groupedLiteralSpanTable is not null &&
            !ShouldUseKelvinSignFallback(input))
        {
            // R05: grouped single-literal shapes discover through the shared value path
            // and project every group from fixed literal-relative spans, with present or
            // absent selection for optional shapes. A hit-length disagreement declines
            // to the routes below instead of shaping blindly.
            Utf8ValueMatch groupedFirst;
            try
            {
                groupedFirst = MatchCore(input, cancellationToken);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!groupedFirst.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            if (TryShapeGroupedLiteralDetailedMatch(input, decoded, analysis.BoundaryMap, groupedFirst, out var groupedDetailed))
            {
                return groupedDetailed;
            }
        }

        if (_alternationBranchSpanMap is not null &&
            !ShouldUseKelvinSignFallback(input))
        {
            // R05: divergent single-alternation shapes discover through the shared
            // value path and project the winning branch spans identified by ordinal
            // text comparison. Ambiguity or disagreement declines to the routes below.
            Utf8ValueMatch alternationFirst;
            try
            {
                alternationFirst = MatchCore(input, cancellationToken);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!alternationFirst.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            if (TryShapeAlternationBranchDetailedMatch(input, decoded, analysis.BoundaryMap, alternationFirst, out var alternationDetailed))
            {
                return alternationDetailed;
            }
        }


        if (TryGetBranchedLiteralFamilyMap(out var branchMap) &&
            TryMatchDetailedBranchedLiteralFamily(input, decoded, analysis.BoundaryMap, branchMap, 0, out var branched))
        {
            return branched;
        }

        // R05: groupless shapes shape Detailed directly from the shared discovery
        // hit (group 0 is the whole span). Served shapes return through the arms
        // above, and BCL-backed discovery builds the fallback inside MatchCore either
        // way, so this arm only ever flips lanes whose discovery is already owned.
        if (_groupNumbers.Length <= 1)
        {
            Utf8ValueMatch grouplessFirst;
            try
            {
                grouplessFirst = MatchCore(input, cancellationToken);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!grouplessFirst.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            return new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, grouplessFirst.IndexInUtf16, grouplessFirst.LengthInUtf16);
        }

        // Native discovery may lack a complete capture-history projection.
        // Keep its qualified shapers above; the cached scalar program owns the
        // remaining admitted detailed result instead of constructing BCL.
        if (_ownedSliceProgram is not null && !ShouldUseKelvinSignFallback(input))
            return OwnedSliceMatchDetailed(input, _ownedSliceProgram, cancellationToken);

        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        return new Utf8MatchContext(input, decoded, match, analysis.BoundaryMap, _groupNames);
    }

    /// <summary>Finds the first match and exposes its groups and captures over the original UTF-8 input, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return MatchDetailedCore(input, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Finds the first detailed match at or after the specified UTF-16 code-unit offset.</summary>
    public Utf8MatchContext MatchDetailedFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        try
        {
            return MatchDetailedFromUtf16OffsetCore(input, utf16Offset, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Finds the first detailed match at or after the specified UTF-16 code-unit offset, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8MatchContext MatchDetailedFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return MatchDetailedFromUtf16OffsetCore(input, utf16Offset, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private Utf8MatchContext MatchDetailedFromUtf16OffsetCore(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        var start = analysis.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        var decoded = analysis.GetDecodedString();
        // Anchored prefixes anchor to offset 0: a nonzero start is a definitive miss,
        // while offset 0 reuses the byte-exact prefix compare without BCL construction.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            _groupNumbers.Length <= 1)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (start.Value == 0 &&
                Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredOffsetLength))
            {
                return new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, 0, anchoredOffsetLength);
            }

            return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
        }

        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            _groupNumbers.Length <= 1)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixOffsetDetailed) &&
                suffixOffsetDetailed.Success &&
                start.Value <= suffixOffsetDetailed.IndexInUtf16)
            {
                return new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, suffixOffsetDetailed.IndexInUtf16, suffixOffsetDetailed.LengthInUtf16);
            }

            return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
        }

        if (_ownedSliceProgram is { } ownedProgram)
        {
            return OwnedSliceMatchDetailedFromByte(input, ownedProgram, analysis.BoundaryMap.Resolve(start.Value).ByteOffset, cancellationToken);
        }


        if (TryGetOwnedDetailedGroupCount(out var ownedOffsetGroupCount) &&
            CanServeLineAnchoredOwned(input) && Utf8InputAnalyzer.IsAscii(input))
        {
            // R05: line-anchored ASCII detailed matches from the start unit discover
            // through the shared value path (mirroring the zero-offset arm); shaping
            // projects owned spans without BCL verification.
            Utf8ValueMatch offsetFirst;
            try
            {
                offsetFirst = MatchFromUtf16OffsetCore(input, utf16Offset, cancellationToken);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!offsetFirst.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            return ownedOffsetGroupCount > 1
                ? new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, offsetFirst.IndexInUtf16, offsetFirst.LengthInUtf16, ownedOffsetGroupCount)
                : new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, offsetFirst.IndexInUtf16, offsetFirst.LengthInUtf16);
        }
        if (TryGetOwnedDetailedGroupCount(out var ownedGroupCount))
        {
            // R03: first-of-shared-enumeration keeps origin semantics (absolute anchors,
            // boundaries, and lookarounds divert to full-input verification inside); shaping
            // stays owned for qualifying shapes.
            Utf8ValueMatch first = default;
            var found = false;
            foreach (var candidate in EnumerateMatchesFromUtf16Offset(input, utf16Offset))
            {
                first = candidate;
                found = true;
                break;
            }

            if (!found || !first.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            return ownedGroupCount > 1
                ? new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, first.IndexInUtf16, first.LengthInUtf16, ownedGroupCount)
                : new Utf8MatchContext(input, decoded, analysis.BoundaryMap, _groupNames, first.IndexInUtf16, first.LengthInUtf16);
        }

        if (_groupedLiteralSpanTable is not null &&
            !ShouldUseKelvinSignFallback(input))
        {
            // R05: grouped single-literal shapes keep origin semantics through
            // first-of-shared-enumeration and project from fixed spans (present or
            // absent for optional shapes), mirroring the zero-offset arm above.
            Utf8ValueMatch groupedOffsetFirst = default;
            var groupedOffsetFound = false;
            foreach (var candidate in EnumerateMatchesFromUtf16Offset(input, utf16Offset))
            {
                groupedOffsetFirst = candidate;
                groupedOffsetFound = true;
                break;
            }

            if (!groupedOffsetFound || !groupedOffsetFirst.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            if (TryShapeGroupedLiteralDetailedMatch(input, decoded, analysis.BoundaryMap, groupedOffsetFirst, out var groupedOffsetDetailed))
            {
                return groupedOffsetDetailed;
            }
        }

        if (_alternationBranchSpanMap is not null &&
            !ShouldUseKelvinSignFallback(input))
        {
            // R05: divergent single-alternation shapes keep origin semantics through
            // first-of-shared-enumeration and project the winning branch spans,
            // mirroring the zero-offset arm above.
            Utf8ValueMatch alternationOffsetFirst = default;
            var alternationOffsetFound = false;
            foreach (var candidate in EnumerateMatchesFromUtf16Offset(input, utf16Offset))
            {
                alternationOffsetFirst = candidate;
                alternationOffsetFound = true;
                break;
            }

            if (!alternationOffsetFound || !alternationOffsetFirst.Success)
            {
                return Utf8MatchContext.Unsuccessful(input, decoded, analysis.BoundaryMap, _groupNames);
            }

            if (TryShapeAlternationBranchDetailedMatch(input, decoded, analysis.BoundaryMap, alternationOffsetFirst, out var alternationOffsetDetailed))
            {
                return alternationOffsetDetailed;
            }
        }

        if (TryGetBranchedLiteralFamilyMap(out var branchMap) &&
            TryMatchDetailedBranchedLiteralFamily(input, decoded, analysis.BoundaryMap, branchMap, start.Value, out var branched))
        {
            return branched;
        }

        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded, start.Value);
        return new Utf8MatchContext(input, decoded, match, analysis.BoundaryMap, _groupNames);
    }

    private bool TryGetOwnedDetailedGroupCount(out int groupCount)
    {
        // R03: groupless shapes (literal or simple) and single groups wrapping a whole
        // literal family discover through the shared value path and shape from owned
        // spans. Semantic declines inside the value path keep their existing BCL
        // routes; everything else keeps BCL discovery and shaping until capture
        // tracking migrates.
        groupCount = 1;
        // Anchored literal prefixes shape through the dedicated prefix-family arm above,
        // not through this literal-kernel gate (their ExecutionKind is FallbackRegex).
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals or NativeExecutionKind.AsciiSimplePattern))
        {
            return false;
        }

        if (_groupNumbers.Length <= 1)
        {
            return true;
        }

        if (_groupNumbers.Length == 2 && GetWholeLiteralCaptureNumber() == 1)
        {
            groupCount = 2;
            return true;
        }

        // R05: single captures wrapping the whole tree project group 1 from the
        // whole span regardless of enclosed content (see the span-transparency
        // boundary in the predicate); literal shapes keep their existing arm above.
        if (_groupNumbers.Length == 2 && GetSingleWrapCaptureNumber() == 1)
        {
            groupCount = 2;
            return true;
        }

        return false;
    }

    private bool TryShapeGroupedLiteralDetailedMatch(
        ReadOnlySpan<byte> input,
        string decoded,
        Utf8BoundaryMap boundaryMap,
        Utf8ValueMatch hit,
        out Utf8MatchContext detailed)
    {
        // R05: projects every group from fixed literal-relative spans onto the owned
        // discovery hit. The hit length selects the present or absent projection for
        // optional shapes; any other disagreement or out-of-range capture number
        // declines to the BCL routes instead of shaping blindly.
        detailed = default;
        var table = _groupedLiteralSpanTable;
        if (table is null)
        {
            return false;
        }

        int[] projectionStarts;
        int[] projectionLengths;
        if (!table.HasOptionalLoop)
        {
            if (hit.LengthInUtf16 != table.TotalLengthInUtf16)
            {
                return false;
            }

            projectionStarts = table.StartsInLiteral;
            projectionLengths = table.LengthsInLiteral;
        }
        else if (hit.LengthInUtf16 == table.TotalLengthInUtf16)
        {
            projectionStarts = table.StartsInLiteral;
            projectionLengths = table.LengthsInLiteral;
        }
        else if (hit.LengthInUtf16 == table.TotalLengthWithoutLoopInUtf16)
        {
            projectionStarts = table.AbsentStartsInLiteral;
            projectionLengths = table.AbsentLengthsInLiteral;
        }
        else
        {
            return false;
        }

        var groupCount = _groupNumbers.Length;
        var starts = new int[groupCount];
        var lengths = new int[groupCount];
        Array.Fill(starts, -1);
        starts[0] = hit.IndexInUtf16;
        lengths[0] = hit.LengthInUtf16;
        for (var i = 0; i < table.Groups.Length; i++)
        {
            var number = table.Groups[i];
            if ((uint)number >= (uint)groupCount)
            {
                return false;
            }

            if (projectionStarts[i] < 0)
            {
                continue;
            }

            starts[number] = hit.IndexInUtf16 + projectionStarts[i];
            lengths[number] = projectionLengths[i];
        }

        detailed = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, hit.IndexInUtf16, hit.LengthInUtf16, starts, lengths);
        return true;
    }


    private bool TryShapeAlternationBranchDetailedMatch(
        ReadOnlySpan<byte> input,
        string decoded,
        Utf8BoundaryMap boundaryMap,
        Utf8ValueMatch hit,
        out Utf8MatchContext detailed)
    {
        // R05: identifies the winning branch by ordinal text comparison against the
        // owned discovery hit, then projects its spans. The first text-matching branch
        // wins: BCL tries branches in order, and with the same text consumed at the same
        // position every earlier-matching branch would have committed already (literal
        // runs carry no backreference or conditional side channels, which decline at
        // construction). Any length or capture-number disagreement still declines.
        detailed = default;
        var map = _alternationBranchSpanMap;
        if (map is null)
        {
            return false;
        }

        var hitSpan = decoded.AsSpan(hit.IndexInUtf16, hit.LengthInUtf16);
        var selected = -1;
        for (var branch = 0; branch < map.BranchExpansions.Length; branch++)
        {
            var expansion = map.BranchExpansions[branch];
            if (expansion.Length != hit.LengthInUtf16 || !hitSpan.SequenceEqual(expansion.AsSpan()))
            {
                continue;
            }

            selected = branch;
            break;
        }

        if (selected < 0)
        {
            return false;
        }

        var groupCount = _groupNumbers.Length;
        var starts = new int[groupCount];
        var lengths = new int[groupCount];
        Array.Fill(starts, -1);
        starts[0] = hit.IndexInUtf16;
        lengths[0] = hit.LengthInUtf16;
        foreach (var whole in map.WholeMatchGroups)
        {
            if ((uint)whole >= (uint)groupCount)
            {
                return false;
            }

            starts[whole] = hit.IndexInUtf16;
            lengths[whole] = hit.LengthInUtf16;
        }

        var selectedGroups = map.BranchGroups[selected];
        var selectedStarts = map.BranchStarts[selected];
        var selectedLengths = map.BranchLengths[selected];
        for (var i = 0; i < selectedGroups.Length; i++)
        {
            var number = selectedGroups[i];
            if ((uint)number >= (uint)groupCount)
            {
                return false;
            }

            starts[number] = hit.IndexInUtf16 + selectedStarts[i];
            lengths[number] = selectedLengths[i];
        }

        detailed = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, hit.IndexInUtf16, hit.LengthInUtf16, starts, lengths);
        return true;
    }

    // R05: construction-cached pure-tree capture predicate for the template divert.
    // A missing tree conservatively reports captures so BCL routing is preserved.
    private static bool HasOwnedCaptures(Utf8ExecutionTree? tree)
    {
        if (tree?.Root is null)
        {
            return true;
        }

        return ContainsCapture(tree.Root);
    }

    private struct CaptureCheckFrame(Utf8ExecutionNode node)
    {
        internal readonly Utf8ExecutionNode Node = node;
        internal int NextChild;
    }

    private static bool ContainsCapture(Utf8ExecutionNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<CaptureCheckFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (frame.Node.Kind == Utf8ExecutionNodeKind.Capture && frame.Node.CaptureNumber > 0) return true;
            if (frame.NextChild < frame.Node.Children.Count)
            {
                var child = frame.Node.Children[frame.NextChild++];
                frames[index] = frame;
                frames.Push(new(child));
            }
            else frames.Pop();
        }
        // This predicate retains no storage, so its construction scope releases
        // the frame descriptor as well as its array on either result.
        return false;
    }

    // R05: construction-cached caret-anchored grouped-literal prefix for direct
    // discovery without BCL verification. Admits ^ (outside Multiline) and \A followed
    // by a single ASCII literal run through transparent groups/captures to the end
    // of the tree, with a grouped span table covering the same total length for
    // shaping. Anchors pin every match at offset 0 with the literal bytes, so a
    // byte-prefix compare decides every group-agnostic lane exactly; group
    // shaping reuses the span table. Multiline ^, RightToLeft, IgnoreCase,
    // NonBacktracking, sets, loops, alternations, and other anchors stay out.
    private static bool TryGetCaretAnchoredGroupedLiteral(
        Utf8ExecutionTree? tree,
        RegexOptions options,
        Utf8GroupedLiteralSpanTable? spanTable,
        out byte[]? prefixUtf8)
    {
        prefixUtf8 = null;
        if (spanTable is null || tree?.Root is not { Kind: Utf8ExecutionNodeKind.Capture, CaptureNumber: 0 } root ||
            root.Children.Count != 1 ||
            (options & (RegexOptions.RightToLeft | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)) != 0)
        {
            return false;
        }

        var node = root.Children[0];
        while (node.Kind == Utf8ExecutionNodeKind.Group && node.Children.Count == 1)
        {
            node = node.Children[0];
        }


        // The anchor leads a top-level concatenation (probed: ^ and \A both lower
        // to Beginning with the literal run as siblings); a trailing anchor, which
        // the span table tolerates for shaping, stays out here.
        if (node.Kind != Utf8ExecutionNodeKind.Concatenate || node.Children.Count < 2)
        {
            return false;
        }

        var head = node.Children[0];
        var absoluteStart = head.Kind == Utf8ExecutionNodeKind.Beginning && head.Children.Count == 0;
        if (!absoluteStart && head.Kind == Utf8ExecutionNodeKind.Bol && head.Children.Count == 0 &&
            (options & RegexOptions.Multiline) == 0)
        {
            absoluteStart = true;
        }

        if (!absoluteStart)
        {
            return false;
        }

        var literal = new System.Collections.Generic.List<byte>();
        for (var i = 1; i < node.Children.Count; i++)
        {
            if (!AppendCaretLiteralRun(node.Children[i], literal))
            {
                return false;
            }
        }

        if (literal.Count == 0 || literal.Count != spanTable.TotalLengthInUtf16)
        {
            return false;
        }

        prefixUtf8 = literal.ToArray();
        return true;
    }

    // Collects ASCII literal bytes through transparent groups/captures. Sets,
    // loops, alternations, anchors, and non-ASCII content stay out, exactly
    // matching the span-table literal-run definition minus its width-only set
    // arm (byte-exact shaping needs the characters themselves).
    private static bool AppendCaretLiteralRun(Utf8ExecutionNode node, System.Collections.Generic.List<byte> literal)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.One:
                if (node.Ch >= 0x80)
                {
                    return false;
                }

                literal.Add((byte)node.Ch);
                return true;
            case Utf8ExecutionNodeKind.Multi:
                if (node.Text is null)
                {
                    return false;
                }

                foreach (var ch in node.Text)
                {
                    if (ch >= 0x80)
                    {
                        return false;
                    }

                    literal.Add((byte)ch);
                }

                return node.Text.Length > 0;
            case Utf8ExecutionNodeKind.Concatenate:
                foreach (var child in node.Children)
                {
                    if (!AppendCaretLiteralRun(child, literal))
                    {
                        return false;
                    }
                }

                return node.Children.Count > 0;
            case Utf8ExecutionNodeKind.Group:
                if (node.Children.Count != 1)
                {
                    return false;
                }

                return AppendCaretLiteralRun(node.Children[0], literal);
            case Utf8ExecutionNodeKind.Capture:
                if (node.CaptureNumber <= 0 || node.Children.Count != 1)
                {
                    return false;
                }

                return AppendCaretLiteralRun(node.Children[0], literal);
            default:
                return false;
        }
    }

    private bool TryGetBranchedLiteralFamilyMap(out Utf8LiteralFamilyCaptureMap? map)
    {
        // R03: multi-group top-level literal alternations with a branch-to-group map discover
        // through the family cursor and shape from owned spans. The cursor scans left to right,
        // so right-to-left shapes keep the BCL routes.
        map = null;
        if (_literalFamilyCaptureMap is null ||
            _preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) ||
            _groupNumbers.Length <= 2 ||
            UsesRightToLeft())
        {
            return false;
        }

        map = _literalFamilyCaptureMap;
        return true;
    }

    // Adapter-owned split discovery: literal shapes (singular or family, grouped or not) share
    // one branch-indexed cursor over the construction-time capture map. False declines to the
    // adapter BCL routes; the caller validates branch identity on its first hit.
    internal bool TryCreateOwnedSplitCursor(ReadOnlySpan<byte> input, out Utf8OperationMatchCursor cursor, out Utf8LiteralFamilyCaptureMap? map)
    {
        cursor = default;
        map = null;
        if (_literalFamilyCaptureMap is null ||
            _preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) ||
            UsesRightToLeft() ||
            ShouldUseKelvinSignFallback(input))
        {
            return false;
        }

        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            CreateExecutionBudget());
        map = _literalFamilyCaptureMap;
        return true;
    }

    // Returns true when the branched path owns the result (hit or miss); false declines to BCL.
    private bool TryMatchDetailedBranchedLiteralFamily(
        ReadOnlySpan<byte> input,
        string decoded,
        Utf8BoundaryMap boundaryMap,
        Utf8LiteralFamilyCaptureMap map,
        int startInUtf16,
        out Utf8MatchContext detailed)
    {
        detailed = default;
        if (ShouldUseKelvinSignFallback(input))
        {
            return false;
        }

        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            budget);
        try
        {
            while (cursor.MoveNext())
            {
                var candidate = cursor.CurrentValueMatch;
                if (!candidate.Success || candidate.IndexInUtf16 < startInUtf16)
                {
                    continue;
                }

                var branch = cursor.LastLiteralFamilyBranchId;
                if (branch < 0 || (uint)branch >= (uint)map.BranchGroups.Length)
                {
                    return false;
                }

                detailed = CreateBranchedDetailedMatch(input, decoded, boundaryMap, map, branch, candidate.IndexInUtf16, candidate.LengthInUtf16);
                return true;
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }

        detailed = Utf8MatchContext.Unsuccessful(input, decoded, boundaryMap, _groupNames);
        return true;
    }

    // Shapes one owned detailed view from a mapped branch hit shared by detailed matching
    // and evaluator replacement.
    private Utf8MatchContext CreateBranchedDetailedMatch(
        ReadOnlySpan<byte> input,
        string decoded,
        Utf8BoundaryMap boundaryMap,
        Utf8LiteralFamilyCaptureMap map,
        int branch,
        int indexInUtf16,
        int lengthInUtf16)
    {
        var starts = new int[_groupNumbers.Length];
        var lengths = new int[_groupNumbers.Length];
        Array.Fill(starts, -1);
        starts[0] = indexInUtf16;
        lengths[0] = lengthInUtf16;
        foreach (var whole in map.WholeMatchGroups)
        {
            if ((uint)whole < (uint)starts.Length)
            {
                starts[whole] = indexInUtf16;
                lengths[whole] = lengthInUtf16;
            }
        }

        foreach (var group in map.BranchGroups[branch])
        {
            if ((uint)group < (uint)starts.Length)
            {
                starts[group] = indexInUtf16;
                lengths[group] = lengthInUtf16;
            }
        }

        return new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, indexInUtf16, lengthInUtf16, starts, lengths);
    }

    /// <summary>Enumerates non-overlapping matches in the well-formed UTF-8 input.</summary>
    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input)
    {
        if (TryCreateNativeSimpleEnumerator(input, CancellationToken.None, out var simpleEnumerator)) return simpleEnumerator;
        if (TryCreateNativeStructuralEnumerator(input, CancellationToken.None, out var structuralEnumerator)) return structuralEnumerator;
        // Anchored prefixes yield at most one match at offset 0 through the
        // anchor-enforcing owned cursor, without BCL construction.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } anchoredEnumerateLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, anchoredEnumerateLiteral, CreateExecutionBudget()))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        // Anchored suffixes yield at most one match at the input end through the
        // anchor-enforcing owned cursor, without BCL construction.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, _fallbackDirectFamily.LiteralUtf8, CreateExecutionBudget(), _fallbackDirectFamily.Kind))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        // Folded anchored prefixes enumerate the same single anchored hit with
        // per-byte invariant folding. Kelvin and Turkic divergences fall through to
        // the BCL-backed routes below; all other inputs stay direct.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } foldedPrefixLiteral)
        {
            if (!ShouldUseKelvinSignFallback(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input, foldedPrefixLiteral, CreateExecutionBudget(), _fallbackDirectFamily.Kind))
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }
        }

        // Folded anchored suffixes enumerate the same single tail hit with per-byte
        // invariant folding, under the same fence contract as prefixes.
        if (_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } foldedSuffixLiteral)
        {
            if (!ShouldUseKelvinSignFallback(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input, foldedSuffixLiteral, CreateExecutionBudget(), _fallbackDirectFamily.Kind))
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }
        }

        // R05: caret-anchored grouped literals enumerate the single anchored hit
        // through the anchor-enforcing prefix cursor (group-agnostic value spans;
        // shaping reuses the span table). Kelvin stays fenced like the direct arms.
        if (_caretAnchoredGroupedLiteralUtf8 is { Length: > 0 } caretEnumeratePrefix &&
            !ShouldUseKelvinSignFallback(input))
        {
            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, caretEnumeratePrefix, CreateExecutionBudget()))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        if (GetOwnedSliceProgram(input) is { } ownedProgram)
        {
            return OwnedSliceEnumerateMatches(input, ownedProgram, CancellationToken.None)
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        var subject = Utf8ValidatedInput.Create(input);
        var startOffsetInBytes = UsesRightToLeft() ? input.Length : 0;
        var start = subject.GetBytePosition(startOffsetInBytes, "startOffsetInBytes");
        return new Utf8ByteOffsetExecution(this).EnumerateMatches(subject, start)
            .WithTimeoutMapping(input, Pattern, MatchTimeout);
    }

    private Utf8ValueMatchEnumerator OwnedSliceEnumerateMatches(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        try
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
            var validation = Utf8Validation.Validate(input, ref invocation);
            var cursor = Utf8OperationMatchCursor.CreateMapped(program, input, validation, ref invocation, false, out _);
            return new Utf8ValueMatchEnumerator(cursor);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private Utf8ValueMatchEnumerator OwnedSliceEnumerateMatchesFromUnit(ReadOnlySpan<byte> input, Utf8SliceProgram program, Utf8BoundaryMap map, int startUnit, CancellationToken token)
    {
        try
        {
            return new Utf8ValueMatchEnumerator(program, input, map, MatchTimeout, token, startUnit);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Enumerates non-overlapping matches in the well-formed UTF-8 input, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryCreateNativeSimpleEnumerator(input, cancellationToken, out var simpleEnumerator)) return simpleEnumerator;
        if (TryCreateNativeStructuralEnumerator(input, cancellationToken, out var structuralEnumerator)) return structuralEnumerator;
        // Single literals now retain one invocation through bounded validation,
        // discovery, confirmation and projection. Other shapes retain the scalar VM.
        // A possible non-ASCII folding partner stays in its existing semantic route.
        if (!_requiresKelvinSignFallback &&
            (_ownedSliceProgram is not { IgnoreCase: true } ||
                _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCase) &&
            _preparedRegex.LiteralUtf8 is { Length: > 0 } &&
            _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or
                NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, cancellationToken);
            try
            {
                var validation = Utf8Validation.Validate(input, ref invocation);
                return new Utf8ValueMatchEnumerator(new Utf8OperationMatchCursor(
                    input, _preparedRegex, validation.IsAscii, UsesRightToLeft(), invocation))
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        }

        if (!UsesRightToLeft() && !_requiresKelvinSignFallback &&
            _ownedSliceProgram is not ({ HasBalancing: true } or { HasSharedCaptureDeclarations: true } or { HasExplicitCaptureNumbers: true }) &&
            (_ownedSliceProgram is not { IgnoreCase: true } ||
                _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) &&
            _preparedRegex.ExecutionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals)
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, cancellationToken);
            try
            {
                var validation = Utf8Validation.Validate(input, ref invocation);
                return new Utf8ValueMatchEnumerator(new Utf8OperationMatchCursor(
                    input, _preparedRegex, validation.IsAscii, invocation)).WithTimeoutMapping(input, Pattern, MatchTimeout);
            }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        }

        if (GetOwnedSliceProgram(input, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceEnumerateMatches(input, ownedProgram, cancellationToken)
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        return EnumerateMatches(input);
    }

    /// <summary>Enumerates non-overlapping matches starting at the specified UTF-16 code-unit offset.</summary>
    public Utf8ValueMatchEnumerator EnumerateMatchesFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        if (TryCreateOwnedOffsetEnumerator(input, utf16Offset, CancellationToken.None, out var ownedEnumerator)) return ownedEnumerator;
        var analysis = Utf8InputAnalyzer.Analyze(input);
        var startInUtf16 = analysis.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        // Window boundaries inspect the original subject, including the unit
        // before startat. Slicing the suffix would invent a word boundary there.
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow &&
            !UsesRightToLeft() &&
            Utf8InputAnalyzer.IsAscii(input))
        {
            return new Utf8ValueMatchEnumerator(
                Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(
                    _preparedRegex, _verifierRuntime, input,
                    new Utf8ValidationResult(input.Length, input.Length, true, false),
                    CreateExecutionBudget(), startInUtf16.Value, startInUtf16.Value))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        // Anchored prefixes from a nonzero start are a definitive miss; from offset 0
        // they reuse the single-match owned cursor without BCL construction.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } anchoredOffsetLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (startInUtf16.Value != 0)
            {
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }

            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, anchoredOffsetLiteral, CreateExecutionBudget()))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        // Anchored suffixes match at most once at a fixed end position: a start past
        // it misses definitively, otherwise the single-match cursor decides.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixEnumerate) &&
                suffixEnumerate.Success &&
                startInUtf16.Value <= suffixEnumerate.IndexInUtf16)
            {
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input, _fallbackDirectFamily.LiteralUtf8, CreateExecutionBudget(), _fallbackDirectFamily.Kind))
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }

            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }


        // R05: slice shapes enumerate from the start unit on full input through
        // the owned cursor above (anchors keep true left context and \\G pins to
        // the scan start with the original context, unlike suffix slicing).
        // Derived starts must resolve to a complete scalar boundary.
        if (_ownedSliceProgram is { } ownedProgram)
        {
            return OwnedSliceEnumerateMatchesFromUnit(input, ownedProgram, analysis.BoundaryMap, startInUtf16.Value, CancellationToken.None)
                .WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        if (!UsesRightToLeft())
        {
            var startBoundary = analysis.BoundaryMap.Resolve(startInUtf16.Value);
            if (startBoundary.IsScalarBoundary)
            {
                var startInBytes = analysis.GetBytePosition(startBoundary.ByteOffset, nameof(utf16Offset));
                return new Utf8ByteOffsetExecution(this).EnumerateMatches(analysis, startInBytes)
                    .WithTimeoutMapping(input, Pattern, MatchTimeout);
            }
        }

        var decoded = analysis.GetDecodedString();
        return new Utf8ValueMatchEnumerator(
            input,
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
            decoded,
            startInUtf16.Value,
            analysis.Utf16ProjectionMap)
            .WithTimeoutMapping(input, Pattern, MatchTimeout);
    }

    /// <summary>Enumerates non-overlapping matches starting at the specified UTF-16 code-unit offset, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueMatchEnumerator EnumerateMatchesFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (TryCreateOwnedOffsetEnumerator(input, utf16Offset, cancellationToken, out var ownedEnumerator)) return ownedEnumerator;
        return EnumerateMatchesFromUtf16Offset(input, utf16Offset);
    }

    /// <summary>Enumerates all split segments, including the final tail.</summary>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input) =>
        EnumerateSplits(input, int.MaxValue);

    /// <summary>Enumerates all split segments, including the final tail, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_ownedSliceProgram is { } ownedProgram)
            return OwnedSliceEnumerateSplits(input, ownedProgram, int.MaxValue, cancellationToken);
        return EnumerateSplits(input);
    }

    /// <summary>Enumerates at most the requested number of split segments.</summary>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (GetOwnedSliceProgram(input) is { } ownedProgram)
            return OwnedSliceEnumerateSplits(input, ownedProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);
        // R05: right-to-left slice shapes split owned through the same cursor
        // the ascending arm uses, projecting segments descending from the input end
        // with UTF-16 indexes exactly like the BCL oracle (encounter order, tail
        // piece first). Culture, Kelvin, and unserved shapes keep the legacy routing
        // below; count 1 keeps the BCL tail while count 0 means unlimited here.
        if ((count == 0 || count > 1) &&
            !ShouldUseKelvinSignFallback(input) &&
            _sliceProgram is not null && _sliceProgram.RightToLeft)
        {
            var rtlAnalysis = Utf8InputAnalyzer.Analyze(input);
            var rtlDecoded = rtlAnalysis.GetDecodedString();
            return new Utf8ValueSplitEnumerator(
                input,
                rtlDecoded,
                new Utf8OperationMatchCursor(_sliceProgram, input, rtlAnalysis.BoundaryMap, MatchTimeout, CancellationToken.None),
                rtlAnalysis.BoundaryMap,
                count == 0 ? int.MaxValue : count,
                rtlDecoded.Length,
                rightToLeft: true)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }
        // R05: right-to-left literal shapes split owned through the reverse-search
        // cursor (the same cursor backing owned RTL enumeration), projecting segments
        // descending from the input end exactly like the oracle encounter order.
        // Mirrors the RTL enumeration gate: boundary/trailing requirements, culture,
        // and Kelvin-folded inputs keep their existing routes.
        if ((count == 0 || count > 1) &&
            UsesRightToLeft() &&
            _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase &&
            _preparedRegex.LiteralUtf8 is { Length: > 0 } rtlSplitLiteral &&
            !_preparedRegex.SearchPlan.HasBoundaryRequirements &&
            !_preparedRegex.SearchPlan.HasTrailingLiteralRequirement &&
            (_preparedRegex.ExecutionKind != NativeExecutionKind.AsciiLiteralIgnoreCase || !ShouldUseKelvinSignFallback(input)))
        {
            var rtlAnalysis = Utf8InputAnalyzer.Analyze(input);
            var rtlDecoded = rtlAnalysis.GetDecodedString();
            var rtlBudget = CreateExecutionBudget();
            return new Utf8ValueSplitEnumerator(
                input,
                rtlDecoded,
                new Utf8OperationMatchCursor(input, _preparedRegex, rtlSplitLiteral, _preparedRegex.ExecutionKind, rtlBudget, Utf8InputAnalyzer.IsAscii(input), rightToLeft: true),
                rtlAnalysis.BoundaryMap,
                count == 0 ? int.MaxValue : count,
                rtlDecoded.Length,
                rightToLeft: true)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        if (UsesRightToLeft() || ShouldUseKelvinSignFallback(input))
        {
            if (count >= 0 && _ownedSliceProgram is { } resumedProgram)
                return OwnedSliceEnumerateSplits(input, resumedProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);
            var subject = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueSplitEnumerator(input, subject.GetDecodedString(), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, subject.BoundaryMap)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        if (count > 0 && TryGetSmallAsciiLiteralFamilySplitSearch(input, out var smallAsciiLiteralFamilySearch))
        {
            return new Utf8ValueSplitEnumerator(
                input,
                smallAsciiLiteralFamilySearch,
                count)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }


        var budget = CreateExecutionBudget();
        var usesAsciiValidationShortcut = TryUseAsciiInputValidationShortcut(input);
        var validation = usesAsciiValidationShortcut
            ? new Utf8ValidationResult(input.Length, input.Length, true, false)
            : Utf8Validation.Validate(input);
        var totalUtf16Length = usesAsciiValidationShortcut
            ? input.Length
            : validation.Utf16Length;
        // R05: anchored ASCII literal families split through the anchor-enforcing
        // owned cursors (at most one match at a fixed position), like enumeration.
        // Inlined to hold the single-caller helper ceiling.
        var isAnchoredDirectSplitFamily = _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            (_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix ||
            Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) ||
            _fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd);
        // Count zero requests an unlimited split. Window cursors can serve it
        // through the same advancing scan as the default (int.MaxValue) count.
        if ((count > 0 || count == 0 && _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow) &&
            (CanUseNativeSplit(validation) || isAnchoredDirectSplitFamily || CanServeLineAnchoredOwned(input)))
        {
            return CreateSplitEnumeratorViaCompiledEngine(input, validation, totalUtf16Length, count == 0 ? int.MaxValue : count, budget)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        // R05: caret-anchored grouped literals split the single anchored hit through
        // the anchor-enforcing prefix cursor (group-agnostic segments). Counts below 2
        // keep the BCL tail like the cursor arms around it.
        if (count > 1 &&
            _caretAnchoredGroupedLiteralUtf8 is { Length: > 0 } caretSplitPrefix &&
            !ShouldUseKelvinSignFallback(input))
        {
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, caretSplitPrefix, CreateExecutionBudget()),
                count,
                totalUtf16Length)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        // R05: absolute-anchored fixed-length simple patterns split owned through
        // the simple-pattern cursor: anchors pin boundary-to-boundary hits with no
        // empty matches, so cursor hits tile identically to the oracle (the same
        // cursor backs owned enumeration). Variable lengths, empty-capable shapes,
        // and inputs the factory cursor declines keep their existing routes.
        if (count > 1 &&
            _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _preparedRegex.SimplePatternPlan.IsAbsoluteStartAnchored &&
            _preparedRegex.SimplePatternPlan.IsAbsoluteEndAnchored &&
            _preparedRegex.SimplePatternPlan.IsFixedLength &&
            _preparedRegex.SimplePatternPlan.MinLength > 0 &&
            (Utf8InputAnalyzer.IsAscii(input) || _preparedRegex.SimplePatternPlan.IsUtf8ByteSafe) &&
            !ShouldUseKelvinSignFallback(input))
        {
            return Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(
                _preparedRegex,
                _verifierRuntime,
                input,
                validation,
                totalUtf16Length,
                count,
                budget)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        // R03: split segments project the owned match cursor, whose empty-match
        // advancement already follows the BCL search order. Counts below 2 keep the
        // BCL tail below with identical behavior (count 0 means unlimited there,
        // while count 1 throws inside its first MoveNext). Right-to-left splits keep
        // legacy routing until the enumerator stages buffered right-to-left ranges
        // (segments stay positional while discovery runs right-to-left).
        if (count > 1 && _sliceProgram is not null && !_sliceProgram.RightToLeft)
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            var decoded = analysis.GetDecodedString();
            return new Utf8ValueSplitEnumerator(
                input,
                decoded,
                new Utf8OperationMatchCursor(_sliceProgram, input, analysis.BoundaryMap, MatchTimeout, CancellationToken.None),
                analysis.BoundaryMap,
                count,
                decoded.Length,
                rightToLeft: false)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        // R05: scalar-verifiable simple patterns split owned through the scalar
        // cursor; anything else keeps the BCL split below.
        if (count > 1 && Utf8ScalarSimplePatternVerifier.CanServeScalar(_preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, input))
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            var decoded = analysis.GetDecodedString();
            return new Utf8ValueSplitEnumerator(
                input,
                decoded,
                new Utf8OperationMatchCursor(input, _preparedRegex, analysis.BoundaryMap, budget),
                analysis.BoundaryMap,
                count,
                decoded.Length,
                rightToLeft: false)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        // Native split cursors above retain their discovery paths. Count zero
        // and any other admitted shape use the owned scalar cursor before BCL.
        if (count >= 0 && _ownedSliceProgram is { } splitProgram)
            return OwnedSliceEnumerateSplits(input, splitProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);
        var fallbackSubject = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(input, fallbackSubject.GetDecodedString(), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, fallbackSubject.BoundaryMap)
            .WithTimeoutMapping(Pattern, MatchTimeout);
    }

    /// <summary>Enumerates at most the requested number of split segments, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="count">The maximum number of split segments to return.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (_ownedSliceProgram is { } ownedProgram)
            return OwnedSliceEnumerateSplits(input, ownedProgram, count == 0 ? int.MaxValue : count, cancellationToken);
        return EnumerateSplits(input, count);
    }

    private Utf8ValueSplitEnumerator OwnedSliceEnumerateSplits(ReadOnlySpan<byte> input, Utf8SliceProgram program, int count, CancellationToken token)
    {
        try
        {
            var invocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, MatchTimeout, token);
            return Utf8OwnedSplitPreparation.Create(program, input, count, ref invocation)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Replaces all matches and returns newly allocated UTF-8 bytes.</summary>
    /// <param name="input">The well-formed UTF-8 input.</param>
    /// <param name="replacement">A .NET replacement pattern expressed as UTF-16 text.</param>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement)
    {
        try
        {
            return ReplaceStringCore(input, replacement);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Replaces all matches and returns newly allocated UTF-8 bytes, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ReplaceStringCore(input, replacement, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private byte[] ReplaceStringCore(ReadOnlySpan<byte> input, string replacement)
    {
        return ReplaceStringCore(input, replacement, CancellationToken.None);
    }

    private byte[] ReplaceStringCore(ReadOnlySpan<byte> input, string replacement, CancellationToken cancellationToken)
    {
        var analyzed = GetParsedReplacement(replacement);
        return ReplaceCore(input, replacement, analyzed, cancellationToken);
    }

    /// <summary>Replaces all matches using a .NET replacement pattern encoded as well-formed UTF-8.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8)
    {
        try
        {
            return ReplaceUtf8Core(input, replacementPatternUtf8);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Replaces all matches using a .NET replacement pattern encoded as well-formed UTF-8, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacementPatternUtf8">The replacement pattern encoded as well-formed UTF-8.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ReplaceUtf8Core(input, replacementPatternUtf8, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private byte[] ReplaceUtf8Core(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8)
    {
        return ReplaceUtf8Core(input, replacementPatternUtf8, CancellationToken.None);
    }

    private byte[] ReplaceUtf8Core(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, CancellationToken cancellationToken)
    {
        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        if (ShouldUseKelvinSignFallback(input) && _ownedSliceProgram is null)
        {
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                Encoding.UTF8.GetString(replacementPatternUtf8)));
        }

        if ((GetOwnedSliceProgram(input, cancellationToken) ??
            (input.IsEmpty || _ownedSliceProgram is { CanMatchEmpty: true } &&
                _preparedRegex.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral ? _ownedSliceProgram : null)) is { } ownedProgram)
        {
            return OwnedSliceReplace(input, ownedProgram,
                GetParsedReplacement(Encoding.UTF8.GetString(replacementPatternUtf8)), Utf8SliceReplacementResultKind.Bytes, default, cancellationToken).Bytes;
        }


        if (TryGetDirectLiteralReplacementBytes(replacementPatternUtf8, out var directReplacementBytes))
        {
            return ReplaceLiteralBytesCore(input, validation, directReplacementBytes);
        }

        var replacementText = Encoding.UTF8.GetString(replacementPatternUtf8);
        var replacementPattern = GetParsedReplacement(replacementText);
        if (GetOwnedReplacementProgram(input, replacementPattern, cancellationToken) is { } requiredProgram)
            return OwnedSliceReplace(input, requiredProgram, replacementPattern,
                Utf8SliceReplacementResultKind.Bytes, default, cancellationToken).Bytes;
        var budget = CreateExecutionBudget();
        if (UsesOwnedTemplateBeforeNativePlan() &&
            TryReplaceTemplateWithOwnedGroups(input, replacementPattern, out var ownedTemplate))
        {
            return Encoding.UTF8.GetBytes(ownedTemplate);
        }

        if (ShouldUseFallbackForSimplePatternReplacement(replacementPattern, input))
        {
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText));
        }

        if (TryGetExactLiteralReplacementBytes(replacementPattern, out var exactLiteralReplacementBytes))
        {
            return ReplaceLiteralBytesCore(input, validation, exactLiteralReplacementBytes);
        }

        if (TryReplaceViaNativePlan(input, validation, replacementPattern, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacementPattern, out var replacementBytes))
        {
            return Encoding.UTF8.GetBytes(ReplaceFallbackWithSharedPlan(input, replacementPattern, cancellationToken));
        }

        return ReplaceLiteralBytesCore(input, validation, replacementBytes);
    }

    /// <summary>Replaces all matches through a stateful callback that writes UTF-8 replacement content.</summary>
    /// <typeparam name="TState">The caller-defined state type.</typeparam>
    /// <exception cref="InvalidOperationException">A match splits a UTF-8 scalar.</exception>
    /// <exception cref="ArgumentException">The evaluator produces malformed UTF-8.</exception>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator)
    {
        return ReplaceEvaluatorCore(input, state, evaluator, default);
    }

    private byte[] ReplaceEvaluatorCore<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        if ((_sliceProgram ?? _ownedSliceProgram) is { } ownedProgram)
        {
            try { return OwnedSliceReplaceEvaluator(input, ownedProgram, state, evaluator, cancellationToken); }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        }
        var validation = Utf8Validation.Validate(input);
        var decoded = Encoding.UTF8.GetString(input);
        var boundaryMap = Utf8BoundaryMap.Create(input, validation);
        // R05: empty input still runs the evaluator loop below (BCL-equivalent
        // patterns can match empty), and the writer rejects a zero capacity.
        var output = new ArrayBufferWriter<byte>(Math.Max(input.Length, 1));
        var writer = new Utf8ReplacementWriter(output);
        var sourcePosition = 0;
        // R03: patterns without captures project every callback argument from owned spans,
        // so discovery and shaping both run without the BCL engine; other patterns keep it.
        if (_groupNumbers.Length <= 1)
        {
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!valueMatch.IsByteAligned)
                {
                    throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
                }

                writer.Append(input[sourcePosition..valueMatch.IndexInBytes]);
                var context = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16);
                evaluator(in context, ref writer, ref state);
                sourcePosition = valueMatch.IndexInBytes + valueMatch.LengthInBytes;
            }

            writer.Append(input[sourcePosition..]);
            return writer.GetValidatedBytes().ToArray();
        }
        // R03: single groups wrapping a whole literal family enumerate like groupless
        // shapes and project group 1 from the whole span.
        if (TryGetOwnedDetailedGroupCount(out var ownedGroupCount) && ownedGroupCount > 1)
        {
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!valueMatch.IsByteAligned)
                {
                    throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
                }

                writer.Append(input[sourcePosition..valueMatch.IndexInBytes]);
                var context = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16, ownedGroupCount);
                evaluator(in context, ref writer, ref state);
                sourcePosition = valueMatch.IndexInBytes + valueMatch.LengthInBytes;
            }

            writer.Append(input[sourcePosition..]);
            return writer.GetValidatedBytes().ToArray();
        }


        // R05: grouped single-literal shapes enumerate like groupless shapes and
        // project every group from fixed literal-relative spans. The first hit validates
        // the projection channel before any output, mirroring the branched contract.
        if (_groupedLiteralSpanTable is not null &&
            !ShouldUseKelvinSignFallback(input))
        {
            var groupedDeclined = false;
            var groupedFirst = true;
            try
            {
                foreach (var valueMatch in EnumerateMatches(input))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!valueMatch.IsByteAligned)
                    {
                        throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
                    }

                    if (!TryShapeGroupedLiteralDetailedMatch(input, decoded, boundaryMap, valueMatch, out var groupedContext))
                    {
                        if (!groupedFirst)
                        {
                            throw new InvalidOperationException("The grouped span projection failed after matching began.");
                        }

                        groupedDeclined = true;
                        break;
                    }

                    groupedFirst = false;
                    writer.Append(input[sourcePosition..valueMatch.IndexInBytes]);
                    evaluator(in groupedContext, ref writer, ref state);
                    sourcePosition = valueMatch.IndexInBytes + valueMatch.LengthInBytes;
                }
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!groupedDeclined)
            {
                writer.Append(input[sourcePosition..]);
                return writer.GetValidatedBytes().ToArray();
            }
        }

        // R03: mapped branch shapes discover through the family cursor and shape every
        // callback argument from owned spans. The first hit validates the branch channel;
        // no output is produced before it, so an unknown branch still declines cleanly.
        if (TryGetBranchedLiteralFamilyMap(out var branchMap) &&
            !ShouldUseKelvinSignFallback(input))
        {
            var budget = CreateExecutionBudget();
            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
                _preparedRegex,
                _verifierRuntime,
                input,
                validation,
                budget);
            var declined = false;
            try
            {
                var first = true;
                while (cursor.MoveNext())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidate = cursor.CurrentValueMatch;
                    if (!candidate.IsByteAligned)
                    {
                        throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
                    }

                    var branch = cursor.LastLiteralFamilyBranchId;
                    if (first)
                    {
                        first = false;
                        if (branch < 0 || (uint)branch >= (uint)branchMap.BranchGroups.Length)
                        {
                            declined = true;
                            break;
                        }
                    }

                    writer.Append(input[sourcePosition..candidate.IndexInBytes]);
                    var context = CreateBranchedDetailedMatch(input, decoded, boundaryMap, branchMap, branch, candidate.IndexInUtf16, candidate.LengthInUtf16);
                    evaluator(in context, ref writer, ref state);
                    sourcePosition = candidate.IndexInBytes + candidate.LengthInBytes;
                }
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!declined)
            {
                writer.Append(input[sourcePosition..]);
                return writer.GetValidatedBytes().ToArray();
            }
        }

        // R05: divergent single-alternation shapes enumerate like groupless shapes and
        // project the winning branch spans identified by ordinal text comparison, with
        // the same first-hit decline contract as the arms above.
        if (_alternationBranchSpanMap is not null &&
            !ShouldUseKelvinSignFallback(input))
        {
            var alternationDeclined = false;
            var alternationFirst = true;
            try
            {
                foreach (var valueMatch in EnumerateMatches(input))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!valueMatch.IsByteAligned)
                    {
                        throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
                    }

                    if (!TryShapeAlternationBranchDetailedMatch(input, decoded, boundaryMap, valueMatch, out var alternationContext))
                    {
                        if (!alternationFirst)
                        {
                            throw new InvalidOperationException("The alternation branch projection failed after matching began.");
                        }

                        alternationDeclined = true;
                        break;
                    }

                    alternationFirst = false;
                    writer.Append(input[sourcePosition..valueMatch.IndexInBytes]);
                    evaluator(in alternationContext, ref writer, ref state);
                    sourcePosition = valueMatch.IndexInBytes + valueMatch.LengthInBytes;
                }
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!alternationDeclined)
            {
                writer.Append(input[sourcePosition..]);
                return writer.GetValidatedBytes().ToArray();
            }
        }


        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        while (match.Success)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var matchStart = boundaryMap.Resolve(match.Index);
            var matchEnd = boundaryMap.Resolve(match.Index + match.Length);
            if (!matchStart.IsScalarBoundary || !matchEnd.IsScalarBoundary)
            {
                throw new InvalidOperationException("The evaluator match is not aligned to UTF-8 scalar boundaries.");
            }

            writer.Append(input[sourcePosition..matchStart.ByteOffset]);
            var context = new Utf8MatchContext(input, decoded, match, boundaryMap, _groupNames);
            evaluator(in context, ref writer, ref state);
            sourcePosition = matchEnd.ByteOffset;
            match = match.NextMatch();
        }

        writer.Append(input[sourcePosition..]);
        return writer.GetValidatedBytes().ToArray();
    }

    /// <summary>Replaces all matches through a stateful callback that writes UTF-8 replacement content, observing cancellation before matching begins.</summary>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match to append replacement content.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReplaceEvaluatorCore(input, state, evaluator, cancellationToken);
    }

    /// <summary>Replaces all matches and returns the result as a UTF-16 string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement)
    {
        return ReplaceToString(input, replacement, CancellationToken.None);
    }

    /// <summary>Replaces all matches and returns the result as a UTF-16 string, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return ReplaceToStringMappedCore(input, replacement, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private string ReplaceToStringMappedCore(ReadOnlySpan<byte> input, string replacement, CancellationToken cancellationToken)
    {
        var analyzed = GetParsedReplacement(replacement);
        _ = Utf8Validation.Validate(input);
        return ReplaceToStringCore(input, analyzed, cancellationToken);
    }

    /// <summary>Replaces all matches through a stateful callback that returns UTF-16 replacement text.</summary>
    /// <typeparam name="TState">The caller-defined state type.</typeparam>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator)
    {
        return ReplaceToStringEvaluatorCore(input, state, evaluator, default);
    }

    private string ReplaceToStringEvaluatorCore<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        if ((_sliceProgram ?? _ownedSliceProgram) is { } ownedProgram)
        {
            try
            {
                return OwnedSliceReplace(input, ownedProgram, new Utf8SliceEvaluatorExpansion<TState>(evaluator, ownedProgram),
                    ref state, Utf8SliceReplacementResultKind.Text, default, cancellationToken).Text;
            }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        }
        var validation = Utf8Validation.Validate(input);
        var inputBytes = input.ToArray();
        var decoded = Encoding.UTF8.GetString(input);
        var boundaryMap = Utf8InputAnalyzer.Analyze(inputBytes).BoundaryMap;
        // R03: groupless matches enumerate and shape from owned spans; string slicing
        // needs no byte alignment, matching the BCL oracle on every input.
        if (_groupNumbers.Length <= 1)
        {
            var builder = new StringBuilder(decoded.Length);
            var sourcePosition = 0;
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                builder.Append(decoded, sourcePosition, valueMatch.IndexInUtf16 - sourcePosition);
                var context = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16);
                builder.Append(evaluator(in context, ref state));
                sourcePosition = valueMatch.IndexInUtf16 + valueMatch.LengthInUtf16;
            }

            builder.Append(decoded, sourcePosition, decoded.Length - sourcePosition);
            return builder.ToString();
        }
        // R03: single groups wrapping a whole literal family enumerate like groupless
        // shapes and project group 1 from the whole span.
        if (TryGetOwnedDetailedGroupCount(out var ownedGroupCount) && ownedGroupCount > 1)
        {
            var singleBuilder = new StringBuilder(decoded.Length);
            var singlePosition = 0;
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                singleBuilder.Append(decoded, singlePosition, valueMatch.IndexInUtf16 - singlePosition);
                var singleContext = new Utf8MatchContext(input, decoded, boundaryMap, _groupNames, valueMatch.IndexInUtf16, valueMatch.LengthInUtf16, ownedGroupCount);
                singleBuilder.Append(evaluator(in singleContext, ref state));
                singlePosition = valueMatch.IndexInUtf16 + valueMatch.LengthInUtf16;
            }

            singleBuilder.Append(decoded, singlePosition, decoded.Length - singlePosition);
            return singleBuilder.ToString();
        }


        // R05: grouped single-literal shapes enumerate like groupless shapes and
        // project every group from fixed literal-relative spans, with the same
        // first-hit decline contract as the arms around it.
        if (_groupedLiteralSpanTable is not null &&
            !ShouldUseKelvinSignFallback(input))
        {
            var groupedBuilder = new StringBuilder(decoded.Length);
            var groupedPosition = 0;
            var groupedDeclined = false;
            var groupedFirst = true;
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryShapeGroupedLiteralDetailedMatch(input, decoded, boundaryMap, valueMatch, out var groupedContext))
                {
                    if (!groupedFirst)
                    {
                        throw new InvalidOperationException("The grouped span projection failed after matching began.");
                    }

                    groupedDeclined = true;
                    break;
                }

                groupedFirst = false;
                groupedBuilder.Append(decoded, groupedPosition, valueMatch.IndexInUtf16 - groupedPosition);
                groupedBuilder.Append(evaluator(in groupedContext, ref state));
                groupedPosition = valueMatch.IndexInUtf16 + valueMatch.LengthInUtf16;
            }

            if (!groupedDeclined)
            {
                groupedBuilder.Append(decoded, groupedPosition, decoded.Length - groupedPosition);
                return groupedBuilder.ToString();
            }
        }

        // R03: mapped branch shapes discover through the family cursor and shape every
        // callback argument from owned spans, with the same first-hit decline contract as
        // the byte-wise evaluator overload.
        if (TryGetBranchedLiteralFamilyMap(out var branchMap) &&
            !ShouldUseKelvinSignFallback(input))
        {
            var budget = CreateExecutionBudget();
            var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
                _preparedRegex,
                _verifierRuntime,
                input,
                validation,
                budget);
            var builder = new StringBuilder(decoded.Length);
            var sourcePosition = 0;
            var declined = false;
            try
            {
                var first = true;
                while (cursor.MoveNext())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var candidate = cursor.CurrentValueMatch;
                    var branch = cursor.LastLiteralFamilyBranchId;
                    if (first)
                    {
                        first = false;
                        if (branch < 0 || (uint)branch >= (uint)branchMap.BranchGroups.Length)
                        {
                            declined = true;
                            break;
                        }
                    }

                    builder.Append(decoded, sourcePosition, candidate.IndexInUtf16 - sourcePosition);
                    var context = CreateBranchedDetailedMatch(input, decoded, boundaryMap, branchMap, branch, candidate.IndexInUtf16, candidate.LengthInUtf16);
                    builder.Append(evaluator(in context, ref state));
                    sourcePosition = candidate.IndexInUtf16 + candidate.LengthInUtf16;
                }
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input);
            }

            if (!declined)
            {
                builder.Append(decoded, sourcePosition, decoded.Length - sourcePosition);
                return builder.ToString();
            }
        }

        // R05: divergent single-alternation shapes enumerate like groupless shapes and
        // project the winning branch spans identified by ordinal text comparison, with
        // the same first-hit decline contract as the arms above.
        if (_alternationBranchSpanMap is not null &&
            !ShouldUseKelvinSignFallback(input))
        {
            var alternationBuilder = new StringBuilder(decoded.Length);
            var alternationPosition = 0;
            var alternationDeclined = false;
            var alternationFirst = true;
            foreach (var valueMatch in EnumerateMatches(input))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!TryShapeAlternationBranchDetailedMatch(input, decoded, boundaryMap, valueMatch, out var alternationContext))
                {
                    if (!alternationFirst)
                    {
                        throw new InvalidOperationException("The alternation branch projection failed after matching began.");
                    }

                    alternationDeclined = true;
                    break;
                }

                alternationFirst = false;
                alternationBuilder.Append(decoded, alternationPosition, valueMatch.IndexInUtf16 - alternationPosition);
                alternationBuilder.Append(evaluator(in alternationContext, ref state));
                alternationPosition = valueMatch.IndexInUtf16 + valueMatch.LengthInUtf16;
            }

            if (!alternationDeclined)
            {
                alternationBuilder.Append(decoded, alternationPosition, decoded.Length - alternationPosition);
                return alternationBuilder.ToString();
            }
        }


        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
            decoded,
            match =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var context = new Utf8MatchContext(inputBytes, decoded, match, boundaryMap, _groupNames);
                return evaluator(in context, ref state);
            });
    }

    /// <summary>Replaces all matches through a stateful callback that returns UTF-16 replacement text, observing cancellation before matching begins.</summary>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match to produce replacement text.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReplaceToStringEvaluatorCore(input, state, evaluator, cancellationToken);
    }

    /// <summary>Attempts to write a replacement result into a caller-provided UTF-8 destination.</summary>
    /// <returns><see cref="OperationStatus.Done"/> on success, or <see cref="OperationStatus.DestinationTooSmall"/> when the destination cannot hold the complete result.</returns>
    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        string replacement,
        Span<byte> destination,
        out int bytesWritten)
    {
        return TryReplace(input, replacement, destination, out bytesWritten, CancellationToken.None);
    }

    private OperationStatus TryReplaceStringCore(
        ReadOnlySpan<byte> input,
        string replacement,
        Span<byte> destination,
        out int bytesWritten,
        CancellationToken cancellationToken)
    {
        var analyzed = GetParsedReplacement(replacement);
        return TryReplaceCore(input, analyzed, replacement, destination, out bytesWritten, cancellationToken);
    }

    /// <summary>Attempts to apply a UTF-8-encoded replacement pattern into a caller-provided UTF-8 destination.</summary>
    /// <returns><see cref="OperationStatus.Done"/> on success, or <see cref="OperationStatus.DestinationTooSmall"/> when the destination cannot hold the complete result.</returns>
    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten)
    {
        return TryReplace(input, replacementPatternUtf8, destination, out bytesWritten, CancellationToken.None);
    }

    private OperationStatus TryReplaceUtf8Core(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten,
        CancellationToken cancellationToken)
    {
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        var validation = Utf8Validation.Validate(input);
        if (ShouldUseKelvinSignFallback(input) && _ownedSliceProgram is null)
        {
            var replaced = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                Encoding.UTF8.GetString(replacementPatternUtf8));
            return TryEncodeUtf8ToDestination(replaced, destination, out bytesWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        if (GetOwnedSliceProgram(input, cancellationToken) is null && !(input.IsEmpty && _ownedSliceProgram is not null) &&
            !(_ownedSliceProgram is { CanMatchEmpty: true } && _preparedRegex.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral) &&
            TryGetDirectLiteralReplacementBytes(replacementPatternUtf8, out var directReplacementBytes))
        {
            return TryReplaceLiteralBytesCore(input, validation, directReplacementBytes, destination, out bytesWritten);
        }

        var replacementText = Encoding.UTF8.GetString(replacementPatternUtf8);
        var replacementPattern = GetParsedReplacement(replacementText);
        return TryReplaceCore(input, replacementPattern, replacementText, destination, out bytesWritten, cancellationToken);
    }

    /// <summary>Attempts to write a replacement result into a caller-provided UTF-8 destination, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, string replacement, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return TryReplaceStringCore(input, replacement, destination, out bytesWritten, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    /// <summary>Attempts to apply a UTF-8-encoded replacement pattern into a caller-provided UTF-8 destination, observing cancellation before matching begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacementPatternUtf8">The replacement pattern encoded as well-formed UTF-8.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="cancellationToken">The token to observe before matching begins.</param>
    /// <exception cref="OperationCanceledException">The token was already canceled.</exception>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            return TryReplaceUtf8Core(input, replacementPatternUtf8, destination, out bytesWritten, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    internal Utf8IsMatchDiagnostics CollectIsMatchDiagnostics(ReadOnlySpan<byte> input)
    {
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = IsMatch(input);
            return new Utf8IsMatchDiagnostics(
                result,
                _preparedRegex.ExecutionKind.ToString(),
                _preparedRegex.SearchPlan.Kind.ToString(),
                _preparedRegex.FallbackVerifier.Mode.ToString(),
                _preparedRegex.FallbackVerifier.RequiresCandidateEndCoverage,
                _preparedRegex.FallbackVerifier.RequiresTrailingAnchorCoverage,
                session.SearchCandidates,
                session.FixedCheckRejects,
                session.VerifierInvocations,
                session.VerifierMatches,
                session.PrefilterWindows,
                session.PrefilterSkippedWindows,
                session.PrefilterPromotedWindows,
                session.PrefilterSkippedBytes,
                session.PrefilterPromotedBytes,
                session.EngineDemotions);
        }
        finally
        {
            session.Complete();
        }
    }

    internal Utf8CountDiagnostics CollectCountDiagnostics(ReadOnlySpan<byte> input)
    {
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = Count(input);
            return new Utf8CountDiagnostics(
                result,
                _preparedRegex.ExecutionKind.ToString(),
                _preparedRegex.SearchPlan.Kind.ToString(),
                _preparedRegex.FallbackVerifier.Mode.ToString(),
                _preparedRegex.FallbackVerifier.RequiresCandidateEndCoverage,
                _preparedRegex.FallbackVerifier.RequiresTrailingAnchorCoverage,
                session.ExecutionRoute.Format(),
                session.SearchCandidates,
                session.FixedCheckRejects,
                session.VerifierInvocations,
                session.VerifierMatches,
                session.PrefilterWindows,
                session.PrefilterSkippedWindows,
                session.PrefilterPromotedWindows,
                session.PrefilterSkippedBytes,
                session.PrefilterPromotedBytes,
                session.EngineDemotions);
        }
        finally
        {
            session.Complete();
        }
    }

    private Utf8ExecutionDeadline CreateExecutionBudget()
    {
        return Utf8ExecutionDeadline.Start(MatchTimeout);
    }

    private RegexMatchTimeoutException CreateMatchTimeoutException(ReadOnlySpan<byte> input) =>
        new(Encoding.UTF8.GetString(input), Pattern, MatchTimeout);

    private static bool TryEncodeUtf8ToDestination(string value, Span<byte> destination, out int bytesWritten)
    {
        var requiredLength = Encoding.UTF8.GetByteCount(value);
        if (requiredLength > destination.Length)
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = Encoding.UTF8.GetBytes(value, destination);
        return true;
    }

    private bool CanUseNativeSplit(Utf8ValidationResult validation)
    {
        // Both ordinary and emitted window selections use the same owned
        // cursor. Admission follows the window program, not the backend kind.
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow)
        {
            return validation.IsAscii;
        }

        // R03: split segments are match-delimited gaps (BCL EnumerateSplits never splices
        // group captures, unlike array Split), so grouped literal shapes split natively on the
        // same group-agnostic cursors. Structural shapes additionally require the same
        // byte-safe program conditions the split cursor factory serves (R05).
        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => true,
            Utf8CompiledEngineKind.LiteralFamily => true,
            Utf8CompiledEngineKind.StructuralLinearAutomaton
                => _preparedRegex.StructuralLinearProgram.DeterministicProgram.HasValue &&
                   (validation.IsAscii || _preparedRegex.StructuralLinearProgram.AllowsUtf8ByteSafe) ||
                   (!validation.IsAscii && _preparedRegex.SimplePatternPlan.IsUtf8ByteSafe),
            _ => false,
        };
    }

    private bool TryGetSmallAsciiLiteralFamilySplitSearch(
        ReadOnlySpan<byte> input,
        out PreparedSmallAsciiLiteralFamilySearch search)
    {
        if (MatchTimeout != Regex.InfiniteMatchTimeout ||
            _preparedRegex.ExecutionPattern.IndexOf('(') >= 0 ||
            _compiledEngineRuntime is not Utf8LiteralFamilyCompiledEngineRuntime literalFamilyRuntime)
        {
            search = default;
            return false;
        }

        return literalFamilyRuntime.Inner.TryGetSmallAsciiLiteralFamilyPrimitive(input, out search);
    }

    private bool UsesRightToLeft()
    {
        return (Options & RegexOptions.RightToLeft) != 0;
    }

    private bool ShouldUseRightToLeftScalarFallback()
    {
        return UsesRightToLeft() &&
            _preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex;
    }

    private bool CanUseWellFormedOnlyValidation()
    {
        return _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals
                or NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                or NativeExecutionKind.AsciiOrderedLiteralWindow or NativeExecutionKind.AsciiStructuralIdentifierFamily ||
            (_compiledEngine.Kind == Utf8CompiledEngineKind.FallbackRegex &&
             !CanGuideFallbackVerification());
    }

    private bool TryUseAsciiInputValidationShortcut(ReadOnlySpan<byte> input)
    {
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or
                                             NativeExecutionKind.ExactUtf8Literal or
                                             NativeExecutionKind.ExactUtf8Literals or
                                             NativeExecutionKind.AsciiLiteralIgnoreCase or
                                             NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals or
                                             NativeExecutionKind.AsciiOrderedLiteralWindow or
                                             NativeExecutionKind.AsciiStructuralIdentifierFamily) &&
            !_preparedRegex.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch)
        {
            return false;
        }

        return Utf8InputAnalyzer.IsAscii(input);
    }

}
