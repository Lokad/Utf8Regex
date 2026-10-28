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
    internal readonly Utf8PackedNibbleLiteralFamilyCountStrategy? _packedNibbleLiteralFamilyCountStrategy;
    private readonly Utf8LiteralFamilyCaptureMap? _literalFamilyCaptureMap;
    private readonly bool _hasCaptures;
    private readonly Utf8GroupedLiteralSpanTable? _groupedLiteralSpanTable;
    private readonly byte[]? _caretAnchoredGroupedLiteralUtf8;
    private readonly Utf8AlternationBranchSpanMap? _alternationBranchSpanMap;
    private readonly Utf8SliceProgram _ownedSliceProgram;
    private readonly Utf8ReplacementPlanCache _replacementCache;

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
    /// <remarks>A finite work, depth, working or result limit selects owned scalar execution. Work covers validation, matching and required result shaping cumulatively; depth bounds live backtracking/continuation frames or pending linear alternatives.
    /// Working bytes cover modeled major active arrays/output controls and old/new overlap; scratch releases synchronously while published cursor maps/tables remain charged for the invocation.
    /// Result bytes cover cumulative modeled major result/staging capacity, including arrays, text, coordinate maps, growth and output controls.
    /// Capacity charges precede acquisition, may conservatively count shared storage and are never refunded; fixed values and borrowed input are excluded.
    /// Replacement parsing and UTF-8 template-key decoding also use the working-byte value as a separate additional-capacity ceiling; ConstructionBytes may precede execution. UTF-8 key decoding needs admission even on cache hits.
    /// Construction/preparation work and retained storage remain separate. Caller callback execution and later standalone result getters are outside this work cap.
    /// Copied value enumerators share their work allowance while keeping independent positions; concurrent advancement is unsupported.</remarks>
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
        var beforeProgram = construction.Budget.ReservedBytes;

        Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout));
        ValidateOptions(options);
        ArgumentNullException.ThrowIfNull(pattern);
        Pattern = pattern;
        Options = options;
        MatchTimeout = matchTimeout;
        ResourcePolicy = resourcePolicy;
        construction.Budget.CheckBytes(ProgramFixedMetadataBytes);
        construction.Budget.ChargeWork(1);
        construction.Budget.ReserveBytes(ProgramFixedMetadataBytes);

        _program = Utf8RegexProgram.Compile(pattern, options, prepareRuntime: !HasFiniteExecutionLimits);
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
        // R02a: fallback-shaped literal/concat/alt/repeat/capture patterns run the owned
        // scalar slice. Instruction-capacity exhaustion fails in owned admission;
        // pattern nesting and resource failure do not select a BCL engine.
        // NonBacktracking regular graphs lower to ordered scalar NFA states,
        // including loops and shared captures. They do not use the backtracker.
        var ownedSliceProgram = preparedRegex.ScalarProgram
            ?? throw new InvalidOperationException("Owned preparation must produce a scalar program.");
        _ownedSliceProgram = ownedSliceProgram;
        _hasCaptures = preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex && !preparedRegex.FallbackDirectFamily.HasValue
            ? ownedSliceProgram.GroupCount != 0 : HasOwnedCaptures(preparedRegex.ExecutionTree);
        // Fixed finite policies/deadlines and captured Unicode atoms use scalar
        // projection. Avoid maps that no public detailed/template route consumes. A missing
        // native tree cannot produce a map either; inactive catalog declarations
        // alone must not fence the literal-family native projection.
        var scalarCaptureProjection = HasFiniteExecutionLimits || matchTimeout != Regex.InfiniteMatchTimeout ||
            _hasCaptures && ownedSliceProgram.HasUnicodePatternAtoms;
        var hasNativeCaptureTree = preparedRegex.ExecutionTree is not null && !UsesRightToLeft();
        _literalFamilyCaptureMap = hasNativeCaptureTree && !scalarCaptureProjection &&
            preparedRegex.ExecutionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals &&
            _groupNumbers.Length > 2
                ? Utf8LiteralFamilyCaptureMap.TryCreate(preparedRegex.ExecutionTree)
                : null;
        // The anchored prefix table also supplies a value-cursor proof before
        // scalar selection. Preserve that dependency and its existing fences.
        _groupedLiteralSpanTable = _hasCaptures && hasNativeCaptureTree &&
            (!scalarCaptureProjection || _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix)
                ? Utf8GroupedLiteralSpanTable.TryCreate(preparedRegex.ExecutionTree, ownedSliceProgram) : null;
        // The existing literal proof respects each anchor's scoped options. Borrow
        // its admitted bytes instead of collecting/copying the execution tree again.
        _caretAnchoredGroupedLiteralUtf8 =
            _groupedLiteralSpanTable is { } spanTable &&
            (options & (RegexOptions.RightToLeft | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)) == 0 &&
            _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } prefix &&
            prefix.Length == spanTable.TotalLengthInUtf16
                ? prefix : null;
        _alternationBranchSpanMap = _hasCaptures && hasNativeCaptureTree && !scalarCaptureProjection
            ? Utf8AlternationBranchSpanMap.TryCreate(preparedRegex.ExecutionTree, ownedSliceProgram) : null;
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
                Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiIpv4Token,
                Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken =>
                    Utf8PrioritizedBooleanFamilyKind.AsciiBoundedDateToken,
                Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail =>
                    Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiLeadingDigitsTail,
                _ => Utf8PrioritizedBooleanFamilyKind.None,
            };
        }

        _replacementCache = Utf8ReplacementPlanCache.Create();
        // Scratch has closed; covered retained capacity and conservative helper
        // charges survive successful children. Explicitly cover the facade and
        // borrowed pattern, and reserve the already-enforced lazy child maximum.
        _cacheProgramBytes = checked(construction.Budget.ReservedBytes - beforeProgram
            + CachedObjectBytes + Utf8ConstructionBudget.StringBytes(Pattern.Length)
            + _replacementCache.MaximumRetainedBytes);
        construction.Complete();
    }

    private bool HasFiniteExecutionLimits => ResourcePolicy.MaxWorkSteps.HasValue || ResourcePolicy.MaxDepth.HasValue || ResourcePolicy.MaxWorkingBytes.HasValue || ResourcePolicy.MaxResultBytes.HasValue;

    private Utf8ExecutionLimits InvocationLimits => new(ResourcePolicy.MaxWorkSteps ?? 0, ResourcePolicy.MaxDepth ?? 0, ResourcePolicy.MaxWorkingBytes ?? 0, ResourcePolicy.MaxResultBytes ?? 0);

    private static Utf8ResourceLimitExceededException CreateExecutionResourceFailure(ref Utf8SliceInvocation invocation)
        => invocation.Budget.CreateResourceFailure();

    private static void ValidateCorePolicy(Utf8ResourcePolicy resourcePolicy, string paramName)
        => resourcePolicy.Validate(paramName);

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

    private Utf8CompiledEngineRuntime _compiledEngineRuntime => _program.CompiledEngineRuntime ??
        throw new InvalidOperationException("A fixed-budget expression has no unmetered runtime.");

    private Utf8EmittedKernelMatcher? _directStructuralFamilyKernelMatcher =>
        _program.DirectStructuralFamilyKernelMatcher;

    private int[] _groupNumbers => _program.CaptureCatalog.GroupNumbers;

    private string[] _groupNames => _program.CaptureCatalog.GroupNames;

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

    private Utf8StructuralVerifierPlan StructuralVerifierPlan => _preparedRegex.StructuralVerifier;

    private AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan => _preparedRegex.StructuralIdentifierFamilyPlan;

    private AsciiSimplePatternPlan SimplePatternPlan => _preparedRegex.SimplePatternPlan;

    private string? FallbackReason => _preparedRegex.FallbackReason;

    private static void ValidateOptions(RegexOptions options)
    {
        // Own the admitted option set; runtime upgrades do not extend the grammar.
        Utf8RegexSyntax.ValidateOptions(options);
    }

    /// <summary>Returns the number assigned to a group name, or <c>-1</c> when the name is undefined.</summary>
    public int GroupNumberFromName(string name)
    {
        if (name is null) throw new ArgumentNullException(nameof(name));
        return _program.CaptureCatalog.GroupNumberFromName(name);
    }

    /// <summary>Returns the name assigned to a group number, or an empty string when the number is undefined.</summary>
    public string GroupNameFromNumber(int i)
    {
        return _program.CaptureCatalog.GroupNameFromNumber(i);
    }

    /// <summary>Returns the group names defined by the expression, including group zero.</summary>
    public string[] GetGroupNames()
    {
        return _program.CaptureCatalog.GroupNames.AsSpan().ToArray();
    }

    /// <summary>Returns the group numbers defined by the expression, including group zero.</summary>
    public int[] GetGroupNumbers()
    {
        return _program.CaptureCatalog.GroupNumbers.AsSpan().ToArray();
    }

    /// <summary>Determines whether the well-formed UTF-8 input contains a match.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input)
    {
        // The entry shortcuts below prove their answer without polling. A finite
        // deadline must start before validation/search under the existing owner.
        if (MatchTimeout != Regex.InfiniteMatchTimeout)
            return IsMatch(input, CancellationToken.None);

        if (!HasFiniteExecutionLimits && _prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiSignedDecimalWhole)
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

        if (!HasFiniteExecutionLimits && _prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AsciiBoundedDateToken &&
            input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0)
        {
            return Utf8AsciiBoundedDateTokenExecutor.TryFindAsciiBoundedDateToken(
                input,
                0,
                _fallbackDirectFamily,
                out _,
                out _);
        }

        if (!HasFiniteExecutionLimits && _prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiOptionalFieldWhole)
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
        if (token.CanBeCanceled || HasFiniteExecutionLimits) return _ownedSliceProgram;
        return GetOwnedSliceProgram(input);
    }

    private Utf8SliceProgram? GetOwnedSliceProgram(ReadOnlySpan<byte> input)
    {
        // Qualified native invocation routes run before this selector. Other
        // shortcuts must not bypass a finite deadline or restart it after work.
        if (HasFiniteExecutionLimits || MatchTimeout != Regex.InfiniteMatchTimeout) return _ownedSliceProgram;
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex) return _ownedSliceProgram;
        // ASCII folded literals cannot start or end inside a UTF-8 scalar.
        // Their non-ASCII fold partner (Kelvin sign) is checked below; unrelated
        // Unicode elsewhere in the subject does not require scalar discovery.
        var scalarAlignedLiteral = _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals ||
            _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals &&
            (Options & RegexOptions.CultureInvariant) != 0;
        if (_ownedSliceProgram.CanMatchEmpty && _preparedRegex.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral ||
             _ownedSliceProgram.UsesInterpretedLazyLoopSemantics ||
             _ownedSliceProgram.HasSharedCaptureDeclarations ||
             _ownedSliceProgram.HasExplicitCaptureNumbers ||
             _ownedSliceProgram.HasBalancing ||
             _ownedSliceProgram.HasContiguousAnchor ||
             // A shorter structural prefix can fail its suffix where a longer
             // branch at the same start succeeds. The owned VM retries it.
             _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
                _preparedRegex.StructuralIdentifierFamilyPlan.HasOverlappingPrefixes ||
             // Anchored simple plans without a dedicated native matcher use
             // the cached scalar program. Keep qualified native and line paths.
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
             (!scalarAlignedLiteral ||
                 _preparedRegex.SearchPlan.HasBoundaryRequirements) &&
                !Utf8InputAnalyzer.IsAscii(input))
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
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            var validation = Utf8Validation.Validate(input, ref invocation);
            return OwnedSliceMatchDetailedFromByte(input, program, fromByte, validation, null, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
    }

    private Utf8MatchContext OwnedSliceMatchDetailedFromByte(ReadOnlySpan<byte> input,
        Utf8SliceProgram program, int fromByte, Utf8ValidationResult validation, Utf8BoundaryMap? admittedMap, scoped ref Utf8SliceInvocation invocation)
    {
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
                throw new InvalidOperationException("Owned slice execution breached the invocation budget.");
            invocation.Budget.ThrowIfCancellationRequested();
            if (!outcome.Found)
                return Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
            // The current context stores derived UTF-16 capture coordinates.
            // Its admitted map is result interoperability, never discovery state.
            var map = admittedMap ?? Utf8BoundaryMap.Create(input, validation, ref invocation, out mapReservation);
            projection = Utf8SliceCaptureProjection.Create(outcome, map, ref invocation);
            invocation.Budget.ThrowIfCancellationRequested();
            return projection.CreateContext(input, map, _program.CaptureCatalog);
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
        if (_prioritizedBooleanFamilyKind == Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiSignedDecimalWhole &&
            (HasFiniteExecutionLimits || cancellationToken.CanBeCanceled || MatchTimeout != Regex.InfiniteMatchTimeout))
        {
            var decimalInvocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken);
            try
            {
                var decimalValidation = Utf8Validation.Validate(input, ref decimalInvocation);
                if (!decimalValidation.IsAscii)
                    return OwnedSliceIsMatchFromByte(input, _ownedSliceProgram, 0, ref decimalInvocation);
                var breach = Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input,
                    ref decimalInvocation, out var matched);
                if (breach != Utf8ExecutionLimitKind.None)
                    throw CreateExecutionResourceFailure(ref decimalInvocation);
                return matched;
            }
            catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException &&
                decimalInvocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
            {
                throw CreateExecutionResourceFailure(ref decimalInvocation);
            }
        }
        if (HasFiniteExecutionLimits) return OwnedSliceIsMatch(input, _ownedSliceProgram, cancellationToken);
        if (TryStartNativeStructuralInvocation(input, cancellationToken, false, out var structuralInvocation, out var structuralValidation))
        {
            // Mixed subjects and overlapping prefixes use shared scalar
            // verification under the same invocation.
            if (!structuralValidation.IsAscii || _preparedRegex.StructuralIdentifierFamilyPlan.HasOverlappingPrefixes ||
                _preparedRegex.SearchPlan.HasAlternateLiteralPrefixOverlap ||
                _preparedRegex.SearchPlan.HasAlternateLiteralProperStartOverlap)
                return OwnedSliceIsMatchFromByte(input, _ownedSliceProgram,
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
            return OwnedSliceIsMatch(input, _ownedSliceProgram, cancellationToken);
        }

        if (ShouldUseRightToLeftScalarFallback())
        {
            return OwnedSliceIsMatch(input, _ownedSliceProgram, cancellationToken);
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

        if (RequiresScalarValueExecution())
        {
            return OwnedSliceIsMatch(input, _ownedSliceProgram, cancellationToken);
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
            return OwnedSliceIsMatch(input, _ownedSliceProgram, cancellationToken);
        }

        if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
        {
            return false;
        }

        var budget = CreateExecutionBudget();

        return IsMatchViaCompiledEngine(input, validation, budget);
    }

    /// <summary>Determines whether the well-formed UTF-8 input contains a match, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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

    /// <summary>Determines whether a match starts at or after the specified UTF-16 code-unit offset, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken);
        ulong mapReservation = 0;
        try
        {
            var map = PrepareOffsetInput(input, utf16Offset, ref invocation,
                out var validation, out var startByte, out mapReservation);
            var controlled = cancellationToken.CanBeCanceled || MatchTimeout != Regex.InfiniteMatchTimeout;
            if (controlled && TryMatchControlledAnchoredOffset(input, validation, startByte, utf16Offset, ref invocation, out var anchored))
                return anchored.Success;
            if (!controlled && !HasFiniteExecutionLimits && !UsesRightToLeft() && _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
                return utf16Offset == 0 &&
                    Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out _);
            }

            // Anchored suffixes match at most once at a fixed end position: a start past
            // it misses definitively, otherwise the direct hit itself decides.
            if (!controlled && !HasFiniteExecutionLimits && !UsesRightToLeft() && Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
                _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                return TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixDirect) &&
                    suffixDirect.Success &&
                    utf16Offset <= suffixDirect.IndexInUtf16;
            }

            // Admitted UTF-16 starts use the cached owned scalar program for ASCII
            // and Unicode atoms alike. Ordinary byte-oriented discovery retains
            // its native choices through GetOwnedSliceProgram.
            return OwnedSliceIsMatchFromByte(input, _ownedSliceProgram,
                startByte, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
        finally { invocation.Ledger.Release(mapReservation); }
    }

    private bool OwnedSliceIsMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
            return OwnedSliceIsMatchFromByte(input, program, startByte, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
    }

    private bool OwnedSliceIsMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, ref Utf8SliceInvocation invocation)
    {
        invocation.AnchorOriginByte = startByte;
        if (HasFiniteExecutionLimits && CanUseNativeLiteralSearch(program))
        {
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan,
                input, startByte, input.Length, false, ref invocation, out _, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Native literal existence search breached the invocation budget.");
            return found;
        }
        // Dot-star can consume nothing: existence is exactly the complete
        // literal suffix's existence. A necessary literal of that full length
        // is the suffix; a fragment from separate captures is insufficient.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral &&
            ReferenceEquals(program, _ownedSliceProgram) &&
            !program.RightToLeft && !program.IgnoreCase && !program.IsNonBacktracking &&
            _preparedRegex.SearchPlan.RequiredPrefilterSearcher is { Kind: PreparedSearcherKind.ExactLiteral } required &&
            required.LiteralSearch.Length == _fallbackDirectFamily.LiteralUtf8.Length)
        {
            var breach = required.TryFindMatch(input, startByte, input.Length, false,
                ref invocation, out _, out var found);
            if (breach == Utf8ExecutionLimitKind.None) breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None) throw CreateExecutionResourceFailure(ref invocation);
            invocation.Budget.ThrowIfCancellationRequested();
            return found;
        }
        if (RejectMissingRequiredLiteral(input, program, ref invocation)) return false;
        var slots = new Utf8SliceSlots([], []);
        try
        {
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, startByte, slots.Starts, slots.Ends, false, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Owned slice execution breached the invocation budget.");
            invocation.Budget.ThrowIfCancellationRequested();
            return outcome.Found;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    private bool RejectMissingRequiredLiteral(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        ref Utf8SliceInvocation invocation)
    {
        if (!ReferenceEquals(program, _ownedSliceProgram) ||
            program.RightToLeft || program.HasContiguousAnchor ||
            // This feature includes internal multiline ^ assertions. The
            // quoted-relation proof starts at an operand, not at that assertion.
            _preparedRegex.Features.HasAbsoluteStartAnchor &&
                !_preparedRegex.StructuralLinearProgram.QuotedRelationPlan.HasValue ||
            _preparedRegex.SearchPlan.CanGuideFallbackStarts) return false;
        var prefilter = _preparedRegex.SearchPlan.RequiredPrefilterSearcher;
        if (prefilter.Kind is not (PreparedSearcherKind.ExactLiteral or PreparedSearcherKind.MultiLiteral)) return false;
        // The existing planner proves this literal is necessary. Search the
        // complete already-validated subject, including lookbehind context;
        // presence proves nothing about a match and retains normal verification.
        var breach = prefilter.TryFindMatch(input, 0, input.Length, false, ref invocation, out _, out var found);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Required literal screening breached the invocation budget.");
        return !found;
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

    /// <summary>Counts non-overlapping matches in the well-formed UTF-8 input, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
        if (HasFiniteExecutionLimits) return OwnedSliceCount(input, _ownedSliceProgram, cancellationToken);
        // This prepared Unicode strategy cannot use the ASCII structural or
        // metered literal routes. Dispatch it before those ineligible probes;
        // finite policies, live tokens and deadlines keep their owned authority.
        if (_invariantCyrillicLiteralCountStrategy is { } invariantCyrillicLiteralCountStrategy &&
            !cancellationToken.CanBeCanceled && MatchTimeout == Regex.InfiniteMatchTimeout)
        {
            return invariantCyrillicLiteralCountStrategy.Count(input);
        }
        if (TryStartNativeStructuralInvocation(input, cancellationToken, false, out var structuralInvocation, out var structuralValidation))
        {
            if (!structuralValidation.IsAscii || _preparedRegex.StructuralIdentifierFamilyPlan.HasOverlappingPrefixes ||
                _preparedRegex.SearchPlan.HasAlternateLiteralPrefixOverlap ||
                _preparedRegex.SearchPlan.HasAlternateLiteralProperStartOverlap)
                return OwnedSliceCountFromByte(input, _ownedSliceProgram,
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
        // This existing ordered grammar proof admits only ASCII scalars. Its
        // checked byte kernel is therefore valid within a fully validated UTF-8
        // subject too; wider scalars cannot contain an ASCII candidate byte.
        // Controlled calls and direction/NB semantics retain their owned routes.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount &&
            !_ownedSliceProgram.RightToLeft && !_ownedSliceProgram.IsNonBacktracking &&
            !cancellationToken.CanBeCanceled && MatchTimeout == Regex.InfiniteMatchTimeout &&
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is null)
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAsciiDottedDecimalQuad);
            return Utf8AsciiIpv4TokenExecutor.CountAsciiIpv4Tokens(input);
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

                var windowInvocation = new Utf8SliceInvocation(Utf8ExecutionLimits.Unlimited, orderedWindowBudget, cancellationToken);
                _ = Utf8Validation.Validate(input, ref windowInvocation);
                return OwnedSliceCountFromByte(input, _ownedSliceProgram, 0, ref windowInvocation);
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
                return OwnedSliceCount(input, _ownedSliceProgram, cancellationToken);
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
            return OwnedSliceCount(input, _ownedSliceProgram, cancellationToken);
        }

        if (ShouldUseRightToLeftScalarFallback())
        {
            return OwnedSliceCount(input, _ownedSliceProgram, cancellationToken);
        }

        // R05: caret-anchored grouped literals count owned by prefix compare (at
        // most one match at offset 0); folding and RTL use the routes above.
        if (_caretAnchoredGroupedLiteralUtf8 is { Length: > 0 } caretCountPrefix)
        {
            return input.Length >= caretCountPrefix.Length &&
                input.Slice(0, caretCountPrefix.Length).SequenceEqual(caretCountPrefix) ? 1 : 0;
        }

        if (RequiresScalarValueExecution())
        {
            return OwnedSliceCount(input, _ownedSliceProgram, cancellationToken);
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
                return OwnedSliceCount(input, _ownedSliceProgram, cancellationToken);
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
            if (_compiledEngineRuntime.SupportsThrowIfInvalidOnlyCount)
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
            return OwnedSliceCount(input, _ownedSliceProgram, cancellationToken);
        }

        if (!ShouldSkipRequiredPrefilterForCount() && RejectsByRequiredPrefilter(input))
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

    /// <summary>Counts non-overlapping matches starting at the specified UTF-16 code-unit offset, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken);
        ulong mapReservation = 0;
        try
        {
            var map = PrepareOffsetInput(input, utf16Offset, ref invocation,
                out var validation, out var startByte, out mapReservation);
            var controlled = cancellationToken.CanBeCanceled || MatchTimeout != Regex.InfiniteMatchTimeout;
            if (controlled && TryMatchControlledAnchoredOffset(input, validation, startByte, utf16Offset, ref invocation, out var anchored))
                return anchored.Success ? 1 : 0;
            // At most one anchored suffix match exists past the start: count it directly.
            if (!controlled && !HasFiniteExecutionLimits && !UsesRightToLeft() && Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
                _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                return TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixCount) &&
                    suffixCount.Success &&
                    utf16Offset <= suffixCount.IndexInUtf16
                    ? 1
                    : 0;
            }

            return OwnedSliceCountFromByte(input, _ownedSliceProgram,
                startByte, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
        finally { invocation.Ledger.Release(mapReservation); }
    }

    /// <summary>Owned scalar count from an admitted byte boundary.</summary>
    private int OwnedSliceCountFromByte(
        ReadOnlySpan<byte> input,
        Utf8SliceProgram program,
        int startByte,
        CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            if (HasFiniteExecutionLimits && CanUseNativeLiteralSearch(program) ||
                CanUseNativeScalarTokenCount(program, startByte))
            {
                // These complete counters consume validated bytes, not coordinate metadata.
                // Keep structural validation and the existing counting kernel intact.
                Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
                return OwnedSliceCountFromByte(input, program, startByte, ref invocation);
            }
            if (HasFiniteExecutionLimits && startByte == 0 && ReferenceEquals(program, _ownedSliceProgram) &&
                !program.RightToLeft && !program.IgnoreCase && !program.CanMatchEmpty &&
                !program.HasBalancing && !program.HasSharedCaptureDeclarations && !program.HasExplicitCaptureNumbers &&
                !program.HasContiguousAnchor && !program.SawLookaround && !program.HasConditionalTest &&
                !_requiresKelvinSignFallback && (Options & RegexOptions.NonBacktracking) == 0 &&
                _preparedRegex.ExecutionKind is NativeExecutionKind.AsciiSimplePattern or NativeExecutionKind.AsciiStructuralIdentifierFamily)
            {
                var validation = Utf8Validation.Validate(input, ref invocation);
                if (validation.IsAscii && _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
                    _preparedRegex.StructuralSearchPlan.HasValue && _preparedRegex.StructuralVerifier.AsciiProgram.LinearProgram.HasValue &&
                    !_preparedRegex.StructuralIdentifierFamilyPlan.HasOverlappingPrefixes &&
                    !_preparedRegex.SearchPlan.HasAlternateLiteralPrefixOverlap &&
                    !_preparedRegex.SearchPlan.HasAlternateLiteralProperStartOverlap)
                {
                    var structuralBreach = Utf8AsciiStructuralIdentifierFamilyExecutor.TryCount(
                        _preparedRegex.StructuralSearchPlan, _preparedRegex.StructuralVerifier.AsciiProgram,
                        input, ref invocation, out var structuralCount);
                    if (structuralBreach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Native structural count breached the invocation budget.");
                    return structuralCount;
                }
                if (validation.IsAscii && _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
                    !_preparedRegex.SearchPlan.HasBoundaryRequirements &&
                    Utf8AsciiInstructionLinearExecutor.SupportsInput(_preparedRegex.StructuralLinearProgram, validation) &&
                    Utf8AsciiDeterministicFixedWidthCountExecutor.TryCountValidatedAscii(
                        _preparedRegex.StructuralLinearProgram, input, ref invocation, out var nativeCount, out var breach))
                {
                    if (breach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Native fixed-width count breached the invocation budget.");
                    return nativeCount;
                }
                if (validation.IsAscii && _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
                    _preparedRegex.SimplePatternPlan is { Branches.Length: > 0, MinLength: > 0 } simple)
                {
                    var branchBreach = Utf8ExecutionInterpreter.TryCountSimplePattern(input,
                        _preparedRegex.SearchPlan, simple, ref invocation,
                        out var branchCount);
                    if (branchBreach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Native simple count breached the invocation budget.");
                    return branchCount;
                }
            }
            else
            {
                // The generic scalar counter consumes bytes only. Derive
                // coordinate metadata solely for the structural lanes above.
                Utf8Validation.ThrowIfInvalidOnly(input, ref invocation);
            }
            return OwnedSliceCountFromByte(input, program, startByte, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
    }

    private bool CanUseNativeScalarTokenCount(Utf8SliceProgram program, int startByte)
        => startByte == 0 && ReferenceEquals(program, _ownedSliceProgram) && !_hasCaptures &&
            !program.RightToLeft && !program.IgnoreCase &&
            (Options & (RegexOptions.ECMAScript | RegexOptions.NonBacktracking)) == 0 &&
            _fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount or Utf8FallbackDirectFamilyKind.Utf8UriToken;

    private int OwnedSliceCountFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, ref Utf8SliceInvocation invocation)
    {
        if (CanUseNativeScalarTokenCount(program, startByte))
        {
            if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.Utf8UriToken)
                return Utf8UriTokenCountExecutor.CountWellFormed(input, ref invocation);
            var breach = Utf8WordDelimitedTokenExecutor.TryCountWellFormed(input, ref invocation, out var count);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Word-delimited count breached the invocation budget.");
            return count;
        }
        // A pure literal/family Count needs no capture slots or continuation frames.
        // Borrow the validated invocation instead of dropping finite callers
        // into the VM solely because they selected an enforceable policy.
        if (HasFiniteExecutionLimits && CanUseNativeLiteralSearch(program))
        {
            var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(
                _preparedRegex.SearchPlan, input[startByte..], false, ref invocation, out var count);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Native literal count breached the invocation budget.");
            return count;
        }
        // Borrow the same validated invocation for invariant folded literals.
        // Their only non-ASCII alias is Kelvin; keep that case on the scalar VM.
        if (startByte == 0 && ReferenceEquals(program, _ownedSliceProgram) &&
            _preparedRegex.ExecutionKind is NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals &&
            (Options & RegexOptions.CultureInvariant) != 0 && !_hasCaptures &&
            !program.RightToLeft && !program.CanMatchEmpty && !program.HasBalancing &&
            !program.HasSharedCaptureDeclarations && !program.HasExplicitCaptureNumbers &&
            !program.UsesInterpretedLazyLoopSemantics && !program.HasContiguousAnchor &&
            !program.SawLookaround && !program.HasConditionalTest &&
            (Options & RegexOptions.NonBacktracking) == 0 &&
            !_preparedRegex.SearchPlan.HasBoundaryRequirements && !_preparedRegex.SearchPlan.HasTrailingLiteralRequirement)
        {
            var containsKelvin = false;
            if (_requiresKelvinSignFallback)
            {
                for (var offset = 0; offset < input.Length;)
                {
                    var scanBreach = invocation.TryChargeInputStep();
                    if (scanBreach != Utf8ExecutionLimitKind.None)
                        throw new InvalidOperationException("Folded literal screening exceeded the invocation budget.");
                    var length = Math.Min(2048, input.Length - offset);
                    // Two lookahead bytes preserve a three-byte scalar crossing
                    // the admitted window. Every search remains bounded.
                    if (ContainsKelvinSign(input.Slice(offset, Math.Min(length + 2, input.Length - offset))))
                    {
                        containsKelvin = true;
                        break;
                    }
                    offset += length;
                }
            }
            if (!containsKelvin)
            {
                var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(
                    _preparedRegex.SearchPlan, input, false, ref invocation, out var count);
                if (breach != Utf8ExecutionLimitKind.None)
                    throw new InvalidOperationException("Folded literal count exceeded the invocation budget.");
                return count;
            }
        }
        if (RejectMissingRequiredLiteral(input, program, ref invocation)) return 0;
        var slots = new Utf8SliceSlots([], []);
        try
        {
            // Count publishes no captures. Reuse the existing whole-tree facts;
            // unknown/alternate programs and any capture-sensitive shape retain
            // their slots. Other operations continue to request all values.
            var captureCount = program.GroupCount;
            if (ReferenceEquals(program, _ownedSliceProgram) &&
                _preparedRegex.Features is { CaptureCount: > 0, HasBackreferences: false, HasConditionals: false, HasLookarounds: false } &&
                !program.IsNonBacktracking && !program.HasBalancing &&
                !program.HasSharedCaptureDeclarations && !program.HasExplicitCaptureNumbers &&
                !program.HasConditionalTest && !program.SawLookaround && !program.SawCapturingLookbehind)
                captureCount = 0;
            slots = Utf8SliceRunner.RentSlots(captureCount, ref invocation);
            var outcome = Utf8SliceRunner.CountFromBytes(program, input, startByte, slots.Starts, slots.Ends,
                ref invocation, _preparedRegex.SearchPlan);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                throw new InvalidOperationException("Owned slice execution breached the invocation budget.");
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

    /// <summary>Finds the first match and returns its UTF-16 and, when scalar-aligned, UTF-8 byte coordinates, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
        if (HasFiniteExecutionLimits) return OwnedSliceMatch(input, _ownedSliceProgram, cancellationToken);
        if (TryStartNativeStructuralInvocation(input, cancellationToken, false, out var structuralInvocation, out var structuralValidation))
        {
            if (!structuralValidation.IsAscii || _preparedRegex.StructuralIdentifierFamilyPlan.HasOverlappingPrefixes ||
                _preparedRegex.SearchPlan.HasAlternateLiteralPrefixOverlap ||
                _preparedRegex.SearchPlan.HasAlternateLiteralProperStartOverlap)
                return OwnedSliceMatchFromByte(input, _ownedSliceProgram,
                    0, structuralValidation, ref structuralInvocation);
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
            return MatchScalarProgram(input, cancellationToken);
        }

        if (ShouldUseRightToLeftScalarFallback())
        {
            return MatchScalarProgram(input, cancellationToken);
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
        // simple-pattern plan is UTF-8 byte-safe: a miss must not
        // shortcut Unicode verification. Those inputs fall through to full
        // validation and the compiled engine, which selects the semantic fallback.
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

    /// <summary>Finds the first match at or after the specified UTF-16 code-unit offset, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken);
        ulong mapReservation = 0;
        try
        {
            var map = PrepareOffsetInput(input, utf16Offset, ref invocation,
                out var validation, out var startByte, out mapReservation);
            var controlled = cancellationToken.CanBeCanceled || MatchTimeout != Regex.InfiniteMatchTimeout;
            if (controlled && TryMatchControlledAnchoredOffset(input, validation, startByte, utf16Offset, ref invocation, out var anchored))
                return anchored;
            if (!controlled && !HasFiniteExecutionLimits && !UsesRightToLeft() && _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
                _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
                if (utf16Offset == 0 &&
                    Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredOffsetMatchLength))
                {
                    return new Utf8ValueMatch(true, true, 0, anchoredOffsetMatchLength, 0, anchoredOffsetMatchLength);
                }

                return Utf8ValueMatch.NoMatch;
            }

            // Folded anchored prefixes serve the same single anchored hit with per-byte
            // invariant folding; nonzero starts and Kelvin/Turkic divergence fall through.
            if (!controlled && !HasFiniteExecutionLimits && !UsesRightToLeft() && _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix &&
                _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
            {
                if (!ShouldUseKelvinSignFallback(input))
                {
                    Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
                    if (utf16Offset == 0 &&
                        Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var foldedOffsetMatchLength))
                    {
                        return new Utf8ValueMatch(true, true, 0, foldedOffsetMatchLength, 0, foldedOffsetMatchLength);
                    }

                    return Utf8ValueMatch.NoMatch;
                }
            }

            if (!controlled && !HasFiniteExecutionLimits && !UsesRightToLeft() && Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
                _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixDirect) &&
                    suffixDirect.Success &&
                    utf16Offset <= suffixDirect.IndexInUtf16)
                {
                    return suffixDirect;
                }

                return Utf8ValueMatch.NoMatch;
            }

            // Folded anchored suffixes serve the same single tail hit with per-byte invariant
            // folding through the suffix kernel; non-native folds use scalar execution.
            if (!controlled && !HasFiniteExecutionLimits && !UsesRightToLeft() && (_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd) &&
                _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
            {
                if (!ShouldUseKelvinSignFallback(input))
                {
                    Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                    if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var foldedSuffixDirect) &&
                        foldedSuffixDirect.Success &&
                        utf16Offset <= foldedSuffixDirect.IndexInUtf16)
                    {
                        return foldedSuffixDirect;
                    }

                    return Utf8ValueMatch.NoMatch;
                }
            }

            return OwnedSliceMatchFromByte(input, _ownedSliceProgram,
                startByte, validation, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
        finally { invocation.Ledger.Release(mapReservation); }
    }

    private Utf8ValueMatch OwnedSliceMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            var validation = Utf8Validation.Validate(input, ref invocation);
            return OwnedSliceMatchFromByte(input, program, startByte, validation, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
    }

    private Utf8ValueMatch OwnedSliceMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        int startByte, Utf8ValidationResult validation, ref Utf8SliceInvocation invocation)
    {
        invocation.AnchorOriginByte = startByte;
        if (HasFiniteExecutionLimits && CanUseNativeLiteralSearch(program))
        {
            // Value Match needs only the selected range. Borrow Count's proven
            // literal discovery and the existing metered coordinate projection;
            // detailed captures still use their own scalar VM path.
            var plan = _preparedRegex.SearchPlan;
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(plan, input, startByte,
                input.Length, false, ref invocation, out var selected, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Native literal match breached the invocation budget.");
            return found ? Utf8ProjectionExecutor.ProjectLiteralFamilyMatch(plan.ProjectionPlan, input,
                plan.AlternateLiteralUtf16Lengths, 0, 0, selected, ref invocation, out _, out _) : Utf8ValueMatch.NoMatch;
        }
        var slots = new Utf8SliceSlots([], []);
        try
        {
            slots = Utf8SliceRunner.RentSlots(program.GroupCount, ref invocation);
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, startByte, slots.Starts, slots.Ends, false, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Owned slice execution breached the invocation budget.");
            invocation.Budget.ThrowIfCancellationRequested();
            if (!outcome.Found) return Utf8ValueMatch.NoMatch;
            var length = outcome.EndByte - outcome.StartByte;
            // Reuse complete-input facts for derived coordinates. Diagnostic
            // callers may supply a sentinel instead of complete validation.
            var completeValidation = validation.ByteLength == input.Length;
            var ascii = completeValidation && validation.IsAscii;
            var utf16Length = ascii ? length
                : completeValidation && outcome.StartByte == 0 && length == input.Length
                    ? validation.Utf16Length
                    : Utf8Validation.Validate(input.Slice(outcome.StartByte, length), ref invocation).Utf16Length;
            return Utf8ProjectionExecutor.ProjectMatch(new Utf8ProjectionPlan(
                    ascii ? Utf8ProjectionKind.ByteOnly : Utf8ProjectionKind.Utf16Incremental),
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
        // Anchored ASCII literal prefixes shape directly from the prefix compare:
        // byte-exact on every input, so detailed matches never construct the BCL engine.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
            _groupNumbers.Length <= 1)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredDetailedLength))
            {
                return new Utf8MatchContext(input, analysis.BoundaryMap, _program.CaptureCatalog, 0, anchoredDetailedLength);
            }

            return Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
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
                return new Utf8MatchContext(input, analysis.BoundaryMap, _program.CaptureCatalog, suffixDetailed.IndexInUtf16, suffixDetailed.LengthInUtf16);
            }

            return Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
        }

        if (TryGetOwnedDetailedGroupCount(out var ownedGroupCount))
        {
            // Discovery uses the shared value path; shaping projects the
            // validated subject with the owned capture catalog.
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
                return Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
            }

            return ownedGroupCount > 1
                ? new Utf8MatchContext(input, analysis.BoundaryMap, _program.CaptureCatalog, first.IndexInUtf16, first.LengthInUtf16, ownedGroupCount)
                : new Utf8MatchContext(input, analysis.BoundaryMap, _program.CaptureCatalog, first.IndexInUtf16, first.LengthInUtf16);
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
                return Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
            }

            if (TryShapeGroupedLiteralDetailedMatch(input, analysis.BoundaryMap, groupedFirst, out var groupedDetailed))
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
                return Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
            }

            if (TryShapeAlternationBranchDetailedMatch(input, analysis.BoundaryMap, alternationFirst, out var alternationDetailed))
            {
                return alternationDetailed;
            }
        }


        if (_literalFamilyCaptureMap is { } branchMap &&
            TryMatchDetailedBranchedLiteralFamily(input, ref analysis, branchMap, 0, out var branched))
        {
            return branched;
        }

        // R05: groupless shapes shape Detailed directly from the shared discovery
        // hit (group 0 is the whole span). Discovery uses the same native/scalar
        // routing as the value API; the remaining captures use the scalar VM.
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
                return Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
            }

            return new Utf8MatchContext(input, analysis.BoundaryMap, _program.CaptureCatalog, grouplessFirst.IndexInUtf16, grouplessFirst.LengthInUtf16);
        }

        // Native discovery may lack complete capture history. Borrow the same
        // admitted scalar program for the remaining detailed projection.
        return OwnedSliceMatchDetailed(input, _ownedSliceProgram, cancellationToken);
    }

    /// <summary>Finds the first match and exposes its groups and captures over the original UTF-8 input, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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

    /// <summary>Finds the first detailed match at or after the specified UTF-16 code-unit offset, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, cancellationToken);
        ulong mapReservation = 0;
        try
        {
            var map = PrepareOffsetInput(input, utf16Offset, ref invocation,
                out var validation, out var startByte, out mapReservation);
            var controlled = cancellationToken.CanBeCanceled || MatchTimeout != Regex.InfiniteMatchTimeout;
            if (controlled && _groupNumbers.Length <= 1 &&
                TryMatchControlledAnchoredOffset(input, validation, startByte, utf16Offset, ref invocation, out var anchored))
                return anchored.Success
                    ? new Utf8MatchContext(input, map, _program.CaptureCatalog, anchored.IndexInUtf16, anchored.LengthInUtf16)
                    : Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
            // Anchored prefixes anchor to offset 0: a nonzero start is a definitive miss,
            // while offset 0 reuses the byte-exact prefix compare without BCL construction.
            if (!controlled && !HasFiniteExecutionLimits && !UsesRightToLeft() && _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
                _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
                _groupNumbers.Length <= 1)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
                if (utf16Offset == 0 &&
                    Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredOffsetLength))
                {
                    return new Utf8MatchContext(input, map, _program.CaptureCatalog, 0, anchoredOffsetLength);
                }

                return Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
            }

            if (!controlled && !HasFiniteExecutionLimits && !UsesRightToLeft() && Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
                _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } &&
                _groupNumbers.Length <= 1)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                if (TryMatchDirectFallbackFamilyWithoutValidation(input, out var suffixOffsetDetailed) &&
                    suffixOffsetDetailed.Success &&
                    utf16Offset <= suffixOffsetDetailed.IndexInUtf16)
                {
                    return new Utf8MatchContext(input, map, _program.CaptureCatalog, suffixOffsetDetailed.IndexInUtf16, suffixOffsetDetailed.LengthInUtf16);
                }

                return Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
            }

            return OwnedSliceMatchDetailedFromByte(input, _ownedSliceProgram,
                startByte, validation, map, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
        finally { invocation.Ledger.Release(mapReservation); }
    }

    private bool TryGetOwnedDetailedGroupCount(out int groupCount)
    {
        // R03: groupless shapes (literal or simple) and single groups wrapping a whole
        // literal family discover through the shared value path and shape from owned
        // spans. Other capture histories use the cached scalar program.
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
        Utf8BoundaryMap boundaryMap,
        Utf8ValueMatch hit,
        out Utf8MatchContext detailed)
    {
        // R05: projects every group from fixed literal-relative spans onto the owned
        // discovery hit. The hit length selects the present or absent projection for
        // optional shapes; any other disagreement or out-of-range capture number
        // declines to scalar capture discovery instead of shaping blindly.
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

        detailed = new Utf8MatchContext(input, boundaryMap, _program.CaptureCatalog, hit.IndexInUtf16, hit.LengthInUtf16, starts, lengths);
        return true;
    }


    private bool TryShapeAlternationBranchDetailedMatch(
        ReadOnlySpan<byte> input,
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

        var hitSpan = input.Slice(hit.IndexInBytes, hit.LengthInBytes);
        var selected = -1;
        for (var branch = 0; branch < map.BranchExpansions.Length; branch++)
        {
            var expansion = map.BranchExpansions[branch];
            if (expansion.Length != hit.LengthInUtf16 || !MatchesScalarLiteralExpansion(hitSpan, expansion.AsSpan()))
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

        detailed = new Utf8MatchContext(input, boundaryMap, _program.CaptureCatalog, hit.IndexInUtf16, hit.LengthInUtf16, starts, lengths);
        return true;
    }

    // Compare a native discovery hit with one pattern expansion without decoding the subject.
    private static bool MatchesScalarLiteralExpansion(ReadOnlySpan<byte> input, ReadOnlySpan<char> expansion)
    {
        while (!input.IsEmpty && !expansion.IsEmpty)
        {
            if (input[0] < 0x80 && expansion[0] < 0x80)
            {
                if (input[0] != expansion[0]) return false;
                input = input[1..];
                expansion = expansion[1..];
                continue;
            }
            if (Rune.DecodeFromUtf8(input, out var subjectScalar, out var bytes) != OperationStatus.Done ||
                Rune.DecodeFromUtf16(expansion, out var patternScalar, out var units) != OperationStatus.Done ||
                subjectScalar != patternScalar) return false;
            input = input[bytes..];
            expansion = expansion[units..];
        }
        return input.IsEmpty && expansion.IsEmpty;
    }

    // Construction-cached capture predicate; a missing tree conservatively reports captures.
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

    // Returns true when native branch discovery owns the result; false selects scalar discovery.
    private bool TryMatchDetailedBranchedLiteralFamily(
        ReadOnlySpan<byte> input,
        scoped ref Utf8ValidatedInput analysis,
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

                detailed = CreateBranchedDetailedMatch(input, analysis.BoundaryMap, map, branch, candidate.IndexInUtf16, candidate.LengthInUtf16);
                return true;
            }
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }

        detailed = Utf8MatchContext.Unsuccessful(input, _program.CaptureCatalog);
        return true;
    }

    // Shapes one owned detailed view from a mapped branch hit shared by detailed matching
    // and its owned group projection.
    private Utf8MatchContext CreateBranchedDetailedMatch(
        ReadOnlySpan<byte> input,
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

        return new Utf8MatchContext(input, boundaryMap, _program.CaptureCatalog, indexInUtf16, lengthInUtf16, starts, lengths);
    }

    /// <summary>Enumerates non-overlapping matches in the well-formed UTF-8 input.</summary>
    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input)
    {
        if (HasFiniteExecutionLimits)
            return OwnedSliceEnumerateMatches(input, _ownedSliceProgram, CancellationToken.None).WithTimeoutMapping(input, Pattern, MatchTimeout);
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
        // scalar execution below; all other inputs stay direct.
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
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            var validation = Utf8Validation.Validate(input, ref invocation);
            // Complete ASCII branches use the same controlled finder as finite
            // Count, without acquiring scalar attempt scratch for every value.
            ref readonly var simple = ref _preparedRegex.OperationMatchCursorPlans.SimplePatternPlan;
            var nativeBranch = HasFiniteExecutionLimits && ReferenceEquals(program, _ownedSliceProgram) &&
                validation.IsAscii && _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
                simple.Branches is { Length: > 0 } && simple.MinLength > 0 && !simple.IgnoreCase &&
                !_preparedRegex.OperationMatchCursorPlans.StructuralLinearProgram.DeterministicProgram.HasValue &&
                !_preparedRegex.SearchPlan.HasBoundaryRequirements &&
                !program.RightToLeft && !program.IgnoreCase && !program.CanMatchEmpty &&
                !program.HasBalancing && !program.HasSharedCaptureDeclarations && !program.HasExplicitCaptureNumbers &&
                !program.HasContiguousAnchor && !program.SawLookaround && !program.HasConditionalTest &&
                !_requiresKelvinSignFallback && (Options & RegexOptions.NonBacktracking) == 0;
            var cursor = nativeBranch
                ? new Utf8OperationMatchCursor(input, _preparedRegex, invocation, true)
                : Utf8OperationMatchCursor.CreateMapped(program, input, validation, ref invocation, false, out _);
            return new Utf8ValueMatchEnumerator(cursor);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
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
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HasFiniteExecutionLimits)
            return OwnedSliceEnumerateMatches(input, _ownedSliceProgram, cancellationToken).WithTimeoutMapping(input, Pattern, MatchTimeout);
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
        return CreateOwnedOffsetEnumerator(input, utf16Offset, CancellationToken.None);
    }

    /// <summary>Enumerates non-overlapping matches starting at the specified UTF-16 code-unit offset, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="utf16Offset">The zero-based UTF-16 code-unit offset of the position where matching starts.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8ValueMatchEnumerator EnumerateMatchesFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset, CancellationToken cancellationToken)
    {
        return CreateOwnedOffsetEnumerator(input, utf16Offset, cancellationToken);
    }

    /// <summary>Enumerates all split segments, including the final tail.</summary>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input) =>
        EnumerateSplits(input, int.MaxValue);

    /// <summary>Enumerates all split segments, including the final tail, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return OwnedSliceEnumerateSplits(input, _ownedSliceProgram, int.MaxValue, cancellationToken);
    }

    /// <summary>Enumerates at most the requested number of split segments.</summary>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (GetOwnedSliceProgram(input) is { } ownedProgram)
            return OwnedSliceEnumerateSplits(input, ownedProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);
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
            var rtlBudget = CreateExecutionBudget();
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, _preparedRegex, rtlSplitLiteral, _preparedRegex.ExecutionKind, rtlBudget, Utf8InputAnalyzer.IsAscii(input), rightToLeft: true),
                rtlAnalysis.BoundaryMap,
                count == 0 ? int.MaxValue : count,
                rtlAnalysis.Utf16Length,
                rightToLeft: true)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        if (UsesRightToLeft() || ShouldUseKelvinSignFallback(input))
            return OwnedSliceEnumerateSplits(input, _ownedSliceProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);

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
        // return the scalar tail through the general owned split preparation.
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
                input,
                validation,
                totalUtf16Length,
                count,
                budget)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        // Scalar-verifiable simple patterns split through their native cursor;
        // remaining shapes use the cached scalar program below.
        if (count > 1 && Utf8ScalarSimplePatternVerifier.CanServeScalar(_preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, input))
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, _preparedRegex, analysis.BoundaryMap, budget),
                analysis.BoundaryMap,
                count,
                analysis.Utf16Length,
                rightToLeft: false)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        // Any remaining admitted shape uses the cached scalar cursor.
        return OwnedSliceEnumerateSplits(input, _ownedSliceProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);
    }

    /// <summary>Enumerates at most the requested number of split segments, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="count">The maximum number of split segments to return.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return OwnedSliceEnumerateSplits(input, _ownedSliceProgram, count == 0 ? int.MaxValue : count, cancellationToken);
    }

    private Utf8ValueSplitEnumerator OwnedSliceEnumerateSplits(ReadOnlySpan<byte> input, Utf8SliceProgram program, int count, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            return Utf8OwnedSplitPreparation.Create(program, input, count, ref invocation)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
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

    /// <summary>Replaces all matches and returns newly allocated UTF-8 bytes, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
        var deadline = CreateExecutionBudget();
        var analyzed = GetParsedReplacement(replacement, new(deadline, cancellationToken));
        return ReplaceCore(input, analyzed, cancellationToken, deadline);
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

    /// <summary>Replaces all matches using a .NET replacement pattern encoded as well-formed UTF-8, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacementPatternUtf8">The replacement pattern encoded as well-formed UTF-8.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
        var deadline = CreateExecutionBudget();
        var control = new ReplacementPreparationControl(deadline, cancellationToken);
        ValidateReplacementTemplateUtf8(replacementPatternUtf8, control);


        if ((GetOwnedSliceProgram(input, cancellationToken) ??
            (input.IsEmpty || _ownedSliceProgram is { CanMatchEmpty: true } &&
                _preparedRegex.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral ? _ownedSliceProgram : null)) is { } ownedProgram)
        {
            return OwnedSliceReplace(input, ownedProgram,
                GetParsedReplacement(replacementPatternUtf8, control), Utf8SliceReplacementResultKind.Bytes, default, cancellationToken, deadline).Bytes;
        }


        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);

        if (replacementPatternUtf8.IndexOf((byte)'$') < 0)
        {
            return ReplaceLiteralBytesCore(input, validation, replacementPatternUtf8, deadline);
        }

        var replacementPattern = GetParsedReplacement(replacementPatternUtf8, control);
        if (GetOwnedReplacementProgram(input, replacementPattern, cancellationToken) is { } requiredProgram)
            return OwnedSliceReplace(input, requiredProgram, replacementPattern,
                Utf8SliceReplacementResultKind.Bytes, default, cancellationToken, deadline).Bytes;
        var budget = deadline;
        if (UsesOwnedTemplateBeforeNativePlan() &&
            TryReplaceTemplateWithOwnedGroups(input, replacementPattern, out var ownedTemplate))
        {
            return Encoding.UTF8.GetBytes(ownedTemplate);
        }

        if (TryGetExactLiteralReplacementBytes(replacementPattern, out var exactLiteralReplacementBytes))
        {
            return ReplaceLiteralBytesCore(input, validation, exactLiteralReplacementBytes, deadline);
        }

        if (TryReplaceViaNativePlan(input, validation, replacementPattern, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacementPattern, out var replacementBytes))
        {
            return Encoding.UTF8.GetBytes(ReplaceWithScalarProgram(input, replacementPattern, cancellationToken, deadline));
        }

        return ReplaceLiteralBytesCore(input, validation, replacementBytes, deadline);
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
        ArgumentNullException.ThrowIfNull(evaluator);
        try { return OwnedSliceReplaceEvaluator(input, _ownedSliceProgram, state, evaluator, cancellationToken); }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
    }

    /// <summary>Replaces all matches through a stateful callback that writes UTF-8 replacement content, observing cancellation during validation, matching and required result processing.</summary>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match to append replacement content.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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

    /// <summary>Replaces all matches and returns the result as a UTF-16 string, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
        var deadline = CreateExecutionBudget();
        var analyzed = GetParsedReplacement(replacement, new(deadline, cancellationToken));
        return ReplaceToStringCore(input, analyzed, cancellationToken, deadline);
    }

    /// <summary>Replaces all matches through a stateful callback that returns UTF-16 replacement text.</summary>
    /// <typeparam name="TState">The caller-defined state type.</typeparam>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator)
    {
        return ReplaceToStringEvaluatorCore(input, state, evaluator, default);
    }

    private string ReplaceToStringEvaluatorCore<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evaluator);
        try
        {
            return OwnedSliceReplace(input, _ownedSliceProgram, new Utf8SliceEvaluatorExpansion<TState>(evaluator, _program.CaptureCatalog),
                ref state, Utf8SliceReplacementResultKind.Text, default, cancellationToken).Text;
        }
        catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
    }

    /// <summary>Replaces all matches through a stateful callback that returns UTF-16 replacement text, observing cancellation during validation, matching and required result processing.</summary>
    /// <typeparam name="TState">The type of additional state passed to the evaluator.</typeparam>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="state">Additional state passed to the evaluator.</param>
    /// <param name="evaluator">The callback invoked for each match to produce replacement text.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ReplaceToStringEvaluatorCore(input, state, evaluator, cancellationToken);
    }

    /// <summary>Attempts to write a replacement result into a caller-provided UTF-8 destination.</summary>
    /// <returns><see cref="OperationStatus.Done"/> on success, or <see cref="OperationStatus.DestinationTooSmall"/> when the destination cannot hold the complete result.</returns>
    /// <remarks>An overlapping input and destination use staged output. An insufficient destination remains unchanged.</remarks>
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
        var deadline = CreateExecutionBudget();
        var analyzed = GetParsedReplacement(replacement, new(deadline, cancellationToken));
        return TryReplaceCore(input, analyzed, destination, out bytesWritten, cancellationToken, deadline);
    }

    /// <summary>Attempts to apply a UTF-8-encoded replacement pattern into a caller-provided UTF-8 destination.</summary>
    /// <returns><see cref="OperationStatus.Done"/> on success, or <see cref="OperationStatus.DestinationTooSmall"/> when the destination cannot hold the complete result.</returns>
    /// <remarks>Input and replacement spans may overlap the destination. Overlapping input uses staged output; an insufficient destination remains unchanged.</remarks>
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
        var deadline = CreateExecutionBudget();
        var control = new ReplacementPreparationControl(deadline, cancellationToken);
        ValidateReplacementTemplateUtf8(replacementPatternUtf8, control);


        if (!input.Overlaps(destination) && GetOwnedSliceProgram(input, cancellationToken) is null && !input.IsEmpty &&
            !(_ownedSliceProgram is { CanMatchEmpty: true } && _preparedRegex.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral) &&
            replacementPatternUtf8.IndexOf((byte)'$') < 0)
        {
            var validation = Utf8Validation.Validate(input);
            // The worker consumes the template synchronously. Own it only when
            // writing the destination could overwrite bytes still needed later.
            ReadOnlySpan<byte> directReplacementBytes = replacementPatternUtf8.Overlaps(destination)
                ? replacementPatternUtf8.ToArray()
                : replacementPatternUtf8;
            return TryReplaceLiteralBytesCore(input, validation, directReplacementBytes, destination, out bytesWritten, deadline);
        }

        var replacementPattern = GetParsedReplacement(replacementPatternUtf8, control);
        return TryReplaceCore(input, replacementPattern, destination, out bytesWritten, cancellationToken, deadline);
    }

    /// <summary>Attempts to write a replacement result into a caller-provided UTF-8 destination, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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

    /// <summary>Attempts to apply a UTF-8-encoded replacement pattern into a caller-provided UTF-8 destination, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacementPatternUtf8">The replacement pattern encoded as well-formed UTF-8.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
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
                "Scalar",
                _preparedRegex.StructuralSearchPlan.RequiresCandidateEndCoverage,
                _preparedRegex.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Window,
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
                "Scalar",
                _preparedRegex.StructuralSearchPlan.RequiresCandidateEndCoverage,
                _preparedRegex.StructuralSearchPlan.YieldKind == Utf8StructuralSearchYieldKind.Window,
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
        CreateMatchTimeoutException(input, Pattern, MatchTimeout, InvocationLimits);

    // Byte-bounded invocations must not copy a subject-sized diagnostic string
    // after their deadline. Keep the original timeout authoritative rather than
    // starting another admission/cleanup path while constructing its exception.
    internal static RegexMatchTimeoutException CreateMatchTimeoutException(ReadOnlySpan<byte> input,
        string pattern, TimeSpan timeout, Utf8ExecutionLimits limits) =>
        new(limits.MaxWorkingBytes != 0 || limits.MaxResultBytes != 0
            ? string.Empty : Encoding.UTF8.GetString(input), pattern, timeout);

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

    internal bool CanUseNativeSplit(Utf8ValidationResult validation)
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

    internal bool TryGetSmallAsciiLiteralFamilySplitSearch(
        ReadOnlySpan<byte> input,
        out PreparedSmallAsciiLiteralFamilySearch search)
    {
        if (MatchTimeout != Regex.InfiniteMatchTimeout ||
            _preparedRegex.ExecutionPattern.IndexOf('(') >= 0 ||
            CompiledEngineKind != Utf8CompiledEngineKind.LiteralFamily ||
            _program.CompiledEngineRuntime is not Utf8LiteralCompiledEngineRuntime literalFamilyRuntime)
        {
            search = default;
            return false;
        }

        return literalFamilyRuntime.TryGetSmallAsciiLiteralFamilyPrimitive(input, out search);
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
