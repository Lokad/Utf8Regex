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

    private bool ShouldUseKelvinSignFallback(ReadOnlySpan<byte> input)
        => _requiresKelvinSignFallback && ContainsKelvinSign(input);

    private const int MaximumFoldedSearchLiteralLength = 64;

    // One-shot calls without interruption or resource controls can look for
    // both literal starts and Kelvin aliases in the same byte scan.
    private bool TryResolveFoldedLiteralMatch(ReadOnlySpan<byte> input, CancellationToken token, out Utf8CaptureRange match)
    {
        match = default;
        if (token.CanBeCanceled || MatchTimeout != Regex.InfiniteMatchTimeout ||
            _hasCaptures || _anchoredLiteral.HasValue || UsesRightToLeft() ||
            (Options & RegexOptions.CultureInvariant) == 0 ||
            Utf8SearchDiagnosticsSession.Current?.SliceCheckpointHook is not null)
            return false;

        ref readonly var plan = ref _preparedRegex.SearchPlan;
        if (plan is not { Kind: Utf8SearchKind.AsciiFoldedByteLiteral, HasBoundaryRequirements: false,
                HasTrailingLiteralRequirement: false, LiteralUtf8: { Length: > 0 and <= MaximumFoldedSearchLiteralLength } literal })
            return false;

        var kelvin = "K"u8;
        var kelvinLead = kelvin[0];
        var first = literal[0];
        var other = (uint)((first | 0x20) - 'a') <= 'z' - 'a' ? (byte)(first ^ 0x20) : first;
        var lastStart = input.Length - literal.Length;
        var found = -1;
        for (var floor = 0; floor < input.Length;)
        {
            var offset = input[floor..].IndexOfAny(first, other, kelvinLead);
            if (offset < 0) break;
            var start = floor + offset;
            if (input[start] == kelvinLead)
            {
                if (!input[start..].StartsWith(kelvin) || GetOwnedSliceProgram(ReadOnlySpan<byte>.Empty) is not null)
                    return false;
                // A failed ASCII candidate can still match across this first
                // Kelvin sign. All scalars before it in such a match are ASCII.
                var resume = Math.Max(0, start - literal.Length + 1);
                while ((input[resume] & 0xc0) == 0x80) resume++;
                match = OwnedSliceMatchFromByte(input, _ownedSliceProgram, resume, token);
                return true;
            }
            if (start <= lastStart && System.Text.Ascii.EqualsIgnoreCase(input.Slice(start, literal.Length), literal))
            {
                found = start;
                break;
            }
            floor = start + 1;
        }

        // An earlier match consumes the same number of scalars. If it
        // overlaps the confirmed ASCII hit, its Kelvin alias must precede it.
        if (GetOwnedSliceProgram(ReadOnlySpan<byte>.Empty) is not null) return false;
        if (found >= 0) match = new Utf8CaptureRange(found, found + literal.Length);
        return true;
    }

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
