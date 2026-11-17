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

/// <summary>Matches Unicode scalars directly over well-formed UTF-8 input.</summary>
/// <remarks>Matching results expose byte ranges; callers derive character coordinates.</remarks>
public sealed partial class Utf8Regex
{

    /// <summary>Compiles an expression for caller-valid, unchanged UTF-8 subjects.</summary>
    /// <remarks>Subjects must be valid unchanged UTF-8; pattern/template checks are independent. A null timeout uses DefaultMatchTimeout.
    /// Work and result traffic are cumulative per operation/cursor; depth and working bytes bound live modeled major state, buffers and growth overlap.
    /// Admission precedes acquisition and can conservatively count shared storage. Borrowed subjects and host decoding/collections are excluded.
    /// Construction/preparation and program funding have separate scope; use Compile for explicit construction limits and cancellation.</remarks>
    public Utf8Regex(string pattern, RegexOptions options = RegexOptions.CultureInvariant, TimeSpan? matchTimeout = null, Utf8ResourcePolicy resourcePolicy = default)
        : this(new Utf8ConstructionPattern(pattern), options, matchTimeout ?? DefaultMatchTimeout, resourcePolicy) { }

    private static TimeSpan s_defaultMatchTimeout = Regex.InfiniteMatchTimeout;

    private readonly Utf8RegexProgram _program;
    private readonly bool _prioritizePlainAsciiLiteralCount;
    private readonly int _fusedAsciiLiteralCountAnchorOffset;
    private readonly bool _prioritizeBoundaryLiteralFamilyCount;
    private readonly Utf8EmittedLiteralFamilyCounter? _fusedAsciiBoundaryLiteralFamilyCounter;
    private readonly bool _requiresKelvinSignFallback;
    private readonly Utf8InvariantCyrillicLiteralCountStrategy? _invariantCyrillicLiteralCountStrategy;
    internal readonly Utf8PackedNibbleLiteralFamilyCountStrategy? _packedNibbleLiteralFamilyCountStrategy;
    private readonly bool _hasCaptures;
    private readonly Utf8GroupedLiteralSpanTable? _groupedLiteralSpanTable;
    private readonly byte[]? _caretAnchoredGroupedLiteralUtf8;
    private readonly Utf8SliceProgram _ownedSliceProgram;
    private readonly Utf8ReplacementPlanCache _replacementCache;

