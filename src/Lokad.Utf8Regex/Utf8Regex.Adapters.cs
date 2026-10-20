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
    internal bool TryMatchDirectWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
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

    internal bool TryIsMatchDirectWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
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

    internal bool TryMatchAnchoredHeadTailWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
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

    internal bool TryIsMatchAnchoredHeadTailWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
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
        // Anchored suffixes match at most once at a fixed end position: a start past
        // it misses definitively, otherwise the direct hit itself decides.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return TryMatchDirectFallbackFamilyWithoutValidation(input.Bytes, out var suffixAdapterIsMatch) &&
                suffixAdapterIsMatch.Success &&
                input.Project(start).Value <= suffixAdapterIsMatch.IndexInUtf16;
        }

        // Anchored prefixes anchor to byte 0: nonzero starts miss definitively,
        // offset 0 reuses the byte-exact prefix compare without BCL construction.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            return start.Value == 0 &&
                Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input.Bytes, _fallbackDirectFamily, out _);
        }

        // General scalar programs preserve the whole subject's anchor context.
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex)
        {
            try
            {
                return OwnedSliceIsMatchFromByte(input.Bytes, _ownedSliceProgram, start.Value, CancellationToken.None);
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input.Bytes);
            }
        }

        // R05: line-anchored ASCII discovery from the start byte runs owned through the
        // shared interpreter; misses are definitive without BCL verification.
        if (CanServeLineAnchoredOwned(input.Bytes) && input.Validation.IsAscii)
        {
            try
            {
                var offsetBudget = CreateExecutionBudget();
                return FindNextSimplePatternViaInterpreter(input.Bytes, start.Value, offsetBudget, out _) >= 0;
            }
            catch (Utf8ExecutionDeadlineExpiredException)
            {
                throw CreateMatchTimeoutException(input.Bytes);
            }
        }

        try
        {
            return OwnedSliceIsMatchFromByte(input.Bytes,
                _ownedSliceProgram,
                start.Value, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input.Bytes);
        }
    }

    private int CountAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return TryMatchDirectFallbackFamilyWithoutValidation(input.Bytes, out var suffixAdapterCount) &&
                suffixAdapterCount.Success &&
                input.Project(start).Value <= suffixAdapterCount.IndexInUtf16
                ? 1
                : 0;
        }

        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } adapterCountLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (start.Value != 0)
            {
                return 0;
            }

            return adapterCountLiteral.Length <= input.Bytes.Length &&
                input.Bytes[..adapterCountLiteral.Length].SequenceEqual(adapterCountLiteral)
                ? 1
                : 0;
        }

        try
        {
            return OwnedSliceCountFromByte(input.Bytes, _ownedSliceProgram, start.Value, CancellationToken.None);
        }
        catch (Utf8ExecutionDeadlineExpiredException)
        {
            throw CreateMatchTimeoutException(input.Bytes);
        }
    }

    private int CountPreparedAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        var remaining = input.Bytes[start.Value..];
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex || ShouldUseKelvinSignFallback(remaining))
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
        // Anchored suffixes yield at most one match at the fixed end position
        // through the anchor-enforcing owned cursor; a start past it misses.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchDirectFallbackFamilyWithoutValidation(input.Bytes, out var suffixAdapterEnumerate) &&
                suffixAdapterEnumerate.Success &&
                input.Project(start).Value <= suffixAdapterEnumerate.IndexInUtf16)
            {
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input.Bytes, _fallbackDirectFamily.LiteralUtf8, CreateExecutionBudget(), _fallbackDirectFamily.Kind))
                    .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
            }

            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input.Bytes[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
        }

        // Anchored prefixes yield at most one match at byte 0 through the
        // anchor-enforcing owned cursor; nonzero starts are a definitive miss.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } adapterEnumerateLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (start.Value != 0)
            {
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input.Bytes[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                    .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
            }

            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input.Bytes, adapterEnumerateLiteral, CreateExecutionBudget()))
                .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
        }

        // Folded anchored prefixes enumerate the same single anchored hit with per-byte
        // invariant folding; nonzero starts miss definitively. Kelvin and Turkic
        // divergences fall through to the fenced BCL paths below.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 } foldedAdapterEnumerateLiteral)
        {
            if (!ShouldUseKelvinSignFallback(input.Bytes))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
                if (start.Value != 0)
                {
                    return new Utf8ValueMatchEnumerator(
                        new Utf8OperationMatchCursor(input.Bytes[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                        .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
                }

                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input.Bytes, foldedAdapterEnumerateLiteral, CreateExecutionBudget(), _fallbackDirectFamily.Kind))
                    .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
            }
        }

        // Folded anchored suffixes enumerate the same single tail hit with per-byte
        // invariant folding under the same fence contract; the suffix kernel projects
        // coordinates.
        if ((_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            if (!ShouldUseKelvinSignFallback(input.Bytes))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                if (TryMatchDirectFallbackFamilyWithoutValidation(input.Bytes, out var foldedSuffixAdapterEnumerate) &&
                    foldedSuffixAdapterEnumerate.Success &&
                    input.Project(start).Value <= foldedSuffixAdapterEnumerate.IndexInUtf16)
                {
                    return new Utf8ValueMatchEnumerator(
                        new Utf8OperationMatchCursor(input.Bytes, _fallbackDirectFamily.LiteralUtf8, CreateExecutionBudget(), _fallbackDirectFamily.Kind))
                        .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
                }

                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input.Bytes[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                    .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
            }
        }

        // R05: line-anchored ASCII enumeration from a nonzero start scans the full input
        // from the start byte through the floored owned cursor; anchors keep their true left
        // context, unlike suffix slicing which diverts below.
        if (start.Value != 0 && CanServeLineAnchoredOwned(input.Bytes) && input.Validation.IsAscii)
        {
            var floorBudget = CreateExecutionBudget();
            var floorCursor = Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(
                _preparedRegex,
                input.Bytes,
                input.Validation,
                floorBudget,
                start.Value,
                input.Project(start).Value);
            return new Utf8ValueMatchEnumerator(floorCursor).WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
        }

        // Adapter enumeration rides the owned slice from the start unit on
        // full input (anchors keep true left context and \\G pins to the scan
        // start exactly like BCL startat, unlike suffix slicing); anything
        // unserved keeps the routing below.
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex)
        {
            return OwnedSliceEnumerateMatchesFromUnit(input.Bytes, _ownedSliceProgram, input.BoundaryMap, input.Project(start).Value, CancellationToken.None)
                .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
        }

        var remaining = input.Bytes[start.Value..];
        var literal = _preparedRegex.LiteralUtf8;
        var budget = CreateExecutionBudget();
        // R05: exact literals enumerate right-to-left through the reverse-search
        // cursor above; anchored shapes never reach literal kinds, boundary and
        // trailing requirements use the owned scalar cursor below, empty literals
        // cannot match, non-ASCII literals project through the literal UTF-16 length,
        // and folded shapes stay Kelvin-fenced like the forward cursor.
        // Other right-to-left starts preserve full context in the scalar cursor.
        if (UsesRightToLeft() &&
            start.Value == input.Bytes.Length &&
            _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase &&
            literal is { Length: > 0 } &&
            !_preparedRegex.SearchPlan.HasBoundaryRequirements &&
            !_preparedRegex.SearchPlan.HasTrailingLiteralRequirement &&
            (_preparedRegex.ExecutionKind != NativeExecutionKind.AsciiLiteralIgnoreCase || !ShouldUseKelvinSignFallback(input.Bytes)))
        {
            var rtlCursor = new Utf8OperationMatchCursor(input.Bytes, _preparedRegex, literal, _preparedRegex.ExecutionKind, budget, Utf8InputAnalyzer.IsAscii(input.Bytes), rightToLeft: true);
            return new Utf8ValueMatchEnumerator(rtlCursor).WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
        }

        if (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiOrderedLiteralWindow &&
            !UsesRightToLeft() &&
            Utf8InputAnalyzer.IsAscii(input.Bytes))
        {
            return new Utf8ValueMatchEnumerator(
                Utf8CompiledOperationCursorFactory.CreateMatchCursorFromOffset(
                    _preparedRegex, input.Bytes,
                    new Utf8ValidationResult(input.Bytes.Length, input.Bytes.Length, true, false),
                    budget, start.Value, start.Value))
                .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
        }

        if (UsesRightToLeft() || ShouldUseKelvinSignFallback(remaining) ||
            (start.Value != 0 && RequiresOriginSemanticsForOffsetSearch()))
        {
            return OwnedSliceEnumerateMatchesFromUnit(input.Bytes,
                _ownedSliceProgram,
                input.BoundaryMap, input.Project(start).Value, CancellationToken.None)
                .WithTimeoutMapping(input.Bytes, Pattern, MatchTimeout);
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

            if (_owner._preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex)
            {
                isMatch = _owner.IsMatchAtByteOffset(input, start);
                return true;
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
                isMatch = _owner.IsMatchAtByteOffset(input, start);
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
