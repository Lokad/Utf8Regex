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

public sealed class Utf8Regex
{
    private static TimeSpan s_defaultMatchTimeout = Regex.InfiniteMatchTimeout;

    private readonly Utf8RegexProgram _program;
    private readonly Utf8ReplacementPlanCache _replacementCache = new();

    public Utf8Regex(string pattern)
        : this(pattern, RegexOptions.CultureInvariant, DefaultMatchTimeout)
    {
    }

    public Utf8Regex(string pattern, RegexOptions options)
        : this(pattern, options, DefaultMatchTimeout)
    {
    }

    public Utf8Regex(string pattern, RegexOptions options, TimeSpan matchTimeout)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        _ = new Regex(string.Empty, RegexOptions.None, matchTimeout);
        ValidateOptions(options);
        Pattern = pattern;
        Options = options;
        MatchTimeout = matchTimeout;

        _program = Utf8RegexProgram.Compile(pattern, options, matchTimeout);
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

    private Utf8PreparedRegex _preparedRegex => _program.PreparedRegex;

    private Utf8CompiledEngine _compiledEngine => _program.CompiledEngine;

    private Utf8VerifierRuntime _verifierRuntime => _program.VerifierRuntime;

    private Utf8CompiledEngineRuntime _compiledEngineRuntime => _program.CompiledEngineRuntime;

    private Utf8AsciiCultureInvariantStrategy? _asciiCultureInvariantStrategy =>
        _program.AsciiCultureInvariantStrategy;

    private Utf8EmittedKernelMatcher? _directStructuralFamilyKernelMatcher =>
        _program.DirectStructuralFamilyKernelMatcher;

    private int[] _groupNumbers => _program.GroupNumbers;

    private string[] _groupNames => _program.GroupNames;

    private bool _allowsTrailingNewlineBeforeEnd =>
        _preparedRegex.SimplePatternPlan.AllowsTrailingNewlineBeforeEnd;

    private AsciiSimplePatternAnchoredHeadTailRunPlan _anchoredHeadTailRunPlan =>
        _preparedRegex.SimplePatternPlan.AnchoredHeadTailRunPlan;

    private AsciiSimplePatternAnchoredValidatorPlan _anchoredValidatorPlan =>
        _preparedRegex.SimplePatternPlan.AnchoredValidatorPlan;

    private AsciiSimplePatternAnchoredBoundedDatePlan _anchoredBoundedDatePlan =>
        _preparedRegex.SimplePatternPlan.AnchoredBoundedDatePlan;

    private Utf8FallbackDirectFamilyPlan _fallbackDirectFamily =>
        _preparedRegex.FallbackDirectFamily;

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

    private string DebugCompiledEngineRuntimeType => _compiledEngineRuntime.GetType().Name;

    private bool DebugCanLowerEmittedKernel =>
        Utf8EmittedKernelLowerer.TryLower(_preparedRegex, out _);

    private string DebugLoweredEmittedKernelKind =>
        Utf8EmittedKernelLowerer.TryLower(_preparedRegex, out var kernelPlan)
            ? kernelPlan.Kind.ToString()
            : "None";

    private bool DebugUsesEmittedKernelMatcher => _compiledEngineRuntime.UsesEmittedKernelMatcher;

    private string DebugFallbackDirectFamilyKind => _preparedRegex.FallbackDirectFamily.Kind.ToString();

    private bool DebugHasAsciiCultureInvariantTwin => _asciiCultureInvariantStrategy is not null;

    private NativeExecutionKind? DebugAsciiCultureInvariantTwinExecutionKind => _asciiCultureInvariantStrategy?.PreparedRegex.ExecutionKind;

    private Utf8CompiledEngineKind? DebugAsciiCultureInvariantTwinCompiledEngineKind => _asciiCultureInvariantStrategy?.CompiledEngine.Kind;

    private string? DebugAsciiCultureInvariantTwinFallbackReason => _asciiCultureInvariantStrategy?.PreparedRegex.FallbackReason;

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

    private bool DebugSimplePatternCanUseDirectAnchoredFixedLength =>
        Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedLengthSimplePattern(_preparedRegex);

    private bool DebugSimplePatternCanUseDirectAnchoredFixedAlternation =>
        Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedAlternationSimplePattern(_preparedRegex);

    private int DebugSimplePatternBranchCount => _preparedRegex.SimplePatternPlan.Branches.Length;

    private string DebugSimplePatternBranchLengths =>
        string.Join(",", _preparedRegex.SimplePatternPlan.Branches.Select(static branch => branch.Length));

    private string? FallbackReason => _preparedRegex.FallbackReason;

    private bool DebugRejectsByRequiredPrefilter(ReadOnlySpan<byte> input)
    {
        return RejectsByRequiredPrefilter(input);
    }

    private bool DebugTryMatchViaAsciiCultureInvariantTwin(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
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

    private bool DebugTryGetAsciiCultureInvariantTwin(
        [NotNullWhen(true)] out Utf8AsciiCultureInvariantStrategy? strategy)
    {
        if (_asciiCultureInvariantStrategy is not null)
        {
            strategy = _asciiCultureInvariantStrategy;
            return true;
        }

        strategy = null;
        return false;
    }

    private int DebugCountViaCompiledEngine(ReadOnlySpan<byte> input)
    {
        return CountViaCompiledEngine(input, default, budget: Utf8ExecutionDeadline.Infinite);
    }

    private bool DebugTryCountExactUtf8LiteralValidatedThreeByte(ReadOnlySpan<byte> input, out int count)
    {
        return _compiledEngineRuntime.TryDebugCountExactUtf8LiteralValidatedThreeByte(input, out count);
    }

    private bool DebugTryCountExactUtf8LiteralLeadingScalarAnchored(ReadOnlySpan<byte> input, out int count)
    {
        return _compiledEngineRuntime.TryDebugCountExactUtf8LiteralLeadingScalarAnchored(input, out count);
    }

    private bool DebugTryCountExactUtf8LiteralPreparedSearch(ReadOnlySpan<byte> input, out int count)
    {
        return _compiledEngineRuntime.TryDebugCountExactUtf8LiteralPreparedSearch(input, out count);
    }

    private bool DebugTryCountExactUtf8LiteralAnchored(ReadOnlySpan<byte> input, out int count)
    {
        return _compiledEngineRuntime.TryDebugCountExactUtf8LiteralAnchored(input, out count);
    }

    private int DebugCountFallbackCandidates(ReadOnlySpan<byte> input)
    {
        return CountFallbackCandidates(input, requireScalarBoundary: false);
    }

    private int DebugCountFallbackBoundaryCandidates(ReadOnlySpan<byte> input)
    {
        return CountFallbackCandidates(input, requireScalarBoundary: true);
    }

    private int DebugCountFallbackViaSearchStarts(ReadOnlySpan<byte> input)
    {
        return CountFallbackViaSearchStarts(input);
    }

    private int DebugCountFallbackDirect(ReadOnlySpan<byte> input)
    {
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(Encoding.UTF8.GetString(input));
    }

    private Utf8ValueMatch DebugMatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        if (RejectsByRequiredPrefilter(input))
        {
            return Utf8ValueMatch.NoMatch;
        }

        return MatchViaCompiledEngine(input, validation, budget: Utf8ExecutionDeadline.Infinite);
    }

