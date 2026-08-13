namespace Lokad.Utf8Regex.Pcre2;

public static class Pcre2ErrorKinds
{
    public const string UnrecognizedEscape = "UnrecognizedEscape";
    public const string InvalidAfterParensQuery = "InvalidAfterParensQuery";
    public const string QuantifierNumbersOutOfOrder = "QuantifierNumbersOutOfOrder";
    public const string QuantifierTooBig = "QuantifierTooBig";
    public const string MissingCharacterClassTerminator = "MissingCharacterClassTerminator";
    public const string EscapeInvalidInClass = "EscapeInvalidInClass";
    public const string ClassRangeOrder = "ClassRangeOrder";
    public const string QuantifierInvalid = "QuantifierInvalid";
    public const string MissingClosingParenthesis = "MissingClosingParenthesis";
    public const string MissingCommentClosing = "MissingCommentClosing";
    public const string BackslashCDisabled = "BackslashCDisabled";
    public const string BackslashCInUtfLookbehind = "BackslashCInUtfLookbehind";
    public const string LookaroundBackslashKDisabled = "LookaroundBackslashKDisabled";
    public const string DisallowedLookaroundBackslashK = "DisallowedLookaroundBackslashK";
    public const string CalloutUnsupported = "CalloutUnsupported";
}

internal enum Pcre2ErrorKind
{
    UnrecognizedEscape,
    InvalidAfterParensQuery,
    QuantifierNumbersOutOfOrder,
    QuantifierTooBig,
    MissingCharacterClassTerminator,
    EscapeInvalidInClass,
    ClassRangeOrder,
    QuantifierInvalid,
    MissingClosingParenthesis,
    MissingCommentClosing,
    BackslashCDisabled,
    BackslashCInUtfLookbehind,
    LookaroundBackslashKDisabled,
    DisallowedLookaroundBackslashK,
    CalloutUnsupported,
    PartialMatch,
    PartialSubs,
    BadReplacementEscape,
    InvalidReplacementReference,
    MalformedReplacementPattern,
    MissingClosingReplacementBrace,
    UnknownReplacementGroup,
    UnsetReplacementGroup,
}

internal static class Pcre2ErrorKindExtensions
{
    internal static string ToPublicString(this Pcre2ErrorKind errorKind)
        => errorKind switch
        {
            Pcre2ErrorKind.UnrecognizedEscape => Pcre2ErrorKinds.UnrecognizedEscape,
            Pcre2ErrorKind.InvalidAfterParensQuery => Pcre2ErrorKinds.InvalidAfterParensQuery,
            Pcre2ErrorKind.QuantifierNumbersOutOfOrder => Pcre2ErrorKinds.QuantifierNumbersOutOfOrder,
            Pcre2ErrorKind.QuantifierTooBig => Pcre2ErrorKinds.QuantifierTooBig,
            Pcre2ErrorKind.MissingCharacterClassTerminator => Pcre2ErrorKinds.MissingCharacterClassTerminator,
            Pcre2ErrorKind.EscapeInvalidInClass => Pcre2ErrorKinds.EscapeInvalidInClass,
            Pcre2ErrorKind.ClassRangeOrder => Pcre2ErrorKinds.ClassRangeOrder,
            Pcre2ErrorKind.QuantifierInvalid => Pcre2ErrorKinds.QuantifierInvalid,
            Pcre2ErrorKind.MissingClosingParenthesis => Pcre2ErrorKinds.MissingClosingParenthesis,
            Pcre2ErrorKind.MissingCommentClosing => Pcre2ErrorKinds.MissingCommentClosing,
            Pcre2ErrorKind.BackslashCDisabled => Pcre2ErrorKinds.BackslashCDisabled,
            Pcre2ErrorKind.BackslashCInUtfLookbehind => Pcre2ErrorKinds.BackslashCInUtfLookbehind,
            Pcre2ErrorKind.LookaroundBackslashKDisabled => Pcre2ErrorKinds.LookaroundBackslashKDisabled,
            Pcre2ErrorKind.DisallowedLookaroundBackslashK => Pcre2ErrorKinds.DisallowedLookaroundBackslashK,
            Pcre2ErrorKind.CalloutUnsupported => Pcre2ErrorKinds.CalloutUnsupported,
            Pcre2ErrorKind.PartialMatch => "PartialMatch",
            Pcre2ErrorKind.PartialSubs => "PartialSubs",
            Pcre2ErrorKind.BadReplacementEscape => "BadReplacementEscape",
            Pcre2ErrorKind.InvalidReplacementReference => "InvalidReplacementReference",
            Pcre2ErrorKind.MalformedReplacementPattern => "MalformedReplacementPattern",
            Pcre2ErrorKind.MissingClosingReplacementBrace => "MissingClosingReplacementBrace",
            Pcre2ErrorKind.UnknownReplacementGroup => "UnknownReplacementGroup",
            Pcre2ErrorKind.UnsetReplacementGroup => "UnsetReplacementGroup",
            _ => throw new ArgumentOutOfRangeException(nameof(errorKind)),
        };
}
