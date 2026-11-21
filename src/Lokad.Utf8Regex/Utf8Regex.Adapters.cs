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

// byte-offset adapters and direct probes, split from the matching facade along the operation-adapter ownership boundary.
/// <summary>Byte-offset operation adapters and direct probes for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    internal bool TryMatchDirectWithoutValidation(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {

        if (TryMatchAnchoredLiteral(input, out match))
        {
            return true;
        }

        if (MatchTimeout != Regex.InfiniteMatchTimeout)
        {
            match = default(Utf8CaptureRange);
            return false;
        }

        if (_program.CompiledEngineRuntime is { } runtime)
            return runtime.TryMatchDirect(input, budget: Utf8ExecutionDeadline.Infinite, out match);

        match = default(Utf8CaptureRange);
        return false;
    }

    private int CountAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        if (_anchoredLiteral.IsExactSuffix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            return TryMatchAnchoredLiteral(input.Bytes, out var suffixAdapterCount) &&
                suffixAdapterCount.Success &&
                start.Value <= suffixAdapterCount.StartOffsetInBytes
                ? 1
                : 0;
        }

        if (_anchoredLiteral.Kind == Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } adapterCountLiteral)
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
        if (CompiledEngineKind == Utf8CompiledEngineKind.FallbackRegex || ShouldUseKelvinSignFallback(remaining))
        {
            return CountAtByteOffset(input, start);
        }

        return _compiledEngineRuntime.Count(remaining, CreateExecutionBudget());
    }

    private Utf8ValueMatchEnumerator EnumerateMatchesAtByteOffset(Utf8ValidatedInput input, Utf8BytePosition start)
    {
        // Anchored suffixes yield at most one match at the fixed end position
        // through the anchor-enforcing owned cursor; a start past it misses.
        if (_anchoredLiteral.IsExactSuffix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 })
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
            if (TryMatchAnchoredLiteral(input.Bytes, out var suffixAdapterEnumerate) &&
                suffixAdapterEnumerate.Success &&
                start.Value <= suffixAdapterEnumerate.StartOffsetInBytes)
            {
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input.Bytes, _anchoredLiteral.LiteralUtf8, CreateExecutionBudget(), _anchoredLiteral.Kind))
                    .WithTimeoutMapping(input.Bytes, this);
            }

            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input.Bytes[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                .WithTimeoutMapping(input.Bytes, this);
        }

        // Anchored prefixes yield at most one match at byte 0 through the
        // anchor-enforcing owned cursor; nonzero starts are a definitive miss.
        if (_anchoredLiteral.Kind == Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } adapterEnumerateLiteral)
        {
            Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
            if (start.Value != 0)
            {
                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input.Bytes[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                    .WithTimeoutMapping(input.Bytes, this);
            }

            return new Utf8ValueMatchEnumerator(
                new Utf8OperationMatchCursor(input.Bytes, adapterEnumerateLiteral, CreateExecutionBudget()))
                .WithTimeoutMapping(input.Bytes, this);
        }

        // Folded anchored prefixes enumerate the same single anchored hit with per-byte
        // invariant folding; nonzero starts miss definitively. Kelvin and Turkic
        // divergences fall through to the fenced BCL paths below.
        if (_anchoredLiteral.Kind == Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 } foldedAdapterEnumerateLiteral)
        {
            if (!ShouldUseKelvinSignFallback(input.Bytes))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralPrefix);
                if (start.Value != 0)
                {
                    return new Utf8ValueMatchEnumerator(
                        new Utf8OperationMatchCursor(input.Bytes[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                        .WithTimeoutMapping(input.Bytes, this);
                }

                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input.Bytes, foldedAdapterEnumerateLiteral, CreateExecutionBudget(), _anchoredLiteral.Kind))
                    .WithTimeoutMapping(input.Bytes, this);
            }
        }

        // Folded anchored suffixes enumerate the same single tail hit with per-byte
        // invariant folding under the same fence contract; the suffix kernel projects
        // coordinates.
        if ((_anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd) &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 })
        {
            if (!ShouldUseKelvinSignFallback(input.Bytes))
            {
                Utf8SearchDiagnosticsSession.Current?.MarkExecutionRoute(Utf8ExecutionRoute.FallbackDirectAnchoredAsciiLiteralSuffix);
                if (TryMatchAnchoredLiteral(input.Bytes, out var foldedSuffixAdapterEnumerate) &&
                    foldedSuffixAdapterEnumerate.Success &&
                    start.Value <= foldedSuffixAdapterEnumerate.StartOffsetInBytes)
                {
                    return new Utf8ValueMatchEnumerator(
                        new Utf8OperationMatchCursor(input.Bytes, _anchoredLiteral.LiteralUtf8, CreateExecutionBudget(), _anchoredLiteral.Kind))
                        .WithTimeoutMapping(input.Bytes, this);
                }

                return new Utf8ValueMatchEnumerator(
                    new Utf8OperationMatchCursor(input.Bytes[..0], Array.Empty<byte>(), CreateExecutionBudget()))
                    .WithTimeoutMapping(input.Bytes, this);
            }
        }

        // Match against the full subject so anchors and lookbehind can use
        // the original context at the byte start.
        if (_preparedRegex.ExecutionKind == NativeExecutionKind.FallbackRegex)
        {
            return OwnedSliceEnumerateMatchesFromByte(input.Bytes, _ownedSliceProgram, start.Value, CancellationToken.None)
                .WithTimeoutMapping(input.Bytes, this);
        }

        var remaining = input.Bytes[start.Value..];
        var literal = _preparedRegex.LiteralUtf8;
        var budget = CreateExecutionBudget();
        // The literal reverse cursor starts at the end. Other RTL starts
        // and matches with boundary or tail requirements use the scalar cursor.
        if (UsesRightToLeft() &&
            start.Value == input.Bytes.Length &&
            _preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or NativeExecutionKind.ExactUtf8Literal or NativeExecutionKind.AsciiLiteralIgnoreCase &&
            literal is { Length: > 0 } &&
            !_preparedRegex.SearchPlan.HasBoundaryRequirements &&
            !_preparedRegex.SearchPlan.HasTrailingLiteralRequirement &&
            (_preparedRegex.ExecutionKind != NativeExecutionKind.AsciiLiteralIgnoreCase || !ShouldUseKelvinSignFallback(input.Bytes)))
        {
            var rtlCursor = new Utf8OperationMatchCursor(input.Bytes, _preparedRegex, literal, _preparedRegex.ExecutionKind, budget, rightToLeft: true);
            return new Utf8ValueMatchEnumerator(rtlCursor).WithTimeoutMapping(input.Bytes, this);
        }

        if (UsesRightToLeft() || ShouldUseKelvinSignFallback(remaining) ||
            (start.Value != 0 && RequiresOriginSemanticsForOffsetSearch()))
        {
            return OwnedSliceEnumerateMatchesFromByte(input.Bytes,
                _ownedSliceProgram, start.Value, CancellationToken.None)
                .WithTimeoutMapping(input.Bytes, this);
        }

        return new Utf8ValueMatchEnumerator(Utf8CompiledOperationCursorFactory.CreateMatchCursor(
                _preparedRegex, remaining, budget))
            .WithBaseOffset(start.Value);
    }

    private Utf8PreparedValueMatchEnumerator EnumeratePreparedMatchesAtByteOffset(
        Utf8ValidatedInput input, Utf8BytePosition start)
    {
        return new Utf8PreparedValueMatchEnumerator(input.Bytes, _preparedRegex.SearchPlan.PreparedSearcher, start);
    }

    // Keep the original origin and prefix for anchors, boundaries and lookbehind.
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

        public int CountPrepared(Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.CountPreparedAtByteOffset(input, start);

        public Utf8ValueMatchEnumerator EnumerateMatches(Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.EnumerateMatchesAtByteOffset(input, start);

        // PCRE2-INTEGRATION-POINT: flavor-neutral prepared byte-range iteration.
        public Utf8PreparedValueMatchEnumerator EnumeratePreparedMatches(
            Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.EnumeratePreparedMatchesAtByteOffset(input, start);

        public Utf8SearchPortfolioKind SearchPortfolioKind =>
            _owner._preparedRegex.SearchPlan.PortfolioKind;
    }
}
