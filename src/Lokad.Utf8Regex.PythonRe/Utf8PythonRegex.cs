using System.Buffers;
using System.Text;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

namespace Lokad.Utf8Regex.PythonRe;

/// <summary>Provides a culture-invariant CPython <c>re</c>-compatible surface over well-formed UTF-8 input.</summary>
/// <remarks>The adapter preserves Python operation shaping while exposing both UTF-8 byte and UTF-16 coordinate projections.</remarks>
public sealed class Utf8PythonRegex
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
    private readonly Regex _managedFullRegex;
    private readonly PythonReTranslation _translation;
    private readonly int[] _managedGroupNumbersByPythonGroup;
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
        _canMatchEmpty = PythonReTranslator.CanMatchEmpty(parseResult.Root);
        _canMatchNonEmpty = PythonReTranslator.CanMatchNonEmpty(parseResult.Root);
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
        _managedRegex = CreateManagedRegex(_translation.Pattern, _translation.RegexOptions, MatchTimeout);
        _managedFullRegex = CreateManagedRegex(@"\A(?:" + _translation.Pattern + @")\z", _translation.RegexOptions, MatchTimeout);
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
        _groupNames = GetPublicGroupNames(_managedRegex, _translation.EmittedGroupNames);
        _nameEntries = GetManagedNameEntries(_managedRegex, _translation.EmittedGroupNames);
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
    public string[] GetGroupNames() => _groupNames;

    /// <summary>Tests whether the UTF-8 input contains a match.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input) => IsMatch(input, 0);

    /// <summary>Tests for a match at or after a zero-based UTF-8 byte offset.</summary>
    public bool IsMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return Search(input, startOffsetInBytes).Success;
    }

    /// <summary>Finds the first match in the UTF-8 input.</summary>
    public Utf8PythonValueMatch Search(ReadOnlySpan<byte> input) => Search(input, 0);

    /// <summary>Finds the first match at or after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonValueMatch Search(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var match = startOffsetInBytes == 0 && _canUseZeroOffsetUtf8SearchValueFastPath
                ? _utf8Regex.Match(input)
                : _utf8Regex.MatchFromUtf16Offset(
                    input,
                    GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));
            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }

        return SearchViaManagedRegex(input, startOffsetInBytes);
    }

    /// <summary>Matches at the beginning of the UTF-8 input.</summary>
    public Utf8PythonValueMatch Match(ReadOnlySpan<byte> input) => Match(input, 0);

    /// <summary>Matches exactly at a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonValueMatch Match(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (startOffsetInBytes == 0 && _asciiLiteralPrefixDigitMatchPrefix is not null)
        {
            var end = PythonReAsciiPrefixDigitMatcher.MatchValidated(
                input,
                _asciiLiteralPrefixDigitMatchPrefix);
            return end == 0
                ? default
                : Utf8PythonValueMatch.Create(input, new PythonReGroupData
                {
                    Number = 0,
                    Success = true,
                    StartOffsetInBytes = 0,
                    EndOffsetInBytes = end,
                    StartOffsetInUtf16 = 0,
                    EndOffsetInUtf16 = end,
                });
        }

        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var startOffsetInUtf16 = startOffsetInBytes == 0
                ? 0
                : GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var match = startOffsetInBytes == 0 && _canUseZeroOffsetUtf8SearchValueFastPath
                ? _utf8Regex.Match(input)
                : _utf8Regex.MatchFromUtf16Offset(input, startOffsetInUtf16);
            if (!match.Success || match.IndexInUtf16 != startOffsetInUtf16)
            {
                return default;
            }

            return Utf8PythonValueMatchFromUtf8Regex(input, match);
        }

        var context = MatchDetailed(input, startOffsetInBytes);
        return context.Value;
    }

    /// <summary>Matches the complete UTF-8 input.</summary>
    public Utf8PythonValueMatch FullMatch(ReadOnlySpan<byte> input) => FullMatch(input, 0);

    /// <summary>Matches the complete UTF-8 suffix beginning at a zero-based byte offset.</summary>
    public Utf8PythonValueMatch FullMatch(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_asciiDotAllFullMatchPrefix is not null && _asciiDotAllFullMatchSuffix is not null)
        {
            var validation = Utf8InputAnalyzer.ValidateOnly(input);
            var tail = input[startOffsetInBytes..];
            if (tail.Length < _asciiDotAllFullMatchPrefix.Length ||
                tail.Length - _asciiDotAllFullMatchPrefix.Length < _asciiDotAllFullMatchSuffix.Length ||
                !tail.StartsWith(_asciiDotAllFullMatchPrefix) ||
                !tail.EndsWith(_asciiDotAllFullMatchSuffix))
            {
                return default;
            }

            var startOffsetInUtf16 = startOffsetInBytes == 0
                ? 0
                : GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return Utf8PythonValueMatch.Create(input, new PythonReGroupData
            {
                Number = 0,
                Success = true,
                StartOffsetInBytes = startOffsetInBytes,
                EndOffsetInBytes = input.Length,
                StartOffsetInUtf16 = startOffsetInUtf16,
                EndOffsetInUtf16 = validation.Utf16Length,
            });
        }

        var utf8FullRegex = GetUtf8FullRegex();
        if (utf8FullRegex is not null)
        {
            var tail = input[startOffsetInBytes..];
            var match = utf8FullRegex.Match(tail);
            if (!match.Success)
            {
                return default;
            }

            var utf16BaseOffset = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return Utf8PythonValueMatchFromUtf8Regex(input, match, startOffsetInBytes, utf16BaseOffset);
        }

        return FullMatchViaManagedRegex(input, startOffsetInBytes);
    }

    /// <summary>Counts non-overlapping Python-style matches in the UTF-8 input.</summary>
    public int Count(ReadOnlySpan<byte> input) => Count(input, 0);

    /// <summary>Counts non-overlapping matches at or after a zero-based UTF-8 byte offset.</summary>
    public int Count(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_canCountAsciiWordBoundariesDirectly && MatchTimeout == Timeout.InfiniteTimeSpan)
        {
            var decoded = Decode(input);
            return CountAsciiWordBoundaries(decoded, GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes));
        }

        if (_countBackend == PythonReDirectBackendKind.Utf8Regex && _utf8Regex is not null && startOffsetInBytes == 0)
        {
            return _utf8Regex.Count(input);
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (!_canMatchEmpty)
        {
            return _managedRegex.Count(subject, startOffsetInUtf16);
        }

        return CountManagedMatchesPythonStyle(subject, startOffsetInUtf16);
    }

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

    /// <summary>Finds the first match and exposes its capture context.</summary>
    public Utf8PythonMatchContext SearchDetailed(ReadOnlySpan<byte> input) => SearchDetailed(input, 0);

    /// <summary>Finds the first capture context at or after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonMatchContext SearchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            return CreateMatchContextFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        return CreateMatchContext(input, match, indexMap);
    }

    /// <summary>Matches at the beginning of the input and exposes its capture context.</summary>
    public Utf8PythonMatchContext MatchDetailed(ReadOnlySpan<byte> input) => MatchDetailed(input, 0);

    /// <summary>Matches at a zero-based UTF-8 byte offset and exposes its capture context.</summary>
    public Utf8PythonMatchContext MatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            if (!utf8Match.Success || utf8Match.IndexInUtf16 != utf8StartOffsetInUtf16)
            {
                return default;
            }

            return CreateMatchContextFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!match.Success || match.Index != startOffsetInUtf16)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateMatchContext(input, match, indexMap);
    }

    /// <summary>Matches the complete input and exposes its capture context.</summary>
    public Utf8PythonMatchContext FullMatchDetailed(ReadOnlySpan<byte> input) => FullMatchDetailed(input, 0);

    /// <summary>Matches the complete UTF-8 suffix and exposes its capture context.</summary>
    public Utf8PythonMatchContext FullMatchDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var utf8FullRegex = GetUtf8FullRegex();
        if (utf8FullRegex is not null)
        {
            var utf8Tail = input[startOffsetInBytes..];
            var utf8Match = utf8FullRegex.MatchDetailed(utf8Tail);
            if (!utf8Match.Success)
            {
                return default;
            }

            var utf16BaseOffset = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return CreateMatchContextFromUtf8(input, utf8Match, startOffsetInBytes, utf16BaseOffset);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var tail = subject[startOffsetInUtf16..];
        var match = _managedFullRegex.Match(tail);
        if (!match.Success)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateMatchContext(input, match, indexMap, startOffsetInUtf16);
    }

    /// <summary>Returns the first matched value as a string, or <see langword="null"/>.</summary>
    public string? SearchToString(ReadOnlySpan<byte> input) => SearchToString(input, 0);

    /// <summary>Returns the first matched value after a UTF-8 byte offset, or <see langword="null"/>.</summary>
    public string? SearchToString(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = Search(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Returns the prefix match as a string, or <see langword="null"/>.</summary>
    public string? MatchToString(ReadOnlySpan<byte> input) => MatchToString(input, 0);

    /// <summary>Returns the match at a UTF-8 byte offset as a string, or <see langword="null"/>.</summary>
    public string? MatchToString(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = Match(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Returns the complete-input match as a string, or <see langword="null"/>.</summary>
    public string? FullMatchToString(ReadOnlySpan<byte> input) => FullMatchToString(input, 0);

    /// <summary>Returns the complete-suffix match as a string, or <see langword="null"/>.</summary>
    public string? FullMatchToString(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var match = FullMatch(input, startOffsetInBytes);
        return match.Success ? match.GetValueString() : null;
    }

    /// <summary>Finds and materializes the first match and all capture data.</summary>
    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input) => SearchDetailedData(input, 0);

    /// <summary>Finds and materializes capture data after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonDetailedMatchData SearchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            return CreateDetailedMatchDataFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateDetailedMatchData(input, match, indexMap);
    }

    /// <summary>Matches at the beginning and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData MatchDetailedData(ReadOnlySpan<byte> input) => MatchDetailedData(input, 0);

    /// <summary>Matches at a zero-based UTF-8 byte offset and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData MatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        if (_utf8Regex is not null)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var utf8StartOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            var utf8Match = _utf8Regex.MatchDetailedFromUtf16Offset(input, utf8StartOffsetInUtf16);
            if (!utf8Match.Success || utf8Match.IndexInUtf16 != utf8StartOffsetInUtf16)
            {
                return default;
            }

            return CreateDetailedMatchDataFromUtf8(input, utf8Match);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!match.Success || match.Index != startOffsetInUtf16)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateDetailedMatchData(input, match, indexMap);
    }

    /// <summary>Matches the complete input and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData FullMatchDetailedData(ReadOnlySpan<byte> input) => FullMatchDetailedData(input, 0);

    /// <summary>Matches the complete UTF-8 suffix and materializes all capture data.</summary>
    public Utf8PythonDetailedMatchData FullMatchDetailedData(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var utf8FullRegex = GetUtf8FullRegex();
        if (utf8FullRegex is not null)
        {
            var utf8Tail = input[startOffsetInBytes..];
            var utf8Match = utf8FullRegex.MatchDetailed(utf8Tail);
            if (!utf8Match.Success)
            {
                return default;
            }

            var utf16BaseOffset = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return CreateDetailedMatchDataFromUtf8(input, utf8Match, startOffsetInBytes, utf16BaseOffset);
        }

        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var tail = subject[startOffsetInUtf16..];
        var match = _managedFullRegex.Match(tail);
        if (!match.Success)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return CreateDetailedMatchData(input, match, indexMap, startOffsetInUtf16);
    }

    /// <summary>Materializes all non-overlapping matches.</summary>
    public Utf8PythonMatchData[] FindAll(ReadOnlySpan<byte> input) => FindAll(input, 0);

    /// <summary>Materializes all matches at or after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonMatchData[] FindAll(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_findAllBackend == PythonReDirectBackendKind.Utf8Regex &&
            TryFindAllViaUtf8Regex(input, startOffsetInBytes, out var utf8Matches))
        {
            return utf8Matches;
        }

        var subject = Decode(input);
        var indexMap = default(PythonReUtf8IndexMap);
        var hasIndexMap = false;
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var matches = new List<Utf8PythonMatchData>();
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            if (!hasIndexMap)
            {
                indexMap = PythonReUtf8IndexMap.Create(input, subject);
                hasIndexMap = true;
            }

            var snapshot = CreateMatchSnapshot(match, indexMap);
            var context = snapshot.ToContext(input, _nameEntries);
            var value = context.Value;
            matches.Add(new Utf8PythonMatchData
            {
                Success = true,
                StartOffsetInBytes = value.StartOffsetInBytes,
                EndOffsetInBytes = value.EndOffsetInBytes,
                StartOffsetInUtf16 = value.StartOffsetInUtf16,
                EndOffsetInUtf16 = value.EndOffsetInUtf16,
                ValueText = value.GetValueString(),
            });

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionMatchSnapshot(subject, match.Index, indexMap, out var nonEmptySnapshot))
            {
                var nonEmptyValue = nonEmptySnapshot.ToContext(input, _nameEntries).Value;
                matches.Add(new Utf8PythonMatchData
                {
                    Success = true,
                    StartOffsetInBytes = nonEmptyValue.StartOffsetInBytes,
                    EndOffsetInBytes = nonEmptyValue.EndOffsetInBytes,
                    StartOffsetInUtf16 = nonEmptyValue.StartOffsetInUtf16,
                    EndOffsetInUtf16 = nonEmptyValue.EndOffsetInUtf16,
                    ValueText = nonEmptyValue.GetValueString(),
                });
                searchIndex = nonEmptyValue.EndOffsetInUtf16;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = AdvancePastScalar(subject, match.Index);
        }

        return matches.ToArray();
    }

    /// <summary>Returns Python <c>findall</c> string values using capture-dependent shaping.</summary>
    public Utf8PythonFindAllResult FindAllToStrings(ReadOnlySpan<byte> input) => FindAllToStrings(input, 0);

    /// <summary>Returns shaped <c>findall</c> strings after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonFindAllResult FindAllToStrings(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 0)
        {
            if (_repeatedExactFindAllString is not null &&
                _findAllBackend == PythonReDirectBackendKind.Utf8Regex &&
                _utf8Regex is not null)
            {
                var validatedInput = Utf8ValidatedInput.Create(input);
                var start = validatedInput.GetBytePosition(startOffsetInBytes, nameof(startOffsetInBytes));
                var values = new string[_utf8Regex.ByteOffsetExecution.CountPrepared(validatedInput, start)];
                Array.Fill(values, _repeatedExactFindAllString);
                return new Utf8PythonFindAllResult
                {
                    Shape = Utf8PythonFindAllShape.FullMatch,
                    ScalarValues = values,
                    TupleValues = [],
                };
            }

            if (TryCollectUtf8MatchRanges(input, startOffsetInBytes, out var ranges))
            {
                try
                {
                    var values = new string[ranges.Count];
                    if (_repeatedExactFindAllString is not null)
                    {
                        Array.Fill(values, _repeatedExactFindAllString);
                    }
                    else
                    {
                        for (var i = 0; i < values.Length; i++)
                        {
                            var range = ranges[i];
                            values[i] = Encoding.UTF8.GetString(input.Slice(range.IndexInBytes, range.LengthInBytes));
                        }
                    }

                    return new Utf8PythonFindAllResult
                    {
                        Shape = Utf8PythonFindAllShape.FullMatch,
                        ScalarValues = values,
                        TupleValues = [],
                    };
                }
                finally
                {
                    ranges.Dispose();
                }
            }

            var matches = FindAll(input, startOffsetInBytes);
            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.FullMatch,
                ScalarValues = matches.Select(x => x.ValueText).ToArray(),
                TupleValues = [],
            };
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 1)
        {
            if (_optionalExactCaptureFindAllValue is not null)
            {
                List<string> directValues = [];
                foreach (var match in _managedRegex.EnumerateMatches(subject, startOffsetInUtf16))
                {
                    directValues.Add(match.Length switch
                    {
                        var length when length == _optionalExactCapturePresentMatchLength =>
                            _optionalExactCaptureFindAllValue,
                        var length when length == _optionalExactCaptureAbsentMatchLength => string.Empty,
                        _ => throw new InvalidOperationException(
                            "The optional exact-capture route encountered an unexpected full-match length."),
                    });
                }

                return new Utf8PythonFindAllResult
                {
                    Shape = Utf8PythonFindAllShape.SingleGroup,
                    ScalarValues = directValues.ToArray(),
                    TupleValues = [],
                };
            }

            if (_singleTrailingCapturePrefixLength >= 0)
            {
                List<string> directValues = [];
                foreach (var match in _managedRegex.EnumerateMatches(subject, startOffsetInUtf16))
                {
                    var captureStart = match.Index + _singleTrailingCapturePrefixLength;
                    directValues.Add(subject.Substring(
                        captureStart,
                        match.Length - _singleTrailingCapturePrefixLength));
                }

                return new Utf8PythonFindAllResult
                {
                    Shape = Utf8PythonFindAllShape.SingleGroup,
                    ScalarValues = directValues.ToArray(),
                    TupleValues = [],
                };
            }

            List<string> collected = [];
            var searchIndex = startOffsetInUtf16;
            while (searchIndex <= subject.Length)
            {
                var match = _managedRegex.Match(subject, searchIndex);
                if (!match.Success)
                {
                    break;
                }

                AppendFindAllScalarValue(collected, match, 1);

                if (match.Length > 0)
                {
                    searchIndex = match.Index + match.Length;
                    continue;
                }

                if (TryCreateNonEmptySamePositionManagedMatch(
                    subject,
                    match.Index,
                    out var nonEmptyMatch,
                    out var nonEmptyUtf16BaseOffset))
                {
                    AppendFindAllScalarValue(collected, nonEmptyMatch, 1);
                    searchIndex = nonEmptyUtf16BaseOffset + nonEmptyMatch.Index + nonEmptyMatch.Length;
                    continue;
                }

                if (match.Index >= subject.Length)
                {
                    break;
                }

                searchIndex = AdvancePastScalar(subject, match.Index);
            }

            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.SingleGroup,
                ScalarValues = collected.ToArray(),
                TupleValues = [],
            };
        }

        if (_separatedCaptureTupleSeparator is { } separator)
        {
            List<string[]> directTuples = [];
            foreach (var match in _managedRegex.EnumerateMatches(subject, startOffsetInUtf16))
            {
                var matched = subject.AsSpan(match.Index, match.Length);
                var separatorIndex = matched.IndexOf(separator);
                directTuples.Add([
                    new string(matched[..separatorIndex]),
                    new string(matched[(separatorIndex + 1)..]),
                ]);
            }

            return new Utf8PythonFindAllResult
            {
                Shape = Utf8PythonFindAllShape.GroupTuple,
                ScalarValues = [],
                TupleValues = directTuples.ToArray(),
            };
        }

        List<string[]> tuples = [];
        {
            var searchIndex = startOffsetInUtf16;
            while (searchIndex <= subject.Length)
            {
                var match = _managedRegex.Match(subject, searchIndex);
                if (!match.Success)
                {
                    break;
                }

                AppendFindAllTupleValue(tuples, match);

                if (match.Length > 0)
                {
                    searchIndex = match.Index + match.Length;
                    continue;
                }

                if (TryCreateNonEmptySamePositionManagedMatch(
                    subject,
                    match.Index,
                    out var nonEmptyMatch,
                    out var nonEmptyUtf16BaseOffset))
                {
                    AppendFindAllTupleValue(tuples, nonEmptyMatch);
                    searchIndex = nonEmptyUtf16BaseOffset + nonEmptyMatch.Index + nonEmptyMatch.Length;
                    continue;
                }

                if (match.Index >= subject.Length)
                {
                    break;
                }

                searchIndex = AdvancePastScalar(subject, match.Index);
            }
        }

        return new Utf8PythonFindAllResult
        {
            Shape = Utf8PythonFindAllShape.GroupTuple,
            ScalarValues = [],
            TupleValues = tuples.ToArray(),
        };
    }

    /// <summary>Returns Python <c>findall</c> values as owned UTF-8 byte arrays.</summary>
    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input) => FindAllToUtf8(input, 0);

    /// <summary>Returns shaped UTF-8 <c>findall</c> values after a byte offset.</summary>
    public Utf8PythonFindAllUtf8Result FindAllToUtf8(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 0)
        {
            if (TryCollectUtf8MatchRanges(input, startOffsetInBytes, out var ranges))
            {
                try
                {
                    var directValues = new byte[ranges.Count][];
                    for (var i = 0; i < directValues.Length; i++)
                    {
                        var range = ranges[i];
                        directValues[i] = input.Slice(range.IndexInBytes, range.LengthInBytes).ToArray();
                    }

                    return new Utf8PythonFindAllUtf8Result
                    {
                        Shape = Utf8PythonFindAllShape.FullMatch,
                        ScalarValues = directValues,
                        TupleValues = [],
                    };
                }
                finally
                {
                    ranges.Dispose();
                }
            }

            var matches = FindAll(input, startOffsetInBytes);
            var values = new byte[matches.Length][];
            for (var i = 0; i < matches.Length; i++)
            {
                values[i] = input[matches[i].StartOffsetInBytes..matches[i].EndOffsetInBytes].ToArray();
            }

            return new Utf8PythonFindAllUtf8Result
            {
                Shape = Utf8PythonFindAllShape.FullMatch,
                ScalarValues = values,
                TupleValues = [],
            };
        }

        var subject = Decode(input);
        var indexMap = default(PythonReUtf8IndexMap);
        var hasIndexMap = false;
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (_translation.CaptureGroupCount == 1)
        {
            List<byte[]> collected = [];
            var searchIndex = startOffsetInUtf16;
            while (searchIndex <= subject.Length)
            {
                var match = _managedRegex.Match(subject, searchIndex);
                if (!match.Success)
                {
                    break;
                }

                if (!hasIndexMap)
                {
                    indexMap = PythonReUtf8IndexMap.Create(input, subject);
                    hasIndexMap = true;
                }

                AppendFindAllScalarBytes(collected, input, CreateMatchSnapshot(match, indexMap), 1);

                if (match.Length > 0)
                {
                    searchIndex = match.Index + match.Length;
                    continue;
                }

                if (TryCreateNonEmptySamePositionMatchSnapshot(subject, match.Index, indexMap, out var nonEmptySnapshot))
                {
                    AppendFindAllScalarBytes(collected, input, nonEmptySnapshot, 1);
                    searchIndex = nonEmptySnapshot.Groups[0].EndOffsetInUtf16;
                    continue;
                }

                if (match.Index >= subject.Length)
                {
                    break;
                }

                searchIndex = AdvancePastScalar(subject, match.Index);
            }

            return new Utf8PythonFindAllUtf8Result
            {
                Shape = Utf8PythonFindAllShape.SingleGroup,
                ScalarValues = collected.ToArray(),
                TupleValues = [],
            };
        }

        List<byte[][]> tuples = [];
        {
            var searchIndex = startOffsetInUtf16;
            while (searchIndex <= subject.Length)
            {
                var match = _managedRegex.Match(subject, searchIndex);
                if (!match.Success)
                {
                    break;
                }

                if (!hasIndexMap)
                {
                    indexMap = PythonReUtf8IndexMap.Create(input, subject);
                    hasIndexMap = true;
                }

                AppendFindAllTupleBytes(tuples, input, CreateMatchSnapshot(match, indexMap));

                if (match.Length > 0)
                {
                    searchIndex = match.Index + match.Length;
                    continue;
                }

                if (TryCreateNonEmptySamePositionMatchSnapshot(subject, match.Index, indexMap, out var nonEmptySnapshot))
                {
                    AppendFindAllTupleBytes(tuples, input, nonEmptySnapshot);
                    searchIndex = nonEmptySnapshot.Groups[0].EndOffsetInUtf16;
                    continue;
                }

                if (match.Index >= subject.Length)
                {
                    break;
                }

                searchIndex = AdvancePastScalar(subject, match.Index);
            }
        }

        return new Utf8PythonFindAllUtf8Result
        {
            Shape = Utf8PythonFindAllShape.GroupTuple,
            ScalarValues = [],
            TupleValues = tuples.ToArray(),
        };
    }

    /// <summary>Materializes detailed data for every non-overlapping match.</summary>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input) => FindIterDetailed(input, 0);

    /// <summary>Materializes detailed matches after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonDetailedMatchData[] FindIterDetailed(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        if (!_canMatchEmpty)
        {
            return FindIterDetailedNonEmpty(input, startOffsetInBytes);
        }

        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var firstMatch = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!firstMatch.Success)
        {
            return [];
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        List<Utf8PythonDetailedMatchData> matches = [];
        var searchIndex = startOffsetInUtf16;
        var match = firstMatch;
        while (match.Success)
        {
            matches.Add(CreateDetailedMatchData(input, match, indexMap));

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                match = _managedRegex.Match(subject, searchIndex);
                continue;
            }

            if (TryCreateNonEmptySamePositionManagedMatch(
                    subject,
                    match.Index,
                    out var nonEmptyMatch,
                    out _))
            {
                matches.Add(CreateDetailedMatchData(input, nonEmptyMatch, indexMap));
                searchIndex = nonEmptyMatch.Index + nonEmptyMatch.Length;
                match = _managedRegex.Match(subject, searchIndex);
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = AdvancePastScalar(subject, match.Index);
            match = _managedRegex.Match(subject, searchIndex);
        }

        return matches.ToArray();
    }

    private Utf8PythonDetailedMatchData[] FindIterDetailedNonEmpty(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var firstMatch = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!firstMatch.Success)
        {
            return [];
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        List<Utf8PythonDetailedMatchData> matches = [];
        for (var match = firstMatch; match.Success; match = match.NextMatch())
        {
            matches.Add(CreateDetailedMatchData(input, match, indexMap));
        }

        return matches.ToArray();
    }

    /// <summary>Replaces all matches and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement) => Replace(input, replacement, 0, 0);

    /// <summary>Replaces at most the requested number of matches and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count) =>
        Replace(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns owned UTF-8 bytes.</summary>
    public byte[] Replace(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes)
    {
        if (!_canMatchEmpty || _canUseManagedEmptyReplacementFastPath)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var plan = PythonReReplacementParser.Parse(replacement, _translation.CaptureGroupCount, _namedGroups);
            if (!_canMatchEmpty && TryReplaceViaUtf8Literal(
                    input,
                    plan,
                    count,
                    startOffsetInBytes,
                    out var resultBytes,
                    out _))
            {
                return resultBytes;
            }

            var subject = Decode(input);
            var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return Encoding.UTF8.GetBytes(ReplaceManagedSubject(subject, plan, count, startOffsetInUtf16));
        }

        return Encoding.UTF8.GetBytes(ReplaceToString(input, replacement, count, startOffsetInBytes));
    }

    /// <summary>Replaces all matches and returns a string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement) =>
        ReplaceToString(input, replacement, 0, 0);

    /// <summary>Replaces at most the requested number of matches and returns a string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int count) =>
        ReplaceToString(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns a string.</summary>
    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement, int count, int startOffsetInBytes)
    {
        if (!_canMatchEmpty || _canUseManagedEmptyReplacementFastPath)
        {
            ValidateStartOffset(input, startOffsetInBytes);
            var plan = PythonReReplacementParser.Parse(replacement, _translation.CaptureGroupCount, _namedGroups);
            if (!_canMatchEmpty && TryReplaceViaUtf8Literal(
                    input,
                    plan,
                    count,
                    startOffsetInBytes,
                    out var resultBytes,
                    out _))
            {
                return Encoding.UTF8.GetString(resultBytes);
            }

            var subject = Decode(input);
            var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
            return ReplaceManagedSubject(subject, plan, count, startOffsetInUtf16);
        }

        return SubnToString(input, replacement, count, startOffsetInBytes).ResultText;
    }

    /// <summary>Replaces all matches and returns the string plus replacement count.</summary>
    public Utf8PythonSubnResult SubnToString(ReadOnlySpan<byte> input, string replacement) =>
        SubnToString(input, replacement, 0, 0);

    /// <summary>Replaces a bounded number of matches and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString(ReadOnlySpan<byte> input, string replacement, int count) =>
        SubnToString(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString(
        ReadOnlySpan<byte> input,
        string replacement,
        int count,
        int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var plan = PythonReReplacementParser.Parse(replacement, _translation.CaptureGroupCount, _namedGroups);
        if (!_canMatchEmpty || _canUseManagedEmptyReplacementFastPath)
        {
            if (!_canMatchEmpty && TryReplaceViaUtf8Literal(
                    input,
                    plan,
                    count,
                    startOffsetInBytes,
                    out var resultBytes,
                    out var replacementCount))
            {
                return new Utf8PythonSubnResult
                {
                    ResultText = Encoding.UTF8.GetString(resultBytes),
                    ReplacementCount = replacementCount,
                };
            }

            return SubnToStringViaManagedRegex(input, plan, count, startOffsetInBytes);
        }

        var subject = Decode(input);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var builder = new StringBuilder();
        builder.Append(subject.AsSpan(0, startOffsetInUtf16));

        var replaced = 0;
        var lastIndex = startOffsetInUtf16;
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            var snapshot = CreateMatchSnapshot(match, indexMap);
            var context = snapshot.ToContext(input, _nameEntries);
            var value = context.Value;
            if (count == 0 || replaced < count)
            {
                builder.Append(subject.AsSpan(lastIndex, value.StartOffsetInUtf16 - lastIndex));
                builder.Append(plan.Expand(context));
                lastIndex = value.EndOffsetInUtf16;
                replaced++;
            }
            else
            {
                break;
            }

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionMatchSnapshot(subject, match.Index, indexMap, out var nonEmptySnapshot))
            {
                var nonEmptyContext = nonEmptySnapshot.ToContext(input, _nameEntries);
                var nonEmptyValue = nonEmptyContext.Value;
                if (count == 0 || replaced < count)
                {
                    builder.Append(subject.AsSpan(lastIndex, nonEmptyValue.StartOffsetInUtf16 - lastIndex));
                    builder.Append(plan.Expand(nonEmptyContext));
                    lastIndex = nonEmptyValue.EndOffsetInUtf16;
                    replaced++;
                }

                searchIndex = nonEmptyValue.EndOffsetInUtf16;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = AdvancePastScalar(subject, match.Index);
        }

        builder.Append(subject.AsSpan(lastIndex));
        return new Utf8PythonSubnResult
        {
            ResultText = builder.ToString(),
            ReplacementCount = replaced,
        };
    }

    /// <summary>Replaces all matches and returns UTF-8 bytes plus replacement count.</summary>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, string replacement) =>
        Subn(input, replacement, 0, 0);

    /// <summary>Replaces a bounded number of matches and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn(ReadOnlySpan<byte> input, string replacement, int count) =>
        Subn(input, replacement, count, 0);

    /// <summary>Replaces matches after a byte offset and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn(
        ReadOnlySpan<byte> input,
        string replacement,
        int count,
        int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var plan = PythonReReplacementParser.Parse(replacement, _translation.CaptureGroupCount, _namedGroups);
        if (!_canMatchEmpty || _canUseManagedEmptyReplacementFastPath)
        {
            if (!_canMatchEmpty && TryReplaceViaUtf8Literal(
                    input,
                    plan,
                    count,
                    startOffsetInBytes,
                    out var resultBytes,
                    out var replacementCount))
            {
                return new Utf8PythonSubnUtf8Result
                {
                    ResultBytes = resultBytes,
                    ReplacementCount = replacementCount,
                };
            }

            var managedResult = SubnToStringViaManagedRegex(input, plan, count, startOffsetInBytes);
            return new Utf8PythonSubnUtf8Result
            {
                ResultBytes = Encoding.UTF8.GetBytes(managedResult.ResultText),
                ReplacementCount = managedResult.ReplacementCount,
            };
        }

        return SubnManagedUtf8(
            input,
            startOffsetInBytes,
            count,
            static (source, match, indexMap, utf16BaseOffset, state) =>
                state.ExpandToUtf8(source, CreateMatchSnapshot(match, indexMap, utf16BaseOffset).Groups),
            plan);
    }

    /// <summary>Replaces all matches with a stateful string evaluator and returns UTF-8 bytes.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => Replace(input, state, evaluator, 0, 0);

    /// <summary>Replaces a bounded number of matches with a stateful string evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => Replace(input, state, evaluator, count, 0);

    /// <summary>Replaces matches after a byte offset with a stateful string evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return Subn(input, state, evaluator, count, startOffsetInBytes).ResultBytes;
    }

    /// <summary>Replaces all matches with a stateful evaluator and returns a string.</summary>
    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => ReplaceToString(input, state, evaluator, 0, 0);

    /// <summary>Replaces a bounded number of matches with a stateful evaluator.</summary>
    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => ReplaceToString(input, state, evaluator, count, 0);

    /// <summary>Replaces matches after a byte offset with a stateful evaluator.</summary>
    public string ReplaceToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnToString(input, state, evaluator, count, startOffsetInBytes).ResultText;
    }

    /// <summary>Evaluates all replacements and returns the string plus replacement count.</summary>
    public Utf8PythonSubnResult SubnToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => SubnToString(input, state, evaluator, 0, 0);

    /// <summary>Evaluates a bounded number of replacements and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => SubnToString(input, state, evaluator, count, 0);

    /// <summary>Evaluates replacements after a byte offset and returns the string plus count.</summary>
    public Utf8PythonSubnResult SubnToString<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);

        var subject = Decode(input);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var builder = new StringBuilder(subject.Length);
        builder.Append(subject.AsSpan(0, startOffsetInUtf16));

        var replaced = 0;
        var lastIndex = startOffsetInUtf16;
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            var detailed = CreateDetailedMatchData(input, match, indexMap);
            var value = detailed.Value;
            if (count == 0 || replaced < count)
            {
                builder.Append(subject.AsSpan(lastIndex, value.StartOffsetInUtf16 - lastIndex));
                builder.Append(evaluator(state, detailed));
                lastIndex = value.EndOffsetInUtf16;
                replaced++;
            }
            else
            {
                break;
            }

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionManagedMatch(
                subject,
                match.Index,
                out var nonEmptyMatch,
                out var nonEmptyUtf16BaseOffset))
            {
                var nonEmptyDetailed = CreateDetailedMatchData(
                    input,
                    nonEmptyMatch,
                    indexMap,
                    nonEmptyUtf16BaseOffset);
                var nonEmptyValue = nonEmptyDetailed.Value;
                if (count == 0 || replaced < count)
                {
                    builder.Append(subject.AsSpan(lastIndex, nonEmptyValue.StartOffsetInUtf16 - lastIndex));
                    builder.Append(evaluator(state, nonEmptyDetailed));
                    lastIndex = nonEmptyValue.EndOffsetInUtf16;
                    replaced++;
                }

                searchIndex = nonEmptyValue.EndOffsetInUtf16;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = AdvancePastScalar(subject, match.Index);
        }

        builder.Append(subject.AsSpan(lastIndex));
        return new Utf8PythonSubnResult
        {
            ResultText = builder.ToString(),
            ReplacementCount = replaced,
        };
    }

    /// <summary>Evaluates all string replacements and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator) => Subn(input, state, evaluator, 0, 0);

    /// <summary>Evaluates a bounded number of string replacements and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count) => Subn(input, state, evaluator, count, 0);

    /// <summary>Evaluates string replacements after a byte offset and returns UTF-8 bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonMatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnManagedUtf8(
            input,
            startOffsetInBytes,
            count,
            static (source, match, indexMap, utf16BaseOffset, state) => Encoding.UTF8.GetBytes(
                state.Evaluator(
                    state.Value,
                    CreateDetailedMatchData(source, match, indexMap, state.NameEntries, utf16BaseOffset))),
            (Value: state, Evaluator: evaluator, NameEntries: _nameEntries));
    }

    /// <summary>Replaces all matches with a stateful UTF-8 evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator) => Replace(input, state, evaluator, 0, 0);

    /// <summary>Replaces a bounded number of matches with a stateful UTF-8 evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count) => Replace(input, state, evaluator, count, 0);

    /// <summary>Replaces matches after a byte offset with a stateful UTF-8 evaluator.</summary>
    public byte[] Replace<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return Subn(input, state, evaluator, count, startOffsetInBytes).ResultBytes;
    }

    /// <summary>Evaluates all UTF-8 replacements and returns bytes plus replacement count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator) => Subn(input, state, evaluator, 0, 0);

    /// <summary>Evaluates a bounded number of UTF-8 replacements and returns bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count) => Subn(input, state, evaluator, count, 0);

    /// <summary>Evaluates UTF-8 replacements after a byte offset and returns bytes plus count.</summary>
    public Utf8PythonSubnUtf8Result Subn<TState>(
        ReadOnlySpan<byte> input,
        TState state,
        Utf8PythonUtf8MatchEvaluator<TState> evaluator,
        int count,
        int startOffsetInBytes)
    {
        return SubnManagedUtf8(
            input,
            startOffsetInBytes,
            count,
            static (source, match, indexMap, utf16BaseOffset, state) => state.Evaluator(
                state.Value,
                CreateDetailedMatchData(source, match, indexMap, state.NameEntries, utf16BaseOffset)),
            (Value: state, Evaluator: evaluator, NameEntries: _nameEntries));
    }

    /// <summary>Splits the complete input with unlimited Python-style splits.</summary>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input) => SplitToStrings(input, 0, 0);

    /// <summary>Splits the input at most the requested number of times.</summary>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input, int maxSplit) =>
        SplitToStrings(input, maxSplit, 0);

    /// <summary>Splits after a zero-based UTF-8 byte offset and returns strings and captures.</summary>
    public string?[] SplitToStrings(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        if (_canCountAsciiWordBoundariesDirectly &&
            MatchTimeout == Timeout.InfiniteTimeSpan &&
            startOffsetInBytes == 0 &&
            maxSplit >= 0)
        {
            return SplitAsciiWordBoundaries(subject, maxSplit);
        }

        // Regex.Split advances empty matches by UTF-16 code unit, whereas Python advances by scalar.
        if (_canUseManagedSplitFastPath &&
            (!_canMatchEmpty || input.IndexOfAnyInRange((byte)0xF0, (byte)0xF4) < 0) &&
            startOffsetInBytes == 0 &&
            maxSplit >= 0)
        {
            return maxSplit is 0 or int.MaxValue
                ? _managedRegex.Split(subject)
                : _managedRegex.Split(subject, maxSplit + 1);
        }

        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var parts = new List<string?>();
        var lastIndex = startOffsetInUtf16;
        var searchIndex = startOffsetInUtf16;
        var splitCount = 0;

        while (searchIndex <= subject.Length && (maxSplit == 0 || splitCount < maxSplit))
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            AppendSplitMatch(parts, subject, match, ref lastIndex);
            splitCount++;

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if ((maxSplit == 0 || splitCount < maxSplit) &&
                TryCreateNonEmptySamePositionManagedMatch(subject, match.Index, out var nonEmptyMatch, out var nonEmptyUtf16BaseOffset))
            {
                AppendSplitMatch(parts, subject, nonEmptyMatch, ref lastIndex, nonEmptyUtf16BaseOffset);
                splitCount++;
                searchIndex = nonEmptyUtf16BaseOffset + nonEmptyMatch.Index + nonEmptyMatch.Length;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = AdvancePastScalar(subject, match.Index);
        }

        parts.Add(subject[lastIndex..]);
        return parts.ToArray();
    }

    private static string?[] SplitAsciiWordBoundaries(string subject, int maxSplit)
    {
        var parts = new List<string?>();
        var lastIndex = 0;
        var splitCount = 0;
        var leftIsWord = false;
        for (var position = 0; position <= subject.Length; position++)
        {
            var rightIsWord = position < subject.Length && IsAsciiWord(subject[position]);
            if (leftIsWord != rightIsWord)
            {
                parts.Add(subject[lastIndex..position]);
                lastIndex = position;
                splitCount++;
                if (maxSplit > 0 && splitCount >= maxSplit)
                {
                    break;
                }
            }

            leftIsWord = rightIsWord;
        }

        parts.Add(subject[lastIndex..]);
        return parts.ToArray();
    }

    /// <summary>Splits the complete input and describes values and captured separators.</summary>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input) => SplitDetailed(input, 0, 0);

    /// <summary>Performs a bounded detailed split.</summary>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input, int maxSplit) =>
        SplitDetailed(input, maxSplit, 0);

    /// <summary>Performs a detailed split after a zero-based UTF-8 byte offset.</summary>
    public Utf8PythonSplitItem[] SplitDetailed(ReadOnlySpan<byte> input, int maxSplit, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        if (_canCountAsciiWordBoundariesDirectly &&
            MatchTimeout == Timeout.InfiniteTimeSpan &&
            startOffsetInBytes == 0 &&
            maxSplit >= 0)
        {
            var values = SplitAsciiWordBoundaries(subject, maxSplit);
            var items = new Utf8PythonSplitItem[values.Length];
            for (var index = 0; index < items.Length; index++)
            {
                items[index] = new Utf8PythonSplitItem
                {
                    ValueText = values[index],
                    IsCapture = false,
                    CaptureGroupNumber = 0,
                };
            }

            return items;
        }

        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var parts = new List<Utf8PythonSplitItem>();
        var lastIndex = startOffsetInUtf16;
        var searchIndex = startOffsetInUtf16;
        var splitCount = 0;

        while (searchIndex <= subject.Length && (maxSplit == 0 || splitCount < maxSplit))
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            AppendSplitDetailedMatch(parts, subject, match, ref lastIndex);
            splitCount++;

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if ((maxSplit == 0 || splitCount < maxSplit) &&
                TryCreateNonEmptySamePositionManagedMatch(subject, match.Index, out var nonEmptyMatch, out var nonEmptyUtf16BaseOffset))
            {
                AppendSplitDetailedMatch(parts, subject, nonEmptyMatch, ref lastIndex, nonEmptyUtf16BaseOffset);
                splitCount++;
                searchIndex = nonEmptyUtf16BaseOffset + nonEmptyMatch.Index + nonEmptyMatch.Length;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = AdvancePastScalar(subject, match.Index);
        }

        parts.Add(new Utf8PythonSplitItem
        {
            ValueText = subject[lastIndex..],
            IsCapture = false,
            CaptureGroupNumber = 0,
        });
        return parts.ToArray();
    }

    private Utf8PythonValueMatch SearchViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var match = _managedRegex.Match(subject, startOffsetInUtf16);
        if (!match.Success)
        {
            return default;
        }

        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        return Utf8PythonValueMatch.Create(input, PythonReGroupData.FromUtf16(0, match.Groups[0], indexMap));
    }

    private int CountManagedMatchesPythonStyle(
        string subject,
        int startOffsetInUtf16)
    {
        var count = 0;
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            count++;

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionManagedMatch(
                    subject,
                    match.Index,
                    out var nonEmptyAtSamePosition,
                    out var nonEmptyUtf16BaseOffset))
            {
                count++;
                searchIndex = nonEmptyUtf16BaseOffset +
                    nonEmptyAtSamePosition.Index +
                    nonEmptyAtSamePosition.Length;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = AdvancePastScalar(subject, match.Index);
        }

        return count;
    }

    private bool TryCreateNonEmptySamePositionMatchSnapshot(
        string subject,
        int utf16Position,
        PythonReUtf8IndexMap indexMap,
        out PythonReManagedMatchSnapshot snapshot)
    {
        if (_lazyManagedNonEmptyAtSamePositionRegex is null ||
            (uint)utf16Position >= (uint)subject.Length)
        {
            snapshot = default;
            return false;
        }

        var match = _lazyManagedNonEmptyAtSamePositionRegex.Value.Match(subject, utf16Position);
        if (!match.Success || match.Index != utf16Position || match.Length == 0)
        {
            snapshot = default;
            return false;
        }

        snapshot = CreateMatchSnapshot(match, indexMap);
        return true;
    }

    private bool TryCreateNonEmptySamePositionManagedMatch(
        string subject,
        int utf16Position,
        out Match match,
        out int utf16BaseOffset)
    {
        if (_lazyManagedNonEmptyAtSamePositionRegex is null ||
            (uint)utf16Position >= (uint)subject.Length)
        {
            match = System.Text.RegularExpressions.Match.Empty;
            utf16BaseOffset = 0;
            return false;
        }

        var candidate = _lazyManagedNonEmptyAtSamePositionRegex.Value.Match(subject, utf16Position);
        if (!candidate.Success || candidate.Index != utf16Position || candidate.Length == 0)
        {
            match = System.Text.RegularExpressions.Match.Empty;
            utf16BaseOffset = 0;
            return false;
        }

        match = candidate;
        utf16BaseOffset = 0;
        return true;
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

    internal string? DebugUtf8ExecutionKind => _utf8Regex?.Inspection.ExecutionKind.ToString();

    internal bool DebugUsesZeroOffsetUtf8ValueFastPath => _canUseZeroOffsetUtf8ValueFastPath;

    internal bool DebugUsesZeroOffsetUtf8SearchValueFastPath =>
        _canUseZeroOffsetUtf8SearchValueFastPath;

    internal string? DebugUtf8FullMatchExecutionKind =>
        GetUtf8FullRegex()?.Inspection.ExecutionKind.ToString();

    internal bool DebugIsUtf8FullRegexValueCreated => _lazyUtf8FullRegex?.IsValueCreated ?? false;

    internal bool DebugIsManagedNonEmptyAtSamePositionRegexValueCreated =>
        _lazyManagedNonEmptyAtSamePositionRegex?.IsValueCreated ?? false;

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
                @"\A(?:" + _translation.Pattern + @")\z",
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

    private static Regex CreateManagedRegex(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        try
        {
            return new Regex(pattern, options, matchTimeout);
        }
        catch (ArgumentException ex)
        {
            throw new PythonRePatternException(ex.Message);
        }
    }

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

    private Regex CreateManagedNonEmptyAtSamePositionRegex() => CreateManagedRegex(
        @"\G(?:(?:" + _translation.Pattern + @"))(?!\G)",
        _translation.RegexOptions,
        MatchTimeout);

    private static int AdvancePastScalar(string subject, int utf16Position) =>
        checked(utf16Position + Rune.GetRuneAt(subject, utf16Position).Utf16SequenceLength);

    private static int GetUtf16OffsetOfBytePrefix(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        return s_strictUtf8Encoding.GetCharCount(input[..startOffsetInBytes]);
    }

    private Utf8PythonValueMatch FullMatchViaManagedRegex(ReadOnlySpan<byte> input, int startOffsetInBytes)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        if (!_managedFullRegex.IsMatch(subject.AsSpan(startOffsetInUtf16)))
        {
            return default;
        }

        return Utf8PythonValueMatch.Create(input, new PythonReGroupData
        {
            Number = 0,
            Success = true,
            StartOffsetInBytes = startOffsetInBytes,
            EndOffsetInBytes = input.Length,
            StartOffsetInUtf16 = startOffsetInUtf16,
            EndOffsetInUtf16 = subject.Length,
        });
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
        if (_findAllBackend != PythonReDirectBackendKind.Utf8Regex || _utf8Regex is null)
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

    private Utf8PythonSubnResult SubnToStringViaManagedRegex(
        ReadOnlySpan<byte> input,
        PythonReReplacementPlan plan,
        int count,
        int startOffsetInBytes)
    {
        var subject = Decode(input);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var maximumReplacements = count == 0 ? int.MaxValue : count;
        var replacementCount = Math.Min(_managedRegex.Count(subject, startOffsetInUtf16), maximumReplacements);
        return new Utf8PythonSubnResult
        {
            ResultText = ReplaceManagedSubject(subject, plan, count, startOffsetInUtf16),
            ReplacementCount = replacementCount,
        };
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

    private string ReplaceManagedSubject(
        string subject,
        PythonReReplacementPlan plan,
        int count,
        int startOffsetInUtf16)
    {
        var maximumReplacements = count == 0 ? int.MaxValue : count;
        return _managedRegex.Replace(
            subject,
            plan.ToDotNetReplacementString(),
            maximumReplacements,
            startOffsetInUtf16);
    }

    private Utf8PythonMatchContext CreateMatchContext(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap) => CreateMatchContext(input, match, indexMap, 0);

    private Utf8PythonMatchContext CreateMatchContext(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap,
        int utf16BaseOffset)
    {
        if (!match.Success)
        {
            return default;
        }

        return CreateMatchSnapshot(match, indexMap, utf16BaseOffset).ToContext(input, _nameEntries);
    }

    private Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap) => CreateDetailedMatchData(input, match, indexMap, 0);

    private Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap,
        int utf16BaseOffset)
    {
        if (!match.Success)
        {
            return default;
        }

        return CreateDetailedMatchData(input, match, indexMap, _nameEntries, utf16BaseOffset);
    }

    private static Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap,
        PythonReNameEntry[] nameEntries) => CreateDetailedMatchData(input, match, indexMap, nameEntries, 0);

    private static Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        Match match,
        PythonReUtf8IndexMap indexMap,
        PythonReNameEntry[] nameEntries,
        int utf16BaseOffset)
    {
        if (!match.Success)
        {
            return default;
        }

        var projectedGroups = new Utf8PythonGroupMatchData[match.Groups.Count];
        for (var i = 0; i < projectedGroups.Length; i++)
        {
            projectedGroups[i] = CreateGroupMatchData(
                input,
                PythonReGroupData.FromUtf16(i, match.Groups[i], indexMap, utf16BaseOffset));
        }

        return new Utf8PythonDetailedMatchData
        {
            Groups = projectedGroups,
            NameEntries = nameEntries,
        };
    }

    private Utf8PythonMatchContext CreateMatchContextFromUtf8(
        ReadOnlySpan<byte> input,
        Utf8MatchContext match) => CreateMatchContextFromUtf8(input, match, 0, 0);

    private Utf8PythonMatchContext CreateMatchContextFromUtf8(
        ReadOnlySpan<byte> input,
        Utf8MatchContext match,
        int byteBaseOffset,
        int utf16BaseOffset)
    {
        if (!match.Success)
        {
            return default;
        }

        var groups = new PythonReGroupData[match.GroupCount];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = PythonReGroupData.FromUtf8Group(i, match.GetGroup(i), byteBaseOffset, utf16BaseOffset);
        }

        return new Utf8PythonMatchContext(input, groups, _nameEntries);
    }

    private Utf8PythonDetailedMatchData CreateDetailedMatchDataFromUtf8(
        ReadOnlySpan<byte> input,
        Utf8MatchContext match) => CreateDetailedMatchDataFromUtf8(input, match, 0, 0);

    private Utf8PythonDetailedMatchData CreateDetailedMatchDataFromUtf8(
        ReadOnlySpan<byte> input,
        Utf8MatchContext match,
        int byteBaseOffset,
        int utf16BaseOffset)
    {
        if (!match.Success)
        {
            return default;
        }

        var groups = new PythonReGroupData[match.GroupCount];
        for (var i = 0; i < groups.Length; i++)
        {
            groups[i] = PythonReGroupData.FromUtf8Group(i, match.GetGroup(i), byteBaseOffset, utf16BaseOffset);
        }

        return CreateDetailedMatchData(input, groups, _nameEntries);
    }

    private static PythonReManagedMatchSnapshot CreateMatchSnapshot(
        Match match,
        PythonReUtf8IndexMap indexMap) => CreateMatchSnapshot(match, indexMap, 0);

    private static PythonReManagedMatchSnapshot CreateMatchSnapshot(
        Match match,
        PythonReUtf8IndexMap indexMap,
        int utf16BaseOffset)
    {
        var groups = new PythonReGroupData[match.Groups.Count];
        for (var i = 0; i < match.Groups.Count; i++)
        {
            groups[i] = PythonReGroupData.FromUtf16(i, match.Groups[i], indexMap, utf16BaseOffset);
        }

        return new PythonReManagedMatchSnapshot(groups);
    }

    private static Utf8PythonDetailedMatchData CreateDetailedMatchData(
        ReadOnlySpan<byte> input,
        PythonReGroupData[]? groups,
        PythonReNameEntry[]? nameEntries)
    {
        if (groups is null)
        {
            return default;
        }

        var projectedGroups = new Utf8PythonGroupMatchData[groups.Length];
        for (var i = 0; i < groups.Length; i++)
        {
            projectedGroups[i] = CreateGroupMatchData(input, groups[i]);
        }

        return new Utf8PythonDetailedMatchData
        {
            Groups = projectedGroups,
            NameEntries = nameEntries ?? [],
        };
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

    private static string[] GetPublicGroupNames(Regex regex, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        var reverseMap = emittedGroupNames.ToDictionary(x => x.Value, x => x.Key, StringComparer.Ordinal);
        var names = regex.GetGroupNames();
        for (var i = 0; i < names.Length; i++)
        {
            if (reverseMap.TryGetValue(names[i], out var original))
            {
                names[i] = original;
            }
        }

        return names;
    }

    private static PythonReNameEntry[] GetManagedNameEntries(Regex regex, IReadOnlyDictionary<string, string> emittedGroupNames)
    {
        var entries = new List<PythonReNameEntry>(emittedGroupNames.Count);
        foreach (var pair in emittedGroupNames)
        {
            entries.Add(new PythonReNameEntry
            {
                Name = pair.Key,
                Number = regex.GroupNumberFromName(pair.Value),
            });
        }

        return entries.ToArray();
    }

    private static Utf8PythonValueMatch Utf8PythonValueMatchFromUtf8Regex(
        ReadOnlySpan<byte> input,
        Utf8ValueMatch match) => Utf8PythonValueMatchFromUtf8Regex(input, match, 0, 0);

    private static Utf8PythonValueMatch Utf8PythonValueMatchFromUtf8Regex(
        ReadOnlySpan<byte> input,
        Utf8ValueMatch match,
        int byteBaseOffset,
        int utf16BaseOffset)
    {
        if (!match.Success)
        {
            return default;
        }

        return Utf8PythonValueMatch.Create(input, PythonReGroupData.FromUtf8Match(match, byteBaseOffset, utf16BaseOffset));
    }

    private static void AppendSplitMatch(List<string?> parts, string subject, Match match, ref int lastIndex)
    {
        AppendSplitMatch(parts, subject, match, ref lastIndex, 0);
    }

    private static void AppendSplitMatch(
        List<string?> parts,
        string subject,
        Match match,
        ref int lastIndex,
        int utf16BaseOffset)
    {
        var absoluteStart = utf16BaseOffset + match.Index;
        var absoluteEnd = absoluteStart + match.Length;
        parts.Add(subject[lastIndex..absoluteStart]);
        for (var i = 1; i < match.Groups.Count; i++)
        {
            parts.Add(match.Groups[i].Success ? match.Groups[i].Value : null);
        }

        lastIndex = absoluteEnd;
    }

    private static void AppendSplitDetailedMatch(
        List<Utf8PythonSplitItem> parts,
        string subject,
        Match match,
        ref int lastIndex)
    {
        AppendSplitDetailedMatch(parts, subject, match, ref lastIndex, 0);
    }

    private static void AppendSplitDetailedMatch(
        List<Utf8PythonSplitItem> parts,
        string subject,
        Match match,
        ref int lastIndex,
        int utf16BaseOffset)
    {
        var absoluteStart = utf16BaseOffset + match.Index;
        var absoluteEnd = absoluteStart + match.Length;
        parts.Add(new Utf8PythonSplitItem
        {
            ValueText = subject[lastIndex..absoluteStart],
            IsCapture = false,
            CaptureGroupNumber = 0,
        });
        for (var i = 1; i < match.Groups.Count; i++)
        {
            parts.Add(new Utf8PythonSplitItem
            {
                ValueText = match.Groups[i].Success ? match.Groups[i].Value : null,
                IsCapture = true,
                CaptureGroupNumber = i,
            });
        }

        lastIndex = absoluteEnd;
    }

    private void AppendFindAllScalarValue(List<string> values, Match match, int groupNumber)
    {
        values.Add(GetFindAllGroupValue(match, groupNumber));
    }

    private static void AppendFindAllScalarBytes(List<byte[]> values, ReadOnlySpan<byte> input, PythonReManagedMatchSnapshot snapshot, int groupNumber)
    {
        values.Add(GetFindAllGroupBytes(input, snapshot.Groups, groupNumber));
    }

    private void AppendFindAllTupleValue(List<string[]> tuples, Match match)
    {
        var tuple = new string[_translation.CaptureGroupCount];
        for (var i = 0; i < tuple.Length; i++)
        {
            tuple[i] = GetFindAllGroupValue(match, i + 1);
        }

        tuples.Add(tuple);
    }

    private void AppendFindAllTupleBytes(List<byte[][]> tuples, ReadOnlySpan<byte> input, PythonReManagedMatchSnapshot snapshot)
    {
        var tuple = new byte[_translation.CaptureGroupCount][];
        for (var i = 0; i < tuple.Length; i++)
        {
            tuple[i] = GetFindAllGroupBytes(input, snapshot.Groups, i + 1);
        }

        tuples.Add(tuple);
    }

    private string GetFindAllGroupValue(Match match, int groupNumber)
    {
        var managedGroupNumber = _managedGroupNumbersByPythonGroup[groupNumber];
        return match.Groups[managedGroupNumber].Success
            ? match.Groups[managedGroupNumber].Value
            : string.Empty;
    }

    private static byte[] GetFindAllGroupBytes(ReadOnlySpan<byte> input, PythonReGroupData[] groups, int groupNumber)
    {
        return (uint)groupNumber < (uint)groups.Length && groups[groupNumber].Success
            ? PythonReValueTextExtractor.GetValueBytes(input, groups[groupNumber])
            : [];
    }

    private static Utf8PythonGroupMatchData CreateGroupMatchData(ReadOnlySpan<byte> input, PythonReGroupData group)
    {
        return new Utf8PythonGroupMatchData
        {
            Number = group.Number,
            Success = group.Success,
            StartOffsetInBytes = group.Success ? group.StartOffsetInBytes : 0,
            EndOffsetInBytes = group.Success ? group.EndOffsetInBytes : 0,
            StartOffsetInUtf16 = group.Success ? group.StartOffsetInUtf16 : 0,
            EndOffsetInUtf16 = group.Success ? group.EndOffsetInUtf16 : 0,
            HasContiguousByteRange = group.HasContiguousByteRange,
            ValueText = PythonReValueTextExtractor.GetValueString(input, group),
        };
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

    private Utf8PythonSubnUtf8Result SubnManagedUtf8<TState>(
        ReadOnlySpan<byte> input,
        int startOffsetInBytes,
        int count,
        Utf8ReplacementBytesFactory<TState> replacementFactory,
        TState state)
    {
        ValidateStartOffset(input, startOffsetInBytes);
        var subject = Decode(input);
        var indexMap = PythonReUtf8IndexMap.Create(input, subject);
        var startOffsetInUtf16 = GetUtf16OffsetOfBytePrefix(input, startOffsetInBytes);
        var builder = input.IsEmpty
            ? new ArrayBufferWriter<byte>()
            : new ArrayBufferWriter<byte>(input.Length);
        AppendUtf8(builder, input[..startOffsetInBytes]);

        var replaced = 0;
        var lastIndexInBytes = startOffsetInBytes;
        var searchIndex = startOffsetInUtf16;
        while (searchIndex <= subject.Length)
        {
            var match = _managedRegex.Match(subject, searchIndex);
            if (!match.Success)
            {
                break;
            }

            var value = PythonReGroupData.FromUtf16(0, match, indexMap);
            if (count == 0 || replaced < count)
            {
                AppendUtf8(builder, input[lastIndexInBytes..value.StartOffsetInBytes]);
                AppendUtf8(builder, replacementFactory(input, match, indexMap, 0, state));
                lastIndexInBytes = value.EndOffsetInBytes;
                replaced++;
            }
            else
            {
                break;
            }

            if (match.Length > 0)
            {
                searchIndex = match.Index + match.Length;
                continue;
            }

            if (TryCreateNonEmptySamePositionManagedMatch(
                subject,
                match.Index,
                out var nonEmptyMatch,
                out var nonEmptyUtf16BaseOffset))
            {
                var nonEmptyValue = PythonReGroupData.FromUtf16(
                    0,
                    nonEmptyMatch,
                    indexMap,
                    nonEmptyUtf16BaseOffset);
                if (count == 0 || replaced < count)
                {
                    AppendUtf8(builder, input[lastIndexInBytes..nonEmptyValue.StartOffsetInBytes]);
                    AppendUtf8(
                        builder,
                        replacementFactory(
                            input,
                            nonEmptyMatch,
                            indexMap,
                            nonEmptyUtf16BaseOffset,
                            state));
                    lastIndexInBytes = nonEmptyValue.EndOffsetInBytes;
                    replaced++;
                }

                searchIndex = nonEmptyValue.EndOffsetInUtf16;
                continue;
            }

            if (match.Index >= subject.Length)
            {
                break;
            }

            searchIndex = AdvancePastScalar(subject, match.Index);
        }

        AppendUtf8(builder, input[lastIndexInBytes..]);
        return new Utf8PythonSubnUtf8Result
        {
            ResultBytes = builder.WrittenSpan.ToArray(),
            ReplacementCount = replaced,
        };
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
