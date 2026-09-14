using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Caching;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Provides a culture-invariant CPython <c>re</c>-compatible surface over well-formed UTF-8 input.</summary>
/// <remarks>The adapter preserves Python operation shaping while exposing both UTF-8 byte and UTF-16 coordinate projections.</remarks>
public sealed partial class Utf8PythonRegex
{
    private delegate byte[] Utf8ReplacementBytesFactory<TState>(
        ReadOnlySpan<byte> source,
        Match match,
        PythonReUtf8IndexMap indexMap,
        int utf16BaseOffset,
        TState state);
    private readonly record struct PythonReUtf8MatchRange(
        int IndexInBytes,
        int LengthInBytes,
        int IndexInUtf16,
        int LengthInUtf16);

    private ref struct PythonReUtf8MatchRangeBuffer
    {
        private PythonReUtf8MatchRange[]? _ranges;

        internal int Count { get; private set; }

        internal readonly PythonReUtf8MatchRange this[int index] =>
            (_ranges ?? throw new InvalidOperationException("The range buffer has not been initialized."))[index];

        internal void Add(PythonReUtf8MatchRange range)
        {
            if (_ranges is null || Count == _ranges.Length)
            {
                Grow();
            }

            (_ranges ?? throw new InvalidOperationException("Growing the range buffer did not initialize it."))[Count] = range;
            Count++;
        }

        internal void Dispose()
        {
            if (_ranges is null)
            {
                return;
            }

            ArrayPool<PythonReUtf8MatchRange>.Shared.Return(_ranges);
            _ranges = null;
            Count = 0;
        }

        private void Grow()
        {
            var newLength = _ranges is null ? 16 : checked(_ranges.Length * 2);
            var grown = ArrayPool<PythonReUtf8MatchRange>.Shared.Rent(newLength);
            try
            {
                if (_ranges is not null)
                {
                    _ranges.AsSpan(0, Count).CopyTo(grown);
                }
            }
            catch
            {
                ArrayPool<PythonReUtf8MatchRange>.Shared.Return(grown);
                throw;
            }

            var previous = _ranges;
            _ranges = grown;
            if (previous is not null)
            {
                ArrayPool<PythonReUtf8MatchRange>.Shared.Return(previous);
            }
        }
    }

    private static readonly UTF8Encoding s_strictUtf8Encoding = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    private static TimeSpan s_defaultMatchTimeout = Timeout.InfiniteTimeSpan;
    private readonly Utf8Regex? _utf8Regex;
    private readonly Lazy<Utf8Regex?>? _lazyUtf8FullRegex;
    private readonly Lazy<Regex>? _lazyManagedNonEmptyAtSamePositionRegex;
    private readonly Regex _managedRegex;
    private readonly Lazy<Regex> _lazyManagedFullRegex;
    private readonly PythonReTranslation _translation;
    private readonly int[] _managedGroupNumbersByPythonGroup;
    private readonly bool _groupNumberingIsIdentity;
    private readonly bool _hasLookbehind;
    private readonly PythonReNameEntry[] _nameEntries;
    private readonly IReadOnlyDictionary<string, int> _namedGroups;
    private readonly bool _canMatchEmpty;
    private readonly bool _canMatchNonEmpty;
    private readonly bool _canUseUtf8IterationFastPath;
    private readonly bool _canUseManagedSplitFastPath;
    private readonly bool _canUseManagedEmptyReplacementFastPath;
    private readonly int _singleTrailingCapturePrefixLength;
    private readonly string? _optionalExactCaptureFindAllValue;
    private readonly int _optionalExactCapturePresentMatchLength;
    private readonly int _optionalExactCaptureAbsentMatchLength;
    private readonly char? _separatedCaptureTupleSeparator;
    private readonly string? _repeatedExactFindAllString;
    private readonly byte? _asciiGreedyStarFindAllByte;
    private readonly byte[]? _asciiLiteralPrefixDigitMatchPrefix;
    private readonly byte[]? _asciiDotAllFullMatchPrefix;
    private readonly byte[]? _asciiDotAllFullMatchSuffix;
    private readonly bool _canCountAsciiWordBoundariesDirectly;
    private readonly bool _canUseZeroOffsetUtf8SearchValueFastPath;
    private readonly bool _canUseZeroOffsetUtf8ValueFastPath;
    private readonly PythonReDirectBackendKind _searchBackend;
    private readonly PythonReDirectBackendKind _matchBackend;
    private readonly PythonReDirectBackendKind _countBackend;
    private readonly PythonReDirectBackendKind _findAllBackend;
    private readonly PythonReDirectBackendKind _replaceBackend;
    private const int ReplacementPlanCacheCapacity = 16;
    private readonly Utf8BoundedPreparationCache<string, PythonReReplacementPlan> _replacementPlanCache = new(ReplacementPlanCacheCapacity);

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
        : this(DecodeUtf8(patternUtf8, nameof(patternUtf8)), options, matchTimeout)
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

