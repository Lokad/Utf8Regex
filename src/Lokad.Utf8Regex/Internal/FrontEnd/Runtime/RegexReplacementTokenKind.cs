namespace Lokad.Utf8Regex.Internal.FrontEnd.Runtime;

internal enum RegexReplacementTokenKind : byte
{
    Literal,
    Group,
    WholeMatch,
    LeftPortion,
    RightPortion,
    LastGroup,
    WholeString,
}
