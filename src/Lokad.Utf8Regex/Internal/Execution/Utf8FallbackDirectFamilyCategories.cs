namespace Lokad.Utf8Regex.Internal.Execution;

internal static class Utf8FallbackDirectFamilyCategories
{
    public static bool IsAnchoredPrefixMatchFamily(Utf8FallbackDirectFamilyKind kind)
        => kind is Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix or
        Utf8FallbackDirectFamilyKind.AnchoredNumberPrefix or
        Utf8FallbackDirectFamilyKind.AnchoredOperatorRun or
        Utf8FallbackDirectFamilyKind.AnchoredQuotedStringPrefix or
        Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte or
        Utf8FallbackDirectFamilyKind.AnchoredTrimmedOptionalLiteralPrefixTail;

    public static bool IsAnchoredWholeMatchFamily(Utf8FallbackDirectFamilyKind kind)
        => kind is Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole;

    public static bool IsTokenFindFamily(Utf8FallbackDirectFamilyKind kind)
        => kind is Utf8FallbackDirectFamilyKind.AsciiIdentifierToken or
        Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns or
        Utf8FallbackDirectFamilyKind.AsciiIpv4Token or
        Utf8FallbackDirectFamilyKind.AsciiUriToken or
        Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken;

    public static bool IsPreparedTokenFindFamily(Utf8FallbackDirectFamilyKind kind)
        => kind is Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount or
        Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount;

    public static bool IsTokenCountFamily(Utf8FallbackDirectFamilyKind kind)
        => kind is Utf8FallbackDirectFamilyKind.AsciiIdentifierToken or
        Utf8FallbackDirectFamilyKind.AsciiIpv4Token or
        Utf8FallbackDirectFamilyKind.AsciiUriToken or
        Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken;

    public static bool IsPreparedTokenCountFamily(Utf8FallbackDirectFamilyKind kind)
        => kind is Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount or
        Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount;

    public static bool IsAsciiDefinitiveMatchFamily(Utf8FallbackDirectFamilyKind kind)
        => kind is Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte or
        Utf8FallbackDirectFamilyKind.AnchoredTrimmedOptionalLiteralPrefixTail or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiCellReferenceWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiRangeReferenceWhole or
        Utf8FallbackDirectFamilyKind.AnchoredIdentifierPrefix or
        Utf8FallbackDirectFamilyKind.AnchoredNumberPrefix or
        Utf8FallbackDirectFamilyKind.AnchoredOperatorRun or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiLeadingDigitsTail or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiEmailWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiDigitsQueryWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiHexQueryWhole or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiHexColorWhole or
        Utf8FallbackDirectFamilyKind.LeadingAnyRunTrailingAsciiLiteral or
        Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns or
        Utf8FallbackDirectFamilyKind.AsciiIdentifierToken or
        Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount or
        Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount or
        Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount or
        Utf8FallbackDirectFamilyKind.AsciiIpv4Token or
        Utf8FallbackDirectFamilyKind.AsciiUriToken or
        Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken;

    public static bool IsNativeFallbackOnlyCountFamily(Utf8FallbackDirectFamilyKind kind)
        => kind is Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount or
        Utf8FallbackDirectFamilyKind.LinePrefixCount;

    public static bool SupportsThrowIfInvalidOnlyCount(Utf8FallbackDirectFamilyKind kind)
        => IsNativeFallbackOnlyCountFamily(kind) ||
        kind is Utf8FallbackDirectFamilyKind.AsciiWordBoundedCount or
        Utf8FallbackDirectFamilyKind.AsciiIdentifierToken or
        Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount or
        Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount or
        Utf8FallbackDirectFamilyKind.AsciiDottedDecimalQuadCount or
        Utf8FallbackDirectFamilyKind.AsciiIpv4Token or
        Utf8FallbackDirectFamilyKind.AsciiUriToken or
        Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken or
        Utf8FallbackDirectFamilyKind.AsciiUntilByteStarCount or
        Utf8FallbackDirectFamilyKind.AnchoredAsciiSignedDecimalWhole;

    public static bool SkipsRequiredPrefilterForCount(Utf8FallbackDirectFamilyKind kind)
        => kind is Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount or
        Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount or
        Utf8FallbackDirectFamilyKind.AsciiIdentifierToken or
        Utf8FallbackDirectFamilyKind.AsciiIpv4Token or
        Utf8FallbackDirectFamilyKind.AsciiUriToken;
}
