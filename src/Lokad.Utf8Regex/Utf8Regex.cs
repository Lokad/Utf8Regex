using System.Buffers;
using System.Text.Unicode;
using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.FrontEnd;
using Lokad.Utf8Regex.Internal.Input;

namespace Lokad.Utf8Regex;

public sealed partial class Utf8Regex
{
    private static TimeSpan s_defaultMatchTimeout = Regex.InfiniteMatchTimeout;

    private readonly Utf8RegexPlan _regexPlan;
    private readonly Utf8CompiledEngine _compiledEngine;
    private readonly Utf8VerifierRuntime _verifierRuntime;
    private readonly Utf8CompiledEngineRuntime _compiledEngineRuntime;
    private readonly int[] _groupNumbers;
    private readonly string[] _groupNames;
    private readonly Utf8Regex? _asciiCultureInvariantTwin;
    private readonly bool _hasDirectAnchoredHeadTailWithoutValidation;
    private readonly bool _hasDirectAnchoredValidatorWithoutValidation;
    private readonly bool _allowsTrailingNewlineBeforeEnd;
    private readonly AsciiSimplePatternAnchoredHeadTailRunPlan _anchoredHeadTailRunPlan;
    private readonly AsciiSimplePatternAnchoredValidatorPlan _anchoredValidatorPlan;
    private readonly AsciiSimplePatternAnchoredBoundedDatePlan _anchoredBoundedDatePlan;
    private readonly Utf8FallbackDirectFamilyPlan _fallbackDirectFamily;
    private readonly Utf8EmittedKernelMatcher? _directStructuralFamilyKernelMatcher;
    private readonly bool _hasDirectFallbackTokenFamilyWithoutValidation;
    private readonly bool _hasDirectFallbackAsciiFamilyWithoutValidation;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, Utf8AnalyzedReplacement> _replacementCache = new(StringComparer.Ordinal);

    public Utf8Regex(string pattern)
        : this(pattern, RegexOptions.CultureInvariant, DefaultMatchTimeout, allowAsciiCultureInvariantTwin: true)
    {
    }

    public Utf8Regex(string pattern, RegexOptions options)
        : this(pattern, options, DefaultMatchTimeout, allowAsciiCultureInvariantTwin: true)
    {
    }

