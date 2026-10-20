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

    private bool DebugRejectsByRequiredPrefilter(ReadOnlySpan<byte> input)
    {
        return RejectsByRequiredPrefilter(input);
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





    private Utf8ValueMatch DebugMatchViaCompiledEngine(ReadOnlySpan<byte> input, Utf8ValidationResult validation)
    {
        if (RejectsByRequiredPrefilter(input))
        {
            return Utf8ValueMatch.NoMatch;
        }

        return MatchViaCompiledEngine(input, validation, budget: Utf8ExecutionDeadline.Infinite);
    }

    private bool DebugCanUseFusedCompiledUtf8LiteralCount => CanUseFusedCompiledUtf8LiteralCount();

    private bool DebugCanUseFusedCompiledUtf8LiteralFamilyCount => CanUseFusedCompiledUtf8LiteralFamilyCount();

    private bool DebugCreatedExecutionBudgetIsNull => CreateExecutionBudget().IsInfinite;

    private int DebugCountViaCompiledEngineWithCreatedBudget(ReadOnlySpan<byte> input)
    {
        return CountViaCompiledEngine(input, default, CreateExecutionBudget());
    }

    private bool DebugSupportsWellFormedOnlyMatch => _compiledEngineRuntime.SupportsWellFormedOnlyMatch;

    private bool DebugWellFormedOnlyMatchMissIsDefinitive => _compiledEngineRuntime.WellFormedOnlyMatchMissIsDefinitive;

    private bool DebugSupportsThrowIfInvalidOnlyCount => _compiledEngineRuntime.SupportsThrowIfInvalidOnlyCount;

    private bool DebugUsesEmittedWholeMatcher => _compiledEngineRuntime.UsesEmittedWholeMatcher;

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


    internal readonly struct Utf8RegexInspection
    {
        private readonly Utf8Regex _owner;

        public Utf8RegexInspection(Utf8Regex owner)
        {
            _owner = owner;
        }

        public NativeExecutionKind ExecutionKind => _owner.ExecutionKind;

        public bool DebugUsesOwnedSliceProgram => _owner._preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex;

        internal Utf8SliceProgram? OwnedSliceProgram => DebugUsesOwnedSliceProgram ? _owner._ownedSliceProgram : null;

        // Profile adapters can use the admitted scalar program even when the
        // core's default route selects a native plan for this pattern.
        internal Utf8SliceProgram AvailableOwnedSliceProgram => _owner._ownedSliceProgram;

        public Utf8PreparedRegex PreparedRegex => _owner.PreparedRegex;

        public Utf8SearchPlan SearchPlan => _owner.SearchPlan;

        public Utf8StructuralSearchPlan StructuralSearchPlan => _owner.StructuralSearchPlan;

        public Utf8SearchPortfolioKind SearchPortfolioKind => _owner.SearchPortfolioKind;

        public Utf8CompiledEngineKind CompiledEngineKind => _owner.CompiledEngineKind;

        // Friend tooling borrows the existing runtime; no replay or extra owner
        // is retained in the shipped matcher.
        internal Utf8CompiledEngineRuntime CompiledRuntime => _owner._compiledEngineRuntime;

        public Utf8CompiledExecutionBackend CompiledExecutionBackend => _owner.CompiledExecutionBackend;

        public string DebugCompiledEngineRuntimeType => _owner.DebugCompiledEngineRuntimeType;

        public bool DebugCanLowerEmittedKernel => _owner.DebugCanLowerEmittedKernel;

        public string DebugLoweredEmittedKernelKind => _owner.DebugLoweredEmittedKernelKind;

        public bool DebugUsesEmittedKernelMatcher => _owner.DebugUsesEmittedKernelMatcher;

        public string DebugFallbackDirectFamilyKind => _owner.DebugFallbackDirectFamilyKind;

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

        public Utf8StructuralLinearProgramKind StructuralLinearProgramKind =>
            _owner.StructuralLinearProgramKind;

        public Utf8StructuralVerifierPlan StructuralVerifierPlan => _owner.StructuralVerifierPlan;

        public AsciiStructuralIdentifierFamilyPlan StructuralIdentifierFamilyPlan =>
            _owner.StructuralIdentifierFamilyPlan;

        public AsciiSimplePatternPlan SimplePatternPlan => _owner.SimplePatternPlan;

        public string? FallbackReason => _owner.FallbackReason;

        public bool DebugRejectsByRequiredPrefilter(ReadOnlySpan<byte> input) =>
            _owner.DebugRejectsByRequiredPrefilter(input);

        public int DebugCountViaCompiledEngine(ReadOnlySpan<byte> input) =>
            _owner.DebugCountViaCompiledEngine(input);

        public string DebugSelectedCountKernelKind => _owner.DebugSelectedCountKernelKind;

        public bool DebugTryCountSelectedKernelWithoutValidation(ReadOnlySpan<byte> input, out int count) =>
            _owner.DebugTryCountSelectedKernelWithoutValidation(input, out count);

        public Utf8ValueMatch DebugMatchViaCompiledEngine(
            ReadOnlySpan<byte> input,
            Utf8ValidationResult validation) =>
            _owner.DebugMatchViaCompiledEngine(input, validation);

        public bool DebugCanUseFusedCompiledUtf8LiteralCount =>
            _owner.DebugCanUseFusedCompiledUtf8LiteralCount;

        public bool DebugCanUseFusedCompiledUtf8LiteralFamilyCount =>
            _owner.DebugCanUseFusedCompiledUtf8LiteralFamilyCount;

        public bool DebugCreatedExecutionBudgetIsNull => _owner.DebugCreatedExecutionBudgetIsNull;

        public int DebugCountViaCompiledEngineWithCreatedBudget(ReadOnlySpan<byte> input) =>
            _owner.DebugCountViaCompiledEngineWithCreatedBudget(input);

        public bool DebugSupportsWellFormedOnlyMatch => _owner.DebugSupportsWellFormedOnlyMatch;

        public bool DebugWellFormedOnlyMatchMissIsDefinitive =>
            _owner.DebugWellFormedOnlyMatchMissIsDefinitive;

        public bool DebugSupportsThrowIfInvalidOnlyCount =>
            _owner.DebugSupportsThrowIfInvalidOnlyCount;

        public bool DebugUsesEmittedWholeMatcher =>
            _owner.DebugUsesEmittedWholeMatcher;

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

        public int DebugReplaceViaNativeTextOperations(
            ReadOnlySpan<byte> input,
            string replacementText) =>
            _owner.DebugReplaceViaNativeTextOperations(input, replacementText);




        public int DebugReplacementCacheEntryCount => _owner.DebugReplacementCacheEntryCount;
    }
}
