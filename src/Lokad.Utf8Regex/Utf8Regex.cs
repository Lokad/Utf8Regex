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
using System.Text;
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
    private readonly bool _canUseUnmeteredScalarBoundaryLiterals;
    private readonly Utf8EmittedLiteralFamilyCounter? _scalarBoundaryLiteralFamilyCounter;
    private readonly bool _requiresKelvinSignFallback;
    private readonly Utf8InvariantCyrillicLiteralCountStrategy? _invariantCyrillicLiteralCountStrategy;
    internal readonly Utf8PackedNibbleLiteralFamilyCountStrategy? _packedNibbleLiteralFamilyCountStrategy;
    private readonly bool _hasCaptures;
    private readonly int _wholeLiteralCaptureNumber;
    private readonly Utf8SliceProgram _ownedSliceProgram;
    private readonly Utf8ReplacementPlanCache _replacementCache;

    // PCRE2-INTEGRATION-POINT: its outer owner decides whether the shared
    // program may warm a branch proof after the construction scope closes.
    internal static Utf8Regex PrepareSharedBranchSearch(Utf8ConstructionPattern pattern, RegexOptions options,
        Utf8RegexPolicy policy, bool sharedBranchTier) => new(pattern, options, policy, default,
            explicitCompile: false, sharedBranchTier: sharedBranchTier);

    private Utf8Regex(Utf8ConstructionPattern pattern, RegexOptions options, Utf8RegexPolicy? policy,
        CancellationToken token, bool explicitCompile, bool sharedBranchTier = false)
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
        var controlledConstruction = account is not null || constructionPolicy != default ||
            constructionTimeout != Timeout.InfiniteTimeSpan;
        var ownsConstruction = explicitCompile || controlledConstruction;
        var canTierUp = !controlledConstruction && (sharedBranchTier || !Utf8ConstructionScope.IsActive) &&
            ((options & RegexOptions.Compiled) == 0 || !token.CanBeCanceled);
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
            InvocationLimits = new(resourcePolicy);
            HasFiniteExecutionLimits = resourcePolicy.MaxWorkSteps.HasValue || resourcePolicy.MaxDepth.HasValue ||
                resourcePolicy.MaxWorkingBytes.HasValue || resourcePolicy.MaxResultBytes.HasValue;
            construction.Budget.CheckBytes(ProgramFixedMetadataBytes);
            construction.Budget.ChargeWork(1);
            construction.Budget.ReserveBytes(ProgramFixedMetadataBytes);

            var proofTier = canTierUp && matchTimeout == Regex.InfiniteMatchTimeout && !HasFiniteExecutionLimits
                ? sharedBranchTier ? Utf8BoundedProgramEmitter.Compilation.TieredBranches : Utf8BoundedProgramEmitter.Compilation.Tiered
                : Utf8BoundedProgramEmitter.Compilation.None;
            _program = Utf8RegexProgram.Compile(pattern, options, prepareRuntime: !HasFiniteExecutionLimits, out var root, proofTier);
            var preparedRegex = _program.PreparedRegex;
            _asciiLiteralCountAnchorOffset =
                matchTimeout == Regex.InfiniteMatchTimeout &&
                IsPlainAsciiLiteral(pattern, preparedRegex, options) &&
                preparedRegex.LiteralUtf8 is { } asciiLiteral
                    ? Utf8AsciiExactLiteralCountExecutor.SelectRarestAnchorOffset(asciiLiteral)
                    : -1;
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
            if (matchTimeout == Regex.InfiniteMatchTimeout &&
                _program.CompiledEngineRuntime is { } literalRuntime && HasScalarBoundaryLiteralSearch(ownedSliceProgram))
            {
                _canUseUnmeteredScalarBoundaryLiterals = true;
                _scalarBoundaryLiteralFamilyCounter = literalRuntime._emittedLiteralFamilyCounter;
            }
            var usesLiteralEngine = preparedRegex.HasLiteralPlan;
            var captureRoot = usesLiteralEngine || _anchoredLiteral.HasValue ? root : null;
            _hasCaptures = ownedSliceProgram.GroupCount != 0 &&
                (captureRoot is null || preparedRegex.Features.HasCaptureNodes);
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

    private bool HasFiniteExecutionLimits { get; }

    private Utf8ExecutionLimits InvocationLimits { get; }

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
    public Utf8ResourcePolicy ResourcePolicy => InvocationLimits.ToResourcePolicy();

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

    internal ref readonly Utf8SearchPlan SearchPlan => ref _preparedRegex.SearchPlan;
    internal int RequiredLiteralContinuation => _preparedRegex.RequiredLiteralContinuation;
    internal byte[]? RequiredLiteralTerminalSuffix => _preparedRegex.RequiredLiteralTerminalSuffix;

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
             // The prepared boundary-only literal route decodes just the
             // neighboring scalars. Other native predicates still need ASCII.
             (!scalarAlignedLiteral ||
                 _preparedRegex.SearchPlan.HasBoundaryRequirements && !_canUseUnmeteredScalarBoundaryLiterals) &&
                !Ascii.IsValid(input))
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
        if (input.Length > MaximumFoldedSearchLiteralLength && _requiresKelvinSignFallback &&
            TryResolveFoldedLiteralMatch(input, cancellationToken, out var folded)) return folded.Success;
        if (GetOwnedSliceProgram(input, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceIsMatch(input, ownedProgram, cancellationToken);
        }

        var runtime = _compiledEngineRuntime;
        if (runtime.TryMatchDirect(input, out var directMatch))
        {
            return directMatch.Success;
        }

        return runtime.IsMatch(input, CreateExecutionBudget());
    }

    private bool OwnedSliceIsMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, CancellationToken token)
    {
        Utf8SliceInvocation.InitializeExclusive(out var invocation, InvocationLimits, MatchTimeout, token, null);
        try
        {
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
        ref readonly var search = ref _preparedRegex.SearchPlan;
        if (_preparedRegex.RequiredLiteralProvesMatch && ReferenceEquals(program, _ownedSliceProgram))
        {
            // The prepared literal is sufficient for existence. Its finder
            // checks the invocation before scanning and before publishing.
            var breach = search.RequiredPrefilterSearcher.TryFindMatch(input, startByte, input.Length, false,
                ref invocation, out _, out var found);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Required literal proof breached the invocation budget.");
            return found;
        }

        Utf8SliceBufferWork.Checkpoint(ref invocation);
        if (program.TerminalRoot == Utf8SliceTerminalRootKind.DelimitedClassRepeat)
        {
            var range = Utf8SliceRunner.FindDelimitedClassRepeat(program, input, startByte, ref invocation);
            if (range.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
            return range.Found;
        }
        if (program.TerminalRoot == Utf8SliceTerminalRootKind.AsciiSuffixAlternatives &&
            Utf8SliceRunner.TryScanTerminalAsciiSuffixAlternatives(program, input, startByte, firstOnly: true,
                ref invocation, out var suffixes, out _))
        {
            if (suffixes.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
            return suffixes.Count != 0;
        }
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
        if (program.IsNonBacktracking && program.NonBacktrackingEpsilonAncestors.Length != 0 ||
            !program.RightToLeft &&
            Utf8SliceRunner.CanSearchOrdinaryOrdered(program, input.Length - startByte, search, ref invocation))
        {
            if (input.Length - startByte >= 256 && Utf8SliceRunner.TryScanLazyAsciiSuffix(program, input,
                startByte, existenceOnly: true, ref invocation, out var suffix))
            {
                if (suffix.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                return suffix.Count != 0;
            }
            if (!program.IsNonBacktracking && search.RequiredPrefilterSearcher.Kind == PreparedSearcherKind.MultiLiteral && screenedLiteral >= startByte)
            {
                var proof = Utf8SliceRunner.TryProveScreenedOrderedClassFamily(program, input, search,
                    startByte, screenedLiteral, ref invocation, out var proved);
                if (proof != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                if (proved) return true;
            }
            var breach = Utf8SliceRunner.IsMatchPackedNonBacktracking(program, input, startByte, ref invocation, out var found);
            if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
            return found;
        }
        if (search.RequiredPrefilterSearcher.Kind != PreparedSearcherKind.ExactLiteral) screenedLiteral = -1;
        if (screenedLiteral >= startByte && search.HasTerminalRepeatPrefix)
        {
            var breach = Utf8SliceRunner.TryProveScreenedTerminalRepeatPrefix(program, input, search,
                startByte, screenedLiteral, ref invocation, out var found);
            if (breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
            if (found) return true;
        }
        if (screenedLiteral < startByte || _preparedRegex.RequiredLiteralContinuation is not (> 0 and var continuation) ||
            !Utf8SliceRunner.TryFindPreparedLiteralContinuation(program, input, ref startByte,
                search.RequiredPrefilterSearcher.LiteralSearch, screenedLiteral, continuation, ref invocation, out var outcome,
                terminalSuffix: RequiredLiteralTerminalSuffix))
            outcome = FindOwnedSliceValue(program, input, startByte, ref invocation, search,
                existenceOnly: true, screenedLiteral: screenedLiteral);
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
        if (found) screenedLiteral = match.Index;
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
        // The prepared boundary counter admits at most eight alternatives.
        // For short automaton families the shared finder can reject a start
        // on its leading boundary before comparing any literals.
        if (CanStartNativeLiteralInvocation(cancellationToken, forCount: true) ||
            !cancellationToken.CanBeCanceled && _scalarBoundaryLiteralFamilyCounter is not null &&
            _preparedRegex.SearchPlan.MultiLiteralSearch is
                { Kind: PreparedMultiLiteralKind.ExactAutomaton, AutomatonSearch.LongestLength: <= 8 })
        {
            var invocation = Utf8SliceInvocation.CreateExclusive(Utf8ExecutionLimits.Unlimited, MatchTimeout, cancellationToken);
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(_preparedRegex.SearchPlan,
                input, UsesRightToLeft(), ref invocation, out var count);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Native literal execution exceeded {breach}.");
            return count;
        }

        if (!cancellationToken.CanBeCanceled && _scalarBoundaryLiteralFamilyCounter is { } boundaryCounter)
        {
            // Check the scalar neighbors of each exact literal candidate.
            // Unrelated bytes cannot affect its boundary requirements.
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.CompiledScalarBoundaryLiteralFamilyCount);
            return boundaryCounter.Count(input);
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
        Utf8SliceInvocation.InitializeExclusive(out var invocation, InvocationLimits, MatchTimeout, token, null);
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
        // A complete scalar run, including a grouped repeat, can be counted
        // without preparing capture slots or entering candidate search.
        if (input.Length - startByte >= 256 && Utf8SliceRunner.TryScanLazyAsciiSuffix(program, input,
                startByte, existenceOnly: false, ref invocation, out var terminal) ||
            Utf8SliceRunner.TryCountTerminalScalar(program, input, startByte, ref invocation, out terminal))
        {
            if (terminal.Breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Terminal class count breached the invocation budget.");
            return terminal.Count;
        }
        var countedPrefix = 0;
        // Invariant folded literals can share the native search until an
        // actual Kelvin sign requires scalar confirmation.
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
            if (_requiresKelvinSignFallback && !invocation.IsUnmetered &&
                _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCase &&
                _preparedRegex.SearchPlan.LiteralSearch is { Length: > 0 and <= 32 } literal)
            {
                var breach = Utf8BackendInstructionExecutor.TryCountSingleLiteral(literal, input, ref invocation,
                    out countedPrefix, out var resume, stopAtKelvin: true);
                if (breach != Utf8ExecutionLimitKind.None)
                    throw new InvalidOperationException("Folded literal count exceeded the invocation budget.");
                if (resume < 0) return countedPrefix;
                startByte = resume;
            }
            else if (!_requiresKelvinSignFallback || !HasMeteredKelvinSign(input, ref invocation))
            {
                var breach = Utf8BackendInstructionExecutor.TryCountNativeLiteralMatches(
                    _preparedRegex.SearchPlan, input, false, ref invocation, out var count);
                if (breach != Utf8ExecutionLimitKind.None)
                    throw new InvalidOperationException("Folded literal count exceeded the invocation budget.");
                return count;
            }
        }
        if (RejectMissingRequiredInput(input, program, ref invocation, out var screenedLiteral)) return countedPrefix;
        if (_preparedRegex.SearchPlan.RequiredPrefilterSearcher.Kind != PreparedSearcherKind.ExactLiteral) screenedLiteral = -1;
        var classWindow = program.LeadingClassWindowProof;
        if (screenedLiteral >= 0 && input.Length - startByte >= 256 &&
            classWindow.Completion is Utf8SliceClassWindowCompletion.TerminalRuns or Utf8SliceClassWindowCompletion.CapturedRepeat &&
            _preparedRegex.SearchPlan.RequiredPrefilterSearcher.LiteralSearch is var delimiter &&
            delimiter.Length == classWindow.LiteralLength &&
            RequiredLiteralContinuation == program.LeadingClassDelimiter.LiteralIndex + delimiter.Length &&
            invocation.Budget.Limits.MaxDepth is 0 or >= Utf8SliceRunner.BranchedPrefixInstructionLimit &&
            Utf8BytePosition.IsScalarBoundary(input, startByte))
        {
            var proved = Utf8SliceRunner.CountTerminalClassWindows(program, input, delimiter.Needle,
                ref startByte, screenedLiteral, ref invocation);
            if (proved.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
            countedPrefix += proved.Count;
            if (startByte == input.Length) return countedPrefix;
            screenedLiteral = -1;
        }
        if (ReferenceEquals(program, _ownedSliceProgram) && RequiredLiteralContinuation is > 0 and var continuation &&
            _preparedRegex.SearchPlan.RequiredPrefilterSearcher.LiteralSearch is var prefix && prefix.Length == continuation &&
            program.Instructions[continuation] is { Kind: Utf8SliceInstructionKind.RepeatGreedy, HasSingleAtomBody: true } repeat)
        {
            var suffix = _preparedRegex.RequiredLiteralTerminalSuffix;
            // Preparation admits a zero-minimum prefix here only for an
            // optional ASCII literal followed by a provable class continuation.
            if ((suffix is not null || screenedLiteral >= 0 && repeat is { RepeatMinimum: 0, RepeatMaximum: 1 }) &&
                invocation.Budget.Limits.MaxDepth is 0 or >= Utf8SliceRunner.BranchedPrefixInstructionLimit &&
                Utf8BytePosition.IsScalarBoundary(input, startByte))
            {
                var proved = Utf8SliceRunner.CountPreparedLiteralRanges(program, input, prefix.Needle,
                    suffix, ref startByte, ref screenedLiteral, ref invocation);
                if (proved.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                countedPrefix += proved.Count;
                if (startByte == input.Length) return countedPrefix;
            }
            else if (repeat is { RepeatMinimum: > 0, RepeatMaximum: long.MaxValue } &&
                program.Instructions[repeat.A].RepeatExitRejectsShorterEndpoints)
            {
                // Count can consume proved ranges without preparing attempt storage.
                // Keep the first unresolved origin and the same invocation for fallback.
                while (Utf8SliceRunner.TryFindPreparedLiteralRange(program, input, ref startByte,
                    prefix, ref screenedLiteral, continuation, ref invocation, out var range))
                {
                    if (range.Breach != Utf8ExecutionLimitKind.None) throw invocation.Budget.CreateResourceFailure();
                    if (!range.Found) return countedPrefix;
                    countedPrefix++;
                    startByte = range.EndByte;
                }
            }
        }
        var slots = Utf8SliceSlots.Empty;
        try
        {
            slots = Utf8SliceRunner.RentSlots(GetValueOperationCaptureSlotCount(program), ref invocation);
            var outcome = Utf8SliceRunner.CountFromBytes(program, input, startByte, slots.Starts, slots.Ends,
                ref invocation, _preparedRegex.SearchPlan, screenedLiteral: screenedLiteral);
            if (outcome.Breach != Utf8ExecutionLimitKind.None)
            {
                throw new InvalidOperationException("Owned slice execution breached the invocation budget.");
            }

            return countedPrefix + outcome.Count;
        }
        finally
        {
            Utf8SliceRunner.ReleaseSlots(slots, ref invocation);
        }
    }

    private static bool HasMeteredKelvinSign(ReadOnlySpan<byte> input, ref Utf8SliceInvocation invocation)
    {
        // Include two lookahead bytes for a three-byte scalar at the edge.
        const int blockSize = 2048;
        for (var offset = 0; offset < input.Length;)
        {
            var breach = invocation.TryChargeInputStep();
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException("Folded literal screening exceeded the invocation budget.");
            var length = Math.Min(blockSize, input.Length - offset);
            if (ContainsKelvinSign(input.Slice(offset, Math.Min(length + 2, input.Length - offset)))) return true;
            offset += length;
        }
        return false;
    }

    // Whole-match operations need slots only when execution reads them.
    // The lowered program includes reads in nested assertions.
    internal bool CanOmitValueCaptureSlots => GetValueOperationCaptureSlotCount(_ownedSliceProgram) == 0;

    private static int GetValueOperationCaptureSlotCount(Utf8SliceProgram program)
        => program.IsNonBacktracking || !program.ReadsCaptures ? 0 : program.GroupCount;

    private Utf8SliceSearchOutcome FindOwnedSliceValue(Utf8SliceProgram program,
        ReadOnlySpan<byte> input, int startByte, ref Utf8SliceInvocation invocation,
        in Utf8SearchPlan search, bool existenceOnly = false, int screenedLiteral = -1)
    {
        var groupCount = GetValueOperationCaptureSlotCount(program);
        return groupCount == 0
            ? Utf8SliceRunner.TryFindFirst(program, input, startByte, [], [],
                false, ref invocation, search, existenceOnly, screenedLiteral,
                !existenceOnly && ReferenceEquals(program, _ownedSliceProgram) ? RequiredLiteralContinuation : 0,
                RequiredLiteralTerminalSuffix)
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
        if (HasFiniteExecutionLimits || !_preparedRegex.HasLiteralPlan)
            return OwnedSliceMatch(input, _ownedSliceProgram, cancellationToken);
        return MatchLiteral(input, cancellationToken);
    }

    private Utf8CaptureRange MatchLiteral(ReadOnlySpan<byte> input, CancellationToken cancellationToken)
    {
        if (CanStartNativeLiteralInvocation(cancellationToken))
            return MatchNativeLiteral(input, cancellationToken);
        if (input.Length > MaximumFoldedSearchLiteralLength && _requiresKelvinSignFallback &&
            TryResolveFoldedLiteralMatch(input, cancellationToken, out var folded)) return folded;
        if (GetOwnedSliceProgram(input, cancellationToken) is { } ownedProgram)
        {
            return OwnedSliceMatch(input, ownedProgram, cancellationToken);
        }

        var runtime = _compiledEngineRuntime;
        if (runtime.TryMatchDirect(input, out var directMatch))
        {
            return directMatch;
        }

        return runtime.Match(input, CreateExecutionBudget());
    }

    private Utf8CaptureRange OwnedSliceMatchFromByte(ReadOnlySpan<byte> input, Utf8SliceProgram program, int startByte, CancellationToken token)
    {
        Utf8SliceInvocation.InitializeExclusive(out var invocation, InvocationLimits, MatchTimeout, token, null);
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
        if (outcome.Found)
            return Utf8SliceBufferWork.CreateMatchRange(outcome.StartByte, outcome.EndByte, ref invocation);
        invocation.Budget.ThrowIfCancellationRequested();
        return default;
    }

    /// <summary>Enumerates non-overlapping matches in the well-formed UTF-8 input.</summary>
    private Utf8ValueMatchEnumerator EnumerateMatchesWithoutToken(ReadOnlySpan<byte> input)
    {
        if (HasFiniteExecutionLimits)
            return OwnedSliceEnumerateMatches(input, _ownedSliceProgram, CancellationToken.None).WithTimeoutMapping(input, this);
        // The classifier admits complete ASCII case fibers for folded literals.
        ref readonly var anchored = ref _anchoredLiteral;
        if (anchored.HasValue)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(
                anchored.Kind is Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix
                    ? Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix
                    : Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input, anchored.LiteralUtf8, CreateExecutionBudget(), anchored.Kind))
                .WithTimeoutMapping(input, this);
        }

        // Exact byte candidates are scalar-aligned. The prepared cursor
        // confirms the scalar neighbors at each candidate boundary.
        if (!_canUseUnmeteredScalarBoundaryLiterals && GetOwnedSliceProgram(input) is { } ownedProgram)
        {
            return OwnedSliceEnumerateMatches(input, ownedProgram, CancellationToken.None)
                .WithTimeoutMapping(input, this);
        }

        if (UsesRightToLeft())
        {
            // The reverse literal cursor starts at the subject end.
            if (_preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or
                    NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase &&
                _preparedRegex.LiteralUtf8 is { Length: > 0 } literal &&
                !_preparedRegex.SearchPlan.HasBoundaryRequirements &&
                !_preparedRegex.SearchPlan.HasTrailingLiteralRequirement)
            {
                var cursor = new Utf8OperationMatchCursor(input, _preparedRegex, literal,
                    _preparedRegex.ExecutionKind, CreateExecutionBudget(), rightToLeft: true);
                return new Utf8ValueMatchEnumerator(cursor).WithTimeoutMapping(input, this);
            }

            return OwnedSliceEnumerateMatches(input, _ownedSliceProgram, CancellationToken.None)
                .WithTimeoutMapping(input, this);
        }

        return new Utf8ValueMatchEnumerator(
            Utf8CompiledOperationCursorFactory.CreateMatchCursor(_preparedRegex, input, CreateExecutionBudget()))
            .WithTimeoutMapping(input, this);
    }

    private Utf8ValueMatchEnumerator OwnedSliceEnumerateMatches(ReadOnlySpan<byte> input, Utf8SliceProgram program, CancellationToken token)
    {
        var invocation = new Utf8SliceInvocation(InvocationLimits, MatchTimeout, token);
        try
        {
            Utf8SliceBufferWork.Checkpoint(ref invocation);
            invocation.ShareStorageForCursor();
            var cursor = !invocation.IsUnmetered && CanUseControlledScalarBoundaryLiteralSearch(program)
                ? Utf8CompiledOperationCursorFactory.CreateInvocationMatchCursor(_preparedRegex, input, invocation, byteFloor: 0)
                : new Utf8OperationMatchCursor(program, input, invocation);
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
        if (_canUseUnmeteredScalarBoundaryLiterals)
        {
            return Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(
                _preparedRegex, input, count == 0 ? int.MaxValue : count, Utf8ExecutionDeadline.Infinite)
                .WithTimeoutMapping(this);
        }
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


        // Anchored literals contribute at most one split point.
        ref readonly var anchored = ref _anchoredLiteral;
        if (count > 0 && anchored.HasValue)
        {
            return new Utf8ValueSplitEnumerator(input,
                    new Utf8OperationMatchCursor(input, anchored.LiteralUtf8, CreateExecutionBudget(), anchored.Kind), count)
                .WithTimeoutMapping(this);
        }
        if (count > 0 && CanUseNativeSplit())
        {
            return Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(_preparedRegex, input, count, CreateExecutionBudget())
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
            var literals = !invocation.IsUnmetered && CanUseControlledScalarBoundaryLiteralSearch(program)
                ? _preparedRegex : null;
            return Utf8OwnedSplitPreparation.Create(program, input, count, ref invocation, literals)
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
