using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Provides a culture-invariant CPython <c>re</c>-compatible surface over well-formed UTF-8 input.</summary>
/// <remarks>The adapter preserves Python operation shaping while exposing both UTF-8 byte and UTF-16 coordinate projections.</remarks>
public sealed partial class Utf8PythonRegex
{
    private readonly record struct PythonReUtf8MatchRange(
        int IndexInBytes,
        int LengthInBytes,
        int IndexInUtf16,
        int LengthInUtf16);

    private ref struct PythonReUtf8MatchRangeBuffer
    {
        private Utf8InvocationStateBuffer<PythonReUtf8MatchRange> _ranges;
        private Utf8SliceInvocation _invocation;

        internal PythonReUtf8MatchRangeBuffer(Utf8SliceInvocation invocation)
        {
            _invocation = invocation;
            _ranges = default;
        }

        internal readonly int Count => _ranges.Count;
        internal readonly PythonReUtf8MatchRange this[int index] => _ranges[index];

        internal void Add(PythonReUtf8MatchRange range)
        {
            var breach = _invocation.TryChargeInputStep();
            if (breach == Utf8ExecutionLimitKind.None) breach = _ranges.TryPush(range, ref _invocation);
            if (breach != Utf8ExecutionLimitKind.None)
                throw new InvalidOperationException($"Python range storage exceeded {breach}.");
        }

        internal void Dispose() => _ranges.Release(ref _invocation);
    }

    private static readonly UTF8Encoding s_strictUtf8Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static TimeSpan s_defaultMatchTimeout = Timeout.InfiniteTimeSpan;
    private readonly Utf8SliceProgram _scalarProgram;
    private readonly Lazy<Utf8SliceProgram> _lazyScalarFullProgram;
    private readonly Utf8Regex? _utf8Regex;
    private readonly PythonReTranslation _translation;
    private readonly int[] _coreGroupNumbersByPythonGroup;
    private readonly PythonReNameEntry[] _nameEntries;
    private readonly IReadOnlyDictionary<string, int> _namedGroups;
    private readonly bool _canMatchEmpty;
    private readonly bool _canUseUtf8IterationFastPath;
    private readonly string? _optionalExactCaptureFindAllValue;
    private readonly string? _repeatedExactFindAllString;
    private readonly byte[]? _asciiLiteralPrefixDigitMatchPrefix;
    private readonly byte[]? _asciiDotAllFullMatchPrefix;
    private readonly byte[]? _asciiDotAllFullMatchSuffix;
    private readonly bool _canUseZeroOffsetUtf8SearchValueFastPath;
    private readonly bool _canUseZeroOffsetUtf8ValueFastPath;
    private const int ReplacementPlanCacheCapacity = 16;
    private readonly Utf8BoundedPreparationCache<string, PythonReReplacementPlan> _replacementPlanCache;

    /// <summary>Creates a Python-compatible expression from UTF-16 text with default options and timeout.</summary>
    public Utf8PythonRegex(string pattern)
        : this(pattern, PythonReCompileOptions.None)
    {
    }

    /// <summary>Creates a Python-compatible expression from a well-formed UTF-8 pattern with default options and timeout.</summary>
    public Utf8PythonRegex(ReadOnlySpan<byte> patternUtf8)
        : this(patternUtf8, PythonReCompileOptions.None)
    {
    }

    /// <summary>Creates a Python-compatible expression from a well-formed UTF-8 pattern using the process-wide default timeout.</summary>
    /// <param name="patternUtf8">The pattern encoded as UTF-8.</param>
    /// <param name="options">The Python-compatible compile options.</param>
    /// <exception cref="ArgumentException">The pattern bytes are not valid UTF-8, the options are incompatible, or the pattern is invalid.</exception>
    public Utf8PythonRegex(ReadOnlySpan<byte> patternUtf8, PythonReCompileOptions options)
        : this(patternUtf8, options, DefaultMatchTimeout)
    {
    }

    /// <summary>Creates a Python-compatible expression from a well-formed UTF-8 pattern with explicit options and timeout.</summary>
    public Utf8PythonRegex(ReadOnlySpan<byte> patternUtf8, PythonReCompileOptions options, TimeSpan matchTimeout)
        : this(new Utf8ConstructionPattern(patternUtf8), options, matchTimeout, Utf8ResourcePolicy.Unlimited, checkPolicy: false)
    {
    }

