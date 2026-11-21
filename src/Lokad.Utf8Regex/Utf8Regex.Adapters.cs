using Lokad.Utf8Regex.Internal.Diagnostics;
using Lokad.Utf8Regex.Internal.Execution;
using Lokad.Utf8Regex.Internal.Input;
using Lokad.Utf8Regex.Internal.Planning;

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

    private Utf8PreparedValueMatchEnumerator EnumeratePreparedMatchesAtByteOffset(
        Utf8ValidatedInput input, Utf8BytePosition start)
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

        public int CountPrepared(Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.CountPreparedAtByteOffset(input, start);

        // PCRE2-INTEGRATION-POINT: flavor-neutral prepared byte-range iteration.
        public Utf8PreparedValueMatchEnumerator EnumeratePreparedMatches(
            Utf8ValidatedInput input, Utf8BytePosition start)
            => _owner.EnumeratePreparedMatchesAtByteOffset(input, start);

        public Utf8SearchPortfolioKind SearchPortfolioKind =>
            _owner._preparedRegex.SearchPlan.PortfolioKind;
    }
}