    private bool DebugTryMatchWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return TryMatchDirectWithoutValidation(input, out match);
    }

    private bool DebugTryIsMatchWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        return TryIsMatchDirectWithoutValidation(input, out isMatch);
    }

    private bool DebugTryIsMatchAsciiSimplePatternWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        return TryIsMatchAsciiSimplePatternWithoutValidation(input, out isMatch);
    }

    private bool DebugTryIsMatchAnchoredHeadTailWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        return TryIsMatchAnchoredHeadTailWithoutValidation(input, out isMatch);
    }

    private bool DebugTryMatchAsciiSimplePatternWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return TryMatchAsciiSimplePatternWithoutValidation(input, out match);
    }

    private bool DebugTryMatchAnchoredHeadTailWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return TryMatchAnchoredHeadTailWithoutValidation(input, out match);
    }

    private bool DebugCanUseFusedCompiledUtf8LiteralCount => CanUseFusedCompiledUtf8LiteralCount();

    private bool DebugCanUseFusedCompiledUtf8LiteralFamilyCount => CanUseFusedCompiledUtf8LiteralFamilyCount();

    private bool DebugCreatedExecutionBudgetIsNull => CreateExecutionBudget().IsInfinite;

    private int DebugCountViaCompiledEngineWithCreatedBudget(ReadOnlySpan<byte> input)
    {
        return CountViaCompiledEngine(input, default, CreateExecutionBudget());
    }

    private bool DebugTryMatchCompiledAsciiLiteralFamilyRaw(ReadOnlySpan<byte> input, out int index, out int matchedLength)
    {
        return _compiledEngineRuntime.TryDebugMatchAsciiLiteralFamilyRaw(input, out index, out matchedLength);
    }

    private bool DebugTryMatchDirectAnchoredFixedLengthSimplePattern(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedLengthSimplePattern(_preparedRegex, input, out matchedLength);
    }

    private bool DebugTryMatchDirectAnchoredFixedAlternationSimplePattern(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8SimplePatternCompiledWholeMatcher.TryMatchDirectAnchoredFixedAlternationSimplePattern(_preparedRegex, input, out matchedLength);
    }

    private string DebugDirectAnchoredFixedAlternationSummary(ReadOnlySpan<byte> input)
    {
        return Utf8SimplePatternCompiledWholeMatcher.GetDirectAnchoredFixedAlternationDebugSummary(_preparedRegex, input);
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

        return _compiledEngineRuntime.TryMatchWithoutValidation(input, budget: Utf8ExecutionDeadline.Infinite, out match);
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

    private bool DebugSupportsWellFormedOnlyMatch => _compiledEngineRuntime.SupportsWellFormedOnlyMatch;

    private bool DebugWellFormedOnlyMatchMissIsDefinitive => _compiledEngineRuntime.WellFormedOnlyMatchMissIsDefinitive;

    private bool DebugSupportsThrowIfInvalidOnlyCount => _compiledEngineRuntime.SupportsThrowIfInvalidOnlyCount;

    private bool DebugUsesEmittedAnchoredValidatorMatcher => _compiledEngineRuntime.UsesEmittedAnchoredValidatorMatcher;

    private string DebugAnchoredValidatorSegmentSummary =>
        Utf8AsciiAnchoredValidatorExecutor.GetSegmentSummary(_anchoredValidatorPlan);

    private bool DebugTryMatchAnchoredValidatorFixedPrefixOnly(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeFixedPrefixOnly(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out matchedLength);
    }

    private bool DebugTryMatchAnchoredValidatorFirstBoundedSegmentOnly(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeFirstBoundedSegmentOnly(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out matchedLength);
    }

    private bool DebugTryMatchAnchoredValidatorSuffixAfterFirstBounded(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWholeSuffixAfterFirstBounded(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out matchedLength);
    }

    private bool DebugTryMatchAnchoredValidatorNativeWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        return Utf8AsciiAnchoredValidatorExecutor.TryMatchWhole(
            input,
            _anchoredValidatorPlan,
            _allowsTrailingNewlineBeforeEnd,
            out matchedLength);
    }

    private bool DebugTryMatchRepeatedDigitGroupWhole(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        var plan = _preparedRegex.SimplePatternPlan.RepeatedDigitGroupPlan;
        return plan.HasValue &&
            Utf8AsciiRepeatedDigitGroupExecutor.TryMatchWhole(
                input,
                plan,
                out matchedLength,
                out _);
    }

    private bool DebugTryFindRepeatedDigitGroup(ReadOnlySpan<byte> input, out int matchedLength)
    {
        matchedLength = 0;
        var plan = _preparedRegex.SimplePatternPlan.RepeatedDigitGroupPlan;
        return plan.HasValue &&
            Utf8AsciiRepeatedDigitGroupExecutor.TryFind(
                input,
                plan,
                out _,
                out matchedLength);
    }

    private bool DebugTryMatchCompiledAnchoredValidatorWithoutValidation(ReadOnlySpan<byte> input, out int matchedLength)
    {
        if (_compiledEngine.Kind != Utf8CompiledEngineKind.SimplePatternInterpreter ||
            _preparedRegex.ExecutionKind != NativeExecutionKind.AsciiSimplePattern ||
            !_anchoredValidatorPlan.HasValue)
        {
            matchedLength = 0;
            return false;
        }

        var direct = Utf8SimplePatternCompiledWholeMatcher.TryMatchAnchoredValidatorWithoutValidation(
            _preparedRegex,
            _allowsTrailingNewlineBeforeEnd,
            input,
            out matchedLength);
        return direct == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match;
    }

    private bool DebugTryFindDirectFallbackTokenWithoutValidation(ReadOnlySpan<byte> input, out int matchIndex, out int matchedLength)
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

    private bool DebugIsMatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        if (RejectsByRequiredPrefilter(input))
        {
            return false;
        }

        return IsMatchViaCompiledEngine(input, validation, budget: Utf8ExecutionDeadline.Infinite);
    }

    private bool DebugCanGuideFallbackVerification => CanGuideFallbackVerification();

    private bool DebugIsMatchFallbackViaSearchStarts(ReadOnlySpan<byte> input)
    {
        if (RejectsByRequiredPrefilter(input) || !CanGuideFallbackVerification())
        {
            return false;
        }

        return IsMatchFallbackViaSearchStarts(input);
    }

    private Utf8ValueMatch DebugMatchAfterValidation(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        if (ShouldFallbackForTrailingNewlineAnchoredValidator(input, validation))
        {
            return MatchFallback(input);
        }

        if (RejectsByRequiredPrefilter(input))
        {
            return Utf8ValueMatch.NoMatch;
        }

        return MatchViaCompiledEngine(input, validation, budget: Utf8ExecutionDeadline.Infinite);
    }

    private bool IsMatchAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
            input.GetDecodedString().AsSpan(),
            input.Project(start).Value);
    }

    private Utf8ValueMatch MatchAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(
            input.GetDecodedString(),
            input.Project(start).Value);
        return CreateValueMatch(input.Bytes, input.BoundaryMap, match);
    }

    private Utf8MatchContext MatchDetailedAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        var decoded = input.GetDecodedString();
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(
            decoded,
            input.Project(start).Value);
        return new Utf8MatchContext(input.Bytes, decoded, match, input.BoundaryMap, _groupNames);
    }

    private int CountAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
            input.GetDecodedString().AsSpan(),
            input.Project(start).Value);
    }

    private int CountPreparedAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        var remaining = input.Bytes[start.Value..];
        if (CanUseFusedCompiledUtf8LiteralCount() || CanUseFusedCompiledUtf8LiteralFamilyCount())
        {
            return CountViaCompiledEngine(remaining, default, CreateExecutionBudget());
        }

        if (CanUseWellFormedOnlyValidation())
        {
            if (RejectsByRequiredPrefilter(remaining))
            {
                return 0;
            }

            return CountViaCompiledEngine(remaining, default, CreateExecutionBudget());
        }

        var validation = input.GetSuffixValidation(start);
        if (RejectsByRequiredPrefilter(remaining))
        {
            return 0;
        }

        return CountViaCompiledEngine(remaining, validation, CreateExecutionBudget());
    }

    private Utf8ValueMatchEnumerator EnumerateMatchesAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        var remaining = input.Bytes[start.Value..];
        var literal = _preparedRegex.LiteralUtf8;
        var budget = CreateExecutionBudget();
        if (UsesRightToLeft())
        {
            var startInUtf16 = input.Project(start);
            return new Utf8ValueMatchEnumerator(
                input.Bytes,
                _verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
                input.GetDecodedString(),
                startInUtf16.Value,
                input.BoundaryMap);
        }

        if (_preparedRegex.ExecutionKind is NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.ExactUtf8Literals)
        {
            var baseInUtf16 = input.Project(start);
            return CreateMatchEnumeratorViaCompiledEngine(remaining, default, literal, budget)
                .WithBaseOffsets(start.Value, baseInUtf16.Value);
        }

        var validation = input.GetSuffixValidation(start);
        var utf16Start = input.Project(start);
        return CreateMatchEnumeratorViaCompiledEngine(remaining, validation, literal, budget)
            .WithBaseOffsets(start.Value, utf16Start.Value);
    }

    private Utf8PreparedValueMatchEnumerator EnumeratePreparedMatchesAtByteOffset(
        Utf8ValidatedInput input,
        Utf8BytePosition start)
    {
        return new Utf8PreparedValueMatchEnumerator(input.Bytes, _preparedRegex.SearchPlan.PreparedSearcher, start);
    }

    internal readonly struct Utf8ByteOffsetExecution
    {
        private readonly Utf8Regex _owner;

        public Utf8ByteOffsetExecution(Utf8Regex owner)
        {
            _owner = owner;
        }

        public bool IsMatch(Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.IsMatchAtByteOffset(input, start);

        public Utf8ValueMatch Match(Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.MatchAtByteOffset(input, start);

        public Utf8MatchContext MatchDetailed(Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.MatchDetailedAtByteOffset(input, start);

        public int Count(Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.CountAtByteOffset(input, start);

        public int CountPrepared(Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.CountPreparedAtByteOffset(input, start);

        public Utf8ValueMatchEnumerator EnumerateMatches(Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.EnumerateMatchesAtByteOffset(input, start);

        public Utf8PreparedValueMatchEnumerator EnumeratePreparedMatches(
            Utf8ValidatedInput input,
            Utf8BytePosition start)
            => _owner.EnumeratePreparedMatchesAtByteOffset(input, start);

        public Utf8SearchPortfolioKind SearchPortfolioKind =>
            _owner._preparedRegex.SearchPlan.PortfolioKind;
    }

    private static void ValidateOptions(RegexOptions options)
    {
        _ = new Regex(string.Empty, Utf8RegexSyntax.NormalizeNonSemanticOptions(options), Regex.InfiniteMatchTimeout);
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
        try
        {
            return IsMatchCore(input);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private bool IsMatchCore(ReadOnlySpan<byte> input)
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

    public bool IsMatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var start = subject.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
            subject.GetDecodedString().AsSpan(),
            start.Value);
    }

    public int Count(ReadOnlySpan<byte> input)
    {
        try
        {
            return CountCore(input);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private int CountCore(ReadOnlySpan<byte> input)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.Count(input);
        }

        if (ShouldDecodeWholeSubjectForFallbackValueOperation())
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
                Utf8Validation.DecodeStrict(input));
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

    public int CountFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var start = subject.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Count(
            subject.GetDecodedString().AsSpan(),
            start.Value);
    }

    public Utf8ValueMatch Match(ReadOnlySpan<byte> input)
    {
        try
        {
            return MatchCore(input);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private Utf8ValueMatch MatchCore(ReadOnlySpan<byte> input)
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

    public Utf8ValueMatch MatchFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var start = subject.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(
            subject.GetDecodedString(),
            start.Value);
        return CreateValueMatch(input, subject.BoundaryMap, match);
    }

    public Utf8MatchContext MatchDetailed(ReadOnlySpan<byte> input)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.MatchDetailed(input);
        }

        var analysis = Utf8InputAnalyzer.Analyze(input);
        var decoded = analysis.GetDecodedString();
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        return new Utf8MatchContext(input, decoded, match, analysis.BoundaryMap, _groupNames);
    }

    public Utf8MatchContext MatchDetailedFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        var start = analysis.GetUtf16Position(utf16Offset, nameof(utf16Offset));
        var decoded = analysis.GetDecodedString();
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded, start.Value);
        return new Utf8MatchContext(input, decoded, match, analysis.BoundaryMap, _groupNames);
    }

    public Utf8ValueMatchEnumerator EnumerateMatches(ReadOnlySpan<byte> input)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.EnumerateMatches(input).WithTimeoutMapping(input, Pattern, MatchTimeout);
        }

        var subject = Utf8ValidatedInput.Create(input);
        var start = subject.GetBytePosition(0, "startOffsetInBytes");
        return new Utf8ByteOffsetExecution(this).EnumerateMatches(subject, start)
            .WithTimeoutMapping(input, Pattern, MatchTimeout);
    }

    public Utf8ValueMatchEnumerator EnumerateMatchesFromUtf16Offset(ReadOnlySpan<byte> input, int utf16Offset)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        var startInUtf16 = analysis.GetUtf16Position(utf16Offset, nameof(utf16Offset));
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
            analysis.BoundaryMap)
            .WithTimeoutMapping(input, Pattern, MatchTimeout);
    }

    public Utf8ValueSplitEnumerator EnumerateSplits(ReadOnlySpan<byte> input, int count = int.MaxValue)
    {
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return twin.EnumerateSplits(input, count).WithTimeoutMapping(Pattern, MatchTimeout);
        }

        if (UsesRightToLeft())
        {
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

        if (ShouldPreferFallbackForCompiledLiteralFamilyTextOperations())
        {
            var subject = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueSplitEnumerator(input, subject.GetDecodedString(), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, subject.BoundaryMap)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        var budget = CreateExecutionBudget();
        var validation = TryUseAsciiInputValidationShortcut(input)
            ? default
            : Utf8Validation.Validate(input);
        if (count > 0 && CanUseNativeSplit(validation))
        {
            return CreateSplitEnumeratorViaCompiledEngine(input, validation, count, budget)
                .WithTimeoutMapping(Pattern, MatchTimeout);
        }

        var fallbackSubject = Utf8InputAnalyzer.Analyze(input);
        return new Utf8ValueSplitEnumerator(input, fallbackSubject.GetDecodedString(), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, fallbackSubject.BoundaryMap)
            .WithTimeoutMapping(Pattern, MatchTimeout);
    }

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

    private byte[] ReplaceStringCore(ReadOnlySpan<byte> input, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var analyzed = GetParsedReplacement(replacement);
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return Utf8AsciiCultureInvariantReplacement.Replace(twin, input, analyzed);
        }

        return ReplaceCore(input, replacement, analyzed);
    }

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

    private byte[] ReplaceUtf8Core(ReadOnlySpan<byte> input, ReadOnlySpan<byte> replacementPatternUtf8)
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
            return ReplaceLiteralBytesCore(input, validation, directReplacementBytes);
        }

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
            return ReplaceLiteralBytesCore(input, validation, exactLiteralReplacementBytes);
        }

        if (TryReplaceViaNativePlan(input, validation, replacementPattern, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacementPattern, out var replacementBytes))
        {
            return Encoding.UTF8.GetBytes(ReplaceFallbackWithSharedPlan(input, replacementPattern));
        }

        return ReplaceLiteralBytesCore(input, validation, replacementBytes);
    }

    public byte[] Replace<TState>(ReadOnlySpan<byte> input, TState state, Utf8MatchEvaluator<TState> evaluator)
    {
        ArgumentNullException.ThrowIfNull(evaluator);

        var validation = Utf8Validation.Validate(input);
        var decoded = Encoding.UTF8.GetString(input);
        var boundaryMap = Utf8BoundaryMap.Create(input, validation);
        var output = new ArrayBufferWriter<byte>(input.Length);
        var writer = new Utf8ReplacementWriter(output);
        var sourcePosition = 0;
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        while (match.Success)
        {
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

    public string ReplaceToString(ReadOnlySpan<byte> input, string replacement)
    {
        try
        {
            return ReplaceToStringMappedCore(input, replacement);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private string ReplaceToStringMappedCore(ReadOnlySpan<byte> input, string replacement)
    {
        ArgumentNullException.ThrowIfNull(replacement);

        var analyzed = GetParsedReplacement(replacement);
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return Utf8AsciiCultureInvariantReplacement.ReplaceToString(twin, input, analyzed);
        }

        _ = Utf8Validation.Validate(input);
        return ReplaceToStringCore(input, analyzed);
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
        try
        {
            return TryReplaceStringCore(input, replacement, destination, out bytesWritten);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private OperationStatus TryReplaceStringCore(
        ReadOnlySpan<byte> input,
        string replacement,
        Span<byte> destination,
        out int bytesWritten)
    {
        ArgumentNullException.ThrowIfNull(replacement);
        var analyzed = GetParsedReplacement(replacement);
        if (TryGetAsciiCultureInvariantTwin(input, out var twin))
        {
            return Utf8AsciiCultureInvariantReplacement.TryReplace(twin, input, analyzed, destination, out bytesWritten);
        }

        return TryReplaceCore(input, analyzed, replacement, destination, out bytesWritten);
    }

    public OperationStatus TryReplace(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten)
    {
        try
        {
            return TryReplaceUtf8Core(input, replacementPatternUtf8, destination, out bytesWritten);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input);
        }
    }

    private OperationStatus TryReplaceUtf8Core(
        ReadOnlySpan<byte> input,
        ReadOnlySpan<byte> replacementPatternUtf8,
        Span<byte> destination,
        out int bytesWritten)
    {
        _ = Utf8Validation.Validate(replacementPatternUtf8);
        var validation = Utf8Validation.Validate(input);
        if (TryGetDirectLiteralReplacementBytes(replacementPatternUtf8, out var directReplacementBytes))
        {
            return TryReplaceLiteralBytesCore(input, validation, directReplacementBytes, destination, out bytesWritten);
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

    private bool DebugCanUseNativeSplit(ReadOnlySpan<byte> input)
    {
        var validation = Utf8Validation.Validate(input);
        return CanUseNativeSplit(validation);
    }

    private bool DebugCanUseSmallAsciiLiteralFamilySplit(ReadOnlySpan<byte> input) =>
        TryGetSmallAsciiLiteralFamilySplitSearch(input, out _);

    private int DebugCountSplitsViaCompiledEngine(ReadOnlySpan<byte> input, int count)
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

    private int DebugCountSplitsViaFallback(ReadOnlySpan<byte> input, int count)
    {
        var subject = Utf8InputAnalyzer.Analyze(input);
        var enumerator = new Utf8ValueSplitEnumerator(
            input,
            subject.GetDecodedString(),
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
            count,
            subject.BoundaryMap);
        var splitCount = 0;
        foreach (var _ in enumerator)
        {
            splitCount++;
        }

        return splitCount;
    }

    private bool DebugShouldPreferFallbackForCompiledLiteralFamilyTextOperations()
    {
        return ShouldPreferFallbackForCompiledLiteralFamilyTextOperations();
    }

    private int DebugReplaceViaFallback(ReadOnlySpan<byte> input, string replacement)
    {
        return Encoding.UTF8.GetByteCount(
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacement));
    }

    private int DebugReplaceViaNativeTextOperations(ReadOnlySpan<byte> input, string replacementText)
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
            return ReplaceLiteralBytesCore(input, validation, replacementBytes).Length;
        }

        if (TryGetExactLiteralReplacementBytes(replacement, out var exactLiteralReplacementBytes))
        {
            return ReplaceLiteralBytesCore(input, validation, exactLiteralReplacementBytes).Length;
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
        if (TryReplaceViaNativePlan(input, validation, replacement, budget, out var nativeResult))
        {
            return nativeResult;
        }

        if (!TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return Encoding.UTF8.GetBytes(ReplaceFallbackWithSharedPlan(input, replacement));
        }

        return ReplaceLiteralBytesCore(input, validation, replacementBytes);
    }

    private Utf8AnalyzedReplacement GetParsedReplacement(string replacement)
    {
        return _replacementCache.GetOrAdd(
            replacement,
            (GroupNumbers: _groupNumbers, GroupNames: _groupNames),
            static (replacementText, state) => Utf8FrontEndReplacementAnalyzer.Analyze(
                replacementText,
                state.GroupNumbers,
                state.GroupNames));
    }

    private static bool TryGetNativeReplacementBytes(
        Utf8AnalyzedReplacement replacement,
        out byte[] replacementBytes)
    {
        if (replacement.IsLiteral)
        {
            replacementBytes = replacement.LiteralUtf8;
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
        if (_preparedRegex.ExecutionKind is not (NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase) ||
            _preparedRegex.LiteralUtf8 is not { Length: > 0 } literalUtf8)
        {
            replacementBytes = [];
            return false;
        }

        if (replacement.IsLiteral)
        {
            replacementBytes = replacement.LiteralUtf8;
            return true;
        }

        var plan = RewriteWholeLiteralCapturePlan(replacement.Plan);
        var totalLength = 0;
        foreach (var instruction in plan.Instructions)
        {
            switch (instruction.Kind)
            {
                case Utf8ReplacementInstructionKind.Literal:
                    totalLength += instruction.LiteralUtf8.Length;
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
                case Utf8ReplacementInstructionKind.Literal when instruction.LiteralUtf8.Length > 0:
                    instruction.LiteralUtf8.CopyTo(replacementBytes.AsSpan(written));
                    written += instruction.LiteralUtf8.Length;
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
            return TryReplaceLiteralBytesCore(
                input,
                validation,
                exactLiteralReplacementBytes,
                destination,
                out bytesWritten);
        }

        if (TryGetNativeReplacementBytes(replacement, out var replacementBytes))
        {
            return TryReplaceLiteralBytesCore(
                input,
                validation,
                replacementBytes,
                destination,
                out bytesWritten);
        }

        return TryEncodeUtf8ToDestination(ReplaceFallbackWithSharedPlan(input, replacement), destination, out bytesWritten)
            ? OperationStatus.Done
            : OperationStatus.DestinationTooSmall;
    }

    private byte[] ReplaceLiteralBytesCore(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        byte[] replacementBytes)
    {
        var budget = CreateExecutionBudget();
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            budget);
        return Utf8CursorReplaceEngine.Replace(input, replacementBytes, ref cursor);
    }

    private OperationStatus TryReplaceLiteralBytesCore(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        byte[] replacementBytes,
        Span<byte> destination,
        out int bytesWritten)
    {
        var budget = CreateExecutionBudget();
        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            budget);
        var success = Utf8CursorReplaceEngine.TryReplace(
            input,
            replacementBytes,
            destination,
            ref cursor,
            out bytesWritten);

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
        Utf8ExecutionDeadline budget,
        out byte[] replaced)
    {
        var plan = RewriteWholeLiteralCapturePlan(replacement.Plan);
        if (replacement.IsLiteral ||
            !Utf8NativeReplacementExecutor.CanExecute(plan) ||
            plan.ReferencedCaptureGroups.Length > 0)
        {
            replaced = [];
            return false;
        }

        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            budget);
        replaced = Utf8NativeReplacementExecutor.Replace(input, plan, ref cursor);
        return true;
    }

    private bool TryReplaceViaNativePlan(
        ReadOnlySpan<byte> input,
        Utf8ValidationResult validation,
        Utf8AnalyzedReplacement replacement,
        Utf8ExecutionDeadline budget,
        Span<byte> destination,
        out int bytesWritten)
    {
        var plan = RewriteWholeLiteralCapturePlan(replacement.Plan);
        if (replacement.IsLiteral ||
            !Utf8NativeReplacementExecutor.CanExecute(plan) ||
            plan.ReferencedCaptureGroups.Length > 0)
        {
            bytesWritten = 0;
            return false;
        }

        var cursor = Utf8CompiledOperationCursorFactory.CreateMatchCursor(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            budget);
        return Utf8NativeReplacementExecutor.TryReplace(
            input,
            plan,
            ref cursor,
            destination,
            out bytesWritten);
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
                rewritten[i] = Utf8ReplacementInstruction.WholeMatch();
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

        if (_preparedRegex.ExecutionTree?.Root is not { } root ||
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

    private Utf8ExecutionDeadline CreateExecutionBudget()
    {
        return Utf8ExecutionDeadline.Start(MatchTimeout);
    }

    private int DebugReplacementCacheEntryCount => _replacementCache.Count;

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
        if (_preparedRegex.ExecutionPattern.IndexOf('(') >= 0)
        {
            return false;
        }

        return _compiledEngine.Kind switch
        {
            Utf8CompiledEngineKind.ExactLiteral => true,
            Utf8CompiledEngineKind.LiteralFamily => !_preparedRegex.SearchPlan.HasBoundaryRequirements &&
                !ShouldPreferFallbackForCompiledLiteralFamilyTextOperations(),
            Utf8CompiledEngineKind.StructuralLinearAutomaton
                => _preparedRegex.StructuralLinearProgram.DeterministicProgram.HasValue && validation.IsAscii,
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

    private bool ShouldPreferFallbackForCompiledLiteralFamilyTextOperations()
    {
        return _compiledEngine.Kind == Utf8CompiledEngineKind.LiteralFamily &&
            !UsesRightToLeft() &&
            _preparedRegex.ExecutionKind is NativeExecutionKind.ExactUtf8Literals or NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals &&
            !_preparedRegex.SearchPlan.HasBoundaryRequirements &&
            !_preparedRegex.SearchPlan.HasTrailingLiteralRequirement;
    }

    private bool UsesRightToLeft()
    {
        return (Options & RegexOptions.RightToLeft) != 0;
    }

    private bool IsMatchViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.IsMatch(input, default, budget);
    }

    private bool IsMatchViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.IsMatch(input, default, budget);
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

    private bool TryGetAsciiCultureInvariantTwin(
        ReadOnlySpan<byte> input,
        [NotNullWhen(true)] out Utf8AsciiCultureInvariantStrategy? strategy)
    {
        if (_asciiCultureInvariantStrategy is not null &&
            Utf8InputAnalyzer.IsAscii(input))
        {
            strategy = _asciiCultureInvariantStrategy;
            return true;
        }

        strategy = null;
        return false;
    }

    private bool CanUseFusedCompiledUtf8LiteralCount()
    {
        if (_compiledEngine.Kind != Utf8CompiledEngineKind.ExactLiteral ||
            _preparedRegex.ExecutionKind != NativeExecutionKind.ExactUtf8Literal ||
            UsesRightToLeft() ||
            _preparedRegex.SearchPlan.HasBoundaryRequirements ||
            _preparedRegex.SearchPlan.HasTrailingLiteralRequirement ||
            _preparedRegex.LiteralUtf8 is not { Length: > 0 } literal)
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
            _preparedRegex.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            UsesRightToLeft() ||
            _preparedRegex.SearchPlan.HasBoundaryRequirements ||
            _preparedRegex.SearchPlan.HasTrailingLiteralRequirement ||
            _preparedRegex.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals)
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
            _preparedRegex.ExecutionKind != NativeExecutionKind.ExactUtf8Literals ||
            UsesRightToLeft() ||
            _preparedRegex.SearchPlan.HasBoundaryRequirements ||
            _preparedRegex.SearchPlan.HasTrailingLiteralRequirement ||
            _preparedRegex.SearchPlan.AlternateLiteralsUtf8 is not { Length: > 0 } literals)
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

    private bool IsMatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation, allowByteSafeStructuralLinear: true))
        {
            return _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(Encoding.UTF8.GetString(input));
        }

        if (ShouldUseInterpreterForTimeoutSensitiveSimplePattern())
        {
            return Utf8ExecutionInterpreter.FindNextSimplePattern(
                input,
                _preparedRegex.ExecutionProgram,
                _preparedRegex.SearchPlan,
                _preparedRegex.SimplePatternPlan,
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

    private int CountViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.Count(input, default, budget);
    }

    private int CountViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.Count(input, default, budget);
    }

    private int CountViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

    private Utf8ValueMatch MatchViaCompiledExactLiteralEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.Match(input, default, budget);
    }

    private Utf8ValueMatch MatchViaCompiledLiteralFamilyEngine(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return _compiledEngineRuntime.Match(input, default, budget);
    }

    private Utf8ValueMatch MatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, Utf8ExecutionDeadline budget)
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

    private Utf8ValueMatchEnumerator CreateMatchEnumeratorViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, byte[]? literal, Utf8ExecutionDeadline budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueMatchEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, analysis.BoundaryMap);
        }

        return Utf8CompiledOperationCursorFactory.CreateMatchEnumerator(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            budget);
    }

    private Utf8ValueSplitEnumerator CreateSplitEnumeratorViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation, int count, Utf8ExecutionDeadline budget)
    {
        if (ShouldUseFallbackForAnchoredSimplePattern() || ShouldUseFallbackForNonAsciiSimplePattern(validation))
        {
            var analysis = Utf8InputAnalyzer.Analyze(input);
            return new Utf8ValueSplitEnumerator(input, Encoding.UTF8.GetString(input), _verifierRuntime.FallbackCandidateVerifier.FallbackRegex, count, analysis.BoundaryMap);
        }

        return Utf8CompiledOperationCursorFactory.CreateSplitEnumerator(
            _preparedRegex,
            _verifierRuntime,
            input,
            validation,
            count,
            budget);
    }

    private Utf8ValueMatch MatchFallback(ReadOnlySpan<byte> input) => MatchFallback(input, null);

    private Utf8ValueMatch MatchFallback(ReadOnlySpan<byte> input, Utf8BoundaryMap? boundaryMap)
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
        var map = boundaryMap ?? Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        return Utf8ProjectionExecutor.ProjectFallbackRegexMatch(input, match, map);
    }

    private bool RejectsByRequiredPrefilter(ReadOnlySpan<byte> input)
    {
        return Utf8PrefilterExecutor.Rejects(_preparedRegex.SearchPlan.PrefilterPlan, input);
    }

    private bool ShouldUseFallbackForAnchoredSimplePattern()
    {
        return _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            !_preparedRegex.SimplePatternPlan.AnchoredValidatorPlan.HasValue &&
            (_preparedRegex.SimplePatternPlan.IsStartAnchored || _preparedRegex.SimplePatternPlan.IsEndAnchored);
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
        var repeatedDigitGroupPlan = _preparedRegex.SimplePatternPlan.RepeatedDigitGroupPlan;
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
            _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _preparedRegex.SimplePatternPlan.AnchoredValidatorPlan.HasValue &&
            _preparedRegex.SearchPlan.Kind == Utf8SearchKind.TrailingAnchorFixedLengthEndZ &&
            input.Length > 0 &&
            input[^1] == (byte)'\n';
    }

    private bool ShouldSkipRequiredPrefilterForCount()
    {
        return _preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            !CanGuideFallbackVerification() &&
            _compiledEngineRuntime.SkipRequiredPrefilterForCount;
    }

    private bool ShouldSkipRequiredPrefilterForMatch()
    {
        return _compiledEngineRuntime.SkipRequiredPrefilterForMatch;
    }

    private bool ShouldDecodeWholeSubjectForFallbackValueOperation()
    {
        var features = _preparedRegex.Features;
        return _preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex &&
            _preparedRegex.SearchPlan.Kind == Utf8SearchKind.AsciiFoldedByteLiteral &&
            _preparedRegex.SearchPlan.MaxPossibleLength != int.MaxValue &&
            features.CaptureCount <= 1 &&
            !features.HasBackreferences &&
            !features.HasLookarounds &&
            !features.HasAtomicGroups &&
            !features.HasConditionals &&
            !features.HasLoops;
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

    private bool ShouldUseFallbackForNonAsciiSimplePattern(Utf8ValidationResult validation) =>
        ShouldUseFallbackForNonAsciiSimplePattern(validation, false);

    private bool ShouldUseFallbackForNonAsciiSimplePattern(Utf8ValidationResult validation, bool allowByteSafeStructuralLinear)
    {
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _preparedRegex.SimplePatternPlan.IsUtf8ByteSafe)
        {
            return false;
        }

        if (allowByteSafeStructuralLinear &&
            _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _compiledEngine.Kind == Utf8CompiledEngineKind.StructuralLinearAutomaton &&
            _preparedRegex.StructuralLinearProgram.AllowsUtf8ByteSafe)
        {
            return false;
        }

        return _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern && !validation.IsAscii;
    }

    private bool ShouldUseInterpreterForTimeoutSensitiveSimplePattern()
    {
        return _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            MatchTimeout != Regex.InfiniteMatchTimeout;
    }

    private bool ShouldUseFallbackForSimplePatternReplacement(Utf8AnalyzedReplacement replacement)
    {
        return replacement.ContainsGroupReferences ||
            (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
             ShouldUseFallbackForAnchoredSimplePattern());
    }

    private bool CanGuideFallbackVerification()
    {
        return _preparedRegex.SearchPlan.FallbackSearch.HasCandidates;
    }

    private bool IsMatchFallbackViaSearchStarts(ReadOnlySpan<byte> input)
    {
        var validation = Utf8InputAnalyzer.ValidateOnly(input);
        Utf8BoundaryMap? boundaryMap = null;
        string? decoded = null;
        if (!Utf8BackendInstructionExecutor.TryFindNextFallbackVerifiedMatch(_preparedRegex.SearchPlan, _preparedRegex.SearchPlan.FirstMatchOperation, _verifierRuntime, input, validation, 0, ref boundaryMap, ref decoded, out _))
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
               Utf8BackendInstructionExecutor.TryFindNextFallbackVerifiedMatch(_preparedRegex.SearchPlan, _preparedRegex.SearchPlan.CountOperation, _verifierRuntime, input, validation, startIndex, ref boundaryMap, ref decoded, out var verification))
        {
            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            count++;
            startIndex = verification.IndexInBytes + Math.Max(verification.LengthInBytes, 1);
        }

        return count;
    }

    private int CountFallbackCandidates(ReadOnlySpan<byte> input, bool requireScalarBoundary)
    {
        return Utf8SearchStrategyExecutor.CountFallbackCandidates(_preparedRegex.SearchPlan, input, requireScalarBoundary);
    }

    private Utf8ValueMatch MatchFallbackViaSearchStarts(ReadOnlySpan<byte> input) =>
        MatchFallbackViaSearchStarts(input, null);

    private Utf8ValueMatch MatchFallbackViaSearchStarts(ReadOnlySpan<byte> input, Utf8BoundaryMap? boundaryMap)
    {
        var validation = boundaryMap is null ? Utf8InputAnalyzer.ValidateOnly(input) : default;
        string? decoded = null;
        if (Utf8BackendInstructionExecutor.TryFindNextFallbackVerifiedMatch(_preparedRegex.SearchPlan, _preparedRegex.SearchPlan.FirstMatchOperation, _verifierRuntime, input, validation, 0, ref boundaryMap, ref decoded, out var verification))
        {
            Utf8SearchDiagnosticsSession.Current?.CountVerifierMatch();
            return Utf8ProjectionExecutor.ProjectFallbackVerification(verification);
        }

        return Utf8ValueMatch.NoMatch;
    }

    private int FindFirstLiteralViaInterpreter(ReadOnlySpan<byte> input, bool ignoreCase, Utf8ExecutionDeadline budget)
    {
        return FindFirstLiteralViaInterpreter(input, ignoreCase, budget, out _);
    }

    private int FindFirstLiteralViaInterpreter(ReadOnlySpan<byte> input, bool ignoreCase, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        return FindNextLiteralViaInterpreter(input, 0, ignoreCase, budget, out matchedLength);
    }

    private int FindNextLiteralViaInterpreter(ReadOnlySpan<byte> input, int startIndex, bool ignoreCase, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        matchedLength = 0;
        for (var index = Utf8SearchExecutor.FindNext(_preparedRegex.SearchPlan, input, startIndex);
             index >= 0;
             index = Utf8SearchExecutor.FindNext(_preparedRegex.SearchPlan, input, index + 1))
        {
            budget.Step();
            if (Utf8ExecutionInterpreter.TryMatchLiteralPrefix(input[index..], _preparedRegex.ExecutionProgram, ignoreCase, budget, out matchedLength))
            {
                return index;
            }
        }

        matchedLength = 0;
        return -1;
    }

    private int FindFirstSimplePatternViaInterpreter(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget)
    {
        return FindFirstSimplePatternViaInterpreter(input, budget, out _);
    }

    private int FindFirstSimplePatternViaInterpreter(ReadOnlySpan<byte> input, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        return FindNextSimplePatternViaInterpreter(input, 0, budget, out matchedLength);
    }

    private int FindNextSimplePatternViaInterpreter(ReadOnlySpan<byte> input, int startIndex, Utf8ExecutionDeadline budget, out int matchedLength)
    {
        return Utf8ExecutionInterpreter.FindNextSimplePattern(
            input,
            _preparedRegex.ExecutionProgram,
            _preparedRegex.SearchPlan,
            _preparedRegex.SimplePatternPlan,
            startIndex,
            captures: null,
            budget,
            out matchedLength);
    }

    internal readonly struct Utf8RegexInspection
    {
        private readonly Utf8Regex _owner;

        public Utf8RegexInspection(Utf8Regex owner)
        {
            _owner = owner;
        }

        public NativeExecutionKind ExecutionKind => _owner.ExecutionKind;

        public Utf8PreparedRegex PreparedRegex => _owner.PreparedRegex;

        public Utf8SearchPlan SearchPlan => _owner.SearchPlan;

        public Utf8StructuralSearchPlan StructuralSearchPlan => _owner.StructuralSearchPlan;

        public Utf8SearchPortfolioKind SearchPortfolioKind => _owner.SearchPortfolioKind;

        public Utf8CompiledEngineKind CompiledEngineKind => _owner.CompiledEngineKind;

        public Utf8CompiledExecutionBackend CompiledExecutionBackend => _owner.CompiledExecutionBackend;

        public string DebugCompiledEngineRuntimeType => _owner.DebugCompiledEngineRuntimeType;

        public bool DebugCanLowerEmittedKernel => _owner.DebugCanLowerEmittedKernel;

        public string DebugLoweredEmittedKernelKind => _owner.DebugLoweredEmittedKernelKind;

        public bool DebugUsesEmittedKernelMatcher => _owner.DebugUsesEmittedKernelMatcher;

        public string DebugFallbackDirectFamilyKind => _owner.DebugFallbackDirectFamilyKind;

        public bool DebugHasAsciiCultureInvariantTwin => _owner.DebugHasAsciiCultureInvariantTwin;

        public NativeExecutionKind? DebugAsciiCultureInvariantTwinExecutionKind =>
            _owner.DebugAsciiCultureInvariantTwinExecutionKind;

        public Utf8CompiledEngineKind? DebugAsciiCultureInvariantTwinCompiledEngineKind =>
            _owner.DebugAsciiCultureInvariantTwinCompiledEngineKind;

        public string? DebugAsciiCultureInvariantTwinFallbackReason =>
            _owner.DebugAsciiCultureInvariantTwinFallbackReason;

        public Utf8StructuralLinearProgramKind StructuralLinearProgramKind =>
            _owner.StructuralLinearProgramKind;

        public Utf8StructuralVerifierPlan StructuralVerifierPlan => _owner.StructuralVerifierPlan;

        public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan =>
            _owner.StructuralIdentifierFamilyPlan;

        public Utf8AsciiStructuralIdentifierFamilyExecutor.SharedPrefixSuffixKernelDiagnostics
            DebugStructuralSharedPrefixSuffixKernelDiagnostics =>
                _owner.DebugStructuralSharedPrefixSuffixKernelDiagnostics;

        public AsciiSimplePatternPlan SimplePatternPlan => _owner.SimplePatternPlan;

        public bool DebugSimplePatternCanUseDirectAnchoredFixedLength =>
            _owner.DebugSimplePatternCanUseDirectAnchoredFixedLength;

        public bool DebugSimplePatternCanUseDirectAnchoredFixedAlternation =>
            _owner.DebugSimplePatternCanUseDirectAnchoredFixedAlternation;

        public int DebugSimplePatternBranchCount => _owner.DebugSimplePatternBranchCount;

        public string DebugSimplePatternBranchLengths => _owner.DebugSimplePatternBranchLengths;

        public string? FallbackReason => _owner.FallbackReason;

        public bool DebugRejectsByRequiredPrefilter(ReadOnlySpan<byte> input) =>
            _owner.DebugRejectsByRequiredPrefilter(input);

        public bool DebugTryMatchViaAsciiCultureInvariantTwin(
            ReadOnlySpan<byte> input,
            out Utf8ValueMatch match) =>
            _owner.DebugTryMatchViaAsciiCultureInvariantTwin(input, out match);

        public bool DebugTryGetAsciiCultureInvariantTwin(
            [NotNullWhen(true)] out Utf8AsciiCultureInvariantStrategy? strategy) =>
            _owner.DebugTryGetAsciiCultureInvariantTwin(out strategy);

        public int DebugCountViaCompiledEngine(ReadOnlySpan<byte> input) =>
            _owner.DebugCountViaCompiledEngine(input);

        public bool DebugTryCountExactUtf8LiteralValidatedThreeByte(
            ReadOnlySpan<byte> input,
            out int count) =>
            _owner.DebugTryCountExactUtf8LiteralValidatedThreeByte(input, out count);

        public bool DebugTryCountExactUtf8LiteralLeadingScalarAnchored(
            ReadOnlySpan<byte> input,
            out int count) =>
            _owner.DebugTryCountExactUtf8LiteralLeadingScalarAnchored(input, out count);

        public bool DebugTryCountExactUtf8LiteralPreparedSearch(
            ReadOnlySpan<byte> input,
            out int count) =>
            _owner.DebugTryCountExactUtf8LiteralPreparedSearch(input, out count);

        public bool DebugTryCountExactUtf8LiteralAnchored(
            ReadOnlySpan<byte> input,
            out int count) =>
            _owner.DebugTryCountExactUtf8LiteralAnchored(input, out count);

        public int DebugCountFallbackCandidates(ReadOnlySpan<byte> input) =>
            _owner.DebugCountFallbackCandidates(input);

        public int DebugCountFallbackBoundaryCandidates(ReadOnlySpan<byte> input) =>
            _owner.DebugCountFallbackBoundaryCandidates(input);

        public int DebugCountFallbackViaSearchStarts(ReadOnlySpan<byte> input) =>
            _owner.DebugCountFallbackViaSearchStarts(input);

        public int DebugCountFallbackDirect(ReadOnlySpan<byte> input) =>
            _owner.DebugCountFallbackDirect(input);

        public Utf8ValueMatch DebugMatchViaCompiledEngine(
            ReadOnlySpan<byte> input,
            Utf8ValidationResult validation) =>
            _owner.DebugMatchViaCompiledEngine(input, validation);

        public bool DebugTryMatchWithoutValidation(
            ReadOnlySpan<byte> input,
            out Utf8ValueMatch match) =>
            _owner.DebugTryMatchWithoutValidation(input, out match);

        public bool DebugTryIsMatchWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch) =>
            _owner.DebugTryIsMatchWithoutValidation(input, out isMatch);

        public bool DebugTryIsMatchAsciiSimplePatternWithoutValidation(
            ReadOnlySpan<byte> input,
            out bool isMatch) =>
            _owner.DebugTryIsMatchAsciiSimplePatternWithoutValidation(input, out isMatch);

        public bool DebugTryIsMatchAnchoredHeadTailWithoutValidation(
            ReadOnlySpan<byte> input,
            out bool isMatch) =>
            _owner.DebugTryIsMatchAnchoredHeadTailWithoutValidation(input, out isMatch);

        public bool DebugTryMatchAsciiSimplePatternWithoutValidation(
            ReadOnlySpan<byte> input,
            out Utf8ValueMatch match) =>
            _owner.DebugTryMatchAsciiSimplePatternWithoutValidation(input, out match);

        public bool DebugTryMatchAnchoredHeadTailWithoutValidation(
            ReadOnlySpan<byte> input,
            out Utf8ValueMatch match) =>
            _owner.DebugTryMatchAnchoredHeadTailWithoutValidation(input, out match);

        public bool DebugCanUseFusedCompiledUtf8LiteralCount =>
            _owner.DebugCanUseFusedCompiledUtf8LiteralCount;

        public bool DebugCanUseFusedCompiledUtf8LiteralFamilyCount =>
            _owner.DebugCanUseFusedCompiledUtf8LiteralFamilyCount;

        public bool DebugCreatedExecutionBudgetIsNull => _owner.DebugCreatedExecutionBudgetIsNull;

        public int DebugCountViaCompiledEngineWithCreatedBudget(ReadOnlySpan<byte> input) =>
            _owner.DebugCountViaCompiledEngineWithCreatedBudget(input);

        public bool DebugTryMatchCompiledAsciiLiteralFamilyRaw(
            ReadOnlySpan<byte> input,
            out int index,
            out int matchedLength) =>
            _owner.DebugTryMatchCompiledAsciiLiteralFamilyRaw(input, out index, out matchedLength);

        public bool DebugTryMatchDirectAnchoredFixedLengthSimplePattern(
            ReadOnlySpan<byte> input,
            out int matchedLength) =>
            _owner.DebugTryMatchDirectAnchoredFixedLengthSimplePattern(input, out matchedLength);

        public bool DebugTryMatchDirectAnchoredFixedAlternationSimplePattern(
            ReadOnlySpan<byte> input,
            out int matchedLength) =>
            _owner.DebugTryMatchDirectAnchoredFixedAlternationSimplePattern(input, out matchedLength);

        public string DebugDirectAnchoredFixedAlternationSummary(ReadOnlySpan<byte> input) =>
            _owner.DebugDirectAnchoredFixedAlternationSummary(input);

        public bool DebugSupportsWellFormedOnlyMatch => _owner.DebugSupportsWellFormedOnlyMatch;

        public bool DebugWellFormedOnlyMatchMissIsDefinitive =>
            _owner.DebugWellFormedOnlyMatchMissIsDefinitive;

        public bool DebugSupportsThrowIfInvalidOnlyCount =>
            _owner.DebugSupportsThrowIfInvalidOnlyCount;

        public bool DebugUsesEmittedAnchoredValidatorMatcher =>
            _owner.DebugUsesEmittedAnchoredValidatorMatcher;

        public string DebugAnchoredValidatorSegmentSummary =>
            _owner.DebugAnchoredValidatorSegmentSummary;

        public bool DebugTryMatchAnchoredValidatorFixedPrefixOnly(
            ReadOnlySpan<byte> input,
            out int matchedLength) =>
            _owner.DebugTryMatchAnchoredValidatorFixedPrefixOnly(input, out matchedLength);

        public bool DebugTryMatchAnchoredValidatorFirstBoundedSegmentOnly(
            ReadOnlySpan<byte> input,
            out int matchedLength) =>
            _owner.DebugTryMatchAnchoredValidatorFirstBoundedSegmentOnly(input, out matchedLength);

        public bool DebugTryMatchAnchoredValidatorSuffixAfterFirstBounded(
            ReadOnlySpan<byte> input,
            out int matchedLength) =>
            _owner.DebugTryMatchAnchoredValidatorSuffixAfterFirstBounded(input, out matchedLength);

        public bool DebugTryMatchAnchoredValidatorNativeWhole(
            ReadOnlySpan<byte> input,
            out int matchedLength) =>
            _owner.DebugTryMatchAnchoredValidatorNativeWhole(input, out matchedLength);

        public bool DebugTryMatchRepeatedDigitGroupWhole(
            ReadOnlySpan<byte> input,
            out int matchedLength) =>
            _owner.DebugTryMatchRepeatedDigitGroupWhole(input, out matchedLength);

        public bool DebugTryFindRepeatedDigitGroup(
            ReadOnlySpan<byte> input,
            out int matchedLength) =>
            _owner.DebugTryFindRepeatedDigitGroup(input, out matchedLength);

        public bool DebugTryMatchCompiledAnchoredValidatorWithoutValidation(
            ReadOnlySpan<byte> input,
            out int matchedLength) =>
            _owner.DebugTryMatchCompiledAnchoredValidatorWithoutValidation(input, out matchedLength);

        public bool DebugTryFindDirectFallbackTokenWithoutValidation(
            ReadOnlySpan<byte> input,
            out int matchIndex,
            out int matchedLength) =>
            _owner.DebugTryFindDirectFallbackTokenWithoutValidation(input, out matchIndex, out matchedLength);

        public bool DebugIsMatchViaCompiledEngine(
            ReadOnlySpan<byte> input,
            Utf8ValidationResult validation) =>
            _owner.DebugIsMatchViaCompiledEngine(input, validation);

        public bool DebugCanGuideFallbackVerification => _owner.DebugCanGuideFallbackVerification;

        public bool DebugIsMatchFallbackViaSearchStarts(ReadOnlySpan<byte> input) =>
            _owner.DebugIsMatchFallbackViaSearchStarts(input);

        public Utf8ValueMatch DebugMatchAfterValidation(
            ReadOnlySpan<byte> input,
            Utf8ValidationResult validation) =>
            _owner.DebugMatchAfterValidation(input, validation);

        public bool DebugCanUseNativeSplit(ReadOnlySpan<byte> input) =>
            _owner.DebugCanUseNativeSplit(input);

        public bool DebugCanUseSmallAsciiLiteralFamilySplit(ReadOnlySpan<byte> input) =>
            _owner.DebugCanUseSmallAsciiLiteralFamilySplit(input);

        public int DebugCountSplitsViaCompiledEngine(ReadOnlySpan<byte> input) =>
            _owner.DebugCountSplitsViaCompiledEngine(input, int.MaxValue);

        public int DebugCountSplitsViaCompiledEngine(ReadOnlySpan<byte> input, int count) =>
            _owner.DebugCountSplitsViaCompiledEngine(input, count);

        public int DebugCountSplitsViaFallback(ReadOnlySpan<byte> input) =>
            _owner.DebugCountSplitsViaFallback(input, int.MaxValue);

        public int DebugCountSplitsViaFallback(ReadOnlySpan<byte> input, int count) =>
            _owner.DebugCountSplitsViaFallback(input, count);

        public bool DebugShouldPreferFallbackForCompiledLiteralFamilyTextOperations() =>
            _owner.DebugShouldPreferFallbackForCompiledLiteralFamilyTextOperations();

        public int DebugReplaceViaFallback(ReadOnlySpan<byte> input, string replacement) =>
            _owner.DebugReplaceViaFallback(input, replacement);

        public int DebugReplaceViaNativeTextOperations(
            ReadOnlySpan<byte> input,
            string replacementText) =>
            _owner.DebugReplaceViaNativeTextOperations(input, replacementText);

        public int DebugReplacementCacheEntryCount => _owner.DebugReplacementCacheEntryCount;
    }

}
