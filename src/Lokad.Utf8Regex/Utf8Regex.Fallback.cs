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

    private bool TryMatchDirectFallbackFamilyWithoutValidation(ReadOnlySpan<byte> input, out Utf8CaptureRange match)
    {
        match = default(Utf8CaptureRange);
        // Anchored ASCII literal prefixes are byte-exact on every input: astral scalars
        // never start with ASCII bytes, so non-ASCII input stays direct rather than
        // requiring the general owned scalar route.
        // Folded prefixes compare per byte under invariant folding (Kelvin and Turkic
        // divergences stay fenced upstream); the executor switches on the kind.
        if (_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            if (Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredPrefixLength))
            {
                match = new Utf8CaptureRange(0, 0 + (anchoredPrefixLength));
            }

            return true;
        }

        // Anchored ASCII literal suffixes are byte-exact on every input, just like
        // prefixes: astral scalars never end with ASCII bytes. Unlike prefixes,
        // the match index needs UTF-16 mapping past non-ASCII content (which also
        // validates the prefix, matching the invalid-input contract on hits).
        if ((Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) ||
            _fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd) &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            if (Utf8AsciiAnchoredSuffixFamilyExecutor.TryMatchSuffix(input, _fallbackDirectFamily, out var suffixByteIndex, out var suffixLength))
            {

                match = new Utf8CaptureRange(suffixByteIndex, (suffixByteIndex) + (suffixLength));
            }

            return true;
        }

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

            match = new Utf8CaptureRange(0, 0 + (signedDecimalLength));
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
            if (directResult == Utf8DirectMatchResult.NeedsScalarFallback)
            {
                return false;
            }

            if (directResult == Utf8DirectMatchResult.Match && tokenMatchedLength > 0)
            {
                match = new Utf8CaptureRange(tokenMatchIndex, (tokenMatchIndex) + (tokenMatchedLength));
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
            match = new Utf8CaptureRange(matchIndex, (matchIndex) + (matchedLength));
        }

        return true;
    }

    private bool TryIsMatchDirectFallbackFamilyWithoutValidation(ReadOnlySpan<byte> input, out bool isMatch)
    {
        isMatch = false;
        // Same byte-exact contract as the Match arm above: anchored ASCII prefixes
        // stay direct even on non-ASCII input. Folded prefixes ride
        // the same arm; the executor switches on the kind.
        if (_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix)
        {
            isMatch = Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out _);
            return true;
        }

        // Same byte-exact contract for suffixes: a tail miss is definitive on
        // every input, and IsMatch needs no coordinates.
        if (Utf8FallbackDirectFamilyCategories.IsAnchoredSuffixMatchFamily(_fallbackDirectFamily.Kind) ||
            _fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralSuffixStrictEnd)
        {
            isMatch = Utf8AsciiAnchoredSuffixFamilyExecutor.TryMatchSuffix(input, _fallbackDirectFamily, out _, out _);
            return true;
        }

        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole)
        {
            if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
            {
                return false;
            }

            isMatch = Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input, out _);
            return true;
        }

        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral &&
            TryIsMatchPrioritizedBooleanFamilyWithoutValidation(input, out isMatch))
        {
            return true;
        }

        if (_hasDirectFallbackTokenFamilyWithoutValidation)
        {
            var directResult = Utf8AsciiTokenFamilyExecutor.TryIsMatchWithoutValidation(
                input,
                0,
                _fallbackDirectFamily);
            if (directResult == Utf8DirectMatchResult.NeedsScalarFallback)
            {
                return false;
            }

            isMatch = directResult == Utf8DirectMatchResult.Match;
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

        isMatch = Utf8AsciiDirectFamilyExecutor.TryFindMatch(
            input,
            _fallbackDirectFamily,
            default,
            default,
            out _,
            out _);
        return true;
    }

    private bool TryIsMatchPrioritizedBooleanFamilyWithoutValidation(
        ReadOnlySpan<byte> input, out bool isMatch)
    {
        isMatch = false;
        switch (_prioritizedBooleanFamilyKind)
        {
            case Utf8PrioritizedBooleanFamilyKind.LeadingAnyRunTrailingAsciiLiteral:
                if (_fallbackDirectFamily.LiteralUtf8 is not { Length: > 0 } trailingLiteralUtf8 ||
                    input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
                {
                    return false;
                }

                isMatch = Utf8AsciiLeadingAnyRunTrailingLiteralExecutor.IsMatch(input, trailingLiteralUtf8);
                return true;

            case Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiLeadingDigitsTail:
                if (Utf8AsciiLeadingDigitsTailExecutor.TryMatchPrintableAsciiWhole(
                    input,
                    _fallbackDirectFamily.LiteralUtf8,
                    out isMatch))
                {
                    return true;
                }

                return false;

            case Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiSignedDecimalWhole:
                if (Utf8AsciiPrefixTokenExecutor.TryMatchSignedDecimalWhole(input, out _))
                {
                    isMatch = true;
                    return true;
                }

                return input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) < 0;

            case Utf8PrioritizedBooleanFamilyKind.AsciiUriToken:
                var uriMatched = Utf8AsciiUriTokenExecutor.TryFindAsciiUriToken(
                    input,
                    0,
                    out var uriMatchIndex,
                    out var uriMatchedLength);

                if (uriMatched && uriMatchIndex == 0 && uriMatchedLength == input.Length)
                {
                    isMatch = true;
                    return true;
                }

                if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
                {
                    return false;
                }

                isMatch = uriMatched;
                return true;

            case Utf8PrioritizedBooleanFamilyKind.AsciiIpv4Token:
                var ipv4Matched = Utf8AsciiIpv4TokenExecutor.TryFindAsciiIpv4Token(
                    input,
                    0,
                    out var ipv4MatchIndex,
                    out var ipv4MatchedLength);

                if (ipv4Matched &&
                    ipv4MatchIndex == 0 &&
                    (ipv4MatchedLength == input.Length ||
                     ipv4MatchedLength == input.Length - 1 && input[^1] < 0x80))
                {
                    isMatch = true;
                    return true;
                }

                if (input.IndexOfAnyInRange((byte)0x80, byte.MaxValue) >= 0)
                {
                    return false;
                }

                isMatch = ipv4Matched;
                return true;

            default:
                return false;
        }
    }

    private bool ShouldSkipRequiredPrefilterForCount()
    {
        // The ASCII relation finder already seeks quotes and verifies operands.
        // Its new necessary-family filter serves owned scalar calls instead;
        // adding another scan to this native route only duplicates discovery.
        return _preparedRegex.StructuralLinearProgram.QuotedRelationPlan.HasValue;
    }

    private bool ShouldSkipRequiredPrefilterForMatch()
    {
        return _preparedRegex.StructuralLinearProgram.QuotedRelationPlan.HasValue ||
            _compiledEngineRuntime.SkipRequiredPrefilterForMatch;
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

    // Native replacement splicers require complete scalar endpoints. Mixed
    // subjects outside these qualified shapes use the cached scalar program.
    private bool CanUseScalarAlignedNativeReplacement(ReadOnlySpan<byte> input)
    {
        if (input.IndexOfAnyInRange((byte)0xF0, byte.MaxValue) < 0)
        {
            return true;
        }

        if (_groupedLiteralSpanTable is not null)
        {
            return true;
        }

        if (_preparedRegex.ExecutionKind is NativeExecutionKind.ExactAsciiLiteral or
            NativeExecutionKind.ExactUtf8Literal or
            NativeExecutionKind.ExactUtf8Literals or
            NativeExecutionKind.AsciiLiteralIgnoreCase or
            NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals)
        {
            return true;
        }

        return false;
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

    private bool CanGuideFallbackVerification()
    {
        return _preparedRegex.SearchPlan.HasFallbackCandidates;
    }
}
