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
    byte[]? LiteralUtf8,
    byte[]? SecondaryLiteralUtf8,
    byte[]? TertiaryLiteralUtf8);

internal readonly record struct Utf8FallbackCharSetPayload(
    byte[]? HeadCharSetUtf8,
    byte[]? MiddleCharSetUtf8,
    byte[]? TailCharSetUtf8,
    byte[]? ExtraCharSetUtf8);

internal readonly record struct Utf8FallbackCountBoundsPayload(
    int MinCount,
    int MaxCount);

internal readonly record struct Utf8FallbackLinePayload(
    bool TrimLeadingAsciiWhitespace,
    byte TerminatorByte);

internal readonly record struct Utf8FallbackDateTokenPayload(
    byte FirstFieldMinCount,
    byte FirstFieldMaxCount,
    byte SecondFieldMinCount,
    byte SecondFieldMaxCount,
    byte ThirdFieldMinCount,
    byte ThirdFieldMaxCount,
    byte SeparatorByte,
    byte SecondSeparatorByte,
    bool RequireLeadingBoundary,
    bool RequireTrailingBoundary);

internal readonly record struct Utf8FallbackUnicodePayload(
    UnicodeCategory UnicodeCategory);

internal readonly record struct Utf8FallbackUrlPayload(
    byte[]? PrimaryPrefixUtf8,
    byte[]? SecondaryPrefixUtf8,
    byte[]? RelativePrefixUtf8,
    byte[]? RouteMarkerUtf8,
    byte[]? RequiredParameterUtf8,
    byte[]? OptionalParameterUtf8);

