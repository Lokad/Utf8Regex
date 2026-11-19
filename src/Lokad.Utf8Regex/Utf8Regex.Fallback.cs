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

namespace Lokad.Utf8Regex;

// scalar fallback execution and native verification, split from the matching facade.
/// <summary>Scalar fallback execution and native verification for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    private Utf8CaptureRange MatchScalarProgram(ReadOnlySpan<byte> input, CancellationToken token)
        => OwnedSliceMatch(input, _ownedSliceProgram, token);

    private Utf8CaptureRange MatchFallback(ReadOnlySpan<byte> input)
    {
        if (RejectsByRequiredPrefilter(input)) return default(Utf8CaptureRange);
        return MatchScalarProgram(input, CancellationToken.None);
    }

    private bool RejectsByRequiredPrefilter(ReadOnlySpan<byte> input)
    {
        return Utf8PrefilterExecutor.Rejects(_preparedRegex.SearchPlan.PrefilterPlan, input);
    }

    private bool TryMatchAnchoredLiteral(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        match = default(Utf8CaptureRange);
        // Anchored ASCII literal prefixes are byte-exact on every input: astral scalars
        // never start with ASCII bytes, so non-ASCII input stays direct rather than
        // requiring the general owned scalar route.
        // Folded prefixes compare per byte under invariant folding (Kelvin and Turkic
        // divergences stay fenced upstream); the executor switches on the kind.
        if (_anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 })
        {
            if (Utf8AnchoredLiteralPrefixExecutor.TryMatchPrefix(input, _anchoredLiteral, out var anchoredPrefixLength))
            {
                match = new Utf8CaptureRange(0, 0 + (anchoredPrefixLength));
            }

            return true;
        }

        // Anchored ASCII literal suffixes are byte-exact on every input, just like
        // prefixes: UTF-8 scalar encodings never end with ASCII bytes. Both
        // operations return byte ranges without describing the subject prefix.
        if ((_anchoredLiteral.IsExactSuffix ||
            _anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd) &&
            _anchoredLiteral.LiteralUtf8 is { Length: > 0 })
        {
            if (Utf8AnchoredLiteralSuffixExecutor.TryMatchSuffix(input, _anchoredLiteral, out var suffixByteIndex, out var suffixLength))
            {

                match = new Utf8CaptureRange(suffixByteIndex, (suffixByteIndex) + (suffixLength));
            }

            return true;
        }
        return false;
    }

    internal bool TryIsMatchAnchoredLiteral(ReadOnlySpan<byte> input, out bool isMatch)
    {
        isMatch = false;
        // Same byte-exact contract as the Match arm above: anchored ASCII prefixes
        // stay direct even on non-ASCII input. Folded prefixes ride
        // the same arm; the executor switches on the kind.
        if (_anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredAsciiLiteralPrefix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralPrefix)
        {
            isMatch = Utf8AnchoredLiteralPrefixExecutor.TryMatchPrefix(input, _anchoredLiteral, out _);
            return true;
        }

        // Same byte-exact contract for suffixes: a tail miss is definitive on
        // every input, and IsMatch needs no coordinates.
        if (_anchoredLiteral.IsExactSuffix ||
            _anchoredLiteral.Kind is Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffix or Utf8AnchoredLiteralKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd)
        {
            isMatch = Utf8AnchoredLiteralSuffixExecutor.TryMatchSuffix(input, _anchoredLiteral, out _, out _);
            return true;
        }
        return false;
    }

    private bool RequiresScalarValueExecution()
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

    private bool ShouldUseKelvinSignFallback(ReadOnlySpan<byte> input)
        => _requiresKelvinSignFallback && ContainsKelvinSign(input);

    private static bool ContainsKelvinSign(ReadOnlySpan<byte> input)
        => input.IndexOf("K"u8) >= 0;

    private static bool RequiresKelvinSignFallback(Utf8PreparedRegex preparedRegex)
    {

        if (preparedRegex.ExecutionKind == NativeExecutionKind.AsciiLiteralIgnoreCase)
        {
            return ContainsAsciiK(preparedRegex.LiteralUtf8);
        }

        if (preparedRegex.ExecutionKind != NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals)
        {
            return false;
        }

        foreach (var literal in preparedRegex.SearchPlan.AlternateLiteralsUtf8 ?? [])
        {
            if (ContainsAsciiK(literal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsAsciiK(byte[]? literal)
        => literal is not null && literal.AsSpan().IndexOfAny((byte)'K', (byte)'k') >= 0;
}
