using Lokad.Utf8Regex.Internal;
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
public sealed partial class Utf8Regex : IDisposable
{

    /// <summary>Compiles an expression for caller-valid, unchanged UTF-8 subjects.</summary>
    /// <remarks>Subjects must be valid unchanged UTF-8; pattern/template checks are independent. A null timeout uses DefaultMatchTimeout.
    /// Work and result traffic are cumulative per operation/cursor; depth and working bytes bound live modeled major state, buffers and growth overlap.
    /// Admission precedes acquisition and can conservatively count shared storage. Borrowed subjects and host decoding/collections are excluded.
    /// Policy construction controls are independent of matching; Compile additionally accepts an original construction token. Funded creation prepays bounded cache capacity and requires explicit disposal.</remarks>
    public Utf8Regex(string pattern, RegexOptions options = RegexOptions.CultureInvariant, Utf8RegexPolicy? policy = null)
        : this(new Utf8ConstructionPattern(pattern), options, policy, default, explicitCompile: false) { }

    private readonly Utf8FundedStorage? _programMemory;

    /// <summary>Closes caller-funded program admission after active calls and persistent cursors finish.</summary>
    /// <remarks>No-op without funding. Copied span enumerators require an open regex at each advance; their returned values remain valid. Persistent memory cursors retain their admitted program use after close. No finalizer is used.</remarks>
    public void Dispose() => _programMemory?.Close();

    private static TimeSpan s_defaultMatchTimeout = Regex.InfiniteMatchTimeout;

    private readonly Utf8RegexProgram _program;
    private readonly int _asciiLiteralCountAnchorOffset;
    private readonly Utf8EmittedLiteralFamilyCounter? _fusedAsciiBoundaryLiteralFamilyCounter;
    private readonly bool _requiresKelvinSignFallback;
    private readonly Utf8InvariantCyrillicLiteralCountStrategy? _invariantCyrillicLiteralCountStrategy;
    internal readonly Utf8PackedNibbleLiteralFamilyCountStrategy? _packedNibbleLiteralFamilyCountStrategy;
    private readonly bool _hasCaptures;
    private readonly int _wholeLiteralCaptureNumber;
    private readonly Utf8SliceProgram _ownedSliceProgram;
    private readonly Utf8ReplacementPlanCache _replacementCache;

