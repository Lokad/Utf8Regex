namespace Lokad.Utf8Regex.Internal.FrontEnd;

/// <summary>Owned syntax failures use offsets in the string pattern's UTF-16
/// representation. These diagnostic coordinates do not affect scalar matching.</summary>
internal readonly record struct Utf8PatternDiagnostic(Utf8PatternError Error, int Offset)
{
    internal bool IsSyntaxError => Error != Utf8PatternError.None;

    internal string Description => Error switch
    {
        Utf8PatternError.InvalidSyntax => "Invalid regex syntax",
        Utf8PatternError.UnclosedGroup => "A group is missing its closing parenthesis",
        Utf8PatternError.UnclosedClass => "A character class is missing its closing bracket",
        Utf8PatternError.UnmatchedClosingParenthesis => "A closing parenthesis has no matching group",
        Utf8PatternError.InvalidEscape => "Invalid or incomplete escape",
        Utf8PatternError.InvalidProperty => "Invalid or unknown Unicode property",
        Utf8PatternError.ReversedRange => "Character-class endpoints must be in ascending Unicode scalar order",
        Utf8PatternError.InvalidQuantifier => "Invalid, overflowing or reversed quantifier bounds",
        Utf8PatternError.MissingQuantifierOperand => "A quantifier requires an atom and cannot be repeated",
        Utf8PatternError.InvalidCaptureName => "Invalid capture name or number",
        Utf8PatternError.UndefinedCapture => "A capture reference has no matching declaration",
        Utf8PatternError.InvalidConditional => "Invalid conditional syntax or branch count",
        _ => throw new InvalidOperationException("No pattern failure was recorded."),
    };

    internal ArgumentException ToException()
    {
        if (!IsSyntaxError) throw new InvalidOperationException("Only syntax failures become pattern argument errors.");
        return new ArgumentException($"{Description} at UTF-16 pattern offset {Offset}.", "pattern");
    }
}

internal enum Utf8PatternError
{
    None,
    InvalidSyntax,
    UnclosedGroup,
    UnclosedClass,
    UnmatchedClosingParenthesis,
    InvalidEscape,
    InvalidProperty,
    ReversedRange,
    InvalidQuantifier,
    MissingQuantifierOperand,
    InvalidCaptureName,
    UndefinedCapture,
    InvalidConditional,
}
