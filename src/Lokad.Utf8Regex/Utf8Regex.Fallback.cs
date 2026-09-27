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

        if (CanGuideFallbackVerification())
        {
            return MatchFallbackViaSearchStarts(input, boundaryMap);
        }

        var decoded = Encoding.UTF8.GetString(input);
        var match = _verifierRuntime.FallbackCandidateVerifier.FallbackRegex.Match(decoded);
        var map = boundaryMap ?? Utf8InputAnalyzer.Analyze(input).BoundaryMap;
        return Utf8ProjectionExecutor.ProjectFallbackRegexMatch(input, match, map);
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
        // out every substring match. Line-anchored (`(?m:^)`) plans can still match
        // after a line feed, so they decline to the validating scan paths.
        if (_preparedRegex.SimplePatternPlan.IsStartAnchored &&
            !_preparedRegex.SimplePatternPlan.IsAbsoluteStartAnchored)
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
        // out every substring match. Line-anchored (`(?m:^)`) plans can still match
        // after a line feed, so they decline to the validating scan paths.
        if (_preparedRegex.SimplePatternPlan.IsStartAnchored &&
            !_preparedRegex.SimplePatternPlan.IsAbsoluteStartAnchored)
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
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix &&
            _fallbackDirectFamily.LiteralUtf8 is { Length: > 0 })
        {
            if (Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out var anchoredPrefixLength))
            {
                match = new Utf8ValueMatch(true, true, 0, anchoredPrefixLength, 0, anchoredPrefixLength);
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
        // never need the BCL fallback, even on non-ASCII input.
        if (_fallbackDirectFamily.Kind == Utf8FallbackDirectFamilyKind.AnchoredAsciiLiteralPrefix)
        {
            isMatch = Utf8AsciiAnchoredPrefixFamilyExecutor.TryMatchPrefix(input, _fallbackDirectFamily, out _);
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
        => _requiresKelvinSignFallback && ContainsKelvinSign(input);

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

    private bool ShouldUseFallbackForSimplePatternReplacement(Utf8AnalyzedReplacement replacement)
    {
        return replacement.ContainsGroupReferences ||
            (_preparedRegex.ExecutionKind == NativeExecutionKind.AsciiSimplePattern &&
             ShouldUseFallbackForAnchoredSimplePattern());
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
