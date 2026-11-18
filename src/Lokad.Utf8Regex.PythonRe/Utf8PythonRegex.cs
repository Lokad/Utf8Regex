using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Provides a culture-invariant CPython <c>re</c>-compatible surface over well-formed UTF-8 input.</summary>
/// <remarks>The adapter preserves the supported Python grammar and capture semantics while exposing UTF-8 byte ranges.</remarks>
public sealed partial class Utf8PythonRegex
{
    /// <summary>Constructs an expression for caller-valid, unchanged UTF-8 subjects.</summary>
    /// <remarks>The immutable policy separates construction and invocation limits/deadlines. Unfunded constructors defer the full-match program; caller-funded construction prepares retained programs before publication. With no explicit construction controls/funding, nested construction borrows its existing owner. Compile always starts an explicit eager construction owner.</remarks>
    public Utf8PythonRegex(string pattern, PythonReCompileOptions options = PythonReCompileOptions.None,
        Utf8RegexPolicy? policy = null)
        : this(new Utf8ConstructionPattern(pattern), options, policy, default, eager: false) { }

    /// <summary>Constructs a strict UTF-8 pattern for caller-valid, unchanged UTF-8 subjects.</summary>
    /// <remarks>The source is borrowed only during construction and copied after admission. Policy settings and lifetime follow the text constructor; subject validation is not a regex operation.</remarks>
    public Utf8PythonRegex(ReadOnlySpan<byte> patternUtf8, PythonReCompileOptions options = PythonReCompileOptions.None,
        Utf8RegexPolicy? policy = null)
        : this(new Utf8ConstructionPattern(patternUtf8), options, policy, default, eager: false) { }

    private static TimeSpan s_defaultMatchTimeout = Timeout.InfiniteTimeSpan;
    private readonly Utf8SliceProgram _scalarProgram;
    private readonly Lazy<Utf8SliceProgram> _lazyScalarFullProgram;
    private readonly Utf8Regex? _utf8Regex;
    private readonly PythonReNameEntry[] _nameEntries;
    private readonly byte[][] _nameEntriesUtf8;
    private readonly IReadOnlyDictionary<string, int> _namedGroups;
    private readonly bool _canMatchEmpty;
    private readonly bool _canUseUtf8IterationFastPath;
    private readonly bool _canUseZeroOffsetUtf8SearchValueFastPath;
    private readonly bool _canUseZeroOffsetUtf8ValueFastPath;
    private const int ReplacementPlanCacheCapacity = 16;
    private readonly Utf8BoundedPreparationCache<object, PythonReReplacementPlan> _replacementPlanCache;
    private readonly object _patternSource;