    private Utf8Regex(Utf8ConstructionPattern pattern, RegexOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy)
    {
        ValidateCorePolicy(resourcePolicy, nameof(resourcePolicy));
        using var construction = Utf8ConstructionScope.Enter();

        Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout));
        ValidateOptions(options);
        if (!pattern.IsUtf8) ArgumentNullException.ThrowIfNull(pattern.Text, nameof(pattern));
        _patternText = pattern.IsUtf8 ? null : pattern.Text;
        Options = options;
        MatchTimeout = matchTimeout;
        ResourcePolicy = resourcePolicy;
        HasFiniteExecutionLimits = resourcePolicy.MaxWorkSteps.HasValue || resourcePolicy.MaxDepth.HasValue ||
            resourcePolicy.MaxWorkingBytes.HasValue || resourcePolicy.MaxResultBytes.HasValue;
        construction.Budget.CheckBytes(ProgramFixedMetadataBytes);
        construction.Budget.ChargeWork(1);
        construction.Budget.ReserveBytes(ProgramFixedMetadataBytes);

        _program = Utf8RegexProgram.Compile(pattern, options, prepareRuntime: !HasFiniteExecutionLimits);
        var preparedRegex = _program.PreparedRegex;
        _prioritizePlainAsciiLiteralCount = CanPrioritizePlainAsciiLiteralCount(pattern, preparedRegex, options);
        _fusedAsciiLiteralCountAnchorOffset =
            _prioritizePlainAsciiLiteralCount &&
            matchTimeout == Regex.InfiniteMatchTimeout &&
            preparedRegex.LiteralUtf8 is { } fusedAsciiLiteral
                ? Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset(fusedAsciiLiteral)
                : -1;
        _prioritizeBoundaryLiteralFamilyCount =
            (options & RegexOptions.RightToLeft) == 0 &&
            preparedRegex.ExecutionKind == NativeExecutionKind.ExactUtf8Literals &&
            preparedRegex.SearchPlan.HasBoundaryRequirements &&
            Utf8EmittedLiteralFamilyCounter.CanCreate(
                preparedRegex.SearchPlan,
                preparedRegex.SearchPlan.CountOperation,
                preparedRegex.SearchPlan.FirstMatchOperation);
        if (matchTimeout == Regex.InfiniteMatchTimeout &&
            (options & RegexOptions.RightToLeft) == 0 &&
            _program.CompiledEngineRuntime is Utf8LiteralCompiledEngineRuntime literalRuntime &&
            literalRuntime._emittedLiteralFamilyCounter is { SupportsAsciiValidatedCount: true } boundaryLiteralFamilyCounter)
        {
            _fusedAsciiBoundaryLiteralFamilyCounter = boundaryLiteralFamilyCounter;
        }
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
        _hasCaptures = ownedSliceProgram.GroupCount != 0 &&
            (preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex && !preparedRegex.AnchoredLiteral.HasValue ||
             HasOwnedCaptures(preparedRegex.ExecutionTree));
        // Grouped literal metadata still proves replacement eligibility and an
        // anchored value-cursor shortcut. Full capture snapshots use the byte
        // cursor, independently of this remaining native preparation.
        var scalarCaptureProjection = pattern.IsUtf8 || HasFiniteExecutionLimits || matchTimeout != Regex.InfiniteMatchTimeout ||
            _hasCaptures && ownedSliceProgram.HasUnicodePatternAtoms;
        var hasNativeCaptureTree = preparedRegex.ExecutionTree is not null && !UsesRightToLeft();
        // The anchored prefix table also supplies a value-cursor proof before
        // scalar selection. Preserve that dependency and its existing fences.
        _groupedLiteralSpanTable = _hasCaptures && hasNativeCaptureTree &&
            (!scalarCaptureProjection || _anchoredLiteral.Kind == Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix)
                ? Utf8GroupedLiteralSpanTable.TryCreate(preparedRegex.ExecutionTree, ownedSliceProgram) : null;
        // The existing literal proof respects each anchor's scoped options. Borrow
        // its admitted bytes instead of collecting/copying the execution tree again.
        _caretAnchoredGroupedLiteralUtf8 =
            _groupedLiteralSpanTable is { } spanTable &&
            (options & (RegexOptions.RightToLeft | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking)) == 0 &&
            _anchoredLiteral.Kind == Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } prefix &&
            prefix.Length == spanTable.TotalLengthInUtf16
                ? prefix : null;
        static bool CanPrioritizePlainAsciiLiteralCount(
            Utf8ConstructionPattern pattern, Utf8PreparedRegex preparedRegex, RegexOptions options)
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

        _replacementCache = Utf8ReplacementPlanCache.Create();
        if (pattern.IsUtf8)
        {
            // Parsing borrows only during this call. Retained source bytes are
            // separately admitted and copied before the expression can escape.
            _patternUtf8 = construction.Budget.AllocateArray<byte>(pattern.Utf8.Length, false);
            construction.Budget.ChargeWork((ulong)pattern.Utf8.Length);
            pattern.Utf8.CopyTo(_patternUtf8);
        }
        construction.Complete();
    }

    // ResourcePolicy is an immutable value copied by the shared constructor.
    private bool HasFiniteExecutionLimits { get; }

    private Utf8ExecutionLimits InvocationLimits => new(ResourcePolicy.MaxWorkSteps ?? 0, ResourcePolicy.MaxDepth ?? 0, ResourcePolicy.MaxWorkingBytes ?? 0, ResourcePolicy.MaxResultBytes ?? 0);

    private static Utf8ResourceLimitExceededException CreateExecutionResourceFailure(ref Utf8SliceInvocation invocation)
        => invocation.Budget.CreateResourceFailure();

    private static void ValidateCorePolicy(Utf8ResourcePolicy resourcePolicy, string paramName)
        => resourcePolicy.Validate(paramName);

    /// <summary>Gets or sets the timeout used by constructors that do not specify one.</summary>
    /// <remarks>The initial value is <see cref="Regex.InfiniteMatchTimeout"/>.</remarks>
    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set
        {
            s_defaultMatchTimeout = Utf8MatchTimeout.Validate(value, nameof(value));
        }
    }


    private readonly string? _patternText;
    private readonly byte[]? _patternUtf8;

    /// <summary>Gets pattern text. A byte-compiled expression projects UTF-8 only for this explicit interoperability request.</summary>
    /// <remarks>Native source is owned as bytes. Each explicit text projection allocates independently; the result is not retained in the program account.</remarks>
    public string Pattern => _patternText ?? Encoding.UTF8.GetString(_patternUtf8!);

    /// <summary>Gets the .NET regular-expression options used during compilation.</summary>
    public RegexOptions Options { get; }

    /// <summary>Gets the maximum duration of an individual matching operation.</summary>
    public TimeSpan MatchTimeout { get; }

    /// <summary>Gets the invocation resource policy.</summary>
    public Utf8ResourcePolicy ResourcePolicy { get; }

    private Utf8PreparedRegex _preparedRegex => _program.PreparedRegex;

    private Utf8CompiledEngine _compiledEngine => _program.CompiledEngine;

    private Utf8CompiledEngineRuntime _compiledEngineRuntime => _program.CompiledEngineRuntime ??
        throw new InvalidOperationException("This expression executes without an unmetered specialized runtime.");

    private int[] _groupNumbers => _program.CaptureCatalog.GroupNumbers;

    private string[] _groupNames => _program.CaptureCatalog.GroupNames;

    private ref readonly Utf8AnchoredLiteralPlan _anchoredLiteral =>
        ref _preparedRegex.AnchoredLiteral;

    private NativeExecutionKind ExecutionKind => _preparedRegex.ExecutionKind;

    private Utf8PreparedRegex PreparedRegex => _preparedRegex;

    internal Utf8ByteOffsetExecution ByteOffsetExecution => new(this); // PCRE2-INTEGRATION-POINT

    internal Utf8RegexInspection Inspection => new(this);

    private Utf8SearchPlan SearchPlan => _preparedRegex.SearchPlan;

    private Utf8StructuralSearchPlan StructuralSearchPlan => _preparedRegex.SearchPlan.StructuralSearchPlan;

    private Utf8SearchPortfolioKind SearchPortfolioKind => _preparedRegex.SearchPlan.PortfolioKind;

    private Utf8CompiledEngineKind CompiledEngineKind => _compiledEngine.Kind;

    private Utf8CompiledExecutionBackend CompiledExecutionBackend => _compiledEngine.Backend;

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
        if (_compiledEngine.Kind == Utf8CompiledEngineKind.FallbackRegex) return _ownedSliceProgram;
        // ASCII folded literals cannot start or end inside a UTF-8 scalar.
        // Their non-ASCII fold partner (Kelvin sign) is checked below; unrelated
        // Unicode elsewhere in the subject does not require scalar discovery.
        var scalarAlignedLiteral = _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals ||
            (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals ||
             _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCase && !_hasCaptures) &&
            (Options & RegexOptions.CultureInvariant) != 0;
        if (_ownedSliceProgram.CanMatchEmpty && _preparedRegex.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral ||
             _ownedSliceProgram.UsesInterpretedLazyLoopSemantics ||
             _ownedSliceProgram.HasSharedCaptureDeclarations ||
             _ownedSliceProgram.HasExplicitCaptureNumbers ||
             _ownedSliceProgram.HasBalancing ||
             _ownedSliceProgram.HasContiguousAnchor ||
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

    private Utf8CaptureRange OwnedSliceMatch(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
        => OwnedSliceMatchFromByte(input, program, program.RightToLeft ? input.Length : 0, token);

    private int OwnedSliceCount(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        return OwnedSliceCountFromByte(input, program, program.RightToLeft ? input.Length : 0, token);
    }

    private bool IsMatchCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (HasFiniteExecutionLimits) return OwnedSliceIsMatch(input, _ownedSliceProgram, cancellationToken);
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

            if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
            {
                return false;
            }

            var fastBudget = CreateExecutionBudget();
            return IsMatchViaCompiledEngine(input, default, fastBudget);
        }

        var validation = Utf8InputAnalyzer.Describe(input);

        if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
        {
            return false;
        }

        var budget = CreateExecutionBudget();

        return IsMatchViaCompiledEngine(input, validation, budget);
    }

    private bool OwnedSliceIsMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            return OwnedSliceIsMatchFromByte(input, program, startByte, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
    }

    private bool OwnedSliceIsMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, ref Utf8SliceInvocation invocation)
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
        if (RejectMissingRequiredLiteral(input, program, ref invocation)) return false;
        var slots = new Utf8SliceSlots([], []);
        try
        {
            slots = Utf8SliceRunner.RentSlots(GetValueOperationCaptureSlotCount(program), ref invocation);
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, startByte, slots.Starts, slots.Ends,
                false, ref invocation, _preparedRegex.SearchPlan);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Owned slice execution breached the invocation budget.");
            invocation.Budget.ThrowIfCancellationRequested();
            return outcome.Found;
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    // Adapters may borrow a necessary exact literal from their existing native
    // rendering. They receive caller-valid text and keep their own verifier;
    // finding this literal never proves a regex match or a candidate start.
    internal bool RejectMissingExactRequiredLiteral(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        ref readonly var prefilter = ref _preparedRegex.SearchPlan.PrefilterPlan.PrimarySearcher;
        if (prefilter.Kind != PreparedSearcherKind.ExactLiteral) return false;
        var breach = prefilter.TryFindMatch(input, 0, input.Length, false, ref invocation, out _, out var found);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Required literal screening breached the invocation budget.");
        return !found;
    }

    private bool RejectMissingRequiredLiteral(ReadOnlySpan<byte> input, Utf8SliceProgram program, ref Utf8SliceInvocation invocation)
    {
        if (!ReferenceEquals(program, _ownedSliceProgram) ||
            program.RightToLeft || program.HasContiguousAnchor ||
            // Includes internal multiline ^ assertions, whose origin must be
            // preserved by the scalar program.
            _preparedRegex.Features.HasAbsoluteStartAnchor ||
            _preparedRegex.SearchPlan.CanGuideFallbackStarts) return false;
        ref readonly var prefilter = ref _preparedRegex.SearchPlan.PrefilterPlan.PrimarySearcher;
        if (prefilter.Kind is not (PreparedSearcherKind.ExactLiteral or PreparedSearcherKind.MultiLiteral)) return false;
        // The existing planner proves this literal is necessary. Search the
        // complete caller-valid subject, including lookbehind context;
        // presence proves nothing about a match and retains normal verification.
        var breach = prefilter.TryFindMatch(input, 0, input.Length, false, ref invocation, out _, out var found);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Required literal screening breached the invocation budget.");
        return !found;
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
        if (TryStartNativeLiteralInvocation(input, cancellationToken, out var invocation, forCount: true))
        {
            var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(_preparedRegex.SearchPlan,
                input, UsesRightToLeft(), ref invocation, out var count);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return count;
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
            return packedNibbleLiteralFamilyCountStrategy.Count(input);
        }

        if (_prioritizePlainAsciiLiteralCount ||
            _prioritizeBoundaryLiteralFamilyCount)
        {

            return CountViaCompiledEngine(input, default, CreateExecutionBudget());
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

        if (CanUseFusedCompiledAsciiLiteralFamilyCount())
        {

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
            if (RejectsByRequiredPrefilter(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
                return 0;
            }

            var fastBudget = CreateExecutionBudget();
            return CountViaCompiledEngine(input, default, fastBudget);
        }

        if (CanUseWellFormedOnlyValidation())
        {

            if (RejectsByRequiredPrefilter(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
                return 0;
            }

            var fastBudget = CreateExecutionBudget();
            return CountViaCompiledEngine(input, default, fastBudget);
        }

        var validation = Utf8InputAnalyzer.Describe(input);

        if (RejectsByRequiredPrefilter(input))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.RequiredPrefilterReject);
            return 0;
        }

        var budget = CreateExecutionBudget();

        return CountViaCompiledEngine(input, validation, budget);
    }

    /// <summary>Owned scalar count from an admitted byte boundary.</summary>
    private int OwnedSliceCountFromByte(
        ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            // Count consumes bytes only, without derived coordinate metadata.
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            return OwnedSliceCountFromByte(input, program, startByte, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
    }

    private int OwnedSliceCountFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, ref Utf8SliceInvocation invocation)
    {
        // Count publishes no capture values. Reuse the complete scalar-run proof
        // before expanded branch search or acquiring unused capture slots.
        if (Utf8SliceRunner.TryCountTerminalClassRepeat(program, input, startByte, ref invocation, out var terminal))
        {
            if (terminal.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Terminal class count breached the invocation budget.");
            return terminal.Count;
        }
        // A pure literal/family Count needs no capture slots or continuation frames.
        // Borrow the validated invocation instead of dropping finite callers
        // into the VM solely because they selected an enforceable policy.
        if (HasFiniteExecutionLimits && CanUseNativeLiteralSearch(program, maximumAlternatives: 8))
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
            slots = Utf8SliceRunner.RentSlots(GetValueOperationCaptureSlotCount(program), ref invocation);
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

    // Boolean/count/value results do not expose captures. This general proof
    // keeps dependent, alternate and unknown programs' storage.
    // Detailed results and cursors continue to request every capture slot.
    internal bool CanOmitValueCaptureSlots => GetValueOperationCaptureSlotCount(_ownedSliceProgram) == 0;

    private int GetValueOperationCaptureSlotCount(Utf8SliceProgram program)
        => ReferenceEquals(program, _ownedSliceProgram) &&
            _preparedRegex.Features is { CaptureCount: > 0, HasBackreferences: false, HasConditionals: false, HasLookarounds: false } &&
            !program.IsNonBacktracking && !program.HasBalancing &&
            !program.HasSharedCaptureDeclarations && !program.HasExplicitCaptureNumbers &&
            !program.HasConditionalTest && !program.SawLookaround && !program.SawCapturingLookbehind
                ? 0 : program.GroupCount;

    private Utf8CaptureRange MatchCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (HasFiniteExecutionLimits) return OwnedSliceMatch(input, _ownedSliceProgram, cancellationToken);
        if (TryStartNativeLiteralInvocation(input, cancellationToken, out var invocation))
        {
            var plan = _preparedRegex.SearchPlan;
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(plan, input, 0, input.Length,
                UsesRightToLeft(), ref invocation, out var selected, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return found ? Utf8SliceBufferWork.CreateMatchRange(selected.Index, (selected.Index) + (selected.Length), ref invocation) : default(Utf8CaptureRange);
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
                ? new Utf8CaptureRange(0, 0 + (caretMatchPrefix.Length))
                : default(Utf8CaptureRange);
        }

        var canUseAsciiWellFormedOnlyMatch =
            _compiledEngineRuntime.SupportsAsciiWellFormedOnlyMatch &&
            CanUseAsciiDirectInput(input);

        var canUseWellFormedOnlyMatch =
            _compiledEngineRuntime.SupportsWellFormedOnlyMatch;

        if (canUseAsciiWellFormedOnlyMatch || canUseWellFormedOnlyMatch)
        {

            if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
            {
                return default(Utf8CaptureRange);
            }

            var matched = canUseAsciiWellFormedOnlyMatch
                ? _compiledEngineRuntime.TryMatchAsciiWellFormedOnly(input, out var wellFormedMatch)
                : _compiledEngineRuntime.TryMatchWellFormedOnly(input, out wellFormedMatch);

            if (matched)
            {
                return wellFormedMatch;
            }
            if (_compiledEngineRuntime.WellFormedOnlyMatchMissIsDefinitive)
            {
                return default;
            }
        }

        var validation = Utf8InputAnalyzer.Describe(input);

        if (!ShouldSkipRequiredPrefilterForMatch() && RejectsByRequiredPrefilter(input))
        {
            return default(Utf8CaptureRange);
        }

        var budget = CreateExecutionBudget();
        return MatchViaCompiledEngine(input, validation, budget);
    }

    private Utf8CaptureRange OwnedSliceMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            return OwnedSliceMatchFromByte(input, program, startByte, ref invocation);
        }
        catch (InvalidOperationException error) when (error is not Utf8ResourceLimitExceededException && invocation.Budget.LimitBreach != Utf8ExecutionLimitKind.None)
        {
            throw CreateExecutionResourceFailure(ref invocation);
        }
    }

    private Utf8CaptureRange OwnedSliceMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, ref Utf8SliceInvocation invocation)
    {
        invocation.AnchorOriginByte = startByte;
        if (HasFiniteExecutionLimits && CanUseNativeLiteralSearch(program, maximumAlternatives: 8))
        {
            // Value Match needs only the selected range. Borrow Count's proven
            // literal discovery and the existing metered coordinate projection;
            // detailed captures still use their own scalar VM path.
            ref readonly var plan = ref _preparedRegex.SearchPlan;
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(plan, input, startByte,
                input.Length, false, ref invocation, out var selected, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Native literal match breached the invocation budget.");
            return found ? Utf8SliceBufferWork.CreateMatchRange(selected.Index, (selected.Index) + (selected.Length), ref invocation) : default(Utf8CaptureRange);
        }
        var slots = new Utf8SliceSlots([], []);
        try
        {
            slots = Utf8SliceRunner.RentSlots(GetValueOperationCaptureSlotCount(program), ref invocation);
            var outcome = Utf8SliceRunner.TryFindFirst(program, input, startByte, slots.Starts, slots.Ends, false, ref invocation);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Owned slice execution breached the invocation budget.");
            invocation.Budget.ThrowIfCancellationRequested();
            if (!outcome.Found) return default(Utf8CaptureRange);
            var length = outcome.EndByte - outcome.StartByte;
            return Utf8SliceBufferWork.CreateMatchRange(outcome.StartByte, (outcome.StartByte) + (length), ref invocation);
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
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

    /// <summary>Enumerates non-overlapping matches in the well-formed UTF-8 input.</summary>
    private Utf8ValueMatchEnumerator EnumerateMatchesWithoutToken(ReadOnlySpan<byte> input)
    {
        if (HasFiniteExecutionLimits)
            return OwnedSliceEnumerateMatches(input, _ownedSliceProgram, CancellationToken.None).WithTimeoutMapping(input, this);
        // Anchored prefixes yield at most one match at offset 0 through the
        // anchor-enforcing owned cursor, without BCL construction.
        if (_anchoredLiteral.Kind == Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } anchoredEnumerateLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, anchoredEnumerateLiteral, CreateExecutionBudget()))
                .WithTimeoutMapping(input, this);
        }

        // Anchored suffixes yield at most one match at the input end through the
        // anchor-enforcing owned cursor, without BCL construction.
        if (_anchoredLiteral.IsExactSuffix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, _anchoredLiteral.LiteralUtf8, CreateExecutionBudget(), _anchoredLiteral.Kind))
                .WithTimeoutMapping(input, this);
        }

        // Folded anchored prefixes enumerate the same single anchored hit with
        // per-byte invariant folding. Kelvin and Turkic divergences fall through to
        // scalar execution below; all other inputs stay direct.
        if (_anchoredLiteral.Kind == Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } foldedPrefixLiteral)
        {
            if (!ShouldUseKelvinSignFallback(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input, foldedPrefixLiteral, CreateExecutionBudget(), _anchoredLiteral.Kind))
                    .WithTimeoutMapping(input, this);
            }
        }

        // Folded anchored suffixes enumerate the same single tail hit with per-byte
        // invariant folding, under the same fence contract as prefixes.
        if (_anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } foldedSuffixLiteral)
        {
            if (!ShouldUseKelvinSignFallback(input))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input, foldedSuffixLiteral, CreateExecutionBudget(), _anchoredLiteral.Kind))
                    .WithTimeoutMapping(input, this);
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
                .WithTimeoutMapping(input, this);
        }

        if (GetOwnedSliceProgram(input) is { } ownedProgram)
        {
            return OwnedSliceEnumerateMatches(input, ownedProgram, CancellationToken.None)
                .WithTimeoutMapping(input, this);
        }

        var subject = Utf8ValidatedInput.Create(input);
        var startOffsetInBytes = UsesRightToLeft() ? input.Length : 0;
        var start = subject.GetBytePosition(startOffsetInBytes, "startOffsetInBytes");
        return new Utf8ByteOffsetExecution(this).EnumerateMatches(subject, start)
            .WithTimeoutMapping(input, this);
    }

    private Utf8ValueMatchEnumerator OwnedSliceEnumerateMatches(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            invocation.ShareStorageForCursor();
            var cursor = new Utf8OperationMatchCursor(program, input, invocation);
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

    private Utf8ValueMatchEnumerator OwnedSliceEnumerateMatchesFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, CancellationToken token)
    {
        try
        {
            return new Utf8ValueMatchEnumerator(program, input, MatchTimeout, token, startByte);
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
    private Utf8ValueMatchEnumerator EnumerateMatchesWithToken(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HasFiniteExecutionLimits)
            return OwnedSliceEnumerateMatches(input, _ownedSliceProgram, cancellationToken).WithTimeoutMapping(input, this);
        // Single literals retain one invocation through entry, discovery and result
        // publication. Other shapes retain the scalar VM.
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
                Utf8SliceBufferWork.Checkpoint(ref invocation);
                return new Utf8ValueMatchEnumerator(new Utf8OperationMatchCursor(
                    input, _preparedRegex, UsesRightToLeft(), invocation))
                    .WithTimeoutMapping(input, this);
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
                Utf8SliceBufferWork.Checkpoint(ref invocation);
                return new Utf8ValueMatchEnumerator(new Utf8OperationMatchCursor(
                    input, _preparedRegex, invocation)).WithTimeoutMapping(input, this);
            }
            catch (Utf8ExecutionDeadlineExpiredException) { throw CreateMatchTimeoutException(input); }
        }

        if (GetOwnedSliceProgram(input, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceEnumerateMatches(input, ownedProgram, cancellationToken)
                .WithTimeoutMapping(input, this);
        }

        return EnumerateMatchesWithoutToken(input);
    }

    /// <summary>Enumerates non-overlapping matches from a scalar-aligned byte offset in the original input.</summary>
    /// <remarks>Null start selects zero forward or the subject end for RTL; explicit starts are scalar-aligned bytes. The complete subject remains available to anchors, boundaries and lookbehind. Original token, deadline and limits apply throughout enumeration. Keep borrowed input unchanged.</remarks>
    /// <exception cref="ArgumentOutOfRangeException">The start is outside the subject or inside a UTF-8 scalar.</exception>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int? startOffsetInBytes = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (startOffsetInBytes is { } start)
            return CreateOwnedByteOffsetEnumerator(input, start, cancellationToken);
        return cancellationToken.CanBeCanceled
            ? EnumerateMatchesWithToken(input, cancellationToken)
            : EnumerateMatchesWithoutToken(input);
    }
    /// <summary>Enumerates at most the requested number of split segments.</summary>
    private Utf8ValueSplitEnumerator EnumerateSplitsWithoutToken(ReadOnlySpan<byte> input, int count)
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
            var rtlBudget = CreateExecutionBudget();
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, _preparedRegex, rtlSplitLiteral, _preparedRegex.ExecutionKind, rtlBudget, rightToLeft: true),
                count == 0 ? int.MaxValue : count,
                rightToLeft: true)
                .WithTimeoutMapping(this);
        }

        if (UsesRightToLeft() || ShouldUseKelvinSignFallback(input))
            return OwnedSliceEnumerateSplits(input, _ownedSliceProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);

        if (count > 0 && TryGetSmallAsciiLiteralFamilySearch(input, out var smallAsciiLiteralFamilySearch))
        {
            return new Utf8ValueSplitEnumerator(
                input,
                smallAsciiLiteralFamilySearch,
                count)
                .WithTimeoutMapping(this);
        }


        var budget = CreateExecutionBudget();
        var usesAsciiDirectInput = CanUseAsciiDirectInput(input);
        var validation = usesAsciiDirectInput
            ? new Utf8ValidationResult(input.Length, true)
            : Utf8InputAnalyzer.Describe(input);
        // R05: anchored ASCII literal families split through the anchor-enforcing
        // owned cursors (at most one match at a fixed position), like enumeration.
        var isAnchoredDirectSplitFamily = _anchoredLiteral.LiteralUtf8 is { Length: > 0 } &&
            (_anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix ||
            _anchoredLiteral.IsExactSuffix ||
            _anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd);
        if (count > 0 &&
            (CanUseNativeSplit(validation) || isAnchoredDirectSplitFamily))
        {
            return CreateSplitEnumeratorViaCompiledEngine(input, validation, count, budget)
                .WithTimeoutMapping(this);
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
                count)
                .WithTimeoutMapping(this);
        }

        // Any remaining admitted shape uses the cached scalar cursor.
        return OwnedSliceEnumerateSplits(input, _ownedSliceProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);
    }

    /// <summary>Enumerates at most the requested number of split segments, observing cancellation when enumeration begins.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="count">The maximum number of split segments to return.</param>
    /// <param name="cancellationToken">The token to observe when enumeration begins.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count = int.MaxValue, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!cancellationToken.CanBeCanceled) return EnumerateSplitsWithoutToken(input, count);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return OwnedSliceEnumerateSplits(input, _ownedSliceProgram, count == 0 ? int.MaxValue : count, cancellationToken);
    }

    private Utf8ValueSplitEnumerator OwnedSliceEnumerateSplits(ReadOnlySpan<byte> input, Utf8SliceProgram program, int count, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            return Utf8OwnedSplitPreparation.Create(program, input, count, ref invocation)
                .WithTimeoutMapping(this);
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

    /// <summary>Replaces all matches and returns newly allocated UTF-8 bytes, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacement">The replacement text.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, CancellationToken cancellationToken = default)
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

    private byte[] ReplaceStringCore(ReadOnlySpan<byte> input, string replacement, CancellationToken cancellationToken)
    {
        var deadline = CreateExecutionBudget();
        var analyzed = GetParsedReplacement(replacement, new(deadline, cancellationToken));
        return ReplaceCore(input, analyzed, cancellationToken, deadline);
    }

    /// <summary>Replaces all matches using a .NET replacement pattern encoded as well-formed UTF-8, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacementPatternUtf8">The replacement pattern encoded as well-formed UTF-8.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, CancellationToken cancellationToken = default)
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

    private byte[] ReplaceUtf8Core(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, CancellationToken cancellationToken)
    {
        var deadline = CreateExecutionBudget();
        var control = new ReplacementPreparationControl(deadline, cancellationToken);
        ValidateReplacementTemplateUtf8(replacementPatternUtf8, control);


        if ((_preparedRegex.ExecutionKind is NativeExecutionKind.FallbackRegex ? _ownedSliceProgram : GetOwnedSliceProgram(input, cancellationToken) ??
            (input.IsEmpty || _ownedSliceProgram is { CanMatchEmpty: true } &&
                _preparedRegex.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral ? _ownedSliceProgram : null)) is { } ownedProgram)
        {
            return OwnedSliceReplace(input, ownedProgram,
                GetParsedReplacement(replacementPatternUtf8, control), Utf8SliceReplacementResultKind.Bytes, default, cancellationToken, deadline).Bytes;
        }


        var validation = CanUseAsciiDirectInput(input)
            ? default
            : Utf8InputAnalyzer.Describe(input);

        if (replacementPatternUtf8.IndexOf((byte)'$') < 0)
        {
            return ReplaceLiteralBytesCore(input, validation, replacementPatternUtf8, deadline);
        }

        var replacementPattern = GetParsedReplacement(replacementPatternUtf8, control);
        if (GetOwnedReplacementProgram(input, replacementPattern, cancellationToken) is { } requiredProgram)
            return OwnedSliceReplace(input, requiredProgram, replacementPattern,
                Utf8SliceReplacementResultKind.Bytes, default, cancellationToken, deadline).Bytes;
        var budget = deadline;
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
            return OwnedSliceReplace(input, _ownedSliceProgram, replacementPattern, Utf8SliceReplacementResultKind.Bytes,
                default, cancellationToken, deadline).Bytes;
        }

        return ReplaceLiteralBytesCore(input, validation, replacementBytes, deadline);
    }

    private OperationStatus TryReplaceStringCore(
        ReadOnlySpan<byte> input, string replacement, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken)
    {
        var deadline = CreateExecutionBudget();
        var analyzed = GetParsedReplacement(replacement, new(deadline, cancellationToken));
        return TryReplaceCore(input, analyzed, destination, out bytesWritten, cancellationToken, deadline);
    }

    private OperationStatus TryReplaceUtf8Core(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken)
    {
        var deadline = CreateExecutionBudget();
        var control = new ReplacementPreparationControl(deadline, cancellationToken);
        ValidateReplacementTemplateUtf8(replacementPatternUtf8, control);


        if (!input.Overlaps(destination) &&
            _preparedRegex.ExecutionKind is not (NativeExecutionKind.FallbackRegex) &&
            GetOwnedSliceProgram(input, cancellationToken) is null && !input.IsEmpty &&
            !(_ownedSliceProgram is { CanMatchEmpty: true } && _preparedRegex.ExecutionBackend == Utf8ExecutionBackend.NativeLiteral) &&
            replacementPatternUtf8.IndexOf((byte)'$') < 0)
        {
            var validation = Utf8InputAnalyzer.Describe(input);
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
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, string replacement, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken = default)
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
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken = default)
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

    private Utf8ExecutionDeadline CreateExecutionBudget()
    {
        return Utf8ExecutionDeadline.Start(MatchTimeout);
    }

    private RegexMatchTimeoutException CreateMatchTimeoutException(ReadOnlySpan<byte> input) =>
        CreateMatchTimeoutException(input, Pattern, MatchTimeout, InvocationLimits);

    // Byte-bounded invocations must not copy a subject-sized diagnostic string
    // after their deadline. Keep the original timeout authoritative rather than
    // starting another admission/cleanup path while constructing its exception.
    internal static RegexMatchTimeoutException CreateMatchTimeoutException(ReadOnlySpan<byte> input, string pattern, TimeSpan timeout, Utf8ExecutionLimits limits) =>
        new(limits.MaxWorkingBytes != 0 || limits.MaxResultBytes != 0
            ? string.Empty : Encoding.UTF8.GetString(input), pattern, timeout);

    internal bool CanUseNativeSplit(Utf8ValidationResult validation)
    {
        // Split segments are match-delimited gaps. Grouped literal shapes use
        // the same group-agnostic cursors as ungrouped literals.
        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => true,
            Utf8CompiledEngineKind.LiteralFamily => true,
            _ => false
        };
    }

    internal bool TryGetSmallAsciiLiteralFamilySearch(
        ReadOnlySpan<byte> input, out PreparedSmallAsciiLiteralFamilySearch search)
    {
        if (MatchTimeout != Regex.InfiniteMatchTimeout ||
            _preparedRegex.ExecutionPattern is null || _preparedRegex.ExecutionPattern.IndexOf('(') >= 0 ||
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
||
            (_compiledEngine.Kind == Utf8CompiledEngineKind.FallbackRegex &&
             !CanGuideFallbackVerification());
    }

    private bool CanUseAsciiDirectInput(ReadOnlySpan<byte> input)
    {
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or
                                             NativeExecutionKind.ExactUtf8Literal or
                                             NativeExecutionKind.ExactUtf8Literals or
                                             NativeExecutionKind.AsciiLiteralIgnoreCase or
                                             NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals) &&
            !_preparedRegex.AnchoredLiteral.IsExact)
        {
            return false;
        }

        return Utf8InputAnalyzer.IsAscii(input);
    }

}
