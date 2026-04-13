using System.Globalization;
using System.Text.RegularExpressions;
using Lokad.Utf8Regex.Internal.FrontEnd;

namespace Lokad.Utf8Regex.Internal.Execution;

internal static partial class Utf8FallbackRegexFamilyAnalyzer
{
    public static Utf8FallbackDirectFamilyPlan Classify(Utf8AnalyzedRegex analyzedRegex)
        => ClassifyPattern(
            analyzedRegex.ExecutionPattern,
            analyzedRegex.SemanticRegex.Pattern,
            analyzedRegex.SemanticRegex.Options,
            analyzedRegex.SearchInfo.RequiredPrefilterLiteralUtf8);

    public static Utf8FallbackDirectFamilyPlan ClassifyPattern(
        string executionPattern,
        string semanticPattern,
        RegexOptions options,
        byte[]? requiredPrefilterLiteralUtf8 = null)
    {
        if (TryParseAnchoredQuotedLineSegmentCountFamily(
            executionPattern,
            options,
            requiredPrefilterLiteralUtf8,
            out var linePrefixUtf8,
            out var optionalSegmentUtf8))
        {
            return Utf8FallbackDirectFamilyPlan.ForQuotedLineSegmentCount(linePrefixUtf8!, optionalSegmentUtf8);
        }

        if (TryParseAnchoredPrefixUntilBytePattern(
            executionPattern,
            options,
            out var prefixUtf8,
            out var terminator) ||
            TryParseAnchoredPrefixUntilBytePattern(
                semanticPattern,
                options,
                out prefixUtf8,
                out terminator))
        {
            return Utf8FallbackDirectFamilyPlan.ForPrefixUntilByte(prefixUtf8!, terminator);
        }

        if (TryParseAnchoredTrimmedOptionalLiteralPrefixTailPattern(
            executionPattern,
            options,
            out prefixUtf8,
            out var optionalPrefixUtf8) ||
            TryParseAnchoredTrimmedOptionalLiteralPrefixTailPattern(
                semanticPattern,
                options,
                out prefixUtf8,
                out optionalPrefixUtf8))
        {
            return Utf8FallbackDirectFamilyPlan.ForTrimmedOptionalLiteralPrefixTail(prefixUtf8!, optionalPrefixUtf8);
        }

        if (TryParseLeadingAnyRunTrailingAsciiLiteral(executionPattern, options, out var trailingLiteralUtf8) ||
            TryParseLeadingAnyRunTrailingAsciiLiteral(semanticPattern, options, out trailingLiteralUtf8))
        {
            return Utf8FallbackDirectFamilyPlan.ForLiteral(
                Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral,
                Utf8FallbackFindModeKind.MatchAtStart,
                literalUtf8: trailingLiteralUtf8);
        }

        if (TryParseAnchoredAsciiDigitsQueryWhole(executionPattern, options, out var urlPayload) ||
            TryParseAnchoredAsciiDigitsQueryWhole(semanticPattern, options, out urlPayload))
        {
            return Utf8FallbackDirectFamilyPlan.ForAnchoredAsciiQueryWhole(
                Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole,
                urlPayload.PrimaryPrefixUtf8!,
                urlPayload.SecondaryPrefixUtf8!,
                urlPayload.RelativePrefixUtf8!,
                urlPayload.RouteMarkerUtf8!,
                urlPayload.RequiredParameterUtf8!);
        }

        if (TryParseAnchoredAsciiHexQueryWhole(executionPattern, options, out urlPayload) ||
            TryParseAnchoredAsciiHexQueryWhole(semanticPattern, options, out urlPayload))
        {
            return Utf8FallbackDirectFamilyPlan.ForAnchoredAsciiQueryWhole(
                Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole,
                urlPayload.PrimaryPrefixUtf8!,
                urlPayload.SecondaryPrefixUtf8!,
                urlPayload.RelativePrefixUtf8!,
                urlPayload.RouteMarkerUtf8!,
                urlPayload.RequiredParameterUtf8!,
                urlPayload.OptionalParameterUtf8);
        }

        if (IsAnchoredAsciiHexColorWhole(executionPattern, options) ||
            IsAnchoredAsciiHexColorWhole(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(
                Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole,
                Utf8FallbackFindModeKind.MatchAtStart);
        }

        if (TryParseLinePrefixCountFamily(executionPattern, options, out var linePrefix, out var trimLeadingAsciiWhitespace))
        {
            return Utf8FallbackDirectFamilyPlan.ForLinePrefixCount(linePrefix, trimLeadingAsciiWhitespace);
        }

        if (IsAnchoredAsciiIdentifierPrefix(executionPattern, options) ||
            IsAnchoredAsciiIdentifierPrefix(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix, Utf8FallbackFindModeKind.MatchAtStart);
        }

        if (IsAnchoredAsciiNumberPrefix(executionPattern, options) ||
            IsAnchoredAsciiNumberPrefix(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AnchoredNumberPrefix, Utf8FallbackFindModeKind.MatchAtStart);
        }

        if (IsAnchoredAsciiSignedDecimalWhole(executionPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole, Utf8FallbackFindModeKind.MatchAtStart);
        }

        if (TryParseAnchoredAsciiLeadingDigitsTail(executionPattern, options, out var separatorBytesUtf8) ||
            TryParseAnchoredAsciiLeadingDigitsTail(semanticPattern, options, out separatorBytesUtf8))
        {
            return Utf8FallbackDirectFamilyPlan.ForLiteral(
                Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail,
                Utf8FallbackFindModeKind.MatchAtStart,
                literalUtf8: separatorBytesUtf8);
        }

        if (IsAnchoredAsciiEmailWhole(executionPattern, options) ||
            IsAnchoredAsciiEmailWhole(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole, Utf8FallbackFindModeKind.MatchAtStart);
        }

        if (IsAnchoredAsciiCellReferenceWhole(executionPattern, options) ||
            IsAnchoredAsciiCellReferenceWhole(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole, Utf8FallbackFindModeKind.MatchAtStart);
        }

        if (IsAnchoredAsciiRangeReferenceWhole(executionPattern, options) ||
            IsAnchoredAsciiRangeReferenceWhole(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole, Utf8FallbackFindModeKind.MatchAtStart);
        }

        if (IsAnchoredAsciiOperatorRun(executionPattern, options) ||
            IsAnchoredAsciiOperatorRun(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AnchoredOperatorRun, Utf8FallbackFindModeKind.MatchAtStart);
        }

        if (IsAnchoredQuotedStringPrefix(executionPattern, options) ||
            IsAnchoredQuotedStringPrefix(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix, Utf8FallbackFindModeKind.MatchAtStart);
        }

        if (TryParseUnicodeLetterBoundedRepeat(executionPattern, options, out var minCount, out var maxCount))
        {
            return Utf8FallbackDirectFamilyPlan.ForCountBounds(
                Utf8FallbackDirectFamilyKind.UnicodeLetterBoundedCount,
                minCount,
                maxCount);
        }

        if (TryParseAsciiWordBoundedRepeat(executionPattern, options, out minCount))
        {
            return Utf8FallbackDirectFamilyPlan.ForCountBounds(
                Utf8FallbackDirectFamilyKind.AsciiWordBoundedCount,
                minCount);
        }

        if (TryParseAsciiIdentifierToken(executionPattern, options) ||
            TryParseAsciiIdentifierToken(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AsciiIdentifierToken, Utf8FallbackFindModeKind.FindToken);
        }

        if (TryParseAsciiDelimitedTokenCount(
            executionPattern,
            options,
            out var headCharSetUtf8,
            out var delimiterUtf8,
            out var middleCharSetUtf8,
            out var secondaryDelimiterUtf8,
            out var tailCharSetUtf8))
        {
            return Utf8FallbackDirectFamilyPlan.ForDelimitedTokenCount(
                delimiterUtf8!,
                secondaryDelimiterUtf8!,
                headCharSetUtf8!,
                middleCharSetUtf8!,
                tailCharSetUtf8!);
        }

        if (TryParseAsciiLiteralBetweenNegatedRuns(
            executionPattern,
            options,
            out var infixLiteralUtf8,
            out var excludedHeadByte,
            out var excludedTailByte))
        {
            return Utf8FallbackDirectFamilyPlan.ForLiteralBetweenNegatedRuns(
                infixLiteralUtf8!,
                excludedHeadByte,
                excludedTailByte);
        }

        if (TryParseAsciiUriToken(executionPattern, options) ||
            TryParseAsciiUriToken(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AsciiUriToken, Utf8FallbackFindModeKind.FindToken);
        }

        if (TryParseAsciiLiteralStructuredTokenCount(
            executionPattern,
            options,
            out headCharSetUtf8,
            out var literalUtf8,
            out middleCharSetUtf8,
            out tailCharSetUtf8,
            out var optionalDelimiterUtf8,
            out var optionalTailCharSetUtf8,
            out var finalDelimiterUtf8,
            out var finalTailCharSetUtf8))
        {
            return Utf8FallbackDirectFamilyPlan.ForLiteralStructuredTokenCount(
                literalUtf8!,
                optionalDelimiterUtf8,
                finalDelimiterUtf8,
                headCharSetUtf8!,
                middleCharSetUtf8!,
                tailCharSetUtf8!,
                [.. (optionalTailCharSetUtf8 ?? []), 0, .. (finalTailCharSetUtf8 ?? [])]);
        }

        if (IsAsciiDottedDecimalQuadCount(executionPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount);
        }

        if (TryParseAsciiIpv4Token(executionPattern, options) ||
            TryParseAsciiIpv4Token(semanticPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.AsciiIpv4Token, Utf8FallbackFindModeKind.FindToken);
        }

        if (TryParseAsciiBoundedDateToken(
            executionPattern,
            options,
            out var firstFieldMinCount,
            out var firstFieldMaxCount,
            out var secondFieldMinCount,
            out var secondFieldMaxCount,
            out var thirdFieldMinCount,
            out var thirdFieldMaxCount,
            out var separatorByte,
            out var secondSeparatorByte,
            out var requireLeadingBoundary,
            out var requireTrailingBoundary) ||
            TryParseAsciiBoundedDateToken(
                semanticPattern,
                options,
                out firstFieldMinCount,
                out firstFieldMaxCount,
                out secondFieldMinCount,
                out secondFieldMaxCount,
                out thirdFieldMinCount,
                out thirdFieldMaxCount,
                out separatorByte,
                out secondSeparatorByte,
                out requireLeadingBoundary,
                out requireTrailingBoundary))
        {
            return Utf8FallbackDirectFamilyPlan.ForBoundedDateToken(
                firstFieldMinCount,
                firstFieldMaxCount,
                secondFieldMinCount,
                secondFieldMaxCount,
                thirdFieldMinCount,
                thirdFieldMaxCount,
                separatorByte,
                secondSeparatorByte,
                requireLeadingBoundary,
                requireTrailingBoundary);
        }

        if (TryParseAsciiUntilByteStarCount(executionPattern, options, out var terminatorByte))
        {
            return Utf8FallbackDirectFamilyPlan.ForAsciiUntilByteStarCount(terminatorByte);
        }

        if (TryParseUnicodeLetterCount(executionPattern, options))
        {
            return Utf8FallbackDirectFamilyPlan.ForKind(Utf8FallbackDirectFamilyKind.UnicodeLetterCount);
        }

        if (TryParseUnicodeCategoryCount(executionPattern, options, out var unicodeCategory))
        {
            return Utf8FallbackDirectFamilyPlan.ForUnicodeCategory(unicodeCategory);
        }

        return default;
    }

}