    public Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout)
        : this(pattern, options, matchTimeout, allowAsciiCultureInvariantTwin: true)
    {
    }

    private Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout, bool allowAsciiCultureInvariantTwin)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        _ = new Regex(string.Empty, RegexOptions.None, matchTimeout);
        ValidateOptions(options);
        var effectiveOptions = Utf8RegexSyntax.NormalizeNonSemanticOptions(options);

        Pattern = pattern;
        Options = options;
        MatchTimeout = matchTimeout;

        var analysis = Utf8FrontEnd.Analyze(pattern, effectiveOptions);
        _regexPlan = analysis.RegexPlan;
        _compiledEngine = Utf8CompiledEngineSelector.Select(_regexPlan, (options & RegexOptions.Compiled) != 0);
        _verifierRuntime = Utf8VerifierRuntime.Create(_regexPlan, pattern, options, MatchTimeout);
        _compiledEngineRuntime = Utf8CompiledEngineRuntime.Create(_compiledEngine, _regexPlan, _verifierRuntime, options);
        _groupNumbers = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GetGroupNumbers();
        _groupNames = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GetGroupNames();
        var simplePatternPlan = _regexPlan.SimplePatternPlan;
        _allowsTrailingNewlineBeforeEnd = simplePatternPlan.AllowsTrailingNewlineBeforeEnd;
        _anchoredHeadTailRunPlan = simplePatternPlan.AnchoredHeadTailRunPlan;
        _anchoredValidatorPlan = simplePatternPlan.AnchoredValidatorPlan;
        _anchoredBoundedDatePlan = simplePatternPlan.AnchoredBoundedDatePlan;
        _fallbackDirectFamily = _regexPlan.FallbackDirectFamily;
        _directStructuralFamilyKernelMatcher =
            _regexPlan.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily &&
            Utf8EmittedKernelMatcher.TryCreate(_regexPlan.StructuralIdentifierFamilyPlan, _regexPlan.SearchPlan, out var structuralFamilyKernelMatcher)
                ? structuralFamilyKernelMatcher
                : null;
        _hasDirectAnchoredHeadTailWithoutValidation =
            _regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _anchoredHeadTailRunPlan.HasValue;
        _hasDirectAnchoredValidatorWithoutValidation =
            _regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _anchoredValidatorPlan.HasValue;
        _hasDirectFallbackTokenFamilyWithoutValidation =
            Utf8FallbackDirectFamilyCategories.IsTokenFindFamily(_fallbackDirectFamily.Kind);
        _hasDirectFallbackAsciiFamilyWithoutValidation =
            (_fallbackDirectFamily.SupportsAsciiDefinitiveIsMatch ||
             _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole) &&
            !_hasDirectFallbackTokenFamilyWithoutValidation;
        if (allowAsciiCultureInvariantTwin &&
            (effectiveOptions & RegexOptions.IgnoreCase) != 0 &&
            (effectiveOptions & RegexOptions.CultureInvariant) == 0)
        {
            var twin = new Utf8Regex(pattern, options | RegexOptions.CultureInvariant, matchTimeout, allowAsciiCultureInvariantTwin: false);
            _asciiCultureInvariantTwin = twin;
        }
    }

    public static TimeSpan DefaultMatchTimeout
    {
        get => s_defaultMatchTimeout;
        set
        {
            _ = new Regex(string.Empty, RegexOptions.None, value);
            s_defaultMatchTimeout = value;
        }
    }

    internal static TimeSpan DefaultTimeout => DefaultMatchTimeout;

    public string Pattern { get; }

    public RegexOptions Options { get; }

    public TimeSpan MatchTimeout { get; }

    internal NativeExecutionKind ExecutionKind => _regexPlan.ExecutionKind;

    internal Utf8RegexPlan RegexPlan => _regexPlan;

    internal Utf8SearchPlan SearchPlan => _regexPlan.SearchPlan;

    internal Utf8StructuralSearchPlan StructuralSearchPlan => _regexPlan.StructuralSearchPlan;

    internal Utf8SearchPortfolioKind SearchPortfolioKind => _regexPlan.SearchPlan.PortfolioKind;

    internal Utf8CompiledEngineKind CompiledEngineKind => _compiledEngine.Kind;

    internal Utf8CompiledExecutionBackend CompiledExecutionBackend => _compiledEngine.Backend;

    internal string DebugCompiledEngineRuntimeType => _compiledEngineRuntime.GetType().Name;

    internal bool DebugCanLowerEmittedKernel =>
        Utf8EmittedKernelLowerer.TryLower(_regexPlan, out _);

    internal string DebugLoweredEmittedKernelKind =>
        Utf8EmittedKernelLowerer.TryLower(_regexPlan, out var kernelPlan)
            ? kernelPlan.Kind.ToString()
            : "None";

    internal bool DebugUsesEmittedKernelMatcher => _compiledEngineRuntime.UsesEmittedKernelMatcher;

    internal string DebugFallbackDirectFamilyKind => _regexPlan.FallbackDirectFamily.Kind.ToString();

    internal bool DebugHasAsciiCultureInvariantTwin => _asciiCultureInvariantTwin is not null;

    internal NativeExecutionKind? DebugAsciiCultureInvariantTwinExecutionKind => _asciiCultureInvariantTwin?._regexPlan.ExecutionKind;

    internal Utf8CompiledEngineKind? DebugAsciiCultureInvariantTwinCompiledEngineKind => _asciiCultureInvariantTwin?._compiledEngine.Kind;

    internal string? DebugAsciiCultureInvariantTwinFallbackReason => _asciiCultureInvariantTwin?._regexPlan.FallbackReason;

    internal Utf8StructuralLinearProgramKind StructuralLinearProgramKind => _regexPlan.StructuralLinearProgram.Kind;

    internal Utf8StructuralVerifierPlan StructuralVerifierPlan => _verifierRuntime.StructuralVerifierPlan;

    internal AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan => _regexPlan.StructuralIdentifierFamilyPlan;

    internal Utf8AsciiStructuralIdentifierFamilyExecutor.SharedPrefixSuffixKernelDiagnostics
        DebugStructuralSharedPrefixSuffixKernelDiagnostics =>
            _regexPlan.ExecutionKind == NativeExecutionKind.AsciiStructuralIdentifierFamily
                ? Utf8AsciiStructuralIdentifierFamilyExecutor.GetSharedPrefixSuffixKernelDiagnostics(
                    _regexPlan.StructuralIdentifierFamilyPlan,
                    _regexPlan.SearchPlan)
                : default;

    internal AsciiSimplePatternPlan SimplePatternPlan => _regexPlan.SimplePatternPlan;

    internal bool DebugSimplePatternCanUseDirectAnchoredFixedLength =>
        Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedLengthSimplePattern(_regexPlan);

    internal bool DebugSimplePatternCanUseDirectAnchoredFixedAlternation =>
        Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedAlternationSimplePattern(_regexPlan);

    internal int DebugSimplePatternBranchCount => _regexPlan.SimplePatternPlan.Branches.Length;

    internal string DebugSimplePatternBranchLengths =>
        string.Join(",", _regexPlan.SimplePatternPlan.Branches.Select(static branch => branch.Length));

    internal string? FallbackReason => _regexPlan.FallbackReason;

    internal bool DebugRejectsByRequiredPrefilter(ReadOnlySpan<byte> input)
    {
        return RejectsByRequiredPrefilter(input);
    }

    internal bool DebugTryMatchViaAsciiCultureInvariantTwin(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            if (twin.TryMatchDirectWithoutValidation(input, out match))
            {
                return true;
            }

            match = twin.Match(input);
            return true;
        }

        match = Utf8ValueMatch.NoMatch;
        return false;
    }

    internal bool DebugTryGetAsciiCultureInvariantTwin(out Utf8Regex twin)
    {
        if (_asciiCultureInvariantTwin is not null)
        {
            twin = _asciiCultureInvariantTwin;
            return true;
        }

        twin = null!;
        return false;
    }

    internal int DebugCountViaCompiledEngine(ReadOnlySpan<byte> input)
    {
        return CountViaCompiledEngine(input, default, budget: null);
    }

    internal bool DebugTryCountExactUtf8LiteralValidatedThreeByte(ReadOnlySpan<byte> input, out int count)
    {
        return _compiledEngineRuntime.TryDebugCountExactUtf8LiteralValidatedThreeByte(input, out count);
    }

    internal bool DebugTryCountExactUtf8LiteralLeadingScalarAnchored(ReadOnlySpan<byte> input, out int count)
    {
        return _compiledEngineRuntime.TryDebugCountExactUtf8LiteralLeadingScalarAnchored(input, out count);
    }

    internal bool DebugTryCountExactUtf8LiteralPreparedSearch(ReadOnlySpan<byte> input, out int count)
    {
        return _compiledEngineRuntime.TryDebugCountExactUtf8LiteralPreparedSearch(input, out count);
    }

    internal bool DebugTryCountExactUtf8LiteralAnchored(ReadOnlySpan<byte> input, out int count)
    {
        return _compiledEngineRuntime.TryDebugCountExactUtf8LiteralAnchored(input, out count);
    }

    internal int DebugCountFallbackCandidates(ReadOnlySpan<byte> input)
    {
        return CountFallbackCandidates(input, requireScalarBoundary: false);
    }

    internal int DebugCountFallbackBoundaryCandidates(ReadOnlySpan<byte> input)
    {
        return CountFallbackCandidates(input, requireScalarBoundary: true);
    }

    internal int DebugCountFallbackViaSearchStarts(ReadOnlySpan<byte> input)
    {
        return CountFallbackViaSearchStarts(input);
    }

    internal int DebugCountFallbackDirect(ReadOnlySpan<byte> input)
    {
        return CountFallbackDirectWithRoute(input, "debug_fallback_direct");
    }

    internal Utf8ValueMatch DebugMatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        if (RejectsByRequiredPrefilter(input))
        {
            return Utf8ValueMatch.NoMatch;
        }

        return MatchViaCompiledEngine(input, validation, budget: null);
    }

    internal bool DebugTryMatchWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return TryMatchDirectWithoutValidation(input, out match);
    }

    internal bool DebugTryIsMatchWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        return TryIsMatchDirectWithoutValidation(input, out isMatch);
    }

    internal bool DebugTryIsMatchAsciiSimplePatternWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        return TryIsMatchAsciiSimplePatternWithoutValidation(input, out isMatch);
    }

    internal bool DebugTryIsMatchAnchoredHeadTailWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        return TryIsMatchAnchoredHeadTailWithoutValidation(input, out isMatch);
    }

    internal bool DebugTryMatchAsciiSimplePatternWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return TryMatchAsciiSimplePatternWithoutValidation(input, out match);
    }

    internal bool DebugTryMatchAnchoredHeadTailWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return TryMatchAnchoredHeadTailWithoutValidation(input, out match);
    }

    internal bool DebugCanUseFusedCompiledUtf8LiteralCount => CanUseFusedCompiledUtf8LiteralCount();

    internal bool DebugCanUseFusedCompiledUtf8LiteralFamilyCount => CanUseFusedCompiledUtf8LiteralFamilyCount();

    internal bool DebugCreatedExecutionBudgetIsNull => CreateExecutionBudget() is null;

    internal int DebugCountViaCompiledEngineWithCreatedBudget(ReadOnlySpan<byte> input)
    {
        return CountViaCompiledEngine(input, default, CreateExecutionBudget());
    }

    internal static int DebugProjectWholeMatchOnly(int matchedLength)
    {
        var match = new Utf8ValueMatch(true, true, 0, matchedLength, 0, matchedLength);
        return match.IndexInUtf16 + match.LengthInUtf16;
    }

    internal static int DebugProjectByteAlignedMatchOnly(int index, int matchedLength)
    {
        var match = new Utf8ValueMatch(true, true, index, matchedLength, index, matchedLength);
        return match.IndexInUtf16 + match.LengthInUtf16;
    }

    internal bool DebugTryMatchCompiledAsciiLiteralFamilyRaw(ReadOnlySpan<byte> input, out int index, out int matchedLength)
    {
        return _compiledEngineRuntime.TryDebugMatchAsciiLiteralFamilyRaw(input, out index, out matchedLength);
    }

    internal bool DebugTryMatchDirectAnchoredFixedLengthSimplePattern(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedLengthSimplePattern(_regexPlan, input, out matchedLength);
    }

    internal bool DebugTryMatchDirectAnchoredFixedAlternationSimplePattern(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedAlternationSimplePattern(_regexPlan, input, out matchedLength);
    }

    internal string DebugDirectAnchoredFixedAlternationSummary(ReadOnlySpan<byte> input)
    {
        return Utf8SimplePatternCompiledWholeMatcher.GetDirectAnchoredFixedAlternationDebugSummary(_regexPlan, input);
    }

    private bool TryMatchDirectWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        if (TryMatchAnchoredHeadTailWithoutValidation(input, out match))
        {
            return true;
        }

        if (TryMatchAsciiSimplePatternWithoutValidation(input, out match))
        {
            return true;
        }

        if (TryMatchDirectFallbackFamilyWithoutValidation(input, out match))
        {
            return true;
        }

        return _compiledEngineRuntime.TryMatchWithoutValidation(input, budget: null, out match);
    }

    private bool TryIsMatchDirectWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        if (TryIsMatchAnchoredHeadTailWithoutValidation(input, out isMatch))
        {
            return true;
        }

        if (TryIsMatchAsciiSimplePatternWithoutValidation(input, out isMatch))
        {
            return true;
        }

        if (TryIsMatchDirectFallbackFamilyWithoutValidation(input, out isMatch))
        {
            return true;
        }

        isMatch = false;
        return false;
    }

    private bool TryMatchAnchoredHeadTailWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        if (!_hasDirectAnchoredHeadTailWithoutValidation)
        {
            return false;
        }

        var directResult = Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeWithoutValidation(
            input,
            _anchoredHeadTailRunPlan,
            _allowsTrailingNewlineBeforeEnd,
            out var matchedLength);
        if (directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation)
        {
            return false;
        }

        if (directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match)
        {
            match = new Utf8ValueMatch(true, true, 0, matchedLength, 0, matchedLength);
        }

        return true;
    }

    private bool TryIsMatchAnchoredHeadTailWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        isMatch = false;
        if (!_hasDirectAnchoredHeadTailWithoutValidation)
        {
            return false;
        }

        var directResult = Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeWithoutValidation(
            input,
            _anchoredHeadTailRunPlan,
            _allowsTrailingNewlineBeforeEnd,
            out _);
        if (directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation)
        {
            return false;
        }

        isMatch = directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match;
        return true;
    }

    internal bool DebugSupportsWellFormedOnlyMatch => _compiledEngineRuntime.SupportsWellFormedOnlyMatch;

    internal bool DebugWellFormedOnlyMatchMissIsDefinitive => _compiledEngineRuntime.WellFormedOnlyMatchMissIsDefinitive;

    internal bool DebugSupportsThrowIfInvalidOnlyCount => _compiledEngineRuntime.SupportsThrowIfInvalidOnlyCount;

    internal bool DebugUsesEmittedAnchoredValidatorMatcher => _compiledEngineRuntime.UsesEmittedAnchoredValidatorMatcher;

    internal string DebugAnchoredValidatorSegmentSummary =>
        Utf8AsciiAnchoredValidatorExecutor.GetSegmentSummary(_anchoredValidatorPlan);

    internal bool DebugTryMatchAnchoredValidatorFixedPrefixOnly(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeFixedPrefixOnly(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out matchedLength);
    }

    internal bool DebugTryMatchAnchoredValidatorFirstBoundedSegmentOnly(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeFirstBoundedSegmentOnly(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out matchedLength);
    }

    internal bool DebugTryMatchAnchoredValidatorSuffixAfterFirstBounded(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeSuffixAfterFirstBounded(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out matchedLength);
    }

    internal bool DebugTryMatchAnchoredValidatorNativeWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWhole(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out matchedLength);
    }

    internal bool DebugTryMatchRepeatedDigitGroupWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        var plan = _regexPlan.SimplePatternPlan.RepeatedDigitGroupPlan;
        return plan.HasValue &&
            Utf8AsciiRepeatedDigitGroupExecutor.TryMatchWhole(
                input,
                plan,
                out matchedLength,
                out _);
    }

    internal bool DebugTryFindRepeatedDigitGroup(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        var plan = _regexPlan.SimplePatternPlan.RepeatedDigitGroupPlan;
        return plan.HasValue &&
            Utf8AsciiRepeatedDigitGroupExecutor.TryFind(
                input,
                plan,
                out _,
                out matchedLength);
    }

    internal bool DebugTryMatchCompiledAnchoredValidatorWithoutValidation(ReadOnlySpan<byte> input, out int matchedLength)
    {
        if (_compiledEngine.Kind != Utf8CompiledEngineKind.SimplePatternInterpreter ||
            _regexPlan.ExecutionKind != NativeExecutionKind.AsciiSimplePattern ||
            !_anchoredValidatorPlan.HasValue)
        {
            matchedLength = 0;
            return false;
        }

        var direct = Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidatorWithoutValidation(
            _regexPlan,
            _allowsTrailingNewlineBeforeEnd,
            input,
            out matchedLength);
        return direct == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match;
    }

    internal bool DebugTryFindDirectFallbackTokenWithoutValidation(ReadOnlySpan<byte> input, out int matchIndex, out int matchedLength)
    {
        matchIndex = -1;
        matchedLength = 0;
        if (!_hasDirectFallbackTokenFamilyWithoutValidation)
        {
            return false;
        }

        var directResult = Utf8AsciiTokenFamilyExecutor.TryFindTokenWithoutValidation(
            input,
            0,
            _fallbackDirectFamily,
            out matchIndex,
            out matchedLength);
        return directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match && matchedLength > 0;
    }

    internal bool DebugIsMatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        if (RejectsByRequiredPrefilter(input))
        {
            return false;
        }

        return IsMatchViaCompiledEngine(input, validation, budget: null);
    }

    internal bool DebugCanGuideFallbackVerification => CanGuideFallbackVerification();

    internal bool DebugIsMatchFallbackViaSearchStarts(ReadOnlySpan<byte> input)
    {
        if (RejectsByRequiredPrefilter(input) || !CanGuideFallbackVerification())
        {
            return false;
        }

        return IsMatchFallbackViaSearchStarts(input);
    }

    internal Utf8ValueMatch DebugMatchAfterValidation(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        if (ShouldFallbackForTrailingNewlineAnchoredValidator(input, validation))
        {
            return MatchFallback(input);
        }

        if (RejectsByRequiredPrefilter(input))
        {
            return Utf8ValueMatch.NoMatch;
        }

        return MatchViaCompiledEngine(input, validation, budget: null);
    }

    private static void ValidateOptions(RegexOptions options)
    {
        _ = new Regex(string.Empty, Utf8RegexSyntax.NormalizeNonSemanticOptions(options), Regex.InfiniteMatchTimeout);
    }

    private static void ValidateStartAt(int startat, int utf16Length)
    {
        if ((uint)startat > (uint)utf16Length)
        {
            throw new ArgumentOutOfRangeException(nameof(startat));
        }
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

    public int GroupNumberFromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GroupNumberFromName(name);
    }

    public string GroupNameFromNumber(int i)
    {
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GroupNameFromNumber(i);
    }

    public string[] GetGroupNames()
    {
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GetGroupNames();
    }

    public int[] GetGroupNumbers()
    {
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.GetGroupNumbers();
    }

    public bool IsMatch(ReadOnlySpan<byte> input)
    {
        if (TryIsMatchDirectWithoutValidation(input, out var directIsMatch))
        {
            return directIsMatch;
        }

        if (TryMatchDirectWithoutValidation(input, out var directMatch))
        {
            return directMatch.Success;
        }

        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            if (twin.TryMatchDirectWithoutValidation(input, out var twinDirectMatch))
            {
                return twinDirectMatch.Success;
            }

            return twin.IsMatch(input);
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

    public bool IsMatch(ReadOnlySpan<byte> input, int startat)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        ValidateStartAt(startat, analysis.Validation.Utf16Length);
        var decoded = Encoding.UTF8.GetString(input);
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(decoded.AsSpan(), startat);
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.Count(input);
        }

        if (CanUseFusedCompiledAsciiLiteralFamilyCount())
        {
            if (!TryUseAsciiInputValidationShortcut(input))
            {
                Utf8Validation.ThrowIfInvalidOnly(input);
            }

            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("compiled_fused_ascii_literal_family_count");
            var fastBudget = CreateExecutionBudget();
            return CountViaCompiledEngine(input, default, fastBudget);
        }

        if (CanUseFusedCompiledUtf8LiteralCount() || CanUseFusedCompiledUtf8LiteralFamilyCount())
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(
                CanUseFusedCompiledUtf8LiteralCount()
                    ? "compiled_fused_utf8_literal_count"
                    : "compiled_fused_utf8_literal_family_count");
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
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("required_prefilter_reject");
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
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("required_prefilter_reject");
                return 0;
            }

            var fastBudget = CreateExecutionBudget();
            if (fastBudget is null &&
                _directStructuralFamilyKernelMatcher is not null)
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("native_structural_family_emit_shared_prefix_suffix");
                return _directStructuralFamilyKernelMatcher.Count(input);
            }

            return CountViaCompiledEngine(input, default, fastBudget);
        }

        var validation = Utf8Validation.Validate(input);
        if (ShouldFallbackForTrailingNewlineAnchoredValidator(input, validation))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
        }

        if (RejectsByRequiredPrefilter(input))
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("required_prefilter_reject");
            return 0;
        }

        var budget = CreateExecutionBudget();

        return CountViaCompiledEngine(input, validation, budget);
    }

    public int Count(ReadOnlySpan<byte> input, int startat)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        ValidateStartAt(startat, analysis.Validation.Utf16Length);
        var decoded = Encoding.UTF8.GetString(input);
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(decoded.AsSpan(), startat);
    }

    public Utf8ValueMatch Match(ReadOnlySpan<byte> input)
    {
        if (TryMatchDirectWithoutValidation(input, out var directMatch))
        {
            return directMatch;
        }

        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            if (twin.TryMatchDirectWithoutValidation(input, out var twinDirectMatch))
            {
                return twinDirectMatch;
            }

            return twin.Match(input);
        }

        var canUseAsciiWellFormedOnlyMatch =
            _compiledEngineRuntime.SupportsAsciiWellFormedOnlyMatch &&
            TryUseAsciiInputValidationShortcut(input);

        if (canUseAsciiWellFormedOnlyMatch || _compiledEngineRuntime.SupportsWellFormedOnlyMatch)
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

            if (_compiledEngineRuntime.WellFormedOnlyMatchMissIsDefinitive)
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

    public Utf8ValueMatch Match(ReadOnlySpan<byte> input, int startat)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        ValidateStartAt(startat, analysis.Validation.Utf16Length);
        var decoded = Encoding.UTF8.GetString(input);
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded, startat);
        return CreateValueMatch(input, analysis.BoundaryMap, match);
    }

    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.MatchDetailed(input);
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        var decoded = Encoding.UTF8.GetString(input);
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        return new Utf8MatchContext(input, decoded, match, analysis.BoundaryMap, _groupNames);
    }

    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input, int startat)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        ValidateStartAt(startat, analysis.Validation.Utf16Length);
        var decoded = Encoding.UTF8.GetString(input);
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded, startat);
        return new Utf8MatchContext(input, decoded, match, analysis.BoundaryMap, _groupNames);
    }

    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.EnumerateMatches(input);
        }

        var literal = _regexPlan.LiteralUtf8;
        var budget = CreateExecutionBudget();
        if (UsesRightToLeft())
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, analysis.BoundaryMap);
        }

        if (_regexPlan.ExecutionKind is NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals)
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return CreateMatchEnumeratorViaCompiledEngine(input, default, literal, budget);
        }

        var validation = Utf8Validation.Validate(input);
        return CreateMatchEnumeratorViaCompiledEngine(input, validation, literal, budget);
    }

    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input, int startat)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        ValidateStartAt(startat, analysis.Validation.Utf16Length);
        var decoded = Encoding.UTF8.GetString(input);
        return new Utf8ValueMatchEnumerator(
            input,
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
            decoded,
            startat,
            analysis.BoundaryMap);
    }

    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count = int.MaxValue)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.EnumerateSplits(input, count);
        }

        if (UsesRightToLeft())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count);
        }

        if (ShouldPreferFallbackForCompiledLiteralFamilyTextOperations())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count);
        }

        var budget = CreateExecutionBudget();
        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        if (count > 0 && CanUseNativeSplit(validation))
        {
            return CreateSplitEnumeratorViaCompiledEngine(input, validation, count, budget);
        }

        return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count);
    }

    public byte[] Replace(ReadOnlySpan<byte> input, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.Replace(input, replacement);
        }

        return ReplaceCore(input, replacement, GetParsedReplacement(replacement));
    }

    public byte[] Replace(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8)
    {
        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        if (ShouldPreferFallbackForCompiledLiteralFamilyTextOperations())
        {
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                Encoding.UTF8.GetString(replacementPatternUtf8)));
        }

        if (TryGetDirectLiteralReplacementBytes(replacementPatternUtf8, out var directReplacementBytes))
        {
            return ReplaceLiteralBytesCore(input, validation, replacementText: null, directReplacementBytes);
        }

        var literal = _regexPlan.LiteralUtf8;
        var replacementText = Encoding.UTF8.GetString(replacementPatternUtf8);
        var replacementPattern = GetParsedReplacement(replacementText);
        var budget = CreateExecutionBudget();
        if (ShouldUseFallbackForSimplePatternReplacement(replacementPattern))
        {
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText));
        }

        if (TryGetExactLiteralReplacementBytes(replacementPattern, out var exactLiteralReplacementBytes))
        {
            return _compiledEngine.Kind switch
            {
                Utf8CompiledEngineKind.ExactLiteral when literal is { Length: > 0 }
                    => ReplaceViaCompiledExactLiteralEngine(input, exactLiteralReplacementBytes, literal, budget),
                Utf8CompiledEngineKind.LiteralFamily
                    => ReplaceViaCompiledLiteralFamilyEngine(input, exactLiteralReplacementBytes, budget),
                _ => throw new InvalidOperationException("Exact literal replacement bytes are only valid for compiled literal engines."),
            };
        }

        if (TryReplaceViaNativePlan(input, validation, replacementPattern, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacementPattern, out var replacementBytes))
        {
            return Encoding.UTF8.GetBytes(ReplaceFallbackWithSharedPlan(input, replacementPattern));
        }

        return ReplaceLiteralBytesCore(input, validation, replacementText, replacementBytes);
    }

    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        _ = Utf8Validation.Validate(input);
        var inputBytes = input.ToArray();
        var decoded = Encoding.UTF8.GetString(input);
        var boundaryMap = Utf8InputAnalyzer.Analyze(inputBytes).BoundaryMap;
        var replaced = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
            decoded,
            match =>
            {
                var context = new Utf8MatchContext(inputBytes, decoded, match, boundaryMap, _groupNames);
                var writer = new Utf8ReplacementWriter();
                evaluator(in context, ref writer, ref state);
                return writer.ToValidatedString();
            });

        return Encoding.UTF8.GetBytes(replaced);
    }

    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.ReplaceToString(input, replacement);
        }

        _ = Utf8Validation.Validate(input);
        return ReplaceToStringCore(input, GetParsedReplacement(replacement));
    }

    public string ReplaceToString<TState>(ReadOnlySpan<byte> input, TState state, Utf16MatchEvaluator<TState> evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        _ = Utf8Validation.Validate(input);
        var inputBytes = input.ToArray();
        var decoded = Encoding.UTF8.GetString(input);
        var boundaryMap = Utf8InputAnalyzer.Analyze(inputBytes).BoundaryMap;
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
            decoded,
            match =>
            {
                var context = new Utf8MatchContext(inputBytes, decoded, match, boundaryMap, _groupNames);
                return evaluator(in context, ref state);
            });
    }

    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        string replacement,
        Span<byte> destination,
        out int bytesWritten)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.TryReplace(input, replacement, destination, out bytesWritten);
        }

        return TryReplaceCore(input, GetParsedReplacement(replacement), replacement, destination, out bytesWritten);
    }

    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten)
    {
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        var validation = Utf8Validation.Validate(input);
        if (TryGetDirectLiteralReplacementBytes(replacementPatternUtf8, out var directReplacementBytes))
        {
            return TryReplaceLiteralBytesCore(input, validation, replacementText: null, directReplacementBytes, destination, out bytesWritten);
        }

        var replacementText = Encoding.UTF8.GetString(replacementPatternUtf8);
        var replacementPattern = GetParsedReplacement(replacementText);
        return TryReplaceCore(input, replacementPattern, replacementText, destination, out bytesWritten);
    }

    public static bool IsMatch(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options = RegexOptions.CultureInvariant)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).IsMatch(input);
    }

    public static bool IsMatch(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).IsMatch(input);
    }

    public static int Count(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options = RegexOptions.CultureInvariant)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).Count(input);
    }

    public static int Count(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).Count(input);
    }

    public static Utf8ValueMatch Match(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options = RegexOptions.CultureInvariant)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).Match(input);
    }

    public static Utf8ValueMatch Match(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).Match(input);
    }

    public static Utf8MatchContext MatchDetailed(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options = RegexOptions.CultureInvariant)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).MatchDetailed(input);
    }

    public static Utf8MatchContext MatchDetailed(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).MatchDetailed(input);
    }

    internal Utf8IsMatchDiagnostics CollectIsMatchDiagnostics(ReadOnlySpan<byte> input)
    {
        var session = Utf8SearchDiagnosticsSession.Start();
        try
        {
            var result = IsMatch(input);
            return new Utf8IsMatchDiagnostics(
                result,
                _regexPlan.ExecutionKind.ToString(),
                _regexPlan.SearchPlan.Kind.ToString(),
                _regexPlan.FallbackVerifier.Mode.ToString(),
                _regexPlan.FallbackVerifier.RequiresCandidateEndCoverage,
                _regexPlan.FallbackVerifier.RequiresTrailingAnchorCoverage,
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
                _regexPlan.ExecutionKind.ToString(),
                _regexPlan.SearchPlan.Kind.ToString(),
                _regexPlan.FallbackVerifier.Mode.ToString(),
                _regexPlan.FallbackVerifier.RequiresCandidateEndCoverage,
                _regexPlan.FallbackVerifier.RequiresTrailingAnchorCoverage,
                session.ExecutionRoute ?? "<none>",
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

    internal bool DebugCanUseNativeSplit(ReadOnlySpan<byte> input)
    {
        var validation = Utf8Validation.Validate(input);
        return CanUseNativeSplit(validation);
    }

    internal int DebugCountSplitsViaCompiledEngine(ReadOnlySpan<byte> input, int count = int.MaxValue)
    {
        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        var enumerator = CreateSplitEnumeratorViaCompiledEngine(input, validation, count, budget);
        var splitCount = 0;
        foreach (var _ in enumerator)
        {
            splitCount++;
        }

        return splitCount;
    }

    internal int DebugCountSplitsViaFallback(ReadOnlySpan<byte> input, int count = int.MaxValue)
    {
        var decoded = Encoding.UTF8.GetString(input);
        var boundaryMap = Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        var enumerator = new Utf8ValueSplitEnumerator(input, decoded, _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, boundaryMap);
        var splitCount = 0;
        foreach (var _ in enumerator)
        {
            splitCount++;
        }

        return splitCount;
    }

    internal bool DebugShouldPreferFallbackForCompiledLiteralFamilyTextOperations()
    {
        return ShouldPreferFallbackForCompiledLiteralFamilyTextOperations();
    }

    internal int DebugReplaceViaFallback(ReadOnlySpan<byte> input, string replacement)
    {
        return Encoding.UTF8.GetByteCount(
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacement));
    }

    internal int DebugReplaceViaNativeTextOperations(ReadOnlySpan<byte> input, string replacementText)
    {
        var validation = Utf8Validation.Validate(input);
        var replacement = GetParsedReplacement(replacementText);
        var budget = CreateExecutionBudget();

        if (TryReplaceViaNativePlan(input, validation, replacement, budget, out var nativeResult))
        {
            return nativeResult.Length;
        }

        if (TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return ReplaceLiteralBytesCore(input, validation, replacementText, replacementBytes).Length;
        }

        if (TryGetExactLiteralReplacementBytes(replacement, out var exactLiteralReplacementBytes))
        {
            return ReplaceLiteralBytesCore(input, validation, replacementText, exactLiteralReplacementBytes).Length;
        }

        return -1;
    }

    public static Utf8ValueMatchEnumerator EnumerateMatches(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options = RegexOptions.CultureInvariant)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).EnumerateMatches(input);
    }

    public static Utf8ValueMatchEnumerator EnumerateMatches(
        ReadOnlySpan<byte> input,
        string pattern,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).EnumerateMatches(input);
    }

    public static Utf8ValueSplitEnumerator EnumerateSplits(
        ReadOnlySpan<byte> input,
        string pattern,
        int count = int.MaxValue,
        RegexOptions options = RegexOptions.CultureInvariant)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).EnumerateSplits(input, count);
    }

    public static Utf8ValueSplitEnumerator EnumerateSplits(
        ReadOnlySpan<byte> input,
        string pattern,
        int count,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).EnumerateSplits(input, count);
    }

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        string pattern,
        string replacement,
        RegexOptions options = RegexOptions.CultureInvariant)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options).Replace(input, replacement);
    }

    public static byte[] Replace(
        ReadOnlySpan<byte> input,
        string pattern,
        string replacement,
        RegexOptions options,
        TimeSpan matchTimeout)
    {
        return Utf8RegexCache.GetOrAdd(pattern, options, matchTimeout).Replace(input, replacement);
    }

    private byte[] ReplaceCore(
        ReadOnlySpan<byte> input,
        string replacementText,
        Utf8AnalyzedReplacement replacement)
    {
        if (ShouldPreferFallbackForCompiledLiteralFamilyTextOperations())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText));
        }

        if (ShouldUseFallbackForSimplePatternReplacement(replacement))
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText));
        }

        if (UsesRightToLeft())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText));
        }

        var validation = Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        var literal = _regexPlan.LiteralUtf8;
        if (TryReplaceViaNativePlan(input, validation, replacement, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return Encoding.UTF8.GetBytes(ReplaceFallbackWithSharedPlan(input, replacement));
        }

        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral when literal is { Length: > 0 }
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    literal.Length,
                    budget),
            NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    literal.Length,
                    budget),
            NativeExecutionKind.ExactUtf8Literals
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                    {
                        matchIndex = FindNextUtf8LiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                        return matchIndex >= 0;
                    },
                    budget),
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                    {
                        matchIndex = FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                        return matchIndex >= 0;
                    },
                    budget),
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    literal.Length,
                    budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii && _regexPlan.SimplePatternPlan.IsFixedLength
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    bytes => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, 0, captures: null, budget, out _),
                    (bytes, start) => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out _),
                    _regexPlan.SimplePatternPlan.MinLength,
                    budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                    {
                        matchIndex = Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out matchLength);
                        return matchIndex >= 0;
                    },
                    budget),
            _ => Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText)),
        };
    }

    private Utf8AnalyzedReplacement GetParsedReplacement(string replacement)
    {
        return _replacementCache.GetOrAdd(
            replacement,
            replacementText => Utf8FrontEndReplacementAnalyzer.Analyze(replacementText, _groupNumbers, _groupNames));
    }

    private static bool TryGetNativeReplacementBytes(
        Utf8AnalyzedReplacement replacement,
        out byte[] replacementBytes)
    {
        if (replacement.LiteralUtf8 is { } literalUtf8)
        {
            replacementBytes = literalUtf8;
            return true;
        }

        replacementBytes = [];
        return false;
    }

    private static bool TryGetDirectLiteralReplacementBytes(
        ReadOnlySpan<byte> replacementUtf8,
        out byte[] replacementBytes)
    {
        if (replacementUtf8.IndexOf((byte)'$') >= 0)
        {
            replacementBytes = [];
            return false;
        }

        replacementBytes = replacementUtf8.ToArray();
        return true;
    }

    private bool TryGetExactLiteralReplacementBytes(
        Utf8AnalyzedReplacement replacement,
        out byte[] replacementBytes)
    {
        if (_regexPlan.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase) ||
            _regexPlan.LiteralUtf8 is not { Length: > 0 } literalUtf8)
        {
            replacementBytes = [];
            return false;
        }

        if (replacement.LiteralUtf8 is { } literalReplacement)
        {
            replacementBytes = literalReplacement;
            return true;
        }

        var plan = RewriteWholeLiteralCapturePlan(replacement.Plan);
        var totalLength = 0;
        foreach (var instruction in plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    totalLength += instruction.LiteralUtf8?.Length ?? 0;
                    break;

                case Utf8ReplacementInstructionKind.WholeMatch:
                    totalLength += literalUtf8.Length;
                    break;

                default:
                    replacementBytes = [];
                    return false;
            }
        }

        replacementBytes = new byte[totalLength];
        var written = 0;
        foreach (var instruction in plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal when instruction.LiteralUtf8 is { Length: > 0 } bytes:
                    bytes.CopyTo(replacementBytes.AsSpan(written));
                    written += bytes.Length;
                    break;

                case Utf8ReplacementInstructionKind.WholeMatch:
                    literalUtf8.CopyTo(replacementBytes.AsSpan(written));
                    written += literalUtf8.Length;
                    break;
            }
        }

        return true;
    }

    private string ReplaceToStringCore(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement)
    {
        if (ShouldPreferFallbackForCompiledLiteralFamilyTextOperations())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacement.OriginalText);
        }

        if (ShouldUseFallbackForSimplePatternReplacement(replacement))
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacement.OriginalText);
        }

        if (UsesRightToLeft())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return ReplaceFallbackWithSharedPlan(input, replacement);
        }

        var validation = Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        if (TryReplaceViaNativePlan(input, validation, replacement, budget, out var nativeResult))
        {
            return Encoding.UTF8.GetString(nativeResult);
        }

        if (TryGetExactLiteralReplacementBytes(replacement, out var exactLiteralReplacementBytes))
        {
            return Encoding.UTF8.GetString(Replace(input, exactLiteralReplacementBytes));
        }

        if (TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return Encoding.UTF8.GetString(Replace(input, replacementBytes));
        }

        return ReplaceFallbackWithSharedPlan(input, replacement);
    }

    private OperationStatus TryReplaceCore(
        ReadOnlySpan<byte> input,
        Utf8AnalyzedReplacement replacement,
        string replacementText,
        Span<byte> destination,
        out int bytesWritten)
    {
        if (ShouldPreferFallbackForCompiledLiteralFamilyTextOperations())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return TryEncodeUtf8ToDestination(
                _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                    Encoding.UTF8.GetString(input),
                    replacementText),
                destination,
                out bytesWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        if (ShouldUseFallbackForSimplePatternReplacement(replacement))
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            var replaced = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText);
            return TryEncodeUtf8ToDestination(replaced, destination, out bytesWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        if (UsesRightToLeft())
        {
            Utf8Validation.ThrowIfInvalidOnly(input);
            return TryEncodeUtf8ToDestination(ReplaceFallbackWithSharedPlan(input, replacement), destination, out bytesWritten)
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        var validation = Utf8Validation.Validate(input);
        var budget = CreateExecutionBudget();
        if (TryReplaceViaNativePlan(input, validation, replacement, budget, destination, out bytesWritten))
        {
            return OperationStatus.Done;
        }

        if (TryGetExactLiteralReplacementBytes(replacement, out var exactLiteralReplacementBytes))
        {
            var literal = _regexPlan.LiteralUtf8;
            var exactLiteralSuccess = _regexPlan.ExecutionKind switch
            {
                NativeExecutionKind.ExactAsciiLiteral when literal is { Length: > 0 }
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        exactLiteralReplacementBytes,
                        bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                        (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                        literal.Length,
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        exactLiteralReplacementBytes,
                        bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                        (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                        literal.Length,
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.ExactUtf8Literals
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        exactLiteralReplacementBytes,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextUtf8LiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        exactLiteralReplacementBytes,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        exactLiteralReplacementBytes,
                        bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                        (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                        literal.Length,
                        destination,
                        out bytesWritten,
                        budget),
                _ => false,
            };

            return exactLiteralSuccess ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
        }

        if (TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            var literal = _regexPlan.LiteralUtf8;
            var success = _regexPlan.ExecutionKind switch
            {
                NativeExecutionKind.ExactAsciiLiteral when literal is { Length: > 0 }
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        replacementBytes,
                        bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                        (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                        literal.Length,
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        replacementBytes,
                        bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                        (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                        literal.Length,
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.ExactUtf8Literals
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        replacementBytes,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextUtf8LiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        replacementBytes,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        replacementBytes,
                        bytes => Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, bytes),
                        (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                        literal.Length,
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.AsciiSimplePattern when validation.IsAscii && _regexPlan.SimplePatternPlan.IsFixedLength
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        replacementBytes,
                        bytes => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, 0, captures: null, budget, out _),
                        (bytes, start) => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out _),
                        _regexPlan.SimplePatternPlan.MinLength,
                        destination,
                        out bytesWritten,
                        budget),
                NativeExecutionKind.AsciiSimplePattern when validation.IsAscii
                    => Utf8LiteralReplaceEngine.TryReplace(
                        input,
                        replacementBytes,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget),
                _ => TryEncodeUtf8ToDestination(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(Encoding.UTF8.GetString(input), replacementText), destination, out bytesWritten),
            };

            return success ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
        }

        return TryEncodeUtf8ToDestination(ReplaceFallbackWithSharedPlan(input, replacement), destination, out bytesWritten)
            ? OperationStatus.Done
            : OperationStatus.DestinationTooSmall;
    }

    private byte[] ReplaceLiteralBytesCore(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        string? replacementText,
        byte[] replacementBytes)
    {
        if (_compiledEngine.Kind == Utf8CompiledEngineKind.StructuralLinearAutomaton)
        {
            return _compiledEngineRuntime.ReplaceLiteralBytes(input, validation, replacementBytes, CreateExecutionBudget());
        }

        var literal = _regexPlan.LiteralUtf8;
        var budget = CreateExecutionBudget();

        return _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral when literal is { Length: > 0 }
                => Utf8FixedMatchReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    literal.Length,
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    budget),
            NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }
                => Utf8FixedMatchReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    literal.Length,
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    budget),
            NativeExecutionKind.ExactUtf8Literals
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                    {
                        matchIndex = FindNextUtf8LiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                        return matchIndex >= 0;
                    },
                    budget),
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => Utf8FixedMatchReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    literal.Length,
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii &&
                _regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern
                => Utf8FixedMatchReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    _regexPlan.StructuralLinearProgram,
                    budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii && _regexPlan.SimplePatternPlan.IsFixedLength
                => Utf8FixedMatchReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    _regexPlan.SimplePatternPlan.MinLength,
                    (bytes, start) => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out _),
                    budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii
                => Utf8LiteralReplaceEngine.Replace(
                    input,
                    replacementBytes,
                    (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                    {
                        matchIndex = Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out matchLength);
                        return matchIndex >= 0;
                    },
                    budget),
            _ => Encoding.UTF8.GetBytes(_verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacementText ?? Encoding.UTF8.GetString(replacementBytes))),
        };
    }

    private OperationStatus TryReplaceLiteralBytesCore(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        string? replacementText,
        byte[] replacementBytes,
        Span<byte> destination,
        out int bytesWritten)
    {
        if (_compiledEngine.Kind == Utf8CompiledEngineKind.StructuralLinearAutomaton)
        {
            return _compiledEngineRuntime.TryReplaceLiteralBytes(input, validation, replacementBytes, destination, out bytesWritten, CreateExecutionBudget())
                ? OperationStatus.Done
                : OperationStatus.DestinationTooSmall;
        }

        var literal = _regexPlan.LiteralUtf8;
        var budget = CreateExecutionBudget();

        var success = _regexPlan.ExecutionKind switch
        {
            NativeExecutionKind.ExactAsciiLiteral when literal is { Length: > 0 }
                => Utf8FixedMatchReplaceEngine.TryReplace(
                    input,
                    replacementBytes,
                    literal.Length,
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    destination,
                    out bytesWritten,
                    budget),
            NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }
                => Utf8FixedMatchReplaceEngine.TryReplace(
                    input,
                    replacementBytes,
                    literal.Length,
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    destination,
                    out bytesWritten,
                    budget),
            NativeExecutionKind.ExactUtf8Literals
                => Utf8LiteralReplaceEngine.TryReplace(
                    input,
                    replacementBytes,
                    (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                    {
                        matchIndex = FindNextUtf8LiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                        return matchIndex >= 0;
                    },
                    destination,
                    out bytesWritten,
                    budget),
            NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }
                => Utf8FixedMatchReplaceEngine.TryReplace(
                    input,
                    replacementBytes,
                    literal.Length,
                    (bytes, start) => Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, bytes, start),
                    destination,
                    out bytesWritten,
                    budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii &&
                _regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern
                => Utf8FixedMatchReplaceEngine.TryReplace(
                    input,
                    replacementBytes,
                    _regexPlan.StructuralLinearProgram,
                    destination,
                    out bytesWritten,
                    budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii && _regexPlan.SimplePatternPlan.IsFixedLength
                => Utf8FixedMatchReplaceEngine.TryReplace(
                    input,
                    replacementBytes,
                    _regexPlan.SimplePatternPlan.MinLength,
                    (bytes, start) => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out _),
                    destination,
                    out bytesWritten,
                    budget),
            NativeExecutionKind.AsciiSimplePattern when validation.IsAscii
                => Utf8LiteralReplaceEngine.TryReplace(
                    input,
                    replacementBytes,
                    (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                    {
                        matchIndex = Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out matchLength);
                        return matchIndex >= 0;
                    },
                    destination,
                    out bytesWritten,
                    budget),
            _ => TryEncodeUtf8ToDestination(
                _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(Encoding.UTF8.GetString(input), replacementText ?? Encoding.UTF8.GetString(replacementBytes)),
                destination,
                out bytesWritten),
        };

        return success ? OperationStatus.Done : OperationStatus.DestinationTooSmall;
    }

    private string ReplaceFallbackWithSharedPlan(ReadOnlySpan<byte> input, Utf8AnalyzedReplacement replacement)
    {
        var decoded = Encoding.UTF8.GetString(input);
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
            decoded,
            match => Utf8ReplacementPlanInterpreter.Apply(replacement.Plan, match, decoded));
    }

    private bool TryReplaceViaNativePlan(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8AnalyzedReplacement replacement,
        Utf8ExecutionBudget? budget,
        out byte[] replaced)
    {
        if (replacement.IsLiteral || !Utf8NativeReplacementExecutor.CanExecute(replacement.Plan))
        {
            replaced = [];
            return false;
        }

        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            replacement.ContainsGroupReferences)
        {
            replaced = [];
            return false;
        }

        var literal = _regexPlan.LiteralUtf8;
        switch (_regexPlan.ExecutionKind)
        {
            case NativeExecutionKind.ExactAsciiLiteral when literal is { Length: > 0 }:
                var exactAsciiReplacementPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(exactAsciiReplacementPlan))
                {
                    replaced = Utf8WholeMatchTemplateReplaceEngine.Replace(
                        input,
                        exactAsciiReplacementPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextLiteralViaInterpreter(bytes, start, ignoreCase: false, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        budget);
                    return true;
                }

                replaced = Utf8NativeReplacementExecutor.Replace(
                    input,
                    exactAsciiReplacementPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        var matchIndex = FindNextLiteralViaInterpreter(bytes, start, ignoreCase: false, budget, out var matchLength);
                        match = new Utf8NativeReplacementMatch(matchIndex, matchLength);
                        return matchIndex >= 0;
                    });
                return true;

            case NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }:
                var exactUtf8ReplacementPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(exactUtf8ReplacementPlan))
                {
                    replaced = Utf8WholeMatchTemplateReplaceEngine.Replace(
                        input,
                        exactUtf8ReplacementPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextExactLiteralViaSearch(bytes, start, literal.Length, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        budget);
                    return true;
                }

                replaced = Utf8NativeReplacementExecutor.Replace(
                    input,
                    exactUtf8ReplacementPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        var matchIndex = FindNextExactLiteralViaSearch(bytes, start, literal.Length, budget, out var matchLength);
                        match = new Utf8NativeReplacementMatch(matchIndex, matchLength);
                        return matchIndex >= 0;
                    });
                return true;

            case NativeExecutionKind.ExactUtf8Literals:
                var exactUtf8AlternationPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(exactUtf8AlternationPlan))
                {
                    replaced = Utf8WholeMatchTemplateReplaceEngine.Replace(
                        input,
                        exactUtf8AlternationPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextUtf8LiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        budget);
                    return true;
                }

                if (_regexPlan.SearchPlan.PreparedSearcher.HasValue &&
                    !_regexPlan.SearchPlan.HasBoundaryRequirements &&
                    !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
                {
                    replaced = Utf8NativeReplacementExecutor.Replace(
                        input,
                        exactUtf8AlternationPlan,
                        _regexPlan.SearchPlan.PreparedSearcher);
                    return true;
                }

                replaced = Utf8NativeReplacementExecutor.Replace(
                    input,
                    exactUtf8AlternationPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        if (Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, bytes, start, out var preparedMatch))
                        {
                            match = new Utf8NativeReplacementMatch(preparedMatch.Index, preparedMatch.Length, LiteralId: preparedMatch.LiteralId);
                            return true;
                        }

                        match = default;
                        return false;
                    });
                return true;

            case NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals:
                var ignoreCaseAlternationPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(ignoreCaseAlternationPlan))
                {
                    replaced = Utf8WholeMatchTemplateReplaceEngine.Replace(
                        input,
                        ignoreCaseAlternationPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        budget);
                    return true;
                }

                if (_regexPlan.SearchPlan.PreparedSearcher.HasValue &&
                    !_regexPlan.SearchPlan.HasBoundaryRequirements &&
                    !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
                {
                    replaced = Utf8NativeReplacementExecutor.Replace(
                        input,
                        ignoreCaseAlternationPlan,
                        _regexPlan.SearchPlan.PreparedSearcher);
                    return true;
                }

                replaced = Utf8NativeReplacementExecutor.Replace(
                    input,
                    ignoreCaseAlternationPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        if (Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, bytes, start, out var preparedMatch))
                        {
                            match = new Utf8NativeReplacementMatch(preparedMatch.Index, preparedMatch.Length, LiteralId: preparedMatch.LiteralId);
                            return true;
                        }

                        match = default;
                        return false;
                    });
                return true;

            case NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }:
                var ignoreCaseLiteralReplacementPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(ignoreCaseLiteralReplacementPlan))
                {
                    replaced = Utf8WholeMatchTemplateReplaceEngine.Replace(
                        input,
                        ignoreCaseLiteralReplacementPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextLiteralViaInterpreter(bytes, start, ignoreCase: true, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        budget);
                    return true;
                }

                replaced = Utf8NativeReplacementExecutor.Replace(
                    input,
                    ignoreCaseLiteralReplacementPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        var matchIndex = FindNextLiteralViaInterpreter(bytes, start, ignoreCase: true, budget, out var matchLength);
                        match = new Utf8NativeReplacementMatch(matchIndex, matchLength);
                        return matchIndex >= 0;
                    });
                return true;

            case NativeExecutionKind.AsciiSimplePattern when validation.IsAscii:
                if (TryGetDeterministicSimplePatternReplacementTemplate(replacement, out var template))
                {
                    if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
                    {
                        replaced = Utf8FixedTemplateReplaceEngine.Replace(
                            input,
                            template,
                            _regexPlan.StructuralLinearProgram,
                            budget);
                        return true;
                    }

                    replaced = Utf8FixedTemplateReplaceEngine.Replace(
                        input,
                        template,
                        bytes => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, 0, captures: null, budget, out _),
                        (bytes, start) => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out _),
                        budget);
                    return true;
                }

                var captures = replacement.ContainsGroupReferences ? new Utf8CaptureSlots(GetNativeCaptureSlotCount()) : null;
                replaced = Utf8NativeReplacementExecutor.Replace(
                    input,
                    replacement.Plan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        var matchIndex = Utf8ExecutionInterpreter.FindNextSimplePattern(
                            bytes,
                            _regexPlan.ExecutionProgram,
                            _regexPlan.SearchPlan,
                            _regexPlan.SimplePatternPlan,
                            start,
                            captures,
                            budget,
                            out var matchLength);
                        match = new Utf8NativeReplacementMatch(matchIndex, matchLength, captures);
                        return matchIndex >= 0;
                    });
                return true;

            default:
                replaced = [];
                return false;
        }
    }

    private bool TryReplaceViaNativePlan(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8AnalyzedReplacement replacement,
        Utf8ExecutionBudget? budget,
        Span<byte> destination,
        out int bytesWritten)
    {
        if (replacement.IsLiteral || !Utf8NativeReplacementExecutor.CanExecute(replacement.Plan))
        {
            bytesWritten = 0;
            return false;
        }

        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            replacement.ContainsGroupReferences)
        {
            bytesWritten = 0;
            return false;
        }

        var literal = _regexPlan.LiteralUtf8;
        switch (_regexPlan.ExecutionKind)
        {
            case NativeExecutionKind.ExactAsciiLiteral when literal is { Length: > 0 }:
                var exactAsciiReplacementPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(exactAsciiReplacementPlan))
                {
                    return Utf8WholeMatchTemplateReplaceEngine.TryReplace(
                        input,
                        exactAsciiReplacementPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextLiteralViaInterpreter(bytes, start, ignoreCase: false, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget);
                }

                return Utf8NativeReplacementExecutor.TryReplace(
                    input,
                    exactAsciiReplacementPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        var matchIndex = FindNextLiteralViaInterpreter(bytes, start, ignoreCase: false, budget, out var matchLength);
                        match = new Utf8NativeReplacementMatch(matchIndex, matchLength);
                        return matchIndex >= 0;
                    },
                    destination,
                    out bytesWritten);

            case NativeExecutionKind.ExactUtf8Literal when literal is { Length: > 0 }:
                var exactUtf8ReplacementPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(exactUtf8ReplacementPlan))
                {
                    return Utf8WholeMatchTemplateReplaceEngine.TryReplace(
                        input,
                        exactUtf8ReplacementPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextExactLiteralViaSearch(bytes, start, literal.Length, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget);
                }

                return Utf8NativeReplacementExecutor.TryReplace(
                    input,
                    exactUtf8ReplacementPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        var matchIndex = FindNextExactLiteralViaSearch(bytes, start, literal.Length, budget, out var matchLength);
                        match = new Utf8NativeReplacementMatch(matchIndex, matchLength);
                        return matchIndex >= 0;
                    },
                    destination,
                    out bytesWritten);

            case NativeExecutionKind.ExactUtf8Literals:
                var exactUtf8LiteralAlternationPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(exactUtf8LiteralAlternationPlan))
                {
                    return Utf8WholeMatchTemplateReplaceEngine.TryReplace(
                        input,
                        exactUtf8LiteralAlternationPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextUtf8LiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget);
                }

                if (_regexPlan.SearchPlan.PreparedSearcher.HasValue &&
                    !_regexPlan.SearchPlan.HasBoundaryRequirements &&
                    !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
                {
                    return Utf8NativeReplacementExecutor.TryReplace(
                        input,
                        exactUtf8LiteralAlternationPlan,
                        _regexPlan.SearchPlan.PreparedSearcher,
                        destination,
                        out bytesWritten);
                }

                return Utf8NativeReplacementExecutor.TryReplace(
                    input,
                    exactUtf8LiteralAlternationPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        if (Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, bytes, start, out var preparedMatch))
                        {
                            match = new Utf8NativeReplacementMatch(preparedMatch.Index, preparedMatch.Length, LiteralId: preparedMatch.LiteralId);
                            return true;
                        }

                        match = default;
                        return false;
                    },
                    destination,
                    out bytesWritten);

            case NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals:
                var ignoreCaseLiteralAlternationPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(ignoreCaseLiteralAlternationPlan))
                {
                    return Utf8WholeMatchTemplateReplaceEngine.TryReplace(
                        input,
                        ignoreCaseLiteralAlternationPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(bytes, start, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget);
                }

                if (_regexPlan.SearchPlan.PreparedSearcher.HasValue &&
                    !_regexPlan.SearchPlan.HasBoundaryRequirements &&
                    !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
                {
                    return Utf8NativeReplacementExecutor.TryReplace(
                        input,
                        ignoreCaseLiteralAlternationPlan,
                        _regexPlan.SearchPlan.PreparedSearcher,
                        destination,
                        out bytesWritten);
                }

                return Utf8NativeReplacementExecutor.TryReplace(
                    input,
                    ignoreCaseLiteralAlternationPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        if (Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, bytes, start, out var preparedMatch))
                        {
                            match = new Utf8NativeReplacementMatch(preparedMatch.Index, preparedMatch.Length, LiteralId: preparedMatch.LiteralId);
                            return true;
                        }

                        match = default;
                        return false;
                    },
                    destination,
                    out bytesWritten);

            case NativeExecutionKind.AsciiLiteralIgnoreCase when literal is { Length: > 0 }:
                var ignoreCaseLiteralReplacementPlan = RewriteWholeLiteralCapturePlan(replacement.Plan);
                if (Utf8WholeMatchTemplateReplaceEngine.CanExecute(ignoreCaseLiteralReplacementPlan))
                {
                    return Utf8WholeMatchTemplateReplaceEngine.TryReplace(
                        input,
                        ignoreCaseLiteralReplacementPlan,
                        (ReadOnlySpan<byte> bytes, int start, out int matchIndex, out int matchLength) =>
                        {
                            matchIndex = FindNextLiteralViaInterpreter(bytes, start, ignoreCase: true, budget, out matchLength);
                            return matchIndex >= 0;
                        },
                        destination,
                        out bytesWritten,
                        budget);
                }

                return Utf8NativeReplacementExecutor.TryReplace(
                    input,
                    ignoreCaseLiteralReplacementPlan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        var matchIndex = FindNextLiteralViaInterpreter(bytes, start, ignoreCase: true, budget, out var matchLength);
                        match = new Utf8NativeReplacementMatch(matchIndex, matchLength);
                        return matchIndex >= 0;
                    },
                    destination,
                    out bytesWritten);

            case NativeExecutionKind.AsciiSimplePattern when validation.IsAscii:
                if (TryGetDeterministicSimplePatternReplacementTemplate(replacement, out var template))
                {
                    if (_regexPlan.StructuralLinearProgram.Kind == Utf8StructuralLinearProgramKind.AsciiFixedTokenPattern)
                    {
                        return Utf8FixedTemplateReplaceEngine.TryReplace(
                            input,
                            template,
                            _regexPlan.StructuralLinearProgram,
                            destination,
                            out bytesWritten,
                            budget);
                    }

                    return Utf8FixedTemplateReplaceEngine.TryReplace(
                        input,
                        template,
                        bytes => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, 0, captures: null, budget, out _),
                        (bytes, start) => Utf8ExecutionInterpreter.FindNextSimplePattern(bytes, _regexPlan.ExecutionProgram, _regexPlan.SearchPlan, _regexPlan.SimplePatternPlan, start, captures: null, budget, out _),
                        destination,
                        out bytesWritten,
                        budget);
                }

                var captures = replacement.ContainsGroupReferences ? new Utf8CaptureSlots(GetNativeCaptureSlotCount()) : null;
                return Utf8NativeReplacementExecutor.TryReplace(
                    input,
                    replacement.Plan,
                    (ReadOnlySpan<byte> bytes, int start, out Utf8NativeReplacementMatch match) =>
                    {
                        var matchIndex = Utf8ExecutionInterpreter.FindNextSimplePattern(
                            bytes,
                            _regexPlan.ExecutionProgram,
                            _regexPlan.SearchPlan,
                            _regexPlan.SimplePatternPlan,
                            start,
                            captures,
                            budget,
                            out var matchLength);
                        match = new Utf8NativeReplacementMatch(matchIndex, matchLength, captures);
                        return matchIndex >= 0;
                    },
                    destination,
                    out bytesWritten);

            default:
                bytesWritten = 0;
                return false;
        }
    }

    private int GetNativeCaptureSlotCount()
    {
        return _groupNumbers.Length == 0 ? 1 : (_groupNumbers[^1] + 1);
    }

    private bool TryGetDeterministicSimplePatternReplacementTemplate(
        Utf8AnalyzedReplacement replacement,
        out Utf8FixedTemplateReplacement template)
    {
        template = default;
        if (_regexPlan.ExecutionKind != NativeExecutionKind.AsciiSimplePattern ||
            !replacement.ContainsGroupReferences ||
            !Utf8ExecutionInterpreter.TryGetDeterministicCaptureLayout(_regexPlan.ExecutionProgram, _regexPlan.SimplePatternPlan, out var captureLayout))
        {
            return false;
        }

        var segments = new List<Utf8FixedTemplateReplacementSegment>(replacement.Plan.Instructions.Count);
        var replacementLength = 0;
        foreach (var instruction in replacement.Plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    var literalLength = instruction.LiteralUtf8?.Length ?? 0;
                    if (literalLength > 0)
                    {
                        segments.Add(new Utf8FixedTemplateReplacementSegment(instruction.LiteralUtf8!));
                        replacementLength += literalLength;
                    }
                    break;

                case Utf8ReplacementInstructionKind.WholeMatch:
                    segments.Add(new Utf8FixedTemplateReplacementSegment(0, captureLayout.MatchLength));
                    replacementLength += captureLayout.MatchLength;
                    break;

                case Utf8ReplacementInstructionKind.Group when instruction.GroupNumber == 0:
                    segments.Add(new Utf8FixedTemplateReplacementSegment(0, captureLayout.MatchLength));
                    replacementLength += captureLayout.MatchLength;
                    break;

                case Utf8ReplacementInstructionKind.Group when captureLayout.Captures.TryGetValue(instruction.GroupNumber, out var capture):
                    segments.Add(new Utf8FixedTemplateReplacementSegment(capture.Offset, capture.Length));
                    replacementLength += capture.Length;
                    break;

                default:
                    return false;
            }
        }

        template = new Utf8FixedTemplateReplacement(captureLayout.MatchLength, replacementLength, [.. segments]);
        return true;
    }

    private Utf8ReplacementPlan RewriteWholeLiteralCapturePlan(Utf8ReplacementPlan plan)
    {
        var captureNumber = GetWholeLiteralCaptureNumber();
        if (captureNumber <= 0)
        {
            return plan;
        }

        List<Utf8ReplacementInstruction>? rewritten = null;
        for (var i = 0; i < plan.Instructions.Count; i++)
        {
            var instruction = plan.Instructions[i];
            if (instruction.Kind == Utf8ReplacementInstructionKind.Group &&
                instruction.GroupNumber == captureNumber)
            {
                rewritten ??= [.. plan.Instructions];
                rewritten[i] = new Utf8ReplacementInstruction(Utf8ReplacementInstructionKind.WholeMatch);
            }
        }

        return rewritten is null ? plan : new Utf8ReplacementPlan(rewritten);
    }

    private int GetWholeLiteralCaptureNumber()
    {
        if (_groupNumbers.Length == 2 &&
            Pattern.Length >= 3 &&
            Pattern[0] == '(' &&
            Pattern[^1] == ')' &&
            IsPlainLiteralPattern(Pattern[1..^1]))
        {
            return 1;
        }

        if (_regexPlan.ExecutionTree?.Root is not { } root ||
            root.Kind != Utf8ExecutionNodeKind.Capture ||
            root.Children.Count != 1)
        {
            return 0;
        }

        return TryGetWholeLiteralCaptureNumber(root.Children[0], out var captureNumber)
            ? captureNumber
            : 0;
    }

    private static bool TryGetWholeLiteralCaptureNumber(Utf8ExecutionNode node, out int captureNumber)
    {
        switch (node.Kind)
        {
            case Utf8ExecutionNodeKind.Capture when node.Children.Count == 1:
                if (TryGetWholeLiteralCaptureNumber(node.Children[0], out _))
                {
                    captureNumber = node.CaptureNumber;
                    return captureNumber > 0;
                }

                break;

            case Utf8ExecutionNodeKind.Group when node.Children.Count == 1:
                return TryGetWholeLiteralCaptureNumber(node.Children[0], out captureNumber);

            case Utf8ExecutionNodeKind.Concatenate:
                Utf8ExecutionNode? substantiveChild = null;
                for (var i = 0; i < node.Children.Count; i++)
                {
                    var child = node.Children[i];
                    if (IsZeroWidthBoundaryNode(child.Kind))
                    {
                        continue;
                    }

                    if (substantiveChild is not null)
                    {
                        captureNumber = 0;
                        return false;
                    }

                    substantiveChild = child;
                }

                if (substantiveChild is not null)
                {
                    return TryGetWholeLiteralCaptureNumber(substantiveChild, out captureNumber);
                }

                break;

            case Utf8ExecutionNodeKind.One:
            case Utf8ExecutionNodeKind.Multi:
                captureNumber = 0;
                return true;

            case Utf8ExecutionNodeKind.Alternate:
                for (var i = 0; i < node.Children.Count; i++)
                {
                    if (!TryGetWholeLiteralCaptureNumber(node.Children[i], out _))
                    {
                        captureNumber = 0;
                        return false;
                    }
                }

                captureNumber = 0;
                return true;
        }

        captureNumber = 0;
        return false;
    }

    private static bool IsZeroWidthBoundaryNode(Utf8ExecutionNodeKind kind)
    {
        return kind is Utf8ExecutionNodeKind.Boundary or
            Utf8ExecutionNodeKind.NonBoundary or
            Utf8ExecutionNodeKind.Bol or
            Utf8ExecutionNodeKind.Eol or
            Utf8ExecutionNodeKind.Beginning or
            Utf8ExecutionNodeKind.Start or
            Utf8ExecutionNodeKind.EndZ or
            Utf8ExecutionNodeKind.End;
    }

    private static bool IsPlainLiteralPattern(string pattern)
    {
        foreach (var ch in pattern)
        {
            if (Utf8RegexSyntax.IsRegexMetaCharacter(ch))
            {
                return false;
            }
        }

        return true;
    }

    private Utf8ExecutionBudget? CreateExecutionBudget()
    {
        return Utf8ExecutionBudget.Create(Pattern, MatchTimeout);
    }

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
        if (_regexPlan.ExecutionPattern.IndexOf('(') >= 0)
        {
            return false;
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => true,
            Utf8CompiledEngineKind.LiteralFamily => !_regexPlan.SearchPlan.HasBoundaryRequirements &&
                !ShouldPreferFallbackForCompiledLiteralFamilyTextOperations(),
            Utf8CompiledEngineKind.StructuralLinearAutomaton
                => _regexPlan.StructuralLinearProgram.DeterministicProgram.HasValue && validation.IsAscii,
            _ => false,
        };
    }

    private bool ShouldPreferFallbackForCompiledLiteralFamilyTextOperations()
    {
        return _compiledEngine.Kind == Utf8CompiledEngineKind.LiteralFamily &&
            !UsesRightToLeft() &&
            _regexPlan.ExecutionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals &&
            !_regexPlan.SearchPlan.HasBoundaryRequirements &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement;
    }

    private bool UsesRightToLeft()
    {
        return (Options & RegexOptions.RightToLeft) != 0;
    }

    private bool IsMatchExactAsciiLiteral(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return UsesRightToLeft()
            ? FindLastLiteralViaSearch(input, budget) >= 0
            : FindFirstLiteralViaSearch(input, budget) >= 0;
    }

    private bool IsMatchExactUtf8Literal(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return UsesRightToLeft()
            ? FindLastLiteralViaSearch(input, budget) >= 0
            : FindFirstLiteralViaSearch(input, budget) >= 0;
    }

    private bool IsMatchExactUtf8Literals(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return UsesRightToLeft()
            ? FindLastUtf8LiteralAlternationViaSearch(input, budget, out _) >= 0
            : FindNextUtf8LiteralAlternationViaSearch(input, 0, budget, out _) >= 0;
    }

    private bool IsMatchAsciiStructuralIdentifierFamily(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return UsesRightToLeft()
            ? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input))
            : FindNextAsciiStructuralIdentifierFamily(input, 0, budget, out _) >= 0;
    }

    private bool IsMatchViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.IsMatch(input, default, budget);
    }

    private bool IsMatchViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.IsMatch(input, default, budget);
    }

    private bool CanUseWellFormedOnlyValidation()
    {
        return _regexPlan.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals
                or NativeExecutionKind.AsciiLiteralIgnoreCase or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals
                or NativeExecutionKind.AsciiOrderedLiteralWindow or NativeExecutionKind.AsciiStructuralIdentifierFamily ||
            (_compiledEngine.Kind == Utf8CompiledEngineKind.FallbackRegex &&
             !CanGuideFallbackVerification());
    }

    private bool TryUseAsciiInputValidationShortcut(ReadOnlySpan<byte> input)
    {
        if (_regexPlan.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or
                                             NativeExecutionKind.ExactUtf8Literal or
                                             NativeExecutionKind.ExactUtf8Literals or
                                             NativeExecutionKind.AsciiLiteralIgnoreCase or
                                             NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals or
                                             NativeExecutionKind.AsciiOrderedLiteralWindow or
                                             NativeExecutionKind.AsciiStructuralIdentifierFamily) &&
            !_regexPlan.FallbackDirectFamily.SupportsAsciiDefinitiveIsMatch)
        {
            return false;
        }

        return Utf8InputAnalyzer.IsAscii(input);
    }

    private bool TryGetAsciiCultureInvariantTwin(ReadOnlySpan<byte> input, out Utf8Regex twin)
    {
        if (_asciiCultureInvariantTwin is not null &&
            Utf8InputAnalyzer.IsAscii(input))
        {
            twin = _asciiCultureInvariantTwin;
            return true;
        }

        twin = null!;
        return false;
    }

    private bool CanUseFusedCompiledUtf8LiteralCount()
    {
        if (_compiledEngine.Kind != Utf8CompiledEngineKind.ExactLiteral ||
            _regexPlan.ExecutionKind != NativeExecutionKind.ExactUtf8Literal ||
            UsesRightToLeft() ||
            _regexPlan.SearchPlan.HasBoundaryRequirements ||
            _regexPlan.SearchPlan.HasTrailingLiteralRequirement ||
            _regexPlan.LiteralUtf8 is not { Length: > 0 } literal)
        {
            return false;
        }

        if (literal.Length % 3 != 0)
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

    private bool CanUseFusedCompiledUtf8LiteralFamilyCount()
    {
        if (_compiledEngine.Kind != Utf8CompiledEngineKind.LiteralFamily ||
            _regexPlan.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            UsesRightToLeft() ||
            _regexPlan.SearchPlan.HasBoundaryRequirements ||
            _regexPlan.SearchPlan.HasTrailingLiteralRequirement ||
            _regexPlan.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals)
        {
            return false;
        }

        for (var i = 0; i < literals.Length; i++)
        {
            var literal = literals[i];
            if (literal.Length == 0 || literal.Length % 3 != 0)
            {
                return false;
            }

            for (var j = 0; j < literal.Length; j += 3)
            {
                var b0 = literal[j];
                if (b0 < 0xE0 || b0 >= 0xF0)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool CanUseFusedCompiledAsciiLiteralFamilyCount()
    {
        if (_compiledEngine.Kind != Utf8CompiledEngineKind.LiteralFamily ||
            _regexPlan.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            UsesRightToLeft() ||
            _regexPlan.SearchPlan.HasBoundaryRequirements ||
            _regexPlan.SearchPlan.HasTrailingLiteralRequirement ||
            _regexPlan.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals)
        {
            return false;
        }

        for (var i = 0; i < literals.Length; i++)
        {
            if (!Utf8InputAnalyzer.IsAscii(literals[i]))
            {
                return false;
            }
        }

        return true;
    }

    private bool IsMatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation, allowByteSafeStructuralLinear: true))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
        }

        if (ShouldUseInterpreterForTimeoutSensitiveSimplePattern())
        {
            return Utf8ExecutionInterpreter.FindNextSimplePattern(
                input,
                _regexPlan.ExecutionProgram,
                _regexPlan.SearchPlan,
                _regexPlan.SimplePatternPlan,
                0,
                captures: null,
                budget,
                out _) >= 0;
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => IsMatchViaCompiledExactLiteralEngine(input, budget),
            Utf8CompiledEngineKind.LiteralFamily => IsMatchViaCompiledLiteralFamilyEngine(input, budget),
            _ => _compiledEngineRuntime.IsMatch(input, validation, budget),
        };
    }

    private int CountViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.Count(input, default, budget);
    }

    private int CountViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.Count(input, default, budget);
    }

    private int CountViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation, allowByteSafeStructuralLinear: true))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => CountViaCompiledExactLiteralEngine(input, budget),
            Utf8CompiledEngineKind.LiteralFamily => CountViaCompiledLiteralFamilyEngine(input, budget),
            _ => _compiledEngineRuntime.Count(input, validation, budget),
        };
    }

    private int CountExactLiteral(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (literal is null || literal.Length == 0)
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - literal.Length)
        {
            var found = Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, input, index);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + literal.Length;
        }

        return count;
    }

    private Utf8ValueMatch MatchExactAsciiLiteral(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (literal is null)
        {
            return Utf8ValueMatch.NoMatch;
        }

        var index = UsesRightToLeft()
            ? FindLastLiteralViaSearch(input, budget)
            : FindFirstLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: index,
            lengthInUtf16: literal.Length,
            indexInBytes: index,
            lengthInBytes: literal.Length);
    }

    private Utf8ValueMatch MatchExactUtf8Literal(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (literal is null)
        {
            return Utf8ValueMatch.NoMatch;
        }

        var index = UsesRightToLeft()
            ? FindLastLiteralViaSearch(input, budget)
            : FindFirstLiteralViaSearch(input, budget);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: GetUtf16LengthOfPrefix(input, index),
            lengthInUtf16: GetLiteralUtf16Length(literal),
            indexInBytes: index,
            lengthInBytes: literal.Length);
    }

    private Utf8ValueMatch MatchExactUtf8Literals(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        PreparedSearchMatch match;
        if (UsesRightToLeft())
        {
            budget?.Step(input);
            if (!Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out match))
            {
                return Utf8ValueMatch.NoMatch;
            }
        }
        else
        {
            budget?.Step(input);
            if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, 0, out match))
            {
                return Utf8ValueMatch.NoMatch;
            }
        }

        var index = match.Index;
        var matchedByteLength = match.Length;
        var matchedUtf16Length = _regexPlan.SearchPlan.AlternateLiteralUtf16Lengths is { Length: > 0 } utf16Lengths &&
            (uint)match.LiteralId < (uint)utf16Lengths.Length
            ? utf16Lengths[match.LiteralId]
            : Utf8Validation.Validate(input.Slice(index, matchedByteLength)).Utf16Length;
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: GetUtf16LengthOfPrefix(input, index),
            lengthInUtf16: matchedUtf16Length,
            indexInBytes: index,
            lengthInBytes: matchedByteLength);
    }

    private Utf8ValueMatch MatchViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.Match(input, default, budget);
    }

    private Utf8ValueMatch MatchViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.Match(input, default, budget);
    }

    private Utf8ValueMatch MatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionBudget? budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            return MatchFallback(input);
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => MatchViaCompiledExactLiteralEngine(input, budget),
            Utf8CompiledEngineKind.LiteralFamily => MatchViaCompiledLiteralFamilyEngine(input, budget),
            _ => _compiledEngineRuntime.Match(input, validation, budget),
        };
    }

    private Utf8ValueMatch MatchAsciiStructuralIdentifierFamily(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        if (UsesRightToLeft())
        {
            return MatchFallback(input);
        }

        var index = FindNextAsciiStructuralIdentifierFamily(input, 0, budget, out var matchedByteLength);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: index,
            lengthInUtf16: matchedByteLength,
            indexInBytes: index,
            lengthInBytes: matchedByteLength);
    }

    private bool IsMatchAsciiLiteralIgnoreCase(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (literal is null)
        {
            return false;
        }

        return UsesRightToLeft()
            ? FindLastIgnoreCaseLiteralViaSearch(input, budget) >= 0
            : FindFirstIgnoreCaseLiteralViaSearch(input, budget) >= 0;
    }

    private bool IsMatchAsciiLiteralIgnoreCaseLiterals(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return UsesRightToLeft()
            ? FindLastAsciiIgnoreCaseLiteralAlternationViaSearch(input, budget, out _) >= 0
            : FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(input, 0, budget, out _) >= 0;
    }

    private int CountAsciiLiteralIgnoreCase(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (literal is null || literal.Length == 0 || _regexPlan.SearchPlan.LiteralSearch is not { } literalSearch)
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
        }

        if (budget is null)
        {
            var preferredCompareIndex = literalSearch.GetIgnoreCasePreferredCompareIndex();
            if (preferredCompareIndex >= 0)
            {
                return literalSearch.CountIgnoreCaseWithPreferredCompareIndex(input, preferredCompareIndex, out _, out _);
            }

            return literalSearch.CountIgnoreCaseWithTier(input, literalSearch.IgnoreCaseTier, out _, out _);
        }

        var count = 0;
        var index = 0;
        while (index <= input.Length - literal.Length)
        {
            budget?.Step(input[index..]);
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

    private Utf8ValueMatch MatchAsciiLiteralIgnoreCase(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var literal = _regexPlan.LiteralUtf8;
        if (literal is null)
        {
            return Utf8ValueMatch.NoMatch;
        }

        if (literal.Length == 0)
        {
            return MatchFallback(input);
        }

        var index = UsesRightToLeft()
            ? FindLastIgnoreCaseLiteralViaSearch(input, budget)
            : FindFirstIgnoreCaseLiteralViaSearch(input, budget);
        if (index >= 0)
        {
            return new Utf8ValueMatch(
                success: true,
                isByteAligned: true,
                indexInUtf16: index,
                lengthInUtf16: literal.Length,
                indexInBytes: index,
                lengthInBytes: literal.Length);
        }

        return Utf8ValueMatch.NoMatch;
    }

    private Utf8ValueMatch MatchAsciiLiteralIgnoreCaseLiterals(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var index = UsesRightToLeft()
            ? FindLastAsciiIgnoreCaseLiteralAlternationViaSearch(input, budget, out var matchedByteLength)
            : FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(input, 0, budget, out matchedByteLength);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: index,
            lengthInUtf16: matchedByteLength,
            indexInBytes: index,
            lengthInBytes: matchedByteLength);
    }

    private Utf8ValueMatch MatchAsciiSimplePattern(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var index = FindFirstSimplePatternViaInterpreter(input, budget, out var matchLength);
        if (index < 0)
        {
            return Utf8ValueMatch.NoMatch;
        }

        return new Utf8ValueMatch(
            success: true,
            isByteAligned: true,
            indexInUtf16: index,
            lengthInUtf16: matchLength,
            indexInBytes: index,
            lengthInBytes: matchLength);
    }

    private Utf8ValueMatchEnumerator CreateMatchEnumeratorViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, byte[] literal, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.CreateMatchEnumerator(input, default, budget);
    }

    private Utf8ValueMatchEnumerator CreateMatchEnumeratorViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.CreateMatchEnumerator(input, default, budget);
    }

    private Utf8ValueSplitEnumerator CreateSplitEnumeratorViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, byte[] literal, int count, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.CreateSplitEnumerator(input, default, count, budget);
    }

    private Utf8ValueSplitEnumerator CreateSplitEnumeratorViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, int count, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.CreateSplitEnumerator(input, default, count, budget);
    }

    private Utf8ValueMatchEnumerator CreateMatchEnumeratorViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, byte[]? literal, Utf8ExecutionBudget? budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, analysis.BoundaryMap);
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral when literal is { Length: > 0 }
                => CreateMatchEnumeratorViaCompiledExactLiteralEngine(input, literal, budget),
            Utf8CompiledEngineKind.LiteralFamily when _regexPlan.SearchPlan.AlternateLiteralsUtf8 is { Length: > 1 }
                => CreateMatchEnumeratorViaCompiledLiteralFamilyEngine(input, budget),
            _ => _compiledEngineRuntime.CreateMatchEnumerator(input, validation, budget),
        };
    }

    private Utf8ValueSplitEnumerator CreateSplitEnumeratorViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionBudget? budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, analysis.BoundaryMap);
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral when _regexPlan.LiteralUtf8 is { Length: > 0 } literal
                => CreateSplitEnumeratorViaCompiledExactLiteralEngine(input, literal, count, budget),
            Utf8CompiledEngineKind.LiteralFamily
                when _regexPlan.SearchPlan.AlternateLiteralsUtf8 is { Length: > 1 } &&
                     !_regexPlan.SearchPlan.HasBoundaryRequirements
                => CreateSplitEnumeratorViaCompiledLiteralFamilyEngine(input, count, budget),
            _ => _compiledEngineRuntime.CreateSplitEnumerator(input, validation, count, budget),
        };
    }

    private Utf8ValueMatch MatchFallback(ReadOnlySpan<byte> input, Utf8BoundaryMap? boundaryMap = null)
    {
        if (RejectsByRequiredPrefilter(input))
        {
            return Utf8ValueMatch.NoMatch;
        }

        if (CanGuideFallbackVerification())
        {
            return MatchFallbackViaSearchStarts(input, boundaryMap);
        }

        var decoded = Encoding.UTF8.GetString(input);
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        boundaryMap ??= Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        return Utf8ProjectionExecutor.ProjectFallbackRegexMatch(input, match, boundaryMap);
    }

    private bool RejectsByRequiredPrefilter(ReadOnlySpan<byte> input)
    {
        return _regexPlan.SearchPlan.PrefilterPlan.Rejects(input);
    }

    private bool ShouldUseFallbackForAnchoredSimplePattern()
    {
        return _regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            !_regexPlan.SimplePatternPlan.AnchoredValidatorPlan.HasValue &&
            (_regexPlan.SimplePatternPlan.IsStartAnchored || _regexPlan.SimplePatternPlan.IsEndAnchored);
    }

    private bool TryMatchAsciiSimplePatternWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        if (!_hasDirectAnchoredValidatorWithoutValidation || _hasDirectAnchoredHeadTailWithoutValidation)
        {
            return false;
        }

        if (_anchoredBoundedDatePlan.HasValue)
        {
            var matched = Utf8AsciiBoundedDateTokenExecutor.TryMatchWhole(
                input,
                _anchoredBoundedDatePlan,
                _allowsTrailingNewlineBeforeEnd,
                out var dateLength,
                out var dateNeedsValidation);
            if (dateNeedsValidation)
            {
                return false;
            }

            if (matched)
            {
                match = new Utf8ValueMatch(true, true, 0, dateLength, 0, dateLength);
            }

            return true;
        }

        var directResult = Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeWithoutValidation(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out var matchedLength);

        if (directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation)
        {
            return false;
        }

        if (directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match)
        {
            match = new Utf8ValueMatch(true, true, 0, matchedLength, 0, matchedLength);
        }

        return true;
    }

    private bool TryIsMatchAsciiSimplePatternWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        isMatch = false;
        var repeatedDigitGroupPlan = _regexPlan.SimplePatternPlan.RepeatedDigitGroupPlan;
        if (repeatedDigitGroupPlan.HasValue)
        {
            var matched = Utf8AsciiRepeatedDigitGroupExecutor.TryMatchWhole(
                input,
                repeatedDigitGroupPlan,
                out _,
                out var needsValidation);
            if (needsValidation)
            {
                return false;
            }

            isMatch = matched;
            return true;
        }

        if (!_hasDirectAnchoredValidatorWithoutValidation || _hasDirectAnchoredHeadTailWithoutValidation)
        {
            return false;
        }

        if (_anchoredBoundedDatePlan.HasValue)
        {
            var matched = Utf8AsciiBoundedDateTokenExecutor.TryMatchWhole(
                input,
                _anchoredBoundedDatePlan,
                _allowsTrailingNewlineBeforeEnd,
                out _,
                out var dateNeedsValidation);
            if (dateNeedsValidation)
            {
                return false;
            }

            isMatch = matched;
            return true;
        }

        var directResult = Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeWithoutValidation(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out _);

        if (directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation)
        {
            return false;
        }

        isMatch = directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match;
        return true;
    }

    private bool TryMatchDirectFallbackFamilyWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole)
        {
            if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            {
                return false;
            }

            if (!Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input, out var signedDecimalLength))
            {
                return false;
            }

            match = new Utf8ValueMatch(true, true, 0, signedDecimalLength, 0, signedDecimalLength);
            return true;
        }

        if (_hasDirectFallbackTokenFamilyWithoutValidation)
        {
            var directResult = Utf8AsciiTokenFamilyExecutor.TryFindTokenWithoutValidation(
                input,
                0,
                _fallbackDirectFamily,
                out var tokenMatchIndex,
                out var tokenMatchedLength);
            if (directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation)
            {
                return false;
            }

            if (directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match && tokenMatchedLength > 0)
            {
                match = new Utf8ValueMatch(true, true, tokenMatchIndex, tokenMatchedLength, tokenMatchIndex, tokenMatchedLength);
            }

            return true;
        }

        if (!_hasDirectFallbackAsciiFamilyWithoutValidation)
        {
            return false;
        }

        if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
        {
            return false;
        }

        if (!Utf8AsciiDirectFamilyExecutor.TryFindMatch(
            input,
            _fallbackDirectFamily,
            default,
            default,
            out var matchIndex,
            out var matchedLength))
        {
            return false;
        }

        if (matchedLength > 0)
        {
            match = new Utf8ValueMatch(true, true, matchIndex, matchedLength, matchIndex, matchedLength);
        }

        return true;
    }

    private bool TryIsMatchDirectFallbackFamilyWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        isMatch = false;
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole)
        {
            if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            {
                return false;
            }

            isMatch = Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input, out _);
            return true;
        }

        if (_hasDirectFallbackTokenFamilyWithoutValidation)
        {
            return false;
        }

        if (!_hasDirectFallbackAsciiFamilyWithoutValidation)
        {
            return false;
        }

        if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
        {
            return false;
        }

        isMatch = Utf8AsciiDirectFamilyExecutor.TryFindMatch(
            input,
            _fallbackDirectFamily,
            default,
            default,
            out _,
            out _);
        return true;
    }

    private bool ShouldFallbackForTrailingNewlineAnchoredValidator(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        return validation.IsAscii &&
            _regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _regexPlan.SimplePatternPlan.AnchoredValidatorPlan.HasValue &&
            _regexPlan.SearchPlan.Kind == Utf8SearchKind.TrailingAnchorFixedLengthEndZ &&
            input.Length > 0 &&
            input[^1] == (byte)'\n';
    }

    private bool ShouldSkipRequiredPrefilterForCount()
    {
        return _regexPlan.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            !CanGuideFallbackVerification() &&
            _compiledEngineRuntime.SkipRequiredPrefilterForCount;
    }

    private bool ShouldSkipRequiredPrefilterForMatch()
    {
        return _compiledEngineRuntime.SkipRequiredPrefilterForMatch;
    }

    private Utf8ValidationResult GetWellFormedOnlyValidation(ReadOnlySpan<byte> input)
    {
        if (_compiledEngineRuntime is Utf8CompiledFallbackCompiledEngineRuntime or Utf8SearchGuidedFallbackCompiledEngineRuntime)
        {
            return Utf8InputAnalyzer.ValidateOnly(input);
        }

        if (!TryUseAsciiInputValidationShortcut(input) &&
            _compiledEngineRuntime.SupportsWellFormedOnlyCount &&
            Utf8.IsValid(input))
        {
            return default;
        }

        Utf8Validation.ThrowIfInvalidOnly(input);
        return default;
    }

    private bool ShouldUseFallbackForNonAsciiSimplePattern(Utf8ValidationResult validation, bool allowByteSafeStructuralLinear = false)
    {
        if (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _regexPlan.SimplePatternPlan.IsUtf8ByteSafe)
        {
            return false;
        }

        if (allowByteSafeStructuralLinear &&
            _regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _compiledEngine.Kind == Utf8CompiledEngineKind.StructuralLinearAutomaton &&
            _regexPlan.StructuralLinearProgram.AllowsUtf8ByteSafe)
        {
            return false;
        }

        return _regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern && !validation.IsAscii;
    }

    private bool ShouldUseInterpreterForTimeoutSensitiveSimplePattern()
    {
        return _regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            MatchTimeout != Regex.InfiniteMatchTimeout;
    }

    private bool ShouldUseFallbackForSimplePatternReplacement(Utf8AnalyzedReplacement replacement)
    {
        return replacement.ContainsGroupReferences ||
            (_regexPlan.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
             ShouldUseFallbackForAnchoredSimplePattern());
    }

    private bool CanGuideFallbackVerification()
    {
        return _regexPlan.SearchPlan.FallbackSearch.HasCandidates;
    }

    private bool IsMatchFallbackViaSearchStarts(ReadOnlySpan<byte> input)
    {
        var validation = Utf8InputAnalyzer.ValidateOnly(input);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        if (!Utf8BackendInstructionExecutor.TryFindNextFallbackVerifiedMatch(_regexPlan.SearchPlan, _regexPlan.SearchPlan.FirstMatchProgram, _verifierRuntime, input, validation, 0, ref boundaryMap, ref decoded, out _))
        {
            return false;
        }

        Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
        return true;
    }

    private int CountFallbackViaSearchStarts(ReadOnlySpan<byte> input)
    {
        var validation = Utf8InputAnalyzer.ValidateOnly(input);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        var count = 0;
        var startIndex = 0;
        while ((uint)startIndex <= (uint)input.Length &&
               Utf8BackendInstructionExecutor.TryFindNextFallbackVerifiedMatch(_regexPlan.SearchPlan, _regexPlan.SearchPlan.CountProgram, _verifierRuntime, input, validation, startIndex, ref boundaryMap, ref decoded, out var verification))
        {
            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            startIndex = verification.IndexInBytes + Math.Max(verification.LengthInBytes, 1);
        }

        return count;
    }

    private int CountFallbackCandidates(ReadOnlySpan<byte> input, bool requireScalarBoundary)
    {
        return Utf8SearchStrategyExecutor.CountFallbackCandidates(_regexPlan.SearchPlan, input, requireScalarBoundary);
    }

    private int CountExactLiteralWithRoute(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, string route)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(route);
        return CountExactLiteral(input, budget);
    }

    private int CountExactUtf8LiteralsWithRoute(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, string route)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(route);
        return CountExactUtf8Literals(input, budget);
    }

    private int CountAsciiLiteralIgnoreCaseWithRoute(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, string route)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(route);
        return CountAsciiLiteralIgnoreCase(input, budget);
    }

    private int CountAsciiLiteralIgnoreCaseLiteralsWithRoute(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, string route)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(route);
        return CountAsciiLiteralIgnoreCaseLiterals(input, budget);
    }

    private int CountAsciiStructuralIdentifierFamilyWithRoute(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, string route)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(route);
        return CountAsciiStructuralIdentifierFamily(input, budget);
    }

    private int CountAsciiSimplePatternWithRoute(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, string route)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(route);
        return CountAsciiSimplePattern(input, budget);
    }

    private int CountByteSafeFallbackWithRoute(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute("fallback_byte_safe_linear");
        return Utf8ByteSafeLinearExecutor.Count(input, _regexPlan, _verifierRuntime.StructuralVerifierRuntime, budget);
    }

    private int CountFallbackViaSearchStartsWithRoute(ReadOnlySpan<byte> input, string route)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(route);
        return CountFallbackViaSearchStarts(input);
    }

    private int CountFallbackDirectWithRoute(ReadOnlySpan<byte> input, string route)
    {
        Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(route);
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
    }

    private byte[] ReplaceViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, byte[] replacementBytes, byte[] literal, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.ReplaceExactLiteral(input, replacementBytes, budget);
    }

    private byte[] ReplaceViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, byte[] replacementBytes, Utf8ExecutionBudget? budget)
    {
        return _compiledEngineRuntime.ReplaceExactLiteral(input, replacementBytes, budget);
    }

    private Utf8ValueMatch MatchFallbackViaSearchStarts(ReadOnlySpan<byte> input, Utf8BoundaryMap? boundaryMap = null)
    {
        var validation = boundaryMap is null ? Utf8InputAnalyzer.ValidateOnly(input) : default;
        string? decoded = null;
        if (Utf8BackendInstructionExecutor.TryFindNextFallbackVerifiedMatch(_regexPlan.SearchPlan, _regexPlan.SearchPlan.FirstMatchProgram, _verifierRuntime, input, validation, 0, ref boundaryMap, ref decoded, out var verification))
        {
            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            return Utf8ProjectionExecutor.ProjectFallbackVerification(verification);
        }

        return Utf8ValueMatch.NoMatch;
    }

    private Utf8StructuralSearchPlan[] GetFallbackCandidatePlans()
    {
        return _regexPlan.SearchPlan.FallbackSearch.CandidatePlans ?? [];
    }

    private int FindFirstLiteralViaInterpreter(ReadOnlySpan<byte> input, bool ignoreCase, Utf8ExecutionBudget? budget)
    {
        return FindFirstLiteralViaInterpreter(input, ignoreCase, budget, out _);
    }

    private int FindFirstLiteralViaInterpreter(ReadOnlySpan<byte> input, bool ignoreCase, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        return FindNextLiteralViaInterpreter(input, 0, ignoreCase, budget, out matchedLength);
    }

    private int FindFirstLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        return Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, input);
    }

    private int FindFirstIgnoreCaseLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        return Utf8SearchExecutor.FindFirst(_regexPlan.SearchPlan, input);
    }

    private int FindLastLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        return Utf8SearchExecutor.FindLast(_regexPlan.SearchPlan, input);
    }

    private int FindLastIgnoreCaseLiteralViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        budget?.Step(input);
        return Utf8SearchExecutor.FindLast(_regexPlan.SearchPlan, input);
    }

    private int FindNextUtf8LiteralAlternationViaSearch(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget?.Step(input);
        if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, startIndex, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
    }

    private int FindLastUtf8LiteralAlternationViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget?.Step(input);
        if (!Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
    }

    private int FindNextLiteralViaInterpreter(ReadOnlySpan<byte> input, int startIndex, bool ignoreCase, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        matchedLength = 0;
        for (var index = Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, input, startIndex);
             index >= 0;
             index = Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, input, index + 1))
        {
            budget?.Step(input);
            if (Utf8ExecutionInterpreter.TryMatchLiteralPrefix(input[index..], _regexPlan.ExecutionProgram, ignoreCase, budget, out matchedLength))
            {
                return index;
            }
        }

        matchedLength = 0;
        return -1;
    }

    private int FindNextExactLiteralViaSearch(ReadOnlySpan<byte> input, int startIndex, int literalLength, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        matchedLength = 0;
        var index = Utf8SearchExecutor.FindNext(_regexPlan.SearchPlan, input, startIndex);
        if (index < 0)
        {
            return -1;
        }

        budget?.Step(input);
        matchedLength = literalLength;
        return index;
    }

    private int CountAsciiSimplePattern(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        var count = 0;
        var index = 0;
        while (index <= input.Length)
        {
            var found = FindNextSimplePatternViaInterpreter(input, index, budget, out var matchedLength);
            if (found < 0)
            {
                return count;
            }

            count++;
            index = found + Math.Max(matchedLength, 1);
        }

        return count;
    }

    private int CountAsciiStructuralIdentifierFamily(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return UsesRightToLeft()
            ? _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input))
            : Utf8AsciiStructuralIdentifierFamilyExecutor.Count(input, _regexPlan.StructuralIdentifierFamilyPlan, _regexPlan.SearchPlan, _regexPlan.StructuralSearchPlan, _verifierRuntime.StructuralVerifierRuntime, budget);
    }

    private int CountExactUtf8Literals(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.SearchPlan.PreparedSearcher.HasValue &&
            !_regexPlan.SearchPlan.HasBoundaryRequirements &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            var fastCount = 0;
            var state = new PreparedMultiLiteralScanState(0, 0, 0);
            while (true)
            {
                budget?.Step(input);
                if (!_regexPlan.SearchPlan.PreparedSearcher.TryFindNextNonOverlappingMatch(input, ref state, out _))
                {
                    return fastCount;
                }

                fastCount++;
            }
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

    private int CountAsciiLiteralIgnoreCaseLiterals(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        if (_regexPlan.SearchPlan.PreparedSearcher.HasValue &&
            !_regexPlan.SearchPlan.HasBoundaryRequirements &&
            !_regexPlan.SearchPlan.HasTrailingLiteralRequirement)
        {
            var fastCount = 0;
            var state = new PreparedMultiLiteralScanState(0, 0, 0);
            while (true)
            {
                budget?.Step(input);
                if (!_regexPlan.SearchPlan.PreparedSearcher.TryFindNextNonOverlappingMatch(input, ref state, out _))
                {
                    return fastCount;
                }

                fastCount++;
            }
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

    private int FindNextAsciiIgnoreCaseLiteralAlternationViaSearch(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget?.Step(input);
        if (!Utf8SearchExecutor.TryFindNextMatch(_regexPlan.SearchPlan, input, startIndex, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
    }

    private int FindLastAsciiIgnoreCaseLiteralAlternationViaSearch(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out int matchedByteLength)
    {
        matchedByteLength = 0;
        budget?.Step(input);
        if (!Utf8SearchExecutor.TryFindLastMatch(_regexPlan.SearchPlan, input, input.Length, out var match))
        {
            return -1;
        }

        matchedByteLength = match.Length;
        return match.Index;
    }

    private int FindFirstSimplePatternViaInterpreter(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget)
    {
        return FindFirstSimplePatternViaInterpreter(input, budget, out _);
    }

    private int FindFirstSimplePatternViaInterpreter(ReadOnlySpan<byte> input, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        return FindNextSimplePatternViaInterpreter(input, 0, budget, out matchedLength);
    }

    private int FindNextSimplePatternViaInterpreter(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        return Utf8ExecutionInterpreter.FindNextSimplePattern(
            input,
            _regexPlan.ExecutionProgram,
            _regexPlan.SearchPlan,
            _regexPlan.SimplePatternPlan,
            startIndex,
            captures: null,
            budget,
            out matchedLength);
    }

    private int FindNextAsciiStructuralIdentifierFamily(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionBudget? budget, out int matchedLength)
    {
        return Utf8AsciiStructuralIdentifierFamilyExecutor.FindNext(
            input,
            _regexPlan.StructuralIdentifierFamilyPlan,
            _regexPlan.SearchPlan,
            _regexPlan.StructuralSearchPlan,
            _verifierRuntime.StructuralVerifierRuntime,
            startIndex,
            budget,
            out matchedLength);
    }

    private static int GetLiteralUtf16Length(byte[] literal)
    {
        return Utf8Validation.Validate(literal).Utf16Length;
    }

    private static int GetUtf16LengthOfPrefix(ReadOnlySpan<byte> input, int byteCount)
    {
        return byteCount == 0
            ? 0
            : Utf8Validation.Validate(input[..byteCount]).Utf16Length;
    }

    private Utf8ValueSplitEnumerator CreateFallbackSplitEnumerator(ReadOnlySpan<byte> input, int count)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, analysis.BoundaryMap);
    }

    private Utf8ValueMatchEnumerator CreateFallbackMatchEnumerator(ReadOnlySpan<byte> input)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, analysis.BoundaryMap);
    }
}