    /// <summary>Creates a Python-compatible expression from UTF-16 text with explicit options and timeout.</summary>
    public Utf8PythonRegex(string pattern, PythonReCompileOptions options, TimeSpan matchTimeout)
    {
        PythonReCompileValidator.Validate(pattern, options);
        Pattern = pattern;
        Options = options;
        MatchTimeout = Utf8MatchTimeout.Validate(matchTimeout, nameof(matchTimeout));

        var parser = new PythonReParser(pattern);
        var parseResult = parser.Parse(options);
        _canMatchEmpty = PythonReTranslator.CanMatchEmpty(parseResult.Root, parseResult);
        _canMatchNonEmpty = PythonReTranslator.CanMatchNonEmpty(parseResult.Root, parseResult);
        _canUseUtf8IterationFastPath = PythonReTranslator.CanUseUtf8IterationFastPath(parseResult.Root);
        _canUseManagedSplitFastPath =
            (!_canMatchEmpty &&
                (parseResult.CaptureGroupCount == 0 ||
                    parseResult.NamedGroups.Count == 0 && PythonReTranslator.CanUseManagedSplitFastPath(parseResult.Root))) ||
            (!_canMatchNonEmpty && parseResult.CaptureGroupCount == 0);
        _canUseManagedEmptyReplacementFastPath =
            parseResult.CaptureGroupCount == 0 &&
            PythonReTranslator.CanUseManagedEmptyReplacementFastPath(parseResult.Root);
        _singleTrailingCapturePrefixLength = !_canMatchEmpty &&
            parseResult.CaptureGroupCount == 1 &&
            PythonReTranslator.TryGetSingleTrailingCapturePrefixLength(parseResult.Root, out var capturePrefixLength)
                ? capturePrefixLength
                : -1;
        var optionalCapturePresentMatchLength = 0;
        var optionalCaptureAbsentMatchLength = 0;
        _optionalExactCaptureFindAllValue = !_canMatchEmpty &&
            parseResult.CaptureGroupCount == 1 &&
            PythonReTranslator.TryGetOptionalExactCaptureFindAllPlan(
                parseResult.Root,
                parseResult.Options,
                out var optionalCaptureValue,
                out optionalCapturePresentMatchLength,
                out optionalCaptureAbsentMatchLength)
                ? optionalCaptureValue
                : null;
        _optionalExactCapturePresentMatchLength = optionalCapturePresentMatchLength;
        _optionalExactCaptureAbsentMatchLength = optionalCaptureAbsentMatchLength;
        _separatedCaptureTupleSeparator = !_canMatchEmpty &&
            parseResult.CaptureGroupCount == 2 &&
            PythonReTranslator.TryGetSeparatedCaptureTupleSeparator(parseResult.Root, out var captureSeparator)
                ? captureSeparator
                : null;
        _repeatedExactFindAllString = !_canMatchEmpty &&
            parseResult.CaptureGroupCount == 0 &&
            PythonReTranslator.TryGetCaseSensitiveExactLiteral(
                parseResult.Root,
                parseResult.Options,
                out var exactFindAllString)
                ? exactFindAllString
                : null;
        _asciiGreedyStarFindAllByte = MatchTimeout == Timeout.InfiniteTimeSpan &&
            parseResult.CaptureGroupCount == 0 &&
            PythonReTranslator.TryGetAsciiGreedyStarFindAllByte(
                parseResult.Root,
                parseResult.Options,
                out var asciiGreedyStarFindAllByte)
                ? asciiGreedyStarFindAllByte
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
        _canCountAsciiWordBoundariesDirectly = pattern == @"\b" && (options & PythonReCompileOptions.Ascii) != 0;
        _translation = PythonReTranslator.Translate(parseResult);
        _hasLookbehind = parseResult.HasLookbehind;
        _managedRegex = CreateManagedRegex(_translation.Pattern, _translation.RegexOptions, MatchTimeout);
        // PLAN.md C08: `\G` pins the full match to the requested start offset (Python `pos` semantics with intact prefix context); `\A` would pin the real start and forbid pos > 0. Every caller below passes the start offset explicitly.
        _lazyManagedFullRegex = new Lazy<Regex>(() => CreateManagedRegex(@"\G(?:" + _translation.Pattern + @")\z", _translation.RegexOptions, MatchTimeout), LazyThreadSafetyMode.ExecutionAndPublication);
        if (_canMatchEmpty && _canMatchNonEmpty)
        {
            _lazyManagedNonEmptyAtSamePositionRegex = new Lazy<Regex>(
                CreateManagedNonEmptyAtSamePositionRegex,
                LazyThreadSafetyMode.ExecutionAndPublication);
        }
        _managedGroupNumbersByPythonGroup = GetManagedGroupNumbersByPythonGroup(
            _managedRegex,
            parseResult.CaptureGroupCount,
            parseResult.NamedGroups,
            _translation.EmittedGroupNames);
        // PLAN.md C07: every consumer below indexes Python slots; managed numbers only appear at the engine boundary through this permutation.
        _groupNumberingIsIdentity = true;
        for (var pythonGroup = 0; pythonGroup < _managedGroupNumbersByPythonGroup.Length; pythonGroup++)
        {
            if (_managedGroupNumbersByPythonGroup[pythonGroup] != pythonGroup)
            {
                _groupNumberingIsIdentity = false;
                break;
            }
        }

        _groupNames = GetPublicGroupNames(parseResult.CaptureGroupCount, parseResult.NamedGroups);
        _nameEntries = GetPythonNameEntries(parseResult.NamedGroups);
        _namedGroups = _nameEntries.ToDictionary(x => x.Name, x => x.Number, StringComparer.Ordinal);

        try
        {
            _utf8Regex = new Utf8Regex(_translation.Pattern, _translation.RegexOptions, MatchTimeout);
            if (_utf8Regex.Inspection.ExecutionKind != NativeExecutionKind.FallbackRegex)
            {
                _lazyUtf8FullRegex = new Lazy<Utf8Regex?>(
                    CreateUtf8FullRegex,
                    LazyThreadSafetyMode.ExecutionAndPublication);
            }
        }
        catch (Exception exception) when (IsOptionalUtf8BackendUnavailableException(exception))
        {
            // Fall back to managed Regex if the translated pattern cannot be executed by Utf8Regex.
        }

        _canUseZeroOffsetUtf8SearchValueFastPath =
            isExactLiteral &&
            _utf8Regex?.Inspection.ExecutionKind is
                NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal;
        _canUseZeroOffsetUtf8ValueFastPath =
            _canUseZeroOffsetUtf8SearchValueFastPath &&
            _utf8Regex?.Inspection.ExecutionKind == NativeExecutionKind.ExactAsciiLiteral;
        _searchBackend = _utf8Regex is not null ? PythonReDirectBackendKind.Utf8Regex : PythonReDirectBackendKind.ManagedRegex;
        _matchBackend = _utf8Regex is not null ? PythonReDirectBackendKind.Utf8Regex : PythonReDirectBackendKind.ManagedRegex;
        _countBackend = _utf8Regex is not null && !_canMatchEmpty
            ? PythonReDirectBackendKind.Utf8Regex
            : PythonReDirectBackendKind.ManagedRegex;
        _findAllBackend = _utf8Regex is not null && !_canMatchEmpty && _canUseUtf8IterationFastPath
            ? PythonReDirectBackendKind.Utf8Regex
            : PythonReDirectBackendKind.ManagedRegex;
        _replaceBackend = _canUseZeroOffsetUtf8ValueFastPath
            ? PythonReDirectBackendKind.Utf8Regex
            : PythonReDirectBackendKind.ManagedRegex;
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

    /// <summary>Returns the group names in group-number order, including group zero.</summary>
    // PLAN.md C09: returned arrays are caller-owned; never expose the compiled backing storage. Empty entry arrays stay shared: they have no slots to mutate.
    public string[] GetGroupNames() => (string[])_groupNames.Clone();

    private static int CountAsciiWordBoundaries(string subject, int startOffsetInUtf16)
    {
        var count = 0;
        var leftIsWord = startOffsetInUtf16 > 0 && IsAsciiWord(subject[startOffsetInUtf16 - 1]);
        for (var position = startOffsetInUtf16; position <= subject.Length; position++)
        {
            var rightIsWord = position < subject.Length && IsAsciiWord(subject[position]);
            if (leftIsWord != rightIsWord)
            {
                count++;
            }

            leftIsWord = rightIsWord;
        }

        return count;
    }

    private static bool IsAsciiWord(char value) =>
        char.IsAsciiLetterOrDigit(value) || value == '_';

    // PLAN.md D02: single owner for the nonempty same-position retry guard; shapers and the managed progression cursor share it.
    internal static bool TryMatchNonEmptyAtSamePosition(
        Lazy<Regex>? nonEmptyMatcher,
        string subject,
        int utf16Position,
        out Match match)
    {
        if (nonEmptyMatcher is null ||
            (uint)utf16Position >= (uint)subject.Length)
        {
            match = System.Text.RegularExpressions.Match.Empty;
            return false;
        }

        match = nonEmptyMatcher.Value.Match(subject, utf16Position);
        return match.Success && match.Index == utf16Position && match.Length != 0;
    }

    internal bool DebugUsesUtf8RegexBackend => _utf8Regex is not null;

    internal PythonReDirectBackendKind DebugSearchBackend => _searchBackend;

    internal PythonReDirectBackendKind DebugMatchBackend => _matchBackend;

    internal PythonReDirectBackendKind DebugFullMatchBackend => GetUtf8FullRegex() is not null
        ? PythonReDirectBackendKind.Utf8Regex
        : PythonReDirectBackendKind.ManagedRegex;

    internal PythonReDirectBackendKind DebugCountBackend => _countBackend;

    internal bool DebugUsesAsciiWordBoundaryCount => _canCountAsciiWordBoundariesDirectly;

    internal bool DebugUsesAsciiWordBoundarySplit => _canCountAsciiWordBoundariesDirectly;

    internal bool DebugUsesManagedEmptyReplacementFastPath => _canUseManagedEmptyReplacementFastPath;

    internal int DebugReplacementPlanCacheEntryCount => _replacementPlanCache.Count;

    internal string? DebugUtf8ExecutionKind => _utf8Regex?.Inspection.ExecutionKind.ToString();

    internal bool DebugUsesZeroOffsetUtf8ValueFastPath => _canUseZeroOffsetUtf8ValueFastPath;

    internal bool DebugUsesZeroOffsetUtf8SearchValueFastPath =>
        _canUseZeroOffsetUtf8SearchValueFastPath;

    internal string? DebugUtf8FullMatchExecutionKind =>
        GetUtf8FullRegex()?.Inspection.ExecutionKind.ToString();

    internal bool DebugIsUtf8FullRegexValueCreated => _lazyUtf8FullRegex?.IsValueCreated ?? false;

    internal bool DebugIsManagedNonEmptyAtSamePositionRegexValueCreated =>
        _lazyManagedNonEmptyAtSamePositionRegex?.IsValueCreated ?? false;

    internal bool DebugIsManagedFullRegexValueCreated =>
        _lazyManagedFullRegex.IsValueCreated;

    internal bool DebugHasUtf8FullRegex => GetUtf8FullRegex() is not null;

    internal string DebugTranslatedPattern => _translation.Pattern;

    internal string DebugDescribeExecutionPlan()
    {
        return $"Search={_searchBackend}, Match={_matchBackend}, " +
               $"FullMatch={DebugFullMatchBackend}, Count={_countBackend}";
    }

    internal PythonReDirectBackendKind DebugFindAllBackend => _findAllBackend;

    internal PythonReDirectBackendKind DebugReplaceBackend => _replaceBackend;

    internal PythonReDirectBackendKind DebugSplitBackend => PythonReDirectBackendKind.ManagedRegex;

    internal bool DebugUsesManagedSplitFastPath => _canUseManagedSplitFastPath;

    internal bool DebugUsesSingleTrailingCaptureFindAllFastPath => _singleTrailingCapturePrefixLength >= 0;

    internal bool DebugUsesOptionalExactCaptureFindAllFastPath => _optionalExactCaptureFindAllValue is not null;

    internal bool DebugUsesSeparatedCaptureTupleFindAllFastPath => _separatedCaptureTupleSeparator.HasValue;

    internal bool DebugUsesRepeatedExactStringFindAllFastPath => _repeatedExactFindAllString is not null;

    internal bool DebugUsesAsciiGreedyStarFindAllFastPath => _asciiGreedyStarFindAllByte.HasValue;

    internal bool DebugUsesCountedRepeatedExactStringFindAllFastPath =>
        _repeatedExactFindAllString is not null &&
        _findAllBackend == PythonReDirectBackendKind.Utf8Regex;

    internal bool DebugUsesAsciiLiteralPrefixDigitMatchFastPath =>
        _asciiLiteralPrefixDigitMatchPrefix is not null;

    internal bool DebugUsesAsciiDotAllFullMatchFastPath =>
        _asciiDotAllFullMatchPrefix is not null;

    private Utf8Regex? GetUtf8FullRegex() => _lazyUtf8FullRegex?.Value;

    private Utf8Regex? CreateUtf8FullRegex()
    {
        try
        {
            var regex = new Utf8Regex(
                @"\G(?:" + _translation.Pattern + @")\z",
                _translation.RegexOptions,
                MatchTimeout);
            return regex.Inspection.ExecutionKind != NativeExecutionKind.FallbackRegex ? regex : null;
        }
        catch (Exception exception) when (IsOptionalUtf8BackendUnavailableException(exception))
        {
            return null;
        }
    }

    internal static bool IsOptionalUtf8BackendUnavailableException(Exception exception)
        => exception is ArgumentException or NotSupportedException;

    private static string Decode(ReadOnlySpan<byte> input)
        => DecodeUtf8(input, nameof(input));

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

    internal static int AdvancePastScalar(string subject, int utf16Position) =>
        checked(utf16Position + Rune.GetRuneAt(subject, utf16Position).Utf16SequenceLength);

    private static int GetUtf16OffsetOfBytePrefix(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return s_strictUtf8Encoding.GetCharCount(input[..startOffsetInBytes]);
    }

    private static void ValidateStartOffset(ReadOnlySpan<byte> input, int startOffsetInBytes)
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

        try
        {
            _ = s_strictUtf8Encoding.GetCharCount(input[..startOffsetInBytes]);
        }
        catch (DecoderFallbackException ex)
        {
            throw new ArgumentException("The input must be valid UTF-8.", nameof(input), ex);
        }
    }