    private Utf8Regex(Utf8ConstructionPattern pattern, RegexOptions options, Utf8RegexPolicy? policy,
        CancellationToken token, bool explicitCompile)
    {
        token.ThrowIfCancellationRequested();
        policy ??= Utf8RegexPolicy.Default;
        var resourcePolicy = policy.ResourcePolicy;
        ValidateCorePolicy(resourcePolicy, nameof(policy));
        var matchTimeout = Utf8MatchTimeout.Validate(policy.MatchTimeout ?? DefaultMatchTimeout, nameof(policy));
        var constructionPolicy = policy.ConstructionPolicy;
        constructionPolicy.Validate(nameof(policy));
        var constructionTimeout = Utf8MatchTimeout.Validate(policy.ConstructionTimeout ?? Timeout.InfiniteTimeSpan, nameof(policy));
        var account = policy.ProgramMemoryAllowance;
        var ownsConstruction = explicitCompile || account is not null || constructionPolicy != default ||
            constructionTimeout != Timeout.InfiniteTimeSpan;
        using var construction = !ownsConstruction ? Utf8ConstructionScope.Enter() : account is null
            ? Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, token)
            : Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, token, account);
        try
        {
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

            _program = Utf8RegexProgram.Compile(pattern, options, prepareRuntime: !HasFiniteExecutionLimits, out var root);
            var preparedRegex = _program.PreparedRegex;
            _asciiLiteralCountAnchorOffset =
                matchTimeout == Regex.InfiniteMatchTimeout &&
                IsPlainAsciiLiteral(pattern, preparedRegex, options) &&
                preparedRegex.LiteralUtf8 is { } asciiLiteral
                    ? Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset(asciiLiteral)
                    : -1;
            if (matchTimeout == Regex.InfiniteMatchTimeout &&
                (options & RegexOptions.RightToLeft) == 0 &&
                _program.CompiledEngineRuntime is { } literalRuntime &&
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
            var usesLiteralEngine = preparedRegex.HasLiteralPlan;
            var captureRoot = usesLiteralEngine || _anchoredLiteral.HasValue ? root : null;
            _hasCaptures = ownedSliceProgram.GroupCount != 0 &&
                (captureRoot is null || ContainsCapture(captureRoot));
            if (usesLiteralEngine && _hasCaptures && !ownedSliceProgram.HasUnicodePatternAtoms &&
                !HasFiniteExecutionLimits && matchTimeout == Regex.InfiniteMatchTimeout && !UsesRightToLeft())
            {
                _wholeLiteralCaptureNumber = GetWholeLiteralCaptureNumber(captureRoot);
            }
            static bool IsPlainAsciiLiteral(
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
            if (account is not null)
            {
                // Prepay the bounded lazy cache once. Its future retained
                // payload must not escape the program's admission contract.
                construction.Budget.ReserveBytes(_replacementCache.MaximumRetainedBytes);
                var storage = construction.Budget.AllocateObject(account,
                    static allowance => new Utf8FundedStorage(allowance, nameof(Utf8Regex)));
                construction.Budget.ChargeWork(0);
                storage.Retain(construction.Budget.TransferFundedBytes());
                _programMemory = storage;
            }
            else if (ownsConstruction) construction.Budget.ChargeWork(0);
            construction.Complete();
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (ownsConstruction)
        { throw new RegexMatchTimeoutException(string.Empty, pattern.IsUtf8 ? string.Empty : pattern.Text, constructionTimeout); }
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


    private Utf8LiteralCompiledEngineRuntime _compiledEngineRuntime => _program.CompiledEngineRuntime ??
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

    private Utf8CompiledEngineKind CompiledEngineKind => _program.CompiledEngineKind;

    private string? FallbackReason => _preparedRegex.FallbackReason;

    private static void ValidateOptions(RegexOptions options)
    {
        // Own the admitted option set; runtime upgrades do not extend the grammar.
        Utf8RegexSyntax.ValidateOptions(options);
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
        if (CompiledEngineKind == Utf8CompiledEngineKind.FallbackRegex) return _ownedSliceProgram;
        // ASCII folded literals cannot start or end inside a UTF-8 scalar.
        // Their non-ASCII fold partner (Kelvin sign) is checked below; unrelated
        // Unicode elsewhere in the subject does not require scalar discovery.
        var scalarAlignedLiteral = _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals ||
            (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals ||
             _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCase && !_hasCaptures) &&
            (Options & RegexOptions.CultureInvariant) != 0;
        if (_ownedSliceProgram.CanMatchEmpty && _preparedRegex.HasLiteralPlan ||
             _ownedSliceProgram.UsesInterpretedLazyLoopSemantics ||
             _ownedSliceProgram.HasSharedCaptureDeclarations ||
             _ownedSliceProgram.HasExplicitCaptureNumbers ||
             _ownedSliceProgram.HasBalancing ||
             _ownedSliceProgram.HasContiguousAnchor ||
             _ownedSliceProgram.IgnoreCase &&
                _preparedRegex.ExecutionKind is (NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals) ||
             ShouldUseKelvinSignFallback(input) ||
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
        if (HasFiniteExecutionLimits || !_preparedRegex.HasLiteralPlan)
            return OwnedSliceIsMatch(input, _ownedSliceProgram, cancellationToken);
        return IsMatchLiteral(input, cancellationToken);
    }

    private bool IsMatchLiteral(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (CanStartNativeLiteralInvocation(cancellationToken))
        {
            var invocation = Utf8SliceInvocation.CreateExclusive(Utf8ExecutionLimits.Unlimited, MatchTimeout, cancellationToken);
            Utf8SliceBufferWork.Checkpoint(ref invocation);
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

        if (TryIsMatchAnchoredLiteral(input, out var directIsMatch))
        {
            return directIsMatch;
        }

        if (TryMatchDirectWithoutValidation(input, out var directMatch))
        {
            return directMatch.Success;
        }

        return _compiledEngineRuntime.IsMatch(input, CreateExecutionBudget());
    }

    private bool OwnedSliceIsMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, CancellationToken token)
    {
        var invocation = Utf8SliceInvocation.CreateExclusive(InvocationLimits, MatchTimeout, token);
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
        // Boolean results need no capture storage. The bounded finder confirms
        // boundary requirements on the original subject under this invocation.
        if ((HasFiniteExecutionLimits || invocation.Ledger.HasMemoryAllowance) &&
            CanUseNativeLiteralSearch(program, maximumAlternatives: 8, allowBoundaries: true))
        {
            var breach = Utf8SearchExecutor.TryFindNativeLiteralMatch(_preparedRegex.SearchPlan,
                input, startByte, input.Length, false, ref invocation, out _, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Native literal existence search breached the invocation budget.");
            return found;
        }
        if (RejectMissingRequiredInput(input, program, ref invocation, out var screenedLiteral)) return false;
        ref readonly var search = ref _preparedRegex.SearchPlan;
        ref readonly var prefilter = ref search.RequiredPrefilterSearcher;
        if (screenedLiteral >= 0 && screenedLiteral >= startByte &&
            Utf8SliceRunner.HasRequiredLiteralWitness(program, prefilter.LiteralSearch.Needle))
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            return true;
        }
        if (program.IsNonBacktracking && program.NonBacktrackingEpsilonAncestors.Length != 0 ||
            Utf8SliceRunner.CanSearchOrdinaryOrdered(program, input.Length - startByte, search, ref invocation))
        {
            var breach = Utf8SliceRunner.IsMatchPackedNonBacktracking(program, input, startByte, ref invocation, out var found);
            if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
            return found;
        }
        var knownDelimiter = screenedLiteral >= 0 && ReferenceEquals(prefilter.LiteralSearch.Needle, search.LiteralUtf8)
            ? screenedLiteral : -1;
        var outcome = FindOwnedSliceValue(program, input, startByte, ref invocation, search,
            existenceOnly: true, screenedLiteral: knownDelimiter);
        if (outcome.Breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned slice execution breached the invocation budget.");
        invocation.Budget.ThrowIfCancellationRequested();
        return outcome.Found;
    }

    // Adapters may borrow a necessary exact literal from their existing native
    // rendering. They receive caller-valid text and keep their own verifier;
    // finding this literal never proves a regex match or a candidate start.
    internal bool RejectMissingExactRequiredLiteral(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        ref readonly var prefilter = ref _preparedRegex.SearchPlan.RequiredPrefilterSearcher;
        if (prefilter.Kind != PreparedSearcherKind.ExactLiteral) return false;
        var breach = prefilter.TryFindMatch(input, 0, input.Length, false, ref invocation, out _, out var found);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Required literal screening breached the invocation budget.");
        return !found;
    }

    private bool RejectMissingRequiredInput(ReadOnlySpan<byte> input, Utf8SliceProgram program,
        ref Utf8SliceInvocation invocation, out int screenedLiteral)
    {
        screenedLiteral = -1;
        if (program.RejectsByFinalAscii(input))
        {
            if (invocation.TryChargeInputStep() != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Final scalar screening breached the invocation budget.");
            return true;
        }
        if (!ReferenceEquals(program, _ownedSliceProgram) ||
            program.RightToLeft || program.HasContiguousAnchor ||
            // Includes internal multiline ^ assertions, whose origin must be
            // preserved by the scalar program.
            _preparedRegex.Features.HasAbsoluteStartAnchor ||
            _preparedRegex.SearchPlan.CanGuideFallbackStarts) return false;
        ref readonly var prefilter = ref _preparedRegex.SearchPlan.RequiredPrefilterSearcher;
        if (prefilter.Kind is not (PreparedSearcherKind.ExactLiteral or PreparedSearcherKind.MultiLiteral)) return false;
        // The existing planner proves this literal is necessary. Search the
        // complete caller-valid subject, including lookbehind context.
        // The caller still verifies whether a hit is sufficient.
        var breach = prefilter.TryFindMatch(input, 0, input.Length, false, ref invocation, out var match, out var found);
        if (breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Required literal screening breached the invocation budget.");
        if (found && prefilter.Kind == PreparedSearcherKind.ExactLiteral)
            screenedLiteral = match.Index;
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
        if (CanStartNativeLiteralInvocation(cancellationToken, forCount: true))
        {
            var invocation = Utf8SliceInvocation.CreateExclusive(Utf8ExecutionLimits.Unlimited, MatchTimeout, cancellationToken);
            Utf8SliceBufferWork.Checkpoint(ref invocation);
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

        if (_asciiLiteralCountAnchorOffset >= 0 &&
            _preparedRegex.LiteralUtf8 is { } asciiLiteral &&
            Utf8AsciiExactLiteralCountExecutor.TryCount(
                input,
                asciiLiteral,
                _asciiLiteralCountAnchorOffset,
                out var asciiLiteralCount))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.VectorAsciiLiteralCount);
            return asciiLiteralCount;
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

        return _compiledEngineRuntime.Count(input, CreateExecutionBudget());
    }

    /// <summary>Owned scalar count from an admitted byte boundary.</summary>
    private int OwnedSliceCountFromByte(
        ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, CancellationToken token)
    {
        var invocation = Utf8SliceInvocation.CreateExclusive(InvocationLimits, MatchTimeout, token);
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
        if (Utf8SliceRunner.TryCountTerminalScalar(program, input, startByte, ref invocation, out var terminal))
        {
            if (terminal.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Terminal class count breached the invocation budget.");
            return terminal.Count;
        }
        // Literal search needs no capture slots or continuation frames. A
        // boundary can use this sliced-input path only at the subject origin.
        if ((HasFiniteExecutionLimits || invocation.Ledger.HasMemoryAllowance) &&
            CanUseNativeLiteralSearch(program, maximumAlternatives: 8, allowBoundaries: startByte == 0))
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
        if (RejectMissingRequiredInput(input, program, ref invocation, out _)) return 0;
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

    // Whole-match operations need slots only when execution reads them.
    // The lowered program includes reads in nested assertions.
    internal bool CanOmitValueCaptureSlots => GetValueOperationCaptureSlotCount(_ownedSliceProgram) == 0;

    private static int GetValueOperationCaptureSlotCount(Utf8SliceProgram program)
        => program.IsNonBacktracking || !program.ReadsCaptures ? 0 : program.GroupCount;

    private static Utf8SliceSearchOutcome FindOwnedSliceValue(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int startByte, ref Utf8SliceInvocation invocation,
        in Utf8SearchPlan search, bool existenceOnly = false, int screenedLiteral = -1)
    {
        var groupCount = GetValueOperationCaptureSlotCount(program);
        return groupCount == 0
            ? Utf8SliceRunner.TryFindFirst(program, input, startByte, [], [],
                false, ref invocation, search, existenceOnly, screenedLiteral)
            : FindOwnedSliceValueWithSlots(program, groupCount, input, startByte, ref invocation,
                search, existenceOnly, screenedLiteral);
    }

    private static Utf8SliceSearchOutcome FindOwnedSliceValueWithSlots(Utf8SliceProgram program, int groupCount,
        ReadOnlySpan<byte> input, int startByte, ref Utf8SliceInvocation invocation,
        in Utf8SearchPlan search, bool existenceOnly, int screenedLiteral)
    {
        var slots = Utf8SliceRunner.RentSlots(groupCount, ref invocation);
        try
        {
            return Utf8SliceRunner.TryFindFirst(program, input, startByte, slots.Starts, slots.Ends,
                false, ref invocation, search, existenceOnly, screenedLiteral);
        }
        finally { Utf8SliceRunner.ReleaseSlots(slots, ref invocation); }
    }

    private Utf8CaptureRange MatchCore(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (HasFiniteExecutionLimits) return OwnedSliceMatch(input, _ownedSliceProgram, cancellationToken);
        if (CanStartNativeLiteralInvocation(cancellationToken))
        {
            var invocation = Utf8SliceInvocation.CreateExclusive(Utf8ExecutionLimits.Unlimited, MatchTimeout, cancellationToken);
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            ref readonly var plan = ref _preparedRegex.SearchPlan;
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

        if (TryMatchDirectWithoutValidation(input, out var directMatch))
        {
            return directMatch;
        }

        var runtime = _compiledEngineRuntime;
        if (runtime.SupportsUtf8MatchProbe)
            return runtime.TryMatchUtf8Probe(input, out var probeMatch) ? probeMatch : default;

        return runtime.Match(input, CreateExecutionBudget());
    }

    private Utf8CaptureRange OwnedSliceMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, CancellationToken token)
    {
        var invocation = Utf8SliceInvocation.CreateExclusive(InvocationLimits, MatchTimeout, token);
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
        var outcome = FindOwnedSliceValue(program, input, startByte, ref invocation, _preparedRegex.SearchPlan);
        if (outcome.Breach != Utf8ExecutionLimitKind.None)
            throw new InvalidOperationException("Owned slice execution breached the invocation budget.");
        invocation.Budget.ThrowIfCancellationRequested();
        return outcome.Found
            ? Utf8SliceBufferWork.CreateMatchRange(outcome.StartByte, outcome.EndByte, ref invocation)
            : default;
    }

    private struct CaptureCheckFrame(Utf8TreeNode node)
    {
        internal readonly Utf8TreeNode Node = node;
        internal int NextChild;
    }

    private static bool ContainsCapture(Utf8TreeNode node)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var budget = construction.Budget;
        using var frames = Utf8ConstructionFrames<CaptureCheckFrame>.Create(budget, new(node));
        while (frames.Count != 0)
        {
            budget.ChargeWork(1);
            var index = frames.Count - 1;
            var frame = frames[index];
            if (frame.Node.Kind == Utf8TreeNodeKind.Capture && frame.Node.M > 0) return true;
            if (frame.NextChild < frame.Node.ChildCount)
            {
                var child = frame.Node.Child(frame.NextChild++);
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

        if (GetOwnedSliceProgram(input) is { } ownedProgram)
        {
            return OwnedSliceEnumerateMatches(input, ownedProgram, CancellationToken.None)
                .WithTimeoutMapping(input, this);
        }

        var subject = Utf8ValidatedInput.BorrowBytes(input);
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
        using var programUse = _programMemory?.Enter();
        var cursor = startOffsetInBytes is { } start
            ? CreateOwnedByteOffsetEnumerator(input, start, cancellationToken)
            : cancellationToken.CanBeCanceled ? EnumerateMatchesWithToken(input, cancellationToken) : EnumerateMatchesWithoutToken(input);
        return cursor.WithProgramMemory(_programMemory);
    }
    /// <summary>Enumerates at most the requested number of split segments.</summary>
    private Utf8ValueSplitEnumerator EnumerateSplitsWithoutToken(ReadOnlySpan<byte> input, int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (GetOwnedSliceProgram(input) is { } ownedProgram)
            return OwnedSliceEnumerateSplits(input, ownedProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);
        // Eligible literal RTL splits use the reverse cursor and return
        // segments in encounter order from the end of the subject.
        if ((count == 0 || count > 1) &&
            UsesRightToLeft() &&
            _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase &&
            _preparedRegex.LiteralUtf8 is { Length: > 0 } rtlSplitLiteral &&
            !_preparedRegex.SearchPlan.HasBoundaryRequirements &&
            !_preparedRegex.SearchPlan.HasTrailingLiteralRequirement)
        {
            var rtlBudget = CreateExecutionBudget();
            return new Utf8ValueSplitEnumerator(
                input,
                new Utf8OperationMatchCursor(input, _preparedRegex, rtlSplitLiteral, _preparedRegex.ExecutionKind, rtlBudget, rightToLeft: true),
                count == 0 ? int.MaxValue : count,
                rightToLeft: true)
                .WithTimeoutMapping(this);
        }

        if (UsesRightToLeft())
            return OwnedSliceEnumerateSplits(input, _ownedSliceProgram, count == 0 ? int.MaxValue : count, CancellationToken.None);

        if (count > 0 && TryGetSmallAsciiLiteralFamilySearch(out var smallAsciiLiteralFamilySearch))
        {
            return new Utf8ValueSplitEnumerator(
                input,
                smallAsciiLiteralFamilySearch,
                count)
                .WithTimeoutMapping(this);
        }


        var budget = CreateExecutionBudget();
        // Anchored literals contribute at most one split point.
        var isAnchoredDirectSplitFamily = _anchoredLiteral.LiteralUtf8 is { Length: > 0 } &&
            (_anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix ||
            _anchoredLiteral.IsExactSuffix ||
            _anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd);
        if (count > 0 &&
            (CanUseNativeSplit() || isAnchoredDirectSplitFamily))
        {
            return CreateSplitEnumeratorViaCompiledEngine(input, count, budget)
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
        using var programUse = _programMemory?.Enter();
        if (!cancellationToken.CanBeCanceled) return EnumerateSplitsWithoutToken(input, count).WithProgramMemory(_programMemory);
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        return OwnedSliceEnumerateSplits(input, _ownedSliceProgram, count == 0 ? int.MaxValue : count, cancellationToken).WithProgramMemory(_programMemory);
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

    /// <summary>Replaces all matches using a .NET replacement pattern encoded as well-formed UTF-8, observing cancellation during validation, matching and required result processing.</summary>
    /// <param name="input">The well-formed UTF-8 input to match against.</param>
    /// <param name="replacementPatternUtf8">The replacement pattern encoded as well-formed UTF-8.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        try
        {
            return ReplaceUtf8(input, replacementPatternUtf8, cancellationToken);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private byte[] ReplaceUtf8(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, CancellationToken cancellationToken)
    {
        var deadline = CreateExecutionBudget();
        var control = new ReplacementPreparationControl(deadline, cancellationToken);
        ValidateReplacementTemplateUtf8(replacementPatternUtf8, control);

        var ownedProgram = _preparedRegex.HasLiteralPlan ? GetOwnedSliceProgram(input, cancellationToken) : _ownedSliceProgram;
        if (ownedProgram is null && (input.IsEmpty || _ownedSliceProgram.CanMatchEmpty)) ownedProgram = _ownedSliceProgram;
        if (ownedProgram is not null)
        {
            return OwnedSliceReplace(input, ownedProgram,
                GetParsedReplacement(replacementPatternUtf8, control), Utf8SliceReplacementResultKind.Bytes, default, cancellationToken, deadline).Bytes;
        }

        if (replacementPatternUtf8.IndexOf((byte)'$') < 0)
        {
            return ReplaceLiteralBytesCore(input, replacementPatternUtf8, deadline);
        }

        var replacementPattern = GetParsedReplacement(replacementPatternUtf8, control);
        if (UsesRightToLeft())
            return OwnedSliceReplace(input, _ownedSliceProgram, replacementPattern,
                Utf8SliceReplacementResultKind.Bytes, default, cancellationToken, deadline).Bytes;
        var budget = deadline;
        if (TryGetExactLiteralReplacementBytes(replacementPattern, out var exactLiteralReplacementBytes))
        {
            return ReplaceLiteralBytesCore(input, exactLiteralReplacementBytes, deadline);
        }

        if (TryReplaceViaNativePlan(input, replacementPattern, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacementPattern, out var replacementBytes))
        {
            return OwnedSliceReplace(input, _ownedSliceProgram, replacementPattern, Utf8SliceReplacementResultKind.Bytes,
                default, cancellationToken, deadline).Bytes;
        }

        return ReplaceLiteralBytesCore(input, replacementBytes, deadline);
    }

    private OperationStatus TryReplaceUtf8Core(
        ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken)
    {
        var deadline = CreateExecutionBudget();
        var control = new ReplacementPreparationControl(deadline, cancellationToken);
        ValidateReplacementTemplateUtf8(replacementPatternUtf8, control);

        // The owned writer stages subject/destination overlap. Reuse the
        // subject selection for parsed templates as well as literal output.
        var ownedProgram = !_preparedRegex.HasLiteralPlan || input.IsEmpty || input.Overlaps(destination)
            ? _ownedSliceProgram
            : GetOwnedSliceProgram(input, cancellationToken);
        if (ownedProgram is null && replacementPatternUtf8.IndexOf((byte)'$') < 0)
        {
            // The worker consumes the template synchronously. Own it only when
            // writing the destination could overwrite bytes still needed later.
            ReadOnlySpan<byte> directReplacementBytes = replacementPatternUtf8.Overlaps(destination)
                ? replacementPatternUtf8.ToArray()
                : replacementPatternUtf8;
            return TryReplaceLiteralBytesCore(input, directReplacementBytes, destination, out bytesWritten, deadline);
        }

        var replacementPattern = GetParsedReplacement(replacementPatternUtf8, control);
        if (UsesRightToLeft()) ownedProgram = _ownedSliceProgram;
        if (ownedProgram is null)
            return TryReplaceCore(input, replacementPattern, destination, out bytesWritten, cancellationToken, deadline);

        var result = OwnedSliceReplace(input, ownedProgram, replacementPattern, Utf8SliceReplacementResultKind.Destination,
            destination, cancellationToken, deadline);
        bytesWritten = result.BytesWritten;
        return result.Status;
    }

    /// <summary>Attempts to apply a UTF-8 replacement template to a caller-provided destination, with cancellation during template processing, matching and output.</summary>
    /// <param name="input">The well-formed UTF-8 subject. Keep the borrowed bytes unchanged during the operation.</param>
    /// <param name="replacementPatternUtf8">The replacement pattern encoded as well-formed UTF-8.</param>
    /// <param name="destination">The destination buffer for the UTF-8 replacement result.</param>
    /// <param name="bytesWritten">When this method returns, the number of bytes written to the destination.</param>
    /// <param name="cancellationToken">The token to observe at entry and during bounded operation checkpoints.</param>
    /// <param name="workingMemoryAllowance">Admission for covered working storage. Null uses the program allowance when present. The engine's numeric limits still apply.</param>
    /// <exception cref="OperationCanceledException">Cancellation was requested through the supplied token.</exception>
    public OperationStatus TryReplace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8, Span<byte> destination, out int bytesWritten, CancellationToken cancellationToken = default, IUtf8MemoryAllowance? workingMemoryAllowance = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        using var programUse = _programMemory?.Enter();
        workingMemoryAllowance ??= _programMemory?.Allowance;
        if (workingMemoryAllowance is not null)
            return TryReplaceFunded(input, replacementPatternUtf8, destination, out bytesWritten, cancellationToken, workingMemoryAllowance);
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

    internal bool CanUseNativeSplit()
    {
        // Split segments are match-delimited gaps. Grouped literal shapes use
        // the same group-agnostic cursors as ungrouped literals.
        return CompiledEngineKind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => true,
            Utf8CompiledEngineKind.LiteralFamily => true,
            _ => false
        };
    }

    internal bool TryGetSmallAsciiLiteralFamilySearch(out PreparedSmallAsciiLiteralFamilySearch search)
    {
        if (MatchTimeout != Regex.InfiniteMatchTimeout ||
            _preparedRegex.ExecutionPattern is null || _preparedRegex.ExecutionPattern.IndexOf('(') >= 0 ||
            CompiledEngineKind != Utf8CompiledEngineKind.LiteralFamily ||
            _program.CompiledEngineRuntime is not { } literalFamilyRuntime)
        {
            search = default;
            return false;
        }

        return literalFamilyRuntime.TryGetSmallAsciiLiteralFamilyPrimitive(out search);
    }

    private bool UsesRightToLeft()
    {
        return (Options & RegexOptions.RightToLeft) != 0;
    }

}
