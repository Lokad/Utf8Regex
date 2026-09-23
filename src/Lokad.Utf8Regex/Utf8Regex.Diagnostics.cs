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

// PLAN.md D09: debug and inspection probes, split from the matching facade along the diagnostics ownership boundary.
/// <summary>Debug and inspection probes for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
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

    private bool DebugHasInvariantCyrillicLiteralCountStrategy =>
        _invariantCyrillicLiteralCountStrategy is not null;

    private bool DebugInvariantCyrillicCountUsesCorrelatedPrefilter =>
        _invariantCyrillicLiteralCountStrategy?.UsesCorrelatedPrefilter == true;

    private bool DebugInvariantCyrillicCountUsesDirectSingleLiteralSearch =>
        _invariantCyrillicLiteralCountStrategy?.UsesDirectSingleLiteralSearch == true;

    private bool DebugHasPackedNibbleLiteralFamilyCountStrategy =>
        _packedNibbleLiteralFamilyCountStrategy is not null;

    private bool DebugPrioritizesBoundaryLiteralFamilyCount =>
        _prioritizeBoundaryLiteralFamilyCount;

    private bool DebugHasFusedAsciiBoundaryLiteralFamilyCount =>
        _fusedAsciiBoundaryLiteralFamilyCounter is not null;

    private NativeExecutionKind? DebugAsciiCultureInvariantTwinExecutionKind => _asciiCultureInvariantStrategy?.PreparedRegex.ExecutionKind;

    private Utf8CompiledEngineKind? DebugAsciiCultureInvariantTwinCompiledEngineKind => _asciiCultureInvariantStrategy?.CompiledEngine.Kind;

    private string? DebugAsciiCultureInvariantTwinFallbackReason => _asciiCultureInvariantStrategy?.PreparedRegex.FallbackReason;

    private bool DebugSimplePatternCanUseDirectAnchoredFixedLength =>
        Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedLengthSimplePattern(_preparedRegex);

    private bool DebugSimplePatternCanUseDirectAnchoredFixedAlternation =>
        Utf8SimplePatternCompiledRuntimePolicy.CanUseDirectAnchoredFixedAlternationSimplePattern(_preparedRegex);

    private int DebugSimplePatternBranchCount => _preparedRegex.SimplePatternPlan.Branches.Length;

    private string DebugSimplePatternBranchLengths =>
        string.Join(",", _preparedRegex.SimplePatternPlan.Branches.Select(static branch => branch.Length));

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

    private string DebugSelectedCountKernelKind =>
        _packedNibbleLiteralFamilyCountStrategy is not null
            ? "PackedNibbleLiteralFamily"
            : _fallbackDirectFamily.Kind is
                Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount or
                Utf8FallbackDirectFamilyKind.Utf8UriToken
                    ? $"FallbackDirect/{_fallbackDirectFamily.Kind}"
                    : _compiledEngine.Kind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily
                        ? $"{_compiledEngine.Kind}/{_compiledEngineRuntime.GetType().Name}"
                        : "Unsupported";

    private bool DebugTryCountSelectedKernelWithoutValidation(ReadOnlySpan<byte> input, out int count)
    {
        if (_packedNibbleLiteralFamilyCountStrategy is { } packedNibbleLiteralFamilyCountStrategy)
        {
            count = packedNibbleLiteralFamilyCountStrategy.Count(input);
            return true;
        }

        if (_fallbackDirectFamily.Kind is
            Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount or
            Utf8FallbackDirectFamilyKind.Utf8UriToken)
        {
            count = CountViaCompiledEngine(input, default, budget: Utf8ExecutionDeadline.Infinite);
            return true;
        }

        if (_compiledEngine.Kind is Utf8CompiledEngineKind.ExactLiteral or Utf8CompiledEngineKind.LiteralFamily)
        {
            count = CountViaCompiledEngine(input, default, budget: Utf8ExecutionDeadline.Infinite);
            return true;
        }

        count = 0;
        return false;
    }

    private bool DebugTryGetSelectedCountKernelMetrics(
        ReadOnlySpan<byte> input,
        out Utf8SelectedCountKernelMetrics metrics)
    {
        if (_packedNibbleLiteralFamilyCountStrategy is { } packedNibbleLiteralFamilyCountStrategy)
        {
            metrics = packedNibbleLiteralFamilyCountStrategy.InspectMetrics(input);
            return true;
        }

        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.Utf8WordDelimitedTokenCount)
        {
            metrics = Utf8WordDelimitedTokenExecutor.InspectMetrics(input);
            return true;
        }

        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.Utf8UriToken)
        {
            metrics = Utf8InputAnalyzer.IsAscii(input)
                ? Utf8AsciiUriTokenExecutor.InspectMetrics(input)
                : Utf8UriTokenCountExecutor.InspectMetrics(input);
            return true;
        }

        return _compiledEngineRuntime.TryDebugGetSelectedCountKernelMetrics(input, out metrics);
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

    private bool DebugTryIsMatchLiteralFamily(ReadOnlySpan<byte> input, out bool isMatch)
    {
        return _compiledEngineRuntime.TryDebugIsMatchLiteralFamily(input, out isMatch);
    }

    private bool DebugTryIsMatchExactLiteral(ReadOnlySpan<byte> input, out bool isMatch)
    {
        return _compiledEngineRuntime.TryDebugIsMatchExactLiteral(input, out isMatch);
    }

    private bool DebugTryMatchExactLiteral(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        return _compiledEngineRuntime.TryDebugMatchExactLiteral(input, out match);
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

    private bool DebugSupportsWellFormedOnlyMatch => _compiledEngineRuntime.SupportsWellFormedOnlyMatch;

    private bool DebugWellFormedOnlyMatchMissIsDefinitive => _compiledEngineRuntime.WellFormedOnlyMatchMissIsDefinitive;

    private bool DebugSupportsThrowIfInvalidOnlyCount => _compiledEngineRuntime.SupportsThrowIfInvalidOnlyCount;

    private bool DebugUsesEmittedWholeMatcher => _compiledEngineRuntime.UsesEmittedWholeMatcher;

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

    private bool DebugCanUseNativeSplit(ReadOnlySpan<byte> input)
    {
        var validation = Utf8Validation.Validate(input);
        return CanUseNativeSplit(validation);
    }

    private bool DebugCanUseSmallAsciiLiteralFamilySplit(ReadOnlySpan<byte> input) =>
        TryGetSmallAsciiLiteralFamilySplitSearch(input, out _);

    private int DebugCountSplitsViaCompiledEngine(ReadOnlySpan<byte> input, int count)
    {
        var usesAsciiValidationShortcut = TryUseAsciiInputValidationShortcut(input);
        var validation = usesAsciiValidationShortcut
            ? default
            : Utf8Validation.Validate(input);
        var totalUtf16Length = usesAsciiValidationShortcut
            ? input.Length
            : validation.Utf16Length;
        var budget = CreateExecutionBudget();
        var enumerator = CreateSplitEnumeratorViaCompiledEngine(input, validation, totalUtf16Length, count, budget);
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

    private string DebugLiteralFamilyCaptureMapSummary()
    {
        if (_literalFamilyCaptureMap is not { } map)
        {
            return "declined";
        }

        var branches = new StringBuilder();
        for (var i = 0; i < map.BranchGroups.Length; i++)
        {
            if (i > 0)
            {
                branches.Append(";");
            }

            branches.Append(string.Join(",", map.BranchGroups[i]));
        }

        return "whole=[" + string.Join(",", map.WholeMatchGroups) + "];branches=[" + branches.ToString() + "]";
    }

    private int DebugReplaceViaFallback(ReadOnlySpan<byte> input, string replacement)
    {
        Utf8Validation.ThrowIfInvalidOnly(input);
        return Encoding.UTF8.GetBytes(
            _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Replace(
                Encoding.UTF8.GetString(input),
                replacement)).Length;
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

    private int DebugReplacementCacheEntryCount => _replacementCache.Count;
    private bool DebugFallbackRegexIsCreated => _verifierRuntime.FallbackCandidateVerifier.IsFallbackRegexCreated;

    private bool DebugAnchoredFallbackRegexIsCreated => _verifierRuntime.FallbackCandidateVerifier.IsAnchoredFallbackRegexCreated;

    internal readonly struct Utf8RegexInspection
    {
        private readonly Utf8Regex _owner;

        public Utf8RegexInspection(Utf8Regex owner)
        {
            _owner = owner;
        }

        public NativeExecutionKind ExecutionKind => _owner.ExecutionKind;

        public bool DebugUsesOwnedSliceProgram => _owner._sliceProgram is not null;

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

        public bool DebugHasInvariantCyrillicLiteralCountStrategy =>
            _owner.DebugHasInvariantCyrillicLiteralCountStrategy;

        public bool DebugInvariantCyrillicCountUsesCorrelatedPrefilter =>
            _owner.DebugInvariantCyrillicCountUsesCorrelatedPrefilter;

        public bool DebugInvariantCyrillicCountUsesDirectSingleLiteralSearch =>
            _owner.DebugInvariantCyrillicCountUsesDirectSingleLiteralSearch;

        public bool DebugHasPackedNibbleLiteralFamilyCountStrategy =>
            _owner.DebugHasPackedNibbleLiteralFamilyCountStrategy;

        public bool DebugPrioritizesBoundaryLiteralFamilyCount =>
            _owner.DebugPrioritizesBoundaryLiteralFamilyCount;

        public bool DebugHasFusedAsciiBoundaryLiteralFamilyCount =>
            _owner.DebugHasFusedAsciiBoundaryLiteralFamilyCount;

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

        public string DebugSelectedCountKernelKind => _owner.DebugSelectedCountKernelKind;

        public bool DebugTryCountSelectedKernelWithoutValidation(ReadOnlySpan<byte> input, out int count) =>
            _owner.DebugTryCountSelectedKernelWithoutValidation(input, out count);

        public bool DebugTryGetSelectedCountKernelMetrics(
            ReadOnlySpan<byte> input,
            out Utf8SelectedCountKernelMetrics metrics) =>
            _owner.DebugTryGetSelectedCountKernelMetrics(input, out metrics);

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

        public bool DebugTryIsMatchLiteralFamily(ReadOnlySpan<byte> input, out bool isMatch) =>
            _owner.DebugTryIsMatchLiteralFamily(input, out isMatch);

        public bool DebugTryIsMatchExactLiteral(ReadOnlySpan<byte> input, out bool isMatch) =>
            _owner.DebugTryIsMatchExactLiteral(input, out isMatch);

        public bool DebugTryMatchExactLiteral(ReadOnlySpan<byte> input, out Utf8ValueMatch match) =>
            _owner.DebugTryMatchExactLiteral(input, out match);

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

        public bool DebugUsesEmittedWholeMatcher =>
            _owner.DebugUsesEmittedWholeMatcher;

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

        public bool DebugTryMatchEmittedWhole(
            ReadOnlySpan<byte> input,
            out int matchedLength)
        {
            if (_owner._compiledEngineRuntime is Utf8SimplePatternCompiledEngineRuntime compiledRuntime)
            {
                return compiledRuntime.TryMatchEmittedWhole(input, out matchedLength);
            }

            matchedLength = 0;
            return false;
        }

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

        public int DebugReplaceViaFallback(ReadOnlySpan<byte> input, string replacement) =>
            _owner.DebugReplaceViaFallback(input, replacement);

        public int DebugReplaceViaNativeTextOperations(
            ReadOnlySpan<byte> input,
            string replacementText) =>
            _owner.DebugReplaceViaNativeTextOperations(input, replacementText);

        public string DebugLiteralFamilyCaptureMapSummary() =>
            _owner.DebugLiteralFamilyCaptureMapSummary();

        public bool DebugFallbackRegexIsCreated => _owner.DebugFallbackRegexIsCreated;

        public bool DebugAnchoredFallbackRegexIsCreated => _owner.DebugAnchoredFallbackRegexIsCreated;

        public int DebugReplacementCacheEntryCount => _owner.DebugReplacementCacheEntryCount;
    }
}
