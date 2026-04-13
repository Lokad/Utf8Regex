using System.Globalization;

namespace Lokad.Utf8Regex.Internal.Execution;

internal enum Utf8FallbackDirectFamilyKind : byte
{
    None = 0,
    AnchoredQuotedLineSegmentCount = 1,
    AnchoredIdentifierPrefix = 2,
    AnchoredNumberPrefix = 3,
    AnchoredOperatorRun = 4,
    AnchoredQuotedStringPrefix = 5,
    UnicodeLetterBoundedCount = 6,
    LinePrefixCount = 7,
    AsciiIdentifierToken = 8,
    AnchoredPrefixUntilByte = 9,
    AsciiDelimitedTokenCount = 10,
    AsciiLiteralStructuredTokenCount = 11,
    AsciiDottedDecimalQuadCount = 12,
    AsciiIpv4Token = 13,
    AsciiUntilByteStarCount = 14,
    UnicodeLetterCount = 15,
    UnicodeCategoryCount = 16,
    AsciiUriToken = 17,
    AsciiBoundedDateToken = 18,
    AsciiWordBoundedCount = 19,
    AnchoredAsciiSignedDecimalWhole = 20,
    AnchoredAsciiLeadingDigitsTail = 21,
    AnchoredAsciiEmailWhole = 22,
    AsciiLiteralBetweenNegatedRuns = 23,
    AnchoredTrimmedOptionalLiteralPrefixTail = 24,
    AnchoredAsciiCellReferenceWhole = 25,
    AnchoredAsciiRangeReferenceWhole = 26,
    AnchoredAsciiDigitsQueryWhole = 27,
    AnchoredAsciiHexQueryWhole = 28,
    AnchoredAsciiHexColorWhole = 29,
    LeadingAnyRunTrailingAsciiLiteral = 30,
}

internal enum Utf8FallbackFindModeKind : byte
{
    None = 0,
    MatchAtStart = 1,
    FindToken = 2,
    CountLines = 3,
}

internal readonly record struct Utf8FallbackLiteralPayload(
    byte[]? LiteralUtf8 = null,
    byte[]? SecondaryLiteralUtf8 = null,
    byte[]? TertiaryLiteralUtf8 = null);

internal readonly record struct Utf8FallbackCharSetPayload(
    byte[]? HeadCharSetUtf8 = null,
    byte[]? MiddleCharSetUtf8 = null,
    byte[]? TailCharSetUtf8 = null,
    byte[]? ExtraCharSetUtf8 = null);

internal readonly record struct Utf8FallbackCountBoundsPayload(
    int MinCount = 0,
    int MaxCount = 0);

internal readonly record struct Utf8FallbackLinePayload(
    bool TrimLeadingAsciiWhitespace = false,
    byte TerminatorByte = 0);

internal readonly record struct Utf8FallbackDateTokenPayload(
    byte FirstFieldMinCount = 0,
    byte FirstFieldMaxCount = 0,
    byte SecondFieldMinCount = 0,
    byte SecondFieldMaxCount = 0,
    byte ThirdFieldMinCount = 0,
    byte ThirdFieldMaxCount = 0,
    byte SeparatorByte = 0,
    byte SecondSeparatorByte = 0,
    bool RequireLeadingBoundary = false,
    bool RequireTrailingBoundary = false);

internal readonly record struct Utf8FallbackUnicodePayload(
    UnicodeCategory UnicodeCategory = UnicodeCategory.OtherNotAssigned);

internal readonly record struct Utf8FallbackUrlPayload(
    byte[]? PrimaryPrefixUtf8 = null,
    byte[]? SecondaryPrefixUtf8 = null,
    byte[]? RelativePrefixUtf8 = null,
    byte[]? RouteMarkerUtf8 = null,
    byte[]? RequiredParameterUtf8 = null,
    byte[]? OptionalParameterUtf8 = null);