    /// <summary>Owns constructor/factory admission and publishes one reusable program.</summary>
    private Utf8PythonRegex(Utf8ConstructionPattern patternSource, PythonReCompileOptions options,
        Utf8RegexPolicy? policy, CancellationToken cancellationToken, bool eager)
    {
        cancellationToken.ThrowIfCancellationRequested();
        policy ??= Utf8RegexPolicy.Default;
        var resourcePolicy = policy.ResourcePolicy;
        var constructionPolicy = policy.ConstructionPolicy;
        var constructionTimeout = policy.ConstructionTimeout ?? Timeout.InfiniteTimeSpan;
        var programMemoryAllowance = policy.ProgramMemoryAllowance;
        constructionPolicy.Validate(nameof(policy));
        resourcePolicy.Validate(nameof(policy));
        Utf8MatchTimeout.Validate(constructionTimeout, nameof(policy));
        PythonReCompileValidator.Validate(options);
        MatchTimeout = Utf8MatchTimeout.Validate(policy.MatchTimeout ?? DefaultMatchTimeout, nameof(policy));
        ArgumentNullException.ThrowIfNull(patternSource.Text, "pattern");
        var explicitConstruction = eager || programMemoryAllowance is not null ||
            constructionPolicy != default || constructionTimeout != Timeout.InfiniteTimeSpan;
        using var construction = !explicitConstruction ? Utf8ConstructionScope.Enter()
            : programMemoryAllowance is null
                ? Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, cancellationToken)
                : Utf8ConstructionScope.Enter(constructionPolicy, constructionTimeout, cancellationToken, programMemoryAllowance);
        try
        {
            if (patternSource.IsUtf8)
            {
                PythonReParser.ValidateNativeSource(patternSource.Utf8, construction.Budget);
                // This retained copy also supplies the parser's borrow. Admission
                // precedes allocation; caller reuse after compilation is harmless.
                var bytes = construction.Budget.AllocateArray<byte>(patternSource.Length, false);
                construction.Budget.ChargeWork((ulong)bytes.Length);
                patternSource.Utf8.CopyTo(bytes);
                _patternSource = bytes;
            }
            else _patternSource = patternSource.Text;
            Options = options;
            ResourcePolicy = resourcePolicy;
            HasFiniteExecutionLimits = resourcePolicy.MaxWorkSteps.HasValue || resourcePolicy.MaxDepth.HasValue ||
                resourcePolicy.MaxWorkingBytes.HasValue || resourcePolicy.MaxResultBytes.HasValue;

            var parser = PythonReParser.Create(_patternSource);
            var parseResult = parser.Parse(options);
            EffectiveOptions = (parseResult.Options & PythonReCompileOptions.Ascii) != 0
                ? parseResult.Options
                : parseResult.Options | PythonReCompileOptions.Unicode;
            _canMatchEmpty = PythonReTranslator.CanMatchEmpty(parseResult.Root, parseResult);
            _canUseUtf8IterationFastPath = PythonReTranslator.CanUseUtf8IterationFastPath(parseResult.Root);
            var isExactLiteral = PythonReTranslator.IsExactLiteral(parseResult.Root);
            // Nullable iteration always uses the Python scalar program. Only the
            // exact empty literal has a separate Core value-operation consumer.
            var prepareCore = !_canMatchEmpty || isExactLiteral;
            var translation = PythonReTranslator.Translate(parseResult, prepareCore);
            _scalarProgram = PythonReScalarCompiler.Compile(parseResult);
            // Failed preparation is retryable: a caller's cancellation must not
            // become a permanently cached failure. Concurrent cold callers may each
            // derive under their own construction scope; only one result is kept.
            _lazyScalarFullProgram = new Lazy<Utf8SliceProgram>(() => PythonReScalarCompiler.RequireFullMatch(_scalarProgram), LazyThreadSafetyMode.PublicationOnly);

            _nameEntries = GetPythonNameEntries(parseResult.NamedGroups);
            _nameEntriesUtf8 = construction.Budget.AllocateArray<byte[]>(_nameEntries.Length, false);
            for (var index = 0; index < _nameEntries.Length; index++)
            {
                construction.Budget.ChargeWork(1);
                _nameEntriesUtf8[index] = construction.Budget.EncodeUtf8(_nameEntries[index].Name.AsSpan());
            }
            // Transfer the parser's admitted immutable name map for replacement
            // lookup without retaining the rest of the parse tree.
            _namedGroups = parseResult.NamedGroups;

            // Other nullable programs use neither Core start hints nor its result
            // lanes. Their operations use Python scalar execution or independent
            // kernels, so avoid rendering/constructing an unused Core program.
            if (prepareCore)
            {
                try
                {
                    var core = new Utf8Regex(translation.NativePatternUtf8, options: translation.NativeRegexOptions, policy: new Utf8RegexPolicy
                    {
                        MatchTimeout = MatchTimeout,
                        ResourcePolicy = resourcePolicy
                    });
                    // Preparation can discover useful native result lanes or exact
                    // start hints. Discard roots declined by every current consumer;
                    // their transient construction remains conservatively admitted.
                    if (_canUseUtf8IterationFastPath || core.CanFindNativeLiteralForResult ||
                        PythonReScalarMatchState.CanUseCandidateRegex(_scalarProgram, core))
                        _utf8Regex = core;
                }
                catch (Exception exception) when (IsOptionalUtf8BackendUnavailableException(exception))
                {
                    // The optional core fast path may be unavailable; the profile scalar program serves the grammar.
                }
            }

            // Core owns its source/literal copies. Rendering was construction
            // scratch, including when every optional Core consumer declined.
            construction.Budget.ReleaseArray(translation.NativePatternUtf8, false);

            _canUseZeroOffsetUtf8SearchValueFastPath =
                isExactLiteral &&
                _utf8Regex?.Inspection.ExecutionKind is
                    NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal;
            _canUseZeroOffsetUtf8ValueFastPath =
                _canUseZeroOffsetUtf8SearchValueFastPath &&
                _utf8Regex?.Inspection.ExecutionKind == NativeExecutionKind.ExactAsciiLiteral;
            _replacementPlanCache = Utf8BoundedPreparationCache<object, PythonReReplacementPlan>.CreateRetained(ReplacementPlanCacheCapacity, PythonReReplacementKeyComparer.Instance);

            // Fund every retained operation program before transferring compilation
            // admission. Lazy unfunded constructors retain their established path.
            if (eager || programMemoryAllowance is not null) _ = _lazyScalarFullProgram.Value;
            if (programMemoryAllowance is not null)
            {
                var owner = construction.Budget.AllocateObject(programMemoryAllowance,
                    static allowance => new Utf8FundedStorage(allowance, nameof(Utf8PythonRegex)));
                construction.Budget.ChargeWork(0);
                _replacementPlanCache.EnableFunding(programMemoryAllowance);
                owner.Retain(construction.Budget.TransferFundedBytes());
                _programMemory = owner;
            }
            else if (explicitConstruction) construction.Budget.ChargeWork(0);
            construction.Complete();
        }
        catch (Utf8ExecutionDeadlineExpiredException) when (explicitConstruction)
        {
            // Byte patterns need no diagnostic decode after expiry.
            throw new RegexMatchTimeoutException(string.Empty,
                patternSource.IsUtf8 ? string.Empty : patternSource.Text, constructionTimeout);
        }
    }

    /// <summary>Gets or sets the timeout used by constructors that do not specify one.</summary>
    /// <remarks>The initial value is an infinite timeout.</remarks>
    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set => s_defaultMatchTimeout = Utf8MatchTimeout.Validate(value, nameof(value));
    }


    /// <summary>Gets the original Python regular-expression pattern.</summary>
    /// <remarks>Byte compilation owns UTF-8 source. This explicit text projection allocates independently on each native request; the string is not retained by the program.</remarks>
    public string Pattern => _patternSource is string text ? text : Encoding.UTF8.GetString((byte[])_patternSource);

    /// <summary>Gets the compile options supplied by the caller.</summary>
    /// <remarks>Use <see cref="EffectiveOptions"/> for validated global inline flags and implicit Unicode behavior.</remarks>
    public PythonReCompileOptions Options { get; }

    /// <summary>Gets the validated global compile options, including leading inline flags and implicit Unicode behavior.</summary>
    /// <remarks>Scoped flags do not affect this value. Unicode is included unless global ASCII behavior is selected.</remarks>
    public PythonReCompileOptions EffectiveOptions { get; }

    /// <summary>Gets the maximum duration of an individual matching operation.</summary>
    public TimeSpan MatchTimeout { get; }

    /// <summary>Gets the invocation resource policy. Finite work, depth, working and result-capacity limits are supported.</summary>
    public Utf8ResourcePolicy ResourcePolicy { get; }

    internal bool DebugUsesUtf8RegexBackend => _utf8Regex is not null;

    internal int DebugReplacementPlanCacheEntryCount => _replacementPlanCache.Count;
    internal ulong DebugReplacementPlanCachePayloadBytes => _replacementPlanCache.PayloadBytes;

    internal string? DebugUtf8ExecutionKind => _utf8Regex?.Inspection.ExecutionKind.ToString();

    // Exact profile classes and lexical reference lowerings come from the Python
    // AST, independently of residual native selectors or BCL case flags.
    private Utf8SliceProgram ScalarProgram => _scalarProgram;

    /// <summary>Gets the number of capture groups, excluding whole-match group zero.</summary>
    /// <remarks>The count comes from the validated grammar and does not depend on match participation.</remarks>
    public int CaptureGroupCount => _scalarProgram.GroupCount;

    private bool HasFiniteExecutionLimits { get; }

    private Utf8ExecutionLimits InvocationLimits => new(ResourcePolicy.MaxWorkSteps ?? 0, ResourcePolicy.MaxDepth ?? 0, ResourcePolicy.MaxWorkingBytes ?? 0, ResourcePolicy.MaxResultBytes ?? 0);

    private RegexMatchTimeoutException CreateMatchTimeoutException(ReadOnlySpan<byte> input, string pattern)
        => _programMemory is not null
            ? new RegexMatchTimeoutException(string.Empty, pattern, MatchTimeout)
            : Utf8Regex.CreateMatchTimeoutException(input, pattern, MatchTimeout, InvocationLimits);

    internal bool DebugUsesZeroOffsetUtf8ValueFastPath => _canUseZeroOffsetUtf8ValueFastPath;

    internal bool DebugUsesZeroOffsetUtf8SearchValueFastPath =>
        _canUseZeroOffsetUtf8SearchValueFastPath;

    internal bool DebugIsScalarFullProgramValueCreated => _lazyScalarFullProgram.IsValueCreated;

    internal static bool IsOptionalUtf8BackendUnavailableException(Exception exception)
        => exception is ArgumentException or NotSupportedException;

    private void ValidateStartOffset(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if ((uint)startOffsetInBytes > (uint)input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startOffsetInBytes));
        }

        if (startOffsetInBytes < input.Length &&
            (input[startOffsetInBytes] & 0xC0) == 0x80)
        {
            throw new ArgumentOutOfRangeException(
                nameof(startOffsetInBytes),
                startOffsetInBytes,
                "The requested byte offset is not aligned to a UTF-8 scalar boundary.");
        }

    }

    // name entries carry Python slot numbers; the managed numbers they replaced were only valid when no named group preceded an unnamed one.
    private static PythonReNameEntry[] GetPythonNameEntries(IReadOnlyDictionary<string, int> namedGroups)
    {
        using var construction = Utf8ConstructionScope.Enter();
        var entries = construction.Budget.AllocateArray<PythonReNameEntry>(namedGroups.Count, false);
        var index = 0;
        foreach (var pair in namedGroups)
        {
            construction.Budget.ChargeWork(1);
            entries[index++] = new PythonReNameEntry
            {
                Name = pair.Key,
                Number = pair.Value
            };
        }

        construction.Complete();
        return entries;
    }

}