    private bool TryFindAllViaUtf8Regex(ReadOnlySpan<byte> input, int startOffsetInBytes, out Utf8PythonMatchData[] matches)
    {
        if (!TryCollectUtf8MatchRanges(input, startOffsetInBytes, out var ranges))
        {
            matches = [];
            return false;
        }

        try
        {
            matches = new Utf8PythonMatchData[ranges.Count];
            for (var i = 0; i < matches.Length; i++)
            {
                var range = ranges[i];
                matches[i] = new Utf8PythonMatchData
                {
                    Success = true,
                    StartOffsetInBytes = range.IndexInBytes,
                    EndOffsetInBytes = range.IndexInBytes + range.LengthInBytes,
                    StartOffsetInUtf16 = range.IndexInUtf16,
                    EndOffsetInUtf16 = range.IndexInUtf16 + range.LengthInUtf16,
                    ValueText = Encoding.UTF8.GetString(input.Slice(range.IndexInBytes, range.LengthInBytes)),
                };
            }

            return true;
        }
        finally
        {
            ranges.Dispose();
        }
    }

    private bool TryCollectUtf8MatchRanges(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        out PythonReUtf8MatchRangeBuffer ranges)
    {
        if (_findAllBackend != PythonReDirectBackendKind.Utf8Regex || _utf8Regex is null || _hasLookbehind)
        {
            ranges = default;
            return false;
        }

        ranges = default;
        try
        {
            var enumerator = startOffsetInBytes == 0
                ? _utf8Regex.EnumerateMatches(input)
                : _utf8Regex.EnumerateMatchesFromUtf16Offset(input, GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));

            foreach (var match in enumerator)
            {
                if (!match.TryGetByteRange(out var indexInBytes, out var lengthInBytes))
                {
                    ranges.Dispose();
                    ranges = default;
                    return false;
                }

                ranges.Add(new PythonReUtf8MatchRange(
                    indexInBytes,
                    lengthInBytes,
                    match.IndexInUtf16,
                    match.LengthInUtf16));
            }

            return true;
        }
        catch
        {
            ranges.Dispose();
            ranges = default;
            throw;
        }
    }

    private bool TryReplaceViaUtf8Literal(
        ReadOnlySpan<byte> input,
        PythonReReplacementPlan plan,
        int count,
        int startOffsetInBytes,
        out byte[] resultBytes,
        out int replacementCount)
    {
        if (count < 0 ||
            startOffsetInBytes != 0 ||
            !_canUseZeroOffsetUtf8ValueFastPath ||
            _utf8Regex is null ||
            !plan.TryGetLiteralText(out var literalText))
        {
            resultBytes = [];
            replacementCount = 0;
            return false;
        }

        byte[] literalBytes;
        try
        {
            literalBytes = s_strictUtf8Encoding.GetBytes(literalText);
        }
        catch (EncoderFallbackException)
        {
            resultBytes = [];
            replacementCount = 0;
            return false;
        }

        var maximumReplacementCount = count == 0 ? int.MaxValue : count;
        resultBytes = _utf8Regex.ReplaceLiteralWithCount(
            input,
            literalBytes,
            maximumReplacementCount,
            out replacementCount);
        return true;
    }

    private static int[] GetManagedGroupNumbersByPythonGroup(
        Regex regex,
        int captureGroupCount,
        IReadOnlyDictionary<string, int> namedGroups,
        IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        var namesByPythonGroup = new string?[captureGroupCount + 1];
        foreach (var pair in namedGroups)
        {
            namesByPythonGroup[pair.Value] = pair.Key;
        }

        var managedGroupNumbers = new int[captureGroupCount + 1];
        var nextUnnamedManagedGroup = 1;
        for (var pythonGroup = 1; pythonGroup <= captureGroupCount; pythonGroup++)
        {
            var name = namesByPythonGroup[pythonGroup];
            managedGroupNumbers[pythonGroup] = name is null
                ? nextUnnamedManagedGroup++
                : regex.GroupNumberFromName(emittedGroupNames[name]);
        }

        return managedGroupNumbers;
    }

    // PLAN.md C07: public names follow Python slot order, not managed order. Unnamed slots keep their number as their name, matching both engines.
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

    private static int GetUtf8RuneLength(byte firstByte)
    {
        if ((firstByte & 0b1000_0000) == 0)
        {
            return 1;
        }

        if ((firstByte & 0b1110_0000) == 0b1100_0000)
        {
            return 2;
        }

        if ((firstByte & 0b1111_0000) == 0b1110_0000)
        {
            return 3;
        }

        return 4;
    }

    private static void AppendUtf8(ArrayBufferWriter<byte> builder, ReadOnlySpan<byte> value)
    {
        if (value.IsEmpty)
        {
            return;
        }

        value.CopyTo(builder.GetSpan(value.Length));
        builder.Advance(value.Length);
    }
}

internal enum PythonReDirectBackendKind
{
    ManagedRegex,
    Utf8Regex,
}

internal readonly record struct PythonReManagedMatchSnapshot(PythonReGroupData[] Groups)
{
    public Utf8PythonMatchContext ToContext(ReadOnlySpan<byte> input, PythonReNameEntry[] nameEntries)
        => new(input, Groups, nameEntries);
}