    /// <summary>Creates a Python-compatible expression using the process-wide default timeout.</summary>
    /// <param name="pattern">The pattern in UTF-16 text.</param>
    /// <param name="options">The Python-compatible compile options.</param>
    /// <exception cref="ArgumentException">The options are incompatible or the pattern is invalid.</exception>
    public Utf8PythonRegex(string pattern, PythonReCompileOptions options)
        : this(pattern, options, DefaultMatchTimeout)
    {
    }

    /// <summary>Creates a Python-compatible expression from a well-formed UTF-8 pattern with explicit options, timeout, and invocation resource policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A policy budget is zero; use null for unlimited.</exception>
    /// <exception cref="NotSupportedException">A work, depth, working-bytes, or result-bytes budget is set. This profile enforces only match timeouts.</exception>
    public Utf8PythonRegex(ReadOnlySpan<byte> patternUtf8, PythonReCompileOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy)
        : this(new Utf8ConstructionPattern(patternUtf8), options, matchTimeout, resourcePolicy, checkPolicy: true)
    {
    }

    /// <summary>Creates a Python-compatible expression from UTF-16 text with explicit options, timeout, and invocation resource policy.</summary>
    /// <exception cref="ArgumentOutOfRangeException">A policy budget is zero; use null for unlimited.</exception>
    /// <exception cref="NotSupportedException">A work, depth, working-bytes, or result-bytes budget is set. This profile enforces only match timeouts.</exception>
    public Utf8PythonRegex(string pattern, PythonReCompileOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy)
        : this(new Utf8ConstructionPattern(pattern), options, matchTimeout, resourcePolicy, checkPolicy: true)
    {
    }

    /// <summary>Creates a Python-compatible expression from UTF-16 text with explicit options and timeout.</summary>
    public Utf8PythonRegex(string pattern, PythonReCompileOptions options, TimeSpan matchTimeout)
        : this(new Utf8ConstructionPattern(pattern), options, matchTimeout, Utf8ResourcePolicy.Unlimited, checkPolicy: false)
    {
    }

