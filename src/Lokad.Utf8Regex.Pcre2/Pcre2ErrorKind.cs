namespace Lokad.Utf8Regex.Pcre2;

/// <summary>Identifies a compile, match, or substitution failure in the managed PCRE2 profile.</summary>
public enum Pcre2ErrorKind
{
    /// <summary>The pattern contains an escape that the dialect does not recognize.</summary>
    UnrecognizedEscape,
    /// <summary>A parenthesized question construct has an invalid discriminator.</summary>
    InvalidAfterParensQuery,
    /// <summary>A quantifier's lower bound exceeds its upper bound.</summary>
    QuantifierNumbersOutOfOrder,
    /// <summary>A quantifier exceeds the supported repetition limit.</summary>
    QuantifierTooBig,
    /// <summary>A character class has no terminating bracket.</summary>
    MissingCharacterClassTerminator,
    /// <summary>An escape is not valid inside a character class.</summary>
    EscapeInvalidInClass,
    /// <summary>A character-class range is reversed.</summary>
    ClassRangeOrder,
    /// <summary>A quantifier does not follow a repeatable item.</summary>
    QuantifierInvalid,
    /// <summary>A group has no closing parenthesis.</summary>
    MissingClosingParenthesis,
    /// <summary>A pattern comment has no closing parenthesis.</summary>
    MissingCommentClosing,
    /// <summary>The active compile settings prohibit <c>\C</c>.</summary>
    BackslashCDisabled,
    /// <summary>The pattern uses <c>\C</c> inside UTF lookbehind.</summary>
    BackslashCInUtfLookbehind,
    /// <summary>The active compile settings prohibit <c>\K</c> in lookaround.</summary>
    LookaroundBackslashKDisabled,
    /// <summary>Iterative execution encountered a non-monotone <c>\K</c> result.</summary>
    DisallowedLookaroundBackslashK,
    /// <summary>The pattern requests an unsupported PCRE2 callout.</summary>
    CalloutUnsupported,
    /// <summary>A full match was required but only a partial match was available.</summary>
    PartialMatch,
    /// <summary>Substitution does not support partial matching.</summary>
    PartialSubs,
    /// <summary>The replacement contains an invalid escape.</summary>
    BadReplacementEscape,
    /// <summary>The replacement contains an invalid capture reference.</summary>
    InvalidReplacementReference,
    /// <summary>The replacement pattern is malformed.</summary>
    MalformedReplacementPattern,
    /// <summary>A braced replacement reference has no closing brace.</summary>
    MissingClosingReplacementBrace,
    /// <summary>The replacement refers to a capture that the pattern does not define.</summary>
    UnknownReplacementGroup,
    /// <summary>The replacement refers to a capture that did not participate.</summary>
    UnsetReplacementGroup,
    /// <summary>The match deadline expired.</summary>
    Timeout,
    /// <summary>The configured match-operation limit was exceeded.</summary>
    MatchLimit,
    /// <summary>The configured backtracking-depth limit was exceeded.</summary>
    DepthLimit,
    /// <summary>The configured match-workspace heap limit was exceeded.</summary>
    HeapLimit,
}
