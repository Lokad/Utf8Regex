namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal enum RegexParseError
{
    InvalidPattern = 0,
    NotEnoughParentheses = 1,
    InvalidGroupingConstruct = 2,
    UndefinedNumberedReference = 3,
    UndefinedNamedReference = 4,
    InsufficientOrInvalidHexDigits = 5,
    QuantifierAfterNothing = 6,
    UnrecognizedEscape = 7,
    ReversedCharacterRange = 8,
    UnterminatedBracket = 9,
    ExclusionGroupNotLast = 10,
}