    /// <summary>Shared construction worker. Validates the policy before any parsing or preparation when requested.</summary>
    private Utf8PythonRegex(Utf8ConstructionPattern patternSource, PythonReCompileOptions options, TimeSpan matchTimeout, Utf8ResourcePolicy resourcePolicy, bool checkPolicy)
    {
        if (checkPolicy)
        {
            ValidatePythonRePolicy(resourcePolicy, nameof(resourcePolicy));
        }
        using var construction = Utf8ConstructionScope.Enter();
        var pattern = patternSource.Text;
        if (patternSource.IsUtf8)
        {
            try { pattern = construction.Budget.DecodeUtf8(patternSource.Utf8, s_strictUtf8Encoding); }
            catch (DecoderFallbackException exception)
            {
                throw new ArgumentException("The input must be valid UTF-8.", "patternUtf8", exception);
            }
        }

        PythonReCompileValidator.Validate(pattern, options);
        Pattern = pattern;
        Options = options;
        MatchTimeout = Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout));

        var parser = PythonReParser.Create(pattern);
        var parseResult = parser.Parse(options);
        _canMatchEmpty = PythonReTranslator.CanMatchEmpty(parseResult.Root, parseResult);
        _canUseUtf8IterationFastPath = PythonReTranslator.CanUseUtf8IterationFastPath(parseResult.Root);
        _optionalExactCaptureFindAllValue = !_canMatchEmpty &&
            parseResult.CaptureGroupCount == 1 &&
            PythonReTranslator.TryGetOptionalExactCaptureFindAllPlan(
                parseResult.Root,
                parseResult.Options,
                out var optionalCaptureValue)
                ? optionalCaptureValue
                : null;
        _repeatedExactFindAllString = !_canMatchEmpty &&
            parseResult.CaptureGroupCount == 0 &&
            PythonReTranslator.TryGetCaseSensitiveExactLiteral(
                parseResult.Root,
                parseResult.Options,
                out var exactFindAllString)
                ? exactFindAllString
                : null;
        _asciiLiteralPrefixDigitMatchPrefix = MatchTimeout == Timeout.InfiniteTimeSpan &&
            parseResult.CaptureGroupCount == 0 &&
            PythonReTranslator.TryGetAsciiLiteralPrefixDigitRepeat(
                parseResult.Root,
                parseResult.Options,
                out var asciiLiteralPrefixDigitMatchPrefix)
                ? asciiLiteralPrefixDigitMatchPrefix
                : null;
        byte[]? asciiDotAllFullMatchPrefix = null;
        byte[]? asciiDotAllFullMatchSuffix = null;
        if (MatchTimeout == Timeout.InfiniteTimeSpan &&
            parseResult.CaptureGroupCount == 0 &&
            PythonReTranslator.TryGetAsciiDotAllFullMatchPlan(
                parseResult.Root,
                parseResult.Options,
                out var candidatePrefix,
                out var candidateSuffix))
        {
            asciiDotAllFullMatchPrefix = candidatePrefix;
            asciiDotAllFullMatchSuffix = candidateSuffix;
        }

        _asciiDotAllFullMatchPrefix = asciiDotAllFullMatchPrefix;
        _asciiDotAllFullMatchSuffix = asciiDotAllFullMatchSuffix;
        var isExactLiteral = PythonReTranslator.IsExactLiteral(parseResult.Root);
        _translation = PythonReTranslator.Translate(parseResult);
        _scalarProgram = PythonReScalarCompiler.Compile(parseResult, _translation, false);
        _lazyScalarFullProgram = new Lazy<Utf8SliceProgram>(() => PythonReScalarCompiler.Compile(parseResult, _translation, true), LazyThreadSafetyMode.ExecutionAndPublication);
        _coreGroupNumbersByPythonGroup = _translation.CoreNumbersByPython;

        _groupNames = GetPublicGroupNames(parseResult.CaptureGroupCount, parseResult.NamedGroups);
        _nameEntries = GetPythonNameEntries(parseResult.NamedGroups);
        _namedGroups = _nameEntries.ToDictionary(x => x.Name, x => x.Number, StringComparer.Ordinal);

        try
        {
            _utf8Regex = new Utf8Regex(_translation.NativePattern, _translation.NativeRegexOptions, MatchTimeout);
        }
        catch (Exception exception) when (IsOptionalUtf8BackendUnavailableException(exception))
        {
            // The optional core fast path may be unavailable; the profile scalar program serves the grammar.
        }

        _canUseZeroOffsetUtf8SearchValueFastPath =
            isExactLiteral &&
            _utf8Regex?.Inspection.ExecutionKind is
                NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal;
        _canUseZeroOffsetUtf8ValueFastPath =
            _canUseZeroOffsetUtf8SearchValueFastPath &&
            _utf8Regex?.Inspection.ExecutionKind == NativeExecutionKind.ExactAsciiLiteral;
        ResourcePolicy = resourcePolicy;
        _replacementPlanCache = Utf8BoundedPreparationCache<string, PythonReReplacementPlan>.CreateRetained(ReplacementPlanCacheCapacity, StringComparer.Ordinal);
        construction.Complete();
    }

    private static void ValidatePythonRePolicy(Utf8ResourcePolicy resourcePolicy, string paramName)
    {
        resourcePolicy.Validate(paramName);
        if (resourcePolicy.MaxWorkSteps.HasValue || resourcePolicy.MaxDepth.HasValue || resourcePolicy.MaxWorkingBytes.HasValue || resourcePolicy.MaxResultBytes.HasValue)
        {
            throw new NotSupportedException("This profile enforces only match timeouts; work, depth, and memory budgets require the PCRE2 profile.");
        }
    }

    /// <summary>Gets or sets the timeout used by constructors that do not specify one.</summary>
    /// <remarks>The initial value is an infinite timeout.</remarks>
    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set => s_defaultMatchTimeout = Utf8MatchTimeout.Validate(value, nameof(value));
    }

    private readonly string[] _groupNames;

    /// <summary>Gets the original Python regular-expression pattern.</summary>
    public string Pattern { get; }

    /// <summary>Gets the Python-compatible compile options.</summary>
    public PythonReCompileOptions Options { get; }

    /// <summary>Gets the maximum duration of an individual matching operation.</summary>
    public TimeSpan MatchTimeout { get; }

    /// <summary>Gets the invocation resource policy.</summary>
    public Utf8ResourcePolicy ResourcePolicy { get; }

    /// <summary>Returns the group names in group-number order, including group zero.</summary>
    // PLAN.md C09: returned arrays are caller-owned; never expose the compiled backing storage. Empty entry arrays stay shared: they have no slots to mutate.
    public string[] GetGroupNames() => (string[])_groupNames.Clone();

    internal bool DebugUsesUtf8RegexBackend => _utf8Regex is not null;

    internal PythonReDirectBackendKind DebugSearchBackend => PythonReDirectBackendKind.Utf8Regex;

    internal PythonReDirectBackendKind DebugMatchBackend => PythonReDirectBackendKind.Utf8Regex;

    internal PythonReDirectBackendKind DebugFullMatchBackend => PythonReDirectBackendKind.Utf8Regex;

    internal PythonReDirectBackendKind DebugCountBackend => PythonReDirectBackendKind.Utf8Regex;

    internal int DebugReplacementPlanCacheEntryCount => _replacementPlanCache.Count;
    internal ulong DebugReplacementPlanCachePayloadBytes => _replacementPlanCache.PayloadBytes;

    internal string? DebugUtf8ExecutionKind => _utf8Regex?.Inspection.ExecutionKind.ToString();

    // Exact profile classes and lexical reference lowerings come from the Python
    // AST, independently of residual native selectors or BCL case flags.
    private Utf8SliceProgram ScalarProgram => _scalarProgram;

    internal bool DebugUsesOwnedSlice => _scalarProgram is not null;

    internal bool DebugUsesZeroOffsetUtf8ValueFastPath => _canUseZeroOffsetUtf8ValueFastPath;

    internal bool DebugUsesZeroOffsetUtf8SearchValueFastPath =>
        _canUseZeroOffsetUtf8SearchValueFastPath;

    internal bool DebugIsScalarFullProgramValueCreated => _lazyScalarFullProgram.IsValueCreated;

    internal bool DebugHasScalarFullProgram => _lazyScalarFullProgram.Value is not null;

    internal string DebugTranslatedPattern => _translation.Pattern;

    internal string DebugNativePattern => _translation.NativePattern;

    internal string DebugDescribeExecutionPlan()
    {
        return $"Search={DebugSearchBackend}, Match={DebugMatchBackend}, " +
               $"FullMatch={DebugFullMatchBackend}, Count={DebugCountBackend}";
    }

    internal PythonReDirectBackendKind DebugFindAllBackend => PythonReDirectBackendKind.Utf8Regex;

    internal PythonReDirectBackendKind DebugReplaceBackend => PythonReDirectBackendKind.Utf8Regex;

    internal PythonReDirectBackendKind DebugSplitBackend => PythonReDirectBackendKind.Utf8Regex;

    internal bool DebugUsesOptionalExactCaptureFindAllFastPath => _optionalExactCaptureFindAllValue is not null;

    internal bool DebugUsesRepeatedExactStringFindAllFastPath => _repeatedExactFindAllString is not null;

    internal bool DebugUsesCountedRepeatedExactStringFindAllFastPath =>
        _repeatedExactFindAllString is not null && _utf8Regex is not null;

    internal bool DebugUsesAsciiLiteralPrefixDigitMatchFastPath =>
        _asciiLiteralPrefixDigitMatchPrefix is not null;

    internal bool DebugUsesAsciiDotAllFullMatchFastPath =>
        _asciiDotAllFullMatchPrefix is not null;

    internal static bool IsOptionalUtf8BackendUnavailableException(Exception exception)
        => exception is ArgumentException or NotSupportedException;

    private static string DecodeUtf8(ReadOnlySpan<byte> input, string parameterName)
    {
        try
        {
            return s_strictUtf8Encoding.GetString(input);
        }
        catch (DecoderFallbackException ex)
        {
            throw new ArgumentException("The input must be valid UTF-8.", parameterName, ex);
        }
    }

    private static int GetUtf16OffsetOfBytePrefix(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return s_strictUtf8Encoding.GetCharCount(input[..startOffsetInBytes]);
    }

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

    // Public names follow Python slot order; the optional core route has its own slot permutation.
    private static string[] GetPublicGroupNames(int captureGroupCount, IReadOnlyDictionary<string, int> namedGroups)
    {
        var namesByPythonGroup = new string?[captureGroupCount + 1];
        foreach (var pair in namedGroups)
        {
            namesByPythonGroup[pair.Value] = pair.Key;
        }

        var names = new string[captureGroupCount + 1];
        names[0] = "0";
        for (var pythonGroup = 1; pythonGroup <= captureGroupCount; pythonGroup++)
        {
            names[pythonGroup] = namesByPythonGroup[pythonGroup] ?? pythonGroup.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return names;
    }

    // PLAN.md C07: name entries carry Python slot numbers; the managed numbers they replaced were only valid when no named group preceded an unnamed one.
    private static PythonReNameEntry[] GetPythonNameEntries(IReadOnlyDictionary<string, int> namedGroups)
    {
        var entries = new List<PythonReNameEntry>(namedGroups.Count);
        foreach (var pair in namedGroups)
        {
            entries.Add(new PythonReNameEntry
            {
                Name = pair.Key,
                Number = pair.Value,
            });
        }

        return entries.ToArray();
    }

}

internal enum PythonReDirectBackendKind
{
    Utf8Regex,
}