internal readonly struct Utf8FallbackDirectFamilyPlan
{
    private Utf8FallbackDirectFamilyPlan(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode,
        Utf8FallbackCountBoundsPayload countBounds = default,
        Utf8FallbackLiteralPayload literals = default,
        Utf8FallbackCharSetPayload charSets = default,
        Utf8FallbackLinePayload line = default,
        Utf8FallbackDateTokenPayload dateToken = default,
        Utf8FallbackUnicodePayload unicode = default,
        Utf8FallbackUrlPayload url = default)
    {
        Kind = kind;
        FindMode = findMode;
        CountBounds = countBounds;
        Literals = literals;
        CharSets = charSets;
        Line = line;
        DateToken = dateToken;
        Unicode = unicode;
        Url = url;
    }

    public Utf8FallbackDirectFamilyKind Kind { get; }

    public Utf8FallbackFindModeKind FindMode { get; }

    public Utf8FallbackCountBoundsPayload CountBounds { get; }

    public Utf8FallbackLiteralPayload Literals { get; }

    public Utf8FallbackCharSetPayload CharSets { get; }

    public Utf8FallbackLinePayload Line { get; }

    public Utf8FallbackDateTokenPayload DateToken { get; }

    public Utf8FallbackUnicodePayload Unicode { get; }

    public Utf8FallbackUrlPayload Url { get; }

    public int MinCount => CountBounds.MinCount;

    public int MaxCount => CountBounds.MaxCount;

    public byte[]? LiteralUtf8 => Literals.LiteralUtf8;

    public byte[]? SecondaryLiteralUtf8 => Literals.SecondaryLiteralUtf8;

    public byte[]? TertiaryLiteralUtf8 => Literals.TertiaryLiteralUtf8;

    public byte[]? HeadCharSetUtf8 => CharSets.HeadCharSetUtf8;

    public byte[]? MiddleCharSetUtf8 => CharSets.MiddleCharSetUtf8;

    public byte[]? TailCharSetUtf8 => CharSets.TailCharSetUtf8;

    public byte[]? ExtraCharSetUtf8 => CharSets.ExtraCharSetUtf8;

    public bool TrimLeadingAsciiWhitespace => Line.TrimLeadingAsciiWhitespace;

    public byte TerminatorByte => Line.TerminatorByte;

    public byte FirstFieldMinCount => DateToken.FirstFieldMinCount;

    public byte FirstFieldMaxCount => DateToken.FirstFieldMaxCount;

    public byte SecondFieldMinCount => DateToken.SecondFieldMinCount;

    public byte SecondFieldMaxCount => DateToken.SecondFieldMaxCount;

    public byte ThirdFieldMinCount => DateToken.ThirdFieldMinCount;

    public byte ThirdFieldMaxCount => DateToken.ThirdFieldMaxCount;

    public byte SeparatorByte => DateToken.SeparatorByte;

    public byte SecondSeparatorByte => DateToken.SecondSeparatorByte;

    public UnicodeCategory UnicodeCategory => Unicode.UnicodeCategory;

    public byte[]? PrimaryPrefixUtf8 => Url.PrimaryPrefixUtf8;

    public byte[]? SecondaryPrefixUtf8 => Url.SecondaryPrefixUtf8;

    public byte[]? RelativePrefixUtf8 => Url.RelativePrefixUtf8;

    public byte[]? RouteMarkerUtf8 => Url.RouteMarkerUtf8;

    public byte[]? RequiredParameterUtf8 => Url.RequiredParameterUtf8;

    public byte[]? OptionalParameterUtf8 => Url.OptionalParameterUtf8;

    public bool RequireLeadingBoundary => DateToken.RequireLeadingBoundary;

    public bool RequireTrailingBoundary => DateToken.RequireTrailingBoundary;

    public bool HasValue => Kind != Utf8FallbackDirectFamilyKind.None;

    public static Utf8FallbackDirectFamilyPlan ForKind(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode = Utf8FallbackFindModeKind.None)
        => new(kind, findMode);

    public static Utf8FallbackDirectFamilyPlan ForCountBounds(
        Utf8FallbackDirectFamilyKind kind,
        int minCount,
        int maxCount = 0)
        => new(kind, Utf8FallbackFindModeKind.None, countBounds: new Utf8FallbackCountBoundsPayload(minCount, maxCount));

    public static Utf8FallbackDirectFamilyPlan ForUnicodeCategory(UnicodeCategory unicodeCategory)
        => new(
            Utf8FallbackDirectFamilyKind.UnicodeCategoryCount,
            Utf8FallbackFindModeKind.None,
            unicode: new Utf8FallbackUnicodePayload(unicodeCategory));

    public static Utf8FallbackDirectFamilyPlan ForLiteral(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode,
        byte[]? literalUtf8 = null,
        byte[]? secondaryLiteralUtf8 = null,
        byte[]? tertiaryLiteralUtf8 = null)
        => new(
            kind,
            findMode,
            literals: new Utf8FallbackLiteralPayload(literalUtf8, secondaryLiteralUtf8, tertiaryLiteralUtf8));

    public static Utf8FallbackDirectFamilyPlan ForPrefixUntilByte(byte[] literalUtf8, byte terminatorByte)
        => new(
            Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte,
            Utf8FallbackFindModeKind.MatchAtStart,
            literals: new Utf8FallbackLiteralPayload(literalUtf8),
            line: new Utf8FallbackLinePayload(false, terminatorByte));

    public static Utf8FallbackDirectFamilyPlan ForTrimmedOptionalLiteralPrefixTail(byte[] literalUtf8, byte[]? secondaryLiteralUtf8)
        => new(
            Utf8FallbackDirectFamilyKind.AnchoredTrimmedOptionalLiteralPrefixTail,
            Utf8FallbackFindModeKind.MatchAtStart,
            literals: new Utf8FallbackLiteralPayload(literalUtf8, secondaryLiteralUtf8),
            line: new Utf8FallbackLinePayload(true, 0));

    public static Utf8FallbackDirectFamilyPlan ForLinePrefixCount(byte[]? literalUtf8, bool trimLeadingAsciiWhitespace)
        => new(
            Utf8FallbackDirectFamilyKind.LinePrefixCount,
            Utf8FallbackFindModeKind.CountLines,
            literals: new Utf8FallbackLiteralPayload(literalUtf8),
            line: new Utf8FallbackLinePayload(trimLeadingAsciiWhitespace, 0));

    public static Utf8FallbackDirectFamilyPlan ForQuotedLineSegmentCount(byte[] literalUtf8, byte[]? secondaryLiteralUtf8)
        => new(
            Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount,
            Utf8FallbackFindModeKind.CountLines,
            literals: new Utf8FallbackLiteralPayload(literalUtf8, secondaryLiteralUtf8));

    public static Utf8FallbackDirectFamilyPlan ForDelimitedTokenCount(
        byte[] delimiterUtf8,
        byte[] secondaryDelimiterUtf8,
        byte[] headCharSetUtf8,
        byte[] middleCharSetUtf8,
        byte[] tailCharSetUtf8)
        => new(
            Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount,
            Utf8FallbackFindModeKind.None,
            literals: new Utf8FallbackLiteralPayload(delimiterUtf8, secondaryDelimiterUtf8),
            charSets: new Utf8FallbackCharSetPayload(headCharSetUtf8, middleCharSetUtf8, tailCharSetUtf8));

    public static Utf8FallbackDirectFamilyPlan ForLiteralStructuredTokenCount(
        byte[] literalUtf8,
        byte[]? secondaryLiteralUtf8,
        byte[]? tertiaryLiteralUtf8,
        byte[] headCharSetUtf8,
        byte[] middleCharSetUtf8,
        byte[] tailCharSetUtf8,
        byte[]? extraCharSetUtf8)
        => new(
            Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount,
            Utf8FallbackFindModeKind.None,
            literals: new Utf8FallbackLiteralPayload(literalUtf8, secondaryLiteralUtf8, tertiaryLiteralUtf8),
            charSets: new Utf8FallbackCharSetPayload(headCharSetUtf8, middleCharSetUtf8, tailCharSetUtf8, extraCharSetUtf8));

    public static Utf8FallbackDirectFamilyPlan ForLiteralBetweenNegatedRuns(
        byte[] literalUtf8,
        byte separatorByte,
        byte secondSeparatorByte)
        => new(
            Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns,
            Utf8FallbackFindModeKind.FindToken,
            literals: new Utf8FallbackLiteralPayload(literalUtf8),
            dateToken: new Utf8FallbackDateTokenPayload(
                SeparatorByte: separatorByte,
                SecondSeparatorByte: secondSeparatorByte));

    public static Utf8FallbackDirectFamilyPlan ForBoundedDateToken(
        byte firstFieldMinCount,
        byte firstFieldMaxCount,
        byte secondFieldMinCount,
        byte secondFieldMaxCount,
        byte thirdFieldMinCount,
        byte thirdFieldMaxCount,
        byte separatorByte,
        byte secondSeparatorByte,
        bool requireLeadingBoundary,
        bool requireTrailingBoundary)
        => new(
            Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken,
            Utf8FallbackFindModeKind.FindToken,
            dateToken: new Utf8FallbackDateTokenPayload(
                firstFieldMinCount,
                firstFieldMaxCount,
                secondFieldMinCount,
                secondFieldMaxCount,
                thirdFieldMinCount,
                thirdFieldMaxCount,
                separatorByte,
                secondSeparatorByte,
                requireLeadingBoundary,
                requireTrailingBoundary));

    public static Utf8FallbackDirectFamilyPlan ForAsciiUntilByteStarCount(byte terminatorByte)
        => new(
            Utf8FallbackDirectFamilyKind.AsciiUntilByteStarCount,
            Utf8FallbackFindModeKind.None,
            line: new Utf8FallbackLinePayload(false, terminatorByte));

    public static Utf8FallbackDirectFamilyPlan ForAnchoredAsciiQueryWhole(
        Utf8FallbackDirectFamilyKind kind,
        byte[] primaryPrefixUtf8,
        byte[] secondaryPrefixUtf8,
        byte[] relativePrefixUtf8,
        byte[] routeMarkerUtf8,
        byte[] requiredParameterUtf8,
        byte[]? optionalParameterUtf8 = null)
        => new(
            kind,
            Utf8FallbackFindModeKind.MatchAtStart,
            url: new Utf8FallbackUrlPayload(
                primaryPrefixUtf8,
                secondaryPrefixUtf8,
                relativePrefixUtf8,
                routeMarkerUtf8,
                requiredParameterUtf8,
                optionalParameterUtf8));

    public bool SupportsAsciiDefinitiveIsMatch => Utf8FallbackDirectFamilySupport.SupportsAsciiDefinitiveIsMatch(Kind);

    public bool SupportsDefinitiveIsMatch => Utf8FallbackDirectFamilySupport.SupportsDefinitiveIsMatch(Kind);

    public bool SupportsNativeFallbackRoute => Utf8FallbackDirectFamilySupport.SupportsNativeFallbackRoute(Kind);

    public bool SupportsThrowIfInvalidOnlyCount => Utf8FallbackDirectFamilySupport.SupportsThrowIfInvalidOnlyCount(Kind);

    public bool SkipsRequiredPrefilterForCount => Utf8FallbackDirectFamilySupport.SkipsRequiredPrefilterForCount(Kind);

    public bool SupportsAsciiTryMatchWithoutValidation => Utf8FallbackDirectFamilySupport.SupportsAsciiTryMatchWithoutValidation(Kind);
}
