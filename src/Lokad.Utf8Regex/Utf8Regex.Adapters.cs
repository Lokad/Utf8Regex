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

// PLAN.md D09: byte-offset adapters and direct probes, split from the matching facade along the operation-adapter ownership boundary.
/// <summary>Byte-offset operation adapters and direct probes for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
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

        if (MatchTimeout != Regex.InfiniteMatchTimeout)
        {
            match = Utf8ValueMatch.NoMatch;
            return false;
        }

        return _compiledEngineRuntime.TryMatchWithoutValidation(input, budget: Utf8ExecutionDeadline.Infinite, out match);
    }

    private bool TryIsMatchDirectWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        var prioritizeFallbackFamily = _hasDirectFallbackTokenFamilyWithoutValidation ||
            _fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral;
        if (prioritizeFallbackFamily &&
            TryIsMatchDirectFallbackFamilyWithoutValidation(input, out isMatch))
        {
            return true;
        }

        if (TryIsMatchAnchoredHeadTailWithoutValidation(input, out isMatch))
        {
            return true;
        }

        if (TryIsMatchAsciiSimplePatternWithoutValidation(input, out isMatch))
        {
            return true;
        }

        if (!prioritizeFallbackFamily &&
            TryIsMatchDirectFallbackFamilyWithoutValidation(input, out isMatch))
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
        if (RequiresCultureSensitiveFallback || ShouldUseKelvinSignFallback(remaining))
        {
            return CountAtByteOffset(input, start);
        }

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
        if (RequiresCultureSensitiveFallback || UsesRightToLeft() || ShouldUseKelvinSignFallback(remaining) ||
            (start.Value != 0 && RequiresOriginSemanticsForOffsetSearch()))
        {
            var startInUtf16 = input.Project(start);
            return new Utf8ValueMatchEnumerator(
                input.Bytes,
                _verifierRuntime.FallbackCandidateVerifier.FallbackRegex,
                input.GetDecodedString(),
                startInUtf16.Value,
                input.Utf16ProjectionMap);
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

    // R03: suffix slicing loses absolute-origin anchors and left context (word boundaries,
    // lookbehind). From a nonzero start those shapes keep full-input BCL verification;
    // multiline-anchored patterns divert conservatively, while \G anchors at the search
    // start exactly like BCL startat and stay native.
    private bool RequiresOriginSemanticsForOffsetSearch()
    {
        return _preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex ||
            _preparedRegex.Features.HasAbsoluteStartAnchor ||
            _preparedRegex.Features.HasWordBoundary ||
            _preparedRegex.Features.HasLookbehind;
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

        // PCRE2-INTEGRATION-POINT: flavor-neutral definitive prepared boolean kernels.
        public bool TryIsMatchPrepared(
            Utf8ValidatedInput input,
            Utf8BytePosition start,
            out bool isMatch)
        {
            if (start.Value != 0)
            {
                isMatch = false;
                return false;
            }

            var bytes = input.Bytes;
            if (_owner.TryIsMatchPrioritizedBooleanFamilyWithoutValidation(bytes, out isMatch) ||
                _owner.TryIsMatchDirectWithoutValidation(bytes, out isMatch))
            {
                return true;
            }

            if (_owner.TryMatchDirectWithoutValidation(bytes, out var match))
            {
                isMatch = match.Success;
                return true;
            }

            if (_owner.ShouldFallbackForTrailingNewlineAnchoredValidator(bytes, input.Validation))
            {
                isMatch = _owner._verifierRuntime.FallbackCandidateVerifier.FallbackRegex.IsMatch(
                    input.GetDecodedString());
                return true;
            }

            if (!_owner.ShouldSkipRequiredPrefilterForMatch() &&
                _owner.RejectsByRequiredPrefilter(bytes))
            {
                isMatch = false;
                return true;
            }

            isMatch = _owner.IsMatchViaCompiledEngine(
                bytes,
                input.Validation,
                Utf8ExecutionDeadline.Infinite);
            return true;
        }

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

        // PCRE2-INTEGRATION-POINT: flavor-neutral prepared byte-range iteration.
        public Utf8PreparedValueMatchEnumerator EnumeratePreparedMatches(
            Utf8ValidatedInput input,
            Utf8BytePosition start)
            => _owner.EnumeratePreparedMatchesAtByteOffset(input, start);

        public Utf8SearchPortfolioKind SearchPortfolioKind =>
            _owner._preparedRegex.SearchPlan.PortfolioKind;
    }
}
