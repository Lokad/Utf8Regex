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

// PLAN.md D09: managed fallback execution, split from the matching facade along the fallback ownership boundary.
/// <summary>Managed fallback execution for <see cref="Utf8Regex"/>.</summary>
public sealed partial class Utf8Regex
{
    private Utf8ValueMatch MatchFallback(ReadOnlySpan<byte> input) => MatchFallback(input, null);

    private Utf8ValueMatch MatchSemanticFallback(ReadOnlySpan<byte> input)
    {
        var analysis = Utf8InputAnalyzer.Analyze(input);
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(
            analysis.GetDecodedString());
        return Utf8ProjectionExecutor.ProjectFallbackRegexMatch(input, match, analysis.BoundaryMap);
    }

    private Utf8ValueMatch MatchFallback(ReadOnlySpan<byte> input, Utf8BoundaryMap? boundaryMap)
    {
        if (RejectsByRequiredPrefilter(input))
        {
            return Utf8ValueMatch.NoMatch;
        }

        // Guided verification only tries scalar-boundary starts, so astral inputs
        // need shapes whose matches start at scalar boundaries; everything else
        // verifies through the full BCL decode below.
        // Startat-guided verification re-pins a leading contiguous anchor (\G) to the
        // candidate start, manufacturing hits away from the true scan pin
        // (bounded-span circularity and plain startat alike). Decline the guided branch
        // for proven \G-headed trees; whole-subject BCL below answers with the pin
        // intact. Consuming, branching, and quantified heads end the walk unproven
        // (out of scope for this gate).
        if (!HasLeadingContiguousAnchor(_preparedRegex.ExecutionTree) && CanGuideFallbackVerification() && OwnedMatchStartsAreScalarAligned(input))
        {
            return MatchFallbackViaSearchStarts(input, boundaryMap);
        }

        var decoded = Encoding.UTF8.GetString(input);
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        var map = boundaryMap ?? Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        return Utf8ProjectionExecutor.ProjectFallbackRegexMatch(input, match, map);
        static bool HasLeadingContiguousAnchor(Utf8ExecutionTree? tree)
        {
            return tree?.Root is not null && LeadsWithContiguousAnchor(tree.Root) is true;
        }

        static bool? LeadsWithContiguousAnchor(Utf8ExecutionNode node)
        {
            switch (node.Kind)
            {
                case Utf8ExecutionNodeKind.Start:
                    return true;
                case Utf8ExecutionNodeKind.Concatenate:
                    foreach (var child in node.Children)
                    {
                        var lead = LeadsWithContiguousAnchor(child);
                        if (lead is not null)
                        {
                            return lead;
                        }
                    }
                    return false;
                case Utf8ExecutionNodeKind.Capture:
                case Utf8ExecutionNodeKind.Group:
                case Utf8ExecutionNodeKind.Atomic:
                    return node.Children.Count == 0
                        ? null
                        : node.Children.Count == 1
                            ? LeadsWithContiguousAnchor(node.Children[0])
                            : (bool?)false;
                case Utf8ExecutionNodeKind.Empty:
                case Utf8ExecutionNodeKind.Bol:
                case Utf8ExecutionNodeKind.Beginning:
                case Utf8ExecutionNodeKind.Eol:
                case Utf8ExecutionNodeKind.EndZ:
                case Utf8ExecutionNodeKind.End:
                case Utf8ExecutionNodeKind.Boundary:
                case Utf8ExecutionNodeKind.NonBoundary:
                case Utf8ExecutionNodeKind.ECMABoundary:
                case Utf8ExecutionNodeKind.NonECMABoundary:
                case Utf8ExecutionNodeKind.PositiveLookaround:
                case Utf8ExecutionNodeKind.NegativeLookaround:
                    return null;
                default:
                    return false;
            }
        }
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

        // Whole-match fast paths are definitive only when a miss at offset 0 rules
        // out every substring match. Line-anchored (`(?m:^)`, `(?m:$)`) plans can
        // still match inside the input, so they decline to the validating scan paths.
        if ((_preparedRegex.SimplePatternPlan.IsStartAnchored &&
            !_preparedRegex.SimplePatternPlan.IsAbsoluteStartAnchored) ||
            (_preparedRegex.SimplePatternPlan.IsEndAnchored &&
            !_preparedRegex.SimplePatternPlan.IsAbsoluteEndAnchored))
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

        if (_anchoredOptionalFieldPlan.HasValue)
        {
            var matched = Utf8AsciiAnchoredOptionalFieldExecutor.TryMatchWhole(
                input,
                _anchoredOptionalFieldPlan,
                _allowsTrailingNewlineBeforeEnd,
                out var optionalFieldLength,
                out var optionalFieldNeedsValidation);
            if (optionalFieldNeedsValidation)
            {
                return false;
            }

            if (matched)
            {
                match = new Utf8ValueMatch(true, true, 0, optionalFieldLength, 0, optionalFieldLength);
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

        // Whole-match fast paths are definitive only when a miss at offset 0 rules
        // out every substring match. Line-anchored (`(?m:^)`, `(?m:$)`) plans can
        // still match inside the input, so they decline to the validating scan paths.
        if ((_preparedRegex.SimplePatternPlan.IsStartAnchored &&
            !_preparedRegex.SimplePatternPlan.IsAbsoluteStartAnchored) ||
            (_preparedRegex.SimplePatternPlan.IsEndAnchored &&
            !_preparedRegex.SimplePatternPlan.IsAbsoluteEndAnchored))
        {
            return false;
        }

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

            isMatch = matched || Utf8AsciiRepeatedDigitGroupExecutor.TryFind(
                input,
                repeatedDigitGroupPlan,
                out _,
                out _);
            return true;
        }

        if (!_hasDirectAnchoredValidatorWithoutValidation || _hasDirectAnchoredHeadTailWithoutValidation)
        {
            return false;
        }

        if (TryIsMatchAnchoredBoundedDateWithoutValidation(input, out isMatch))
        {
            return true;
        }

        if (_anchoredOptionalFieldPlan.HasValue)
        {
            var matched = Utf8AsciiAnchoredOptionalFieldExecutor.TryMatchWhole(
                input,
                _anchoredOptionalFieldPlan,
                _allowsTrailingNewlineBeforeEnd,
                out _,
                out var optionalFieldNeedsValidation);
            if (optionalFieldNeedsValidation)
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

    private bool TryIsMatchAnchoredBoundedDateWithoutValidation(
        ReadOnlySpan<byte> input,
        out bool isMatch)
    {
        isMatch = false;
        if (!_anchoredBoundedDatePlan.HasValue)
        {
            return false;
        }

        if ((Options & RegexOptions.Compiled) != 0 &&
            _compiledEngineRuntime is Utf8SimplePatternCompiledEngineRuntime compiledRuntime &&
            compiledRuntime.TryMatchEmittedWhole(input, out _))
        {
            isMatch = true;
            return true;
        }

        var matched = Utf8AsciiBoundedDateTokenExecutor.TryMatchWhole(
            input,
            _anchoredBoundedDatePlan,
            _allowsTrailingNewlineBeforeEnd,
            out _,
            out var needsValidation);
        if (needsValidation)
        {
            return false;
        }

        isMatch = matched;
        return true;
    }

    private bool TryMatchDirectFallbackFamilyWithoutValidation(ReadOnlySpan<byte> input, out Utf8ValueMatch match)
    {
        match = Utf8ValueMatch.NoMatch;
        // Anchored ASCII literal prefixes are byte-exact on every input: astral scalars
        // never start with ASCII bytes, so non-ASCII input stays direct instead of
        // declining to the BCL fallback like the other ASCII direct families below.
        // Folded prefixes compare per byte under invariant folding (Kelvin and Turkic
        // divergences stay fenced upstream); the executor switches on the kind.
        if (_fallbackDirectFamily.Kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix or Utf8FallbackDirectFamilyKind.AnchoredFoldedAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            if (Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredPrefixLength))
            {
                match = new Utf8ValueMatch(true, true, 0, anchoredPrefixLength, 0, anchoredPrefixLength);
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
                var suffixUtf16Index = Utf8InputAnalyzer.IsAscii(input)
                    ? suffixByteIndex
                    : Utf8Validation.Validate(input[..suffixByteIndex]).Utf16Length;
                match = new Utf8ValueMatch(true, true, suffixUtf16Index, suffixLength, suffixByteIndex, suffixLength);
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
        // Same byte-exact contract as the Match arm above: anchored ASCII prefixes
        // never need the BCL fallback, even on non-ASCII input. Folded prefixes ride
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
            if (directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.NeedsValidation)
            {
                return false;
            }

            isMatch = directResult == Utf8AsciiAnchoredValidatorExecutor.DirectMatchResult.Match;
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
        ReadOnlySpan<byte> input,
        out bool isMatch)
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

            case Utf8PrioritizedBooleanFamilyKind.AnchoredBoundedDate:
                return TryIsMatchAnchoredBoundedDateWithoutValidation(input, out isMatch);

            case Utf8PrioritizedBooleanFamilyKind.CompiledAnchoredOptionalField:
                if (_compiledEngineRuntime is Utf8SimplePatternCompiledEngineRuntime compiledRuntime &&
                    compiledRuntime.TryMatchEmittedWhole(input, out _))
                {
                    isMatch = true;
                    return true;
                }

                return false;

            case Utf8PrioritizedBooleanFamilyKind.AnchoredAsciiLeadingDigitsTail:
                if (Utf8AsciiLeadingDigitsTailExecutor.TryMatchPrintableAsciiWhole(
                    input,
                    _fallbackDirectFamily.LiteralUtf8,
                    out isMatch))
                {
                    return true;
                }

                return false;

            case Utf8PrioritizedBooleanFamilyKind.CompiledRepeatedDigitGroup:
                if (_compiledEngineRuntime is Utf8SimplePatternCompiledEngineRuntime repeatedGroupRuntime &&
                    repeatedGroupRuntime.TryMatchEmittedWhole(input, out _))
                {
                    isMatch = true;
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

    private bool ShouldUseKelvinSignFallback(ReadOnlySpan<byte> input)
        => _requiresKelvinSignFallback && ContainsKelvinSign(input) && !ScalarVerifierServesInput(input);

    // R05: line-anchored shapes serve owned through the interpreter/linear
    // verifiers and the owned split cursors, which enforce line edges per
    // candidate; absolute-anchored shapes keep their dedicated arms and
    // fallbacks. Culture, Kelvin, and right-to-left keep their fences.
    private bool CanServeLineAnchoredOwned(ReadOnlySpan<byte> input)
        => _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
        !_preparedRegex.SimplePatternPlan.IsAbsoluteStartAnchored &&
        !_preparedRegex.SimplePatternPlan.IsAbsoluteEndAnchored &&
        (_preparedRegex.SimplePatternPlan.IsStartAnchored || _preparedRegex.SimplePatternPlan.IsEndAnchored) &&
        !RequiresCultureSensitiveFallback &&
        !UsesRightToLeft() &&
        !ShouldUseKelvinSignFallback(input);

    // Guided verification and owned template replacement only try scalar-boundary
    // starts, so astral inputs need shapes whose matches start at scalar
    // boundaries: literal spans (exact and folded literals, grouped literal tables),
    // ASCII-first non-empty matches (byte-safe plans with positive minimum length),
    // or anchored matches (positions 0, line edges, input end). BMP and ASCII inputs
    // stay on the fast paths: their units are scalars. Everything else falls back
    // to full BCL decode on astral input.
    private bool OwnedMatchStartsAreScalarAligned(ReadOnlySpan<byte> input)
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

        return _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            _preparedRegex.SimplePatternPlan.IsUtf8ByteSafe &&
            (_preparedRegex.SimplePatternPlan.MinLength > 0 ||
            _preparedRegex.SimplePatternPlan.IsStartAnchored ||
            _preparedRegex.SimplePatternPlan.IsEndAnchored);
    }

    // Scalar-verifiable simple patterns fold Kelvin owned through the scalar
    // walk, so they bypass Kelvin fencing everywhere; every other fenced route
    // (anchored validators, literal families, byte-safe plans) keeps the fence.
    private bool ScalarVerifierServesInput(ReadOnlySpan<byte> input)
        => _preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
        Utf8ScalarSimplePatternVerifier.CanServeScalar(_preparedRegex.ExecutionKind, _preparedRegex.SimplePatternPlan, input);

    private bool CanValidateKelvinSignDuringNativeCount()
        => _requiresKelvinSignFallback &&
           (Options & RegexOptions.CultureInvariant) != 0 &&
           !UsesRightToLeft() &&
           _compiledEngineRuntime.SupportsWellFormedOnlyCount &&
           _preparedRegex.ExecutionKind is NativeExecutionKind.AsciiLiteralIgnoreCase or
               NativeExecutionKind.AsciiLiteralIgnoreCaseLiterals;

    private static bool ContainsKelvinSign(ReadOnlySpan<byte> input)
    {
        while (input.Length >= 3)
        {
            var index = input.IndexOf((byte)0xE2);
            if (index < 0)
            {
                return false;
            }

            input = input[index..];
            if (input[1] == 0x84 && input[2] == 0xAA)
            {
                return true;
            }

            input = input[1..];
        }

        return false;
    }

    private static bool RequiresKelvinSignFallback(Utf8PreparedRegex preparedRegex)
    {
        // Anchored simple-pattern routes compare ASCII literals and classes without
        // Kelvin folding, while .NET matches U+212A against `k`/`K` under invariant
        // IgnoreCase (PLAN.md C05). Fencing the whole kind when IgnoreCase is set is
        // conservative but exact: only Kelvin-containing inputs take the fallback.
        if (preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
            preparedRegex.SimplePatternPlan.IgnoreCase)
        {
            return true;
        }

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

    private bool ShouldUseFallbackForSimplePatternReplacement(Utf8AnalyzedReplacement replacement, ReadOnlySpan<byte> input)
    {
        // R05: groupless templates stay on owned routes under an infinite timeout
        // (mirroring the groupless evaluator arm); finite timeouts keep the legacy
        // routing so BCL timeout enforcement is preserved exactly.
        if (MatchTimeout == Regex.InfiniteMatchTimeout && !_hasCaptures)
        {
            return false;
        }

        if (replacement.ContainsGroupReferences && MatchTimeout == Regex.InfiniteMatchTimeout && HasOwnedTemplateGroupShaping(input))
        {
            return false;
        }

        // R05: caret-anchored grouped literals replace owned from the exact prefix
        // hit (group shaping reuses the span table); the anchored simple-pattern
        // clause below stays for every other anchored shape.
        if (MatchTimeout == Regex.InfiniteMatchTimeout &&
            _caretAnchoredGroupedLiteralUtf8 is { Length: > 0 } &&
            _groupedLiteralSpanTable is not null &&
            !RequiresCultureSensitiveFallback && !ShouldUseKelvinSignFallback(input))
        {
            return false;
        }

        return replacement.ContainsGroupReferences ||
            (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
             ShouldUseFallbackForAnchoredSimplePattern());
    }

    // R05: template group references stay on the owned replacement route when the
    // grouped span maps cover the shape, or when the shape is groupless (mirroring the
    // groupless evaluator arm, which carries no declines). Culture and Kelvin declines
    // still divert grouped shapes, and the anchored simple-pattern clause below
    // is untouched.
    private bool HasOwnedTemplateGroupShaping(ReadOnlySpan<byte> input)
    {
        if (!_hasCaptures)
        {
            return true;
        }

        if ((_groupedLiteralSpanTable is not null || _alternationBranchSpanMap is not null) &&
            !RequiresCultureSensitiveFallback &&
            !ShouldUseKelvinSignFallback(input))
        {
            return true;
        }

        // R05: single-wrap shapes project group 1 from the whole span through the
        // worker arm, under the same culture and Kelvin fences as the maps.
        if (_groupNumbers.Length == 2 &&
            GetSingleWrapCaptureNumber() == 1 &&
            !RequiresCultureSensitiveFallback &&
            !ShouldUseKelvinSignFallback(input))
        {
            return true;
        }

        // R05: unit branch-span shapes project static branch-invariant unit spans
        // through the worker arm, under the same culture and Kelvin fences.
        return _unitBranchSpanMap is not null &&
            !RequiresCultureSensitiveFallback &&
            !ShouldUseKelvinSignFallback(input);
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
}