internal readonly struct Utf8FallbackDirectFamilyPlan
{
    private Utf8FallbackDirectFamilyPlan(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode,
        Utf8FallbackCountBoundsPayload countBounds,
        Utf8FallbackLiteralPayload literals,
        Utf8FallbackCharSetPayload charSets,
        Utf8FallbackLinePayload line,
        Utf8FallbackDateTokenPayload dateToken,
        Utf8FallbackUnicodePayload unicode,
        Utf8FallbackUrlPayload url)
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

    private static Utf8FallbackDirectFamilyPlan Create(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode) =>
        new(kind, findMode, default, default, default, default, default, default, default);

    private static Utf8FallbackDirectFamilyPlan CreateWithCountBounds(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackCountBoundsPayload countBounds) =>
        new(kind, Utf8FallbackFindModeKind.None, countBounds, default, default, default, default, default, default);

    private static Utf8FallbackDirectFamilyPlan CreateWithLiterals(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode,
        Utf8FallbackLiteralPayload literals) =>
        new(kind, findMode, default, literals, default, default, default, default, default);

    private static Utf8FallbackDirectFamilyPlan CreateWithLiteralsAndLine(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode,
        Utf8FallbackLiteralPayload literals,
        Utf8FallbackLinePayload line) =>
        new(kind, findMode, default, literals, default, line, default, default, default);

    private static Utf8FallbackDirectFamilyPlan CreateWithLiteralsAndCharSets(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode,
        Utf8FallbackLiteralPayload literals,
        Utf8FallbackCharSetPayload charSets) =>
        new(kind, findMode, default, literals, charSets, default, default, default, default);

    private static Utf8FallbackDirectFamilyPlan CreateWithLiteralsAndDateToken(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode,
        Utf8FallbackLiteralPayload literals,
        Utf8FallbackDateTokenPayload dateToken) =>
        new(kind, findMode, default, literals, default, default, dateToken, default, default);

    private static Utf8FallbackDirectFamilyPlan CreateWithLine(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackLinePayload line) =>
        new(kind, Utf8FallbackFindModeKind.None, default, default, default, line, default, default, default);

    private static Utf8FallbackDirectFamilyPlan CreateWithDateToken(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode,
        Utf8FallbackDateTokenPayload dateToken) =>
        new(kind, findMode, default, default, default, default, dateToken, default, default);

    private static Utf8FallbackDirectFamilyPlan CreateWithUnicode(Utf8FallbackUnicodePayload unicode) =>
        new(Utf8FallbackDirectFamilyKind.UnicodeCategoryCount, Utf8FallbackFindModeKind.None,
            default, default, default, default, default, unicode, default);

    private static Utf8FallbackDirectFamilyPlan CreateWithUrl(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackUrlPayload url) =>
        new(kind, Utf8FallbackFindModeKind.MatchAtStart, default, default, default, default, default, default, url);

    public static Utf8FallbackDirectFamilyPlan ForKind(
        Utf8FallbackDirectFamilyKind kind) => Create(kind, Utf8FallbackFindModeKind.None);

    public static Utf8FallbackDirectFamilyPlan ForKind(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode) => Create(kind, findMode);

    public static Utf8FallbackDirectFamilyPlan ForCountBounds(
        Utf8FallbackDirectFamilyKind kind,
        int minCount) => ForCountBounds(kind, minCount, 0);

    public static Utf8FallbackDirectFamilyPlan ForCountBounds(
        Utf8FallbackDirectFamilyKind kind,
        int minCount,
        int maxCount) => CreateWithCountBounds(kind, new Utf8FallbackCountBoundsPayload(minCount, maxCount));

    public static Utf8FallbackDirectFamilyPlan ForUnicodeCategory(UnicodeCategory unicodeCategory)
        => CreateWithUnicode(new Utf8FallbackUnicodePayload(unicodeCategory));

    public static Utf8FallbackDirectFamilyPlan ForLiteral(
        Utf8FallbackDirectFamilyKind kind,
        Utf8FallbackFindModeKind findMode,
        byte[]? literalUtf8) =>
        CreateWithLiterals(kind, findMode, new Utf8FallbackLiteralPayload(literalUtf8, null, null));

    public static Utf8FallbackDirectFamilyPlan ForPrefixUntilByte(byte[] literalUtf8, byte terminatorByte)
        => CreateWithLiteralsAndLine(
            Utf8FallbackDirectFamilyKind.AnchoredPrefixUntilByte,
            Utf8FallbackFindModeKind.MatchAtStart,
            new Utf8FallbackLiteralPayload(literalUtf8, null, null),
            new Utf8FallbackLinePayload(false, terminatorByte));

    public static Utf8FallbackDirectFamilyPlan ForTrimmedOptionalLiteralPrefixTail(byte[] literalUtf8, byte[]? secondaryLiteralUtf8)
        => CreateWithLiteralsAndLine(
            Utf8FallbackDirectFamilyKind.AnchoredTrimmedOptionalLiteralPrefixTail,
            Utf8FallbackFindModeKind.MatchAtStart,
            new Utf8FallbackLiteralPayload(literalUtf8, secondaryLiteralUtf8, null),
            new Utf8FallbackLinePayload(true, 0));

    public static Utf8FallbackDirectFamilyPlan ForLinePrefixCount(byte[]? literalUtf8, bool trimLeadingAsciiWhitespace)
        => CreateWithLiteralsAndLine(
            Utf8FallbackDirectFamilyKind.LinePrefixCount,
            Utf8FallbackFindModeKind.CountLines,
            new Utf8FallbackLiteralPayload(literalUtf8, null, null),
            new Utf8FallbackLinePayload(trimLeadingAsciiWhitespace, 0));

    public static Utf8FallbackDirectFamilyPlan ForQuotedLineSegmentCount(byte[] literalUtf8, byte[]? secondaryLiteralUtf8)
        => CreateWithLiterals(
            Utf8FallbackDirectFamilyKind.AnchoredQuotedLineSegmentCount,
            Utf8FallbackFindModeKind.CountLines,
            new Utf8FallbackLiteralPayload(literalUtf8, secondaryLiteralUtf8, null));

    public static Utf8FallbackDirectFamilyPlan ForDelimitedTokenCount(
        byte[] delimiterUtf8,
        byte[] secondaryDelimiterUtf8,
        byte[] headCharSetUtf8,
        byte[] middleCharSetUtf8,
        byte[] tailCharSetUtf8)
        => CreateWithLiteralsAndCharSets(
            Utf8FallbackDirectFamilyKind.AsciiDelimitedTokenCount,
            Utf8FallbackFindModeKind.None,
            new Utf8FallbackLiteralPayload(delimiterUtf8, secondaryDelimiterUtf8, null),
            new Utf8FallbackCharSetPayload(headCharSetUtf8, middleCharSetUtf8, tailCharSetUtf8, null));

    public static Utf8FallbackDirectFamilyPlan ForLiteralStructuredTokenCount(
        byte[] literalUtf8,
        byte[]? secondaryLiteralUtf8,
        byte[]? tertiaryLiteralUtf8,
        byte[] headCharSetUtf8,
        byte[] middleCharSetUtf8,
        byte[] tailCharSetUtf8,
        byte[]? extraCharSetUtf8)
        => CreateWithLiteralsAndCharSets(
            Utf8FallbackDirectFamilyKind.AsciiLiteralStructuredTokenCount,
            Utf8FallbackFindModeKind.None,
            new Utf8FallbackLiteralPayload(literalUtf8, secondaryLiteralUtf8, tertiaryLiteralUtf8),
            new Utf8FallbackCharSetPayload(headCharSetUtf8, middleCharSetUtf8, tailCharSetUtf8, extraCharSetUtf8));

    public static Utf8FallbackDirectFamilyPlan ForLiteralBetweenNegatedRuns(
        byte[] literalUtf8,
        byte separatorByte,
        byte secondSeparatorByte)
        => CreateWithLiteralsAndDateToken(
            Utf8FallbackDirectFamilyKind.AsciiLiteralBetweenNegatedRuns,
            Utf8FallbackFindModeKind.FindToken,
            new Utf8FallbackLiteralPayload(literalUtf8, null, null),
            new Utf8FallbackDateTokenPayload(0, 0, 0, 0, 0, 0, separatorByte, secondSeparatorByte, false, false));

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
        => CreateWithDateToken(
            Utf8FallbackDirectFamilyKind.AsciiBoundedDateToken,
            Utf8FallbackFindModeKind.FindToken,
            new Utf8FallbackDateTokenPayload(
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
        => CreateWithLine(
            Utf8FallbackDirectFamilyKind.AsciiUntilByteStarCount,
            new Utf8FallbackLinePayload(false, terminatorByte));

    public static Utf8FallbackDirectFamilyPlan ForAnchoredAsciiQueryWhole(
        Utf8FallbackDirectFamilyKind kind,
        byte[] primaryPrefixUtf8,
        byte[] secondaryPrefixUtf8,
        byte[] relativePrefixUtf8,
        byte[] routeMarkerUtf8,
        byte[] requiredParameterUtf8) =>
        ForAnchoredAsciiQueryWhole(
            kind,
            primaryPrefixUtf8,
            secondaryPrefixUtf8,
            relativePrefixUtf8,
            routeMarkerUtf8,
            requiredParameterUtf8,
            null);

    public static Utf8FallbackDirectFamilyPlan ForAnchoredAsciiQueryWhole(
        Utf8FallbackDirectFamilyKind kind,
        byte[] primaryPrefixUtf8,
        byte[] secondaryPrefixUtf8,
        byte[] relativePrefixUtf8,
        byte[] routeMarkerUtf8,
        byte[] requiredParameterUtf8,
        byte[]? optionalParameterUtf8)
        => CreateWithUrl(
            kind,
            new Utf8FallbackUrlPayload(
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
