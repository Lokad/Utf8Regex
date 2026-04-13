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
